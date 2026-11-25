using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

// Shows the top runs for both games side by side.
// Point the crosshair at Back and press A, or just press B, to go back.
// In debug builds, 1 and 2 add a random Gone Fishing or Fish Ninja score so the page can be tested before the games have endings.
public partial class HighScoresScreen : Control
{
	// Assigned in the Inspector so the nodes can be moved or renamed in the scene.
	// Each table's first row (the column headings) is set up in the scene, and the score rows are added under it.
	[Export] private GridContainer goneFishingTable;
	[Export] private GridContainer fishNinjaTable;
	[Export] private Button backButton;
	[Export] private Label debugLabel;

	private WiimoteManager wiimote;

	public override void _Ready()
	{
		// Same track as the main menu, so it keeps playing instead of restarting
		AudioManager.Instance?.PlayMusic(AudioManager.MenuMusic);

		if (goneFishingTable == null || fishNinjaTable == null || backButton == null || debugLabel == null) {
			GD.PushError("HighScoresScreen: assign the tables, BackButton and DebugLabel in the Inspector");
			return;
		}

		backButton.Pressed += GoBack;
		wiimote = GetTree().Root.GetNodeOrNull<WiimoteManager>("WiimoteManager");
		if (wiimote != null) {
			wiimote.OnWiimoteBClicked += OnBPressed;
		}

		debugLabel.Visible = OS.IsDebugBuild();
		ShowScores();
	}

	public override void _ExitTree()
	{
		if (wiimote != null) {
			wiimote.OnWiimoteBClicked -= OnBPressed;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("back")) {
			GetViewport().SetInputAsHandled();
			GoBack();
		} else if (OS.IsDebugBuild() && @event is InputEventKey key && key.Pressed && !key.Echo) {
			if (key.Keycode == Key.Key1) {
				AddRandomScore(SceneManager.GoneFishingId);
			} else if (key.Keycode == Key.Key2) {
				AddRandomScore(SceneManager.FishNinjaId);
			}
		}
	}

	private void ShowScores()
	{
		FillTable(goneFishingTable, SaveManager.Instance.GetHighScores(SceneManager.GoneFishingId));
		FillTable(fishNinjaTable, SaveManager.Instance.GetHighScores(SceneManager.FishNinjaId));
	}

	// Rebuilds the rows under the headings. Every rank is shown, with dashes until a run fills it.
	private static void FillTable(GridContainer table, List<HighScoreEntry> scores)
	{
		foreach (Node oldCell in table.GetChildren().Skip(table.Columns)) {
			table.RemoveChild(oldCell);
			oldCell.QueueFree();
		}

		for (int i = 0; i < SaveManager.MaxHighScores; i++) {
			bool filled = i < scores.Count;
			AddCell(table, $"{i + 1}.");
			AddCell(table, filled ? scores[i].Score.ToString() : "-");
			AddCell(table, filled ? FormatTime(scores[i].TimePlayed) : "-");
			AddCell(table, filled ? FormatDate(scores[i].Date) : "-");
		}
	}

	private static void AddCell(GridContainer table, string text)
	{
		table.AddChild(new Label {
			Text = text,
			HorizontalAlignment = HorizontalAlignment.Center,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
		});
	}

	// 63.5 seconds shows as 1:03
	private static string FormatTime(float seconds)
	{
		int wholeSeconds = (int)seconds;
		return $"{wholeSeconds / 60}:{wholeSeconds % 60:00}";
	}

	// "2026-10-31" shows as Oct 31, 2026
	private static string FormatDate(string date)
	{
		if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed)) {
			return parsed.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
		}
		return date;
	}

	// Debug only: stands in for a game ending, with a random score and run length
	private void AddRandomScore(string gameId)
	{
		int score = GD.RandRange(0, 1000);
		float timePlayed = (float)GD.RandRange(30.0, 300.0);
		bool madeList = SaveManager.Instance.SubmitScore(gameId, score, timePlayed);
		debugLabel.Text = $"Debug: {gameId} scored {score}, " + (madeList ? "made the top 5" : "didn't make the top 5");
		ShowScores();
	}

	// The remote's B still arrives while the game is paused, so ignore it then (the pause menu is on top)
	private void OnBPressed()
	{
		if (CanProcess()) {
			GoBack();
		}
	}

	// Returns to whichever screen opened this one
	private void GoBack()
	{
		SceneManager.Instance.ChangeScene(SceneManager.Instance.PreviousScene);
	}
}
