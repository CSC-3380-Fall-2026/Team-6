using Godot;

public partial class FishSpawner : Node2D
{
    [Export]
    private PackedScene _fishScene;
    
    [Export]
    private int _maxFishCount = 10;
    
    [Export]
    private Polygon2D _waterPolygon;

    private int _currentFishCount = 0;

    public override void _Ready()
    {
        for (int i = 0; i < _maxFishCount; i++)
        {
            SpawnFish();
        }
    }

    private void SpawnFish()
    {
        if (_currentFishCount >= _maxFishCount) return;

        Fish newFish = _fishScene.Instantiate<Fish>();
        newFish.GlobalPosition = GetRandomWaterPoint();
        
        // Subscribe to movement and cleanup events
        newFish.OnReachedWaypoint += AssignNewWaypoint;
        newFish.OnDespawn += HandleFishDespawn;

        AddChild(newFish);
        
        // Give the fish its first destination
        newFish.SetTarget(GetRandomWaterPoint());
        
        _currentFishCount++;
    }

    private void AssignNewWaypoint(Fish fish)
    {
        fish.SetTarget(GetRandomWaterPoint());
    }

    private void HandleFishDespawn(Fish fish)
    {
        fish.OnReachedWaypoint -= AssignNewWaypoint;
        fish.OnDespawn -= HandleFishDespawn;
        
        _currentFishCount--;
        SpawnFish();
    }

    private Vector2 GetRandomWaterPoint()
    {
        if (_waterPolygon == null || _waterPolygon.Polygon.Length < 3)
        {
            return GlobalPosition; 
        }

        Vector2[] vertices = _waterPolygon.Polygon;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (Vector2 point in vertices)
        {
            if (point.X < minX) minX = point.X;
            if (point.X > maxX) maxX = point.X;
            if (point.Y < minY) minY = point.Y;
            if (point.Y > maxY) maxY = point.Y;
        }

        for (int i = 0; i < 30; i++)
        {
            float randomX = (float)GD.RandRange(minX, maxX);
            float randomY = (float)GD.RandRange(minY, maxY);
            Vector2 candidatePoint = new Vector2(randomX, randomY);

            if (Geometry2D.IsPointInPolygon(candidatePoint, vertices))
            {
                return _waterPolygon.ToGlobal(candidatePoint);
            }
        }

        return _waterPolygon.ToGlobal(vertices[0]);
    }
}