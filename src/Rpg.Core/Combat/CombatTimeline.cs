using RpgGame.Core.Combat.Formation;

namespace RpgGame.Core.Combat;

/// <summary>Closed, integer-only timing rules for wait-mode ATB initiative.</summary>
public static class CombatTimeline
{
    public const int BaseActionDelay = 1000;
    public const int SpeedBaseline = 4;
    public const int OpeningWindow = 100;
    public const int OpeningSpeedScale = 5;
    public const int MinActionDelay = 1;

    public static int CalculateActionDelay(CombatantSnapshot combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        int effectiveSpeed = combatant.Statistics.TryGetValue(
            CombatStatisticIds.Speed,
            out int speed)
            ? Math.Max(1, speed)
            : throw new InvalidDataException(
                $"Combatant '{combatant.InstanceId}' is missing required combat statistic "
                + $"'{CombatStatisticIds.Speed}'.");

        long delay = BaseActionDelay / (effectiveSpeed + SpeedBaseline);
        return (int)Math.Max(MinActionDelay, delay);
    }

    /// <summary>
    /// Creates a deterministic opening offset. The opening window keeps ordinary actors close,
    /// while an exceptionally fast actor may complete another delay before slow actors open.
    /// </summary>
    public static long CalculateOpeningActionTime(CombatantSnapshot combatant)
    {
        int effectiveSpeed = EffectiveSpeed(combatant);
        return Math.Max(0, OpeningWindow - (long)effectiveSpeed * OpeningSpeedScale);
    }

    public static IReadOnlyList<CombatantSnapshot> OrderReadyActors(
        IEnumerable<CombatantSnapshot> combatants)
    {
        ArgumentNullException.ThrowIfNull(combatants);
        return combatants
            .Where(combatant => !combatant.IsDefeated)
            .OrderBy(combatant => combatant.NextActionTime)
            .ThenByDescending(EffectiveSpeed)
            .ThenBy(combatant => combatant.Side == BattleSide.Party ? 0 : 1)
            .ThenBy(combatant => combatant.InstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    public static CombatantSnapshot SelectReadyActor(CombatSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.Outcome != BattleOutcome.InProgress)
        {
            throw new CombatTimelineValidationException(
                CombatTimelineProblemCodes.BattleAlreadyEnded,
                "A terminal battle has no next ready actor.");
        }

        return OrderReadyActors(snapshot.Combatants).FirstOrDefault()
            ?? throw new InvalidDataException("An in-progress battle has no living combatant.");
    }

    public static int EffectiveSpeed(CombatantSnapshot combatant)
    {
        ArgumentNullException.ThrowIfNull(combatant);
        return combatant.Statistics.TryGetValue(CombatStatisticIds.Speed, out int speed)
            ? Math.Max(1, speed)
            : throw new InvalidDataException(
                $"Combatant '{combatant.InstanceId}' is missing required combat statistic "
                + $"'{CombatStatisticIds.Speed}'.");
    }
}

public static class CombatTimelineProblemCodes
{
    public const string BattleAlreadyEnded = "combat.timeline.battle-already-ended";
    public const string ActorNotReady = "combat.timeline.actor-not-ready";
}

public sealed class CombatTimelineValidationException : InvalidOperationException
{
    public CombatTimelineValidationException(string problemCode, string message)
        : base(message)
    {
        ProblemCode = problemCode;
    }

    public string ProblemCode { get; }
}

/// <summary>Coordinates one ready actor, one command, and one immutable reschedule.</summary>
public sealed class CombatTimelineResolver : ICombatTimelineResolver
{
    private readonly ICombatResolver _actionResolver;

    public CombatTimelineResolver(ICombatResolver actionResolver)
    {
        _actionResolver = actionResolver
            ?? throw new ArgumentNullException(nameof(actionResolver));
    }

    public CombatResolution ResolveNext(CombatSnapshot current, CombatCommand command)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(command);

        CombatantSnapshot ready = CombatTimeline.SelectReadyActor(current);
        if (!string.Equals(ready.InstanceId, command.ActingCombatantId, StringComparison.Ordinal))
        {
            throw new CombatTimelineValidationException(
                CombatTimelineProblemCodes.ActorNotReady,
                $"Combatant '{command.ActingCombatantId}' is not ready; "
                + $"'{ready.InstanceId}' is the next ready actor.");
        }

        // Advancing to the selected actor's absolute time happens only after the command has
        // passed the action resolver. A rejected command therefore cannot move the timeline.
        CombatSnapshot readySnapshot = new CombatSnapshot(
            current.Round,
            ready.NextActionTime,
            current.Combatants);
        CombatResolution action = _actionResolver.Resolve(readySnapshot, command);
        if (action.Next.Outcome != BattleOutcome.InProgress)
        {
            return action;
        }

        CombatantSnapshot updatedActor = action.Next.GetRequiredCombatant(ready.InstanceId);
        long nextActionTime = checked(
            action.Next.TimelineTime + CombatTimeline.CalculateActionDelay(updatedActor));
        CombatantSnapshot[] nextCombatants = action.Next.Combatants.ToArray();
        int actorIndex = Array.FindIndex(
            nextCombatants,
            combatant => string.Equals(
                combatant.InstanceId,
                ready.InstanceId,
                StringComparison.Ordinal));
        if (actorIndex < 0)
        {
            throw new InvalidDataException(
                $"Resolved combat snapshot lost acting combatant '{ready.InstanceId}'.");
        }

        nextCombatants[actorIndex] = updatedActor.WithNextActionTime(nextActionTime);
        return new CombatResolution(
            new CombatSnapshot(action.Next.Round, action.Next.TimelineTime, nextCombatants),
            action.Events);
    }
}

public sealed record TurnOrderPreviewEntry(
    string CombatantInstanceId,
    BattleSide Side,
    long ActionTime);

public sealed class TurnOrderPreview
{
    public TurnOrderPreview(IReadOnlyList<TurnOrderPreviewEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = Array.AsReadOnly(entries.ToArray());
    }

    public IReadOnlyList<TurnOrderPreviewEntry> Entries { get; }
}

/// <summary>Forecasts initiative without mutating the authoritative combat snapshot.</summary>
public sealed class TurnOrderPreviewService
{
    public TurnOrderPreview Create(CombatSnapshot snapshot, int count = 8)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var simulated = snapshot.Combatants
            .Where(combatant => !combatant.IsDefeated)
            .Select(combatant => new ForecastCombatant(
                combatant.InstanceId,
                combatant.Side,
                combatant.NextActionTime,
                CombatTimeline.EffectiveSpeed(combatant)))
            .ToDictionary(value => value.InstanceId, StringComparer.Ordinal);
        var entries = new List<TurnOrderPreviewEntry>(count);
        for (int index = 0; index < count && simulated.Count > 0; index++)
        {
            ForecastCombatant next = simulated.Values
                .OrderBy(value => value.NextActionTime)
                .ThenByDescending(value => value.Speed)
                .ThenBy(value => value.Side == BattleSide.Party ? 0 : 1)
                .ThenBy(value => value.InstanceId, StringComparer.Ordinal)
                .First();
            entries.Add(new TurnOrderPreviewEntry(
                next.InstanceId,
                next.Side,
                next.NextActionTime));
            simulated[next.InstanceId] = next with
            {
                NextActionTime = checked(
                    next.NextActionTime + CombatTimeline.BaseActionDelay
                    / (next.Speed + CombatTimeline.SpeedBaseline)),
            };
        }

        return new TurnOrderPreview(entries);
    }

    private sealed record ForecastCombatant(
        string InstanceId,
        BattleSide Side,
        long NextActionTime,
        int Speed);
}
