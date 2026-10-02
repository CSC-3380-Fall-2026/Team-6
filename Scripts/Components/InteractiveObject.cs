using Godot;

public partial class InteractiveObject : Area2D
{
    private Vector2 originalScale;

    public override void _Ready()
    {
        originalScale = Scale;

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    private void OnMouseEntered()
    {
        Scale = originalScale * 1.1f;
    }

    private void OnMouseExited()
    {
        Scale = originalScale;
    }
}