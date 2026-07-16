namespace RpgGame.Core.Combat;

/// <summary>
/// Stable statistic IDs whose meanings are required by implemented combat rules.
/// </summary>
/// <remarks>
/// This is not a closed list of all statistics. <see cref="CombatStatisticResolver"/> still
/// resolves every registered statistic dynamically. Constants are added here only when trusted
/// combat code needs one statistic's specific meaning; content-defined statistics that no rule
/// consumes continue to require no code change.
/// </remarks>
public static class CombatStatisticIds
{
    public const string MaxHp = "stat.max-hp";

    /// <summary>
    /// Maximum MP selects the transient current-MP pool used by supported ability costs.
    /// </summary>
    public const string MaxMp = "stat.max-mp";

    public const string Strength = "stat.strength";

    public const string Defense = "stat.defense";

    /// <summary>
    /// Higher values act first during a deterministic round; ties use instance ID order.
    /// </summary>
    public const string Speed = "stat.speed";
}
