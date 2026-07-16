namespace RpgGame.Core.Equipment;

/// <summary>Stable supported equipment slots for the first equipment slice.</summary>
public static class EquipmentSlotIds
{
    public const string MainHandWeapon = "slot.weapon.main-hand";

    public static IReadOnlyList<string> Supported { get; } = [MainHandWeapon];
}
