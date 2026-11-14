using Godot;

// Runs the pause menu (MenuOverlay): the pause key (P) opens and closes it, X and Esc close it,
// and Recalibrate Remote opens the calibration screen on top. Loaded as the PauseMenu autoload
// so one copy covers both minigames, but P only opens it while one of them is the current scene.
public partial class PauseMenu : Node
{
	// The scenes P opens the menu in. Menus like the title and high score screens have nothing to pause.
	private static readonly string[] GameScenes = { SceneManager.FishingLevel, SceneManager.FishNinja };

	// Assigned in the Inspector
	[Export] private CanvasLayer menuLayer;
	[Export] private Button recalibrateButton;
	[Export] private Button closeButton;
	[Export] private Button homeButton;
	[Export] private PackedScene calibrationScene;

	private CalibrationScreen calibration;

	public bool IsOpen => menuLayer != null && menuLayer.Visible;

	public override void _Ready()
	{
		// Keep the remote driving the pointer while the game is paused, and load the player's saved calibration
		WiimoteManager wiimote = GetTree().Root.GetNodeOrNull<WiimoteManager>("WiimoteManager");
		if (wiimote != null) {
			wiimote.ProcessMode = ProcessModeEnum.Always;
			CalibrationScreen.ApplySavedCalibration(wiimote);
		}

		if (menuLayer == null || recalibrateButton == null || closeButton == null || calibrationScene == null) {
			GD.PushError("PauseMenu: assign the menu layer, buttons and calibration scene in the Inspector");
			return;
		}

		menuLayer.Visible = false;
		recalibrateButton.Pressed += OpenCalibration;
		closeButton.Pressed += Close;
		if (homeButton != null)
		{
			homeButton.Pressed += GoHome;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		// The calibration screen handles its own input while it's open
		if (menuLayer == null || calibration != null) {
			return;
		}

		if (@event.IsActionPressed("pause")) {
			if (IsOpen) {
				GetViewport().SetInputAsHandled();
				Close();
			} else if (InGameScene()) {
				GetViewport().SetInputAsHandled();
				Open();
			}
		} else if (IsOpen && @event.IsActionPressed("back")) {
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	// True while one of the minigames is the current scene
	private bool InGameScene()
	{
		Node scene = GetTree().CurrentScene;
		return scene != null && System.Array.IndexOf(GameScenes, scene.SceneFilePath) >= 0;
	}

	public void Open()
	{
		menuLayer.Visible = true;
		GetTree().Paused = true;
	}

	public void Close()
	{
		menuLayer.Visible = false;
		GetTree().Paused = false;
	}

	private void GoHome()
	{
		Close();
		FishingLevelManager levels = GetTree().CurrentScene?.GetNodeOrNull<FishingLevelManager>("FishingLevelManager");
		if (levels != null)
		{
			levels.EndRun();
		}
		else
		{
			SceneManager.Instance.ChangeScene(SceneManager.MainMenu);
		}
	}

	private void OpenCalibration()
	{
		menuLayer.Visible = false;
		calibration = calibrationScene.Instantiate<CalibrationScreen>();
		calibration.Finished += OnCalibrationFinished;
		AddChild(calibration);
	}

	// Back to the pause menu whether the calibration was accepted or cancelled
	private void OnCalibrationFinished(bool accepted)
	{
		calibration.QueueFree();
		calibration = null;
		menuLayer.Visible = true;
	}
}
