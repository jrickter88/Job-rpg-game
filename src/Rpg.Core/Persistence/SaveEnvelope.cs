using RpgGame.Core.Mods;
using RpgGame.Core.State;

namespace RpgGame.Core.Persistence;

/// <summary>
/// Top-level, serializable document written for one save slot.
/// </summary>
/// <remarks>
/// The envelope stores file-format metadata separately from gameplay state. This lets the
/// loader validate the current document version before attempting strong deserialization.
/// It is a record/DTO, not the service that decides paths or performs disk IO.
/// </remarks>
public sealed record SaveEnvelope
{
    /// <summary>
    /// Current save document format. Older formats are intentionally unsupported during
    /// active development and must be discarded when the shape changes.
    /// </summary>
    public int SaveFormatVersion { get; init; } = SaveJsonSerializer.CurrentFormatVersion;

    /// <summary>
    /// Human-diagnostic build version that created the file. It is not used for loading.
    /// </summary>
    public string GameVersion { get; init; } = "0.0.0";

    /// <summary>UTC timestamp of the most recent successful write.</summary>
    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Scene-independent campaign state being persisted.</summary>
    public required GameState State { get; init; }

    /// <summary>
    /// Data mods whose records may be referenced by this campaign.
    /// </summary>
    public List<ModReference> EnabledMods { get; init; } = [];

}
