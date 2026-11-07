using Godot;

public partial class FishSpawner : Node2D
{
    [Export]
    private PackedScene _fishScene;
    
    [Export]
    private int _maxFishCount = 10;
    
    [Export]
    private Polygon2D _waterPolygon;
    [Export]
    private FishData[] _availableFish;

    private int _currentFishCount = 0;

    // Cache the bounding box so we don't recalculate it hundreds of times a minute
    private float _minX;
    private float _maxX;
    private float _minY;
    private float _maxY;
    
    private const int MaxSpawnAttempts = 30;

    public override void _Ready()
    {
		//Calculate the bounding box exactly ONCE at startup
        if (_waterPolygon != null && _waterPolygon.Polygon.Length >= 3)
        {
            _minX = _maxX = _waterPolygon.Polygon[0].X;
            _minY = _maxY = _waterPolygon.Polygon[0].Y;

            foreach (Vector2 point in _waterPolygon.Polygon)
            {
                if (point.X < _minX) { _minX = point.X; }
                if (point.X > _maxX) { _maxX = point.X; }
                if (point.Y < _minY) { _minY = point.Y; }
                if (point.Y > _maxY) { _maxY = point.Y; }
            }
        }

        // 2. Spawn the initial population
        for (int i = 0; i < _maxFishCount; i++)
        {
            SpawnFish();
        }
    }

    private void SpawnFish()
    {
        if (_currentFishCount >= _maxFishCount) 
        {
            return;
        }

        Fish newFish = _fishScene.Instantiate<Fish>();
        newFish.GlobalPosition = GetRandomWaterPoint();
        

        FishData selectedData = GetRandomFishData();
        // Pass the randomly selected data into the newly spawned fish so it updates its speed!
        newFish.ApplyData(selectedData);
        
        newFish.OnReachedWaypoint += AssignNewWaypoint;
        newFish.OnDespawn += HandleFishDespawn;

        AddChild(newFish);
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

    private FishData GetRandomFishData()
    {
        // Failsafe if the array is empty in the Inspector
        if (_availableFish == null || _availableFish.Length == 0)
        {
            return null;
        }
        int randomIndex = (int)(GD.Randi() % _availableFish.Length);
        return _availableFish[randomIndex];
    }

    private Vector2 GetRandomWaterPoint()
    {
        if (_waterPolygon == null || _waterPolygon.Polygon.Length < 3)
        {
            return GlobalPosition; 
        }

        Vector2[] vertices = _waterPolygon.Polygon;

        for (int i = 0; i < MaxSpawnAttempts; i++)
        {
            float randomX = (float)GD.RandRange(_minX, _maxX);
            float randomY = (float)GD.RandRange(_minY, _maxY);
            Vector2 candidatePoint = new Vector2(randomX, randomY);

            if (Geometry2D.IsPointInPolygon(candidatePoint, vertices))
            {
                // Convert the local polygon point to a global screen position
                return _waterPolygon.ToGlobal(candidatePoint);
            }
        }

        return _waterPolygon.ToGlobal(vertices[0]);
    }
}