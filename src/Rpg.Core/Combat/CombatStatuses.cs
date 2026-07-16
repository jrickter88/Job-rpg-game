using System.Collections.ObjectModel;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Combat;

/// <summary>Immutable active instance of one authored status in one battle.</summary>
public sealed record ActiveStatusEffect
{
	public ActiveStatusEffect(
		string statusEffectId,
		string? sourceCombatantId,
		long appliedTimelineTime,
		long duration,
		int stackCount = 1)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(statusEffectId);
		if (sourceCombatantId is not null && string.IsNullOrWhiteSpace(sourceCombatantId))
		{
			throw new ArgumentException(
				"A status source combatant ID must be blank or a stable instance ID.",
				nameof(sourceCombatantId));
		}
		if (appliedTimelineTime < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(appliedTimelineTime));
		}

		if (duration <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(duration));
		}

		if (stackCount < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(stackCount));
		}

		StatusEffectId = statusEffectId;
		SourceCombatantId = sourceCombatantId;
		AppliedTimelineTime = appliedTimelineTime;
		Duration = duration;
		StackCount = stackCount;
	}

	public string StatusEffectId { get; }

	public string? SourceCombatantId { get; }

	public long AppliedTimelineTime { get; }

	public long Duration { get; }

	public int StackCount { get; }

	public long ExpiresAt => checked(AppliedTimelineTime + Duration);

	public bool IsExpired(long timelineTime) => timelineTime >= ExpiresAt;
}

public sealed class StatusResolution
{
	public StatusResolution(CombatSnapshot next, IReadOnlyList<CombatEvent> events)
	{
		ArgumentNullException.ThrowIfNull(next);
		ArgumentNullException.ThrowIfNull(events);
		Next = next;
		Events = new ReadOnlyCollection<CombatEvent>(events.ToArray());
	}

	public CombatSnapshot Next { get; }

	public IReadOnlyList<CombatEvent> Events { get; }
}

/// <summary>Applies and removes transient statuses through immutable snapshot replacements.</summary>
public sealed class CombatStatusService
{
	private readonly IContentCatalog _content;

	public CombatStatusService(IContentCatalog content)
	{
		_content = content ?? throw new ArgumentNullException(nameof(content));
	}

	public StatusResolution ApplyStatus(
		CombatSnapshot snapshot,
		string? sourceCombatantId,
		string targetCombatantId,
		string statusEffectId)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		StatusEffectDefinition definition = _content.GetRequired<StatusEffectDefinition>(
			statusEffectId);
		CombatantSnapshot target = snapshot.GetRequiredCombatant(targetCombatantId);
		if (target.IsDefeated)
		{
			throw new StatusValidationException(
				StatusProblemCodes.TargetDefeated,
				$"Defeated combatant '{targetCombatantId}' cannot receive status "
				+ $"'{statusEffectId}'.");
		}

		if (sourceCombatantId is not null)
		{
			snapshot.GetRequiredCombatant(sourceCombatantId);
		}

		ActiveStatusEffect? existing = target.ActiveStatusEffects.FirstOrDefault(status =>
			string.Equals(status.StatusEffectId, statusEffectId, StringComparison.Ordinal));
		if (existing is not null
			&& string.Equals(
				definition.StackingRuleId,
				StatusStackingRuleIds.IgnoreIfPresent,
				StringComparison.Ordinal))
		{
			return new StatusResolution(
				snapshot,
				[new StatusIgnored(
					sourceCombatantId,
					targetCombatantId,
					statusEffectId,
					"ignore-if-present")]);
		}

		ActiveStatusEffect replacement = new(
			statusEffectId,
			sourceCombatantId,
			snapshot.TimelineTime,
			definition.DefaultDuration);
		CombatEvent statusEvent = existing is null
			? new StatusApplied(
				sourceCombatantId,
				targetCombatantId,
				statusEffectId,
				definition.DefaultDuration,
				replacement.StackCount)
			: new StatusRefreshed(
				sourceCombatantId,
				targetCombatantId,
				statusEffectId,
				definition.DefaultDuration,
				replacement.StackCount);
		ActiveStatusEffect[] statuses = target.ActiveStatusEffects
			.Where(status => !string.Equals(
				status.StatusEffectId,
				statusEffectId,
				StringComparison.Ordinal))
			.Append(replacement)
			.ToArray();
		return new StatusResolution(
			ReplaceCombatant(snapshot, target.WithActiveStatusEffects(statuses)),
			[statusEvent]);
	}

	public StatusResolution RemoveStatus(
		CombatSnapshot snapshot,
		string targetCombatantId,
		string statusEffectId,
		string reason = "removed")
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		CombatantSnapshot target = snapshot.GetRequiredCombatant(targetCombatantId);
		ActiveStatusEffect? status = target.ActiveStatusEffects.FirstOrDefault(candidate =>
			string.Equals(candidate.StatusEffectId, statusEffectId, StringComparison.Ordinal));
		if (status is null)
		{
			return new StatusResolution(snapshot, []);
		}

		ActiveStatusEffect[] remaining = target.ActiveStatusEffects
			.Where(candidate => !ReferenceEquals(candidate, status))
			.ToArray();
		return new StatusResolution(
			ReplaceCombatant(snapshot, target.WithActiveStatusEffects(remaining)),
			[new StatusRemoved(targetCombatantId, statusEffectId, reason)]);
	}

	public StatusResolution ExpireStatuses(CombatSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		CombatSnapshot next = snapshot;
		var events = new List<CombatEvent>();
		foreach (CombatantSnapshot combatant in snapshot.Combatants)
		{
			ActiveStatusEffect[] expired = combatant.ActiveStatusEffects
				.Where(status => status.IsExpired(snapshot.TimelineTime))
				.ToArray();
			if (expired.Length == 0)
			{
				continue;
			}

			ActiveStatusEffect[] remaining = combatant.ActiveStatusEffects
				.Where(status => !status.IsExpired(snapshot.TimelineTime))
				.ToArray();
			next = ReplaceCombatant(next, combatant.WithActiveStatusEffects(remaining));
			events.AddRange(expired.Select(status => new StatusExpired(
				combatant.InstanceId,
				status.StatusEffectId)));
		}

		return new StatusResolution(next, events);
	}

	public IReadOnlyList<ActiveStatusEffect> QueryActiveStatuses(
		CombatSnapshot snapshot,
		string combatantId)
	{
		CombatantSnapshot combatant = snapshot.GetRequiredCombatant(combatantId);
		return combatant.ActiveStatusEffects
			.Where(status => !status.IsExpired(snapshot.TimelineTime))
			.ToArray();
	}

	public static int ResolveEffectiveSpeed(
		CombatSnapshot snapshot,
		CombatantSnapshot combatant,
		IContentCatalog content)
	{
		int baseSpeed = CombatTimeline.EffectiveSpeed(combatant);
		int percent = combatant.ActiveStatusEffects
			.Where(status => !status.IsExpired(snapshot.TimelineTime))
			.Select(status => content.GetRequired<StatusEffectDefinition>(status.StatusEffectId))
			.Where(definition => definition.EffectKindIds.Contains(
				StatusEffectKindIds.ModifySpeedPercent,
				StringComparer.Ordinal))
			.Sum(definition => definition.SpeedPercentModifier);
		return Math.Max(1, (int)Math.Floor(baseSpeed * (100m + percent) / 100m));
	}

	private static CombatSnapshot ReplaceCombatant(
		CombatSnapshot snapshot,
		CombatantSnapshot replacement)
	{
		CombatantSnapshot[] combatants = snapshot.Combatants.ToArray();
		int index = Array.FindIndex(
			combatants,
			combatant => string.Equals(
				combatant.InstanceId,
				replacement.InstanceId,
				StringComparison.Ordinal));
		if (index < 0)
		{
			throw new InvalidDataException(
				$"Combat snapshot lost combatant '{replacement.InstanceId}'.");
		}

		combatants[index] = replacement;
		return new CombatSnapshot(snapshot.Round, snapshot.TimelineTime, combatants);
	}
}

public static class StatusProblemCodes
{
	public const string TargetDefeated = "combat.status.target-defeated";
}

public sealed class StatusValidationException : InvalidOperationException
{
	public StatusValidationException(string problemCode, string message)
		: base(message)
	{
		ProblemCode = problemCode;
	}

	public string ProblemCode { get; }
}
