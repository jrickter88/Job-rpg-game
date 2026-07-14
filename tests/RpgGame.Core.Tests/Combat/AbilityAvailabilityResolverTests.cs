using RpgGame.Core.Combat;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class AbilityAvailabilityResolverTests
{
    [Fact]
    public void ResolvePartyActor_CheckedInVanguard_ReceivesEligibleClassAbility()
    {
        CombatantSnapshot james = CombatTestFixture.CreateFixedBattle()
            .Snapshot.GetRequiredCombatant("party-0");

        Assert.Equal([CombatTestFixture.GuardId], james.AbilityIds);
    }

    [Fact]
    public void ResolvePartyActor_CheckedInBlackMage_HasNoInventedFallbackAbility()
    {
        CombatantSnapshot james = CombatTestFixture
            .CreateFixedBattle(CombatTestFixture.BlackMageId)
            .Snapshot.GetRequiredCombatant("party-0");

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

        IReadOnlyList<string> levelOne = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
            Level = 1,
        });
        IReadOnlyList<string> levelTwo = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
            Level = 2,
        });

        // The duplicate learned ability keeps its first actor-authored position. The
        // level-two class grant appears afterward, still in authored unlock order.
        Assert.Equal([innate.Id, learned.Id], levelOne);
        Assert.Equal([innate.Id, learned.Id, future.Id], levelTwo);
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

        IReadOnlyList<string> result = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
            Level = 1,
            Experience = 999_999,
        });

        Assert.Empty(result);
    }

    [Fact]
    public void ResolvePartyActor_ResultCollectionRejectsMutation()
    {
        AbilityDefinition innate = CombatTestFixture.Ability("ability.test.innate");
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = [innate.Id],
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
        };
        var resolver = new AbilityAvailabilityResolver(
            new TestCatalog(innate, actor, classDefinition));

        IReadOnlyList<string> result = resolver.ResolvePartyActor(new ActorProgressState
        {
            ActorId = actor.Id,
            ClassId = classDefinition.Id,
        });

        Assert.Throws<NotSupportedException>(() =>
            ((IList<string>)result).Add("ability.test.cheat"));
    }

    [Fact]
    public void ResolvePartyActor_MissingAbilityReference_IsRejected()
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
        };
        var resolver = new AbilityAvailabilityResolver(new TestCatalog(actor, classDefinition));

        Assert.Throws<KeyNotFoundException>(() => resolver.ResolvePartyActor(
            new ActorProgressState
            {
                ActorId = actor.Id,
                ClassId = classDefinition.Id,
            }));
    }

    [Fact]
    public void ResolvePartyActor_NullActorAbilityCollection_IsRejected()
    {
        var actor = new ActorDefinition
        {
            Id = "actor.test.hero",
            DisplayNameKey = "actor.test.hero.name",
            StartingAbilityIds = null!,
        };
        var classDefinition = new ClassDefinition
        {
            Id = "class.test.job",
            DisplayNameKey = "class.test.job.name",
        };
        var resolver = new AbilityAvailabilityResolver(new TestCatalog(actor, classDefinition));

        Assert.Throws<InvalidDataException>(() => resolver.ResolvePartyActor(
            new ActorProgressState
            {
                ActorId = actor.Id,
                ClassId = classDefinition.Id,
            }));
    }

    [Fact]
    public void ResolvePartyActor_NullClassUnlockCollection_IsRejected()
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
            AbilityUnlocks = null!,
        };
        var resolver = new AbilityAvailabilityResolver(new TestCatalog(actor, classDefinition));

        Assert.Throws<InvalidDataException>(() => resolver.ResolvePartyActor(
            new ActorProgressState
            {
                ActorId = actor.Id,
                ClassId = classDefinition.Id,
            }));
    }
}
