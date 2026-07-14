using System.Text.Json;
using System.Text.Json.Serialization;
using RpgGame.Core.Mods;
using RpgGame.Core.State;

namespace RpgGame.Core.Persistence;

/// <summary>
/// Top-level, serializable document written for one save slot.
/// </summary>
/// <remarks>
/// The envelope stores file-format metadata separately from gameplay state. This lets the
/// loader decide whether JSON migration is required before attempting strong deserialization.
/// It is a record/DTO, not the service that decides paths or performs disk IO.
/// </remarks>
public sealed record SaveEnvelope
{
    /// <summary>
    /// Version controlling ordered <see cref="ISaveMigration"/> execution.
    /// Increment only for a breaking file-format change.
    /// </summary>
    public int SaveFormatVersion { get; init; } = 1;

    /// <summary>
    /// Human-diagnostic build version that created the file. Compatibility decisions use
    /// <see cref="SaveFormatVersion"/>, not this value.
    /// </summary>
    public string GameVersion { get; init; } = "0.0.0";

    /// <summary>UTC timestamp of the most recent successful write.</summary>
    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Scene-independent campaign state being persisted.</summary>
    public required GameState State { get; init; }

    /// <summary>
    /// Data mods whose records may be referenced by this campaign. This additive field does
    /// not require a save-format migration: saves written before Milestone 1.5 deserialize it
    /// as an empty list and remain valid.
    /// </summary>
    public List<ModReference> EnabledMods { get; init; } = [];

    /// <summary>Unknown future envelope fields retained during round-trip serialization.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}
