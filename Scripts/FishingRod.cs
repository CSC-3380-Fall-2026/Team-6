using Godot;
using System;

public partial class FishingRod : Node2D
{
    public enum RodState { Idle, Casting, InWater, Reeling }

    // signals other scripts can listen to
    [Signal] public delegate void StateChangedEventHandler(int newState);
    [Signal] public delegate void CastLandedEventHandler(Vector2 bobberPosition);
    [Signal] public delegate void CastFailedEventHandler(Vector2 aimPosition);
    [Signal] public delegate void ReelFinishedEventHandler();

    [Export] private Polygon2D _waterShape;
    [Export] private CursorNode _cursorNode;

    // defaults loaded automatically
    [Export] private Texture2D[] _rodTextures;
    [Export] private Texture2D _bobberTexture;

    [ExportGroup("Look")]
    [Export] private float _rodScale = 12f; 
    [Export] private float _bobberScale = 4f;
    [Export] private Color _lineColor = new Color(0.95f, 0.95f, 0.95f, 0.9f);
    [Export] private float _lineWidth = 2f;

    [ExportGroup("Timing")]
    [Export] private float _flightSpeed = 1400f; // pixels per second while bobber flies
    [Export] private float _reelSpeed = 900f; // pixels per second while reeling in

    [ExportGroup("Bobbing")]
    [Export] private float _bobSpeed = 3f; // how fast bobber floats up and down
    [Export] private float _bobHeight = 2f; // how far bobber floats up & down

    [ExportGroup("Splash")]
    [Export] private int _splashRingCount = 2; // number of ripple rings per splash
    [Export] private float _splashRingWidth = 3f; //thickness of each ring's line
    [Export] private Color _splashColor = new Color(1f, 1f, 1f, 0.85f);
    [Export] private float _splashRadius = 30f; // ring size in pixels before scaling
    [Export] private Vector2 _splashStartScale = new Vector2(0.2f, 0.1f); // rings start small
    [Export] private Vector2 _splashEndScale = new Vector2(1.6f, 1.6f); // rings grow to this size
    [Export] private float _splashDuration = 0.6f; // seconds for ring to grow and fade out
    [Export] private float _splashRingDelay = 0.12f; // seconds between each ring starting

    public RodState State { get; private set; } = RodState.Idle;

    private static readonly string[] DefaultRodPaths =
    {
        "res://Assets/Rod/rod1.png",
        "res://Assets/Rod/rod2.png",
        "res://Assets/Rod/rod3.png",
        "res://Assets/Rod/rod4.png",
        "res://Assets/Rod/rod5.png",
    };
    private const string DefaultBobberPath = "res://Assets/Rod/bobber.png";

    private const int SplashRingPoints = 24; // how many points make up each ring (more = smoother circle)
    private const float SplashSquash = 0.5f; // flattens rings into ovals so they look top-down

    // pixel positions inside each rod sprite found automatically in FindTipAndHandle()
    private Vector2[] _handlePixels; // where player holds rod
    private Vector2[] _tipPixels; // where line comes out

    private Sprite2D _rodSprite;
    private Sprite2D _bobber;
    private Line2D _line;
    private int _column = 2; // starts pointing straight ahead
    private Vector2 _landedPosition; // where bobber sits in water
    private float _bobTime;
    private Tween _activeTween;

    public override void _Ready()
    {
        LoadDefaultTexturesIfMissing();
        FindTipAndHandle();
        BuildNodes();
        SetColumn(2);

        if (_cursorNode != null)
        {
            _cursorNode.PrimaryPressed += OnPrimaryPressed;
            _cursorNode.SecondaryPressed += OnSecondaryPressed;
        }
        else
        {
            GD.PushWarning("FishingRod: no CursorNode assigned in the Inspector, casting won't work.");
        }
    }

    public override void _ExitTree()
    {
        if (_cursorNode != null)
        {
            _cursorNode.PrimaryPressed -= OnPrimaryPressed;
            _cursorNode.SecondaryPressed -= OnSecondaryPressed;
        }
    }

    public override void _Process(double delta)
    {
        switch (State)
        {
            case RodState.Idle:
                // rod points at whichever pond columnt the cursor is over
                if (_cursorNode != null)
                {
                    SetColumn(ColumnFor(_cursorNode.GlobalPosition));
                }
                break;

            case RodState.InWater:
                // gentle float animation
                _bobTime += (float)delta;
                _bobber.GlobalPosition = _landedPosition + new Godot.Vector2(0, Mathf.Sin(_bobTime * _bobSpeed) * _bobHeight);
                break;
        }

        UpdateLine();
    }

    // INPUT
    private void OnPrimaryPressed(Vector2 _screenPosition)
    {
        // can only cast when line is fully reeled in
        if (State != RodState.Idle)
        {
            return;
        }

        Vector2 aim = _cursorNode.GlobalPosition;
        if (IsOverWater(aim))
        {
            StartCast(aim);
        }
        else
        {
            FailCast(aim);
        }
    }

    private void OnSecondaryPressed(Vector2 _screenPosition)
    {
        if (State == RodState.InWater)
        {
            StartReel();
        }
    }

    // CASTING
    private void StartCast(Vector2 target)
    {
        SetState(RodState.Casting);
        SetColumn(ColumnFor(target));
        LaunchBobber(target);
    }

    private void LaunchBobber(Vector2 target)
    {
        Vector2 start = GetTipGlobalPosition();
        float distance = start.DistanceTo(target);
        float duration = Mathf.Max(0.25f, distance / _flightSpeed);

        _bobber.GlobalPosition = start;
        _bobber.Visible = true;

        // move bobber from tip to target over "duration" seconds
        Tween flight = CreateTween();
        flight.TweenMethod(Callable.From<float>(t =>
        {
            _bobber.GlobalPosition = start.Lerp(target,t);
        }), 0f, 1f, duration);
        flight.TweenCallback(Callable.From(() => Land(target)));
    }

    private void Land(Vector2 target)
    {
        _landedPosition = target;
        _bobTime = 0f;
        _bobber.Scale = Vector2.One * _bobberScale;
        SpawnSplash(target);
        SetState(RodState.InWater);
        EmitSignal(SignalName.CastLanded, target);
    }

    private void FailCast(Vector2 aim)
    {
        // wiggle so players knows click registered but didn't work
        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.TweenProperty(_rodSprite, "rotation", 0.08f, 0.05f);
        _activeTween.TweenProperty(_rodSprite, "rotation", -0.08f, 0.05f);
        _activeTween.TweenProperty(_rodSprite, "rotation", 0.04f, 0.05f);
        _activeTween.TweenProperty(_rodSprite, "rotation", 0f, 0.05f);
        EmitSignal(SignalName.CastFailed, aim);
    }

    // REELING
    private void StartReel()
    {
        SetState(RodState.Reeling);

        Vector2 from = _bobber.GlobalPosition;
        float duration = Mathf.Max(0.2f, from.DistanceTo(GetTipGlobalPosition()) / _reelSpeed);

        Tween reel = CreateTween();
        reel.TweenMethod(Callable.From<float>(t =>
        {
            _bobber.GlobalPosition = from.Lerp(GetTipGlobalPosition(), t);
        }), 0f, 1f, duration).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
        reel.TweenCallback(Callable.From(() =>
        {
            _bobber.Visible = false;
            SetState(RodState.Idle);
            EmitSignal(SignalName.ReelFinished);
        }));
    }

    // HELPERS
    private void SetState(RodState newState)
    {
        State = newState;
        EmitSignal(SignalName.StateChanged, (int)newState);
    }

    private bool IsOverWater(Vector2 globalPoint)
    {
        if (_waterShape == null)
        {
            return false;
        }
        Vector2 local = _waterShape.ToLocal(globalPoint);
        return Geometry2D.IsPointInPolygon(local, _waterShape.Polygon);
    }

    // splits pond into columns to determine rod direction
    private int ColumnFor(Vector2 globalPoint)
    {
        if (_waterShape == null || _waterShape.Polygon.Length == 0)
        {
            return 2;
        }
        
        float minX = float.MaxValue;
        float maxX = float.MinValue;
        foreach (Vector2 p in _waterShape.Polygon)
        {
            float x = _waterShape.ToGlobal(p).X;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
        }

        int count = _rodTextures.Length;
        float percent = (globalPoint.X - minX) / (maxX - minX);
        return Mathf.Clamp((int)(percent * count), 0, count - 1);
    }

    private void SetColumn(int column)
    {
        _column = column;
        _rodSprite.Texture = _rodTextures[column];
        _rodSprite.Offset = -_handlePixels[column]; // shift picture so handle always sits on the node
    }

    private Vector2 GetTipGlobalPosition()
    {
        Vector2 tipLocal = _tipPixels[_column] - _handlePixels[_column];
        return _rodSprite.ToGlobal(tipLocal); // includes rod's scale and cast rotation
    }

    private void UpdateLine()
    {
        if (!_bobber.Visible)
        {
            _line.ClearPoints();
            return;
        }
        _line.Points = new[] { GetTipGlobalPosition(), _bobber.GlobalPosition };
    }

    private void SpawnSplash(Vector2 at)
    {
        // built from Line2D
        for (int r = 0; r < _splashRingCount; r++)
        {
            Line2D ring = new Line2D
            {
                TopLevel = true,
                Width = _splashRingWidth,
                DefaultColor = _splashColor,
                Closed = true,
                Scale = _splashStartScale,
            };
            for (int i = 0; i < SplashRingPoints; i++)
            {
                float a = i / (float)SplashRingPoints * Mathf.Tau;
                ring.AddPoint(new Godot.Vector2(Mathf.Cos(a), Mathf.Sin(a) * SplashSquash) * _splashRadius);
            }
            AddChild(ring);
            ring.GlobalPosition = at;

            float delay = r * _splashRingDelay;
            Tween t = ring.CreateTween().SetParallel();
            t.TweenProperty(ring, "scale", _splashEndScale, _splashDuration).SetDelay(delay).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            t.TweenProperty(ring, "modulate:a", 0f, _splashDuration).SetDelay(delay);
            t.Chain().TweenCallback(Callable.From(() => ring.QueueFree()));
        }
    }

    // finds handle and tip for each rod sprite
    private void FindTipAndHandle()
    {
        int count = _rodTextures.Length;
        _handlePixels = new Vector2[count];
        _tipPixels = new Vector2[count];

        for (int i = 0; i < count; i++)
        {
            Image img = _rodTextures[i].GetImage();
            if (img.IsCompressed()) 
            {
                img.Decompress();
            }
            int w = img.GetWidth();
            int h = img.GetHeight();

            int bottomY = -1;
            float sumX = 0f;
            int n = 0;
            for (int y = h - 1; y >= 0 && bottomY < 0; y--)
            {
                for (int x = 0; x < w; x++)
                {
                    if (img.GetPixel(x, y).A > 0.1f)
                    {
                        sumX += x;
                        n++;
                        bottomY = y;
                    }
                }
            }
            if (n == 0)
            {
                GD.PushWarning($"FishingRod: rod picture {i + 1} looks empty.");
                continue;
            }

            Vector2 handle = new Vector2(sumX / n + 0.5f, bottomY + 0.5f);

            Vector2 tip = handle;
            float best = -1f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (img.GetPixel(x, y).A <= 0.1f) 
                    {
                        continue;
                    }
                    Vector2 p = new Godot.Vector2(x + 0.5f, y + .5f);
                    float d = p.DistanceSquaredTo(handle);
                    if (d > best)
                    {
                        best = d;
                        tip = p;
                    }
                }
            }

            _handlePixels[i] = handle;
            _tipPixels[i] = tip;
        }
    }

    private void LoadDefaultTexturesIfMissing()
    {
        if (_rodTextures == null || _rodTextures.Length == 0)
        {
            _rodTextures = new Texture2D[DefaultRodPaths.Length];
            for (int i = 0; i < DefaultRodPaths.Length; i++)
            {
                _rodTextures[i] = GD.Load<Texture2D>(DefaultRodPaths[i]);
            }
        }
        _bobberTexture ??= GD.Load<Texture2D>(DefaultBobberPath);
    }

    private void BuildNodes()
    {
        _rodSprite = new Sprite2D
        {
            Name = "RodSprite",
            Centered = false,
            Scale = Vector2.One * _rodScale,
            TextureFilter = TextureFilterEnum.Nearest,
        };
        AddChild(_rodSprite);

        _line = new Line2D
        {
            Name = "FishingLine",
            TopLevel = true,
            Width = _lineWidth,
            DefaultColor = _lineColor,
        };
        AddChild(_line);

        _bobber = new Sprite2D
        {
            Name = "Bobber",
            TopLevel = true,
            Texture = _bobberTexture,
            Scale = Vector2.One * _bobberScale,
            TextureFilter = TextureFilterEnum.Nearest,
            Visible = false,
        };
        AddChild(_bobber);
    }
}