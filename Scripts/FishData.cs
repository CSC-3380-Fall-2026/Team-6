using Godot;

public enum FishRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

[GlobalClass]
public partial class FishData : Resource
{
    [Export]
    public string FishName { get; set; } = "New Fish";

    [Export]
    public Texture2D GoneFishingSprite { get; set; }

    [Export]
    public Texture2D FishNinjaSprite { get; set; }

    [Export]
    public Texture2D FishNinjaCutSprite { get; set; }

    [Export]
    public int Points { get; set; }

    [Export]
    public float Speed { get; set; }

    [Export]
    public bool DoNotCut { get; set; } = false;

    [Export]
    public FishRarity Rarity { get; set; } = FishRarity.Common;
}