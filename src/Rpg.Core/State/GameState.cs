using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpgGame.Core.State;

/// <summary>
/// Scene-independent, serializable state for one campaign/playthrough.
/// </summary>
/// <remarks>
/// Godot scenes are temporary presentation objects: a map or battle scene may be freed
/// at any time. This record is the durable source of truth that survives those scene
/// changes and is written into a save file. It contains stable content IDs and simple
/// values instead of Nodes, Resources, or file paths.
/// </remarks>
public sealed record GameState
{
    /// <summary>
    /// Version of the state object's shape. The outer save envelope has its own format
    /// version because file-level and state-level changes do not always happen together.
    /// </summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Stable identity of this playthrough/save lineage, independent of a UI slot number.
    /// </summary>
    public required string SaveId { get; init; }

    /// <summary>Location from which the exploration scene can reconstruct the player.</summary>
    public MapLocationState Location { get; init; } = new();

    /// <summary>
    /// Actor definition IDs in active-party order. The IDs select persistent actor
    /// progress from <see cref="ActorProgress"/> rather than copying actor definitions.
    /// A list supports the four-hero party while allowing the game to begin with only James.
    /// Every use case that changes this list must enforce <see cref="PartyRules"/>.
    /// </summary>
    public List<string> ActivePartyActorIds { get; init; } = [];

    /// <summary>
    /// Mutable progression keyed by actor definition ID. A dictionary gives direct lookup;
    /// party order is intentionally stored by <see cref="ActivePartyActorIds"/> instead.
    /// </summary>
    public Dictionary<string, ActorProgressState> ActorProgress { get; init; } = [];

    /// <summary>
    /// Persistent facts used to connect otherwise independent systems—for example,
    /// whether a chest is open, a bridge is repaired, or a boss is defeated.
    /// </summary>
    public Dictionary<string, bool> EventFlags { get; init; } = [];

    /// <summary>
    /// JSON properties unknown to this version of the game. Preserving them prevents an
    /// older reader from silently deleting future data when it loads and re-saves a file.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Minimal persistent exploration position, expressed without Godot vector or node types.
/// </summary>
public sealed record MapLocationState
{
    /// <summary>Stable ID of the map definition/entry to reconstruct.</summary>
    public string MapId { get; init; } = string.Empty;

    /// <summary>Horizontal tile coordinate, not a pixel coordinate.</summary>
    public int X { get; init; }

    /// <summary>Vertical tile coordinate, not a pixel coordinate.</summary>
    public int Y { get; init; }

    /// <summary>
    /// Stable logical direction. A string keeps save data independent of any Godot enum;
    /// the content/session boundary will validate the accepted values.
    /// </summary>
    public string Facing { get; init; } = "south";

    /// <summary>Unknown future location fields retained during a load/save round trip.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

/// <summary>
/// Mutable, save-specific progression for one story actor.
/// </summary>
/// <remarks>
/// Base statistics and starting values still come from ActorDefinition. This state records
/// only what can differ between playthroughs. Inventory/equipment fields are deliberately
/// absent until those systems are implemented and their actual requirements are known.
/// </remarks>
public sealed record ActorProgressState
{
    /// <summary>Stable actor definition ID that this progress belongs to.</summary>
    public required string ActorId { get; init; }

    /// <summary>Stable ID of the actor's currently assigned class.</summary>
    public required string ClassId { get; init; }

    /// <summary>Current persistent actor level.</summary>
    public int Level { get; init; } = 1;

    /// <summary>Total or in-level experience; the eventual progression rule will define it.</summary>
    public int Experience { get; init; }

    /// <summary>Unknown future actor-progress fields retained for compatibility.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
