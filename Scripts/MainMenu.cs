using Godot;

// Classes open the two games and bring to current test screen
public partial class MainMenu : Control
{
	// Assigned in the Inspector so the buttons can be moved or renamed in the scene
	[Export] private Button goneFishingButton;
	[Export] private Button fishNinjaButton;

	public override void _Ready()
	{
		if (goneFishingButton == null || fishNinjaButton == null) {
			GD.PushError("MainMenu: assign the game buttons in the Inspector");
			return;
		}

		goneFishingButton.Pressed += OnGoneFishingPressed;
		fishNinjaButton.Pressed += OnFishNinjaPressed;
	}

	private void OnGoneFishingPressed()
	{
		SceneManager.Instance.StartGame("Gone Fishing", SceneManager.GoneFishing);
	}

	private void OnFishNinjaPressed()
	{
		SceneManager.Instance.StartGame("Fish Ninja", SceneManager.FishNinja);
	}
}
