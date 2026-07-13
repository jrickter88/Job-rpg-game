namespace RpgGame.Core.Content.Definitions;

public sealed record QuestDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public required string DescriptionKey { get; init; }

    public List<QuestObjectiveDefinition> Objectives { get; init; } = [];

    public List<QuestRewardDefinition> Rewards { get; init; } = [];

    public string? CompletionFlagId { get; init; }
}

public sealed record QuestObjectiveDefinition
{
    /// <summary>
    /// Stable within its parent quest because save data may refer to it.
    /// </summary>
    public required string Id { get; init; }

    public required string Kind { get; init; }

    public required string TargetId { get; init; }

    public int RequiredCount { get; init; } = 1;
}

public sealed record QuestRewardDefinition
{
    public required string ItemId { get; init; }

    public int Quantity { get; init; } = 1;
}

