using Godot;

// feedback when a fish is caught in Gone Fishing
// lists to rod's FishCaught signal and shows:
//      "+{score}" that pops up at the bottom of the screen where the caught fish dropped out
//      card in the bottom-right corner with fish's picture, name, and rarity
//          first time fish is caught, card says "NEW FISH CAUGHT!" with border in rarity's color
//          after that, card is smaller grey "caught again" card
public partial class CatchFeedback: Control
{
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

    [ExportGroup("Catch Card")]
    [Export] private string _newFishMessage = "NEW FISH CAUGHT!";
    [Export] private string _caughtAgainMessage = "{0}  ·  caught again"; // {0} replaced by rarity
    [Export] private float _cardMargin = 24f; // gap between card and bottom-right corner
    [Export] private float _newCardTime = 3.5f; // seconds "new fish card" stays before fading
    [Export] private float _repeatCardTime = 2f; // seconds "caught again" card stays before fading
    [Export] private float _slideTime = 0.3f; // seconds to slide in from the right
    [Export] private float _cardFadeTime = 0.4f; // seconds to fade out
    // colors for common, uncommon, rare, epic, legendary fish rarity
    [Export] private Color[] _rarityColors =
    {
        new Color("c8c8c8"),
        new Color("6edc78"),
        new Color("5aaaff"),
        new Color("be6eff"),
        new Color("ffc83c"),
    };

    private const int OutlineSize = 12;
    private const float StartScale = 0.5f; // stars at half size and pops up
    private const float Invisible = 0f;
    private const float ScreenLeft = 0f;

    // look of "new fish" card (matches HUD style)
    private static readonly Color NewCardBackground = new Color("0c1c2dd9");
    private static readonly Color NewHeaderColor = new Color("ffd75a");
    private static readonly Color NewNameColor = Colors.White;
    private static readonly Color PillTextColor = new Color("14141e");
    private const int NewCardBorder = 4;
    private const int NewIconSize = 120;
    private const int NewHeaderFontSize = 24;
    private const int NewNameFontSize = 46;
    private const int PillFontSize = 20;

    // look of "caught again" card (smaller and grey)
    private static readonly Color RepeatCardBackground = new Color("1e2024be");
    private static readonly Color RepeatCardBorderColor = new Color("ffffff28");
    private static readonly Color RepeatNameColor = new Color("c8c8c8");
    private static readonly Color RepeatInfoColor = new Color("969696");
    private static readonly Color GreyedOutIcon = new Color(0.6f, 0.6f, 0.6f, 0.7f);
    private const int RepeatCardBorder = 2;
    private const int RepeatIconSize = 64;
    private const int RepeatNameFontSize = 32;
    private const int RepeatInfoFontSize = 20;

    // shared card layout
    private const int CardCornerRadius = 16;
    private const int CardPaddingSides = 20;
    private const int CardPaddingTopBottom = 14;
    private const int CardSpacing = 20; // gap between picture and text
    private const int PillCornerRadius = 12;
    private const int PillPaddingSides = 14;
    private const int PillPaddingTopBottom = 3;

    private Control _currentCard; // only one card at a time, new catch replaces it

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

        bool isNewFish = SaveManager.Instance.MarkFishCaught(fish.FishName);
        ShowCatchCard(fish, isNewFish);
    }

    // POINTS POPUP
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

    // CATCH CARD
    private void ShowCatchCard(FishData fish, bool isNewFish)
    {
        if (IsInstanceValid(_currentCard))
        {
            _currentCard.QueueFree();
        }

        Control card = isNewFish ? BuildNewFishCard(fish) : BuildRepeatCard(fish);
        _currentCard = card;
        AddChild(card);
        card.ResetSize(); // shrink to fit its contents

        // start just off right edge, then slide into bottom-right corner
        Vector2 screen = GetViewportRect().Size;
        Vector2 target = screen - card.Size - Vector2.One * _cardMargin;
        card.Position = new Vector2(screen.X, target.Y);

        Tween tween = card.CreateTween();
        tween.TweenProperty(card, "position:x", target.X, _slideTime).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tween.TweenInterval(isNewFish ? _newCardTime : _repeatCardTime);
        tween.TweenProperty(card, "modulate:a", Invisible, _cardFadeTime);
        tween.TweenCallback(Callable.From(() => card.QueueFree()));
    }

    // NEW FISH CARD
    private Control BuildNewFishCard(FishData fish)
    {
        Color rarityColor = RarityColor(fish.Rarity);
        PanelContainer card = MakeCard(NewCardBackground, rarityColor, NewCardBorder);
        HBoxContainer row = MakeRow(card);
        row.AddChild(MakeIcon(fish, NewIconSize, Colors.White));

        VBoxContainer text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(text);
        text.AddChild(MakeLabel(_newFishMessage, NewHeaderFontSize, NewHeaderColor));
        text.AddChild(MakeLabel(fish.FishName, NewNameFontSize, NewNameColor));
        text.AddChild(MakeRarityPill(fish.Rarity, rarityColor));
        return card;
    }

    // SMALL GREY CARD
    private Control BuildRepeatCard(FishData fish)
    {
        PanelContainer card = MakeCard(RepeatCardBackground, RepeatCardBorderColor, RepeatCardBorder);
        HBoxContainer row = MakeRow(card);
        row.AddChild(MakeIcon(fish, RepeatIconSize, GreyedOutIcon));

        VBoxContainer text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddChild(text);
                text.AddChild(MakeLabel(fish.FishName, RepeatNameFontSize, RepeatNameColor));
        text.AddChild(MakeLabel(string.Format(_caughtAgainMessage, fish.Rarity), RepeatInfoFontSize, RepeatInfoColor));
        return card;
    }

    private Color RarityColor(FishRarity rarity)
    {
        int index = (int)rarity;
        if (_rarityColors == null || index < 0 || index >= _rarityColors.Length)
        {
            return Colors.White;
        }
        return _rarityColors[index];
    }

    // GENERAL CARD STYLES
    private static PanelContainer MakeCard(Color background, Color border, int borderWidth)
    {
        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(CardCornerRadius);
        style.SetContentMarginAll(CardPaddingTopBottom);
        style.ContentMarginLeft = CardPaddingSides;
        style.ContentMarginRight = CardPaddingSides;

        PanelContainer card = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", style);
        return card;
    }

    private static HBoxContainer MakeRow(PanelContainer card)
    {
        HBoxContainer row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", CardSpacing);
        card.AddChild(row);
        return row;
    }

    // fish picture
    private static TextureRect MakeIcon(FishData fish, int size, Color tint)
    {
        Texture2D sheet = fish.GoneFishingSprite;
        Texture2D picture = sheet;
        if (sheet != null)
        {
            int frameSize = sheet.GetHeight();
            picture = new AtlasTexture { Atlas = sheet, Region = new Rect2(0, 0, frameSize, frameSize) };
        }

        return new TextureRect
        {
            Texture = picture,
            CustomMinimumSize = Vector2.One * size,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            Modulate = tint,
            MouseFilter = MouseFilterEnum.Ignore,
        };
    }

    private static Label MakeLabel(string text, int fontSize, Color color)
    {
        return new Label
        {
            Text = text,
            MouseFilter = MouseFilterEnum.Ignore,
            LabelSettings = new LabelSettings { FontSize = fontSize, FontColor = color },
        };
    }

    // rounded badge in rarity's color
    private static Label MakeRarityPill(FishRarity rarity, Color color)
    {
        StyleBoxFlat style = new StyleBoxFlat { BgColor = color };
        style.SetCornerRadiusAll(PillCornerRadius);
        style.ContentMarginLeft = PillPaddingSides;
        style.ContentMarginRight = PillPaddingSides;
        style.ContentMarginTop = PillPaddingTopBottom;
        style.ContentMarginBottom = PillPaddingTopBottom;

        Label pill = MakeLabel(rarity.ToString().ToUpper(), PillFontSize, PillTextColor);
        pill.AddThemeStyleboxOverride("normal", style);
        pill.SizeFlagsHorizontal = SizeFlags.ShrinkBegin; // only as wide as the word
        return pill;
    }
}