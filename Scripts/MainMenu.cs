using Godot;

// Classes open the two games and bring to current test screen
public partial class MainMenu : Control
{
	public override void _Ready()
	{
		GetNode<Button>("GoneFishingButton").Pressed += OnGoneFishingPressed;
		GetNode<Button>("FishNinjaButton").Pressed += OnFishNinjaPressed;
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
