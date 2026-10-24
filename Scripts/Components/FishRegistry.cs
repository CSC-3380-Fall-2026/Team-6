using Godot;
using System;

public static class FishRegistry
{
    private const string FishFolder = "res://Resources/Fish";

    public static Godot.Collections.Array<FishData> GetAllFish()
    {
        var result = new Godot.Collections.Array<FishData>();

        using var directory = DirAccess.Open(FishFolder);

        if (directory == null)
        {
            GD.PushError($"Could not open fish folder: {FishFolder}");
            return result;
        }

        foreach (string file in directory.GetFiles())
        {
            // only load .tres files
            if (!file.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string path = $"{FishFolder}/{file}";
            FishData fish = ResourceLoader.Load<FishData>(path);

            if (fish == null)
            {
                GD.PushWarning($"Could not load fish: {path}");
                continue;
            }

            result.Add(fish);
        }

        return result;
    }
}