using System.Diagnostics.CodeAnalysis;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Combat;

/// <summary>
/// Applies one currently supported physical-damage command to an immutable combat snapshot.
/// </summary>
/// <remarks>
/// This resolver intentionally has no turn queue, AI, Guard, rewards, randomness, or Godot
/// dependency. It owns only the rules needed to prove one complete action: validate intent,
/// calculate deterministic damage, replace the target state, and emit battle-local facts.
/// </remarks>
public sealed class CombatResolver : ICombatResolver
{
    private readonly IContentCatalog _content;

    public CombatResolver(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>
    /// Resolves one free, single-enemy physical ability and returns new state plus typed events.
    /// </summary>
    /// <exception cref="CombatCommandValidationException">
    /// The command is not currently legal for the supplied snapshot.
    /// </exception>
    public CombatResolution Resolve(CombatSnapshot current, CombatCommand command)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(command);

        BattleOutcome startingOutcome = current.Outcome;
        if (startingOutcome != BattleOutcome.InProgress)
        {
            Reject(
                CombatCommandProblemCodes.BattleAlreadyEnded,
                $"Combat command cannot be resolved because the battle outcome is "
                + $"'{startingOutcome}'.");
        }

        LocatedCombatant actor = FindRequiredCombatant(
            current,
            command.ActingCombatantId,
            CombatCommandProblemCodes.ActorMissing,
            "Acting combatant");
        if (actor.Value.IsDefeated)
        {
            Reject(
                CombatCommandProblemCodes.ActorDefeated,
                $"Defeated combatant '{actor.Value.InstanceId}' cannot act.");
        }

        if (string.IsNullOrWhiteSpace(command.AbilityId)
            || !actor.Value.AbilityIds.Contains(command.AbilityId, StringComparer.Ordinal))
        {
            Reject(
                CombatCommandProblemCodes.AbilityNotOwned,
                $"Combatant '{actor.Value.InstanceId}' cannot use ability "
                + $"'{command.AbilityId ?? "<null>"}'.");
        }

        if (!_content.TryGet<AbilityDefinition>(command.AbilityId, out AbilityDefinition? ability))
        {
            Reject(
                CombatCommandProblemCodes.AbilityMissing,
                $"Owned ability '{command.AbilityId}' is missing from the content catalog.");
        }

        // Resource pools such as current MP do not exist yet. Even a zero-valued authored cost
        // with a resource ID is rejected so the resolver never pretends it paid a resource.
        if (!CombatAbilityExecutionSupport.HasSupportedCost(ability))
        {
            Reject(
                CombatCommandProblemCodes.AbilityCostUnsupported,
                $"Ability '{ability.Id}' declares a resource cost, but combat resource "
                + "payment is not implemented.");
        }

        if (command.TargetCombatantIds.Count != 1)
        {
            Reject(
                CombatCommandProblemCodes.TargetCountInvalid,
                $"Ability '{ability.Id}' requires exactly one target; received "
                + $"{command.TargetCombatantIds.Count}.");
        }

        LocatedCombatant target = FindRequiredCombatant(
            current,
            command.TargetCombatantIds[0],
            CombatCommandProblemCodes.TargetMissing,
            "Target combatant");
        if (target.Value.IsDefeated)
        {
            Reject(
                CombatCommandProblemCodes.TargetDefeated,
                $"Defeated combatant '{target.Value.InstanceId}' cannot be targeted by "
                + $"ability '{ability.Id}'.");
        }

        if (target.Value.Side == actor.Value.Side)
        {
            Reject(
                CombatCommandProblemCodes.TargetSameSide,
                $"Ability '{ability.Id}' requires an opposing target, but "
                + $"'{actor.Value.InstanceId}' and '{target.Value.InstanceId}' are both on "
                + $"the '{actor.Value.Side}' side.");
        }

        if (!CombatAbilityExecutionSupport.HasSupportedContract(ability))
        {
            Reject(
                CombatCommandProblemCodes.AbilityContractUnsupported,
                $"Ability '{ability.Id}' uses targeting '{ability.TargetingId}' and ruleset "
                + $"'{ability.RulesetId}'. Milestone 3.10 supports only "
                + $"'{AbilityTargetingIds.SingleEnemy}' with "
                + $"'{AbilityRulesetIds.PhysicalDamage}'.");
        }

        decimal power = RequirePositivePower(ability);
        string damageTypeId = RequireDamageTypeId(ability);
        int strength = RequireStatistic(actor.Value, CombatStatisticIds.Strength);
        int defense = RequireStatistic(target.Value, CombatStatisticIds.Defense);
        int damagePercentModifier = target.Value.DamageTypePercentModifiers.TryGetValue(
            damageTypeId,
            out int authoredModifier)
            ? authoredModifier
            : 0;
        int appliedDamage = CalculateAppliedDamage(
            strength,
            power,
            defense,
            target.Value.CurrentHp,
            damagePercentModifier);
        int nextHp = target.Value.CurrentHp - appliedDamage;

        // Copy the ordered collection and replace exactly the target's slot. Every unaffected
        // combatant keeps the same immutable instance; the CombatSnapshot constructor then owns
        // a new read-only list and preserves formation/order/round data.
        CombatantSnapshot[] nextCombatants = current.Combatants.ToArray();
        nextCombatants[target.Index] = target.Value.WithCurrentHp(nextHp);
        var nextSnapshot = new CombatSnapshot(current.Round, nextCombatants);

        var events = new List<CombatEvent>
        {
            new DamageApplied(
                actor.Value.InstanceId,
                target.Value.InstanceId,
                ability.Id,
                damageTypeId,
                damagePercentModifier,
                appliedDamage,
                target.Value.CurrentHp,
                nextHp),
        };
        if (nextHp == 0)
        {
            events.Add(new CombatantDefeated(target.Value.InstanceId));
        }

        // Outcome is derived from the replacement snapshot, never guessed from the selected
        // target. This matters when a side contains several combatants: defeating one member is
        // not a victory, while defeating its final living member emits exactly one terminal fact.
        BattleOutcome nextOutcome = nextSnapshot.Outcome;
        if (nextOutcome != BattleOutcome.InProgress)
        {
            events.Add(new BattleEnded(nextOutcome));
        }

        return new CombatResolution(nextSnapshot, events);
    }

    private static LocatedCombatant FindRequiredCombatant(
        CombatSnapshot snapshot,
        string? instanceId,
        string missingProblemCode,
        string role)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            Reject(missingProblemCode, $"{role} ID cannot be blank.");
        }

        LocatedCombatant? found = null;
        for (int index = 0; index < snapshot.Combatants.Count; index++)
        {
            CombatantSnapshot candidate = snapshot.Combatants[index];
            if (!string.Equals(candidate.InstanceId, instanceId, StringComparison.Ordinal))
            {
                continue;
            }

            if (found is not null)
            {
                throw new InvalidDataException(
                    $"Combat snapshot contains duplicate battle-local instance ID "
                    + $"'{instanceId}'.");
            }

            found = new LocatedCombatant(candidate, index);
        }

        if (found is null)
        {
            Reject(missingProblemCode, $"{role} '{instanceId}' does not exist.");
        }

        return found;
    }

    private static int RequireStatistic(CombatantSnapshot combatant, string statisticId)
    {
        if (!combatant.Statistics.TryGetValue(statisticId, out int value))
        {
            throw new InvalidDataException(
                $"Combatant '{combatant.InstanceId}' is missing required combat statistic "
                + $"'{statisticId}'.");
        }

        return value;
    }

    private static decimal RequirePositivePower(AbilityDefinition ability)
    {
        IReadOnlyDictionary<string, decimal> parameters = ability.NumericParameters
            ?? throw new InvalidDataException(
                $"Ability '{ability.Id}' has a null numeric-parameter map.");
        if (!parameters.TryGetValue(AbilityNumericParameterIds.Power, out decimal power)
            || power <= 0m)
        {
            throw new InvalidDataException(
                $"Physical-damage ability '{ability.Id}' must have a positive "
                + $"'{AbilityNumericParameterIds.Power}' parameter.");
        }

        return power;
    }

    private static string RequireDamageTypeId(AbilityDefinition ability)
    {
        string damageTypeId = ability.DamageTypeId ?? DamageTypeIds.Energy;
        if (!DamageTypeIds.IsSupported(damageTypeId))
        {
            throw new InvalidDataException(
                $"Physical-damage ability '{ability.Id}' has unsupported damage type "
                + $"'{damageTypeId}'.");
        }

        return damageTypeId;
    }

    private static int CalculateAppliedDamage(
        int attackerStrength,
        decimal authoredPower,
        int defenderDefense,
        int remainingHp,
        int damagePercentModifier)
    {
        if (damagePercentModifier < -100)
        {
            throw new InvalidDataException(
                $"Damage modifier {damagePercentModifier} is below the -100 immunity floor.");
        }

        if (damagePercentModifier == -100)
        {
            return 0;
        }

        // Apply the signed percentage after Strength, power, Defense, and the base minimum, then
        // round down once. A positive but heavily resisted hit still deals one damage. Comparing
        // against the amount needed for defeat first keeps decimal.MaxValue power overflow-safe.
        decimal multiplier = (100m + damagePercentModifier) / 100m;
        decimal statisticDifference = (decimal)attackerStrength - defenderDefense;
        decimal baseDamageNeededToDefeat = remainingHp / multiplier;
        decimal powerNeededToDefeat = baseDamageNeededToDefeat - statisticDifference;
        if (authoredPower >= powerNeededToDefeat)
        {
            return remainingHp;
        }

        decimal rawDamage = authoredPower + statisticDifference;
        decimal modifiedDamage = Math.Max(1m, rawDamage) * multiplier;
        int roundedDamage = Math.Max(
            1,
            decimal.ToInt32(decimal.Floor(modifiedDamage)));
        return Math.Min(roundedDamage, remainingHp);
    }

    [DoesNotReturn]
    private static void Reject(string problemCode, string message) =>
        throw new CombatCommandValidationException(problemCode, message);

    private sealed record LocatedCombatant(CombatantSnapshot Value, int Index);
}
