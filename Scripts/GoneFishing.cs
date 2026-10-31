using Godot;
using System;

public partial class GoneFishing : Node
{
	[Export] private Node2D _FishingRod; //this may not be a 'ColorRect' when emily adds the fishing rod. this should be a temp variable.
	[Export] private Polygon2D _waterShape;
	[Export] private CursorNode _cursorNode;
	//controls the speed/intensity of the rod following the cursor
	[Export] private float rodRotation = 12.0f;
	//controls where the rod is considered to be so rotation doesn't attempt to follow the cursor when it's at the rod.
	[Export] private float rodDeadzone = 10.0f;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		//This grabs the actual array of points creating the water polygon
		Vector2[] waterPoints = _waterShape.Polygon;
		//virtually nothing should happen if the user is pointing at grass. So everything is in this method.
		if(Geometry2D.IsPointInPolygon(_cursorNode.GlobalPosition, waterPoints))
		{
			Vector2 direction = _cursorNode.GlobalPosition - _FishingRod.GlobalPosition;
			//The mathf.pi is put in to allow the rod to start vertical rather than horizontal.
			//Without it, the rod point is 90 degrees offset to the left.
			float angle = Mathf.Atan2(direction.Y, direction.X) + Mathf.Pi / 2;
			
			//this makes the rod not SNAP over to the cursor, giving it a feeling of weight.
			if(direction.Length() > rodDeadzone)
			{
				_FishingRod.Rotation = Mathf.LerpAngle(_FishingRod.Rotation, angle, rodRotation * (float)delta);
			
			}
			if(_cursorNode.IsPrimaryHeld)
			{
				//TODO CASTING LOGIC GOES HERE.
			}
		}



	}
}
