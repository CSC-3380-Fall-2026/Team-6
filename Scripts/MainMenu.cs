using Godot;

public partial class MainMenu : Control
{
	// Assigned in the Inspector so the buttons can be moved or renamed in the scene
	[Export] private Button goneFishingButton;
	[Export] private Button fishNinjaButton;
	[Export] private Button highScoresButton;
	[Export] private Button quitButton;
	// New Close game stuff
	[Export] private Control quitBox;
	[Export] private Button quitYesButton;
	[Export] private Button quitNoButton;
	[Export] private CrosshairButtons menuCrosshairButtons;

	public override void _Ready()
	{
		AudioManager.Instance?.PlayMusic(AudioManager.MenuMusic);

		if (goneFishingButton == null || fishNinjaButton == null || highScoresButton == null) {
			GD.PushError("MainMenu: assign the menu buttons in the Inspector");
			return;
		}

		goneFishingButton.Pressed += OnGoneFishingPressed;
		fishNinjaButton.Pressed += OnFishNinjaPressed;
		highScoresButton.Pressed += OnHighScoresPressed;
	// Error checking for the quit box and buttons
		if (quitButton == null || quitBox == null || quitYesButton == null || quitNoButton == null || menuCrosshairButtons == null) {
			GD.PushError("MainMenu: assign the quit button, quit box, its Yes and No buttons and the menu's CrosshairButtons in the Inspector");
			return;
		}

		quitBox.Visible = false;
		quitButton.Pressed += OpenQuitBox;
		quitYesButton.Pressed += QuitGame;
		quitNoButton.Pressed += CloseQuitBox;
	}

	// Esc closes the box
	public override void _UnhandledInput(InputEvent @event)
	{
		if (quitBox != null && quitBox.Visible && @event.IsActionPressed("back")) {
			GetViewport().SetInputAsHandled();
			CloseQuitBox();
		}
	}

	private void OnGoneFishingPressed()
	{
		SceneManager.Instance.StartGame(SceneManager.GoneFishingId, SceneManager.FishingLevel);
	}

	private void OnFishNinjaPressed()
	{
		SceneManager.Instance.StartGame(SceneManager.FishNinjaId, SceneManager.FishNinja);
	}

	private void OnHighScoresPressed()
	{
		SceneManager.Instance.ChangeScene(SceneManager.HighScoresScreen);
	}
	
	private void OpenQuitBox()
	{
		quitBox.Visible = true;
		menuCrosshairButtons.ProcessMode = ProcessModeEnum.Disabled;
	}

	private void CloseQuitBox()
	{
		quitBox.Visible = false;
		// Turned back on at the end of the frame so the click that pressed no can't also press a menu button behind it
		Callable.From(() => { menuCrosshairButtons.ProcessMode = ProcessModeEnum.Inherit; }).CallDeferred();
	}

	// Saves anything unsaved through SaveManager then closes the game
	private void QuitGame()
	{
		SaveManager.Instance?.Save();
		GetTree().Quit();
	}
}
