using Godot;
using System.Collections.Generic;

public partial class SliceDetector : Node2D
{
	// References to the cursor, fish launcher, and HUD assigned in the Inspector.
	[Export] private CanvasLayer cursorLayer;
	private CursorNode cursor;
	[Export] private FishLauncher fishLauncher;
	[Export] private GameHud gameHud;

	// Minimum swipe speed required to slice fish, measured in pixels per second.
	[Export] private float minimumSwipeSpeed = 500f;

	// How many seconds of cursor movement are used to calculate swipe speed.
	[Export] private float historyDuration = 0.15f;

	// Controls the thickness of the blade trail.
	[Export] private float trailWidth = 12f;

	// Controls how long each trail point remains visible.
	[Export] private float trailDuration = 0.2f;

	private Line2D bladeTrail;
	private bool slicing;
	private Vector2 previousPosition;
	private double elapsedTime;

	private readonly List<SwipeSample> swipeHistory = new List<SwipeSample>();
	private readonly List<TrailSample> trailSamples = new List<TrailSample>();
	private readonly HashSet<NinjaFish> slicedFish = new HashSet<NinjaFish>();

	// Stores a cursor position and the time it was recorded.
	private struct SwipeSample
	{
		public Vector2 Position;
		public double Time;

		// Creates a swipe sample containing its position and timestamp.
		public SwipeSample(Vector2 position, double time)
		{
			Position = position;
			Time = time;
		}
	}

	// Stores a blade trail position and the time it was recorded.
	private struct TrailSample
	{
		public Vector2 Position;
		public double Time;

		public TrailSample(Vector2 position, double time)
		{
			Position = position;
			Time = time;
		}
	}

	// Checks the required references, creates the blade trail, and connects cursor input signals.
	public override void _Ready()
	{
		if (cursorLayer != null)
		{
			cursor = cursorLayer.GetNode<CursorNode>("CursorNode");
		}

		if (cursor == null || fishLauncher == null || gameHud == null)
		{
			GD.PushError("SliceDetector: assign the cursor, fish launcher and HUD in the Inspector.");
			SetProcess(false);
			return;
		}

		bladeTrail = new Line2D();
		bladeTrail.Width = trailWidth;
		bladeTrail.DefaultColor = Colors.White;

		Gradient trailGradient = new Gradient();
		trailGradient.SetColor(0, new Color(1f, 1f, 1f, 0f));
		trailGradient.SetColor(1, new Color(1f, 1f, 1f, 0f));
		trailGradient.AddPoint(0.15f, Colors.White);
		trailGradient.AddPoint(0.85f, Colors.White);

		// Tapers the sword slash to a point at both ends.
		bladeTrail.Gradient = trailGradient;
		Curve trailCurve = new Curve();
		trailCurve.AddPoint(new Vector2(0f, 0f));
		trailCurve.AddPoint(new Vector2(0.15f, 0.7f));
		trailCurve.AddPoint(new Vector2(0.5f, 1f));
		trailCurve.AddPoint(new Vector2(0.85f, 0.7f));
		trailCurve.AddPoint(new Vector2(1f, 0f));
		bladeTrail.WidthCurve = trailCurve;

		AddChild(bladeTrail);

		cursor.PrimaryPressed += OnPrimaryPressed;
		cursor.PrimaryReleased += OnPrimaryReleased;
	}

	// Tracks cursor movement, calculates swipe speed, checks for fish intersections, and updates the fading trail.
	public override void _Process(double delta)
	{
		elapsedTime += delta;

		while (trailSamples.Count > 0 &&
			   elapsedTime - trailSamples[0].Time > trailDuration)
		{
			trailSamples.RemoveAt(0);
		}

		bladeTrail.ClearPoints();

		foreach (TrailSample sample in trailSamples)
		{
			bladeTrail.AddPoint(ScreenToLocal(sample.Position));
		}

		if (!slicing)
		{
			return;
		}

		Vector2 currentPosition = cursor.ScreenPosition;

		if (currentPosition.DistanceSquaredTo(previousPosition) < 1f)
		{
			return;
		}

		swipeHistory.Add(new SwipeSample(currentPosition, elapsedTime));

		while (swipeHistory.Count > 1 &&
			   elapsedTime - swipeHistory[0].Time > historyDuration)
		{
			swipeHistory.RemoveAt(0);
		}

		if (swipeHistory.Count >= 2)
		{
			SwipeSample oldest = swipeHistory[0];
			double timePassed = elapsedTime - oldest.Time;

			if (timePassed > 0.001)
			{
				float speed = oldest.Position.DistanceTo(currentPosition) / (float)timePassed;

				if (speed >= minimumSwipeSpeed)
				{
					CheckForSlices(previousPosition, currentPosition);
				}
			}
		}

		trailSamples.Add(new TrailSample(currentPosition, elapsedTime));
		bladeTrail.AddPoint(ScreenToLocal(currentPosition));

		previousPosition = currentPosition;
	}

	// Starts a new swipe, resets its movement history, and begins drawing the blade trail.
	private void OnPrimaryPressed(Vector2 position)
	{
		slicing = true;
		previousPosition = position;

		swipeHistory.Clear();
		swipeHistory.Add(new SwipeSample(position, elapsedTime));

		trailSamples.Clear();
		trailSamples.Add(new TrailSample(position, elapsedTime));

		bladeTrail.ClearPoints();
		bladeTrail.AddPoint(ScreenToLocal(position));
	}

	// Stops slicing and clears the swipe history while allowing the trail to fade.
	private void OnPrimaryReleased(Vector2 position)
	{
		slicing = false;
		swipeHistory.Clear();
	}

	// Checks active fish for intersections with the current swipe and awards points to sliced fish.
	private void CheckForSlices(Vector2 from, Vector2 to)
	{
		foreach (NinjaFish fish in fishLauncher.InFlight)
		{
			if (fish == null || !GodotObject.IsInstanceValid(fish))
			{
				continue;
			}

			if (slicedFish.Contains(fish) || !fish.ShouldCut)
			{
				continue;
			}

			if (!fish.IntersectsSwipe(from, to))
			{
				continue;
			}

			slicedFish.Add(fish);
			fish.TreeExited += () => slicedFish.Remove(fish);

			gameHud.AddScore(fish.Data.Points);
			fish.Split(to - from);
		}
	}

	// Converts a viewport screen position into the local coordinate space used by the blade trail.
	private Vector2 ScreenToLocal(Vector2 position)
	{
		return GetGlobalTransformWithCanvas().AffineInverse() * position;
	}
}
