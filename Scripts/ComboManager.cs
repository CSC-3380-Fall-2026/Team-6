using Godot;

public partial class ComboManager : Node
{
    // fired whenever streak changes so audio and feedback can react
    [Signal] public delegate void ComboChangedEventHandler(int streak, int multiplier);
    // fired  when a streak is lost
    // lostStreak is how long the streak was
    [Signal] public delegate void StreakResetEventHandler(int lostStreak);

    // hook these up in inspector
    [Export] private FishLauncher _fishLauncher; // tells us when fish falls off-screen uncut
    [Export] private Label _streakLabel; // on the HUD: "Streak 12 · x2"
    [Export] private Label _comboLostLabel; // short "Combo Lost" popup

    [ExportGroup("Streak")]
    // streak needed for each multiplier step: x2, x3, x4
    [Export] private int[] _multiplierThresholds = { 10, 25, 50 };

    [ExportGroup("Swipe Combo")]
    [Export] private int _swipeComboMinFish = 3; // fish needed in one swipe to count as combo
    [Export] private int _bonusPerExtraFish = 10; // bonus points for each fish after first in combo swipe
    [Export] private string _comboMessage = "{0}x Combo!"; // {0} replacd by number of fish
    [Export] private int _comboFontSize = 48;
    [Export] private Color _comboColor = new Color(1f, 0.85f, 0.3f);
    [Export] private float _comboRiseDistance = 60f; // pixels the combo tezt floats up
    [Export] private float _comboPopupTime = 0.8f; // seconds before combo tezt is gone

    [ExportGroup("Combo Lost")]
    [Export] private float _comboLostTime = 0.6f; // seconds the "Combo Lost" popup takes to fad

    [ExportGroup("Testing")]
    // TESTING ONLY: base points for each fake cu, until slice detection cuts real fish
    [Export] private int _testFishPoints = 10;

    private const int BaseMultiplier = 1;
    private const int NoStreak = 0;
    private const int NoPoints = 0;
    private const int FirstFish = 1; // swipe bonus counts the fish after he first one
    private const float Invisible = 0f;
    private const float Opaque = 1f;
    private const int ComboOutlineSize = 10;
    private const int PopupZIndex = 100; // draw combo popups on top of fish

    public int Streak { get; private set; } = NoStreak;
    public int BestStreak { get; private set; } = NoStreak; // longest streak this run, for results screen
    public int Multiplier { get; private set; } = BaseMultiplier;

    private Tween _comboLostTween;

    public override void _Ready()
    {
        if (_fishLauncher != null)
        {
            _fishLauncher.FishMissed += OnFishMissed;
        }
        else
        {
            GD.PushWarning("ComboManager: assign the Fish Launcher in the Inspector, or missed fish won't end the streak");
        }

        if (_comboLostLabel != null)
        {
            _comboLostLabel.Visible = false;
        }
        UpdateStreakLabel();
    }

    public override void _ExitTree()
    {
        if (_fishLauncher != null)
        {
            _fishLauncher.FishMissed -= OnFishMissed;
        }
    }

    // call for every fish that gets cut
    // returns what the cut is worth with streak multiplier or 0 for "do not cut" fish (which also ends streak)
    public int FishCut(NinjaFish fish)
    {
        return RegisterCut(fish.ShouldCut, fish.Data.Points);
    }

    // call when a fast swipe ends
    // fishCut is how many fish that one swipe cut
    // endPoint is where trail stopped
    public void SwipeEnded(int fishCut, Vector2 endPoint)
    {
        if (fishCut < _swipeComboMinFish)
        {
            return;
        }

        int bonus = (fishCut - FirstFish) * _bonusPerExtraFish;
        AwardPoints(bonus);
        ShowComboPopup(string.Format(_comboMessage, fishCut), endPoint);
    }

    // STREAK
    private int RegisterCut(bool shouldCut, int basePoints)
    {
        if (!shouldCut)
        {
            LoseStreak();
            return NoPoints;
        }

        Streak++;
        BestStreak = Mathf.Max(BestStreak, Streak);
        UpdateMultiplier();
        UpdateStreakLabel();
        EmitSignal(SignalName.ComboChanged, Streak, Multiplier);
        return basePoints * Multiplier;
    }

    private void OnFishMissed(FishData fish)
    {
        LoseStreak();
    }

    private void LoseStreak()
    {
        // nothing to lost, so no flash
        if (Streak == NoStreak)
        {
            return;
        }

        int lostStreak = Streak;
        Streak = NoStreak;
        Multiplier = BaseMultiplier;
        UpdateStreakLabel();
        FlashComboLost();
        EmitSignal(SignalName.StreakReset, lostStreak);
        EmitSignal(SignalName.ComboChanged, Streak, Multiplier);
    }

    // x1, plus 1 for every threshold the streak has reached
    private void UpdateMultiplier()
    {
        Multiplier = BaseMultiplier;
        foreach (int threshold in _multiplierThresholds)
        {
            if (Streak >= threshold)
            {
                Multiplier++;
            }
        }
    }

    // SCORE
    private void AwardPoints(int points)
    {
        // TODO: call FishNinjaGame.AddScore(points) once Fish Ninja has a score
        // all score changes should go through it
        GD.Print($"ComboManager: +{points} combo bonus (not added to the score yet: Fish Ninja has no AddScore)");
    }

    // FEEDBACK
    private void UpdateStreakLabel()
    {
        if (_streakLabel == null)
        {
            return;
        }
        _streakLabel.Text = $"Streak {Streak} · x{Multiplier}";
    }

    private void FlashComboLost()
    {
        if (_comboLostLabel == null)
        {
            return;
        }

        _comboLostTween?.Kill();
        _comboLostLabel.Visible = true;
        _comboLostLabel.Modulate = new Color(Colors.White, Opaque);

        _comboLostTween = CreateTween();
        _comboLostTween.TweenProperty(_comboLostLabel, "modulate:a", Invisible, _comboLostTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _comboLostTween.TweenCallback(Callable.From(() => { _comboLostLabel.Visible = false; }));
    }

    // text that appears centered on a point
    // floats up and fades out
    private void ShowComboPopup(string message, Vector2 at)
    {
        Label popup = new Label
        {
            Text = message,
            ZIndex = PopupZIndex,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings
            {
                FontSize = _comboFontSize,
                FontColor = _comboColor,
                OutlineSize = ComboOutlineSize,
                OutlineColor = Colors.Black,
            },
        };
        GetParent().AddChild(popup);
        popup.ResetSize(); // shrink to fit text so it can be centered
        popup.GlobalPosition = at - popup.Size / 2f;

        Tween tween = popup.CreateTween().SetParallel();
        tween.TweenProperty(popup, "position:y", popup.Position.Y - _comboRiseDistance, _comboPopupTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(popup, "modulate:a", Invisible, _comboPopupTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Chain().TweenCallback(Callable.From(() => popup.QueueFree()));
    }

    // TESTING
    // TESTING ONLY: fakes cuts and swipes until slice detection is done
    // only works when running from the editor
    // C - cut fish
    // X - cut a "do not cut" fish
    // 3 / 4 / 5 = one swipe that cut that many fish, ending where the mouse is
    // can be deleted once slice detection calls FishCut and SwipeEnded
    // (also delete _testFishPoints and "Testing" export group at top of file)
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild() || GetTree().Paused)
        {
            return;
        }
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
        {
            return;
        }

        Vector2 mouse = GetViewport().GetMousePosition();
        switch (key.Keycode)
        {
            case Key.C:
                GD.Print($"ComboManager test: cut worth {RegisterCut(true, _testFishPoints)} points ");
                break;
            case Key.X:
                RegisterCut(false, _testFishPoints);
                break;
            case Key.Key3:
                SwipeEnded(3, mouse);
                break;
            case Key.Key4:
                SwipeEnded(4, mouse);
                break;
            case Key.Key5:
                SwipeEnded(5, mouse);
                break;
        }
    }
}