
using Godot;

public partial class InputManager : Node
{
	public static InputManager Instance;

	public Vector2 PointerPosition;

	public bool SelectPressed;
	public bool BackPressed;
	public bool PausePressed;

	public override void _Ready()
	{
		Instance = this;
	}

	public override void _Process(double delta)
	{
		PointerPosition = GetViewport().GetMousePosition();

		SelectPressed = Input.IsActionJustPressed("select");
		BackPressed = Input.IsActionJustPressed("back");
		PausePressed = Input.IsActionJustPressed("pause");
		if (SelectPressed)
		GD.Print("Select pressed");
		if (BackPressed)
		GD.Print("Back pressed");
		if (PausePressed)
		GD.Print("Pause pressed");
	}
}
