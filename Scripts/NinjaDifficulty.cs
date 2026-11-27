using Godot;

// Ramps Fish Ninja up as the score climbs. A "Ramp up!" banner shows when the difficulty changes.
public partial class NinjaDifficulty : Node
{
	// Banner animation
	private const float BannerStartScale = 0.1f;
	private const float BannerPopScale = 1.3f;
	private const float BannerPopTime = 0.25f;
	private const float BannerFadeTime = 0.3f;

	// Assigned in the Inspector
	[Export] private FishLauncher launcher;
	[Export] private GameHud hud;
	[Export] private Label banner;

	[ExportGroup("Ramp")]
	// Points needed for each ramp
	[Export] private int pointsPerRamp = 100;
	// Ramps before the difficulty stops climbing
	[Export] private int maxRamp = 10;
	// Values at the start of a run and at the last ramp. Each ramp moves an equal step from one to the other.
	[Export] private float startSpeed = 1f;
	[Export] private float maxSpeed = 2f;
	[Export] private int startWaveSize = 3;
	[Export] private int maxWaveSize = 7;
	// Share of thrown fish that are do-not-cut, from 0 to 1
	[Export] private float startDoNotCutShare = 0.2f;
	[Export] private float maxDoNotCutShare = 0.45f;

	[ExportGroup("Banner")]
	[Export] private string rampMessage = "Ramp up!";
	// Seconds the banner is on screen. The game keeps going under it.
	[Export] private float bannerDuration = 1.5f;

	[ExportGroup("Testing")]
	// Points K adds in debug builds
	[Export] private int debugPoints = 50;

	private Tween bannerTween;

	// 0 at the start of a run, MaxRamp at the hardest
	public int Ramp { get; private set; }
	public int MaxRamp => maxRamp;
	// Checks the Inspector slots and settings, then sets the launcher to the starting difficulty
	public override void _Ready()
	{
		if (launcher == null || hud == null || banner == null) {
			GD.PushError("NinjaDifficulty: assign the launcher, HUD and banner in the Inspector");
			SetProcess(false);
			SetProcessUnhandledInput(false);
			return;
		}
		if (pointsPerRamp < 1 || maxRamp < 1) {
			GD.PushError("NinjaDifficulty: Points Per Ramp and Max Ramp need to be 1 or more");
			SetProcess(false);
			SetProcessUnhandledInput(false);
			return;
		}

		banner.Visible = false;
		ApplyRamp();
	}
	// Ramps up when the score reaches the next ramp's points
	public override void _Process(double delta)
	{
		RampUpTo(Mathf.Min(hud.GetScore() / pointsPerRamp, maxRamp));
	}

	// K adds points and L jumps straight to the last ramp in debug builds.
	public override void _UnhandledInput(InputEvent @event)
	{
		if (!OS.IsDebugBuild()) {
			return;
		}
		if (@event is InputEventKey key && key.Pressed && !key.Echo) {
			if (key.Keycode == Key.K) {
				hud.AddScore(debugPoints);
			} else if (key.Keycode == Key.L) {
				RampUpTo(maxRamp);
			}
		}
	}

	// Moves up to a higher ramp. Check prevents going down
	private void RampUpTo(int ramp)
	{
		if (ramp <= Ramp) {
			return;
		}
		Ramp = ramp;
		ApplyRamp();
		ShowBanner();
	}

	// Sets the launcher's speed, wave size, and do-not-cut share for the current ramp
	private void ApplyRamp()
	{
		float progress = (float)Ramp / maxRamp;
		launcher.SpeedMultiplier = Mathf.Lerp(startSpeed, maxSpeed, progress);
		launcher.WaveSize = Mathf.RoundToInt(Mathf.Lerp(startWaveSize, maxWaveSize, progress));
		launcher.DoNotCutShare = Mathf.Lerp(startDoNotCutShare, maxDoNotCutShare, progress);
	}

	private void ShowBanner()
	{
		// A ramp during the last banner starts it over
		bannerTween?.Kill();

		banner.Text = rampMessage;
		banner.Visible = true;
		banner.Modulate = Colors.White;
		banner.PivotOffset = banner.Size / 2f;
		banner.Scale = Vector2.One * BannerStartScale;

		float holdTime = Mathf.Max(0f, bannerDuration - 2f * BannerPopTime - BannerFadeTime);

		bannerTween = CreateTween();
		bannerTween.TweenProperty(banner, "scale", Vector2.One * BannerPopScale, BannerPopTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		bannerTween.TweenProperty(banner, "scale", Vector2.One, BannerPopTime);
		bannerTween.TweenInterval(holdTime);
		bannerTween.TweenProperty(banner, "modulate:a", 0f, BannerFadeTime);
		bannerTween.TweenCallback(Callable.From(() => { banner.Visible = false; }));
	}
}
