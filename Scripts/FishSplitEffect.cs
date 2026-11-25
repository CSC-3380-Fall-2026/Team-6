using Godot;

public partial class FishSplitEffect : Node2D
{
    private Sprite2D topHalf;
    private Sprite2D bottomHalf;

    private Vector2 topVelocity;
    private Vector2 bottomVelocity;

    private float gravity = 1000f;
    private float topSpin = 3f;
    private float bottomSpin = -3f;

    // Creates two cropped fish halves and launches them apart.
    public void Start(Texture2D texture, Vector2 swipeDirection, Vector2 scale)
    {
        Vector2 size = texture.GetSize();
        float halfHeight = size.Y / 2f;

        AtlasTexture topTexture = new AtlasTexture();
        topTexture.Atlas = texture;
        topTexture.Region = new Rect2(0, 0, size.X, halfHeight);

        AtlasTexture bottomTexture = new AtlasTexture();
        bottomTexture.Atlas = texture;
        bottomTexture.Region = new Rect2(0, halfHeight, size.X, halfHeight);

        topHalf = new Sprite2D();
        topHalf.Texture = topTexture;
        topHalf.Scale = scale;
        topHalf.Position = new Vector2(0, -halfHeight * scale.Y / 2f);
        AddChild(topHalf);

        bottomHalf = new Sprite2D();
        bottomHalf.Texture = bottomTexture;
        bottomHalf.Scale = scale;
        bottomHalf.Position = new Vector2(0, halfHeight * scale.Y / 2f);
        AddChild(bottomHalf);

        Vector2 direction = swipeDirection.Normalized();
        Vector2 splitDirection = new Vector2(-direction.Y, direction.X);

        topVelocity = splitDirection * 250f + new Vector2(0, -100f);
        bottomVelocity = -splitDirection * 250f + new Vector2(0, -100f);
    }

    // Moves both halves independently and applies gravity and rotation.
    public override void _Process(double delta)
    {
        float time = (float)delta;

        topVelocity.Y += gravity * time;
        bottomVelocity.Y += gravity * time;

        topHalf.Position += topVelocity * time;
        bottomHalf.Position += bottomVelocity * time;

        topHalf.Rotation += topSpin * time;
        bottomHalf.Rotation += bottomSpin * time;

        if (topHalf.Position.Y > 1200f && bottomHalf.Position.Y > 1200f)
        {
            QueueFree();
        }
    }
}
