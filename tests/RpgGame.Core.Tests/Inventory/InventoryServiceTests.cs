using System.Diagnostics.CodeAnalysis;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using RpgGame.Core.Inventory;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Inventory;

/// <summary>Tests content-aware, atomic mutations of persistent item stacks.</summary>
public sealed class InventoryServiceTests
{
    private const string PotionId = "item.consumable.potion";
    private const string SwordId = "item.equipment.iron-sword";

    [Fact]
    public void GetQuantity_AbsentKnownItem_ReturnsZero()
    {
        (InventoryService inventory, _) = CreateService();

        Assert.Equal(0, inventory.GetQuantity(PotionId));
    }

    [Fact]
    public void AddItems_EmptyBatch_IsANoOp()
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        inventory.AddItems([]);

        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void AddItems_MultipleItems_PublishesOneReplacement()
    {
        (InventoryService inventory, GameSession session) = CreateService();
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        inventory.AddItems(
        [
            new InventoryAddition(PotionId, 2),
            new InventoryAddition(SwordId, 1),
        ]);

        Assert.Equal(2, session.Current.Inventory[PotionId]);
        Assert.Equal(1, session.Current.Inventory[SwordId]);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void AddItems_DuplicatesAggregateInFirstOccurrenceOrder()
    {
        (InventoryService inventory, GameSession session) = CreateService();

        inventory.AddItems(
        [
            new InventoryAddition(PotionId, 1),
            new InventoryAddition(SwordId, 1),
            new InventoryAddition(PotionId, 2),
        ]);

        Assert.Equal(3, session.Current.Inventory[PotionId]);
        Assert.Equal(1, session.Current.Inventory[SwordId]);
        Assert.Equal([PotionId, SwordId], session.Current.Inventory.Keys);
    }

    [Fact]
    public void AddItems_ExactlyToMaximumStack_Succeeds()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 96)));

        inventory.AddItems(
        [
            new InventoryAddition(PotionId, 1),
            new InventoryAddition(PotionId, 2),
        ]);

        Assert.Equal(99, session.Current.Inventory[PotionId]);
    }

    [Fact]
    public void AddItems_ExceedingMaximumStack_FailsWithoutMutationOrNotification()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 98)));
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<InvalidOperationException>(() => inventory.AddItems(
        [
            new InventoryAddition(PotionId, 1),
            new InventoryAddition(PotionId, 1),
        ]));

        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void AddItems_CheckedAggregateOverflow_FailsWithoutMutation()
    {
        const string itemId = "item.test.batch-overflow";
        var content = new TestCatalog(Item(itemId, int.MaxValue));
        (InventoryService inventory, GameSession session) = CreateService(content: content);
        GameState previous = session.Current;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            inventory.AddItems(
            [
                new InventoryAddition(itemId, int.MaxValue),
                new InventoryAddition(itemId, 1),
            ]));

        Assert.IsType<OverflowException>(exception.InnerException);
        Assert.Same(previous, session.Current);
    }

    [Fact]
    public void AddItems_InvalidLaterAddition_PreventsEarlierPublication()
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<KeyNotFoundException>(() => inventory.AddItems(
        [
            new InventoryAddition(PotionId, 2),
            new InventoryAddition("item.test.missing", 1),
        ]));

        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void AddItems_NullCollectionOrEntry_IsRejected()
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;

        Assert.Throws<ArgumentNullException>(() => inventory.AddItems(null!));
        Assert.Throws<ArgumentException>(() => inventory.AddItems([null!]));
        Assert.Same(previous, session.Current);
    }

    [Fact]
    public void AddItem_NewItem_CreatesOneStackWithoutMutatingPreviousState()
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;

        inventory.AddItem(PotionId, 2);

        Assert.Empty(previous.Inventory);
        Assert.Single(session.Current.Inventory);
        Assert.Equal(2, session.Current.Inventory[PotionId]);
        Assert.Same(StringComparer.Ordinal, session.Current.Inventory.Comparer);
    }

    [Fact]
    public void AddItem_ExistingItem_IncreasesSingleStack()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 2)));

        inventory.AddItem(PotionId, 3);

        Assert.Single(session.Current.Inventory);
        Assert.Equal(5, session.Current.Inventory[PotionId]);
    }

    [Fact]
    public void AddItem_ExactlyToMaximumStack_Succeeds()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 98)));

        inventory.AddItem(PotionId, 1);

        Assert.Equal(99, session.Current.Inventory[PotionId]);
    }

    [Fact]
    public void AddItem_BeyondMaximumStack_ReportsContextAndDoesNotPublish()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 98)));
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => inventory.AddItem(PotionId, 2));

        Assert.Contains(PotionId, exception.Message, StringComparison.Ordinal);
        Assert.Contains("current quantity is 98", exception.Message, StringComparison.Ordinal);
        Assert.Contains("requested quantity is 2", exception.Message, StringComparison.Ordinal);
        Assert.Contains("maximum stack is 99", exception.Message, StringComparison.Ordinal);
        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void AddItem_IntegerOverflow_ReportsContextAndDoesNotPublish()
    {
        const string itemId = "item.test.unbounded-stack";
        var content = new TestCatalog(Item(itemId, int.MaxValue));
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((itemId, int.MaxValue)),
            content);
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => inventory.AddItem(itemId, 1));

        Assert.IsType<OverflowException>(exception.InnerException);
        Assert.Contains(itemId, exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"current quantity is {int.MaxValue}",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Contains("requested quantity is 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains(
            $"maximum stack is {int.MaxValue}",
            exception.Message,
            StringComparison.Ordinal);
        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_NonpositiveQuantity_IsRejected(int quantity)
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => inventory.AddItem(PotionId, quantity));

        Assert.Same(previous, session.Current);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddItem_BlankItemId_IsRejected(string itemId)
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;

        Assert.Throws<ArgumentException>(() => inventory.AddItem(itemId, 1));

        Assert.Same(previous, session.Current);
    }

    [Theory]
    [InlineData("item.test.missing")]
    [InlineData("enemy.forest.green-slime")]
    public void AddItem_UnknownOrWrongCategoryItem_IsRejected(string itemId)
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<KeyNotFoundException>(() => inventory.AddItem(itemId, 1));

        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void RemoveItem_PartialStack_ReducesQuantity()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 5)));

        inventory.RemoveItem(PotionId, 2);

        Assert.Equal(3, session.Current.Inventory[PotionId]);
    }

    [Fact]
    public void RemoveItem_EntireStack_DeletesEntry()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 5)));

        inventory.RemoveItem(PotionId, 5);

        Assert.Empty(session.Current.Inventory);
    }

    [Fact]
    public void RemoveItem_MoreThanOwned_IsRejectedWithoutPublishing()
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 2)));
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<InvalidOperationException>(() => inventory.RemoveItem(PotionId, 3));

        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void RemoveItem_AbsentItem_IsRejected()
    {
        (InventoryService inventory, GameSession session) = CreateService();
        GameState previous = session.Current;

        Assert.Throws<InvalidOperationException>(() => inventory.RemoveItem(PotionId, 1));

        Assert.Same(previous, session.Current);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RemoveItem_NonpositiveQuantity_IsRejected(int quantity)
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((PotionId, 2)));
        GameState previous = session.Current;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => inventory.RemoveItem(PotionId, quantity));

        Assert.Same(previous, session.Current);
    }

    [Theory]
    [InlineData(" ", 1)]
    [InlineData("item.test.missing", 1)]
    [InlineData("enemy.forest.green-slime", 1)]
    [InlineData(PotionId, 0)]
    [InlineData(PotionId, 100)]
    public void Mutation_InvalidExistingInventory_IsRejectedDefensively(
        string existingItemId,
        int existingQuantity)
    {
        (InventoryService inventory, GameSession session) = CreateService(
            Inventory((existingItemId, existingQuantity)));
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<InvalidDataException>(() => inventory.AddItem(SwordId, 1));

        Assert.Same(previous, session.Current);
        Assert.Equal(0, notifications);
    }

    [Fact]
    public void AddItem_Success_PreservesUnrelatedStateAndNotifiesOnce()
    {
        MapLocationState location = new()
        {
            MapId = "map.prologue.test-room",
            X = 4,
            Y = 7,
            Facing = "east",
        };
        var party = new List<string> { "actor.hero.james" };
        var progress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal)
        {
            ["actor.hero.james"] = new ActorProgressState
            {
                ActorId = "actor.hero.james",
                ClassId = "class.martial.knight",
                Level = 3,
                Experience = 12,
            },
        };
        var flags = new Dictionary<string, bool>(StringComparer.Ordinal)
        {
            ["flag.test-room.npc-spoken-to"] = true,
        };
        var state = new GameState
        {
            SaveId = "preservation-test",
            Location = location,
            ActivePartyActorIds = party,
            ActorProgress = progress,
            Inventory = Inventory((SwordId, 1)),
            EventFlags = flags,
        };
        (InventoryService inventory, GameSession session) = CreateService(state: state);
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        inventory.AddItem(PotionId, 2);

        Assert.NotSame(state, session.Current);
        Assert.Equal(state.SaveId, session.Current.SaveId);
        Assert.Same(location, session.Current.Location);
        Assert.Same(party, session.Current.ActivePartyActorIds);
        Assert.Same(progress, session.Current.ActorProgress);
        Assert.Same(flags, session.Current.EventFlags);
        Assert.Equal(1, state.Inventory[SwordId]);
        Assert.False(state.Inventory.ContainsKey(PotionId));
        Assert.Equal(1, session.Current.Inventory[SwordId]);
        Assert.Equal(2, session.Current.Inventory[PotionId]);
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void AddItem_ModOwnedItemPresentInCatalog_IsStored()
    {
        const string modItemId = "item.example.inventory.tonic";
        var modSource = new InMemoryContentSource(
            "mod.example.inventory",
            new ContentDocument("items/tonic.json", $$"""
                {
                  "schemaVersion": 1,
                  "id": "{{modItemId}}",
                  "displayNameKey": "item.example.inventory.tonic.name",
                  "descriptionKey": "item.example.inventory.tonic.description",
                  "buyPrice": 0,
                  "sellPrice": 0,
                  "maxStack": 7
                }
                """));
        ContentLoadResult result = new JsonContentLoader().Load(
        [
            new DirectoryContentSource(
                Path.Combine(TestContent.RepositoryRoot, "game", "content")),
            modSource,
        ]);
        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        (InventoryService inventory, GameSession session) = CreateService(
            content: result.Catalog!);

        inventory.AddItem(modItemId, 3);

        Assert.Equal(3, session.Current.Inventory[modItemId]);
    }

    private static (InventoryService Inventory, GameSession Session) CreateService(
        Dictionary<string, int>? inventory = null,
        IContentCatalog? content = null,
        GameState? state = null)
    {
        var session = new GameSession();
        session.ReplaceState(state ?? new GameState
        {
            SaveId = "inventory-service-test",
            Inventory = inventory ?? Inventory(),
        });

        return (new InventoryService(content ?? TestContent.LoadCatalog(), session), session);
    }

    private static Dictionary<string, int> Inventory(
        params (string ItemId, int Quantity)[] entries)
    {
        var inventory = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string itemId, int quantity) in entries)
        {
            inventory.Add(itemId, quantity);
        }

        return inventory;
    }

    private static ItemDefinition Item(string id, int maxStack) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
        MaxStack = maxStack,
    };


    private sealed class TestCatalog : IContentCatalog
    {
        private readonly Dictionary<Type, Dictionary<string, ContentDefinition>> _definitions;

        public TestCatalog(params ContentDefinition[] definitions)
        {
            _definitions = definitions
                .GroupBy(definition => definition.GetType())
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(
                        definition => definition.Id,
                        definition => definition,
                        StringComparer.Ordinal));
            Count = definitions.Length;
        }

        public int Count { get; }

        public IReadOnlyCollection<TDefinition> GetAll<TDefinition>()
            where TDefinition : ContentDefinition
        {
            if (!_definitions.TryGetValue(typeof(TDefinition), out var definitions))
            {
                return Array.Empty<TDefinition>();
            }

            return definitions.Values
                .Cast<TDefinition>()
                .OrderBy(definition => definition.Id, StringComparer.Ordinal)
                .ToArray();
        }

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
            if (_definitions.TryGetValue(typeof(TDefinition), out var definitions)
                && definitions.TryGetValue(id, out ContentDefinition? untyped))
            {
                definition = (TDefinition)untyped;
                return true;
            }

            definition = null;
            return false;
        }
    }

    private sealed class InMemoryContentSource : IContentSource
    {
        private readonly IReadOnlyList<ContentDocument> _documents;

        public InMemoryContentSource(string sourceId, params ContentDocument[] documents)
        {
            SourceId = sourceId;
            _documents = documents;
        }

        public string SourceId { get; }

        public IReadOnlyList<ContentDocument> ReadAll() => _documents;
    }
}
