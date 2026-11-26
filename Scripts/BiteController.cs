using Godot;
using System.Collections.Generic;

// Bite logic for Gone Fishing. Once the bobber lands, fish near it can notice it (each fish's NoticeChance).
// Only one fish comes over at a time: it turns toward the bobber, swims up, nibbles a random number of times at random
// moments (0 nibbles means it bites as soon as it arrives), then bites. On harder levels some nibbles are hard nibbles that
// pull the bobber partly under, so they look close to a bite. Only the real bite dips the bobber fully under and shows the "!",
// then the rod enters its Bite state and emits FishBit. Reeling in before the bite scares the fish away,
// and pulling while it's nibbling shows "Too Early!". What happens after the bite belongs to the reel-in task.
public partial class BiteController : Node2D
{
	private enum Stage
	{
		// no bobber in the water
		Idle,        
		// bobber in the water, waiting for a fish to notice it
		Watching,    
		// the chosen fish is turning and swimming to the bobber
		Approaching, 
		// the fish is at the bobber, nibbling until it bites
		Nibbling,   
		// the fish bit, so the rod is in its Bite state
		Hooked,      
	}

	// Assigned in the Inspector
	[Export] private FishingRod fishingRod;
	[Export] private FishSpawner fishSpawner;
	[Export] private Polygon2D waterShape;
	[Export] private PackedScene biteAlertScene;
	// Loaded from Scenes/TooEarlyAlert.tscn when left empty
	[Export] private PackedScene tooEarlyAlertScene;

	[ExportGroup("Noticing")]
	// Fish within this many pixels of the bobber can notice it
	[Export] private float noticeRadius = 250f;
	// Seconds between rolls for the nearby fish to notice the bobber
	[Export] private float noticeInterval = 0.5f;
	// Difficulty settings: every fish's NoticeChance and BiteDelay are multiplied by these
	[Export] private float noticeChanceScale = 1f;
	[Export] private float biteDelayScale = 1f;
	// Each bite delay is randomly up to this fraction shorter or longer, so bites aren't predictable
	[Export] private float biteDelayVariation = 0.2f;

	[ExportGroup("Approach")]
	// Pixels per second the fish swims toward the bobber
	[Export] private float approachSpeed = 90f;
	// How quickly the fish turns to face the bobber
	[Export] private float turnSpeed = 5f;
	// Distance from a fish's center to its mouth, so its mouth ends up on the bobber. Pond fish are 64 pixels long.
	[Export] private float mouthReach = 28f;
	// How far back from the bobber the fish waits between nibbles
	[Export] private float nibbleBackoff = 14f;
	// How many times a fish nibbles before biting, picked at random. 0 means it bites as soon as it arrives,
	// so there's never a safe wait.
	[Export] private int minNibbles = 0;
	[Export] private int maxNibbles = 5;

	[ExportGroup("Bobber")]
	// Pixels the bobber jerks down when nibbled
	[Export] private float twitchDepth = 6f;
	// A hard nibble pulls the bobber this far down, this see-through, and holds it there this many seconds
	[Export] private float hardNibbleDepth = 9f;
	[Export] private float hardNibbleAlpha = 0.6f;
	[Export] private float hardNibbleHold = 0.3f;
	// Pixels the bobber sinks when the fish bites, and how see-through it gets fully under the water.
	// Kept well past a hard nibble so a real bite is always clearly different.
	[Export] private float dipDepth = 16f;
	[Export] private float dippedAlpha = 0.15f;

	[ExportGroup("Swimming Away")]
	// How far a scared fish swims from the bobber
	[Export] private float fleeDistance = 300f;

	// Close enough to the waiting spot to start nibbling
	private const float ArriveDistance = 2f;
	// Seconds for each part of a nibble, the bite lunge and the bobber's movements
	private const float NibbleInTime = 0.08f;
	private const float NibbleOutTime = 0.18f;
	private const float BiteLungeTime = 0.1f;
	private const float TwitchTime = 0.06f;
	private const float DipTime = 0.15f;
	private const float HardNibbleRiseTime = 0.2f;
	// The gaps between nibbles are random: each is this many times longer or shorter than the others, and never under MinNibbleGap seconds
	private const float MinGapWeight = 0.3f;
	private const float MaxGapWeight = 1.7f;
	private const float MinNibbleGap = 0.3f;
	private const string DefaultTooEarlyAlertPath = "res://Scenes/TooEarlyAlert.tscn";
	// A scared fish tries straight away from the bobber first, then swings this far to either side each try until it finds water
	private const float FleeAngleStep = Mathf.Pi / 6f;
	private const int FleeAngleSteps = 6;

	private readonly RandomNumberGenerator random = new RandomNumberGenerator();
	private Stage stage = Stage.Idle;
	private Vector2 bobberPosition;
	private float noticeTimer;
	private Fish fish;
	private Tween schedule;
	private Tween fishMotion;
	private Tween bobberMotion;
	private BiteAlert alert;
	private bool listening;

	// Checks the Inspector slots, then listens to the rod for casts landing and the line coming back in
	public override void _Ready()
	{
		if (fishingRod == null || fishSpawner == null || waterShape == null || biteAlertScene == null) {
			GD.PushError("BiteController: assign the fishing rod, fish spawner, water shape and bite alert scene in the Inspector");
			SetProcess(false);
			return;
		}

		random.Randomize();
		tooEarlyAlertScene ??= GD.Load<PackedScene>(DefaultTooEarlyAlertPath);
		fishingRod.CastLanded += OnCastLanded;
		fishingRod.StateChanged += OnRodStateChanged;
		listening = true;
	}

	public override void _ExitTree()
	{
		if (listening) {
			fishingRod.CastLanded -= OnCastLanded;
			fishingRod.StateChanged -= OnRodStateChanged;
		}
	}

	// While watching, rolls for a fish to notice the bobber every noticeInterval. While approaching, swims the fish in.
	public override void _Process(double delta)
	{
		// Something else removed the fish before it bit, so go back to waiting for another one
		if ((stage == Stage.Approaching || stage == Stage.Nibbling) && !IsInstanceValid(fish)) {
			StopFishMotion();
			ResetBobber();
			fish = null;
			stage = Stage.Watching;
		}

		if (stage == Stage.Watching) {
			noticeTimer -= (float)delta;
			if (noticeTimer <= 0f) {
				noticeTimer = noticeInterval;
				TryNotice();
			}
		} else if (stage == Stage.Approaching) {
			Approach((float)delta);
		}
	}

	// The bobber is in the water, so start watching for a fish
	private void OnCastLanded(Vector2 position)
	{
		ResetBobber();

		bobberPosition = position;
		noticeTimer = noticeInterval;
		stage = Stage.Watching;
	}

	// Anything that takes the bobber out of the water (reeling in) ends the bite. A fish on its way swims off scared.
	// A hooked fish is let go here too for now, until the reel-in task decides what happens to it.
	private void OnRodStateChanged(int newState)
	{
		FishingRod.RodState state = (FishingRod.RodState)newState;
		if (stage == Stage.Idle || state == FishingRod.RodState.InWater || state == FishingRod.RodState.Bite) 
		{
			return;
		}

		// If fish is already hooed, keep it attached for the reel animation.
		if (stage == Stage.Hooked && state == FishingRod.RodState.Reeling)
		{
			StopFishMotion();
			bobberMotion?.Kill();
			stage = Stage.Idle;

			if (alert != null)
			{
				alert.Dismiss();
				alert = null;
			}

			return;
		}

		// Pulling while the fish is only nibbling spooks it, and the line comes back in so the player has to cast again
		if (stage == Stage.Nibbling && state == FishingRod.RodState.Reeling)
		{
			ShowTooEarly();
		}

		LetFishGo();
		ResetBobber();

		if (alert != null)
		{
			alert.Dismiss();
			alert = null;
		}

		stage = Stage.Idle;
	}

	// Rolls each fish near the bobber, closest first. The first one that notices it is the only fish that comes over.
	private void TryNotice()
	{
		List<Fish> nearby = new List<Fish>();
		foreach (Node child in fishSpawner.GetChildren()) {
			// Fading fish turn off Monitorable so they can't be caught
			if (child is Fish candidate && candidate.Monitorable && candidate.Data != null
					&& candidate.GlobalPosition.DistanceTo(bobberPosition) <= noticeRadius) {
				nearby.Add(candidate);
			}
		}
		nearby.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(bobberPosition).CompareTo(b.GlobalPosition.DistanceSquaredTo(bobberPosition)));

		foreach (Fish candidate in nearby) {
			if (random.Randf() < candidate.Data.NoticeChance * noticeChanceScale) {
				fish = candidate;
				fish.IsBiting = true;
				stage = Stage.Approaching;
				return;
			}
		}
	}

	// Turns the fish toward the bobber and swims it to a spot just short of it. It only picks up speed once it's facing the bobber.
	private void Approach(float delta)
	{
		float facingAngle = DirectionToBobber().Angle();
		fish.Rotation = Mathf.LerpAngle(fish.Rotation, facingAngle, turnSpeed * delta);

		Vector2 waitSpot = bobberPosition - DirectionToBobber() * (mouthReach + nibbleBackoff);
		float facing = Mathf.Max(0f, Mathf.Cos(Mathf.AngleDifference(fish.Rotation, facingAngle)));
		fish.GlobalPosition = fish.GlobalPosition.MoveToward(waitSpot, approachSpeed * facing * delta);

		if (fish.GlobalPosition.DistanceTo(waitSpot) <= ArriveDistance) {
			StartNibbling();
		}
	}

	// The fish nibbles a random number of times at random moments through its bite delay, then bites.
	// With 0 nibbles it bites as soon as it arrives. Each nibble can be a hard nibble, by the level's HardNibbleChance.
	private void StartNibbling()
	{
		stage = Stage.Nibbling;
		Vector2 direction = DirectionToBobber();
		fish.Rotation = direction.Angle();
		Vector2 waitSpot = bobberPosition - direction * (mouthReach + nibbleBackoff);
		Vector2 mouthSpot = bobberPosition - direction * mouthReach;

		int nibbles = random.RandiRange(minNibbles, maxNibbles);
		if (nibbles == 0) {
			Bite(mouthSpot);
			return;
		}

		float variation = 1f + random.RandfRange(-biteDelayVariation, biteDelayVariation);
		float delay = Mathf.Max(0f, fish.Data.BiteDelay * biteDelayScale * variation);
		float hardNibbleChance = fishSpawner.CurrentLevelSettings?.HardNibbleChance ?? 0f;
		float[] gaps = RandomGaps(nibbles + 1, delay);
		// A hard nibble holds the bobber down for a while, so whatever comes next waits until it's back up
		float hardNibbleTime = DipTime + hardNibbleHold + HardNibbleRiseTime;

		schedule = CreateTween();
		bool lastWasHard = false;
		for (int i = 0; i < nibbles; i++) {
			bool hard = random.Randf() < hardNibbleChance;
			schedule.TweenInterval(Mathf.Max(gaps[i], lastWasHard ? hardNibbleTime : MinNibbleGap));
			schedule.TweenCallback(Callable.From(() => Nibble(waitSpot, mouthSpot, hard)));
			lastWasHard = hard;
		}
		schedule.TweenInterval(Mathf.Max(gaps[nibbles], lastWasHard ? hardNibbleTime : MinNibbleGap));
		schedule.TweenCallback(Callable.From(() => Bite(mouthSpot)));
	}

	// Splits the bite delay into random gaps that add up to it, so players can't time the bite by counting
	private float[] RandomGaps(int count, float total)
	{
		float[] gaps = new float[count];
		float sum = 0f;
		for (int i = 0; i < count; i++) {
			gaps[i] = random.RandfRange(MinGapWeight, MaxGapWeight);
			sum += gaps[i];
		}
		for (int i = 0; i < count; i++) {
			gaps[i] = gaps[i] / sum * total;
		}
		return gaps;
	}

	// The fish darts in to the bobber and back out. A normal nibble twitches the bobber. A hard nibble holds on and
	// pulls the bobber partly under, close to a bite, but there's no "!" and the bobber comes back up.
	private void Nibble(Vector2 waitSpot, Vector2 mouthSpot, bool hard)
	{
		fishMotion?.Kill();
		fishMotion = CreateTween();
		fishMotion.TweenProperty(fish, "global_position", mouthSpot, NibbleInTime);
		if (hard) {
			fishMotion.TweenCallback(Callable.From(PullBobberUnder));
			fishMotion.TweenInterval(DipTime + hardNibbleHold);
		} else {
			fishMotion.TweenCallback(Callable.From(TwitchBobber));
		}
		fishMotion.TweenProperty(fish, "global_position", waitSpot, NibbleOutTime);
	}

	// A hard nibble: the bobber sinks partway and fades a little, holds there, then floats back up
	private void PullBobberUnder()
	{
		Sprite2D bobber = fishingRod.Bobber;
		bobberMotion?.Kill();
		bobberMotion = CreateTween().SetParallel();
		bobberMotion.TweenProperty(bobber, "offset", BobberOffset(hardNibbleDepth), DipTime);
		bobberMotion.TweenProperty(bobber, "self_modulate:a", hardNibbleAlpha, DipTime);
		bobberMotion.Chain().TweenInterval(hardNibbleHold);
		bobberMotion.Chain().TweenProperty(bobber, "offset", Vector2.Zero, HardNibbleRiseTime);
		bobberMotion.TweenProperty(bobber, "self_modulate:a", 1f, HardNibbleRiseTime);
	}

	// "Too Early!" over the bobber when the player pulls while the fish is only nibbling
	private void ShowTooEarly()
	{
		if (tooEarlyAlertScene == null) {
			return;
		}
		TooEarlyAlert tooEarly = tooEarlyAlertScene.Instantiate<TooEarlyAlert>();
		AddChild(tooEarly);
		tooEarly.GlobalPosition = bobberPosition;
	}

	private void TwitchBobber()
	{
		Sprite2D bobber = fishingRod.Bobber;
		bobberMotion?.Kill();
		bobberMotion = CreateTween();
		bobberMotion.TweenProperty(bobber, "offset", BobberOffset(twitchDepth), TwitchTime);
		bobberMotion.TweenProperty(bobber, "offset", Vector2.Zero, TwitchTime);
	}

	// The fish lunges onto the bobber and bites: the bobber dips under, the "!" pops up and the rod enters its Bite state
	private void Bite(Vector2 mouthSpot)
	{
		stage = Stage.Hooked;

		fishMotion?.Kill();
		bobberMotion?.Kill();

		// Register the fish as hooked immediately.
		fishingRod.Bite(fish);

		Sprite2D bobber = fishingRod.Bobber;

        // Move the fish and dip the bobber at the same time.
		fishMotion = CreateTween().SetParallel();
		bobberMotion = CreateTween().SetParallel();

		fishMotion.TweenProperty(fish, "global_position", mouthSpot, BiteLungeTime);
		
		bobberMotion.TweenProperty(bobber, "offset", BobberOffset(dipDepth), DipTime);
		bobberMotion.TweenProperty(bobber, "self_modulate:a", dippedAlpha, DipTime);

		fishMotion.Chain().TweenCallback(Callable.From(() =>
		{
			if (!IsInstanceValid(fish))
			{
				return;
			}
			alert = biteAlertScene.Instantiate<BiteAlert>();
			AddChild(alert);
			alert.GlobalPosition = bobberPosition;
		}));

	}

	// Hands the fish back to its own swimming, darting away from the bobber
	private void LetFishGo()
	{
		StopFishMotion();
		if (IsInstanceValid(fish)) {
			fish.IsBiting = false;
			fish.SetTarget(FleeSpot());
		}
		fish = null;
	}

	// A spot in the water away from the bobber for a scared fish to swim to
	private Vector2 FleeSpot()
	{
		Vector2 away = fish.GlobalPosition - bobberPosition;
		float awayAngle = fish.Rotation + Mathf.Pi;
		if (away != Vector2.Zero) {
			awayAngle = away.Angle();
		}

		for (int step = 0; step <= FleeAngleSteps; step++) {
			foreach (int side in new[] { 1, -1 }) {
				Vector2 spot = fish.GlobalPosition + Vector2.FromAngle(awayAngle + side * step * FleeAngleStep) * fleeDistance;
				if (IsInWater(spot)) {
					return spot;
				}
			}
		}
		// Boxed in by the shore, so stay put and let the spawner pick the next spot
		return fish.GlobalPosition;
	}

	private void StopFishMotion()
	{
		schedule?.Kill();
		fishMotion?.Kill();
	}

	// Puts the bobber back to normal after a twitch or dip
	private void ResetBobber()
	{
		bobberMotion?.Kill();
		fishingRod.Bobber.Offset = Vector2.Zero;
		fishingRod.Bobber.SelfModulate = Colors.White;
	}

	// The bobber's Offset is in its own unscaled pixels, so a screen distance is divided by its scale
	private Vector2 BobberOffset(float screenPixels)
	{
		return new Vector2(0f, screenPixels / fishingRod.Bobber.Scale.Y);
	}

	private Vector2 DirectionToBobber()
	{
		Vector2 toBobber = bobberPosition - fish.GlobalPosition;
		if (toBobber == Vector2.Zero) {
			return Vector2.FromAngle(fish.Rotation);
		}
		return toBobber.Normalized();
	}

	private bool IsInWater(Vector2 globalPoint)
	{
		return Geometry2D.IsPointInPolygon(waterShape.ToLocal(globalPoint), waterShape.Polygon);
	}
}
