using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class CombatTimelineTests
{
    [Fact]
    public void Factory_InitializesOpeningTimelineFromSpeed()
    {
        CombatSnapshot snapshot = CombatTestFixture.CreateFixedBattle().Snapshot;

        Assert.Equal(0, snapshot.TimelineTime);
        Assert.Equal(70, snapshot.GetRequiredCombatant("party-0").NextActionTime);
        Assert.Equal(90, snapshot.GetRequiredCombatant("enemy-0").NextActionTime);
        Assert.Equal(90, snapshot.GetRequiredCombatant("enemy-1").NextActionTime);
        Assert.Equal("party-0", CombatTimeline.SelectReadyActor(snapshot).InstanceId);
    }

    [Fact]
    public void ReadySelection_UsesNextTimeThenSpeedSideAndStableId()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatantSnapshot party = battle.Snapshot.GetRequiredCombatant("party-0")
            .WithNextActionTime(100);
        CombatantSnapshot firstEnemy = battle.Snapshot.GetRequiredCombatant("enemy-0")
            .WithNextActionTime(20);
        CombatSnapshot delayed = Replace(battle.Snapshot, party, firstEnemy);

        Assert.Equal("enemy-0", CombatTimeline.SelectReadyActor(delayed).InstanceId);

        CombatSnapshot tied = Replace(
            delayed,
            delayed.GetRequiredCombatant("enemy-1").WithNextActionTime(20));
        Assert.Equal("enemy-0", CombatTimeline.SelectReadyActor(tied).InstanceId);
    }

    [Fact]
    public void FasterCombatantAppearsMoreOftenInForecast()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot snapshot = Replace(
            battle.Snapshot,
            battle.Snapshot.GetRequiredCombatant("party-0").WithNextActionTime(0),
            battle.Snapshot.GetRequiredCombatant("enemy-0").WithNextActionTime(80),
            battle.Snapshot.GetRequiredCombatant("enemy-1").WithNextActionTime(80));

        TurnOrderPreview preview = new TurnOrderPreviewService().Create(snapshot, 6);

        Assert.Equal("party-0", preview.Entries[0].CombatantInstanceId);
        Assert.True(preview.Entries.Count(entry => entry.CombatantInstanceId == "party-0") >
            preview.Entries.Count(entry => entry.CombatantInstanceId == "enemy-0"));
    }

    [Fact]
    public void ResolveNext_AdvancesTimeAndReschedulesActingCombatant()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot snapshot = battle.Snapshot;
        CombatResolution result = new CombatTimelineResolver(new CombatResolver(battle.Content))
            .ResolveNext(
                snapshot,
                new CombatCommand("party-0", CombatTestFixture.AttackId, ["enemy-0"]));

        Assert.Equal(70, result.Next.TimelineTime);
        Assert.Equal(
            snapshot.GetRequiredCombatant("party-0").NextActionTime
            + CombatTimeline.CalculateActionDelay(snapshot.GetRequiredCombatant("party-0")),
            result.Next.GetRequiredCombatant("party-0").NextActionTime);
        Assert.Equal(90, result.Next.GetRequiredCombatant("enemy-0").NextActionTime);
    }

    [Fact]
    public void ResolveNext_NonReadyActorIsRejectedWithoutMutation()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot original = battle.Snapshot;

        CombatTimelineValidationException exception = Assert.Throws<CombatTimelineValidationException>(
            () => new CombatTimelineResolver(new CombatResolver(battle.Content)).ResolveNext(
                original,
                new CombatCommand("enemy-0", CombatTestFixture.TackleId, ["party-0"])));

        Assert.Equal(CombatTimelineProblemCodes.ActorNotReady, exception.ProblemCode);
        Assert.Equal(original, battle.Snapshot);
    }

    [Fact]
    public void Preview_ExcludesDefeatedCombatantsAndDoesNotMutateSnapshot()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot original = battle.Snapshot;
        CombatSnapshot defeated = Replace(
            original,
            original.GetRequiredCombatant("enemy-0").WithCurrentHp(0));

        TurnOrderPreview preview = new TurnOrderPreviewService().Create(defeated, 8);

        Assert.DoesNotContain(preview.Entries, entry => entry.CombatantInstanceId == "enemy-0");
        Assert.Equal(original, battle.Snapshot);
    }

    [Fact]
    public void Preview_ChangesWhenNextActionTimeChanges()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        TurnOrderPreviewService service = new();
        TurnOrderPreview before = service.Create(battle.Snapshot, 1);
        CombatSnapshot delayed = Replace(
            battle.Snapshot,
            battle.Snapshot.GetRequiredCombatant("party-0").WithNextActionTime(100));
        TurnOrderPreview after = service.Create(delayed, 1);

        Assert.NotEqual(
            before.Entries[0].CombatantInstanceId,
            after.Entries[0].CombatantInstanceId);
    }

    private static CombatSnapshot Replace(
        CombatSnapshot source,
        params CombatantSnapshot[] replacements)
    {
        CombatantSnapshot[] combatants = source.Combatants.ToArray();
        foreach (CombatantSnapshot replacement in replacements)
        {
            int index = Array.FindIndex(
                combatants,
                combatant => combatant.InstanceId == replacement.InstanceId);
            combatants[index] = replacement;
        }

        return new CombatSnapshot(source.Round, source.TimelineTime, combatants);
    }
}
