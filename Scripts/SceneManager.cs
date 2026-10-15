using Godot;

public partial class SceneManager : Node
{
	// Lets any script call SceneManager.Instance.ChangeScene(...)
	public static SceneManager Instance { get; private set; }

	// Keep all scene paths in one place
	public const string MainMenu = "res://Scenes/MainMenu.tscn";
	public const string TestLevel = "res://Scenes/TestLevel.tscn";

	// Opens the testing level for now until real levels are made
	public const string GoneFishing = TestLevel;
	public const string FishNinja = TestLevel;

	// Name of the last minigame picked, so the next scene knows which game to run
	public string SelectedGame { get; private set; } = "";

	// Scene that was open before the current one, so Back buttons know where to go
	public string PreviousScene { get; private set; } = MainMenu;

	public override void _Ready()
	{
		Instance = this;
	}

	public void StartGame(string gameName, string path)
	{
		SelectedGame = gameName;
		ChangeScene(path);
	}

	public void ChangeScene(string path)
	{
		string current = GetTree().CurrentScene?.SceneFilePath;
		Error err = GetTree().ChangeSceneToFile(path);
		if (err != Error.Ok) {
			GD.PushError($"SceneManager: couldn't load '{path}' ({err})");
		} else if (!string.IsNullOrEmpty(current)) {
			PreviousScene = current;
		}
	}
}
