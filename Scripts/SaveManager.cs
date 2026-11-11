using Godot;
using System.Collections.Generic;
using System.Linq;

// Keeps high scores and settings in a file on the player's computer so they stick between play sessions.
// Loads once when the game starts and saves every time a value changes.
public partial class SaveManager : Node
{
	// Lets any script call SaveManager.Instance.SubmitScore(...)
	public static SaveManager Instance { get; private set; }

	// How many runs each game's high score list keeps
	public const int MaxHighScores = 5;

	// user:// is Godot's save folder for this game (on Windows: %APPDATA%\Godot\app_userdata\Wii Fishing)
	private const string SavePath = "user://save.cfg";

	// Sections of the save file
	private const string HighScoresSection = "high_scores";
	private const string SettingsSection = "settings";

	// Games that get a high score list
	private static readonly string[] HighScoreGames = { SceneManager.GoneFishingId, SceneManager.FishNinjaId };

	// Keys inside each high score entry
	private const string ScoreKey = "score";
	private const string TimePlayedKey = "time_played";
	private const string DateKey = "date";

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

	// A game's top runs, highest score first. Empty if the game hasn't been played yet.
	// gameId is SceneManager.GoneFishingId or SceneManager.FishNinjaId, the same as SceneManager.Instance.SelectedGame
	public List<HighScoreEntry> GetHighScores(string gameId)
	{
		List<HighScoreEntry> scores = new List<HighScoreEntry>();
		if (!IsHighScoreGame(gameId)) {
			return scores;
		}

		// Anything that isn't a list of entries (like the single best score older saves kept) counts as no scores yet
		Variant stored = save.GetValue(HighScoresSection, gameId, new Godot.Collections.Array());
		if (stored.VariantType != Variant.Type.Array) {
			return scores;
		}

		foreach (Variant item in stored.AsGodotArray()) {
			if (scores.Count == MaxHighScores) {
				break;
			}
			if (item.VariantType != Variant.Type.Dictionary) {
				continue;
			}
			Godot.Collections.Dictionary entry = item.AsGodotDictionary();
			if (entry.ContainsKey(ScoreKey) && entry.ContainsKey(TimePlayedKey) && entry.ContainsKey(DateKey)) {
				scores.Add(new HighScoreEntry(entry[ScoreKey].AsInt32(), entry[TimePlayedKey].AsSingle(), entry[DateKey].AsString()));
			}
		}
		return scores;
	}

	// Call when a game ends, with the final score and how many seconds the run lasted.
	// Adds the run to that game's list if it's in the top 5, and returns true when it made the list.
	public bool SubmitScore(string gameId, int score, float timePlayed)
	{
		if (!IsHighScoreGame(gameId)) {
			return false;
		}
		if (!float.IsFinite(timePlayed) || timePlayed < 0f) {
			GD.PushError($"SaveManager: timePlayed should be 0 or more seconds, got {timePlayed}");
			return false;
		}

		List<HighScoreEntry> scores = GetHighScores(gameId);
		// A tie goes below the runs already on the list, so the earlier run keeps its place
		int rank = scores.FindIndex(entry => score > entry.Score);
		if (rank == -1) {
			rank = scores.Count;
		}
		if (rank >= MaxHighScores) {
			return false;
		}

		scores.Insert(rank, new HighScoreEntry(score, timePlayed, Time.GetDateStringFromSystem()));
		if (scores.Count > MaxHighScores) {
			scores.RemoveAt(MaxHighScores);
		}

		Godot.Collections.Array stored = new Godot.Collections.Array();
		foreach (HighScoreEntry entry in scores) {
			stored.Add(new Godot.Collections.Dictionary {
				{ ScoreKey, entry.Score },
				{ TimePlayedKey, entry.TimePlayed },
				{ DateKey, entry.Date },
			});
		}
		save.SetValue(HighScoresSection, gameId, stored);
		Save();
		return true;
	}

	private static bool IsHighScoreGame(string gameId)
	{
		if (HighScoreGames.Contains(gameId)) {
			return true;
		}
		GD.PushError($"SaveManager: '{gameId}' has no high score list, use SceneManager.GoneFishingId or SceneManager.FishNinjaId");
		return false;
	}

	// Wiimote calibration: how far the pointer reads from the middle of the screen
	// when the player aims at the middle. Subtract it from the raw aim position.
	public Vector2 WiimoteCenterOffset
	{
		get => save.GetValue(SettingsSection, WiimoteCenterOffsetKey, Vector2.Zero).AsVector2();
		set => SetSetting(WiimoteCenterOffsetKey, value);
	}

	// Wiimote calibration: multiplies how far the pointer moves when the Wiimote moves. 1 is normal speed.
	public Vector2 WiimoteSensitivity
	{
		get => save.GetValue(SettingsSection, WiimoteSensitivityKey, Vector2.One).AsVector2();
		set => SetSetting(WiimoteSensitivityKey, value);
	}

	// True once the player has saved a calibration
	public bool HasWiimoteCalibration => save.HasSectionKey(SettingsSection, WiimoteSensitivityKey) && save.HasSectionKey(SettingsSection, WiimoteCenterOffsetKey);

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

// One run on a high score list. TimePlayed is in seconds and Date looks like "2026-10-31".
public record HighScoreEntry(int Score, float TimePlayed, string Date);
