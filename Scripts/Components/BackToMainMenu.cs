using Godot;

// Add this node to a screen so Esc goes back to the title screen.
// A stopgap for screens without a Back button, until the pause menu's Back to Home button is hooked up.
public partial class BackToMainMenu : Node
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("back")) {
			GetViewport().SetInputAsHandled();
			SceneManager.Instance.ChangeScene(SceneManager.MainMenu);
		}
	}
}
