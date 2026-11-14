using Godot;

// The settings for one Gone Fishing level. There's one per level in Resources/Levels (level1.tres to level4.tres),
// and the FishSpawner switches to the next one when the level manager sends LevelChanged.
// The spawner uses the fish, fish count and swim speed. The bite delay, reaction window and pull strength are for
// the bite (#67) and reel-in (#69) code, which can read them from FishSpawner.CurrentLevelSettings or from the
// LevelSettings of the fish that bit.
[GlobalClass]
public partial class FishingLevel : Resource
{
	// Every fish that can spawn on this level, including the ones from earlier levels
	[Export]
	public FishData[] Fish { get; set; } = System.Array.Empty<FishData>();

	// How many fish swim in the pond at once
	[Export]
	public int MaxFish { get; set; } = 7;

	// Multiplies each fish's swim speed from its fish data (1 = unchanged)
	[Export]
	public float SwimSpeedMultiplier { get; set; } = 1f;

	// How long a fish waits before biting, in seconds. Each bite picks a random time between these two.
	[Export]
	public float BiteDelayMin { get; set; } = 1f;

	[Export]
	public float BiteDelayMax { get; set; } = 3f;

	// Seconds the player has to press the reel button after a fish bites. Each level should be shorter than the one before.
	[Export]
	public float ReactionWindow { get; set; } = 1f;

	// How hard fish pull while being reeled in (1 = normal)
	[Export]
	public float PullStrength { get; set; } = 1f;
}
