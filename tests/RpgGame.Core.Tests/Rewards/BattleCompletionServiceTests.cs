using RpgGame.Core.Combat;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Inventory;
using RpgGame.Core.Loot;
using RpgGame.Core.Rewards;
using RpgGame.Core.State;
using RpgGame.Core.Tests.Combat;
using Xunit;

namespace RpgGame.Core.Tests.Rewards;

public sealed class BattleCompletionServiceTests
{
    private const string ClearanceFlagId = "flag.test.rewarded";
    private const string ItemId = "item.test.reward";

    [Fact]
    public void Complete_PartyVictoryAppliesRewardsThenClearance()
    {
        (BattleCompletionService service, GameSession session, RecordingLootResolver resolver) =
            CreateService([Award(quantity: 2)]);
        var publishedStates = new List<(int Quantity, bool Cleared)>();
        session.StateChanged += (_, _) => publishedStates.Add((
            session.Current.Inventory.GetValueOrDefault(ItemId),
            session.GetEventFlag(ClearanceFlagId)));

        BattleCompletionResult result = service.Complete(
            Request(BattleOutcome.PartyVictory),
            ClearanceFlagId,
            new UnusedRandomSource());

        Assert.Equal(BattleCompletionDisposition.VictoryRewardsApplied, result.Disposition);
        Assert.NotNull(result.Rewards);
        Assert.Equal(1, resolver.CallCount);
        Assert.Equal(2, session.Current.Inventory[ItemId]);
        Assert.True(session.GetEventFlag(ClearanceFlagId));
        Assert.Equal([(2, false), (2, true)], publishedStates);
    }

    [Fact]
    public void Complete_PartyDefeatDoesNotResolveLootChangeInventoryOrClearEncounter()
    {
        (BattleCompletionService service, GameSession session, RecordingLootResolver resolver) =
            CreateService([Award(quantity: 2)]);
        GameState previous = session.Current;

        BattleCompletionResult result = service.Complete(
            Request(BattleOutcome.PartyDefeat),
            ClearanceFlagId,
            new UnusedRandomSource());

        Assert.Equal(BattleCompletionDisposition.PartyDefeat, result.Disposition);
        Assert.Null(result.Rewards);
        Assert.Equal(0, resolver.CallCount);
        Assert.Same(previous, session.Current);
        Assert.False(session.GetEventFlag(ClearanceFlagId));
    }

    [Fact]
    public void Complete_AlreadyClearedVictoryCannotGrantRewardsAgain()
    {
        (BattleCompletionService service, GameSession session, RecordingLootResolver resolver) =
            CreateService(
                [Award(quantity: 2)],
                eventFlags: new Dictionary<string, bool>(StringComparer.Ordinal)
                {
                    [ClearanceFlagId] = true,
                });
        GameState previous = session.Current;

        BattleCompletionResult result = service.Complete(
            Request(BattleOutcome.PartyVictory),
            ClearanceFlagId,
            new UnusedRandomSource());

        Assert.Equal(BattleCompletionDisposition.AlreadyCleared, result.Disposition);
        Assert.Null(result.Rewards);
        Assert.Equal(0, resolver.CallCount);
        Assert.Same(previous, session.Current);
        Assert.Empty(session.Current.Inventory);
    }

    [Fact]
    public void Complete_RewardFailureLeavesClearanceFalseAndInventoryUnchanged()
    {
        (BattleCompletionService service, GameSession session, RecordingLootResolver resolver) =
            CreateService(
                [Award(quantity: 2)],
                maxStack: 2,
                inventory: new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [ItemId] = 1,
                });
        GameState previous = session.Current;
        int notifications = 0;
        session.StateChanged += (_, _) => notifications++;

        Assert.Throws<InvalidOperationException>(() => service.Complete(
            Request(BattleOutcome.PartyVictory),
            ClearanceFlagId,
            new UnusedRandomSource()));

        Assert.Equal(1, resolver.CallCount);
        Assert.Same(previous, session.Current);
        Assert.Equal(1, session.Current.Inventory[ItemId]);
        Assert.False(session.GetEventFlag(ClearanceFlagId));
        Assert.Equal(0, notifications);
    }

    private static (
        BattleCompletionService Service,
        GameSession Session,
        RecordingLootResolver Resolver) CreateService(
            IReadOnlyList<LootAward> awards,
            int maxStack = 99,
            Dictionary<string, int>? inventory = null,
            Dictionary<string, bool>? eventFlags = null)
    {
        var item = new ItemDefinition
        {
            Id = ItemId,
            DisplayNameKey = "item.test.reward.name",
            DescriptionKey = "item.test.reward.description",
            MaxStack = maxStack,
        };
        var session = new GameSession();
        session.ReplaceState(new GameState
        {
            SaveId = "battle-completion-test",
            Inventory = inventory ?? new Dictionary<string, int>(StringComparer.Ordinal),
            EventFlags = eventFlags ?? new Dictionary<string, bool>(StringComparer.Ordinal),
        });
        var resolver = new RecordingLootResolver(awards);
        var rewards = new VictoryRewardService(
            resolver,
            new InventoryService(new TestCatalog(item), session));
        return (new BattleCompletionService(rewards, session), session, resolver);
    }

    private static BattleCompletionRequest Request(BattleOutcome outcome) => new(
        "encounter.test.reward",
        outcome,
        ["enemy.test.reward"]);

    private static LootAward Award(int quantity) => new(
        "enemy.test.reward",
        "loot-table.test.reward",
        ItemId,
        quantity);

    private sealed class RecordingLootResolver : ILootResolver
    {
        private readonly LootResolution _resolution;

        public RecordingLootResolver(IReadOnlyList<LootAward> awards)
        {
            _resolution = new LootResolution(awards);
        }

        public int CallCount { get; private set; }

        public LootResolution Resolve(
            IReadOnlyList<string> defeatedEnemyDefinitionIds,
            IRandomSource random)
        {
            CallCount++;
            return _resolution;
        }
    }

    private sealed class UnusedRandomSource : IRandomSource
    {
        public int Next(int minInclusive, int maxExclusive) =>
            throw new InvalidOperationException("The recording resolver must not use randomness.");
    }
}
