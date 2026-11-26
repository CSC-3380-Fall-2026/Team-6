using Godot;

// frog hops around near where it was placed
public partial class Frog : AnimatedSprite2D
{
    [Export] private Polygon2D _waterShape; // so frog doesn't land in water

    [Export] private StringName _hopAnimation = "hop";
    [Export] private bool _drawnFacingRight = true; // which way frog faces in frames

    [ExportGroup("Hoppng")]
    [Export] private float _minWait = 2f; // shortest time between hops
    [Export] private float _maxWait = 6f; // longest time between hops
    [Export] private float _minHopDistance = 40f;
    [Export] private float _maxHopDistance = 100f;
    [Export] private float _wanderRadius = 250f; // how far from its starting spot it can end up

    private const int RestFrame = 0;
    private const int MaxSpotTries = 12; // tries to find dry spot before skipping this hop

    private Vector2 _home; // where it was placed in screen
    private Timer _waitTimer;

    public override void _Ready()
    {
        _home = Position;
        Animation = _hopAnimation;
        Stop();
        Frame = RestFrame;

        // Timer node pauses with game and goes away with frog
        _waitTimer = new Timer { OneShot = true };
        AddChild(_waitTimer);
        _waitTimer.Timeout += Hop;
        AnimationFinished += OnHopFinished;

        WaitThenHop();
    }

    private void WaitThenHop()
    {
        _waitTimer.Start(GD.RandRange(_minWait, _maxWait));
    }

    private void Hop()
    {
        if (!TryPickLandingSpot(out Vector2 landing))
        {
            WaitThenHop(); // no where dry nearby this time, try again later
            return;
        }

        // face the way it's jumping
        bool goingRight = landing.X > Position.X;
        FlipH = goingRight != _drawnFacingRight;

        Play(_hopAnimation);

        // slid to landing spot over exactly as long as animation lasts
        Tween jump = CreateTween();
        jump.TweenProperty(this, "position", landing, HopDuration());
    }

    private void OnHopFinished()
    {
        Stop();
        Frame = RestFrame;
        WaitThenHop();
    }

    // random spot a short hop away that's on land near home
    private bool TryPickLandingSpot(out Vector2 landing)
    {
        for (int i = 0; i < MaxSpotTries; i++)
        {
            float angle = (float)GD.RandRange(0.0, Mathf.Tau);
            float distance = (float)GD.RandRange(_minHopDistance, _maxHopDistance);
            Vector2 spot = Position + Vector2.FromAngle(angle) * distance;

            if (spot.DistanceTo(_home) <= _wanderRadius && !IsInWater(spot))
            {
                landing = spot;
                return true;
            }
        }
        landing = Position;
        return false;
    }

    private bool IsInWater(Vector2 localSpot)
    {
        if (_waterShape == null)
        {
            return false;
        }
        // frog's position is relative to its parent
        Vector2 global = GetParent<CanvasItem>().GetGlobalTransform() * localSpot;
        return Geometry2D.IsPointInPolygon(_waterShape.ToLocal(global), _waterShape.Polygon);
    }

    // how long one play of hop animation takes
    private float HopDuration()
    {
        SpriteFrames frames = SpriteFrames;
        float speed = (float)frames.GetAnimationSpeed(_hopAnimation);
        float totalFrameTime = 0f;
        for (int i = 0; i < frames.GetFrameCount(_hopAnimation); i++)
        {
            totalFrameTime += (float)frames.GetFrameDuration(_hopAnimation, i);
        }
        return totalFrameTime / Mathf.Max(speed, Mathf.Epsilon);
    }
}