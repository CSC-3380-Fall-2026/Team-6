using Godot;

/// this is code for the crosshair that is here until we integrate the Wiimote.
/// can be nudged with the arrow keys and WASD. also reads the cursor when you press game on godot.

public partial class CursorNode : Node2D
{
	[Signal] public delegate void PrimaryPressedEventHandler(Vector2 screenPosition);
	[Signal] public delegate void PrimaryReleasedEventHandler(Vector2 screenPosition);
	[Signal] public delegate void SecondaryPressedEventHandler(Vector2 screenPosition);
	[Signal] public delegate void RecalibratedEventHandler();

	[Export] public float KeyboardSpeed = 600f;

	[Export] public bool HideSystemCursor = true;

	/// Tint applied to the cursor, can be removed if wanted by dev/gitmaster.
	[Export] public Color PressedTint = new Color(1f, 0.8f, 0.2f);

	private Vector2 _screenPosition;


	/// Wiimote code can set and use this directly once fully implemented.

	public Vector2 ScreenPosition
	{
		get => _screenPosition;
		set => _screenPosition = value.Clamp(Vector2.Zero, GetViewportRect().Size);
	}

	public bool IsPrimaryHeld { get; private set; }

	public override void _Ready()
	{
		if (HideSystemCursor) {
			Input.MouseMode = Input.MouseModeEnum.Hidden;
		}

		ScreenPosition = GetViewport().GetMousePosition();
		UpdateGlobalPosition();
	}

	public override void _ExitTree()
	{
		if (HideSystemCursor) {
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion) {
			ScreenPosition = motion.Position;
		} else if (@event.IsActionPressed("cursor_primary")) {
			SetPrimaryHeld(true);
		} else if (@event.IsActionReleased("cursor_primary")) {
			SetPrimaryHeld(false);
		} else if (@event.IsActionPressed("cursor_secondary")) {
			EmitSignal(SignalName.SecondaryPressed, ScreenPosition);
		} else if (@event.IsActionPressed("cursor_recalibrate")) {
			Recalibrate();
		}
	}

	public override void _Process(double delta)
	{
		Vector2 direction = Input.GetVector("cursor_left", "cursor_right", "cursor_up", "cursor_down");
		if (direction != Vector2.Zero) {
			ScreenPosition += direction * KeyboardSpeed * (float)delta;
		}

		UpdateGlobalPosition();
	}

	/// Snaps cursor back to the middle of the screen, while using a mouse this just recenters.
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
		// scene doesn't drag the crosshair away from where the player is pointing.

		GlobalPosition = GetCanvasTransform().AffineInverse() * ScreenPosition;
	}
}
