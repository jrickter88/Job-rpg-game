using System.Text.Json.Serialization;

namespace RpgGame.Core.Content;

/// <summary>
/// Base contract for every independently addressable game-content record.
/// </summary>
/// <remarks>
/// A definition describes game design data (for example, what a potion or enemy is),
/// not mutable progress from a particular playthrough. Definitions are modeled as
/// records so they have value-based equality and are convenient to deserialize.
/// Properties use <c>init</c> so a loader can construct them, after which the catalog
/// can treat them as immutable.
/// </remarks>
public abstract record ContentDefinition
{
    /// <summary>
    /// Version of this category's JSON shape, used if authored content needs migration.
    /// This is deliberately separate from the save-file format version.
    /// </summary>
    /// <remarks>
    /// The default keeps hand-built test/tool definitions concise. <see cref="JsonRequiredAttribute"/>
    /// still requires JSON authors to write the field explicitly, because silently treating a
    /// forgotten version as version 1 would make a later schema migration ambiguous.
    /// </remarks>
    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;

    /// <summary>
    /// Permanent, globally unique identity used by other content and save data.
    /// Display names and filenames may change; this ID must not change after release.
    /// </summary>
    public required string Id { get; init; }
}
