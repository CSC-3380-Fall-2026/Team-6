using Godot;

// shows +{score} popup at the bottom of screen where caught fish dropped out
// floats up and fades
// listens to the rod's FishCaught signal
public partial class CatchFeedback: Control
{
    // hook this up in inspector
    [Export] private FishingRod _fishingRod;

    [ExportGroup("Points Popup")]
    [Export] private string _pointsMessage = "+{0}"; // {0} replaced by points
    [Export] private int _pointsFontSize = 56;
    [Export] private Color _pointsColor = new Color(1f, 0.85f, 0.3f);
    [Export] private float _bottomMargin = 110f; // how far above bottom edge the popup appears
    [Export] private float _riseDistance = 80f; // pixels the popup floats up
    [Export] private float _popupTime = 1f; // seconds to float up and fade out
    [Export] private float _popScale = 1.3f; // how big it grows when it first appears
    [Export] private float _popTime = 0.15f; // seconds to grow and settle

    private const int OutlineSize = 12;
    private const float StartScale = 0.5f; // stars at half size and pops up
    private const float Invisible = 0f;
    private const float ScreenLeft = 0f;

    public override void _Ready()
    {
        // popups only for show, so clicks go through to game
        MouseFilter = MouseFilterEnum.Ignore;

        if (_fishingRod != null)
        {
            _fishingRod.FishCaught += OnFishCaught;
        }
        else
        {
            GD.PushWarning("CatchFeedback: assign the Fishing Rod in the Inspector, or no catch popups will show");
        }
    }

    public override void _ExitTree()
    {
        if (_fishingRod != null)
        {
            _fishingRod.FishCaught -= OnFishCaught;
        }
    }

    private void OnFishCaught(FishData fish, int points, Vector2 dropPosition)
    {
        ShowPointsPopup(points, dropPosition);
    }

    private void ShowPointsPopup(int points, Vector2 dropPosition)
    {
        Label popup = new Label
        {
            Text = string.Format(_pointsMessage, points),
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings
            {
                FontSize = _pointsFontSize,
                FontColor = _pointsColor,
                OutlineSize = OutlineSize,
                OutlineColor = Colors.Black,
            },
        };
        AddChild(popup);
        popup.ResetSize(); // shrink to fit text so it can be centered
        popup.PivotOffset = popup.Size / 2f; // pop from middle

        // centered on where fish dropped out, a little above bottom edge, kept fully on screen
        float screenWidth = GetViewportRect().Size.X;
        float x = Mathf.Clamp(dropPosition.X - popup.Size.X / 2f, ScreenLeft, screenWidth - popup.Size.X);
        float y = dropPosition.Y - _bottomMargin - popup.Size.Y / 2f;
        popup.GlobalPosition = new Vector2(x, y);
        popup.Scale = Vector2.One * StartScale;

        Tween tween = popup.CreateTween();
        tween.TweenProperty(popup, "scale", Vector2.One * _popScale, _popTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(popup, "scale", Vector2.One, _popTime);
        tween.TweenProperty(popup, "position:y", y - _riseDistance, _popupTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        tween.Parallel().TweenProperty(popup, "modulate:a", Invisible, _popupTime).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.TweenCallback(Callable.From(() => popup.QueueFree()));
    }
}