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
	private const string BasicAttackAbilityId = "ability.command.attack";
	private readonly IContentCatalog _content;
	private readonly IRandomSource _random;

	public CombatResolver(IContentCatalog content, IRandomSource? random = null)
	{
		_content = content ?? throw new ArgumentNullException(nameof(content));
		_random = random ?? new FixedRandomSource(100);
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

		if (!CombatAbilityExecutionSupport.HasSupportedCost(ability))
		{
			Reject(
				CombatCommandProblemCodes.AbilityCostUnsupported,
				$"Ability '{ability.Id}' has unsupported cost statistic "
				+ $"'{ability.CostStatisticId ?? "<null>"}'. Only null and "
				+ $"'{CombatStatisticIds.MaxMp}' are supported.");
		}

		if (!CombatAbilityExecutionSupport.HasSufficientResource(actor.Value, ability))
		{
			Reject(
				CombatCommandProblemCodes.AbilityResourceInsufficient,
				$"Combatant '{actor.Value.InstanceId}' has {actor.Value.CurrentMp} current MP, "
				+ $"but ability '{ability.Id}' requires {ability.CostAmount}.");
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

		if (!CombatAbilityExecutionSupport.HasSupportedContract(ability))
		{
			Reject(
				CombatCommandProblemCodes.AbilityContractUnsupported,
				$"Ability '{ability.Id}' uses targeting '{ability.TargetingId}' and ruleset "
				+ $"'{ability.RulesetId}', which is not executable.");
		}

		return ability.RulesetId switch
		{
			AbilityRulesetIds.PhysicalDamage => ResolvePhysicalDamage(
				current,
				actor,
				target,
				ability),
			AbilityRulesetIds.FlatHealing => ResolveFlatHealing(
				current,
				actor,
				target,
				ability),
			_ => throw new InvalidDataException(
				$"Supported ability '{ability.Id}' has unknown ruleset '{ability.RulesetId}'."),
		};
	}

	private CombatResolution ResolvePhysicalDamage(
		CombatSnapshot current,
		LocatedCombatant actor,
		LocatedCombatant target,
		AbilityDefinition ability)
	{
		if (target.Value.Side == actor.Value.Side)
		{
			Reject(
				CombatCommandProblemCodes.TargetSameSide,
				$"Ability '{ability.Id}' requires an opposing target, but "
				+ $"'{actor.Value.InstanceId}' and '{target.Value.InstanceId}' are both on "
				+ $"the '{actor.Value.Side}' side.");
		}

		decimal power = RequirePositivePower(ability);
		bool isBasicAttack = string.Equals(ability.Id, BasicAttackAbilityId, StringComparison.Ordinal);
		string damageTypeId = isBasicAttack
			? actor.Value.EquippedWeaponDamageTypeId
				?? (ability.DamageTypeId is null ? DamageTypeIds.Blunt : RequireDamageTypeId(ability))
			: RequireDamageTypeId(ability);
		int strength = RequireStatistic(actor.Value, CombatStatisticIds.Strength);
		int defense = RequireStatistic(target.Value, CombatStatisticIds.Defense);
		int damagePercentModifier = target.Value.DamageTypePercentModifiers.TryGetValue(
			damageTypeId,
			out int authoredModifier)
			? authoredModifier
			: 0;
		DamageVarianceDefinition variance = ability.DamageVariance
			?? actor.Value.EquippedWeaponDamageVariance
			?? (ability.AbilityKindId == AbilityKindIds.Magic
				? new DamageVarianceDefinition { MinimumPercent = 80, MaximumPercent = 120 }
				: new DamageVarianceDefinition { MinimumPercent = 95, MaximumPercent = 105 });
		(int appliedDamage, int variancePercent) = CalculateAppliedDamage(
			strength,
			isBasicAttack ? actor.Value.EquippedWeaponAttack : 0,
			power,
			defense,
			target.Value.CurrentHp,
			damagePercentModifier,
			variance,
			_random);
		int nextHp = target.Value.CurrentHp - appliedDamage;

		// Copy the ordered collection and replace exactly the target's slot. Every unaffected
        // combatant keeps the same immutable instance; the CombatSnapshot constructor then owns
        // a new read-only list and preserves formation/order/round data.
        int costAmount = ability.CostAmount;
        int nextMp = actor.Value.CurrentMp - costAmount;
        CombatantSnapshot[] nextCombatants = current.Combatants.ToArray();
        if (actor.Index == target.Index)
        {
            nextCombatants[actor.Index] = costAmount > 0
                ? actor.Value.WithCurrentMp(nextMp).WithCurrentHp(nextHp)
                : actor.Value.WithCurrentHp(nextHp);
        }
        else
        {
            if (costAmount > 0)
            {
                nextCombatants[actor.Index] = actor.Value.WithCurrentMp(nextMp);
            }

            nextCombatants[target.Index] = target.Value.WithCurrentHp(nextHp);
        }
        var nextSnapshot = new CombatSnapshot(current.Round, current.TimelineTime, nextCombatants);

        var events = new List<CombatEvent>();
        if (costAmount > 0)
        {
            events.Add(new ResourceSpent(
                actor.Value.InstanceId,
                ability.Id,
                ability.CostStatisticId!,
                costAmount,
                actor.Value.CurrentMp,
                nextMp));
        }

        events.Add(new DamageApplied(
            actor.Value.InstanceId,
            target.Value.InstanceId,
            ability.Id,
			damageTypeId,
			damagePercentModifier,
			variancePercent,
			appliedDamage,
            target.Value.CurrentHp,
            nextHp));
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

    private static CombatResolution ResolveFlatHealing(
        CombatSnapshot current,
        LocatedCombatant actor,
        LocatedCombatant target,
        AbilityDefinition ability)
    {
        if (target.Value.Side != actor.Value.Side)
        {
            Reject(
                CombatCommandProblemCodes.TargetAllyRequired,
				$"Ability '{ability.Id}' requires an ally target, but "
				+ $"'{actor.Value.InstanceId}' and '{target.Value.InstanceId}' are on "
                + "opposing sides.");
        }

        int authoredHealing = RequirePositiveWholePower(ability);
        int nextHp = Math.Min(target.Value.MaximumHp, target.Value.CurrentHp + authoredHealing);
        int appliedHealing = nextHp - target.Value.CurrentHp;
        int costAmount = ability.CostAmount;
        int nextMp = actor.Value.CurrentMp - costAmount;
        CombatantSnapshot[] nextCombatants = current.Combatants.ToArray();
        if (actor.Index == target.Index)
        {
            nextCombatants[actor.Index] = costAmount > 0
                ? actor.Value.WithCurrentMp(nextMp).WithCurrentHp(nextHp)
                : actor.Value.WithCurrentHp(nextHp);
        }
        else
        {
            if (costAmount > 0)
            {
                nextCombatants[actor.Index] = actor.Value.WithCurrentMp(nextMp);
            }

            nextCombatants[target.Index] = target.Value.WithCurrentHp(nextHp);
        }
        var nextSnapshot = new CombatSnapshot(current.Round, current.TimelineTime, nextCombatants);
        var events = new List<CombatEvent>();
        if (costAmount > 0)
        {
            events.Add(new ResourceSpent(
                actor.Value.InstanceId,
                ability.Id,
                ability.CostStatisticId!,
                costAmount,
                actor.Value.CurrentMp,
                nextMp));
        }

        events.Add(new HealingApplied(
            actor.Value.InstanceId,
            target.Value.InstanceId,
            ability.Id,
            appliedHealing,
            target.Value.CurrentHp,
            nextHp));
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

    private static int RequirePositiveWholePower(AbilityDefinition ability)
    {
        decimal power = RequirePositivePower(ability);
        if (decimal.Truncate(power) != power || power > int.MaxValue)
        {
            throw new InvalidDataException(
				$"Flat-healing ability '{ability.Id}' must have a positive whole "
				+ $"'{AbilityNumericParameterIds.Power}' parameter no greater than "
                + $"{int.MaxValue}.");
        }

        return decimal.ToInt32(power);
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

    private static (int Amount, int VariancePercent) CalculateAppliedDamage(
        int attackerStrength,
        int weaponAttack,
        decimal authoredPower,
        int defenderDefense,
        int remainingHp,
        int damagePercentModifier,
        DamageVarianceDefinition variance,
        IRandomSource random)
    {
        if (damagePercentModifier < -100)
        {
            throw new InvalidDataException(
                $"Damage modifier {damagePercentModifier} is below the -100 immunity floor.");
        }

        if (damagePercentModifier == -100)
        {
            return (0, 100);
        }

        // Apply the signed percentage after Strength, power, Defense, and the base minimum, then
        // round down once. A positive but heavily resisted hit still deals one damage. Comparing
        // against the amount needed for defeat first keeps decimal.MaxValue power overflow-safe.
        decimal multiplier = (100m + damagePercentModifier) / 100m;
        decimal statisticDifference = (decimal)attackerStrength + weaponAttack - defenderDefense;
        int variancePercent = random.Next(variance.MinimumPercent, variance.MaximumPercent + 1);
        decimal rawDamage = authoredPower + statisticDifference;
        decimal modifiedDamage = Math.Max(1m, rawDamage) * multiplier * variancePercent / 100m;
        if (modifiedDamage >= remainingHp)
        {
            return (remainingHp, variancePercent);
        }
        int roundedDamage = Math.Max(
            1,
            decimal.ToInt32(decimal.Floor(modifiedDamage)));
        return (Math.Min(roundedDamage, remainingHp), variancePercent);
    }

    [DoesNotReturn]
    private static void Reject(string problemCode, string message) =>
        throw new CombatCommandValidationException(problemCode, message);

    private sealed record LocatedCombatant(CombatantSnapshot Value, int Index);
}
