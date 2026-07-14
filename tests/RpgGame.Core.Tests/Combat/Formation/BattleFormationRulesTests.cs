using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using Xunit;

namespace RpgGame.Core.Tests.Combat.Formation;

/// <summary>Headless executable specification for the fixed battle formations.</summary>
public sealed class BattleFormationRulesTests
{
    [Fact]
    public void Contains_FixedEnemyAndPartyDimensions_RejectsCellsPastEachBoundary()
    {
        Assert.True(BattleFormationRules.Contains(
            new FormationCell(BattleSide.Enemy, 3, 3)));
        Assert.False(BattleFormationRules.Contains(
            new FormationCell(BattleSide.Enemy, 4, 3)));
        Assert.False(BattleFormationRules.Contains(
            new FormationCell(BattleSide.Enemy, 3, 4)));

        Assert.True(BattleFormationRules.Contains(
            new FormationCell(BattleSide.Party, 3, 1)));
        Assert.False(BattleFormationRules.Contains(
            new FormationCell(BattleSide.Party, 4, 1)));
        Assert.False(BattleFormationRules.Contains(
            new FormationCell(BattleSide.Party, 3, 2)));
    }

    [Fact]
    public void ValidatePlacements_OneByOneEnemy_IsValid()
    {
        FormationPlacement placement = Enemy("enemy-0", 1, 0);

        IReadOnlyList<FormationProblem> problems =
            BattleFormationRules.ValidatePlacements([placement]);

        Assert.Empty(problems);
        Assert.Equal(
            new[] { new FormationCell(BattleSide.Enemy, 1, 0) },
            BattleFormationRules.GetOccupiedCells(placement));
    }

    [Fact]
    public void GetOccupiedCells_TwoByTwoEnemy_ReturnsDeterministicRowMajorOrder()
    {
        FormationPlacement placement = Enemy("enemy-0", 1, 0, rows: 2, columns: 2);

        IReadOnlyList<FormationCell> cells =
            BattleFormationRules.GetOccupiedCells(placement);

        Assert.Equal(
            new[]
            {
                new FormationCell(BattleSide.Enemy, 1, 0),
                new FormationCell(BattleSide.Enemy, 1, 1),
                new FormationCell(BattleSide.Enemy, 2, 0),
                new FormationCell(BattleSide.Enemy, 2, 1),
            },
            cells);
        Assert.Empty(BattleFormationRules.ValidatePlacements([placement]));
    }

    [Fact]
    public void ValidatePlacements_FootprintCrossesBottomBoundary_IsRejected()
    {
        FormationPlacement placement = Enemy("enemy-0", 3, 0, rows: 2, columns: 1);

        FormationProblem problem = Assert.Single(
            BattleFormationRules.ValidatePlacements([placement]),
            candidate => candidate.Kind == FormationProblemKind.OutOfBounds);

        Assert.Equal(
            new[] { new FormationCell(BattleSide.Enemy, 4, 0) },
            problem.Cells);
    }

    [Fact]
    public void ValidatePlacements_FootprintCrossesRearBoundary_IsRejected()
    {
        FormationPlacement placement = Enemy("enemy-0", 0, 3, rows: 1, columns: 2);

        FormationProblem problem = Assert.Single(
            BattleFormationRules.ValidatePlacements([placement]),
            candidate => candidate.Kind == FormationProblemKind.OutOfBounds);

        Assert.Equal(
            new[] { new FormationCell(BattleSide.Enemy, 0, 4) },
            problem.Cells);
    }

    [Fact]
    public void ValidatePlacements_LargeAndSmallEnemyOverlap_IsRejected()
    {
        FormationPlacement large = Enemy("enemy-0", 1, 0, rows: 2, columns: 2);
        FormationPlacement small = Enemy("enemy-1", 2, 1);

        FormationProblem problem = Assert.Single(
            BattleFormationRules.ValidatePlacements([large, small]),
            candidate => candidate.Kind == FormationProblemKind.Overlap);

        Assert.Equal("enemy-1", problem.InstanceId);
        Assert.Equal("enemy-0", problem.ConflictingInstanceId);
        Assert.Equal(
            new[] { new FormationCell(BattleSide.Enemy, 2, 1) },
            problem.Cells);
    }

    [Fact]
    public void ValidatePlacements_TwoLargeEnemiesOverlap_IsRejected()
    {
        FormationPlacement first = Enemy("enemy-0", 0, 0, rows: 2, columns: 2);
        FormationPlacement second = Enemy("enemy-1", 1, 1, rows: 2, columns: 2);

        FormationProblem problem = Assert.Single(
            BattleFormationRules.ValidatePlacements([first, second]),
            candidate => candidate.Kind == FormationProblemKind.Overlap);

        Assert.Equal(
            new[] { new FormationCell(BattleSide.Enemy, 1, 1) },
            problem.Cells);
    }

    [Fact]
    public void ValidatePlacements_AdjacentLargeEnemies_DoNotOverlap()
    {
        FormationPlacement top = Enemy("enemy-0", 0, 0, rows: 2, columns: 2);
        FormationPlacement bottom = Enemy("enemy-1", 2, 0, rows: 2, columns: 2);

        IReadOnlyList<FormationProblem> problems =
            BattleFormationRules.ValidatePlacements([top, bottom]);

        Assert.Empty(problems);
    }

    [Fact]
    public void ValidatePlacements_DuplicateInstanceId_IsRejected()
    {
        FormationPlacement first = Enemy("enemy-0", 0, 0);
        FormationPlacement second = Enemy("enemy-0", 1, 0);

        FormationProblem problem = Assert.Single(
            BattleFormationRules.ValidatePlacements([first, second]),
            candidate => candidate.Kind == FormationProblemKind.DuplicateInstanceId);

        Assert.Equal("enemy-0", problem.InstanceId);
    }

    [Fact]
    public void ValidatePlacements_NonpositiveFootprint_IsRejected()
    {
        FormationPlacement placement = Enemy("enemy-0", 0, 0, rows: 0, columns: 1);

        FormationProblem problem = Assert.Single(
            BattleFormationRules.ValidatePlacements([placement]),
            candidate => candidate.Kind == FormationProblemKind.InvalidFootprint);

        Assert.Equal("enemy-0", problem.InstanceId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("formation.left")]
    [InlineData("formation.party.r0.c0")]
    [InlineData("formation.enemy.r4.c0")]
    [InlineData("formation.enemy.r0.c4")]
    [InlineData("formation.enemy.r00.c0")]
    [InlineData("Formation.enemy.r0.c0")]
    public void TryParseEnemy_MalformedOrNonEnemySlot_IsRejected(string? slotId)
    {
        bool parsed = FormationSlotId.TryParseEnemy(slotId, out _);

        Assert.False(parsed);
    }

    [Fact]
    public void Format_EnemyAndPartyCells_UsesCanonicalSideRelativeIds()
    {
        Assert.Equal(
            "formation.enemy.r3.c3",
            FormationSlotId.Format(new FormationCell(BattleSide.Enemy, 3, 3)));
        Assert.Equal(
            "formation.party.r0.c0",
            FormationSlotId.Format(new FormationCell(BattleSide.Party, 0, 0)));
    }

    [Fact]
    public void EncounterFormationBuilder_CheckedInEncounter_PreservesOrderAndIds()
    {
        var catalog = TestContent.LoadCatalog();
        EncounterDefinition encounter = catalog.GetRequired<EncounterDefinition>(
            "encounter.forest.slimes-01");

        IReadOnlyList<FormationPlacement> placements =
            new EncounterFormationBuilder(catalog).Build(encounter);

        Assert.Collection(
            placements,
            placement =>
            {
                Assert.Equal("enemy-0", placement.InstanceId);
                Assert.Equal("enemy.forest.green-slime", placement.DefinitionId);
                Assert.Equal(new FormationCell(BattleSide.Enemy, 1, 0), placement.Anchor);
                Assert.Equal(FormationFootprint.SingleCell, placement.Footprint);
            },
            placement =>
            {
                Assert.Equal("enemy-1", placement.InstanceId);
                Assert.Equal("enemy.forest.green-slime", placement.DefinitionId);
                Assert.Equal(new FormationCell(BattleSide.Enemy, 2, 0), placement.Anchor);
                Assert.Equal(FormationFootprint.SingleCell, placement.Footprint);
            });
    }

    [Fact]
    public void PartyFormationBuilder_FourActors_UsesRowsInFrontColumn()
    {
        string[] actorIds =
        [
            "actor.hero.james",
            "actor.hero.second",
            "actor.hero.third",
            "actor.hero.fourth",
        ];

        IReadOnlyList<FormationPlacement> placements = PartyFormationBuilder.Build(actorIds);

        Assert.Equal(4, placements.Count);
        for (int index = 0; index < placements.Count; index++)
        {
            Assert.Equal($"party-{index}", placements[index].InstanceId);
            Assert.Equal(actorIds[index], placements[index].DefinitionId);
            Assert.Equal(
                new FormationCell(BattleSide.Party, index, 0),
                placements[index].Anchor);
            Assert.Equal(FormationFootprint.SingleCell, placements[index].Footprint);
        }
    }

    private static FormationPlacement Enemy(
        string instanceId,
        int row,
        int column,
        int rows = 1,
        int columns = 1) =>
        new(
            instanceId,
            "enemy.test.fixture",
            new FormationCell(BattleSide.Enemy, row, column),
            new FormationFootprint(rows, columns));
}
