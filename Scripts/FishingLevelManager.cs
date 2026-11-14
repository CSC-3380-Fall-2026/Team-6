using Godot;
using System;

public partial class FishingLevelManager : Node
{
    [Signal] public delegate void LevelChangedEventHandler(int level);

    [Export] private GameHud _hud;
    [Export] private FishSpawner _fishSpawner;
    [Export] private Label _levelLabel; // shows points needed to reach new level
    [Export] private Label _banner; // shows new level
    [Export] private Label _statsLabel; // score and time

    [ExportGroup("Levels")]
    [Export] private int[] _scoreTargets = { 100, 250, 450, 700 }; // score needed to finish each level

    [ExportGroup("Banner")]
    [Export] private float _bannerDuration = 2f; // seconds the banner is on screen
    [Export] private float _bannerPopScale = 1.3f; // how big banner grows
    [Export] private float _bannerPopTime = 0.25f; // seconds to grow
    [Export] private float _bannerFadeTime = 0.3f; // seconds to fade out
    [Export] private string _levelUpMessage = "Level {0}!"; // {0} replaced by each level number
    [Export] private string _finishedMessage = "You mastered the pond!"; // message after level 4 completed
    [Export] private string _finishedStatsMessage = "Score: {0}     Time: {1}";
    [Export] private float _finishedBannerDuration = 4f; // final popup stays longer so player can read score

    [ExportGroup("Testing")]
    [Export] private int _debugPoints = 50;

    private const int FirstLevel = 1;
    private const float BannerStartScale = 0.1f; // banner starts tiny and pops up from middle
    private const float Invisible = 0f;
    private const int SecondsPerMinute = 60;

    public int CurrentLevel { get; private set; } = FirstLevel;
    public int LevelCount => _scoreTargets.Length;
    public int CurrentTarget => _scoreTargets[CurrentLevel - 1];

    private bool _changingLevel = false; //true while banner is up so target isn't reached twice
    private bool _runOver = false;
    private int _lastShownScore = -1; // only rewrite level label when score changes

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always; // keep running while game is paused

        if (_hud == null || _levelLabel == null || _banner == null)
        {
            GD.PushError("FishingLevelManager: assign the Hud, Level Label, and Banner in the Inspector");
            SetProcess(false);
            SetProcessUnhandledInput(false);
            return;
        }
        if (_scoreTargets.Length == 0)
        {
            GD.PushError("FishingLevelManager: add at least one score target in the Inspector");
            SetProcess(false);
            SetProcessUnhandledInput(false);
            return;
        }

        _banner.Visible = false;
        if (_statsLabel != null)
        {
            _statsLabel.Visible = false;
        }
        UpdateLevelLabel();
    }

    public override void _Process(double delta)
    {
        // pause menu and level-up banner both pause the game
        // only check score during play
        if (GetTree().Paused || _changingLevel || _runOver)
        {
            return;
        }

        int score = _hud.GetScore();
        if (score != _lastShownScore)
        {
            UpdateLevelLabel();
        }
        if (score >= CurrentTarget)
        {
            CompleteLevel();
        }
    }

    // TESTING ONLY: press K to add points so levels can be tested
    // can be deleted once catching works
    // (also delete _debugPoints and "Testing" export group at top of file)
    public override void _UnhandledInput(InputEvent @event)
    {
        if (!OS.IsDebugBuild() || GetTree().Paused)
        {
            return;
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.K)
        {
            _hud.AddScore(_debugPoints);
        }
    }

    // ends run and goes to results screen with score
    // called when last level is finished OR pause menu Back To Home button
    public void EndRun()
    {
        if (_runOver)
        {
            return;
        }
        _runOver = true;

        int score = _hud.GetScore();
        float timePlayed = (float)_hud.GetElapsedTime();
        GetTree().Paused = false;

        // saves run to high score list
        SaveManager.Instance.SubmitScore(SceneManager.GoneFishingId, score, timePlayed);
        SceneManager.Instance.ChangeScene(SceneManager.HighScoresScreen);
    }

    // LEVELS
    private void CompleteLevel()
    {
        _changingLevel = true;
        GetTree().Paused = true;

        bool lastLevel = CurrentLevel == LevelCount;
        if (lastLevel)
        {
            string stats = string.Format(_finishedStatsMessage, _hud.GetScore(), FormatTime(_hud.GetElapsedTime()));
            Tween banner = ShowBanner(_finishedMessage, _finishedBannerDuration);
            if (_statsLabel != null)
            {
                _statsLabel.Text = stats;
                _statsLabel.Visible = true;
            }
            banner.TweenCallback(Callable.From(() => EndRun()));
        }
        else
        {
            Tween banner = ShowBanner(string.Format(_levelUpMessage, CurrentLevel + 1), _bannerDuration);
            banner.TweenCallback(Callable.From(() => StartNextLevel()));
        }
    }

    private void StartNextLevel()
    {
        if (_runOver)
        {
            return;
        }

        CurrentLevel++;
        UpdateLevelLabel();
        GetTree().Paused = false;
        _changingLevel = false;

        EmitSignal("LevelChanged", CurrentLevel);
        ClearPond();
    }

    // every fish already in pond swims off & fades out
    private void ClearPond()
    {
        if (_fishSpawner == null)
        {
            return;
        }
        foreach (Node child in _fishSpawner.GetChildren())
        {
            if (child is Fish fish)
            {
                fish.Despawn();
            }
        }
    }

    private void UpdateLevelLabel()
    {
        int score = _hud.GetScore();
        _lastShownScore = score;
        _levelLabel.Text = $"Lv {CurrentLevel} · {score} / {CurrentTarget}";
    }

    // BANNER
    private Tween ShowBanner(string message, float duration)
    {
        _banner.Text = message;
        _banner.Visible = true;
        _banner.Modulate = Colors.White;
        _banner.PivotOffset = _banner.Size / 2f; // grow from middle
        _banner.Scale = Vector2.One * BannerStartScale;

        float holdTime = Mathf.Max(0f, duration - (2f * _bannerPopTime) - _bannerFadeTime);

        Tween tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process); // keep animating while game is paused
        tween.TweenProperty(_banner, "scale", Vector2.One * _bannerPopScale, _bannerPopTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(_banner, "scale", Vector2.One, _bannerPopTime);
        tween.TweenInterval(holdTime);
        tween.TweenProperty(_banner, "modulate:a", Invisible, _bannerFadeTime);
        tween.TweenCallback(Callable.From(() => { _banner.Visible = false; }));
        return tween;
    }

    private static string FormatTime(double seconds)
    {
        int wholeSeconds = (int)seconds;
        return $"{wholeSeconds / SecondsPerMinute}:{wholeSeconds % SecondsPerMinute:00}";
    }
}