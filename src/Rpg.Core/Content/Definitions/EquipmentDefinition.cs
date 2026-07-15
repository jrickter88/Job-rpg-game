namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Adds equippable behavior to an ordinary item without duplicating its name, price,
/// description, or inventory properties.
/// </summary>
public sealed record EquipmentDefinition : ContentDefinition
{
    /// <summary>Stable ID of the corresponding inventory item.</summary>
    public required string ItemId { get; init; }

    /// <summary>Stable game-owned slot ID, such as <c>slot.weapon.main-hand</c>.</summary>
    public required string SlotId { get; init; }

    /// <summary>Additive equipped bonuses keyed by statistic definition ID.</summary>
    public Dictionary<string, int> StatisticModifiers { get; init; } = [];

    /// <summary>
    /// Weapon damage composition keyed by code-owned damage type ID. Values are whole
    /// percentages and a nonempty profile must total exactly 100.
    /// </summary>
    /// <remarks>
    /// This milestone validates and exposes the authored profile but does not apply it because
    /// persistent equipment ownership and active weapon selection do not exist yet.
    /// </remarks>
    public Dictionary<string, int> WeaponDamagePercentages { get; init; } = [];

    /// <summary>Abilities available only while this equipment is active.</summary>
    public List<string> GrantedAbilityIds { get; init; } = [];
}
