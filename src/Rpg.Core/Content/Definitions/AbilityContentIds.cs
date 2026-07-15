namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Stable target-selection contracts understood by the current game build.
/// </summary>
/// <remarks>
/// These are code-owned IDs rather than content records because each value eventually needs
/// trusted target-selection and command-validation code. A JSON mod may select a supported
/// value, but declaring a new string does not create new executable behavior.
/// </remarks>
public static class AbilityTargetingIds
{
    /// <summary>The acting combatant is the only legal target.</summary>
    public const string Self = "target.self";

    /// <summary>Exactly one combatant on the opposing side is selected.</summary>
    public const string SingleEnemy = "target.enemy.single";
}

/// <summary>
/// Stable behavior-family contracts understood by authored ability data.
/// </summary>
/// <remarks>
/// A ruleset ID selects trusted C# behavior; it is not a method name, script path, or plugin
/// hook. Adding a genuinely new effect family therefore requires code, validation, and tests.
/// Most new abilities should reuse one of these IDs and vary only validated numeric values.
/// </remarks>
public static class AbilityRulesetIds
{
    /// <summary>Applies the Guard-style defensive reduction to the acting combatant.</summary>
    public const string Guard = "rules.defense.guard";

    /// <summary>Uses the physical-damage formula against one opposing combatant.</summary>
    public const string PhysicalDamage = "rules.damage.physical";
}

/// <summary>
/// Stable keys accepted in <see cref="AbilityDefinition.NumericParameters"/> by current rulesets.
/// </summary>
public static class AbilityNumericParameterIds
{
    /// <summary>Positive strength supplied to the physical-damage ruleset.</summary>
    public const string Power = "power";

    /// <summary>Fraction from greater than zero through one used by the Guard ruleset.</summary>
    public const string DamageReduction = "damage-reduction";
}
