using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class CombatSnapshotFactoryTests
{
    [Fact]
    public void Create_FixedEncounter_HasPartyThenBothEnemiesInDeterministicOrder()
    {
        CombatSnapshot snapshot = CombatTestFixture.CreateFixedBattle().Snapshot;

        Assert.Equal(1, snapshot.Round);
        Assert.Equal(
            ["party-0", "enemy-0", "enemy-1"],
            snapshot.Combatants.Select(combatant => combatant.InstanceId));
        Assert.Equal(
            [
                CombatTestFixture.JamesId,
                CombatTestFixture.GreenSlimeId,
                CombatTestFixture.GreenSlimeId,
            ],
            snapshot.Combatants.Select(combatant => combatant.DefinitionId));
    }

    [Fact]
    public void Create_FixedEncounter_PreservesFormationSideAnchorAndFootprint()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();

        Assert.Equal(
            battle.PartyPlacements[0],
            battle.Snapshot.GetRequiredCombatant("party-0").Placement);
        Assert.Equal(
            battle.EnemyPlacements[0],
            battle.Snapshot.GetRequiredCombatant("enemy-0").Placement);
        Assert.Equal(
            battle.EnemyPlacements[1],
            battle.Snapshot.GetRequiredCombatant("enemy-1").Placement);

        Assert.Equal(BattleSide.Party,
            battle.Snapshot.GetRequiredCombatant("party-0").Side);
        Assert.Equal(new FormationCell(BattleSide.Enemy, 1, 0),
            battle.Snapshot.GetRequiredCombatant("enemy-0").Placement.Anchor);
        Assert.Equal(new FormationCell(BattleSide.Enemy, 2, 0),
            battle.Snapshot.GetRequiredCombatant("enemy-1").Placement.Anchor);
        Assert.All(
            battle.Snapshot.Combatants,
            combatant => Assert.Equal(
                FormationFootprint.SingleCell,
                combatant.Placement.Footprint));
    }

    [Fact]
    public void Create_FixedEncounter_JamesCurrentHpEqualsResolvedMaximumHp()
    {
        CombatantSnapshot james = CombatTestFixture.CreateFixedBattle()
            .Snapshot.GetRequiredCombatant("party-0");

        Assert.Equal(96, james.MaximumHp);
        Assert.Equal(james.MaximumHp, james.CurrentHp);
        Assert.False(james.IsDefeated);
    }

    [Fact]
    public void Create_FixedEncounter_InitializesCurrentMpFromResolvedMaximumMp()
    {
        CombatSnapshot snapshot = CombatTestFixture.CreateFixedBattle().Snapshot;

        CombatantSnapshot james = snapshot.GetRequiredCombatant("party-0");
        Assert.Equal(12, james.MaximumMp);
        Assert.Equal(james.MaximumMp, james.CurrentMp);
        Assert.All(
            snapshot.Combatants.Where(combatant => combatant.Side == BattleSide.Enemy),
            combatant =>
            {
                Assert.Equal(0, combatant.MaximumMp);
                Assert.Equal(0, combatant.CurrentMp);
            });
    }

    [Fact]
    public void Create_FixedEncounter_PreservesStructuredPartyAbilityAvailability()
    {
        CombatantSnapshot james = CombatTestFixture.CreateFixedBattle()
            .Snapshot.GetRequiredCombatant("party-0");

        Assert.NotNull(james.PartyAbilityAvailability);
        Assert.Equal([CombatTestFixture.AttackId], james.DirectSkillIds);
        Assert.Empty(james.MagicDisciplines);
        Assert.Equal(james.PartyAbilityAvailability!.ExecutableAbilityIds, james.AbilityIds);
    }

    [Fact]
    public void Create_FixedEncounter_BothSlimesCurrentHpEqualsResolvedMaximumHp()
    {
        CombatSnapshot snapshot = CombatTestFixture.CreateFixedBattle().Snapshot;

        CombatantSnapshot first = snapshot.GetRequiredCombatant("enemy-0");
        CombatantSnapshot second = snapshot.GetRequiredCombatant("enemy-1");
        Assert.Equal(22, first.MaximumHp);
        Assert.Equal(first.MaximumHp, first.CurrentHp);
        Assert.Equal(22, second.MaximumHp);
        Assert.Equal(second.MaximumHp, second.CurrentHp);
    }

    [Fact]
    public void Create_EnemyDamageTypeModifiersAreCopiedIntoEachSnapshot()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        EnemyDefinition enemy = battle.Content.GetRequired<EnemyDefinition>(
            CombatTestFixture.GreenSlimeId);
        enemy.DamageTypePercentModifiers[DamageTypeIds.Fire] = 50;

        CombatSnapshot snapshot = new CombatSnapshotFactory(battle.Content).Create(
            battle.Campaign,
            battle.Encounter,
            battle.EnemyPlacements,
            battle.PartyPlacements);
        enemy.DamageTypePercentModifiers[DamageTypeIds.Fire] = 0;

        Assert.Empty(snapshot.GetRequiredCombatant("party-0").DamageTypePercentModifiers);
        Assert.All(
            snapshot.Combatants.Where(combatant => combatant.Side == BattleSide.Enemy),
            combatant => Assert.Equal(
                50,
                combatant.DamageTypePercentModifiers[DamageTypeIds.Fire]));
    }

    [Fact]
    public void Create_EnemyAbilities_PreserveAuthoredOrder()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        EnemyDefinition enemy = battle.Content.GetRequired<EnemyDefinition>(
            CombatTestFixture.GreenSlimeId);
        enemy.AbilityIds.Insert(0, CombatTestFixture.AttackId);

        CombatSnapshot snapshot = new CombatSnapshotFactory(battle.Content).Create(
            battle.Campaign,
            battle.Encounter,
            battle.EnemyPlacements,
            battle.PartyPlacements);

        Assert.Equal(
            [CombatTestFixture.AttackId, CombatTestFixture.TackleId],
            snapshot.GetRequiredCombatant("enemy-0").AbilityIds);
    }

    [Fact]
    public void Create_EnemyWithNoAbilities_RemainsValidInitialState()
    {
        SnapshotInput input = CreateInMemoryInput(actorMaximumHp: 10);

        CombatSnapshot snapshot = input.Factory.Create(
            input.Campaign,
            input.Encounter,
            input.EnemyPlacements,
            input.PartyPlacements);

        Assert.Empty(snapshot.GetRequiredCombatant("enemy-0").AbilityIds);
    }

    [Fact]
    public void Create_DuplicateBattleLocalInstanceId_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        FormationPlacement duplicate = battle.EnemyPlacements[0] with
        {
            InstanceId = "party-0",
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                battle.Campaign,
                battle.Encounter,
                [duplicate, battle.EnemyPlacements[1]],
                battle.PartyPlacements));

        Assert.Contains("party-0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_PartyPlacementOnEnemySide_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        FormationPlacement source = battle.PartyPlacements[0];
        FormationPlacement wrongSide = source with
        {
            Anchor = source.Anchor with { Side = BattleSide.Enemy },
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                battle.Campaign,
                battle.Encounter,
                battle.EnemyPlacements,
                [wrongSide]));

        Assert.Contains("party-0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Party", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_EnemyPlacementOnPartySide_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        FormationPlacement source = battle.EnemyPlacements[0];
        FormationPlacement wrongSide = source with
        {
            Anchor = source.Anchor with { Side = BattleSide.Party },
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                battle.Campaign,
                battle.Encounter,
                [wrongSide, battle.EnemyPlacements[1]],
                battle.PartyPlacements));

        Assert.Contains("enemy-0", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Enemy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_PartyPlacementReferencingEnemyDefinition_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        FormationPlacement wrongCategory = battle.PartyPlacements[0] with
        {
            DefinitionId = CombatTestFixture.GreenSlimeId,
        };

        Assert.Throws<KeyNotFoundException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                battle.Campaign,
                battle.Encounter,
                battle.EnemyPlacements,
                [wrongCategory]));
    }

    [Fact]
    public void Create_EnemyPlacementReferencingActorDefinition_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        FormationPlacement wrongCategory = battle.EnemyPlacements[0] with
        {
            DefinitionId = CombatTestFixture.JamesId,
        };

        Assert.Throws<KeyNotFoundException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                battle.Campaign,
                battle.Encounter,
                [wrongCategory, battle.EnemyPlacements[1]],
                battle.PartyPlacements));
    }

    [Fact]
    public void Create_ActivePartyActorWithoutProgress_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        GameState invalidCampaign = battle.Campaign with
        {
            ActorProgress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal),
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                invalidCampaign,
                battle.Encounter,
                battle.EnemyPlacements,
                battle.PartyPlacements));

        Assert.Contains(CombatTestFixture.JamesId, exception.Message, StringComparison.Ordinal);
        Assert.Contains("progress", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_MultipleProgressRecordsForOneActor_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        var duplicatedProgress = new Dictionary<string, ActorProgressState>(
            battle.Campaign.ActorProgress,
            StringComparer.Ordinal)
        {
            ["duplicate-key"] = battle.Campaign.ActorProgress[CombatTestFixture.JamesId],
        };
        GameState invalidCampaign = battle.Campaign with
        {
            ActorProgress = duplicatedProgress,
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                invalidCampaign,
                battle.Encounter,
                battle.EnemyPlacements,
                battle.PartyPlacements));

        Assert.Contains("2 progress records", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_PartyPlacementForActorAbsentFromActiveParty_IsRejected()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        GameState invalidCampaign = battle.Campaign with
        {
            ActivePartyActorIds = ["actor.test.someone-else"],
        };

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            new CombatSnapshotFactory(battle.Content).Create(
                invalidCampaign,
                battle.Encounter,
                battle.EnemyPlacements,
                battle.PartyPlacements));

        Assert.Contains("absent from the active party", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_MissingMaximumHpStatistic_IsRejected()
    {
        SnapshotInput input = CreateInMemoryInput(
            actorMaximumHp: null,
            includeMaximumHpDefinition: false);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            input.Factory.Create(
                input.Campaign,
                input.Encounter,
                input.EnemyPlacements,
                input.PartyPlacements));

        Assert.Contains(CombatStatisticIds.MaxHp, exception.Message, StringComparison.Ordinal);
        Assert.Contains("missing", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_NonpositiveMaximumHp_IsRejected(int maximumHp)
    {
        SnapshotInput input = CreateInMemoryInput(actorMaximumHp: maximumHp);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            input.Factory.Create(
                input.Campaign,
                input.Encounter,
                input.EnemyPlacements,
                input.PartyPlacements));

        Assert.Contains(maximumHp.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains("positive", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ManuallyConstructedNullStatisticMap_IsRejected()
    {
        SnapshotInput input = CreateInMemoryInput(
            actorMaximumHp: 10,
            nullActorStatistics: true);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            input.Factory.Create(
                input.Campaign,
                input.Encounter,
                input.EnemyPlacements,
                input.PartyPlacements));

        Assert.Contains("BaseStatistics", exception.Message, StringComparison.Ordinal);
        Assert.Contains("null", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_ManuallyConstructedNullEnemyAbilityCollection_IsRejected()
    {
        SnapshotInput input = CreateInMemoryInput(
            actorMaximumHp: 10,
            nullEnemyAbilities: true);

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            input.Factory.Create(
                input.Campaign,
                input.Encounter,
                input.EnemyPlacements,
                input.PartyPlacements));

        Assert.Contains("null ability list", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_MissingEnemyAbilityReference_IsRejected()
    {
        SnapshotInput input = CreateInMemoryInput(
            actorMaximumHp: 10,
            enemyAbilityIds: ["ability.test.missing"]);

        Assert.Throws<KeyNotFoundException>(() => input.Factory.Create(
            input.Campaign,
            input.Encounter,
            input.EnemyPlacements,
            input.PartyPlacements));
    }

    [Fact]
    public void Create_ReturnedCollectionsRejectMutationAndIgnoreLaterContentEdits()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatantSnapshot james = battle.Snapshot.GetRequiredCombatant("party-0");
        CombatantSnapshot enemy = battle.Snapshot.GetRequiredCombatant("enemy-0");

        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, int>)james.Statistics)[CombatStatisticIds.MaxHp] = 999);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)enemy.AbilityIds).Add("ability.test.cheat"));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)james.DirectSkillIds).Add("ability.test.cheat"));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CombatantSnapshot>)battle.Snapshot.Combatants).Clear());

        battle.Content.GetRequired<EnemyDefinition>(
            CombatTestFixture.GreenSlimeId).AbilityIds.Add(CombatTestFixture.AttackId);
        Assert.Equal([CombatTestFixture.TackleId], enemy.AbilityIds);
    }

    [Fact]
    public void Create_TwoSnapshots_AreEquivalentButIndependentlyOwned()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        var factory = new CombatSnapshotFactory(battle.Content);

        CombatSnapshot first = factory.Create(
            battle.Campaign,
            battle.Encounter,
            battle.EnemyPlacements,
            battle.PartyPlacements);
        CombatSnapshot second = factory.Create(
            battle.Campaign,
            battle.Encounter,
            battle.EnemyPlacements,
            battle.PartyPlacements);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Combatants, second.Combatants);
        Assert.Equal(
            first.Combatants.Select(combatant => combatant.InstanceId),
            second.Combatants.Select(combatant => combatant.InstanceId));

        for (int index = 0; index < first.Combatants.Count; index++)
        {
            Assert.NotSame(first.Combatants[index], second.Combatants[index]);
            Assert.NotSame(first.Combatants[index].Statistics, second.Combatants[index].Statistics);
            Assert.NotSame(first.Combatants[index].AbilityIds, second.Combatants[index].AbilityIds);
            if (first.Combatants[index].PartyAbilityAvailability is not null)
            {
                Assert.NotSame(
                    first.Combatants[index].PartyAbilityAvailability,
                    second.Combatants[index].PartyAbilityAvailability);
            }

            Assert.Equal(
                first.Combatants[index].Statistics.ToArray(),
                second.Combatants[index].Statistics.ToArray());
            Assert.Equal(
                first.Combatants[index].AbilityIds,
                second.Combatants[index].AbilityIds);
            Assert.Equal(
                first.Combatants[index].CurrentHp,
                second.Combatants[index].CurrentHp);
        }
    }

    [Fact]
    public void CombatantSnapshot_ZeroRuntimeCurrentHp_IsDefeated()
    {
        var placement = new FormationPlacement(
            "party-0",
            CombatTestFixture.JamesId,
            new FormationCell(BattleSide.Party, 0, 0),
            FormationFootprint.SingleCell);

        var combatant = new CombatantSnapshot(
            placement,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 10,
            },
            [],
            currentHp: 0);

        Assert.Equal(0, combatant.CurrentHp);
        Assert.True(combatant.IsDefeated);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void CombatantSnapshot_CurrentHpOutsideRuntimeRange_IsRejected(int currentHp)
    {
        var placement = new FormationPlacement(
            "party-0",
            CombatTestFixture.JamesId,
            new FormationCell(BattleSide.Party, 0, 0),
            FormationFootprint.SingleCell);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSnapshot(
            placement,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 10,
            },
            [],
            currentHp));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(11)]
    public void CombatantSnapshot_CurrentMpOutsideRuntimeRange_IsRejected(int currentMp)
    {
        var placement = new FormationPlacement(
            "party-0",
            CombatTestFixture.JamesId,
            new FormationCell(BattleSide.Party, 0, 0),
            FormationFootprint.SingleCell);

        Assert.Throws<ArgumentOutOfRangeException>(() => new CombatantSnapshot(
            placement,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 10,
                [CombatStatisticIds.MaxMp] = 10,
            },
            [],
            currentHp: 10,
            currentMp: currentMp));
    }

    [Fact]
    public void CombatantSnapshot_CurrentMpReplacementPreservesHpAndOtherState()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatantSnapshot original = battle.Snapshot.GetRequiredCombatant("party-0");

        CombatantSnapshot replaced = original.WithCurrentMp(3);
        CombatantSnapshot damaged = replaced.WithCurrentHp(80);

        Assert.Equal(12, original.CurrentMp);
        Assert.Equal(3, replaced.CurrentMp);
        Assert.Equal(original.CurrentHp, replaced.CurrentHp);
        Assert.Equal(3, damaged.CurrentMp);
        Assert.Equal(80, damaged.CurrentHp);
        Assert.Equal(original.DamageTypePercentModifiers, damaged.DamageTypePercentModifiers);
    }

    [Fact]
    public void CombatantSnapshot_DamageModifiersAreImmutableAndPreservedByHpReplacement()
    {
        var placement = new FormationPlacement(
            "enemy-0",
            CombatTestFixture.GreenSlimeId,
            new FormationCell(BattleSide.Enemy, 0, 0),
            FormationFootprint.SingleCell);
        var authored = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [DamageTypeIds.Fire] = 50,
            [DamageTypeIds.Slash] = -75,
        };
        var combatant = new CombatantSnapshot(
            placement,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 10,
            },
            [],
            currentHp: 10,
            damageTypePercentModifiers: authored);
        authored[DamageTypeIds.Fire] = 0;

        CombatantSnapshot damaged = combatant.WithCurrentHp(4);

        Assert.Equal(50, combatant.DamageTypePercentModifiers[DamageTypeIds.Fire]);
        Assert.Equal(
            combatant.DamageTypePercentModifiers.ToArray(),
            damaged.DamageTypePercentModifiers.ToArray());
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, int>)combatant.DamageTypePercentModifiers).Add(
                DamageTypeIds.Ice,
                20));
    }

    [Theory]
    [InlineData("damage-type.poison", 20, typeof(ArgumentException))]
    [InlineData(DamageTypeIds.Fire, -101, typeof(ArgumentOutOfRangeException))]
    public void CombatantSnapshot_InvalidDamageModifierIsRejected(
        string damageTypeId,
        int modifier,
        Type exceptionType)
    {
        var placement = new FormationPlacement(
            "enemy-0",
            CombatTestFixture.GreenSlimeId,
            new FormationCell(BattleSide.Enemy, 0, 0),
            FormationFootprint.SingleCell);

        Exception exception = Record.Exception(() => new CombatantSnapshot(
            placement,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 10,
            },
            [],
            currentHp: 10,
            damageTypePercentModifiers: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [damageTypeId] = modifier,
            }))!;

        Assert.IsType(exceptionType, exception);
    }

    private static SnapshotInput CreateInMemoryInput(
        int? actorMaximumHp,
        bool includeMaximumHpDefinition = true,
        bool nullActorStatistics = false,
        bool nullEnemyAbilities = false,
        IReadOnlyList<string>? enemyAbilityIds = null)
    {
        const string actorId = "actor.test.hero";
        const string classId = "class.test.job";
        const string enemyId = "enemy.test.slime";
        const string encounterId = "encounter.test.slime";

        var definitions = new List<RpgGame.Core.Content.ContentDefinition>();
        if (includeMaximumHpDefinition)
        {
            definitions.Add(new StatisticDefinition
            {
                Id = CombatStatisticIds.MaxHp,
                DisplayNameKey = "stat.max-hp.name",
                MinimumValue = -10,
                MaximumValue = 100,
                DefaultValue = 1,
            });
        }

        Dictionary<string, int> actorStatistics = [];
        if (actorMaximumHp.HasValue)
        {
            actorStatistics[CombatStatisticIds.MaxHp] = actorMaximumHp.Value;
        }

        var actor = new ActorDefinition
        {
            Id = actorId,
            DisplayNameKey = "actor.test.hero.name",
            BaseStatistics = nullActorStatistics ? null! : actorStatistics,
        };
        var classDefinition = new ClassDefinition
        {
            Id = classId,
            DisplayNameKey = "class.test.job.name",
        };
        var enemy = new EnemyDefinition
        {
            Id = enemyId,
            DisplayNameKey = "enemy.test.slime.name",
            Statistics = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 10,
            },
            AbilityIds = nullEnemyAbilities
                ? null!
                : enemyAbilityIds?.ToList() ?? [],
        };
        var encounter = new EncounterDefinition
        {
            Id = encounterId,
            EnemyGroup =
            [
                new EncounterEnemyDefinition
                {
                    EnemyId = enemyId,
                    SlotId = "formation.enemy.r0.c0",
                },
            ],
        };

        definitions.Add(actor);
        definitions.Add(classDefinition);
        definitions.Add(enemy);
        definitions.Add(encounter);
        if (enemyAbilityIds is not null)
        {
            foreach (string abilityId in enemyAbilityIds.Where(id =>
                         !string.Equals(id, "ability.test.missing", StringComparison.Ordinal)))
            {
                definitions.Add(CombatTestFixture.Ability(abilityId));
            }
        }

        var content = new TestCatalog(definitions.ToArray());
        var campaign = new GameState
        {
            SaveId = "in-memory-snapshot",
            ActivePartyActorIds = [actorId],
            ActorProgress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal)
            {
                [actorId] = new ActorProgressState
                {
                    ActorId = actorId,
                    ClassId = classId,
                },
            },
        };
        IReadOnlyList<FormationPlacement> partyPlacements =
            PartyFormationBuilder.Build(campaign.ActivePartyActorIds);
        IReadOnlyList<FormationPlacement> enemyPlacements =
            new EncounterFormationBuilder(content).Build(encounter);

        return new SnapshotInput(
            new CombatSnapshotFactory(content),
            campaign,
            encounter,
            enemyPlacements,
            partyPlacements);
    }

    private sealed record SnapshotInput(
        CombatSnapshotFactory Factory,
        GameState Campaign,
        EncounterDefinition Encounter,
        IReadOnlyList<FormationPlacement> EnemyPlacements,
        IReadOnlyList<FormationPlacement> PartyPlacements);
}
