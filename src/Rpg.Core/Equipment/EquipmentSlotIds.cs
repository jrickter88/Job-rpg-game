namespace RpgGame.Core.Equipment;

/// <summary>Stable supported equipment slots in their character-screen display order.</summary>
public static class EquipmentSlotIds
{
    public const string MainHandWeapon = "slot.weapon.main-hand";
    public const string OffHandWeapon = "slot.weapon.off-hand";
    public const string BodyArmor = "slot.armor.body";
    public const string FeetArmor = "slot.armor.feet";
    public const string HelmArmor = "slot.armor.helm";
    public const string AccessoryOne = "slot.accessory.one";
    public const string AccessoryTwo = "slot.accessory.two";
    public const string AccessoryCategory = "slot.accessory";

    public static IReadOnlyList<string> Supported { get; } =
        [MainHandWeapon, OffHandWeapon, BodyArmor, FeetArmor, HelmArmor, AccessoryOne, AccessoryTwo];

    public static bool IsCompatible(string authoredSlotId, string equippedSlotId) =>
        string.Equals(authoredSlotId, equippedSlotId, StringComparison.Ordinal)
        || (string.Equals(authoredSlotId, AccessoryCategory, StringComparison.Ordinal)
            && (string.Equals(equippedSlotId, AccessoryOne, StringComparison.Ordinal)
                || string.Equals(equippedSlotId, AccessoryTwo, StringComparison.Ordinal)));
}
