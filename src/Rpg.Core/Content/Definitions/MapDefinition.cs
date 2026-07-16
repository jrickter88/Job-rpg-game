namespace RpgGame.Core.Content.Definitions;

/// <summary>Defines an exploration map and its named logical spawn points.</summary>
public sealed record MapDefinition : ContentDefinition
{
    public required string DisplayNameKey { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required List<string> Rows { get; init; }
    public List<MapSpawnDefinition> Spawns { get; init; } = [];
    public List<MapEncounterMarkerDefinition> Encounters { get; init; } = [];
    public List<MapTransitionDefinition> Transitions { get; init; } = [];
}

/// <summary>A logical tile used when entering an exploration map.</summary>
public sealed record MapSpawnDefinition
{
    public required string Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public string Facing { get; init; } = "south";
}

/// <summary>Map-owned encounter marker positioned in the ASCII logic layer.</summary>
public sealed record MapEncounterMarkerDefinition
{
    public required string Id { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public required string EncounterId { get; init; }
    public required string ClearedFlagId { get; init; }
}
