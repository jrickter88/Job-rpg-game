using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class BattleCommandAvailabilityResolverTests
{
    [Fact]
    public void Resolve_ProjectsExecutableDirectSkillsAndNestedMagicInAuthoredOrder()
    {
        CommandInput input = CreateInput(currentMp: 5);

        BattleCommandAvailability result = new BattleCommandAvailabilityResolver(input.Content)
            .Resolve(input.PartyCombatant);

        Assert.Equal([input.DirectSkill.Id], result.DirectAbilityIds);
        MagicBattleCommandAvailability discipline = Assert.Single(result.MagicDisciplines);
        Assert.Equal(input.Discipline.Id, discipline.MagicDisciplineId);
        Assert.Equal([input.Spell.Id], discipline.SpellAbilityIds);
    }

    [Fact]
    public void Resolve_ExcludesInsufficientMpSpellButKeepsItsUnlockedMagicContainer()
    {
        CommandInput input = CreateInput(currentMp: 2);

        BattleCommandAvailability result = new BattleCommandAvailabilityResolver(input.Content)
            .Resolve(input.PartyCombatant);

        Assert.Equal([input.DirectSkill.Id], result.DirectAbilityIds);
        MagicBattleCommandAvailability discipline = Assert.Single(result.MagicDisciplines);
        Assert.Equal(input.Discipline.Id, discipline.MagicDisciplineId);
        Assert.Empty(discipline.SpellAbilityIds);
    }

    [Fact]
    public void Resolve_ExcludesLearnedButUnimplementedDirectSkill()
    {
        CommandInput input = CreateInput(currentMp: 5);
        AbilityDefinition guard = CombatTestFixture.Ability("ability.test.guard");
        ActorDefinition actor = input.Actor with
        {
            StartingAbilityIds = [guard.Id, input.DirectSkill.Id, input.Spell.Id],
        };
        var content = new TestCatalog(
            guard,
            input.DirectSkill,
            input.Spell,
            input.Discipline,
            actor,
            input.ClassDefinition);
        PartyAbilityAvailability availability = new AbilityAvailabilityResolver(content)
            .ResolvePartyActor(new ActorProgressState
            {
                ActorId = actor.Id,
                ClassId = input.ClassDefinition.Id,
            });
        CombatantSnapshot party = CreatePartyCombatant(availability, currentMp: 5);

        BattleCommandAvailability result = new BattleCommandAvailabilityResolver(content)
            .Resolve(party);

        Assert.Equal([input.DirectSkill.Id], result.DirectAbilityIds);
    }

    private static CommandInput CreateInput(int currentMp)
    {
        AbilityDefinition directSkill = PhysicalAbility("ability.test.strike", costAmount: 0);
        MagicDisciplineDefinition discipline = new()
        {
            Id = "magic-discipline.test.elemental",
            DisplayNameKey = "magic-discipline.test.elemental.name",
            DescriptionKey = "magic-discipline.test.elemental.description",
        };
        AbilityDefinition spell = PhysicalAbility("ability.test.fire", costAmount: 3) with
        {
            AbilityKindId = AbilityKindIds.Magic,
            MagicDisciplineIds = [discipline.Id],
            DamageTypeId = DamageTypeIds.Fire,
        };
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [directSkill.Id, spell.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.mage",
            DisplayNameKey = "class.test.mage.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition
                {
                    Level = 1,
                    MagicDisciplineId = discipline.Id,
                },
            ],
        };
        var content = new TestCatalog(directSkill, spell, discipline, actor, classDefinition);
        PartyAbilityAvailability availability = new AbilityAvailabilityResolver(content)
            .ResolvePartyActor(new ActorProgressState
            {
                ActorId = actor.Id,
                ClassId = classDefinition.Id,
            });

        return new CommandInput(
            content,
            actor,
            classDefinition,
            directSkill,
            discipline,
            spell,
            CreatePartyCombatant(availability, currentMp));
    }

    private static AbilityDefinition PhysicalAbility(string id, int costAmount) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
        AbilityKindId = AbilityKindIds.Skill,
        TargetingId = AbilityTargetingIds.SingleEnemy,
        CostStatisticId = costAmount == 0 ? null : CombatStatisticIds.MaxMp,
        CostAmount = costAmount,
        RulesetId = AbilityRulesetIds.PhysicalDamage,
        NumericParameters = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [AbilityNumericParameterIds.Power] = 4m,
        },
    };

    private static CombatantSnapshot CreatePartyCombatant(
        PartyAbilityAvailability availability,
        int currentMp) => new(
        new FormationPlacement(
            "party-0",
            "actor.test.hero",
            new FormationCell(BattleSide.Party, 0, 0),
            FormationFootprint.SingleCell),
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CombatStatisticIds.MaxHp] = 10,
            [CombatStatisticIds.MaxMp] = 5,
            [CombatStatisticIds.Strength] = 5,
            [CombatStatisticIds.Defense] = 5,
            [CombatStatisticIds.Speed] = 5,
        },
        availability,
        currentHp: 10,
        currentMp: currentMp);

    private sealed record CommandInput(
        TestCatalog Content,
        ActorDefinition Actor,
        ClassDefinition ClassDefinition,
        AbilityDefinition DirectSkill,
        MagicDisciplineDefinition Discipline,
        AbilityDefinition Spell,
        CombatantSnapshot PartyCombatant);
}
