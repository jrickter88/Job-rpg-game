using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class BattleCompletionRequestTests
{
    [Fact]
    public void FromFinalSnapshot_PreservesDefeatedEnemyDefinitionOrderAndDuplicates()
    {
        var finalSnapshot = new CombatSnapshot(
            3,
            [
                Combatant("party-0", "actor.test.hero", BattleSide.Party, currentHp: 5),
                Combatant("enemy-0", "enemy.test.second", BattleSide.Enemy, currentHp: 0),
                Combatant("enemy-1", "enemy.test.first", BattleSide.Enemy, currentHp: 0),
                Combatant("enemy-2", "enemy.test.second", BattleSide.Enemy, currentHp: 0),
            ]);

        BattleCompletionRequest request = BattleCompletionRequest.FromFinalSnapshot(
            "encounter.test.ordered",
            finalSnapshot);

        Assert.Equal(BattleOutcome.PartyVictory, request.Outcome);
        Assert.Equal(
            ["enemy.test.second", "enemy.test.first", "enemy.test.second"],
            request.DefeatedEnemyDefinitionIds);
    }

    [Fact]
    public void FromFinalSnapshot_PartyDefeatIncludesOnlyEnemiesDefeatedBeforeLoss()
    {
        var finalSnapshot = new CombatSnapshot(
            4,
            [
                Combatant("party-0", "actor.test.hero", BattleSide.Party, currentHp: 0),
                Combatant("enemy-0", "enemy.test.defeated", BattleSide.Enemy, currentHp: 0),
                Combatant("enemy-1", "enemy.test.living", BattleSide.Enemy, currentHp: 2),
            ]);

        BattleCompletionRequest request = BattleCompletionRequest.FromFinalSnapshot(
            "encounter.test.defeat",
            finalSnapshot);

        Assert.Equal(BattleOutcome.PartyDefeat, request.Outcome);
        Assert.Equal(["enemy.test.defeated"], request.DefeatedEnemyDefinitionIds);
    }

    [Fact]
    public void Constructor_DefensivelyCopiesDefeatedEnemyDefinitionIds()
    {
        var source = new List<string> { "enemy.test.first" };
        var request = new BattleCompletionRequest(
            "encounter.test.copy",
            BattleOutcome.PartyVictory,
            source);

        source[0] = "enemy.test.changed";
        source.Add("enemy.test.added");

        Assert.Equal(["enemy.test.first"], request.DefeatedEnemyDefinitionIds);
        var exposed = Assert.IsAssignableFrom<IList<string>>(
            request.DefeatedEnemyDefinitionIds);
        Assert.Throws<NotSupportedException>(() => exposed[0] = "enemy.test.changed");
    }

    [Fact]
    public void Constructor_RejectsNullOrBlankDefeatedEnemyDefinitionIds()
    {
        Assert.Throws<ArgumentNullException>(() => new BattleCompletionRequest(
            "encounter.test.invalid",
            BattleOutcome.PartyVictory,
            null!));
        Assert.Throws<ArgumentException>(() => new BattleCompletionRequest(
            "encounter.test.invalid",
            BattleOutcome.PartyVictory,
            ["enemy.test.valid", null!]));
        Assert.Throws<ArgumentException>(() => new BattleCompletionRequest(
            "encounter.test.invalid",
            BattleOutcome.PartyVictory,
            ["enemy.test.valid", " "]));
    }

    private static CombatantSnapshot Combatant(
        string instanceId,
        string definitionId,
        BattleSide side,
        int currentHp) => new(
            new FormationPlacement(
                instanceId,
                definitionId,
                new FormationCell(side, 0, 0),
                FormationFootprint.SingleCell),
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CombatStatisticIds.MaxHp] = 5,
            },
            Array.Empty<string>(),
            currentHp);
}
