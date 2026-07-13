namespace RpgGame.Core.Content.Definitions;

public sealed record ClassDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public Dictionary<string, int> BaseStatisticBonuses { get; init; } = [];

    public List<AbilityUnlockDefinition> AbilityUnlocks { get; init; } = [];
}

public sealed record AbilityUnlockDefinition
{
    public int Level { get; init; } = 1;

    public required string AbilityId { get; init; }
}

