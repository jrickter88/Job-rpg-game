using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

/// <summary>
/// Proves complete-round coordination independently from Godot, animation, and future menus.
/// </summary>
public sealed class CombatRoundResolverTests
{
    [Fact]
    public void ResolveRound_RepeatedCheckedInCommandsReachDeterministicTerminalState()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        var rounds = RoundResolver(battle);
        var enemyPlanner = new EnemyCommandPlanner(battle.Content);
        CombatSnapshot current = battle.Snapshot;

        for (int resolvedRoundCount = 0;
            resolvedRoundCount < 10 && !current.IsSideDefeated(BattleSide.Enemy);
            resolvedRoundCount++)
        {
            CombatantSnapshot partyTarget = current.Combatants
                .Where(combatant =>
                    combatant.Side == BattleSide.Enemy && !combatant.IsDefeated)
                .OrderBy(combatant => combatant.InstanceId, StringComparer.Ordinal)
                .First();
            var commands = new List<CombatCommand>
            {
                Attack("party-0", partyTarget.InstanceId),
            };
            commands.AddRange(current.Combatants
                .Where(combatant =>
                    combatant.Side == BattleSide.Enemy && !combatant.IsDefeated)
                .Select(combatant => enemyPlanner.Plan(current, combatant.InstanceId)));

            current = rounds.ResolveRound(current, commands).Next;
        }

        Assert.True(current.IsSideDefeated(BattleSide.Enemy));
        Assert.False(current.IsSideDefeated(BattleSide.Party));
        Assert.Equal(BattleOutcome.PartyVictory, current.Outcome);
        Assert.Equal(4, current.Round);
        Assert.Equal(80, current.GetRequiredCombatant("party-0").CurrentHp);
    }

    [Fact]
    public void ResolveRound_IgnoresInputOrderAndActsBySpeedThenInstanceId()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        var enemyPlanner = new EnemyCommandPlanner(battle.Content);
        IReadOnlyList<CombatCommand> deliberatelyReversed = new CombatCommand[]
        {
            Attack("party-0", "enemy-0"),
            enemyPlanner.Plan(battle.Snapshot, "enemy-0"),
            enemyPlanner.Plan(battle.Snapshot, "enemy-1"),
        }.Reverse().ToArray();

        CombatResolution resolution = RoundResolver(battle).ResolveRound(
            battle.Snapshot,
            deliberatelyReversed);

        Assert.Equal(
            ["party-0", "enemy-0", "enemy-1"],
            DamageActors(resolution.Events));
        Assert.Equal(2, resolution.Next.Round);
        Assert.Equal(11, resolution.Next.GetRequiredCombatant("enemy-0").CurrentHp);
        Assert.Equal(88, resolution.Next.GetRequiredCombatant("party-0").CurrentHp);

        // The round threads immutable replacements internally; the caller's starting snapshot
        // remains suitable for a replay or deterministic comparison.
        Assert.Equal(1, battle.Snapshot.Round);
        Assert.Equal(22, battle.Snapshot.GetRequiredCombatant("enemy-0").CurrentHp);
        Assert.Equal(96, battle.Snapshot.GetRequiredCombatant("party-0").CurrentHp);
    }

    [Fact]
    public void ResolveRound_EqualSpeedUsesOrdinalInstanceIdTieBreak()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot equalSpeed = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => WithStatistic(combatant, CombatStatisticIds.Speed, 2));

        CombatResolution resolution = RoundResolver(battle).ResolveRound(
            equalSpeed,
            CompleteCommands());

        Assert.Equal(
            ["enemy-0", "enemy-1", "party-0"],
            DamageActors(resolution.Events));
    }

    [Fact]
    public void ResolveRound_ActorDefeatedEarlierInRoundLosesPendingAction()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot vulnerableEnemy = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => combatant.WithCurrentHp(5));

        CombatResolution resolution = RoundResolver(battle).ResolveRound(
            vulnerableEnemy,
            CompleteCommands());

        Assert.Equal(
            ["party-0", "enemy-1"],
            DamageActors(resolution.Events));
        Assert.Contains(
            resolution.Events,
            combatEvent => combatEvent is CombatantDefeated { CombatantId: "enemy-0" });
        Assert.Equal(2, resolution.Next.Round);
    }

    [Fact]
    public void ResolveRound_StopsImmediatelyWhenEnemySideIsDefeated()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot lastEnemy = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => combatant.WithCurrentHp(0));
        lastEnemy = ReplaceCombatant(
            lastEnemy,
            "enemy-1",
            combatant => combatant.WithCurrentHp(5));
        CombatCommand[] commands =
        [
            Attack("party-0", "enemy-1"),
            Tackle("enemy-1", "party-0"),
        ];

        CombatResolution resolution = RoundResolver(battle).ResolveRound(lastEnemy, commands);

        DamageApplied damage = Assert.IsType<DamageApplied>(resolution.Events[0]);
        Assert.Equal("party-0", damage.ActingCombatantId);
        Assert.IsType<CombatantDefeated>(resolution.Events[1]);
        BattleEnded ended = Assert.IsType<BattleEnded>(resolution.Events[2]);
        Assert.Equal(BattleOutcome.PartyVictory, ended.Outcome);
        Assert.Equal(3, resolution.Events.Count);
        Assert.True(resolution.Next.IsSideDefeated(BattleSide.Enemy));
        Assert.False(resolution.Next.IsSideDefeated(BattleSide.Party));
        Assert.Equal(BattleOutcome.PartyVictory, resolution.Next.Outcome);
        Assert.Equal(1, resolution.Next.Round);
        Assert.Equal(96, resolution.Next.GetRequiredCombatant("party-0").CurrentHp);
    }

    [Fact]
    public void ResolveRound_StopsImmediatelyWhenPartySideIsDefeated()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot vulnerableParty = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => WithStatistic(combatant.WithCurrentHp(4), CombatStatisticIds.Speed, 1));

        CombatResolution resolution = RoundResolver(battle).ResolveRound(
            vulnerableParty,
            CompleteCommands());

        DamageApplied damage = Assert.IsType<DamageApplied>(resolution.Events[0]);
        Assert.Equal("enemy-0", damage.ActingCombatantId);
        Assert.IsType<CombatantDefeated>(resolution.Events[1]);
        BattleEnded ended = Assert.IsType<BattleEnded>(resolution.Events[2]);
        Assert.Equal(BattleOutcome.PartyDefeat, ended.Outcome);
        Assert.Equal(3, resolution.Events.Count);
        Assert.True(resolution.Next.IsSideDefeated(BattleSide.Party));
        Assert.False(resolution.Next.IsSideDefeated(BattleSide.Enemy));
        Assert.Equal(BattleOutcome.PartyDefeat, resolution.Next.Outcome);
        Assert.Equal(1, resolution.Next.Round);
        Assert.Equal(22, resolution.Next.GetRequiredCombatant("enemy-0").CurrentHp);
    }

    [Fact]
    public void ResolveRound_MissingLivingCombatantCommandIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();

        CombatRoundValidationException exception = Assert.Throws<
            CombatRoundValidationException>(() => RoundResolver(battle).ResolveRound(
                battle.Snapshot,
                [Attack("party-0", "enemy-0"), Tackle("enemy-0", "party-0")]));

        Assert.Equal(CombatRoundProblemCodes.CommandMissing, exception.ProblemCode);
        Assert.Contains("enemy-1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveRound_DuplicateActorCommandIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatCommand[] duplicate =
        [
            .. CompleteCommands(),
            Attack("party-0", "enemy-1"),
        ];

        CombatRoundValidationException exception = Assert.Throws<
            CombatRoundValidationException>(() => RoundResolver(battle).ResolveRound(
                battle.Snapshot,
                duplicate));

        Assert.Equal(CombatRoundProblemCodes.CommandDuplicate, exception.ProblemCode);
    }

    [Fact]
    public void ResolveRound_CommandFromInitiallyDefeatedActorIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot defeatedEnemy = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => combatant.WithCurrentHp(0));

        CombatRoundValidationException exception = Assert.Throws<
            CombatRoundValidationException>(() => RoundResolver(battle).ResolveRound(
                defeatedEnemy,
                CompleteCommands()));

        Assert.Equal(CombatRoundProblemCodes.ActorDefeated, exception.ProblemCode);
    }

    [Fact]
    public void ResolveRound_CommandFromUnknownActorIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatCommand[] commands =
        [
            .. CompleteCommands(),
            Attack("party-missing", "enemy-0"),
        ];

        CombatRoundValidationException exception = Assert.Throws<
            CombatRoundValidationException>(() => RoundResolver(battle).ResolveRound(
                battle.Snapshot,
                commands));

        Assert.Equal(CombatRoundProblemCodes.ActorMissing, exception.ProblemCode);
    }

    [Fact]
    public void ResolveRound_AlreadyTerminalSnapshotIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot terminal = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => combatant.WithCurrentHp(0));
        terminal = ReplaceCombatant(
            terminal,
            "enemy-1",
            combatant => combatant.WithCurrentHp(0));

        CombatRoundValidationException exception = Assert.Throws<
            CombatRoundValidationException>(() => RoundResolver(battle).ResolveRound(
                terminal,
                []));

        Assert.Equal(CombatRoundProblemCodes.BattleAlreadyEnded, exception.ProblemCode);
    }

    [Fact]
    public void Plan_CheckedInSlimeUsesAuthoredTackleAgainstJames()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();

        CombatCommand command = new EnemyCommandPlanner(battle.Content).Plan(
            battle.Snapshot,
            "enemy-0");

        Assert.Equal("enemy-0", command.ActingCombatantId);
        Assert.Equal(CombatTestFixture.TackleId, command.AbilityId);
        Assert.Equal(["party-0"], command.TargetCombatantIds);
    }

    [Fact]
    public void Plan_TargetsLowestCurrentHpThenOrdinalInstanceId()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatantSnapshot originalParty = battle.Snapshot.GetRequiredCombatant("party-0");
        CombatantSnapshot partyZero = originalParty.WithCurrentHp(5);
        var partyOne = new CombatantSnapshot(
            new FormationPlacement(
                "party-1",
                "actor.hero.test-companion",
                new FormationCell(BattleSide.Party, 1, 0),
                FormationFootprint.SingleCell),
            originalParty.Statistics,
            originalParty.AbilityIds,
            5);
        var twoPartySnapshot = new CombatSnapshot(
            battle.Snapshot.Round,
            [
                partyOne,
                partyZero,
                battle.Snapshot.GetRequiredCombatant("enemy-0"),
                battle.Snapshot.GetRequiredCombatant("enemy-1"),
            ]);

        CombatCommand tied = new EnemyCommandPlanner(battle.Content).Plan(
            twoPartySnapshot,
            "enemy-0");
        CombatSnapshot lowerPartyOne = ReplaceCombatant(
            twoPartySnapshot,
            "party-1",
            combatant => combatant.WithCurrentHp(4));
        CombatCommand lower = new EnemyCommandPlanner(battle.Content).Plan(
            lowerPartyOne,
            "enemy-0");

        Assert.Equal(["party-0"], tied.TargetCombatantIds);
        Assert.Equal(["party-1"], lower.TargetCombatantIds);
    }

    [Fact]
    public void Plan_SkipsUnsupportedAbilityAndUsesFirstExecutableAbility()
    {
        const string unsupportedId = "ability.test.costly-strike";
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        AbilityDefinition unsupported = CostlyPhysicalAbility(unsupportedId);
        AbilityDefinition tackle = battle.Content.GetRequired<AbilityDefinition>(
            CombatTestFixture.TackleId);
        CombatSnapshot mixedAbilities = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => WithAbilities(
                combatant,
                [unsupportedId, CombatTestFixture.TackleId]));

        CombatCommand command = new EnemyCommandPlanner(
            new TestCatalog(unsupported, tackle)).Plan(mixedAbilities, "enemy-0");

        Assert.Equal(CombatTestFixture.TackleId, command.AbilityId);
    }

    [Fact]
    public void Plan_NoExecutableAbilityIsRejected()
    {
        const string unsupportedId = "ability.test.costly-strike";
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        AbilityDefinition unsupported = CostlyPhysicalAbility(unsupportedId);
        CombatSnapshot unsupportedOnly = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => WithAbilities(combatant, [unsupportedId]));

        EnemyCommandPlanningException exception = Assert.Throws<
            EnemyCommandPlanningException>(() => new EnemyCommandPlanner(
                new TestCatalog(unsupported)).Plan(unsupportedOnly, "enemy-0"));

        Assert.Equal(
            EnemyCommandPlanningProblemCodes.AbilityUnavailable,
            exception.ProblemCode);
    }

    [Fact]
    public void Plan_AffordableMpAbilityUsesTheSamePaymentPathAsPartyCommands()
    {
        const string abilityId = "ability.test.mana-tackle";
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        AbilityDefinition manaTackle = CostlyPhysicalAbility(abilityId) with
        {
            CostAmount = 2,
        };
        CombatSnapshot prepared = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => WithAbilities(
                WithStatistic(combatant, CombatStatisticIds.MaxMp, 3),
                [abilityId]).WithCurrentMp(3));
        var planner = new EnemyCommandPlanner(new TestCatalog(manaTackle));

        CombatCommand command = planner.Plan(prepared, "enemy-0");
        CombatResolution resolution = new CombatResolver(new TestCatalog(manaTackle)).Resolve(
            prepared,
            command);

        Assert.Equal(abilityId, command.AbilityId);
        Assert.Equal(1, resolution.Next.GetRequiredCombatant("enemy-0").CurrentMp);
        Assert.IsType<ResourceSpent>(resolution.Events[0]);
    }

    [Fact]
    public void Plan_NoLivingPartyTargetIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot defeatedParty = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => combatant.WithCurrentHp(0));

        EnemyCommandPlanningException exception = Assert.Throws<
            EnemyCommandPlanningException>(() => new EnemyCommandPlanner(battle.Content).Plan(
                defeatedParty,
                "enemy-0"));

        Assert.Equal(
            EnemyCommandPlanningProblemCodes.TargetUnavailable,
            exception.ProblemCode);
    }

    [Fact]
    public void Plan_PartyOrDefeatedActorIsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        var planner = new EnemyCommandPlanner(battle.Content);
        CombatSnapshot defeatedEnemy = ReplaceCombatant(
            battle.Snapshot,
            "enemy-0",
            combatant => combatant.WithCurrentHp(0));

        EnemyCommandPlanningException party = Assert.Throws<
            EnemyCommandPlanningException>(() => planner.Plan(battle.Snapshot, "party-0"));
        EnemyCommandPlanningException defeated = Assert.Throws<
            EnemyCommandPlanningException>(() => planner.Plan(defeatedEnemy, "enemy-0"));

        Assert.Equal(EnemyCommandPlanningProblemCodes.ActorNotEnemy, party.ProblemCode);
        Assert.Equal(EnemyCommandPlanningProblemCodes.ActorDefeated, defeated.ProblemCode);
    }

    private static CombatRoundResolver RoundResolver(FixedBattle battle) =>
        new(new CombatResolver(battle.Content));

    private static IReadOnlyList<CombatCommand> CompleteCommands() =>
    [
        Attack("party-0", "enemy-0"),
        Tackle("enemy-0", "party-0"),
        Tackle("enemy-1", "party-0"),
    ];

    private static CombatCommand Attack(string actorId, string targetId) => new(
        actorId,
        CombatTestFixture.AttackId,
        [targetId]);

    private static CombatCommand Tackle(string actorId, string targetId) => new(
        actorId,
        CombatTestFixture.TackleId,
        [targetId]);

    private static AbilityDefinition CostlyPhysicalAbility(string id) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
        TargetingId = AbilityTargetingIds.SingleEnemy,
        CostStatisticId = "stat.max-mp",
        CostAmount = 1,
        RulesetId = AbilityRulesetIds.PhysicalDamage,
        NumericParameters = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [AbilityNumericParameterIds.Power] = 1m,
        },
    };

    private static string[] DamageActors(IReadOnlyList<CombatEvent> events) => events
        .OfType<DamageApplied>()
        .Select(damage => damage.ActingCombatantId)
        .ToArray();

    private static CombatSnapshot ReplaceCombatant(
        CombatSnapshot source,
        string instanceId,
        Func<CombatantSnapshot, CombatantSnapshot> replace)
    {
        CombatantSnapshot[] combatants = source.Combatants.ToArray();
        int index = Array.FindIndex(
            combatants,
            combatant => string.Equals(
                combatant.InstanceId,
                instanceId,
                StringComparison.Ordinal));
        Assert.True(index >= 0, $"Fixture combatant '{instanceId}' was not found.");
        combatants[index] = replace(combatants[index]);
        return new CombatSnapshot(source.Round, combatants);
    }

    private static CombatantSnapshot WithStatistic(
        CombatantSnapshot source,
        string statisticId,
        int value)
    {
        var statistics = new Dictionary<string, int>(source.Statistics, StringComparer.Ordinal)
        {
            [statisticId] = value,
        };
        return source.PartyAbilityAvailability is null
            ? new CombatantSnapshot(
                source.Placement,
                statistics,
                source.AbilityIds,
                source.CurrentHp,
                source.DamageTypePercentModifiers,
                source.CurrentMp)
            : new CombatantSnapshot(
                source.Placement,
                statistics,
                source.PartyAbilityAvailability,
                source.CurrentHp,
                source.DamageTypePercentModifiers,
                source.CurrentMp);
    }

    private static CombatantSnapshot WithAbilities(
        CombatantSnapshot source,
        IReadOnlyList<string> abilityIds) => new(
        source.Placement,
        source.Statistics,
        abilityIds,
        source.CurrentHp,
        source.DamageTypePercentModifiers,
        source.CurrentMp);
}
