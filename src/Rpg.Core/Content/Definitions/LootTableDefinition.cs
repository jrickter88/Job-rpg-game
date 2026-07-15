using System.Text.Json.Serialization;

namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Defines one reusable set of independent item-drop possibilities.
/// </summary>
/// <remarks>
/// A loot table is immutable authored content. It describes what may be awarded, but it does
/// not roll randomness, mutate inventory, or remember whether an enemy was defeated. Keeping
/// this record separate from <see cref="EnemyDefinition"/> lets several enemies share one
/// table and lets a content author rebalance drops without editing combat statistics or AI.
/// A future pure-core reward resolver will interpret these entries after victory.
/// </remarks>
public sealed record LootTableDefinition : ContentDefinition
{
    /// <summary>
    /// Independent item-drop entries evaluated in authored order by a future resolver.
    /// </summary>
    /// <remarks>
    /// The property must be present in JSON even when the deliberate value is an empty array.
    /// Empty tables are legal and provide a convenient way to disable a referenced table
    /// without rewriting every enemy that uses it. Multiple entries for the same item are
    /// also legal because each entry represents an independent future roll.
    /// </remarks>
    [JsonRequired]
    public List<LootEntryDefinition> Entries { get; init; } = [];
}

/// <summary>One independently evaluated item possibility inside a loot table.</summary>
/// <remarks>
/// Entries have no stable ID because no save or external record addresses an individual
/// entry. The containing loot-table ID is the permanent cross-record identity.
/// </remarks>
public sealed record LootEntryDefinition
{
    /// <summary>Stable ID of the item that may be awarded.</summary>
    public required string ItemId { get; init; }

    /// <summary>Probability from 0 through 1; for example, 0.125 means 12.5%.</summary>
    /// <remarks>
    /// Zero is intentionally legal so an author can temporarily disable an entry without
    /// deleting it. One represents a guaranteed award once reward resolution exists.
    /// </remarks>
    [JsonRequired]
    public decimal Chance { get; init; }

    /// <summary>Smallest quantity awarded when this entry succeeds.</summary>
    [JsonRequired]
    public int MinQuantity { get; init; } = 1;

    /// <summary>Largest quantity awarded when this entry succeeds.</summary>
    [JsonRequired]
    public int MaxQuantity { get; init; } = 1;
}
