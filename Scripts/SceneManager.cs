using Godot;

public partial class SceneManager : Node
{
	// Lets any script call SceneManager.Instance.ChangeScene(...)
	public static SceneManager Instance { get; private set; }

	// Keep all scene paths in one place
	public const string MainMenu = "res://Scenes/MainMenu.tscn";
	public const string TestLevel = "res://Scenes/TestLevel.tscn";

	public override void _Ready()
	{
		Instance = this;
	}

	public void ChangeScene(string path)
	{
		Error err = GetTree().ChangeSceneToFile(path);
		if (err != Error.Ok)
		{
			GD.PushError($"SceneManager: couldn't load '{path}' ({err})");
		}
	}
}
