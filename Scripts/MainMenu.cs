using Godot;

// Classes open the two games and the High Scores page
public partial class MainMenu : Control
{
	// Assigned in the Inspector so the buttons can be moved or renamed in the scene
	[Export] private Button goneFishingButton;
	[Export] private Button fishNinjaButton;
	[Export] private Button highScoresButton;

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
}
