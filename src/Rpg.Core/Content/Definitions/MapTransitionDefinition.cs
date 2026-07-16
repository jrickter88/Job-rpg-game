namespace RpgGame.Core.Content.Definitions;

/// <summary>Defines one step-on transition between two authored maps.</summary>
public sealed record MapTransitionDefinition
{
    public required string Id { get; init; }
    public required MapCellDefinition SourceCell { get; init; }
    public required string DestinationMapId { get; init; }
    public required string DestinationSpawnId { get; init; }
}

public sealed record MapCellDefinition
{
    public int X { get; init; }
    public int Y { get; init; }
}
