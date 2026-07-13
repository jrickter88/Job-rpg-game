namespace RpgGame.Core.Content.Definitions;

public sealed record ActorDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public required string StartingClassId { get; init; }

    public int StartingLevel { get; init; } = 1;

    public Dictionary<string, int> BaseStatistics { get; init; } = [];

    public List<string> StartingAbilityIds { get; init; } = [];
}

