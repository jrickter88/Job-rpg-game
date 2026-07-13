namespace RpgGame.Core.Content.Definitions;

public sealed record ItemDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public required string DescriptionKey { get; init; }

    public int BuyPrice { get; init; }

    public int SellPrice { get; init; }

    public int MaxStack { get; init; } = 99;
}

