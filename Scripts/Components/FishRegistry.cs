using Godot;
using System;

public static class FishRegistry
{
    private const string FishFolder = "res://Resources/Fish";
    private static Godot.Collections.Array<FishData> _fish;

    public static Godot.Collections.Array<FishData> GetAllFish()
    {
        // if fish have already been loaded, return cached list
        if (_fish != null)
        {
            return _fish;
        }
        
        // first time GetAllFish() called, create and fill the list
        _fish = new Godot.Collections.Array<FishData>();

        using var directory = DirAccess.Open(FishFolder);

        if (directory == null)
        {
            GD.PushError($"Could not open fish folder: {FishFolder}");
            return _fish;
        }

        foreach (string file in directory.GetFiles())
        {
            // only load .tres files
            if (!file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string path = $"{FishFolder}/{file}";

            // load each fish as FishData resource
            FishData fish = ResourceLoader.Load<FishData>(path);

            if (fish == null)
            {
                GD.PushWarning($"Could not load fish: {path}");
                continue;
            }

            _fish.Add(fish);
        }

        return _fish;
    }
}