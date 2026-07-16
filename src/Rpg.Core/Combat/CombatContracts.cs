using System.Collections.ObjectModel;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Combat;

/// <summary>
/// Pure rules boundary for applying one validated combat command to immutable battle state.
/// </summary>
/// <remarks>
/// Implementations return a replacement snapshot and typed events. They never mutate the input,
/// touch Godot, update campaign state, or decide how the result is animated.
/// </remarks>
public interface ICombatResolver
{
    CombatResolution Resolve(CombatSnapshot current, CombatCommand command);
}

/// <summary>
/// Pure coordinator for resolving one complete, already collected set of combat commands.
/// </summary>
/// <remarks>
/// Every combatant alive at the beginning of the round must have exactly one command. The
/// coordinator owns deterministic ordering and round boundaries; individual action legality
/// and effects remain owned by <see cref="ICombatResolver"/>.
/// </remarks>
public interface ICombatRoundResolver
{
    CombatResolution ResolveRound(
        CombatSnapshot current,
        IReadOnlyList<CombatCommand> commands);
}

/// <summary>Creates an ordinary combat command for one living enemy combatant.</summary>
/// <remarks>
/// The planner chooses intent only. It does not resolve damage, mutate the snapshot, or receive
/// a special AI-only command type, so enemy and player actions pass through identical rules.
/// </remarks>
public interface IEnemyCommandPlanner
{
    CombatCommand Plan(CombatSnapshot current, string enemyInstanceId);
}

/// <summary>
/// Shared random-number boundary for deterministic core rules.
/// Physical damage currently does not consume randomness; loot resolution does through an
/// injected implementation so the same defeated definitions and scripted rolls stay repeatable.
/// </summary>
public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}

/// <summary>Battle-local progress derived from which sides still have living combatants.</summary>
/// <remarks>
/// Outcome is transient combat state. It is not a content ID, a saved campaign flag, or a
/// reward decision. Keeping the three currently meaningful values closed in code prevents
/// presentation and future application layers from inventing incompatible string statuses.
/// </remarks>
public enum BattleOutcome
{
    InProgress,
    PartyVictory,
    PartyDefeat,
}

/// <summary>
/// Complete immutable state at one point in a transient battle.
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
    /// Initial snapshots start at one. A completed nonterminal round advances this by one.
    /// </summary>
    public int Round { get; }

    /// <summary>Party combatants first, then enemies, each in supplied placement order.</summary>
    public IReadOnlyList<CombatantSnapshot> Combatants { get; }

    /// <summary>
    /// Gets the authoritative battle-local outcome derived from the current immutable HP state.
    /// </summary>
    /// <remarks>
    /// Outcome is deliberately calculated rather than accepted as a constructor argument. A
    /// separately stored value could say "in progress" after the final enemy reached zero HP.
    /// Current single-target rules cannot defeat both sides at once; encountering such a snapshot
    /// indicates malformed runtime state and is rejected instead of choosing an arbitrary winner.
    /// </remarks>
    public BattleOutcome Outcome
    {
        get
        {
            bool partyDefeated = IsSideDefeated(BattleSide.Party);
            bool enemyDefeated = IsSideDefeated(BattleSide.Enemy);
            if (partyDefeated && enemyDefeated)
            {
                throw new InvalidDataException(
                    "A combat snapshot cannot determine an outcome because both sides are "
                    + "defeated.");
            }

            if (enemyDefeated)
            {
                return BattleOutcome.PartyVictory;
            }

            return partyDefeated
                ? BattleOutcome.PartyDefeat
                : BattleOutcome.InProgress;
        }
    }

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

    /// <summary>
    /// Returns true when a side has no living combatants in this transient battle state.
    /// </summary>
    /// <remarks>
    /// This is a battle-lifetime query, not a campaign victory flag. A later battle/application
    /// boundary may translate the terminal state into rewards or persistent progress.
    /// </remarks>
    public bool IsSideDefeated(BattleSide side) => !Combatants.Any(
        combatant => combatant.Side == side && !combatant.IsDefeated);
}

/// <summary>
/// Immutable runtime state for one actor or enemy instance in one battle.
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
        int currentHp,
        IReadOnlyDictionary<string, int>? damageTypePercentModifiers = null,
        int? currentMp = null)
        : this(
            placement,
            statistics,
            abilityIds,
            currentHp,
            partyAbilityAvailability: null,
            damageTypePercentModifiers,
            currentMp)
    {
    }

    public CombatantSnapshot(
        FormationPlacement placement,
        IReadOnlyDictionary<string, int> statistics,
        PartyAbilityAvailability partyAbilityAvailability,
        int currentHp,
        IReadOnlyDictionary<string, int>? damageTypePercentModifiers = null,
        int? currentMp = null)
        : this(
            placement,
            statistics,
            partyAbilityAvailability?.ExecutableAbilityIds
                ?? throw new ArgumentNullException(nameof(partyAbilityAvailability)),
            currentHp,
            partyAbilityAvailability,
            damageTypePercentModifiers,
            currentMp)
    {
    }

    private CombatantSnapshot(
        FormationPlacement placement,
        IReadOnlyDictionary<string, int> statistics,
        IReadOnlyList<string> abilityIds,
        int currentHp,
        PartyAbilityAvailability? partyAbilityAvailability,
        IReadOnlyDictionary<string, int>? damageTypePercentModifiers,
        int? currentMp)
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

        // Runtime state must be able to represent defeat. Initial construction remains stricter:
        // CombatSnapshotFactory still derives a positive starting value from stat.max-hp.
        if (currentHp < 0 || currentHp > maximumHp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentHp),
                currentHp,
                $"Current HP for '{placement.InstanceId}' must be within "
                + $"0..{maximumHp}.");
        }

        int maximumMp = statistics.TryGetValue(CombatStatisticIds.MaxMp, out int authoredMaximumMp)
            ? authoredMaximumMp
            : 0;
        if (maximumMp < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(statistics),
                maximumMp,
                $"Combatant '{placement.InstanceId}' has negative "
                + $"'{CombatStatisticIds.MaxMp}' statistic {maximumMp}.");
        }

        int resolvedCurrentMp = currentMp ?? maximumMp;
        if (resolvedCurrentMp < 0 || resolvedCurrentMp > maximumMp)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentMp),
                resolvedCurrentMp,
                $"Current MP for '{placement.InstanceId}' must be within 0..{maximumMp}.");
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

        var damageModifierCopy = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (damageTypePercentModifiers is not null)
        {
            foreach ((string damageTypeId, int modifier) in damageTypePercentModifiers)
            {
                if (!DamageTypeIds.IsSupported(damageTypeId))
                {
                    throw new ArgumentException(
                        $"Combatant '{placement.InstanceId}' has unsupported damage type "
                        + $"'{damageTypeId}'.",
                        nameof(damageTypePercentModifiers));
                }

                if (modifier < -100)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(damageTypePercentModifiers),
                        modifier,
                        $"Combatant '{placement.InstanceId}' damage modifier for "
                        + $"'{damageTypeId}' cannot be below -100.");
                }

                damageModifierCopy.Add(damageTypeId, modifier);
            }
        }

        Placement = placement;
        Statistics = new ReadOnlyDictionary<string, int>(statisticCopy);
        AbilityIds = Array.AsReadOnly(abilityIds.ToArray());
        DamageTypePercentModifiers = new ReadOnlyDictionary<string, int>(damageModifierCopy);
        PartyAbilityAvailability = partyAbilityAvailability;
        CurrentHp = currentHp;
        CurrentMp = resolvedCurrentMp;
    }

    public FormationPlacement Placement { get; }

    public IReadOnlyDictionary<string, int> Statistics { get; }

    public IReadOnlyList<string> AbilityIds { get; }

    /// <summary>
    /// Signed whole-percent adjustments copied into battle state. Positive values increase
    /// matching damage, negative values reduce it, and omitted types are neutral.
    /// </summary>
    public IReadOnlyDictionary<string, int> DamageTypePercentModifiers { get; }

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

    /// <summary>
    /// Transient MP remaining in this battle. It is initialized from <see cref="MaximumMp"/>
    /// and never appears in campaign state or authored content.
    /// </summary>
    public int CurrentMp { get; }

    public string InstanceId => Placement.InstanceId;

    public string DefinitionId => Placement.DefinitionId;

    public BattleSide Side => Placement.Anchor.Side;

    public int MaximumHp => Statistics[CombatStatisticIds.MaxHp];

    /// <summary>
    /// Resolved maximum MP, or zero when the combatant has no <c>stat.max-mp</c> statistic.
    /// </summary>
    public int MaximumMp => Statistics.TryGetValue(CombatStatisticIds.MaxMp, out int value)
        ? value
        : 0;

    /// <summary>True when this battle-local combatant has no remaining HP.</summary>
    public bool IsDefeated => CurrentHp == 0;

    /// <summary>
    /// Creates a replacement state with different current HP while preserving every other
    /// authored and battle-local property.
    /// </summary>
    /// <remarks>
    /// Keeping this copy operation beside the state type prevents effect resolvers from
    /// accidentally dropping structured party abilities when replacing a damaged combatant.
    /// The constructor still enforces the shared <c>0..MaximumHp</c> runtime invariant.
    /// </remarks>
    public CombatantSnapshot WithCurrentHp(int currentHp) =>
        PartyAbilityAvailability is null
            ? new CombatantSnapshot(
                Placement,
                Statistics,
                AbilityIds,
                currentHp,
                DamageTypePercentModifiers,
                CurrentMp)
            : new CombatantSnapshot(
                Placement,
                Statistics,
                PartyAbilityAvailability,
                currentHp,
                DamageTypePercentModifiers,
                CurrentMp);

    /// <summary>
    /// Creates a replacement state with different current MP while preserving every other
    /// battle-local property.
    /// </summary>
    public CombatantSnapshot WithCurrentMp(int currentMp) =>
        PartyAbilityAvailability is null
            ? new CombatantSnapshot(
                Placement,
                Statistics,
                AbilityIds,
                CurrentHp,
                DamageTypePercentModifiers,
                currentMp)
            : new CombatantSnapshot(
                Placement,
                Statistics,
                PartyAbilityAvailability,
                CurrentHp,
                DamageTypePercentModifiers,
                currentMp);
}

/// <summary>
/// Player- or AI-authored intent to use one ability against explicit battle-local targets.
/// </summary>
/// <remarks>
/// The target collection is defensively copied. Its shape and legality are deliberately checked
/// by <see cref="ICombatResolver"/>, because those rules depend on the selected ability and the
/// current snapshot rather than on command syntax alone.
/// </remarks>
public sealed record CombatCommand
{
    public CombatCommand(
        string actingCombatantId,
        string abilityId,
        IReadOnlyList<string> targetCombatantIds)
    {
        ArgumentNullException.ThrowIfNull(targetCombatantIds);

        ActingCombatantId = actingCombatantId;
        AbilityId = abilityId;
        TargetCombatantIds = Array.AsReadOnly(targetCombatantIds.ToArray());
    }

    public string ActingCombatantId { get; }

    public string AbilityId { get; }

    public IReadOnlyList<string> TargetCombatantIds { get; }
}

/// <summary>
/// Immutable result of one accepted combat command or one complete combat round.
/// </summary>
public sealed record CombatResolution
{
    public CombatResolution(CombatSnapshot next, IReadOnlyList<CombatEvent> events)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(events);
        if (events.Any(combatEvent => combatEvent is null))
        {
            throw new ArgumentException(
                "A combat resolution cannot contain a null event.",
                nameof(events));
        }

        Next = next;
        Events = Array.AsReadOnly(events.ToArray());
    }

    public CombatSnapshot Next { get; }

    public IReadOnlyList<CombatEvent> Events { get; }
}

/// <summary>
/// Presentation-neutral fact emitted by trusted combat rules.
/// </summary>
/// <remarks>
/// Godot may translate concrete events into animation, sound, or UI updates, but presentation
/// must not recalculate their gameplay meaning.
/// </remarks>
public abstract record CombatEvent;
