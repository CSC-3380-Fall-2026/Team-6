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

    // Chance (0 to 1) that this fish notices a bobber near it, rolled every few moments while it's close.
    // The default suits a common fish. Rarer fish are set lower so they're more cautious.
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float NoticeChance { get; set; } = 0.35f;

    // Seconds the fish hesitates at the bobber, nibbling, before it bites. Rarer fish wait longer.
    [Export]
    public float BiteDelay { get; set; } = 1f;
}