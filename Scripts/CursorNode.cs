using Godot;

/// <summary>
/// On-screen crosshair that stands in for the Wiimote IR pointer.
/// Until the Wiimote integration is done it follows the mouse, can be nudged with
/// the arrow keys / WASD, and turns the cursor_* input actions into signals the
/// minigames can listen to.
/// </summary>
public partial class CursorNode : Node2D
{
	[Signal] public delegate void PrimaryPressedEventHandler(Vector2 screenPosition);
	[Signal] public delegate void PrimaryReleasedEventHandler(Vector2 screenPosition);
	[Signal] public delegate void SecondaryPressedEventHandler(Vector2 screenPosition);
	[Signal] public delegate void RecalibratedEventHandler();

	/// <summary>How fast the keyboard moves the cursor, in pixels per second.</summary>
	[Export] public float KeyboardSpeed = 600f;

	/// <summary>Hide the OS mouse pointer so only the crosshair is visible.</summary>
	[Export] public bool HideSystemCursor = true;

	/// <summary>Tint applied to the crosshair while the primary button is held.</summary>
	[Export] public Color PressedTint = new Color(1f, 0.8f, 0.2f);

	private Vector2 _screenPosition;

	/// <summary>
	/// Cursor position in viewport (screen) pixels, clamped to the visible area.
	/// The Wiimote code can set this directly once it's ready.
	/// </summary>
	public Vector2 ScreenPosition
	{
		get => _screenPosition;
		set => _screenPosition = value.Clamp(Vector2.Zero, GetViewportRect().Size);
	}

	public bool IsPrimaryHeld { get; private set; }

	public override void _Ready()
	{
		if (HideSystemCursor)
			Input.MouseMode = Input.MouseModeEnum.Hidden;

		ScreenPosition = GetViewport().GetMousePosition();
		UpdateGlobalPosition();
	}

	public override void _ExitTree()
	{
		if (HideSystemCursor)
			Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion)
			ScreenPosition = motion.Position;
		else if (@event.IsActionPressed("cursor_primary"))
			SetPrimaryHeld(true);
		else if (@event.IsActionReleased("cursor_primary"))
			SetPrimaryHeld(false);
		else if (@event.IsActionPressed("cursor_secondary"))
			EmitSignal(SignalName.SecondaryPressed, ScreenPosition);
		else if (@event.IsActionPressed("cursor_recalibrate"))
			Recalibrate();
	}

	public override void _Process(double delta)
	{
		Vector2 direction = Input.GetVector("cursor_left", "cursor_right", "cursor_up", "cursor_down");
		if (direction != Vector2.Zero)
			ScreenPosition += direction * KeyboardSpeed * (float)delta;

		UpdateGlobalPosition();
	}

	/// <summary>
	/// Snaps the cursor back to the middle of the screen. With a mouse this just recenters;
	/// with the Wiimote it will be where the "point at the center" calibration step happens.
	/// </summary>
	public void Recalibrate()
	{
		ScreenPosition = GetViewportRect().GetCenter();
		GetViewport().WarpMouse(ScreenPosition);
		EmitSignal(SignalName.Recalibrated);
	}

	private void SetPrimaryHeld(bool held)
	{
		IsPrimaryHeld = held;
		Modulate = held ? PressedTint : Colors.White;
		EmitSignal(held ? SignalName.PrimaryPressed : SignalName.PrimaryReleased, ScreenPosition);
	}

	private void UpdateGlobalPosition()
	{
		// Map screen pixels into whatever canvas we live in, so a Camera2D in a game
		// scene doesn't drag the crosshair away from where the player is pointing.
		GlobalPosition = GetCanvasTransform().AffineInverse() * ScreenPosition;
	}
}
