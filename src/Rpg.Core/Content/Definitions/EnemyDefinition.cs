using System.Text.Json.Serialization;
using RpgGame.Core.Combat.Formation;

namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Defines a reusable enemy species/template from which encounter combatants are created.
/// </summary>
/// <remarks>
/// Per-battle instance data such as current HP, status effects, and chosen actions belongs
/// in combat state. This record only holds stable authored defaults.
/// </remarks>
public sealed record EnemyDefinition : ContentDefinition
{
    /// <summary>Localization key for the enemy name shown to the player.</summary>
    public required string DisplayNameKey { get; init; }

    /// <summary>Authored level used by future scaling and reward calculations.</summary>
    public int Level { get; init; } = 1;

    /// <summary>Base values keyed by statistic definition ID.</summary>
    public Dictionary<string, int> Statistics { get; init; } = [];

    /// <summary>Abilities available to this enemy's future AI.</summary>
    public List<string> AbilityIds { get; init; } = [];

    /// <summary>
    /// Signed whole-percent damage adjustments keyed by code-owned damage type ID.
    /// Positive values are weaknesses, negative values are resistances, and -100 is immunity.
    /// Omitted entries are neutral.
    /// </summary>
    public Dictionary<string, int> DamageTypePercentModifiers { get; init; } = [];

    /// <summary>
    /// Authored rectangular size on the 4 × 4 enemy formation. Omission remains compatible
    /// and means one cell; explicit JSON null is rejected by content validation.
    /// </summary>
    public EnemyFootprintDefinition FormationFootprint { get; init; } = new();

    /// <summary>
    /// Stable ID of the reusable loot table for this enemy, or null when it drops no items.
    /// </summary>
    /// <remarks>
    /// The member is required in enemy-schema version 2 JSON so accidentally forgetting loot
    /// ownership cannot silently mean "no drops." A present JSON null is the explicit no-loot
    /// choice. The referenced table remains definition data; the loot resolver returns
    /// transient award facts, while later campaign code decides whether to grant inventory.
    /// </remarks>
    [JsonRequired]
    public string? LootTableId { get; init; }
}

/// <summary>Rectangular rows-by-columns footprint authored for one enemy species.</summary>
public sealed record EnemyFootprintDefinition
{
    /// <summary>Occupied rows extending downward from the encounter anchor.</summary>
    public int Rows { get; init; } = 1;

    /// <summary>Occupied depth columns extending backward from the encounter anchor.</summary>
    public int Columns { get; init; } = 1;

    /// <summary>
    /// Converts authored content into the immutable footprint used by formation rules.
    /// </summary>
    /// <remarks>
    /// Validation deliberately happens in the production content validator. This conversion
    /// copies the authored values exactly—it never clamps or silently repairs invalid content.
    /// Keeping the conversion here prevents future battle consumers from repeatedly mapping
    /// the same two fields by hand while keeping this DTO separate from battle state.
    /// </remarks>
    public FormationFootprint ToFormationFootprint() => new(Rows, Columns);
}
