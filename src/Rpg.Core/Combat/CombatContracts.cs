using System.Collections.ObjectModel;
using RpgGame.Core.Combat.Formation;

namespace RpgGame.Core.Combat;

/// <summary>
/// Pure rules boundary reserved for later combat-command resolution.
/// </summary>
/// <remarks>
/// Milestone 3.0 constructs only the initial snapshot. No implementation of this interface,
/// command validation, damage, targeting, or turn behavior exists yet.
/// </remarks>
public interface ICombatResolver
{
    CombatResolution Resolve(CombatSnapshot current, CombatCommand command);
}

/// <summary>
/// Random-number boundary retained for future deterministic combat rules.
/// Milestone 3.0 does not consume randomness.
/// </summary>
public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>
/// Complete immutable state at the beginning of a transient battle.
/// </summary>
/// <remarks>
/// This state belongs to one encounter, not to <c>GameState</c> or a save file. Combatants
/// remain in explicit supplied order so later rules never depend on dictionary enumeration.
/// </remarks>
public sealed record CombatSnapshot
{
    public CombatSnapshot(int round, IReadOnlyList<CombatantSnapshot> combatants)
    {
        if (round < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(round),
                round,
                "A combat round number must be at least 1.");
        }

        ArgumentNullException.ThrowIfNull(combatants);
        if (combatants.Any(combatant => combatant is null))
        {
            throw new ArgumentException(
                "A combat snapshot cannot contain a null combatant.",
                nameof(combatants));
        }

        Round = round;

        // Copy before wrapping. A ReadOnlyCollection over the caller's original List would
        // still change if that caller later edited the list.
        Combatants = Array.AsReadOnly(combatants.ToArray());
    }

    /// <summary>
    /// Initial snapshots start at one. Turn advancement remains deliberately unimplemented.
    /// </summary>
    public int Round { get; }

    /// <summary>Party combatants first, then enemies, each in supplied placement order.</summary>
    public IReadOnlyList<CombatantSnapshot> Combatants { get; }

    /// <summary>Finds one combatant by deterministic battle-local identity.</summary>
    public CombatantSnapshot GetRequiredCombatant(string instanceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceId);
        return Combatants.FirstOrDefault(combatant => string.Equals(
                combatant.InstanceId,
                instanceId,
                StringComparison.Ordinal))
            ?? throw new KeyNotFoundException(
                $"Combatant instance '{instanceId}' does not exist in this battle snapshot.");
    }
}

/// <summary>
/// Immutable initial state for one actor or enemy instance in one battle.
/// </summary>
/// <remarks>
/// <see cref="Placement"/> remains authoritative for identity, side, anchor, and footprint.
/// Statistics and abilities are copied into independently owned read-only collections.
/// Current HP is transient and distinct from the immutable maximum-HP statistic.
/// </remarks>
public sealed record CombatantSnapshot
{
    public CombatantSnapshot(
        FormationPlacement placement,
        IReadOnlyDictionary<string, int> statistics,
        IReadOnlyList<string> abilityIds,
        int currentHp)
        : this(placement, statistics, abilityIds, currentHp, partyAbilityAvailability: null)
    {
    }

    public CombatantSnapshot(
        FormationPlacement placement,
        IReadOnlyDictionary<string, int> statistics,
        PartyAbilityAvailability partyAbilityAvailability,
        int currentHp)
        : this(
            placement,
            statistics,
            partyAbilityAvailability?.ExecutableAbilityIds
                ?? throw new ArgumentNullException(nameof(partyAbilityAvailability)),
            currentHp,
            partyAbilityAvailability)
    {
    }

    private CombatantSnapshot(
        FormationPlacement placement,
        IReadOnlyDictionary<string, int> statistics,
        IReadOnlyList<string> abilityIds,
        int currentHp,
        PartyAbilityAvailability? partyAbilityAvailability)
    {
        ArgumentNullException.ThrowIfNull(placement);
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(abilityIds);

        if (string.IsNullOrWhiteSpace(placement.InstanceId))
        {
            throw new ArgumentException(
                "A combatant placement must have a nonblank instance ID.",
                nameof(placement));
        }

        if (string.IsNullOrWhiteSpace(placement.DefinitionId))
        {
            throw new ArgumentException(
                "A combatant placement must have a nonblank definition ID.",
                nameof(placement));
        }

        if (!statistics.TryGetValue(CombatStatisticIds.MaxHp, out int maximumHp))
        {
            throw new ArgumentException(
                $"Combatant '{placement.InstanceId}' is missing required statistic "
                + $"'{CombatStatisticIds.MaxHp}'.",
                nameof(statistics));
        }

        if (maximumHp <= 0)
        {
            throw new ArgumentException(
                $"Combatant '{placement.InstanceId}' must have a positive "
                + $"'{CombatStatisticIds.MaxHp}' statistic, but resolved {maximumHp}.",
                nameof(statistics));
        }

        if (currentHp <= 0 || currentHp > maximumHp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentHp),
                currentHp,
                $"Initial current HP for '{placement.InstanceId}' must be within "
                + $"1..{maximumHp}.");
        }

        if (abilityIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                $"Combatant '{placement.InstanceId}' ability IDs cannot be blank.",
                nameof(abilityIds));
        }

        var statisticCopy = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach ((string statisticId, int value) in statistics)
        {
            statisticCopy.Add(statisticId, value);
        }

        Placement = placement;
        Statistics = new ReadOnlyDictionary<string, int>(statisticCopy);
        AbilityIds = Array.AsReadOnly(abilityIds.ToArray());
        PartyAbilityAvailability = partyAbilityAvailability;
        CurrentHp = currentHp;
    }

    public FormationPlacement Placement { get; }

    public IReadOnlyDictionary<string, int> Statistics { get; }

    public IReadOnlyList<string> AbilityIds { get; }

    /// <summary>
    /// Structured party command projection for future menus. This is null for enemies because
    /// enemy AI continues to consume authored flat ability IDs in this milestone.
    /// </summary>
    public PartyAbilityAvailability? PartyAbilityAvailability { get; }

    /// <summary>Party direct Skills, or an empty view for enemies.</summary>
    public IReadOnlyList<string> DirectSkillIds =>
        PartyAbilityAvailability?.DirectSkillIds ?? Array.Empty<string>();

    /// <summary>Party magic containers, or an empty view for enemies.</summary>
    public IReadOnlyList<MagicDisciplineAvailability> MagicDisciplines =>
        PartyAbilityAvailability?.MagicDisciplines ?? Array.Empty<MagicDisciplineAvailability>();

    public int CurrentHp { get; }

    public string InstanceId => Placement.InstanceId;

    public string DefinitionId => Placement.DefinitionId;

    public BattleSide Side => Placement.Anchor.Side;

    public int MaximumHp => Statistics[CombatStatisticIds.MaxHp];
}

/// <summary>
/// Existing intent contract reserved for later milestones. Milestone 3.0 creates no commands.
/// </summary>
public sealed record CombatCommand(
    string ActingCombatantId,
    string AbilityId,
    IReadOnlyList<string> TargetCombatantIds);

/// <summary>
/// Existing result contract reserved for later milestones. Milestone 3.0 resolves no command.
/// </summary>
public sealed record CombatResolution(
    CombatSnapshot Next,
    IReadOnlyList<CombatEvent> Events);

/// <summary>
/// Presentation adapters will eventually translate concrete domain events into visuals.
/// Concrete event types remain deferred until command resolution is implemented.
/// </summary>
public abstract record CombatEvent;
