using Godot;

// Placeholder screen each minigame opens until the real games exist.
// Shows which game was picked so each button can be checked.
public partial class TestLevel : Control
{
	// Assigned in the Inspector so the nodes can be moved or renamed in the scene
	[Export] private Label gameMarker;
	[Export] private Button backButton;

	public override void _Ready()
	{
		if (gameMarker == null || backButton == null) {
			GD.PushError("TestLevel: assign GameMarker and BackButton in the Inspector");
			return;
		}

		string game = SceneManager.Instance.SelectedGame;
		if (game != "") {
			gameMarker.Text = game;
		}

		backButton.Pressed += GoBack;
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
