using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Combat;

/// <summary>
/// Single description of the ability contract the current action resolver can execute.
/// </summary>
/// <remarks>
/// Both command execution and enemy planning need this answer. Keeping it here prevents the AI
/// from offering an ability that <see cref="CombatResolver"/> will reject. This is deliberately
/// not a registry or generic effect engine; it describes only the one implemented physical
/// contract and should grow only alongside real resolver behavior and focused tests.
/// </remarks>
public static class CombatAbilityExecutionSupport
{
    public static bool HasSupportedCost(AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        return ability.CostStatisticId is null
            ? ability.CostAmount == 0
            : string.Equals(
                ability.CostStatisticId,
                CombatStatisticIds.MaxMp,
                StringComparison.Ordinal)
              && ability.CostAmount >= 0;
    }

    public static bool HasSupportedContract(AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(ability);
        return string.Equals(
                ability.TargetingId,
                AbilityTargetingIds.SingleEnemy,
                StringComparison.Ordinal)
            && string.Equals(
                ability.RulesetId,
                AbilityRulesetIds.PhysicalDamage,
                StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns whether the actor can currently execute this supported contract, including its
    /// transient MP balance. This is shared by menu projection and enemy planning.
    /// </summary>
    public static bool IsCurrentlyUsable(CombatantSnapshot actor, AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(ability);
        return HasSupportedCost(ability)
            && HasSupportedContract(ability)
            && HasSufficientResource(actor, ability);
    }

    public static bool HasSufficientResource(CombatantSnapshot actor, AbilityDefinition ability)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(ability);
        return ability.CostStatisticId switch
        {
            null => ability.CostAmount == 0,
            CombatStatisticIds.MaxMp => actor.CurrentMp >= ability.CostAmount,
            _ => false,
        };
    }
}
