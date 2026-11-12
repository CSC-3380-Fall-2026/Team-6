using Godot;
using System.Collections.Generic;

// Throws fish up from the bottom, left and right edges of the screen in waves, with a short break between waves.
// Each arc is worked out from gravity so the fish peaks inside the screen and falls back out the bottom.
// WaveSize and SpeedMultiplier can be changed while the game runs, so score or difficulty can drive them later.
public partial class FishLauncher : Node
{
	// A fish the player was meant to cut fell off the bottom. Later this can take a life off the player.
	[Signal] public delegate void FishMissedEventHandler(FishData fish);
	[Signal] public delegate void WaveStartedEventHandler(int waveNumber);

	private enum Edge { Bottom, Left, Right }

	// Screen fractions ThrowFish picks from. X is measured from the left edge and Y from the top.
	// Bottom fish start and land anywhere between these widths
	private const float BottomMinX = 0.15f;
	private const float BottomMaxX = 0.85f;
	// Side fish start between these heights
	private const float SideStartMinY = 0.45f;
	private const float SideStartMaxY = 0.8f;
	// Side fish land this far across the screen, measured from the side they were thrown from
	private const float SideLandingMin = 0.4f;
	private const float SideLandingMax = 0.85f;
	// Fish always peak at least this far above where they start, as a fraction of the screen height
	private const float MinRise = 0.15f;

	// Assigned in the Inspector
	[Export] private PackedScene fishScene;
	[Export] private Node2D fishParent;

	// Fish thrown in each wave
	[Export] public int WaveSize = 3;
	// 1 is normal speed. 2 sends every fish along the same arc twice as fast.
	[Export] public float SpeedMultiplier = 1f;

	// Downward pull at normal speed, in pixels per second squared
	[Export] private float gravity = 1400f;
	// Seconds between fish in the same wave
	[Export] private float throwInterval = 0.35f;
	// Seconds to wait once a wave's fish are all gone, before the next wave
	[Export] private float breakBetweenWaves = 1.5f;
	// Range the arcs peak in, as a fraction of the screen height from the top
	[Export] private float highestPeak = 0.12f;
	[Export] private float lowestPeak = 0.45f;
	// How far off screen the fish start in pixels
	[Export] private float edgeMargin = 140f;
	// Fastest a fish spins, in radians per second
	[Export] private float maxSpin = 4f;
	// How likely each rarity is to be thrown: common, uncommon, rare, epic, legendary
	[Export] private float[] rarityWeights = { 50f, 25f, 15f, 7f, 3f };

	private readonly List<NinjaFish> inFlight = new List<NinjaFish>();
	private readonly List<FishData> fishPool = new List<FishData>();
	private readonly RandomNumberGenerator random = new RandomNumberGenerator();
	private int fishLeftToThrow;
	private float timer;

	public int WaveNumber { get; private set; }
	public int FishInFlight => inFlight.Count;
	// Checks the Inspector slots and loads every fish that has a Fish Ninja sprite, then waits one break before the first wave
 	public override void _Ready()
	{
		if (fishScene == null || fishParent == null) {
			GD.PushError("FishLauncher: assign the fish scene and fish parent in the Inspector");
			SetProcess(false);
			return;
		}

		Godot.Collections.Array<FishData> allFish = FishRegistry.GetAllFish();
		if (allFish == null) {
			GD.PushError("FishLauncher: the fish registry didn't load, check Resources/Fish");
			SetProcess(false);
			return;
		}

		foreach (FishData fish in allFish) {
			if (fish.FishNinjaSprite != null) {
				fishPool.Add(fish);
			} else {
				GD.PushWarning($"FishLauncher: {fish.FishName} has no Fish Ninja sprite, so it won't be thrown");
			}
		}
		if (fishPool.Count == 0) {
			GD.PushError("FishLauncher: no fish to throw, check Resources/Fish");
			SetProcess(false);
			return;
		}

		random.Randomize();
		timer = breakBetweenWaves;
	}
	// Called every frame. Throws fish in waves, with a break between waves.
	public override void _Process(double delta)
	{
		timer -= (float)delta;

		if (fishLeftToThrow > 0) {
			if (timer <= 0f) {
				ThrowFish();
				fishLeftToThrow--;
				timer = throwInterval;
			}
			return;
		}

		// The break only starts once the whole wave is gone
		if (inFlight.Count > 0) {
			timer = breakBetweenWaves;
		} else if (timer <= 0f) {
			StartWave();
		}
	}

	// Launch velocity for an arc that leaves start, peaks at peakY and comes back down through (landingX, landingY)
	public static Vector2 ArcVelocity(Vector2 start, float peakY, float landingX, float landingY, float gravity)
	{
		float timeUp = Mathf.Sqrt(2f * (start.Y - peakY) / gravity);
		float timeDown = Mathf.Sqrt(2f * (landingY - peakY) / gravity);
		return new Vector2((landingX - start.X) / (timeUp + timeDown), -gravity * timeUp);
	}
	//Starts a new wave, resetting the number of fish to throw and firing WaveStarted. WaveNumber is increased
	private void StartWave()
	{
		WaveNumber++;
		fishLeftToThrow = Mathf.Max(1, WaveSize);
		timer = 0f;
		EmitSignal(SignalName.WaveStarted, WaveNumber);
	}
	// Throws a fish from a random edge, random peak and landing point. 
	// Adds the fish to the scene and calls its launch method
	// PickFish choses a fish using the rarity weights
	private void ThrowFish()
	{
		Vector2 screen = fishParent.GetViewportRect().Size;
		Edge edge = (Edge)random.RandiRange(0, 2);
		Vector2 start;
		float landingX;
		// Side fish land on the far half of the screen so they fly across it
		if (edge == Edge.Bottom) {
			start = new Vector2(screen.X * random.RandfRange(BottomMinX, BottomMaxX), screen.Y + edgeMargin);
			landingX = screen.X * random.RandfRange(BottomMinX, BottomMaxX);
		} else if (edge == Edge.Left) {
			start = new Vector2(-edgeMargin, screen.Y * random.RandfRange(SideStartMinY, SideStartMaxY));
			landingX = screen.X * random.RandfRange(SideLandingMin, SideLandingMax);
		} else {
			start = new Vector2(screen.X + edgeMargin, screen.Y * random.RandfRange(SideStartMinY, SideStartMaxY));
			landingX = screen.X * (1f - random.RandfRange(SideLandingMin, SideLandingMax));
		}
		// Always peak at least a little above where the fish starts
		float peakY = Mathf.Min(screen.Y * random.RandfRange(highestPeak, lowestPeak), start.Y - screen.Y * MinRise);

		// Squaring the speed for gravity keeps the arc the same shape, just faster
		float pull = gravity * SpeedMultiplier * SpeedMultiplier;
		Vector2 velocity = ArcVelocity(start, peakY, landingX, screen.Y + edgeMargin, pull);

		NinjaFish fish = fishScene.Instantiate<NinjaFish>();
		fishParent.AddChild(fish);
		fish.FellOffScreen += OnFishFellOffScreen;
		// Also covers fish that leave some other way later on, like being cut
		fish.TreeExited += () => inFlight.Remove(fish);
		inFlight.Add(fish);
		fish.Launch(PickFish(), start, velocity, pull, random.RandfRange(-maxSpin, maxSpin) * SpeedMultiplier);
	}

	// Picks a fish at random, with rarer fish thrown less often
	private FishData PickFish()
	{
		float total = 0f;
		foreach (FishData fish in fishPool) {
			total += RarityWeight(fish);
		}

		float roll = random.RandfRange(0f, total);
		foreach (FishData fish in fishPool) {
			roll -= RarityWeight(fish);
			if (roll <= 0f) {
				return fish;
			}
		}
		return fishPool[fishPool.Count - 1];
	}
	// Returns a weight for the fish based on its rarity, so rarer fish are thrown less often. If the rarity is out of range, returns 1.
	private float RarityWeight(FishData fish)
	{
		int rarity = (int)fish.Rarity;
		if (rarity < 0 || rarity >= rarityWeights.Length) {
			return 1f;
		}
		return rarityWeights[rarity];
	}
	// Called when a fish falls off the bottom of the screen. If it was meant to be cut, fires FishMissed with its data.
	private void OnFishFellOffScreen(NinjaFish fish)
	{
		if (fish.ShouldCut) {
			EmitSignal(SignalName.FishMissed, fish.Data);
		}
	}
}
