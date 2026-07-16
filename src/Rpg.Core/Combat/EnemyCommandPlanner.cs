using System.Diagnostics.CodeAnalysis;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Combat;

/// <summary>
/// Produces one deterministic command using the deliberately small Milestone 3.12 enemy policy.
/// </summary>
/// <remarks>
/// The planner scans the enemy's authored ability order and selects the first contract the
/// current resolver can execute. It then targets the living party combatant with the lowest
/// absolute current HP, breaking ties by ordinal battle-local instance ID. It performs no
/// damage calculation and has no privileged AI command path.
/// </remarks>
public sealed class EnemyCommandPlanner : IEnemyCommandPlanner
{
    private readonly IContentCatalog _content;

    public EnemyCommandPlanner(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <inheritdoc />
    public CombatCommand Plan(CombatSnapshot current, string enemyInstanceId)
    {
        ArgumentNullException.ThrowIfNull(current);

        CombatantSnapshot enemy = FindRequiredCombatant(current, enemyInstanceId);
        if (enemy.Side != BattleSide.Enemy)
        {
            Reject(
                EnemyCommandPlanningProblemCodes.ActorNotEnemy,
                $"Combatant '{enemy.InstanceId}' belongs to the '{enemy.Side}' side, not "
                + "the enemy side.");
        }

        if (enemy.IsDefeated)
        {
            Reject(
                EnemyCommandPlanningProblemCodes.ActorDefeated,
                $"Defeated enemy '{enemy.InstanceId}' cannot receive a planned command.");
        }

        AbilityDefinition? selectedAbility = null;
        foreach (string abilityId in enemy.AbilityIds)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                throw new InvalidDataException(
                    $"Enemy combatant '{enemy.InstanceId}' contains a blank ability ID.");
            }

            if (!_content.TryGet<AbilityDefinition>(abilityId, out AbilityDefinition? ability))
            {
                throw new InvalidDataException(
                    $"Enemy combatant '{enemy.InstanceId}' owns missing ability "
                    + $"'{abilityId}'.");
            }

            if (CombatAbilityExecutionSupport.IsCurrentlyUsable(enemy, ability))
            {
                selectedAbility = ability;
                break;
            }
        }

        if (selectedAbility is null)
        {
            Reject(
                EnemyCommandPlanningProblemCodes.AbilityUnavailable,
                $"Living enemy '{enemy.InstanceId}' has no ability the current combat "
                + "resolver can execute.");
        }

        CombatantSnapshot? target = current.Combatants
            .Where(combatant => combatant.Side == BattleSide.Party && !combatant.IsDefeated)
            .OrderBy(combatant => combatant.CurrentHp)
            .ThenBy(combatant => combatant.InstanceId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (target is null)
        {
            Reject(
                EnemyCommandPlanningProblemCodes.TargetUnavailable,
                $"Living enemy '{enemy.InstanceId}' has no living party target.");
        }

        return new CombatCommand(enemy.InstanceId, selectedAbility.Id, [target.InstanceId]);
    }

    private static CombatantSnapshot FindRequiredCombatant(
        CombatSnapshot current,
        string? instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            Reject(
                EnemyCommandPlanningProblemCodes.ActorMissing,
                "Enemy instance ID cannot be blank.");
        }

        CombatantSnapshot? result = null;
        foreach (CombatantSnapshot combatant in current.Combatants)
        {
            if (!string.Equals(combatant.InstanceId, instanceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (result is not null)
            {
                throw new InvalidDataException(
                    $"Combat snapshot contains duplicate battle-local instance ID "
                    + $"'{instanceId}'.");
            }

            result = combatant;
        }

        return result ?? throw new EnemyCommandPlanningException(
            EnemyCommandPlanningProblemCodes.ActorMissing,
            $"Enemy combatant '{instanceId}' does not exist.");
    }

    [DoesNotReturn]
    private static void Reject(string problemCode, string message) =>
        throw new EnemyCommandPlanningException(problemCode, message);
}
