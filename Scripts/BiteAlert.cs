using Godot;

// The "!" that pops up over the bobber when a fish bites, with a splash of rings around the bobber.
// BiteController places it on the bobber and calls Dismiss once the bite is over.
public partial class BiteAlert : Node2D
{
	// Assigned in the Inspector
	[Export] private Node2D mark;

	[ExportGroup("Splash")]
	[Export] private int ringCount = 2;
	// Ring size in pixels before it grows, and its line thickness
	[Export] private float ringRadius = 30f;
	[Export] private float ringWidth = 3f;
	[Export] private Color ringColor = new Color(1f, 1f, 1f, 0.9f);
	// Seconds for a ring to grow and fade, and between rings starting
	[Export] private float ringDuration = 0.5f;
	[Export] private float ringDelay = 0.1f;

	// The "!" grows past full size, then settles back, then gently bobs while it waits
	private const float PopScale = 1.4f;
	private const float PopTime = 0.12f;
	private const float SettleTime = 0.1f;
	private const float BobHeight = 4f;
	private const float BobTime = 0.35f;
	private const float DismissTime = 0.15f;
	// Rings are squashed into ovals so they look flat on the water, and grow from small to big
	private const int RingPoints = 24;
	private const float RingSquash = 0.5f;
	private static readonly Vector2 RingStartScale = new Vector2(0.3f, 0.3f);
	private static readonly Vector2 RingEndScale = new Vector2(1.8f, 1.8f);

	// Pops the "!" in and splashes the water as soon as it appears
	public override void _Ready()
	{
		if (mark == null) {
			GD.PushError("BiteAlert: assign the mark in the Inspector");
			return;
		}

		mark.Scale = Vector2.Zero;
		Tween pop = CreateTween();
		pop.TweenProperty(mark, "scale", Vector2.One * PopScale, PopTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		pop.TweenProperty(mark, "scale", Vector2.One, SettleTime);

		Vector2 restPosition = mark.Position;
		Tween bob = CreateTween().SetLoops();
		bob.TweenProperty(mark, "position", restPosition + Vector2.Up * BobHeight, BobTime).SetTrans(Tween.TransitionType.Sine);
		bob.TweenProperty(mark, "position", restPosition, BobTime).SetTrans(Tween.TransitionType.Sine);

		Splash();
	}

	// Fades out and removes itself
	public void Dismiss()
	{
		Tween fade = CreateTween();
		fade.TweenProperty(this, "modulate:a", 0f, DismissTime);
		fade.TweenCallback(Callable.From(QueueFree));
	}

	// Rings that spread out from the bobber and fade, drawn under the "!"
	private void Splash()
	{
		for (int r = 0; r < ringCount; r++) {
			Line2D ring = new Line2D {
				Width = ringWidth,
				DefaultColor = ringColor,
				Closed = true,
				Scale = RingStartScale,
			};
			for (int i = 0; i < RingPoints; i++) {
				float angle = i / (float)RingPoints * Mathf.Tau;
				ring.AddPoint(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * RingSquash) * ringRadius);
			}
			AddChild(ring);
			MoveChild(ring, 0);

			Tween grow = ring.CreateTween().SetParallel();
			grow.TweenProperty(ring, "scale", RingEndScale, ringDuration).SetDelay(r * ringDelay).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
			grow.TweenProperty(ring, "modulate:a", 0f, ringDuration).SetDelay(r * ringDelay);
			grow.Chain().TweenCallback(Callable.From(ring.QueueFree));
		}
	}
}
