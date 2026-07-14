namespace RpgGame.Core.Mods;

/// <summary>
/// Authored metadata stored in the required <c>manifest.json</c> at a mod's root.
/// </summary>
/// <remarks>
/// The manifest describes data and dependencies only. It deliberately contains no entry
/// point, assembly, script, or executable hook. Milestone 1.5 therefore cannot execute
/// community code.
/// </remarks>
public sealed record ModManifest
{
    /// <summary>Schema version for the manifest document itself.</summary>
    public int SchemaVersion { get; init; } = 1;

    /// <summary>Permanent community-wide ID in <c>mod.author.mod-name</c> form.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable name shown by future tooling.</summary>
    public required string Name { get; init; }

    /// <summary>Author-controlled Semantic Version for save compatibility.</summary>
    public required string Version { get; init; }

    /// <summary>
    /// Integer contract version supported by the game. Unlike a game build number, this
    /// changes only when the public data-mod contract becomes incompatible.
    /// </summary>
    public int GameApiVersion { get; init; } = 1;

    /// <summary>Stable IDs of mods that must load before this mod.</summary>
    public List<string> Dependencies { get; init; } = [];
}

/// <summary>
/// Small serializable identity stored with a save; installation paths never enter save data.
/// </summary>
public sealed record ModReference
{
    public required string Id { get; init; }

    public required string Version { get; init; }
}

/// <summary>
/// Validated loose-folder mod discovered on disk. Paths stay runtime metadata rather than
/// authored cross-record references.
/// </summary>
public sealed record DiscoveredMod(
    ModManifest Manifest,
    string RootDirectory,
    string ContentDirectory)
{
    public ModReference ToReference() => new()
    {
        Id = Manifest.Id,
        Version = Manifest.Version,
    };
}
