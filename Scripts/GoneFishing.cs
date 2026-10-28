using Godot;
using System;

public partial class GoneFishing : Node
{
	[Export] private ColorRect _FishingRod; //this may not be a 'ColorRect' when emily adds the fishing rod. this should be a temp variable.
	[Export] private Polygon2D _waterShape;
	[Export] private CursorNode _cursorNode;
	//controls the speed/intensity of the rod following the cursor
	[Export] private float rodRotation = 12.0f;
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
			float angle = Mathf.Atan2(direction.Y, direction.X);
			//this makes the rod not SNAP over to the cursor, giving it a feeling of weight.
			_FishingRod.Rotation = Mathf.LerpAngle(_FishingRod.Rotation, angle, rodRotation * (float)delta);
			if(_cursorNode.IsPrimaryHeld)
			{
				//TODO CASTING LOGIC GOES HERE.
			}
		}



	}
}
