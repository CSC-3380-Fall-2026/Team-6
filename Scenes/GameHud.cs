
using Godot;

public partial class GameHud : Control
{
	[Export]
	public string GameId { get; set; } = "";

	private Label _timeValue = null!;
	private Label _scoreValue = null!;
	private Label _highScoreValue = null!;

	private double _elapsedTime = 0.0;
	private int _currentScore = 0;

	public override void _Ready()
	{
		_timeValue = GetNode<Label>(
			"MarginContainer/HBoxContainer/TimerPanel/VBoxContainer/TimeValue");

		_scoreValue = GetNode<Label>(
			"MarginContainer/HBoxContainer/ScorePanel/VBoxContainer/ScoreValue");

		_highScoreValue = GetNode<Label>(
			"MarginContainer/HBoxContainer/HighScorePanel/VBoxContainer/HighScoreValue");

		// The HUD pauses along with the game.
		ProcessMode = ProcessModeEnum.Pausable;

		UpdateTimerDisplay();
		UpdateScoreDisplay();

		// We'll connect this to SaveManager in a later step.
		_highScoreValue.Text = "0";
	}

	public override void _Process(double delta)
	{
		_elapsedTime += delta;
		UpdateTimerDisplay();
	}

	public void SetScore(int score)
	{
		_currentScore = Mathf.Max(0, score);
		UpdateScoreDisplay();
	}

	public void AddScore(int points)
	{
		SetScore(_currentScore + points);
	}

	public int GetScore()
	{
		return _currentScore;
	}

	public double GetElapsedTime()
	{
		return _elapsedTime;
	}

	private void UpdateTimerDisplay()
	{
		int totalSeconds = (int)_elapsedTime;
		int minutes = totalSeconds / 60;
		int seconds = totalSeconds % 60;

		_timeValue.Text = $"{minutes:00}:{seconds:00}";
	}

	private void UpdateScoreDisplay()
	{
		_scoreValue.Text = _currentScore.ToString();
	}

	public void ResetHud()
	{
		_elapsedTime = 0.0;
		_currentScore = 0;

		UpdateTimerDisplay();
		UpdateScoreDisplay();
	}
}
