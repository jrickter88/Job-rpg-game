using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class FirstClassKitsTests
{
    private const string KnightId = "class.martial.knight";
    private const string BlackMageId = "class.magic.black-mage";
    private const string WhiteMageId = "class.magic.white-mage";
    private const string PowerStrikeId = "ability.knight.power-strike";
    private const string FireId = "ability.black-magic.fire";
    private const string IceId = "ability.black-magic.ice";
    private const string LightningId = "ability.black-magic.lightning";
    private const string BlackMagicId = "magic-discipline.black-magic";
    private const string WhiteMagicId = "magic-discipline.white-magic";
    private const string CureId = "ability.white-magic.cure";
    private const string PotionId = "ability.item.potion";

    [Fact]
    public void ResolvePartyActor_KnightAddsPowerStrikeAsDirectSkill()
    {
        ContentCatalog content = TestContent.LoadCatalog();

        PartyAbilityAvailability availability = ResolveAvailability(content, KnightId);

        Assert.Equal([CombatTestFixture.AttackId, PowerStrikeId], availability.DirectSkillIds);
        Assert.Empty(availability.MagicDisciplines);
    }

    [Fact]
    public void ResolvePartyActor_BlackMageStartsWithItsFullTestSpellbook()
    {
        ContentCatalog content = TestContent.LoadCatalog();

        AssertSpellbook(content, 1, [FireId, IceId, LightningId]);
    }

    [Fact]
    public void ResolvePartyActor_WhiteMageHasLearnedCureInWhiteMagic()
    {
        ContentCatalog content = TestContent.LoadCatalog();

        PartyAbilityAvailability availability = ResolveAvailability(content, WhiteMageId);

        Assert.Equal([CombatTestFixture.AttackId, CureId], availability.ExecutableAbilityIds);
        MagicDisciplineAvailability discipline = Assert.Single(availability.MagicDisciplines);
        Assert.Equal(WhiteMagicId, discipline.MagicDisciplineId);
        Assert.Equal([CureId], discipline.SpellAbilityIds);
    }

    [Theory]
    [InlineData(FireId, DamageTypeIds.Fire, 1)]
    [InlineData(IceId, DamageTypeIds.Ice, 1)]
    [InlineData(LightningId, DamageTypeIds.Lightning, 1)]
    public void Resolve_BlackMageSpellEmitsItsAuthoredDamageTypeAtUnlockLevel(
        string abilityId,
        string expectedDamageTypeId,
        int level)
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(BlackMageId, level);

        CombatResolution resolution = new CombatResolver(battle.Content).Resolve(
            battle.Snapshot,
            new CombatCommand("party-0", abilityId, ["enemy-0"]));

        Assert.IsType<ResourceSpent>(resolution.Events[0]);
        DamageApplied damage = Assert.IsType<DamageApplied>(resolution.Events[1]);
        Assert.Equal(expectedDamageTypeId, damage.DamageTypeId);
    }

    private static void AssertSpellbook(
        ContentCatalog content,
        int level,
        IReadOnlyList<string> expectedSpellIds)
    {
        PartyAbilityAvailability availability = ResolveAvailability(content, BlackMageId, level);

        Assert.Equal([CombatTestFixture.AttackId], availability.DirectSkillIds);
        MagicDisciplineAvailability discipline = Assert.Single(availability.MagicDisciplines);
        Assert.Equal(BlackMagicId, discipline.MagicDisciplineId);
        Assert.Equal(expectedSpellIds, discipline.SpellAbilityIds);
        Assert.Equal(
            [CombatTestFixture.AttackId, .. expectedSpellIds],
            availability.ExecutableAbilityIds);
    }

    [Fact]
    public void Resolve_FireSpendsMpExactlyOnce()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(BlackMageId);
        CombatantSnapshot originalParty = battle.Snapshot.GetRequiredCombatant("party-0");

        CombatResolution resolution = new CombatResolver(battle.Content).Resolve(
            battle.Snapshot,
            new CombatCommand("party-0", FireId, ["enemy-0"]));

        CombatantSnapshot updatedParty = resolution.Next.GetRequiredCombatant("party-0");
        ResourceSpent spent = Assert.IsType<ResourceSpent>(resolution.Events[0]);
        Assert.Equal(FireId, spent.AbilityId);
        Assert.Equal(3, spent.Amount);
        Assert.Equal(originalParty.CurrentMp, spent.PreviousValue);
        Assert.Equal(originalParty.CurrentMp - spent.Amount, spent.CurrentValue);
        Assert.Equal(spent.CurrentValue, updatedParty.CurrentMp);
    }

    [Fact]
    public void Resolve_PotionSelfHeal_RestoresHpWithoutSpendingMp()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatantSnapshot[] combatants = battle.Snapshot.Combatants.ToArray();
        combatants[0] = combatants[0].WithCurrentHp(40);
        var injuredSnapshot = new CombatSnapshot(
            battle.Snapshot.Round,
            battle.Snapshot.TimelineTime,
            combatants);

        CombatResolution resolution = new CombatResolver(battle.Content).Resolve(
            injuredSnapshot,
            new CombatCommand("party-0", PotionId, ["party-0"]));

        HealingApplied healing = Assert.IsType<HealingApplied>(Assert.Single(resolution.Events));
        Assert.Equal(30, healing.Amount);
        Assert.Equal(40, healing.PreviousHp);
        Assert.Equal(70, healing.CurrentHp);
        Assert.Equal(70, resolution.Next.GetRequiredCombatant("party-0").CurrentHp);
        Assert.Equal(
            injuredSnapshot.GetRequiredCombatant("party-0").CurrentMp,
            resolution.Next.GetRequiredCombatant("party-0").CurrentMp);
    }

    [Fact]
    public void Resolve_InsufficientFireMpRejectsWithoutChangingHpOrMp()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(BlackMageId);
        CombatSnapshot insufficientMp = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => combatant.WithCurrentMp(2));

        CombatCommandValidationException exception = Assert.Throws<CombatCommandValidationException>(
            () => new CombatResolver(battle.Content).Resolve(
                insufficientMp,
                new CombatCommand("party-0", FireId, ["enemy-0"])));

        Assert.Equal(CombatCommandProblemCodes.AbilityResourceInsufficient, exception.ProblemCode);
        Assert.Equal(2, insufficientMp.GetRequiredCombatant("party-0").CurrentMp);
        Assert.Equal(22, insufficientMp.GetRequiredCombatant("enemy-0").CurrentHp);
    }

    [Fact]
    public void Resolve_CureHealsLivingAllyAndSpendsMpOnce()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(WhiteMageId);
        CombatSnapshot damaged = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => combatant.WithCurrentHp(60));
        CombatantSnapshot originalParty = damaged.GetRequiredCombatant("party-0");

        CombatResolution resolution = new CombatResolver(battle.Content).Resolve(
            damaged,
            new CombatCommand("party-0", CureId, ["party-0"]));

        CombatantSnapshot healed = resolution.Next.GetRequiredCombatant("party-0");
        Assert.Equal(72, healed.CurrentHp);
        Assert.Equal(originalParty.CurrentMp - 3, healed.CurrentMp);
        Assert.Collection(
            resolution.Events,
            combatEvent => Assert.IsType<ResourceSpent>(combatEvent),
            combatEvent =>
            {
                HealingApplied healing = Assert.IsType<HealingApplied>(combatEvent);
                Assert.Equal("party-0", healing.ActingCombatantId);
                Assert.Equal("party-0", healing.TargetCombatantId);
                Assert.Equal(CureId, healing.AbilityId);
                Assert.Equal(12, healing.Amount);
                Assert.Equal(60, healing.PreviousHp);
                Assert.Equal(72, healing.CurrentHp);
            });
    }

    [Fact]
    public void Resolve_CureClampsAtMaximumHpAndReportsActualHealing()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(WhiteMageId);
        CombatantSnapshot party = battle.Snapshot.GetRequiredCombatant("party-0");
        CombatSnapshot damaged = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => combatant.WithCurrentHp(combatant.MaximumHp - 4));

        CombatResolution resolution = new CombatResolver(battle.Content).Resolve(
            damaged,
            new CombatCommand("party-0", CureId, ["party-0"]));

        HealingApplied healing = Assert.IsType<HealingApplied>(resolution.Events[1]);
        Assert.Equal(4, healing.Amount);
        Assert.Equal(party.MaximumHp, healing.CurrentHp);
        Assert.Equal(party.MaximumHp, resolution.Next.GetRequiredCombatant("party-0").CurrentHp);
    }

    [Fact]
    public void Resolve_CureCannotTargetEnemy()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(WhiteMageId);

        CombatCommandValidationException exception = Assert.Throws<CombatCommandValidationException>(
            () => new CombatResolver(battle.Content).Resolve(
                battle.Snapshot,
                new CombatCommand("party-0", CureId, ["enemy-0"])));

        Assert.Equal(CombatCommandProblemCodes.TargetAllyRequired, exception.ProblemCode);
    }

    [Fact]
    public void Resolve_CureCannotTargetDefeatedAlly()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(WhiteMageId);
        CombatantSnapshot party = battle.Snapshot.GetRequiredCombatant("party-0");
        var defeatedAlly = new CombatantSnapshot(
            new FormationPlacement(
                "party-1",
                "actor.hero.james",
                new FormationCell(BattleSide.Party, 1, 0),
                FormationFootprint.SingleCell),
            party.Statistics,
            Array.Empty<string>(),
            currentHp: 0,
            currentMp: party.CurrentMp);
        var withDefeatedAlly = new CombatSnapshot(
            battle.Snapshot.Round,
            battle.Snapshot.Combatants.Concat([defeatedAlly]).ToArray());

        CombatCommandValidationException exception = Assert.Throws<CombatCommandValidationException>(
            () => new CombatResolver(battle.Content).Resolve(
                withDefeatedAlly,
                new CombatCommand("party-0", CureId, ["party-1"])));

        Assert.Equal(CombatCommandProblemCodes.TargetDefeated, exception.ProblemCode);
    }

    [Fact]
    public void Resolve_InsufficientCureMpRejectsWithoutChangingHpOrMp()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle(WhiteMageId);
        CombatSnapshot insufficientMp = ReplaceCombatant(
            battle.Snapshot,
            "party-0",
            combatant => combatant.WithCurrentHp(60).WithCurrentMp(2));

        CombatCommandValidationException exception = Assert.Throws<CombatCommandValidationException>(
            () => new CombatResolver(battle.Content).Resolve(
                insufficientMp,
                new CombatCommand("party-0", CureId, ["party-0"])));

        Assert.Equal(CombatCommandProblemCodes.AbilityResourceInsufficient, exception.ProblemCode);
        Assert.Equal(60, insufficientMp.GetRequiredCombatant("party-0").CurrentHp);
        Assert.Equal(2, insufficientMp.GetRequiredCombatant("party-0").CurrentMp);
    }

    [Fact]
    public void KnightId_ResolvesTheKnightKit()
    {
        ContentCatalog content = TestContent.LoadCatalog();

        PartyAbilityAvailability availability = ResolveAvailability(content, KnightId);

        Assert.Equal(
            [CombatTestFixture.AttackId, PowerStrikeId],
            availability.ExecutableAbilityIds);
    }

    private static PartyAbilityAvailability ResolveAvailability(
        ContentCatalog content,
        string classId,
        int level = 1) =>
        new AbilityAvailabilityResolver(content).ResolvePartyActor(new ActorProgressState
        {
            ActorId = CombatTestFixture.JamesId,
            ClassId = classId,
            Level = level,
        });

    private static CombatSnapshot ReplaceCombatant(
        CombatSnapshot snapshot,
        string instanceId,
        Func<CombatantSnapshot, CombatantSnapshot> replacement)
    {
        CombatantSnapshot[] combatants = snapshot.Combatants
            .Select(combatant => string.Equals(combatant.InstanceId, instanceId, StringComparison.Ordinal)
                ? replacement(combatant)
                : combatant)
            .ToArray();
        return new CombatSnapshot(snapshot.Round, combatants);
    }
}
