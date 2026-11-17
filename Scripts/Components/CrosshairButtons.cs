using Godot;
using System.Collections.Generic;

// Lets the Wii Remote press menu buttons. Add this node to a screen, and pressing A (or left click, or E)
// presses whichever button the crosshair is over. Hook up each button's Pressed signal like normal.
// Godot buttons only react to the real mouse, which the Wii Remote doesn't move, so this takes over
// pressing every button under this node's parent. It also plays the menu hover and click sounds.
public partial class CrosshairButtons : Node
{
	private readonly List<BaseButton> buttons = new List<BaseButton>();
	private WiimoteManager wiimote;
	private BaseButton hoveredButton;
	private ulong lastPressFrame = ulong.MaxValue;

	public override void _Ready()
	{
		foreach (Node node in GetParent().FindChildren("*", "BaseButton", true, false)) {
			BaseButton button = (BaseButton)node;
			// Clicks come through here instead, otherwise a mouse click would press the button twice
			button.ButtonMask = 0;
			buttons.Add(button);
		}

		wiimote = GetTree().Root.GetNodeOrNull<WiimoteManager>("WiimoteManager");
		if (wiimote != null) {
			wiimote.OnWiimoteAClicked += OnAPressed;
		}

		// No hover sound for a button the crosshair is already on when the screen opens
		hoveredButton = ButtonUnderCrosshair();
	}

	public override void _ExitTree()
	{
		if (wiimote != null) {
			wiimote.OnWiimoteAClicked -= OnAPressed;
		}
	}

	public override void _Process(double delta)
	{
		BaseButton hovered = ButtonUnderCrosshair();
		if (hovered != hoveredButton) {
			hoveredButton = hovered;
			if (hovered != null) {
				AudioManager.Instance?.PlaySFX(AudioManager.MenuHover);
			}
		}

		// Left click and E
		if (Input.IsActionJustPressed("cursor_primary")) {
			OnAPressed();
		}
	}

	private void OnAPressed()
	{
		// The remote's A still arrives while the game is paused, so ignore it then (the pause menu is on top)
		if (!CanProcess()) {
			return;
		}
		// A left click counts as both cursor_primary and the Wiimote's A, so only take one press per frame
		ulong frame = Engine.GetProcessFrames();
		if (frame == lastPressFrame) {
			return;
		}
		lastPressFrame = frame;

		BaseButton button = ButtonUnderCrosshair();
		if (button != null) {
			AudioManager.Instance?.PlaySFX(AudioManager.MenuClick);
			button.EmitSignal(BaseButton.SignalName.Pressed);
		}
	}

	// The button the crosshair is over, or null if it isn't over one
	private BaseButton ButtonUnderCrosshair()
	{
		Vector2 crosshair = GetCrosshairPosition();
		// Buttons later in the scene are drawn on top, so check those first
		for (int i = buttons.Count - 1; i >= 0; i--) {
			BaseButton button = buttons[i];
			if (IsInstanceValid(button) && button.IsVisibleInTree() && !button.Disabled && button.GetGlobalRect().HasPoint(crosshair)) {
				return button;
			}
		}
		return null;
	}

	// Where the crosshair is drawn, or the mouse if the Cursor autoload is missing
	private Vector2 GetCrosshairPosition()
	{
		CursorNode cursor = GetTree().Root.GetNodeOrNull<CursorNode>("Cursor/CursorNode");
		if (cursor != null) {
			return cursor.ScreenPosition;
		}
		return GetViewport().GetMousePosition();
	}
}
