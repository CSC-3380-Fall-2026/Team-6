using Godot;
using System;

// Godot 4 requires the 'partial' keyword so it can inject its own generated C# code behind the scenes.
public partial class FishSpawner : Node2D
{
    [Export]
    private Polygon2D _waterShape;

    [Export]
    private FishData[] _availableFish;

    private float _minX;
    private float _maxX;
    private float _minY;
    private float _maxY;

    public override void _Ready()
    {
        
        if (_waterShape.Polygon.Length == 0)
        {
            // The equivalent to System.err.println() in Godot C# is GD.PushError()
            GD.PushError("Water shape polygon is empty");
            return;
        }

    
        _minX = _maxX = _waterShape.Polygon[0].X;
        _minY = _maxY = _waterShape.Polygon[0].Y;

        foreach (Vector2 point in _waterShape.Polygon)
        {
            if (point.X < _minX) 
            { 
                _minX = point.X; 
            }
            if (point.X > _maxX) 
            { 
                _maxX = point.X; 
            }
            if (point.Y < _minY) 
            { 
                _minY = point.Y; 
            }
            if (point.Y > _maxY) 
            { 
                _maxY = point.Y; 
            }
        }
    }

    // Helper method to execute our Rejection Sampling algorithm
    private Vector2 GetRandomWaterPoint()
    {
        while (true)
        {
            float x = (float)GD.RandRange(_minX, _maxX);
            float y = (float)GD.RandRange(_minY, _maxY);
            Vector2 point = new Vector2(x, y);

            if (Geometry2D.IsPointInPolygon(point, _waterShape.Polygon))
            {
                return point;
            }
        }
    }
}