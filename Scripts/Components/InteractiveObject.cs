using Godot;

public partial class InteractiveObject : Area2D
{
    [Export]
    private float hoverScaleMultiplier = 1.1f;

    private Vector2 originalScale;

    public override void _Ready()
    {
        originalScale = Scale;

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void OnMouseEntered()
    {
        Scale = originalScale * hoverScaleMultiplier;
    }

    private void OnMouseExited()
    {
        Scale = originalScale;
    }
}