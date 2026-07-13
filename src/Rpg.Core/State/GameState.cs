using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpgGame.Core.State;

/// <summary>
/// Scene-independent state owned by the current game session.
/// </summary>
public sealed record GameState
{
    public int SchemaVersion { get; init; } = 1;

    public required string SaveId { get; init; }

    public MapLocationState Location { get; init; } = new();

    public List<string> ActivePartyActorIds { get; init; } = [];

    public Dictionary<string, ActorProgressState> ActorProgress { get; init; } = [];

    public Dictionary<string, bool> EventFlags { get; init; } = [];

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record MapLocationState
{
    public string MapId { get; init; } = string.Empty;

    public int X { get; init; }

    public int Y { get; init; }

    public string Facing { get; init; } = "south";

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

public sealed record ActorProgressState
{
    public required string ActorId { get; init; }

    public required string ClassId { get; init; }

    public int Level { get; init; } = 1;

    public int Experience { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

