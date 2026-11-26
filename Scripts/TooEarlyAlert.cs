using Godot;

// The "Too Early!" that pops up over the bobber when the player pulls while a fish is only nibbling.
// BiteController places it on the bobber. It pops in, floats up, fades out and removes itself.
public partial class TooEarlyAlert : Node2D
{
	// Assigned in the Inspector
	[Export] private Node2D mark;

	// Pixels it floats up and seconds it stays on screen
	[Export] private float riseHeight = 50f;
	[Export] private float lifetime = 1.2f;

	// The text grows past full size, then settles back, and fades out at the end
	private const float PopScale = 1.3f;
	private const float PopTime = 0.12f;
	private const float SettleTime = 0.1f;
	private const float FadeTime = 0.35f;

	public override void _Ready()
	{
		if (mark == null) {
			GD.PushError("TooEarlyAlert: assign the mark in the Inspector");
			QueueFree();
			return;
		}

		mark.Scale = Vector2.Zero;
		Tween pop = CreateTween();
		pop.TweenProperty(mark, "scale", Vector2.One * PopScale, PopTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		pop.TweenProperty(mark, "scale", Vector2.One, SettleTime);

		Tween drift = CreateTween();
		drift.TweenProperty(mark, "position", mark.Position + Vector2.Up * riseHeight, lifetime).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

		Tween fade = CreateTween();
		fade.TweenInterval(Mathf.Max(0f, lifetime - FadeTime));
		fade.TweenProperty(this, "modulate:a", 0f, FadeTime);
		fade.TweenCallback(Callable.From(QueueFree));
	}
}
