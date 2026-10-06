using Godot;
using System;
using System.Numerics;

public partial class WiimoteManager : Node2D
{
	public Vector2 CurrentAimPosition { get; private set; }


	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		
		//placeholder. This will be coded later.

	}

	public event Action OnWiimoteAClicked;



	// Called every frame. 'delta' is the elapsed time since the previous frame.
	//This is a Godot thing^.
	public override void _Process(double delta)
	{
		CurrentAimPosition = GetGlobalMousePosition();

		if(Input.IsActionJustPressed("wii_a_button"))
		{
			OnWiimoteAClicked?.Invoke();

			GD.Print("Wiimote A Clicked at: " + CurrentAimPosition);

		}


	}
}
