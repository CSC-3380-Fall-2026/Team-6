using Godot;

// Keeps high scores and settings in a file on the player's computer so they stick between play sessions.
// Loads once when the game starts and saves every time a value changes.
public partial class SaveManager : Node
{
	// Lets any script call SaveManager.Instance.GetHighScore(...)
	public static SaveManager Instance { get; private set; }

	// user:// is Godot's save folder for this game (on Windows: %APPDATA%\Godot\app_userdata\Wii Fishing)
	private const string SavePath = "user://save.cfg";

	// Sections of the save file
	private const string HighScoresSection = "high_scores";
	private const string SettingsSection = "settings";

	// Keys inside the settings section
	private const string WiimoteCenterOffsetKey = "wiimote_center_offset";
	private const string WiimoteSensitivityKey = "wiimote_sensitivity";

	private readonly ConfigFile save = new ConfigFile();

	public override void _Ready()
	{
		if (Instance != null)
		{
			QueueFree();
			return;
		}
		Instance = this;
		Load();
	}

	// Best score for a game, or 0 if it hasn't been played yet.
	// Use the same name passed to SceneManager.StartGame, e.g. SceneManager.Instance.SelectedGame
	public int GetHighScore(string gameName)
	{
		return save.GetValue(HighScoresSection, gameName, 0).AsInt32();
	}

	// Call when a game ends. Saves the score if it beats the old best and returns true when it does.
	public bool SubmitScore(string gameName, int score)
	{
		if (string.IsNullOrEmpty(gameName)) {
			GD.PushError("SaveManager: can't save a high score without a game name");
			return false;
		}
		if (score <= GetHighScore(gameName)) {
			return false;
		}

		save.SetValue(HighScoresSection, gameName, score);
		Save();
		return true;
	}

	// Wiimote calibration: how far the pointer reads from the middle of the screen
	// when the player aims at the middle. Subtract it from the raw aim position.
	public Vector2 WiimoteCenterOffset
	{
		get => save.GetValue(SettingsSection, WiimoteCenterOffsetKey, Vector2.Zero).AsVector2();
		set => SetSetting(WiimoteCenterOffsetKey, value);
	}

	// Wiimote calibration: multiplies how far the pointer moves when the Wiimote moves. 1 is normal speed.
	public float WiimoteSensitivity
	{
		get => save.GetValue(SettingsSection, WiimoteSensitivityKey, 1f).AsSingle();
		set => SetSetting(WiimoteSensitivityKey, value);
	}

	private void SetSetting(string key, Variant value)
	{
		save.SetValue(SettingsSection, key, value);
		Save();
	}

	private void Load()
	{
		Error err = save.Load(SavePath);
		if (err == Error.FileNotFound) {
			// First time playing, everything starts at its default
			return;
		}
		if (err != Error.Ok) {
			GD.PushWarning($"SaveManager: couldn't read '{SavePath}' ({err}), starting with a fresh save");
			save.Clear();
		}
	}

	private void Save()
	{
		Error err = save.Save(SavePath);
		if (err != Error.Ok) {
			GD.PushError($"SaveManager: couldn't write '{SavePath}' ({err})");
		}
	}
}
