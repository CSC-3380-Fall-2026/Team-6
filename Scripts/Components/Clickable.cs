using Godot;

// Base class for everything involving the minigames
// Extend it ("public partial class Fish : Clickable") and override OnClicked().
// Needs a CollisionShape2D child for its clickable area and a Sprite2D 
// AnimatedSprite2D assigned to visual in the Inspector.
// If a subclass overrides _Ready or _PhysicsProcess call the base version first.
public partial class Clickable : Area2D
{
	public enum HighlightStyle { Outline, Glow, None }

	[Signal] public delegate void HoveredEventHandler();
	[Signal] public delegate void UnhoveredEventHandler();
	[Signal] public delegate void ClickedEventHandler();

	private const string OutlineShaderPath = "res://Shaders/ClickableOutline.gdshader";

	// Assigned in the Inspector, this makes the sprite glow or outline when hovered. If null the sprite switches off
	[Export] private Node2D visual;
	[Export] private bool clickable = true;
	[Export] private HighlightStyle highlightStyle = HighlightStyle.Outline;
	[Export] private Color outlineColor = new Color(1f, 0.85f, 0.2f);
	[Export] private float outlineWidth = 1f;
	[Export] private float glowStrength = 1.4f;
	[Export] private float clickPulseScale = 1.15f;
	[Export] private float clickPulseGrowTime = 0.06f;
	[Export] private float clickPulseShrinkTime = 0.1f;
	// This is for when the game gets sound
	[Export] private AudioStreamPlayer2D clickSound;

	private ShaderMaterial outlineMaterial;
	private Color baseSelfModulate;
	private Vector2 baseVisualScale;
	private Tween clickTween;

	public bool IsHovered { get; private set; }

	// Turn off switch for things that don't need to change when hovered, like a fish that's already caught
	public bool IsClickable
	{
		get => clickable;
		set
		{
			clickable = value;
			if (!clickable && IsHovered) {
				SetHovered(false);
			}
		}
	}
	// This is for if the visual slot is empty, saving the sprite's original color and size
	public override void _Ready()
	{
		if (visual == null) {
			GD.PushError($"{Name}: assign the visual in the Inspector");
			SetPhysicsProcess(false);
			return;
		}

		baseSelfModulate = visual.SelfModulate;
		baseVisualScale = visual.Scale;

		if (highlightStyle == HighlightStyle.Outline) {
			SetUpOutline();
		}
	}
	// Tick to check if the object is hovered or clicked
	public override void _PhysicsProcess(double delta)
	{
		bool hovered = clickable && IsVisibleInTree() && !IsPointerOverUi() && IsTopmostUnderPointer();
		if (hovered != IsHovered) {
			SetHovered(hovered);
		}

		if (IsHovered && WasSelectJustPressed()) {
			HandleClick();
		}
	}

	// Override these in a minigame object. The highlight and pulse will still happen
	protected virtual void OnHoverStarted() { }
	protected virtual void OnHoverEnded() { }
	protected virtual void OnClicked() { }

	// The only place this class reads input. This will need to be changed later after the inputmanager is fully implemented. I'll come back to this when everything is fully discussed and decided

	//This reads the crosshair's position from a cursor autoload, or the mouse if the cursor node isn't found. 
	protected virtual Vector2 GetPointerScreenPosition()
	{
		CursorNode cursor = GetTree().Root.GetNodeOrNull<CursorNode>("Cursor/CursorNode");
		if (cursor != null) {
			return cursor.ScreenPosition;
		}
		return GetViewport().GetMousePosition();
	}
	//just a checker for the button (A/E)
	protected virtual bool WasSelectJustPressed()
	{
		return Input.IsActionJustPressed("cursor_primary");
	}

	// Prevents clicks from going throught to the scene behind a UI, like the pause screen and such
	// Full-screen Controls like backgrounds need Mouse Filter = Ignore.
	protected virtual bool IsPointerOverUi()
	{
		return GetViewport().GuiGetHoveredControl() != null;
	}

	// Only the clickable drawn on top reacts when several overlap under the pointer, same concept as IsPointerOverUi() but for the game scene.
	private bool IsTopmostUnderPointer()
	{
		var query = new PhysicsPointQueryParameters2D
		{
			Position = GetCanvasTransform().AffineInverse() * GetPointerScreenPosition(),
			CollideWithAreas = true,
			CollideWithBodies = false,
		};
		CanvasLayer layer = GetCanvasLayerNode();
		if (layer != null) {
			query.CanvasInstanceId = layer.GetInstanceId();
		}

		Clickable top = null;
		foreach (var hit in GetWorld2D().DirectSpaceState.IntersectPoint(query)) {
			if (hit["collider"].AsGodotObject() is Clickable other && other.clickable && other.IsVisibleInTree()) {
				if (top == null || other.DrawsAbove(top)) {
					top = other;
				}
			}
		}
		return top == this;
	}

	// Whenever there is an equal Z, whichever comes later in the scene tree is drawn on top
	private bool DrawsAbove(Clickable other)
	{
		if (ZIndex != other.ZIndex) {
			return ZIndex > other.ZIndex;
		}
		return IsGreaterThan(other);
	}
	//Gives the outline for a shader material. If the visual already has a material, it will not be applied.
	private void SetUpOutline()
	{
		if (visual.Material != null) {
			GD.PushWarning($"{Name}: the visual already has a material, so the hover outline is turned off");
			return;
		}

		outlineMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(OutlineShaderPath) };
		outlineMaterial.SetShaderParameter("outline_color", outlineColor);
		outlineMaterial.SetShaderParameter("width", outlineWidth);
		visual.Material = outlineMaterial;
	}
	//simple update for the hover state and prevents the hover state from getting stuck
	private void SetHovered(bool hovered)
	{
		IsHovered = hovered;
		ShowHighlight(hovered);

		if (hovered) {
			OnHoverStarted();
			EmitSignal(SignalName.Hovered);
		} else {
			OnHoverEnded();
			EmitSignal(SignalName.Unhovered);
		}
	}
	// Where the outline or glow is actually applied. 
	private void ShowHighlight(bool on)
	{
		if (highlightStyle == HighlightStyle.Outline && outlineMaterial != null) {
			outlineMaterial.SetShaderParameter("enabled", on);
		} else if (highlightStyle == HighlightStyle.Glow) {
			Color glow = new Color(baseSelfModulate.R * glowStrength, baseSelfModulate.G * glowStrength, baseSelfModulate.B * glowStrength, baseSelfModulate.A);
			visual.SelfModulate = on ? glow : baseSelfModulate;
		}
	}
	// Plays the click pulse, sound if we have any, and calls OnClicked to handle the click, and emits the click signal
	private void HandleClick()
	{
		PlayClickFeedback();
		OnClicked();
		EmitSignal(SignalName.Clicked);
	}
	// Sets the visual back to its original scale, it'll play a pulse and then go back. Can play a sound if we got one
	private void PlayClickFeedback()
	{
		clickTween?.Kill();
		visual.Scale = baseVisualScale;
		clickTween = CreateTween();
		clickTween.TweenProperty(visual, "scale", baseVisualScale * clickPulseScale, clickPulseGrowTime);
		clickTween.TweenProperty(visual, "scale", baseVisualScale, clickPulseShrinkTime);

		if (clickSound != null) {
			clickSound.Play();
		}
	}
}
