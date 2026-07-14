using System.Diagnostics.CodeAnalysis;
using RpgGame.Core.Combat;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

/// <summary>
/// Proves the Godot-free boundary that turns authored definitions plus campaign class
/// progress into complete starting statistic maps. Small hand-built catalogs deliberately
/// bypass JSON validation for defensive runtime-boundary cases.
/// </summary>
public sealed class CombatStatisticResolverTests
{
    private const string JamesId = "actor.hero.james";
    private const string VanguardId = "class.martial.vanguard";
    private const string BlackMageId = "class.magic.black-mage";
    private const string GreenSlimeId = "enemy.forest.green-slime";

    [Fact]
    public void Constructor_NullCatalog_IsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CombatStatisticResolver(null!));
    }

    [Fact]
    public void ResolvePartyActor_CheckedInJamesWithVanguard_CombinesExpectedValues()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> result = resolver.ResolvePartyActor(
            Progress(VanguardId));

        Assert.Equal(5, result.Count);
        Assert.Equal(96, result["stat.max-hp"]);
        Assert.Equal(12, result["stat.max-mp"]);
        Assert.Equal(9, result["stat.strength"]);
        Assert.Equal(9, result["stat.defense"]);
        Assert.Equal(6, result["stat.speed"]);
    }

    [Fact]
    public void ResolvePartyActor_BlackMageClassId_DoesNotAssumeVanguard()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> result = resolver.ResolvePartyActor(
            Progress(BlackMageId));

        Assert.Equal(84, result["stat.max-hp"]);
        Assert.Equal(22, result["stat.max-mp"]);
        Assert.Equal(9, result["stat.strength"]);
        Assert.Equal(7, result["stat.defense"]);
        Assert.Equal(7, result["stat.speed"]);
    }

    [Fact]
    public void ResolvePartyActor_ChangingClass_ChangesOnlyThatClassContributions()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> vanguard = resolver.ResolvePartyActor(
            Progress(VanguardId));
        IReadOnlyDictionary<string, int> blackMage = resolver.ResolvePartyActor(
            Progress(BlackMageId));

        Assert.Equal(vanguard["stat.strength"], blackMage["stat.strength"]);
        Assert.Equal(96, vanguard["stat.max-hp"]);
        Assert.Equal(84, blackMage["stat.max-hp"]);
        Assert.Equal(12, vanguard["stat.max-mp"]);
        Assert.Equal(22, blackMage["stat.max-mp"]);
        Assert.Equal(9, vanguard["stat.defense"]);
        Assert.Equal(7, blackMage["stat.defense"]);
        Assert.Equal(6, vanguard["stat.speed"]);
        Assert.Equal(7, blackMage["stat.speed"]);
    }

    [Fact]
    public void ResolvePartyActor_OmittedActorValue_UsesDefaultBeforeClassBonus()
    {
        StatisticDefinition focus = Statistic("stat.focus", minimum: 0, maximum: 100, defaultValue: 5);
        ActorDefinition actor = Actor(baseStatistics: []);
        ClassDefinition classDefinition = JobClass(
            bonuses: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [focus.Id] = 3,
            });
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(focus, actor, classDefinition));

        IReadOnlyDictionary<string, int> result = resolver.ResolvePartyActor(
            Progress(classDefinition.Id, actor.Id));

        Assert.Equal(8, result[focus.Id]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolvePartyActor_LevelBelowOne_IsRejected(int level)
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => resolver.ResolvePartyActor(Progress(VanguardId, level: level)));
    }

    [Fact]
    public void ResolvePartyActor_LevelAboveOne_DoesNotInventScaling()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> levelOne = resolver.ResolvePartyActor(
            Progress(VanguardId, level: 1));
        IReadOnlyDictionary<string, int> levelNinetyNine = resolver.ResolvePartyActor(
            Progress(VanguardId, level: 99));

        Assert.Equal(levelOne.ToArray(), levelNinetyNine.ToArray());
    }

    [Fact]
    public void ResolvePartyActor_Experience_DoesNotInventScaling()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> noExperience = resolver.ResolvePartyActor(
            Progress(VanguardId, experience: 0));
        IReadOnlyDictionary<string, int> highExperience = resolver.ResolvePartyActor(
            Progress(VanguardId, experience: 999_999));

        Assert.Equal(noExperience.ToArray(), highExperience.ToArray());
    }

    [Fact]
    public void ResolvePartyActor_NullProgress_IsRejected()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        Assert.Throws<ArgumentNullException>(() => resolver.ResolvePartyActor(null!));
    }

    [Theory]
    [InlineData("", VanguardId)]
    [InlineData("   ", VanguardId)]
    [InlineData(JamesId, "")]
    [InlineData(JamesId, "   ")]
    public void ResolvePartyActor_BlankActorOrClassId_IsRejected(
        string actorId,
        string classId)
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        Assert.Throws<ArgumentException>(
            () => resolver.ResolvePartyActor(Progress(classId, actorId)));
    }

    [Theory]
    [InlineData("actor.hero.missing")]
    [InlineData(GreenSlimeId)]
    public void ResolvePartyActor_MissingOrWrongCategoryActor_FailsTypedLookup(string actorId)
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => resolver.ResolvePartyActor(Progress(VanguardId, actorId)));

        Assert.Contains(actorId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ActorDefinition), exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("class.martial.missing")]
    [InlineData(JamesId)]
    public void ResolvePartyActor_MissingOrWrongCategoryClass_FailsTypedLookup(string classId)
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => resolver.ResolvePartyActor(Progress(classId)));

        Assert.Contains(classId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ClassDefinition), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveEnemy_CheckedInGreenSlime_UsesAuthoredValuesAndDefault()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> result = resolver.ResolveEnemy(GreenSlimeId);

        Assert.Equal(5, result.Count);
        Assert.Equal(22, result["stat.max-hp"]);
        Assert.Equal(0, result["stat.max-mp"]);
        Assert.Equal(3, result["stat.strength"]);
        Assert.Equal(2, result["stat.defense"]);
        Assert.Equal(2, result["stat.speed"]);
    }

    [Fact]
    public void ResolveEnemy_AuthoredLevel_DoesNotInventScaling()
    {
        StatisticDefinition power = Statistic("stat.power", minimum: 0, maximum: 100, defaultValue: 4);
        EnemyDefinition lowLevel = Enemy(
            "enemy.test.low-level",
            level: 1,
            statistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = 9,
            });
        EnemyDefinition highLevel = Enemy(
            "enemy.test.high-level",
            level: 99,
            statistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = 9,
            });
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(power, lowLevel, highLevel));

        Assert.Equal(
            resolver.ResolveEnemy(lowLevel.Id).ToArray(),
            resolver.ResolveEnemy(highLevel.Id).ToArray());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveEnemy_NullOrBlankId_IsRejected(string? enemyId)
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        Assert.ThrowsAny<ArgumentException>(() => resolver.ResolveEnemy(enemyId!));
    }

    [Theory]
    [InlineData("enemy.forest.missing")]
    [InlineData(JamesId)]
    public void ResolveEnemy_MissingOrWrongCategoryId_FailsTypedLookup(string enemyId)
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => resolver.ResolveEnemy(enemyId));

        Assert.Contains(enemyId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(EnemyDefinition), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePartyActor_DerivedValueAboveMaximum_ReportsFullContext()
    {
        StatisticDefinition power = Statistic("stat.power", minimum: 0, maximum: 100, defaultValue: 0);
        ActorDefinition actor = Actor(
            baseStatistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = 90,
            });
        ClassDefinition classDefinition = JobClass(
            bonuses: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = 20,
            });
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(power, actor, classDefinition));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => resolver.ResolvePartyActor(Progress(classDefinition.Id, actor.Id)));

        AssertRangeMessage(exception, actor.Id, classDefinition.Id, power.Id, "110", "0..100");
    }

    [Fact]
    public void ResolvePartyActor_DerivedValueBelowMinimum_ReportsFullContext()
    {
        StatisticDefinition power = Statistic("stat.power", minimum: -10, maximum: 100, defaultValue: 0);
        ActorDefinition actor = Actor(
            baseStatistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = -10,
            });
        ClassDefinition classDefinition = JobClass(
            bonuses: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = -5,
            });
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(power, actor, classDefinition));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => resolver.ResolvePartyActor(Progress(classDefinition.Id, actor.Id)));

        AssertRangeMessage(exception, actor.Id, classDefinition.Id, power.Id, "-15", "-10..100");
    }

    [Fact]
    public void ResolveEnemy_ValueOutsideRange_IsRejectedDefensively()
    {
        StatisticDefinition power = Statistic("stat.power", minimum: 0, maximum: 10, defaultValue: 0);
        EnemyDefinition enemy = Enemy(
            statistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [power.Id] = 11,
            });
        var resolver = new CombatStatisticResolver(new FixtureCatalog(power, enemy));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => resolver.ResolveEnemy(enemy.Id));

        Assert.Contains(enemy.Id, exception.Message, StringComparison.Ordinal);
        Assert.Contains(power.Id, exception.Message, StringComparison.Ordinal);
        Assert.Contains("11", exception.Message, StringComparison.Ordinal);
        Assert.Contains("0..10", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePartyActor_UnknownActorStatistic_IsRejectedDefensively()
    {
        StatisticDefinition known = Statistic("stat.known");
        ActorDefinition actor = Actor(
            baseStatistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["stat.unknown"] = 1,
            });
        ClassDefinition classDefinition = JobClass();
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(known, actor, classDefinition));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => resolver.ResolvePartyActor(Progress(classDefinition.Id, actor.Id)));

        Assert.Contains(actor.Id, exception.Message, StringComparison.Ordinal);
        Assert.Contains("stat.unknown", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePartyActor_UnknownClassStatistic_IsRejectedDefensively()
    {
        StatisticDefinition known = Statistic("stat.known");
        ActorDefinition actor = Actor();
        ClassDefinition classDefinition = JobClass(
            bonuses: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["stat.unknown"] = 1,
            });
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(known, actor, classDefinition));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => resolver.ResolvePartyActor(Progress(classDefinition.Id, actor.Id)));

        Assert.Contains(classDefinition.Id, exception.Message, StringComparison.Ordinal);
        Assert.Contains("stat.unknown", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveEnemy_UnknownStatistic_IsRejectedDefensively()
    {
        StatisticDefinition known = Statistic("stat.known");
        EnemyDefinition enemy = Enemy(
            statistics: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["stat.unknown"] = 1,
            });
        var resolver = new CombatStatisticResolver(new FixtureCatalog(known, enemy));

        InvalidDataException exception = Assert.Throws<InvalidDataException>(
            () => resolver.ResolveEnemy(enemy.Id));

        Assert.Contains(enemy.Id, exception.Message, StringComparison.Ordinal);
        Assert.Contains("stat.unknown", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolvePartyActor_UnorderedCatalog_ReturnsEveryStatisticInOrdinalOrder()
    {
        StatisticDefinition zeta = Statistic("stat.zeta", defaultValue: 3);
        StatisticDefinition alpha = Statistic("stat.alpha", defaultValue: 1);
        StatisticDefinition middle = Statistic("stat.middle", defaultValue: 2);
        ActorDefinition actor = Actor();
        ClassDefinition classDefinition = JobClass();
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(zeta, actor, middle, classDefinition, alpha));

        IReadOnlyDictionary<string, int> result = resolver.ResolvePartyActor(
            Progress(classDefinition.Id, actor.Id));

        Assert.Equal(3, result.Count);
        Assert.Equal(["stat.alpha", "stat.middle", "stat.zeta"], result.Keys);
        Assert.Equal([1, 2, 3], result.Values);
    }

    [Fact]
    public void ResolveEnemy_UnorderedCatalog_ReturnsEveryStatisticInOrdinalOrder()
    {
        StatisticDefinition zeta = Statistic("stat.zeta", defaultValue: 3);
        StatisticDefinition alpha = Statistic("stat.alpha", defaultValue: 1);
        StatisticDefinition middle = Statistic("stat.middle", defaultValue: 2);
        EnemyDefinition enemy = Enemy();
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(zeta, enemy, middle, alpha));

        IReadOnlyDictionary<string, int> result = resolver.ResolveEnemy(enemy.Id);

        Assert.Equal(3, result.Count);
        Assert.Equal(["stat.alpha", "stat.middle", "stat.zeta"], result.Keys);
        Assert.Equal([1, 2, 3], result.Values);
    }

    [Fact]
    public void ResolvePartyActor_RepeatedResolution_IsEquivalentAndIndependentlyOwned()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> first = resolver.ResolvePartyActor(Progress(VanguardId));
        IReadOnlyDictionary<string, int> second = resolver.ResolvePartyActor(Progress(VanguardId));

        Assert.NotSame(first, second);
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public void ResolveEnemy_RepeatedResolution_IsEquivalentAndIndependentlyOwned()
    {
        var resolver = new CombatStatisticResolver(TestContent.LoadCatalog());

        IReadOnlyDictionary<string, int> first = resolver.ResolveEnemy(GreenSlimeId);
        IReadOnlyDictionary<string, int> second = resolver.ResolveEnemy(GreenSlimeId);

        Assert.NotSame(first, second);
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public void ResolvedMap_CallerMutationIsBlockedAndCannotAffectContentOrOtherResults()
    {
        StatisticDefinition power = Statistic("stat.power", defaultValue: 1);
        var actorValues = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [power.Id] = 7,
        };
        ActorDefinition actor = Actor(baseStatistics: actorValues);
        ClassDefinition classDefinition = JobClass();
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(power, actor, classDefinition));

        IReadOnlyDictionary<string, int> first = resolver.ResolvePartyActor(
            Progress(classDefinition.Id, actor.Id));
        IReadOnlyDictionary<string, int> second = resolver.ResolvePartyActor(
            Progress(classDefinition.Id, actor.Id));
        IDictionary<string, int> mutationView = Assert.IsAssignableFrom<IDictionary<string, int>>(first);

        Assert.NotSame(actorValues, first);
        Assert.Throws<NotSupportedException>(() => mutationView[power.Id] = 999);
        Assert.Equal(7, actorValues[power.Id]);
        Assert.Equal(7, second[power.Id]);
        Assert.Equal(
            7,
            resolver.ResolvePartyActor(Progress(classDefinition.Id, actor.Id))[power.Id]);
    }

    [Fact]
    public void Resolve_CustomStatistic_AutomaticallyFlowsThroughPartyClassAndEnemyDefaults()
    {
        StatisticDefinition magicDefense = Statistic(
            "stat.magic-defense",
            minimum: 0,
            maximum: 100,
            defaultValue: 4);
        ActorDefinition actor = Actor();
        ClassDefinition classDefinition = JobClass(
            bonuses: new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [magicDefense.Id] = 3,
            });
        EnemyDefinition enemy = Enemy();
        var resolver = new CombatStatisticResolver(
            new FixtureCatalog(enemy, classDefinition, magicDefense, actor));

        IReadOnlyDictionary<string, int> party = resolver.ResolvePartyActor(
            Progress(classDefinition.Id, actor.Id));
        IReadOnlyDictionary<string, int> enemyStatistics = resolver.ResolveEnemy(enemy.Id);

        Assert.Equal([magicDefense.Id], party.Keys);
        Assert.Equal(7, party[magicDefense.Id]);
        Assert.Equal([magicDefense.Id], enemyStatistics.Keys);
        Assert.Equal(4, enemyStatistics[magicDefense.Id]);
    }

    private static ActorProgressState Progress(
        string classId,
        string actorId = JamesId,
        int level = 1,
        int experience = 0) => new()
        {
            ActorId = actorId,
            ClassId = classId,
            Level = level,
            Experience = experience,
        };

    private static StatisticDefinition Statistic(
        string id,
        int minimum = 0,
        int maximum = 999,
        int defaultValue = 0) => new()
        {
            Id = id,
            DisplayNameKey = $"{id}.name",
            MinimumValue = minimum,
            MaximumValue = maximum,
            DefaultValue = defaultValue,
        };

    private static ActorDefinition Actor(
        string id = "actor.test.hero",
        Dictionary<string, int>? baseStatistics = null) => new()
        {
            Id = id,
            DisplayNameKey = $"{id}.name",
            BaseStatistics = baseStatistics ?? new Dictionary<string, int>(StringComparer.Ordinal),
        };

    private static ClassDefinition JobClass(
        string id = "class.test.job",
        Dictionary<string, int>? bonuses = null) => new()
        {
            Id = id,
            DisplayNameKey = $"{id}.name",
            BaseStatisticBonuses = bonuses ?? new Dictionary<string, int>(StringComparer.Ordinal),
        };

    private static EnemyDefinition Enemy(
        string id = "enemy.test.creature",
        int level = 1,
        Dictionary<string, int>? statistics = null) => new()
        {
            Id = id,
            DisplayNameKey = $"{id}.name",
            Level = level,
            Statistics = statistics ?? new Dictionary<string, int>(StringComparer.Ordinal),
        };

    private static void AssertRangeMessage(
        InvalidDataException exception,
        string actorId,
        string classId,
        string statisticId,
        string calculatedValue,
        string range)
    {
        Assert.Contains(actorId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(classId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(statisticId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(calculatedValue, exception.Message, StringComparison.Ordinal);
        Assert.Contains(range, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Minimal test catalog that can intentionally contain data normal production loading
    /// would reject. Input order is preserved so resolver ordering is tested independently.
    /// </summary>
    private sealed class FixtureCatalog : IContentCatalog
    {
        private readonly ContentDefinition[] _definitions;

        public FixtureCatalog(params ContentDefinition[] definitions)
        {
            _definitions = [.. definitions];
        }

        public int Count => _definitions.Length;

        public IReadOnlyCollection<TDefinition> GetAll<TDefinition>()
            where TDefinition : ContentDefinition =>
            _definitions.OfType<TDefinition>().ToArray();

        public TDefinition GetRequired<TDefinition>(string id)
            where TDefinition : ContentDefinition
        {
            if (TryGet<TDefinition>(id, out TDefinition? definition))
            {
                return definition;
            }

            throw new KeyNotFoundException(
                $"Content definition '{id}' was not found as {typeof(TDefinition).Name}.");
        }

        public bool TryGet<TDefinition>(
            string id,
            [NotNullWhen(true)] out TDefinition? definition)
            where TDefinition : ContentDefinition
        {
            definition = _definitions
                .OfType<TDefinition>()
                .FirstOrDefault(candidate => string.Equals(candidate.Id, id, StringComparison.Ordinal));
            return definition is not null;
        }
    }
}
