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
    [Export] private string _newFishMessage = "*  NEW FISH CAUGHT!  *";
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

    // look of "new fish" card
    private const float NewBackgroundDarknes = 0.35f; // background is rarity color at 35% brightness
    private const float NewBackgroundOpacity = 0.96f;
    private const float GlowOpacity = 0.6f;
    private const int GlowSize = 18; // how far colored glow spreads around card
    private const int NewCardBorder = 5;
    private const int NewCornerRadius = 18;
    private const int NewIconSize = 112;
    private const int BannerFontSize = 24;
    private const int BannerPaddingTopBottom = 8;
    private const int NewNameFontSize = 46;
    private const int NewRarityFontSize = 30;
    private const int NewBodyPaddingLeft = 24;
    private const int NewBodyPaddingRight = 72; // extra room on right for sparkles
    private const int NewBodyPaddingTopBottom = 14;
    private static readonly Color BannerTextColor = new Color("19140a");
    private static readonly Color NewNameColor = Colors.White;

    // sparkles for "new fish" card
    // each position is a fraction of the card's width and height
    private static readonly Vector2[] SparklePositions =
    {
        new Vector2(0.93f, 0.42f),
        new Vector2(0.87f, 0.74f),
        new Vector2(0.96f, 0.80f),
    };
    private const float SparkleSize = 9f; // pixels from middle of sparkle to tips
    private const float SparkleWaist = 3f; // pixels from middle to pinched-in corners
    private const float SparkleSmallScale = 0.5f;
    private const float SparkleBigScale = 1.1f;
    private const float SparkleTwinkleTime = 0.45f; // seconds to grow or shrink once
    private const float SparkleStagger = 0.2f; // seconds between each sparkle starting
    private static readonly Color SparkleColor = new Color(1f, 1f, 1f, 0.9f);

    // look of "caught again" card (smaller and grey)
    private static readonly Color RepeatCardBackground = new Color("3c3e42dc");
    private static readonly Color RepeatCardBorderColor = new Color("8c8c8c");
    private static readonly Color RepeatShadowColor = new Color(0f, 0f, 0f, 0.35f);
    private static readonly Vector2 RepeatShadowOffset = new Vector2(4f, 6f);
    private static readonly Color RepeatNameColor = new Color("d7d7d7");
    private static readonly Color GreyedOutIcon = new Color(0.6f, 0.6f, 0.6f, 0.7f);
    private const float RepeatRarityOpacity = 0.67f; // rarity word keeps its color but dimmer
    private const int RepeatCardBorder = 3;
    private const int RepeatCornerRadius = 16;
    private const int RepeatIconSize = 68;
    private const int RepeatNameFontSize = 32;
    private const int RepeatRarityFontSize = 22;
    private const int RepeatPaddingSides = 20;
    private const int RepeatPaddingTopBottom = 14;

    private const int CardSpacing = 20; // gap between picture and text
    private const int NoSpacing = 0;
    private const int NoBorder = 0;
    private const int NoRounding = 0;

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

        if (isNewFish)
        {
            AddSparkles(card); // needs card's final size, so it happens after ResetSize
        }

        // start just off right edge, then slide into bottom-right corner
        // glow spreads past card's edge
        Vector2 screen = GetViewportRect().Size;
        float edgeGap = isNewFish ? _cardMargin + GlowSize : _cardMargin;
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
        Color background = rarityColor.Darkened(1f - NewBackgroundDarknes);
        background.A = NewBackgroundOpacity;

        StyleBoxFlat cardStyle = MakeStyle(background, rarityColor, NewCardBorder, NewCornerRadius);
        cardStyle.ShadowColor = new Color(rarityColor, GlowOpacity); // the glow
        cardStyle.ShadowSize = GlowSize;
        cardStyle.SetContentMarginAll(NewCardBorder); // contents sit just inside border

        PanelContainer card = MakePanel(cardStyle);
        VBoxContainer layout = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        layout.AddThemeConstantOverride("separation", NoSpacing);
        card.AddChild(layout);

        layout.AddChild(MakeBanner(rarityColor));

        MarginContainer body = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        body.AddThemeConstantOverride("margin_left", NewBodyPaddingLeft);
        body.AddThemeConstantOverride("margin_right", NewBodyPaddingRight);
        body.AddThemeConstantOverride("margin_top", NewBodyPaddingTopBottom);
        body.AddThemeConstantOverride("margin_bottom", NewBodyPaddingTopBottom);
        layout.AddChild(body);

        HBoxContainer row = MakeRow();
        body.AddChild(row);
        row.AddChild(MakeIcon(fish, NewIconSize, Colors.White));

        VBoxContainer text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(text);
        text.AddChild(MakeLabel(fish.FishName, NewNameFontSize, NewNameColor));
        text.AddChild(MakeLabel(fish.Rarity.ToString(), NewRarityFontSize, rarityColor));
        return card;
    }

    // colored strip across top of "new fish" card
    private Control MakeBanner(Color rarityColor)
    {
        StyleBoxFlat style = MakeStyle(rarityColor, rarityColor, NoBorder, NoRounding);
        // only top corners rounded
        int innerRadius = NewCornerRadius - NewCardBorder;
        style.CornerRadiusTopLeft = innerRadius;
        style.CornerRadiusTopRight = innerRadius;
        style.ContentMarginTop = BannerPaddingTopBottom;
        style.ContentMarginBottom = BannerPaddingTopBottom;

        PanelContainer banner = MakePanel(style);
        Label title = MakeLabel(_newFishMessage, BannerFontSize, BannerTextColor);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        banner.AddChild(title);
        return banner;
    }

    // SMALL GREY CARD
    private Control BuildRepeatCard(FishData fish)
    {
        StyleBoxFlat style = MakeStyle(RepeatCardBackground, RepeatCardBorderColor, RepeatCardBorder, RepeatCornerRadius);
        style.ShadowColor = RepeatShadowColor;
        style.ShadowOffset = RepeatShadowOffset;
        style.ShadowSize = RepeatCardBorder;
        style.ContentMarginLeft = RepeatPaddingSides;
        style.ContentMarginRight = RepeatPaddingSides;
        style.ContentMarginTop = RepeatPaddingTopBottom;
        style.ContentMarginBottom = RepeatPaddingTopBottom;

        PanelContainer card = MakePanel(style);
        HBoxContainer row = MakeRow();
        card.AddChild(row);
        row.AddChild(MakeIcon(fish, RepeatIconSize, GreyedOutIcon));

        VBoxContainer text = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
        row.AddChild(text);
        text.AddChild(MakeLabel(fish.FishName, RepeatNameFontSize, RepeatNameColor));
        Color dimRarity = new Color(RarityColor(fish.Rarity), RepeatRarityOpacity);
        text.AddChild(MakeLabel(fish.Rarity.ToString(), RepeatRarityFontSize, dimRarity));
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

    // SPARKLES
    private static void AddSparkles(Control card)
    {
        for (int i = 0; i < SparklePositions.Length; i++)
        {
            Polygon2D sparkle = new Polygon2D
            {
                Polygon = SparkleShape(),
                Color = SparkleColor,
                Position = card.Size * SparklePositions[i],
                Scale = Vector2.One * SparkleSmallScale,
            };
            card.AddChild(sparkle);

            // twinkle until card is removed
            Tween twinkle = sparkle.CreateTween().SetLoops();
            twinkle.TweenInterval(i * SparkleStagger);
            twinkle.TweenProperty(sparkle, "scale", Vector2.One * SparkleBigScale, SparkleTwinkleTime).SetTrans(Tween.TransitionType.Sine);
            twinkle.TweenProperty(sparkle, "scale", Vector2.One * SparkleSmallScale, SparkleTwinkleTime).SetTrans(Tween.TransitionType.Sine);
        }
    }

    // four-pointed star, pinched in between
    private static Vector2[] SparkleShape()
    {
        return new Vector2[]
        {
            new Vector2(0f, -SparkleSize),
            new Vector2(SparkleWaist, -SparkleWaist),
            new Vector2(SparkleSize, 0f),
            new Vector2(SparkleWaist, SparkleWaist),
            new Vector2(0f, SparkleSize),
            new Vector2(-SparkleWaist, SparkleWaist),
            new Vector2(-SparkleSize, 0f),
            new Vector2(-SparkleWaist, -SparkleWaist),
        };
    }

    // GENERAL CARD PIECES
    private static StyleBoxFlat MakeStyle(Color background, Color border, int borderWidth, int cornerRadius)
    {
        StyleBoxFlat style = new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
        };
        style.SetBorderWidthAll(borderWidth);
        style.SetCornerRadiusAll(cornerRadius);
        return style;
    }

    private static PanelContainer MakePanel(StyleBoxFlat style)
    {
        PanelContainer panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);
        return panel;
    }

    private static HBoxContainer MakeRow()
    {
        HBoxContainer row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", CardSpacing);
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
}