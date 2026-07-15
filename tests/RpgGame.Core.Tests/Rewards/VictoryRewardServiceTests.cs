using RpgGame.Core.Combat;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Inventory;
using RpgGame.Core.Loot;
using RpgGame.Core.Rewards;
using RpgGame.Core.State;
using RpgGame.Core.Tests.Combat;
using Xunit;

namespace RpgGame.Core.Tests.Rewards;

public sealed class VictoryRewardServiceTests
{
    [Fact]
    public void Apply_CallsResolverExactlyOnceAndPreservesOrderedRawAwards()
    {
        ItemDefinition potion = Item("item.test.potion", 99);
        ItemDefinition material = Item("item.test.material", 99);
        LootAward[] awards =
        [
            Award(potion.Id, 1, "enemy.test.first"),
            Award(potion.Id, 2, "enemy.test.second"),
            Award(material.Id, 3, "enemy.test.second"),
        ];
        var resolver = new RecordingLootResolver(awards);
        (VictoryRewardService service, _) = CreateService(resolver, potion, material);

        VictoryRewardResult result = service.Apply(
            ["enemy.test.first", "enemy.test.second"],
            new UnusedRandomSource());

        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(
            ["enemy.test.first", "enemy.test.second"],
            resolver.LastDefeatedEnemyDefinitionIds);
        Assert.Equal(awards, result.Awards);
        Assert.Equal([1, 2, 3], result.Awards.Select(award => award.Quantity));
    }

    [Fact]
    public void Apply_DuplicateAwardsRemainRawAndAggregateInFirstAwardOrder()
    {
        ItemDefinition potion = Item("item.test.potion", 99);
        ItemDefinition material = Item("item.test.material", 99);
        LootAward[] awards =
        [
            Award(potion.Id, 1, "enemy.test.first"),
            Award(material.Id, 2, "enemy.test.second"),
            Award(potion.Id, 3, "enemy.test.second"),
        ];
        (VictoryRewardService service, _) = CreateService(
            new RecordingLootResolver(awards),
            potion,
            material);

        VictoryRewardResult result = service.Apply(
            ["enemy.test.first", "enemy.test.second"],
            new UnusedRandomSource());

        Assert.Equal(3, result.Awards.Count);
        Assert.Equal(
        [
            new ItemRewardSummary(potion.Id, 4),
            new ItemRewardSummary(material.Id, 2),
        ],
        result.ItemSummaries);
    }

    [Fact]
    public void Apply_EmptyLoot_DoesNotPublishInventory()
    {
        var resolver = new RecordingLootResolver([]);
        (VictoryRewardService service, GameSession session) = CreateService(resolver);
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        VictoryRewardResult result = service.Apply([], new UnusedRandomSource());

        Assert.Equal(1, resolver.CallCount);
        Assert.Empty(result.Awards);
        Assert.Empty(result.ItemSummaries);
        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Apply_ValidAwards_UpdateInventoryOnce()
    {
        ItemDefinition potion = Item("item.test.potion", 99);
        ItemDefinition material = Item("item.test.material", 99);
        var resolver = new RecordingLootResolver(
        [
            Award(potion.Id, 2),
            Award(material.Id, 1),
            Award(potion.Id, 3),
        ]);
        (VictoryRewardService service, GameSession session) = CreateService(
            resolver,
            potion,
            material);
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        _ = service.Apply(["enemy.test"], new UnusedRandomSource());

        Assert.Equal(5, session.Current.Inventory[potion.Id]);
        Assert.Equal(1, session.Current.Inventory[material.Id]);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void Apply_StackOverflow_LeavesInventoryUnchanged()
    {
        ItemDefinition potion = Item("item.test.potion", 3);
        var resolver = new RecordingLootResolver([Award(potion.Id, 2)]);
        (VictoryRewardService service, GameSession session) = CreateService(
            resolver,
            [potion],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [potion.Id] = 2,
            });
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<InvalidOperationException>(() =>
            service.Apply(["enemy.test"], new UnusedRandomSource()));

        Assert.Same(previous, session.Current);
        Assert.Equal(2, session.Current.Inventory[potion.Id]);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void Apply_InvalidAwardOrSummaryOverflow_FailsBeforeInventoryPublication()
    {
        ItemDefinition item = Item("item.test.unbounded", int.MaxValue);
        var invalidIdResolver = new RecordingLootResolver([Award("item.test.missing", 1)]);
        var invalidQuantityResolver = new RecordingLootResolver([Award(item.Id, 0)]);
        var overflowResolver = new RecordingLootResolver(
        [
            Award(item.Id, int.MaxValue),
            Award(item.Id, 1),
        ]);

        AssertAtomicFailure(invalidIdResolver, item);
        AssertAtomicFailure(invalidQuantityResolver, item);
        AssertAtomicFailure(overflowResolver, item);
    }

    [Fact]
    public void Apply_ReturnedCollectionsRejectMutation()
    {
        ItemDefinition item = Item("item.test.immutable", 99);
        LootAward award = Award(item.Id, 1);
        (VictoryRewardService service, _) = CreateService(
            new RecordingLootResolver([award]),
            item);

        VictoryRewardResult result = service.Apply(
            ["enemy.test"],
            new UnusedRandomSource());
        var awards = Assert.IsAssignableFrom<IList<LootAward>>(result.Awards);
        var summaries = Assert.IsAssignableFrom<IList<ItemRewardSummary>>(
            result.ItemSummaries);

        Assert.Throws<NotSupportedException>(() => awards[0] = Award(item.Id, 2));
        Assert.Throws<NotSupportedException>(() =>
            summaries[0] = new ItemRewardSummary(item.Id, 2));
    }

    [Fact]
    public void Apply_EquivalentScriptedRandomSequences_ProduceEquivalentAppliedResults()
    {
        ItemDefinition item = Item("item.test.deterministic", 99);
        var enemy = new EnemyDefinition
        {
            Id = "enemy.test.deterministic",
            DisplayNameKey = "enemy.test.deterministic.name",
            LootTableId = "loot-table.test.deterministic",
        };
        var table = new LootTableDefinition
        {
            Id = enemy.LootTableId,
            Entries =
            [
                new LootEntryDefinition
                {
                    ItemId = item.Id,
                    Chance = 0.5m,
                    MinQuantity = 1,
                    MaxQuantity = 3,
                },
            ],
        };
        var content = new TestCatalog(item, enemy, table);
        (VictoryRewardService firstService, GameSession firstSession) = CreateService(
            new LootResolver(content),
            content);
        (VictoryRewardService secondService, GameSession secondSession) = CreateService(
            new LootResolver(content),
            content);

        VictoryRewardResult first = firstService.Apply(
            [enemy.Id],
            new ScriptedRandomSource(125_000, 3));
        VictoryRewardResult second = secondService.Apply(
            [enemy.Id],
            new ScriptedRandomSource(125_000, 3));

        Assert.Equal(first.Awards, second.Awards);
        Assert.Equal(first.ItemSummaries, second.ItemSummaries);
        Assert.Equal(firstSession.Current.Inventory, secondSession.Current.Inventory);
        Assert.Equal(3, secondSession.Current.Inventory[item.Id]);
    }

    private static void AssertAtomicFailure(
        ILootResolver resolver,
        params ItemDefinition[] items)
    {
        (VictoryRewardService service, GameSession session) = CreateService(resolver, items);
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.ThrowsAny<Exception>(() =>
            service.Apply(["enemy.test"], new UnusedRandomSource()));
        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    private static (VictoryRewardService Service, GameSession Session) CreateService(
        ILootResolver resolver,
        params ItemDefinition[] items) => CreateService(
            resolver,
            new TestCatalog(items),
            new Dictionary<string, int>(StringComparer.Ordinal));

    private static (VictoryRewardService Service, GameSession Session) CreateService(
        ILootResolver resolver,
        IReadOnlyList<ItemDefinition> items,
        Dictionary<string, int> inventory) => CreateService(
            resolver,
            new TestCatalog(items.Cast<ContentDefinition>().ToArray()),
            inventory);

    private static (VictoryRewardService Service, GameSession Session) CreateService(
        ILootResolver resolver,
        IContentCatalog content,
        Dictionary<string, int>? inventory = null)
    {
        var session = new GameSession();
        session.ReplaceState(new GameState
        {
            SaveId = "victory-reward-test",
            Inventory = inventory ?? new Dictionary<string, int>(StringComparer.Ordinal),
        });
        var service = new VictoryRewardService(
            resolver,
            new InventoryService(content, session));
        return (service, session);
    }

    private static ItemDefinition Item(string id, int maxStack) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
        MaxStack = maxStack,
    };

    private static LootAward Award(
        string itemId,
        int quantity,
        string enemyId = "enemy.test") =>
        new(enemyId, "loot-table.test", itemId, quantity);

    private sealed class RecordingLootResolver : ILootResolver
    {
        private readonly LootResolution _resolution;

        public RecordingLootResolver(IReadOnlyList<LootAward> awards)
        {
            _resolution = new LootResolution(awards);
        }

        public int CallCount { get; private set; }

        public IReadOnlyList<string>? LastDefeatedEnemyDefinitionIds { get; private set; }

        public LootResolution Resolve(
            IReadOnlyList<string> defeatedEnemyDefinitionIds,
            IRandomSource random)
        {
            CallCount++;
            LastDefeatedEnemyDefinitionIds = defeatedEnemyDefinitionIds.ToArray();
            return _resolution;
        }
    }

    private sealed class UnusedRandomSource : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) =>
            throw new InvalidOperationException("The recording resolver must not use randomness.");
    }

    private sealed class ScriptedRandomSource : IRandomSource
    {
        private readonly Queue<int> _values;

        public ScriptedRandomSource(params int[] values)
        {
            _values = new Queue<int>(values);
        }

        public int Next(int minInclusive, int maxExclusive)
        {
            int value = _values.Dequeue();
            Assert.InRange(value, minInclusive, maxExclusive - 1);
            return value;
        }
    }
}
