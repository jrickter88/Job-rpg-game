namespace RpgGame.Core.Combat;

/// <summary>
/// Stable statistic IDs whose meanings are required during combat-state initialization.
/// </summary>
/// <remarks>
/// This is not a closed list of all statistics. <see cref="CombatStatisticResolver"/> still
/// resolves every registered statistic dynamically. This constant only prevents the initial
/// current-HP rule from repeating a fragile string literal.
/// </remarks>
public static class CombatStatisticIds
{
    public const string MaxHp = "stat.max-hp";
}
