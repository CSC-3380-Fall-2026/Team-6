using Godot;

public partial class MenuOverlay : Node
{
	private Control _overlay;

	public override void _Ready()
	{
		_overlay = GetNode<Control>("MenuOverlay/Overlay");
		_overlay.Visible = false;
	}

	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("pause"))
		{
			if (GetTree().Paused)
			{
				ResumeGame();
			}
			else
			{
				PauseGame();
			}
		}
	}

	public void PauseGame()
	{
		_overlay.Visible = true;
		GetTree().Paused = true;
	}

	public void ResumeGame()
	{
		GetTree().Paused = false;
		_overlay.Visible = false;
	}
	
	public void _on_close_button_pressed()
	{
		ResumeGame();
	}
}
