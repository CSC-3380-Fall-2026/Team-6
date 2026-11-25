
using Godot;

public partial class GameHud : Control
{
	[Export]
	public string GameId { get; set; } = "";

	[Export]
	public Label TimeValue { get; set; } = null!;

	[Export]
	public Label ScoreValue { get; set; } = null!;

	[Export]
	public Label HighScoreValue { get; set; } = null!;

	private double _elapsedTime = 0.0;
	private int _currentScore = 0;

	private int _lastDisplayedSecond = -1;

	public override void _Ready()
	{
		// The HUD pauses along with the game.
		ProcessMode = ProcessModeEnum.Pausable;

		UpdateTimerDisplay();
		UpdateScoreDisplay();

		// We'll connect this to SaveManager in a later step.
		HighScoreValue.Text = "0";
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

	if (totalSeconds == _lastDisplayedSecond)
	{
		return;
	}

	_lastDisplayedSecond = totalSeconds;

	int minutes = totalSeconds / 60;
	int seconds = totalSeconds % 60;

	TimeValue.Text = $"{minutes:00}:{seconds:00}";
}

	private void UpdateScoreDisplay()
	{
		ScoreValue.Text = _currentScore.ToString();
	}

	public void ResetHud()
	{
		_elapsedTime = 0.0;
		_currentScore = 0;

		UpdateTimerDisplay();
		UpdateScoreDisplay();
	}
}
