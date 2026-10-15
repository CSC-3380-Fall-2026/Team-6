using Godot;

// Placeholder screen each minigame opens until the real games exist.
// Shows which game was picked so each button can be checked.
public partial class TestLevel : Control
{
	public override void _Ready()
	{
		string game = SceneManager.Instance.SelectedGame;
		if (game != "") {
			GetNode<Label>("GameMarker").Text = game;
		}

		GetNode<Button>("BackButton").Pressed += GoBack;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("ui_cancel")) {
			GetViewport().SetInputAsHandled();
			GoBack();
		}
	}

	// Returns to whichever screen opened this one
	private void GoBack()
	{
		SceneManager.Instance.ChangeScene(SceneManager.Instance.PreviousScene);
	}
}
