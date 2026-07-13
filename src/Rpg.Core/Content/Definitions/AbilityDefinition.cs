namespace RpgGame.Core.Content.Definitions;

public sealed record AbilityDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public required string DescriptionKey { get; init; }

    public required string TargetingId { get; init; }

    public string? CostStatisticId { get; init; }

    public int CostAmount { get; init; }

    /// <summary>
    /// Identifies a small, code-owned rules implementation; this is not a scripting DSL.
    /// </summary>
    public required string RulesetId { get; init; }

    public Dictionary<string, decimal> NumericParameters { get; init; } = [];
}

