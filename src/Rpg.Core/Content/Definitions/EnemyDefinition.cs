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
    /// Authored rectangular size on the 4 × 4 enemy formation. Omission remains compatible
    /// and means one cell; explicit JSON null is rejected by content validation.
    /// </summary>
    public EnemyFootprintDefinition FormationFootprint { get; init; } = new();

    /// <summary>Independent item-drop possibilities evaluated after victory.</summary>
    public List<LootEntryDefinition> Loot { get; init; } = [];
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

/// <summary>Embedded description of one possible item drop.</summary>
public sealed record LootEntryDefinition
{
    /// <summary>Stable ID of the item that may drop.</summary>
    public required string ItemId { get; init; }

    /// <summary>Probability from 0 through 1; for example, 0.125 means 12.5%.</summary>
    public decimal Chance { get; init; }

    /// <summary>Smallest quantity awarded when this entry succeeds.</summary>
    public int MinQuantity { get; init; } = 1;

    /// <summary>Largest quantity awarded when this entry succeeds.</summary>
    public int MaxQuantity { get; init; } = 1;
}
