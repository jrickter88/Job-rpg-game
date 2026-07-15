using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Combat;

/// <summary>
/// Builds the initial transient combat snapshot from campaign state, encounter content, and
/// already constructed formation placements.
/// </summary>
/// <remarks>
/// This pure-core factory reads only explicit arguments and <see cref="IContentCatalog"/>.
/// It never reads files, touches Godot, mutates campaign state, or stores the result in a save.
/// </remarks>
public sealed class CombatSnapshotFactory
{
    private readonly IContentCatalog _content;
    private readonly CombatStatisticResolver _statisticResolver;
    private readonly AbilityAvailabilityResolver _abilityResolver;

    public CombatSnapshotFactory(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _statisticResolver = new CombatStatisticResolver(content);
        _abilityResolver = new AbilityAvailabilityResolver(content);
    }

    /// <summary>
    /// Creates round one with party placements first and enemy placements second, preserving
    /// each supplied list's order and deterministic battle-local instance IDs.
    /// </summary>
    public CombatSnapshot Create(
        GameState campaign,
        EncounterDefinition encounter,
        IReadOnlyList<FormationPlacement> enemyPlacements,
        IReadOnlyList<FormationPlacement> partyPlacements)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(enemyPlacements);
        ArgumentNullException.ThrowIfNull(partyPlacements);

        IReadOnlyList<string> activePartyIds = campaign.ActivePartyActorIds
            ?? throw new InvalidDataException("Campaign active-party IDs cannot be null.");
        IReadOnlyDictionary<string, ActorProgressState> actorProgress = campaign.ActorProgress
            ?? throw new InvalidDataException("Campaign actor progress cannot be null.");
        IReadOnlyList<EncounterEnemyDefinition> encounterEnemies = encounter.EnemyGroup
            ?? throw new InvalidDataException(
                $"Encounter '{encounter.Id}' has a null enemy group.");

        PartyRules.ValidateMemberCount(activePartyIds.Count, nameof(campaign));
        if (partyPlacements.Count != activePartyIds.Count)
        {
            throw new InvalidDataException(
                $"Party placement count {partyPlacements.Count} does not match active-party "
                + $"count {activePartyIds.Count}.");
        }

        if (enemyPlacements.Count != encounterEnemies.Count)
        {
            throw new InvalidDataException(
                $"Enemy placement count {enemyPlacements.Count} does not match encounter "
                + $"'{encounter.Id}' enemy count {encounterEnemies.Count}.");
        }

        ValidateUniqueInstanceIds(partyPlacements, enemyPlacements);

        var combatants = new List<CombatantSnapshot>(
            partyPlacements.Count + enemyPlacements.Count);
        var representedPartyActorIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (FormationPlacement placement in partyPlacements)
        {
            ValidateSide(placement, BattleSide.Party, "Party");
            _content.GetRequired<ActorDefinition>(placement.DefinitionId);

            if (!activePartyIds.Contains(placement.DefinitionId, StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Party placement '{placement.InstanceId}' references actor "
                    + $"'{placement.DefinitionId}', which is absent from the active party.");
            }

            if (!representedPartyActorIds.Add(placement.DefinitionId))
            {
                throw new InvalidDataException(
                    $"Party actor '{placement.DefinitionId}' has more than one formation "
                    + "placement.");
            }

            ActorProgressState progress = FindExactlyOneProgress(
                actorProgress,
                placement.DefinitionId);
            IReadOnlyDictionary<string, int> statistics =
                _statisticResolver.ResolvePartyActor(progress);
            int maximumHp = RequirePositiveMaximumHp(placement.InstanceId, statistics);
            PartyAbilityAvailability abilityAvailability =
                _abilityResolver.ResolvePartyActor(progress);

            combatants.Add(new CombatantSnapshot(
                placement,
                statistics,
                abilityAvailability,
                maximumHp));
        }

        foreach (string activeActorId in activePartyIds)
        {
            if (!representedPartyActorIds.Contains(activeActorId))
            {
                throw new InvalidDataException(
                    $"Active-party actor '{activeActorId}' has no party formation placement.");
            }
        }

        for (int index = 0; index < enemyPlacements.Count; index++)
        {
            FormationPlacement placement = enemyPlacements[index];
            ValidateSide(placement, BattleSide.Enemy, "Enemy");

            // Resolve the category before comparing encounter identity. A placement that points
            // at an actor should report the wrong content category, not merely an ID mismatch.
            EnemyDefinition enemy = _content.GetRequired<EnemyDefinition>(
                placement.DefinitionId);
            EncounterEnemyDefinition encounterEnemy = encounterEnemies[index]
                ?? throw new InvalidDataException(
                    $"Encounter '{encounter.Id}' contains a null enemy entry at index {index}.");
            if (!string.Equals(
                    placement.DefinitionId,
                    encounterEnemy.EnemyId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Enemy placement '{placement.InstanceId}' references "
                    + $"'{placement.DefinitionId}', but encounter index {index} references "
                    + $"'{encounterEnemy.EnemyId}'.");
            }

            IReadOnlyList<string> enemyAbilityIds = enemy.AbilityIds
                ?? throw new InvalidDataException(
                    $"Enemy '{enemy.Id}' has a null ability list.");
            ValidateAbilityReferences(enemy.Id, enemyAbilityIds);

            IReadOnlyDictionary<string, int> statistics =
                _statisticResolver.ResolveEnemy(enemy.Id);
            int maximumHp = RequirePositiveMaximumHp(placement.InstanceId, statistics);

            combatants.Add(new CombatantSnapshot(
                placement,
                statistics,
                enemyAbilityIds,
                maximumHp));
        }

        ValidateFormationRules(partyPlacements, enemyPlacements);
        return new CombatSnapshot(1, combatants);
    }

    private static void ValidateSide(
        FormationPlacement placement,
        BattleSide expectedSide,
        string placementKind)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (placement.Anchor.Side != expectedSide)
        {
            throw new InvalidDataException(
                $"{placementKind} placement '{placement.InstanceId}' is on "
                + $"'{placement.Anchor.Side}', but must be on '{expectedSide}'.");
        }
    }

    private static ActorProgressState FindExactlyOneProgress(
        IReadOnlyDictionary<string, ActorProgressState> actorProgress,
        string actorId)
    {
        var matches = new List<ActorProgressState>();
        foreach ((string key, ActorProgressState? progress) in actorProgress)
        {
            if (progress is null)
            {
                throw new InvalidDataException(
                    $"Campaign actor-progress entry '{key}' cannot be null.");
            }

            if (string.Equals(progress.ActorId, actorId, StringComparison.Ordinal))
            {
                matches.Add(progress);
            }
        }

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidDataException(
                $"Active-party actor '{actorId}' has no matching progress state."),
            _ => throw new InvalidDataException(
                $"Active-party actor '{actorId}' has {matches.Count} progress records; "
                + "exactly one is required."),
        };
    }

    private void ValidateAbilityReferences(
        string sourceDefinitionId,
        IReadOnlyList<string> abilityIds)
    {
        foreach (string abilityId in abilityIds)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                throw new InvalidDataException(
                    $"Ability source '{sourceDefinitionId}' contains a blank ability ID.");
            }

            _content.GetRequired<AbilityDefinition>(abilityId);
        }
    }

    private static int RequirePositiveMaximumHp(
        string instanceId,
        IReadOnlyDictionary<string, int> statistics)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        if (!statistics.TryGetValue(CombatStatisticIds.MaxHp, out int maximumHp))
        {
            throw new InvalidDataException(
                $"Combatant '{instanceId}' is missing required statistic "
                + $"'{CombatStatisticIds.MaxHp}'.");
        }

        if (maximumHp <= 0)
        {
            throw new InvalidDataException(
                $"Combatant '{instanceId}' resolved '{CombatStatisticIds.MaxHp}' to "
                + $"{maximumHp}; maximum HP must be positive.");
        }

        return maximumHp;
    }

    private static void ValidateUniqueInstanceIds(
        IReadOnlyList<FormationPlacement> partyPlacements,
        IReadOnlyList<FormationPlacement> enemyPlacements)
    {
        var instanceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (FormationPlacement? placement in partyPlacements.Concat(enemyPlacements))
        {
            ArgumentNullException.ThrowIfNull(placement);
            if (string.IsNullOrWhiteSpace(placement.InstanceId))
            {
                throw new InvalidDataException(
                    "A formation placement has a blank battle-local instance ID.");
            }

            if (!instanceIds.Add(placement.InstanceId))
            {
                throw new InvalidDataException(
                    $"Battle-local instance ID '{placement.InstanceId}' is duplicated.");
            }
        }
    }

    private static void ValidateFormationRules(
        IReadOnlyList<FormationPlacement> partyPlacements,
        IReadOnlyList<FormationPlacement> enemyPlacements)
    {
        IReadOnlyList<FormationProblem> problems = BattleFormationRules.ValidatePlacements(
            partyPlacements.Concat(enemyPlacements));
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                "Initial combat placements are invalid: "
                + string.Join(
                    "; ",
                    problems.Select(problem => $"{problem.InstanceId} {problem.Kind}")));
        }
    }
}
