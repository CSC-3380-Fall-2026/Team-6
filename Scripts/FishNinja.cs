using Godot;

// The Fish Ninja minigame: the background and the FishLauncher throwing waves of fish.
// Cutting, score and lives come in later tasks.
// Esc goes back to the title screen until the pause menu's Back to Home button is hooked up.
public partial class FishNinja : Control
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("back")) {
			GetViewport().SetInputAsHandled();
			SceneManager.Instance.ChangeScene(SceneManager.MainMenu);
		}
	}
}
