using RpgGame.Core.Combat;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class AbilityAvailabilityResolverTests
{
    [Fact]
    public void ResolvePartyActor_CheckedInVanguard_GuardRemainsDirectSkill()
    {
        CombatantSnapshot james = CombatTestFixture.CreateFixedBattle()
            .Snapshot.GetRequiredCombatant("party-0");

        Assert.Equal([CombatTestFixture.GuardId], james.DirectSkillIds);
        Assert.Empty(james.MagicDisciplines);
        Assert.Equal([CombatTestFixture.GuardId], james.AbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_CheckedInBlackMage_HasNoInventedFallbackAbility()
    {
        CombatantSnapshot james = CombatTestFixture
            .CreateFixedBattle(CombatTestFixture.BlackMageId)
            .Snapshot.GetRequiredCombatant("party-0");

        Assert.Empty(james.DirectSkillIds);
        Assert.Empty(james.MagicDisciplines);
        Assert.Empty(james.AbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_AuthoredSources_PreserveOrderFilterLevelAndDeduplicate()
    {
        AbilityDefinition innate = CombatTestFixture.Ability("ability.test.innate");
        AbilityDefinition learned = CombatTestFixture.Ability("ability.test.learned");
        AbilityDefinition future = CombatTestFixture.Ability("ability.test.future");
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [innate.Id, learned.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            AbilityUnlocks =
            [
                new AbilityUnlockDefinition { Level = 1, AbilityId = learned.Id },
                new AbilityUnlockDefinition { Level = 2, AbilityId = future.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(innate, learned, future, actor, classDefinition));

        PartyAbilityAvailability levelOne = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
            Level = 1,
        });
        PartyAbilityAvailability levelTwo = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
            Level = 2,
        });

        Assert.Equal([innate.Id, learned.Id], levelOne.DirectSkillIds);
        Assert.Equal([innate.Id, learned.Id], levelOne.ExecutableAbilityIds);
        Assert.Equal([innate.Id, learned.Id, future.Id], levelTwo.DirectSkillIds);
        Assert.Equal([innate.Id, learned.Id, future.Id], levelTwo.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_HighExperienceWithoutLevel_DoesNotUnlockFutureAbility()
    {
        AbilityDefinition future = CombatTestFixture.Ability("ability.test.future");
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            AbilityUnlocks =
            [
                new AbilityUnlockDefinition { Level = 2, AbilityId = future.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(future, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
            Level = 1,
            Experience = 999_999,
        });

        Assert.Empty(result.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_MagicRequiresLearnedSpellAndMatchingDisciplineAccess()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        AbilityDefinition guard = CombatTestFixture.Ability("ability.test.guard");
        AbilityDefinition cure = MagicAbility("ability.test.cure", restoration.Id);
        AbilityDefinition unlearned = MagicAbility("ability.test.protect", restoration.Id);
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [guard.Id, cure.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition
                {
                    Level = 1,
                    MagicDisciplineId = restoration.Id,
                },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, guard, cure, unlearned, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(Progress(actor, classDefinition));

        Assert.Equal([guard.Id], result.DirectSkillIds);
        MagicDisciplineAvailability discipline = Assert.Single(result.MagicDisciplines);
        Assert.Equal(restoration.Id, discipline.MagicDisciplineId);
        Assert.Equal([cure.Id], discipline.SpellAbilityIds);
        Assert.Equal([guard.Id, cure.Id], result.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_LearnedMagicWithoutMatchingDiscipline_IsNotExecutable()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        MagicDisciplineDefinition wind = MagicDiscipline("magic-discipline.test.wind");
        AbilityDefinition cure = MagicAbility("ability.test.cure", restoration.Id);
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [cure.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = wind.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, wind, cure, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(Progress(actor, classDefinition));

        Assert.Empty(result.DirectSkillIds);
        Assert.Empty(Assert.Single(result.MagicDisciplines).SpellAbilityIds);
        Assert.Empty(result.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_UnlockedDisciplineWithNoLearnedSpells_IsEmptyAndValid()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition
                {
                    Level = 1,
                    MagicDisciplineId = restoration.Id,
                },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(Progress(actor, classDefinition));

        MagicDisciplineAvailability discipline = Assert.Single(result.MagicDisciplines);
        Assert.Equal(restoration.Id, discipline.MagicDisciplineId);
        Assert.Empty(discipline.SpellAbilityIds);
        Assert.Empty(result.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_MultiDisciplineSpell_AppearsInEveryContainerButOnceExecutable()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        MagicDisciplineDefinition wind = MagicDiscipline("magic-discipline.test.wind");
        AbilityDefinition hybridSpell = MagicAbility(
            "ability.test.restoration-wind",
            restoration.Id,
            wind.Id);
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [hybridSpell.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = restoration.Id },
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = wind.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, wind, hybridSpell, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(Progress(actor, classDefinition));

        Assert.Equal([restoration.Id, wind.Id],
            result.MagicDisciplines.Select(discipline => discipline.MagicDisciplineId));
        Assert.All(result.MagicDisciplines,
            discipline => Assert.Equal([hybridSpell.Id], discipline.SpellAbilityIds));
        Assert.Equal([hybridSpell.Id], result.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_DisciplineOrderAndDuplicateGrants_PreserveFirstOccurrence()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        MagicDisciplineDefinition wind = MagicDiscipline("magic-discipline.test.wind");
        AbilityDefinition spell = MagicAbility("ability.test.wind-cure", restoration.Id, wind.Id);
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [spell.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            AbilityUnlocks =
            [
                new AbilityUnlockDefinition { Level = 1, AbilityId = spell.Id },
            ],
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = wind.Id },
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = restoration.Id },
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = wind.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, wind, spell, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(Progress(actor, classDefinition));

        Assert.Equal([wind.Id, restoration.Id],
            result.MagicDisciplines.Select(discipline => discipline.MagicDisciplineId));
        Assert.Equal([spell.Id], result.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_ReturnedCollectionsRejectMutation()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        AbilityDefinition guard = CombatTestFixture.Ability("ability.test.guard");
        AbilityDefinition cure = MagicAbility("ability.test.cure", restoration.Id);
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [guard.Id, cure.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = restoration.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, guard, cure, actor, classDefinition));

        PartyAbilityAvailability result = resolver.ResolvePartyActor(Progress(actor, classDefinition));

        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)result.DirectSkillIds).Add("ability.test.cheat"));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<MagicDisciplineAvailability>)result.MagicDisciplines).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)result.ExecutableAbilityIds).Add("ability.test.cheat"));
        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)result.MagicDisciplines[0].SpellAbilityIds).Add("ability.test.cheat"));
    }

    [Fact]
    public void ResolvePartyActor_TwoResults_AreEquivalentButIndependentlyOwned()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline("magic-discipline.test.restoration");
        AbilityDefinition cure = MagicAbility("ability.test.cure", restoration.Id);
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [cure.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition { Level = 1, MagicDisciplineId = restoration.Id },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, cure, actor, classDefinition));
        ActorProgressState progress = Progress(actor, classDefinition);

        PartyAbilityAvailability first = resolver.ResolvePartyActor(progress);
        PartyAbilityAvailability second = resolver.ResolvePartyActor(progress);

        Assert.NotSame(first, second);
        Assert.NotSame(first.MagicDisciplines, second.MagicDisciplines);
        Assert.NotSame(first.MagicDisciplines[0].SpellAbilityIds,
            second.MagicDisciplines[0].SpellAbilityIds);
        Assert.Equal(first.ExecutableAbilityIds, second.ExecutableAbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_MissingReferencesAndNullCollections_AreRejected()
    {
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = ["ability.test.missing"],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            MagicDisciplineUnlocks =
            [
                new MagicDisciplineUnlockDefinition
                {
                    Level = 1,
                    MagicDisciplineId = "magic-discipline.test.missing",
                },
            ],
        };

        Assert.Throws<KeyNotFoundException>(() =>
            new AbilityAvailabilityResolver(new TestCatalog(actor, classDefinition))
                .ResolvePartyActor(Progress(actor, classDefinition)));

        actor = actor with { StartingAbilityIds = null! };
        Assert.Throws<InvalidDataException>(() =>
            new AbilityAvailabilityResolver(new TestCatalog(actor, classDefinition))
                .ResolvePartyActor(Progress(actor, classDefinition)));

        classDefinition = classDefinition with { AbilityUnlocks = null! };
        Assert.Throws<InvalidDataException>(() =>
            new AbilityAvailabilityResolver(new TestCatalog(actor with { StartingAbilityIds = [] }, classDefinition))
                .ResolvePartyActor(Progress(actor, classDefinition)));

        classDefinition = classDefinition with { AbilityUnlocks = [], MagicDisciplineUnlocks = null! };
        Assert.Throws<InvalidDataException>(() =>
            new AbilityAvailabilityResolver(new TestCatalog(actor with { StartingAbilityIds = [] }, classDefinition))
                .ResolvePartyActor(Progress(actor, classDefinition)));
    }

    [Fact]
    public void ResolvePartyActor_HandBuiltCatalogWithInvalidAbilityShape_IsRejected()
    {
        MagicDisciplineDefinition restoration = MagicDiscipline(
            "magic-discipline.test.restoration");
        AbilityDefinition invalidSkill = CombatTestFixture.Ability("ability.test.invalid-skill") with
        {
            MagicDisciplineIds = [restoration.Id],
        };
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [invalidSkill.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(restoration, invalidSkill, actor, classDefinition));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(() =>
            resolver.ResolvePartyActor(Progress(actor, classDefinition)));

        Assert.Contains(invalidSkill.Id, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePartyActor_InvalidFutureUnlock_IsRejectedBeforeRequiredLevel()
    {
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
            AbilityUnlocks =
            [
                new AbilityUnlockDefinition
                {
                    Level = 99,
                    AbilityId = "ability.test.missing",
                },
            ],
        };
        var resolver = new AbilityAvailabilityResolver(new TestCatalog(actor, classDefinition));

        Assert.Throws<KeyNotFoundException>(() =>
            resolver.ResolvePartyActor(Progress(actor, classDefinition)));
    }

    private static ActorProgressState Progress(
        ActorDefinition actor,
        ClassDefinition classDefinition,
        int level = 1) => new()
    {
        ActorId = actor.Id,
        ClassId = classDefinition.Id,
        Level = level,
    };

    private static MagicDisciplineDefinition MagicDiscipline(string id) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
    };

    private static AbilityDefinition MagicAbility(string id, params string[] magicDisciplineIds) =>
        CombatTestFixture.Ability(id) with
        {
            AbilityKindId = AbilityKindIds.Magic,
            MagicDisciplineIds = magicDisciplineIds.ToList(),
        };
}
