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

    // One FishingLevel per level (Resources/Levels/level1.tres to level4.tres): which fish spawn, how many,
    // and how fast they swim. Level 1's settings replace _availableFish and _maxFishCount when the game starts.
    [Export]
    private FishingLevel[] _levels;

    // The settings of the level being played. The bite and reel code can read the bite delay,
    // reaction window and pull strength from here.
    public FishingLevel CurrentLevelSettings { get; private set; }

    // The level manager's signal for a new level (#86)
    private const string LevelChangedSignal = "LevelChanged";

    // The shortest reaction window a player can still hit, in seconds (people need about a quarter second to react)
    private const float ShortestFairReactionWindow = 0.3f;

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

        CheckLevels();
        LoadLevel(1);

        // 2. Spawn the initial population
        for (int i = 0; i < _maxFishCount; i++)
        {
            SpawnFish();
        }

        ConnectToLevelManager();
    }

    // Switches to a level's settings (1 is the first level). Fish that spawn from now on use them.
    public void LoadLevel(int level)
    {
        if (_levels == null || _levels.Length == 0)
        {
            return;
        }

        CurrentLevelSettings = _levels[Mathf.Clamp(level, 1, _levels.Length) - 1];
        _availableFish = CurrentLevelSettings.Fish;
        _maxFishCount = CurrentLevelSettings.MaxFish;
    }

    private void OnLevelChanged(int level)
    {
        LoadLevel(level);
        // A level with more fish fills up straight away. With fewer, the extra fish aren't replaced when they leave.
        while (_currentFishCount < _maxFishCount)
        {
            SpawnFish();
        }
    }

    // Listens for the level manager's LevelChanged (from #86). It's found by that signal next to the spawner,
    // so nothing has to be assigned in the Inspector. Without a level manager the pond stays on level 1.
    private void ConnectToLevelManager()
    {
        foreach (Node node in GetParent().GetChildren())
        {
            if (node != this && node.HasSignal(LevelChangedSignal))
            {
                node.Connect(LevelChangedSignal, Callable.From<int>(OnLevelChanged));
                return;
            }
        }
    }

    // Warns about level settings that would break the difficulty curve
    private void CheckLevels()
    {
        if (_levels == null)
        {
            return;
        }

        for (int i = 0; i < _levels.Length; i++)
        {
            FishingLevel level = _levels[i];
            if (level == null || level.Fish.Length == 0 || level.MaxFish < 1)
            {
                GD.PushWarning($"FishSpawner: level {i + 1} needs at least one fish and a Max Fish of 1 or more");
                continue;
            }
            if (level.ReactionWindow < ShortestFairReactionWindow)
            {
                GD.PushWarning($"FishSpawner: level {i + 1}'s reaction window ({level.ReactionWindow}s) is too short for anyone to react");
            }
            if (i > 0 && _levels[i - 1] != null && level.ReactionWindow >= _levels[i - 1].ReactionWindow)
            {
                GD.PushWarning($"FishSpawner: level {i + 1}'s reaction window should be shorter than level {i}'s");
            }
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
        newFish.ApplyData(selectedData, CurrentLevelSettings);
        
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