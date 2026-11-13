using Godot;

public partial class HoverButton : Button
{
    [Export] private float hoverScale = 1.1f;
    [Export] private float time = 0.1f;
    private Tween tween;

    public override void _Ready()
    {
        MouseEntered += () => ScaleTo(hoverScale);
        MouseExited += () => ScaleTo(1f);
        FocusEntered += () => ScaleTo(hoverScale); // controller selection
        FocusExited += () => ScaleTo(1f);
    }

    private void ScaleTo(float s)
    {
        PivotOffset = Size / 2; // grow from center
        tween?.Kill();
        tween = CreateTween();
        tween.TweenProperty(this, "scale", Vector2.One * s, time);
    }
}