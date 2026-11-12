using Godot;

// Wiimote calibration. Shows five targets one at a time 
// The player points at each one and presses A.
// The raw IR readings are then fitted to the targets to work out WiimoteManager's sensitivity and center offset, followed by a quick "point at the target" check and a choice to retry or accept. 
// Accepting saves the values in SaveManager, which get loaded back in at startup.
// Make sure to aim for the center of each target and not the edges
// Without a Wiimote connected, the mouse stands in for the IR camera.
public partial class CalibrationScreen : CanvasLayer
{
	// Emitted when the player accepts or cancels the calibration. The bool is true if accepted, false if canceled.
	[Signal] public delegate void FinishedEventHandler(bool accepted);
	// The calibration process has three steps: capturing the five targets, checking the result, and done (accept or retry). The step is used to decide what to do when the player presses A.
	private enum Step
	{
		Capturing,
		Checking,
		Done,
	}
	// Corner targets sit this far in from the edges of the screen
	private const float CornerInset = 150f;
	// Where the final check target appears as a fraction of the screen size. The check target is deliberately not at the center so the calibration is tested in a different part of the screen.
	private static readonly Vector2 CheckTargetSpot = new Vector2(0.3f, 0.65f);
	// The pointer must land within this many pixels of the check target to pass
	private const float CheckTolerance = 90f;
	// If the readings spread out less than this, the remote barely moved between targets and there's nothing to fit
	private const float MinReadingSpread = 1f;

	// Pretend remote used when no Wiimote is connected. Its readings are deliberately a bit off so calibration has something to correct and can be tested with a mouse.
	private static readonly Vector2 SimulatedRange = new Vector2(0.5f, 0.6f);
	private static readonly Vector2 SimulatedShift = new Vector2(40f, -30f);

	// Same IR camera and screen sizes WiimoteManager uses, so the math here matches its mapping
	private static readonly Vector2 IrCameraSize = new Vector2(1023f, 767f);
	private static readonly Vector2 ScreenSize = new Vector2(1920f, 1080f);
	// A remote in someone's hand always jitters a little, so a pointer that hasn't moved for this long means the remote can't see the sensor bar
	private const ulong SignalTimeoutMs = 500;

	// Assigned in the Inspector
	[Export] private Control targetMarker;
	[Export] private Control checkMarker;
	[Export] private Label instructionsLabel;
	[Export] private Label progressLabel;
	[Export] private Label resultLabel;
	[Export] private Control buttonRow;
	[Export] private Button retryButton;
	[Export] private Button acceptButton;

	private WiimoteManager wiimote;
	private CanvasLayer cursorLayer;
	private Vector2[] targets;
	private Vector2[] recorded;
	private int index;
	private Step step;
	private Vector2 checkTarget;
	private Vector2 oldSensitivity;
	private Vector2 oldCenterOffset;
	private Vector2 newSensitivity;
	private Vector2 newCenterOffset;
	private ulong lastPressFrame = ulong.MaxValue;
	private bool usingRemote;
	private Vector2 lastRemoteCursor;
	private ulong lastRemoteMoveMs;
	// Checks all the slots are filled and the WiimoteManager autoload is present, then sets up the targets and starts capturing. Also remembers the current calibration for retry and cancel while subscribing to WiimoteManager's A button event.
	public override void _Ready()
	{
		if (targetMarker == null || checkMarker == null || instructionsLabel == null || progressLabel == null
				|| resultLabel == null || buttonRow == null || retryButton == null || acceptButton == null) {
			GD.PushError("CalibrationScreen: assign all the node slots in the Inspector");
			return;
		}

		wiimote = GetTree().Root.GetNodeOrNull<WiimoteManager>("WiimoteManager");
		if (wiimote == null) {
			GD.PushError("CalibrationScreen: the WiimoteManager autoload is missing");
			return;
		}
		cursorLayer = GetTree().Root.GetNodeOrNull<CanvasLayer>("Cursor");

		// WiimoteManager's cursor position stays at (0, 0) until a remote sends a reading
		usingRemote = wiimote.CursorPosition != Vector2.Zero;
		lastRemoteCursor = wiimote.CursorPosition;

		oldSensitivity = wiimote.Sensitivity;
		oldCenterOffset = wiimote.CenterOffset;
		wiimote.OnWiimoteAClicked += OnAPressed;

		Vector2 size = GetViewport().GetVisibleRect().Size;
		targets = new Vector2[] {
			new Vector2(CornerInset, CornerInset),
			new Vector2(size.X - CornerInset, CornerInset),
			new Vector2(size.X - CornerInset, size.Y - CornerInset),
			new Vector2(CornerInset, size.Y - CornerInset),
			size / 2,
		};
		recorded = new Vector2[targets.Length];
		checkTarget = size * CheckTargetSpot;

		StartCapture();
	}
	// Unsubscribe from WiimoteManager's A button event and restore the crosshair when leaving the scene tree
	public override void _ExitTree()
	{
		if (wiimote != null) {
			wiimote.OnWiimoteAClicked -= OnAPressed;
		}
		SetCrosshairVisible(true);
	}
	// Handle left click and E key as A, and Escape key as back. The buttons are picked with A wherever the pointer is, so a real remote can use them too.
	public override void _Input(InputEvent @event)
	{
		if (wiimote == null) {
			return;
		}

		if (@event.IsActionPressed("cursor_primary")) {
			GetViewport().SetInputAsHandled();
			OnAPressed();
		} else if (@event.IsActionPressed("back")) {
			GetViewport().SetInputAsHandled();
			Cancel();
		}
	}
	public override void _Process(double delta)
	{
		// Remember when the remote's pointer last moved, to tell when it can't see the sensor bar (see SignalTimeoutMs)
		if (usingRemote && wiimote.CursorPosition != lastRemoteCursor) {
			lastRemoteCursor = wiimote.CursorPosition;
			lastRemoteMoveMs = Time.GetTicksMsec();
		}

		// Live preview of where the new calibration puts the pointer
		if (step == Step.Checking && TryReadRawIr(out Vector2 raw)) {
			MoveMarker(checkMarker, MapIrToScreen(raw, newSensitivity, newCenterOffset));
		}
	}

	// Fits the sensitivity and center offset that make MapIrToScreen send each recorded reading as close as possible to its target.
	// Returns false if the readings can't produce a usable calibration.
	public static bool TrySolve(Vector2[] rawIr, Vector2[] screenTargets, out Vector2 sensitivity, out Vector2 centerOffset)
	{
		sensitivity = Vector2.One;
		centerOffset = Vector2.Zero;
		int count = rawIr.Length;
		if (count < 2 || count != screenTargets.Length) {
			return false;
		}

		// MapIrToScreen is a straight line on the mirrored reading: screen = mirrored * slope + intercept
		Vector2 camera = IrCameraSize;
		Vector2 meanIr = Vector2.Zero;
		Vector2 meanScreen = Vector2.Zero;
		for (int i = 0; i < count; i++) {
			meanIr += Mirror(rawIr[i]);
			meanScreen += screenTargets[i];
		}
		meanIr /= count;
		meanScreen /= count;

		Vector2 covariance = Vector2.Zero;
		Vector2 variance = Vector2.Zero;
		for (int i = 0; i < count; i++) {
			Vector2 ir = Mirror(rawIr[i]) - meanIr;
			covariance += ir * (screenTargets[i] - meanScreen);
			variance += ir * ir;
		}
		// The remote barely moved between targets, so there's nothing to fit
		if (variance.X < MinReadingSpread || variance.Y < MinReadingSpread) {
			return false;
		}

		Vector2 slope = covariance / variance;
		Vector2 intercept = meanScreen - slope * meanIr;

		// Turn the line back into WiimoteManager's terms
		Vector2 pixelsPerCameraUnit = ScreenSize / camera;
		Vector2 cameraCenter = camera / 2;
		sensitivity = slope / pixelsPerCameraUnit;
		centerOffset = cameraCenter * pixelsPerCameraUnit * (Vector2.One - sensitivity) - intercept;

		// A negative sensitivity means the targets came out flipped (pointed at in the wrong order)
		return sensitivity.X > 0f && sensitivity.Y > 0f && sensitivity.IsFinite() && centerOffset.IsFinite();
	}

	// The camera sees the screen mirrored left to right; MapIrToScreen flips X back the same way
	private static Vector2 Mirror(Vector2 rawIr)
	{
		return new Vector2(IrCameraSize.X - rawIr.X, rawIr.Y);
	}

	// Same math as WiimoteManager's reading loop: turns a raw IR reading into a screen position
	private static Vector2 MapIrToScreen(Vector2 rawIr, Vector2 sensitivity, Vector2 centerOffset)
	{
		Vector2 cameraCenter = IrCameraSize / 2;
		Vector2 centered = (Mirror(rawIr) - cameraCenter) * sensitivity;
		return (centered + cameraCenter) / IrCameraSize * ScreenSize - centerOffset;
	}

	// MapIrToScreen run backwards: the raw IR reading that WiimoteManager turned into this screen position
	private static Vector2 ScreenToIr(Vector2 screenPosition, Vector2 sensitivity, Vector2 centerOffset)
	{
		Vector2 cameraCenter = IrCameraSize / 2;
		Vector2 centered = (screenPosition + centerOffset) / ScreenSize * IrCameraSize - cameraCenter;
		return Mirror(centered / sensitivity + cameraCenter);
	}

	// Called once at startup (by PauseMenu) so players don't need to recalibrate every time they play
	public static void ApplySavedCalibration(WiimoteManager wiimote)
	{
		if (SaveManager.Instance != null && SaveManager.Instance.HasWiimoteCalibration) {
			ApplyCalibration(wiimote, SaveManager.Instance.WiimoteSensitivity, SaveManager.Instance.WiimoteCenterOffset);
		}
	}

	// Sets the values WiimoteManager uses for every reading from then on
	private static void ApplyCalibration(WiimoteManager wiimote, Vector2 sensitivity, Vector2 centerOffset)
	{
		wiimote.Sensitivity = sensitivity;
		wiimote.CenterOffset = centerOffset;
	}

	// Turns a screen position into the raw IR reading the pretend remote would give: mirrored left to right like the real camera, squeezed into part of its view and shifted
	private static Vector2 SimulatedIr(Vector2 screenPosition)
	{
		Vector2 camera = IrCameraSize;
		Vector2 screen = ScreenSize;
		Vector2 fromCenter = (screenPosition - screen / 2) / screen * camera * SimulatedRange;
		return new Vector2(camera.X / 2 - fromCenter.X, camera.Y / 2 + fromCenter.Y) + SimulatedShift;
	}
	// Starts the capture step, restoring the old calibration and showing the first target. The player points at each target in turn and presses A to record it.
	private void StartCapture()
	{
		step = Step.Capturing;
		index = 0;
		ApplyCalibration(wiimote, oldSensitivity, oldCenterOffset);

		// With a real remote, hide the crosshair so the player points the remote itself at each target instead of steering an uncalibrated crosshair onto it. 
		// With the mouse, the crosshair is where the "remote" points, so it stays.
		SetCrosshairVisible(!usingRemote);

		if (usingRemote) {
			instructionsLabel.Text = "Point the Wii Remote at the center of each target and press A.";
		} else {
			instructionsLabel.Text = "No Wii Remote found, so the mouse stands in for it. Point at the center of each target and click (or press E).";
		}
		resultLabel.Text = "";
		checkMarker.Visible = false;
		buttonRow.Visible = false;
		ShowTarget();
	}
	// Moves the target marker to the current target and updates the progress label. Called at the start of capture and after each target is recorded.
	private void ShowTarget()
	{
		MoveMarker(targetMarker, targets[index]);
		progressLabel.Text = $"Target {index + 1} of {targets.Length}";
	}
	// Called when the player presses A. Depending on the step, it either records a target, checks the calibration, or presses a button under the pointer.
	private void OnAPressed()
	{
		// A left click arrives twice, so only take one per frame since it fires both cursor_primary and WiimoteManager.OnWiimoteAClicked in the same frames.
		ulong frame = Engine.GetProcessFrames();
		if (frame == lastPressFrame) {
			return;
		}
		lastPressFrame = frame;

		if (step == Step.Capturing) {
			CaptureTarget();
		} else if (step == Step.Checking) {
			FinishCheck();
		} else {
			PressButtonUnderPointer();
		}
	}
	// Reads the current raw IR reading and stores it in the recorded array. If all targets have been recorded, it fits a new sensitivity and center offset. If that fails, it shows an error and lets the player retry. Otherwise, it starts the check step.
	private void CaptureTarget()
	{
		if (!TryReadRawIr(out Vector2 raw)) {
			resultLabel.Text = "The remote can't see the sensor bar. Point it at the screen and try again.";
			return;
		}

		resultLabel.Text = "";
		recorded[index] = raw;
		index++;
		if (index < targets.Length) {
			ShowTarget();
			return;
		}

		if (!TrySolve(recorded, targets, out newSensitivity, out newCenterOffset)) {
			instructionsLabel.Text = "That calibration didn't work.";
			resultLabel.Text = "Make sure to point at each target in turn, then try again.";
			targetMarker.Visible = false;
			ShowChoice(false);
			return;
		}
		StartCheck();
	}
	// Applies the new calibration and shows the check target. The player points at it and presses A to see how close the pointer lands.
	private void StartCheck()
	{
		step = Step.Checking;
		ApplyCalibration(wiimote, newSensitivity, newCenterOffset);
		SetCrosshairVisible(true);

		MoveMarker(targetMarker, checkTarget);
		checkMarker.Visible = true;
		instructionsLabel.Text = "Last step: point at the target and press A to check the calibration.";
		progressLabel.Text = "Check";
	}
	// Reads the current raw IR reading, maps it to the screen with the new calibration, and compares it to the check target. Shows how far off it was and lets the player accept or retry.
	private void FinishCheck()
	{
		if (!TryReadRawIr(out Vector2 raw)) {
			resultLabel.Text = "The remote can't see the sensor bar. Point it at the screen and try again.";
			return;
		}

		float miss = MapIrToScreen(raw, newSensitivity, newCenterOffset).DistanceTo(checkTarget);
		if (miss <= CheckTolerance) {
			resultLabel.Text = $"Looks good! The pointer landed {miss:0} pixels from the target.";
		} else {
			resultLabel.Text = $"The pointer landed {miss:0} pixels from the target, so you may want to retry.";
		}
		instructionsLabel.Text = "Accept this calibration, or retry it.";
		ShowChoice(true);
	}
	// Shows the retry and accept buttons. Accept is hidden only when the calibration couldn't be worked out
	private void ShowChoice(bool canAccept)
	{
		step = Step.Done;
		buttonRow.Visible = true;
		acceptButton.Visible = canAccept;
		// A failed calibration skips the check step, so the crosshair could still be hidden; the player needs it to aim at the buttons
		SetCrosshairVisible(true);
	}

	// The buttons are picked with A wherever the pointer is, so a real remote can use them too
	private void PressButtonUnderPointer()
	{
		Vector2 pointer = GetPointer();
		if (retryButton.IsVisibleInTree() && retryButton.GetGlobalRect().HasPoint(pointer)) {
			StartCapture();
		} else if (acceptButton.IsVisibleInTree() && acceptButton.GetGlobalRect().HasPoint(pointer)) {
			Accept();
		}
	}
	// Saves the new calibration in SaveManager and emits the Finished signal with true. The scene that opened the calibration screen can then close it and return to the pause menu.
	private void Accept()
	{
		if (SaveManager.Instance != null) {
			SaveManager.Instance.WiimoteSensitivity = newSensitivity;
			SaveManager.Instance.WiimoteCenterOffset = newCenterOffset;
		}
		EmitSignal(SignalName.Finished, true);
	}
	// Restores the old calibration and emits the Finished signal with false. The scene that opened the calibration screen can then close it and return to the pause menu.
	private void Cancel()
	{
		ApplyCalibration(wiimote, oldSensitivity, oldCenterOffset);
		EmitSignal(SignalName.Finished, false);
	}
	// Reads the current raw IR reading from the WiimoteManager. If no Wiimote is connected, it simulates a reading based on where the mouse pointer is. Returns true if a reading was obtained, false if the Wiimote can't see the sensor bar.
	private bool TryReadRawIr(out Vector2 raw)
	{
		if (usingRemote) {
			// WiimoteManager only shares the screen position, so work back to the raw reading it came from
			raw = ScreenToIr(wiimote.CursorPosition, wiimote.Sensitivity, wiimote.CenterOffset);
			return Time.GetTicksMsec() - lastRemoteMoveMs < SignalTimeoutMs;
		}
		raw = SimulatedIr(GetPointer());
		return true;
	}
	// Returns the current pointer position in screen coordinates.
	private Vector2 GetPointer()
	{
		if (InputManager.Instance != null) {
			return InputManager.Instance.PointerPosition;
		}
		return GetViewport().GetMousePosition();
	}
	// Moves a marker to the given screen position and makes it visible. The marker's position is its top left corner, so the center is adjusted to match the given position.
	private static void MoveMarker(Control marker, Vector2 center)
	{
		marker.Position = center - marker.Size / 2;
		marker.Visible = true;
	}
	// Shows or hides the crosshair layer. The crosshair is hidden during capture with a real remote so the player points the remote itself at each target instead of steering an uncalibrated crosshair onto it. With the mouse, the crosshair is where the "remote" points, so it stays.
	private void SetCrosshairVisible(bool visible)
	{
		if (cursorLayer != null) {
			cursorLayer.Visible = visible;
		}
	}
}
