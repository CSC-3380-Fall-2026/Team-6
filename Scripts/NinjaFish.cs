using Godot;

// One fish thrown by the FishLauncher, built from a FishData entry (Fish Ninja sprite, points and DoNotCut flag).
// It flies its arc on its own and removes itself once it falls off the bottom of the screen.
// Fish that shouldn't be cut get an outline so they're easy to tell apart. This can be changed later for a more fitting thing
public partial class NinjaFish : Node2D
{
	// Fired just before the fish removes itself for falling off the bottom
	[Signal] public delegate void FellOffScreenEventHandler(NinjaFish fish);

	private const string OutlineShaderPath = "res://Shaders/ClickableOutline.gdshader";

	// Assigned in the Inspector
	[Export] private Sprite2D sprite;
	// How fish that shouldn't be cut are marked (red for now, can be changed when needed)
	[Export] private Color doNotCutOutlineColor = new Color(0.9f, 0.1f, 0.1f);
	[Export] private float doNotCutOutlineWidth = 1f;

	private Vector2 startPosition;
	private Vector2 startVelocity;
	private float gravity;
	private float spin;
	private float flightTime;
	private float bottomEdge;
	// Half the sprite's diagonal, so the fish is fully off screen before it's removed
	private float reach;

	public FishData Data { get; private set; }

	// True for fish the player is meant to cut
	public bool ShouldCut => Data != null && !Data.DoNotCut;

	public bool IsFalling => startVelocity.Y + gravity * flightTime > 0f;

	// Sets the fish up from its data and throws it. Call after adding it to the scene.
	// Gravity is in pixels per second squared and spin in radians per second.
	public void Launch(FishData data, Vector2 start, Vector2 velocity, float gravity, float spin)
	{
		if (sprite == null) {
			GD.PushError("NinjaFish: assign the sprite in the Inspector");
			QueueFree();
			return;
		}

		Data = data;
		startPosition = start;
		startVelocity = velocity;
		this.gravity = gravity;
		this.spin = spin;
		flightTime = 0f;
		Position = start;

		sprite.Texture = data.FishNinjaSprite;
		reach = sprite.Texture.GetSize().Length() * Mathf.Abs(sprite.Scale.X) / 2;
		bottomEdge = GetViewportRect().Size.Y;
		if (data.DoNotCut) {
			AddOutline();
		}
	}
	// Called every frame. Moves the fish along its arc and removes it once it falls off the bottom of the screen.
	public override void _Process(double delta)
	{
		if (Data == null) {
			return;
		}

		// Worked out from the launch instead of step by step, so the arc is the same at any frame rate
		flightTime += (float)delta;
		Position = startPosition + startVelocity * flightTime + new Vector2(0f, gravity * flightTime * flightTime / 2);
		Rotation = spin * flightTime;

		if (IsFalling && Position.Y - reach > bottomEdge) {
			EmitSignal(SignalName.FellOffScreen, this);
			QueueFree();
		}
	}
	// Adds a shader to the sprite that draws an outline around it, so fish that shouldn't be cut are easy to tell apart.
	private void AddOutline()
	{
		ShaderMaterial outline = new ShaderMaterial { Shader = GD.Load<Shader>(OutlineShaderPath) };
		outline.SetShaderParameter("enabled", true);
		outline.SetShaderParameter("outline_color", doNotCutOutlineColor);
		outline.SetShaderParameter("width", doNotCutOutlineWidth);
		sprite.Material = outline;
	}
}
