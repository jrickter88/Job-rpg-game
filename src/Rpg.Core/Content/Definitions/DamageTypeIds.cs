namespace RpgGame.Core.Content.Definitions;

/// <summary>Stable code-owned IDs for damage categories understood by combat rules.</summary>
/// <remarks>
/// Content may select these IDs but cannot create behavior by inventing a new string. Adding a
/// future damage type requires trusted code, validation, documentation, and focused tests.
/// </remarks>
public static class DamageTypeIds
{
    public const string Slash = "damage-type.slash";

    public const string Energy = "damage-type.energy";

    public const string Fire = "damage-type.fire";

    public const string Ice = "damage-type.ice";

    public const string Lightning = "damage-type.lightning";

    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly(
        new[] { Slash, Energy, Fire, Ice, Lightning });

    public static bool IsSupported(string? damageTypeId) => damageTypeId is
        Slash or Energy or Fire or Ice or Lightning;
}
