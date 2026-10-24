using Godot;
using System;

public partial class WiimoteManager : Node2D
{
	public Vector2 CurrentAimPosition { get; private set; }
	//Variables to store 'A' and 'B' so it's easier to call later.
	private const string WiiAAction = "wii_a_button";
    private const string WiiBAction = "wii_b_trigger";

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
		//placeholder. This will be coded later.

	}

	public event Action OnWiimoteAClicked;
	public event Action OnWiimoteBClicked;


	// Called every frame. 'delta' is the elapsed time since the previous frame.
	//This is a Godot thing^.
	public override void _Process(double delta)
	{
		//This basically treats the wiimote as a mouse. Pointing logic is quite simple.
		//Touchmote is the software I landed upon.
		CurrentAimPosition = GetGlobalMousePosition();

		if(Input.IsActionJustPressed(WiiAAction))
		{
			OnWiimoteAClicked?.Invoke();

			GD.Print("Wiimote 'A button' clicked at: " + CurrentAimPosition);
		}

		if(Input.IsActionJustPressed(WiiBAction))
		{
			OnWiimoteBClicked?.Invoke();

			GD.Print("Wiimote 'B button' clicked at: " + CurrentAimPosition);
		}


	}
}
