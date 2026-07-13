namespace RpgGame.Core.Content.Definitions;

public sealed record EnemyDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public int Level { get; init; } = 1;

    public Dictionary<string, int> Statistics { get; init; } = [];

    public List<string> AbilityIds { get; init; } = [];

    public List<LootEntryDefinition> Loot { get; init; } = [];
}

public sealed record LootEntryDefinition
{
    public required string ItemId { get; init; }

    public decimal Chance { get; init; }

    public int MinQuantity { get; init; } = 1;

    public int MaxQuantity { get; init; } = 1;
}

