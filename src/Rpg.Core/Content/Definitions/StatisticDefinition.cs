namespace RpgGame.Core.Content.Definitions;

public sealed record StatisticDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }

    public int MinimumValue { get; init; }

    public int MaximumValue { get; init; }

    public int DefaultValue { get; init; }
}

