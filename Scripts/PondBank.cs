using Godot;

// makes pond look sunken into ground
// built from pond's own shape when scene starts, so it still fits if pond is reshaped

public partial class PondBank : Node2D
{
    [Export] private Polygon2D _waterShape;
    [Export] private Texture2D _dirtTexture;

    [ExportGroup("Size")]
    [Export] private float _rimWidth = 14f; // pixels of dirt around outside edge
    [Export] private float _wallHeight = 30f; // pixels of dirt wall showing on far side
    [Export] private float _shadowHeight = 18f; // pixels of shadow on water below wall
    [Export] private float _dirtPixelSize = 4f; // how big each pixel of dirt texture is drawn

    [ExportGroup("Colors")]
    // multiplied with dirt texture
    // white keeps texture's own color, grey darkens it
    [Export] private Color _rimColor = new Color(1f, 1f, 1f);
    [Export] private Color _wallTopColor = new Color(0.85f, 0.85f, 0.85f);
    [Export] private Color _wallBottomColor = new Color(0.6f, 0.6f, 0.6f);
    [Export] private Color _shadowColor = new Color(0f, 0f, 0f, 0.25f);

    private const float Half = 0.5f;

    public override void _Ready()
    {
        if (_waterShape == null || _waterShape.Polygon.Length < 3)
        {
            GD.PushWarning("PondBank: assign the Water Shape in the Inspector");
            return;
        }

        Vector2[] pond = PondInThisNode();
        AddRim(pond);
        AddWall(pond);
        AddShadow(pond);
    }

    // water's outline, moved into this node's coordinates in case nodes aren't in same spot
    private Vector2[] PondInThisNode()
    {
        Vector2[] points = _waterShape.Polygon;
        Vector2[] pond = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            pond[i] = ToLocal(_waterShape.ToGlobal(points[i]));
        }
        return pond;
    }

    // thick dirt line traced just outside water's edge
    private void AddRim(Vector2[] pond)
    {
        // trace line halfway into rim, so its full width sits outside water
        Godot.Collections.Array<Vector2[]> traced = Geometry2D.OffsetPolygon(pond, _rimWidth * Half, Geometry2D.PolyJoinType.Round);
        if (traced.Count == 0)
        {
            return;
        }

        Line2D rim = new Line2D
        {
            Name = "Rim",
            Points = traced[0],
            Closed = true,
            Width = _rimWidth,
            DefaultColor = _rimColor,
            Texture = _dirtTexture,
            TextureMode = Line2D.LineTextureMode.Tile,
            TextureRepeat = TextureRepeatEnum.Enabled,
            TextureFilter = TextureFilterEnum.Nearest,
            JointMode = Line2D.LineJointMode.Round,
        };
        AddChild(rim);
    }

    // part of the pond that disappears when pond is slide down by wall height is far wall
    // top half of it is drawn lighter and bottom half darker
    private void AddWall(Vector2[] pond)
    {
        Vector2[] halfDown = Shifted(pond, _wallHeight * Half);
        Vector2[] fullDown = Shifted(pond, _wallHeight);

        foreach (Vector2[] piece in Geometry2D.ClipPolygons(pond, halfDown))
        {
            AddDirtPiece("WallTop", piece, _wallTopColor);
        }
        foreach (Vector2[] piece in Geometry2D.ClipPolygons(halfDown, fullDown))
        {
            foreach (Vector2[] inPond in Geometry2D.IntersectPolygons(piece, pond))
            {
                AddDirtPiece("WallBottom", inPond, _wallBottomColor);
            }
        }
    }

    // strip of shadow on water just below wall
    private void AddShadow(Vector2[] pond)
    {
        Vector2[] wallBottom = Shifted(pond, _wallHeight);
        Vector2[] shadowBottom = Shifted(pond, _wallHeight + _shadowHeight);

        foreach (Vector2[] strip in Geometry2D.ClipPolygons(wallBottom, shadowBottom))
        {
            foreach(Vector2[] onWater in Geometry2D.IntersectPolygons(strip, pond))
            {
                AddChild(new Polygon2D { Name = "Shadow", Polygon = onWater, Color = _shadowColor});
            }
        }
    }

    private void AddDirtPiece(string name, Vector2[] shape, Color tint)
    {
        AddChild(new Polygon2D
        {
            Name = name,
            Polygon = shape,
            Color = tint,
            Texture = _dirtTexture,
            TextureScale = Vector2.One / _dirtPixelSize,
            TextureRepeat = TextureRepeatEnum.Enabled,
            TextureFilter = TextureFilterEnum.Nearest,
        });
    }

    // same outline, moved straight down
    private static Vector2[] Shifted(Vector2[] shape, float down)
    {
        Vector2[] moved = new Vector2[shape.Length];
        for (int i = 0; i < shape.Length; i++)
        {
            moved[i] = shape[i] + Vector2.Down * down;
        }
        return moved;
    }
}