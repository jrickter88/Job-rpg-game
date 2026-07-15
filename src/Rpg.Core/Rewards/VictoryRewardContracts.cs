using RpgGame.Core.Loot;

namespace RpgGame.Core.Rewards;

/// <summary>Aggregated player-facing total for one stable item definition.</summary>
public sealed record ItemRewardSummary(string ItemId, int Quantity);

/// <summary>Immutable raw and aggregated facts from one accepted victory reward grant.</summary>
public sealed record VictoryRewardResult
{
    public VictoryRewardResult(
        IReadOnlyList<LootAward> awards,
        IReadOnlyList<ItemRewardSummary> itemSummaries)
    {
        ArgumentNullException.ThrowIfNull(awards);
        ArgumentNullException.ThrowIfNull(itemSummaries);
        if (awards.Any(award => award is null))
        {
            throw new ArgumentException(
                "Victory rewards cannot contain a null award.",
                nameof(awards));
        }

        if (itemSummaries.Any(summary => summary is null))
        {
            throw new ArgumentException(
                "Victory rewards cannot contain a null item summary.",
                nameof(itemSummaries));
        }

        Awards = Array.AsReadOnly(awards.ToArray());
        ItemSummaries = Array.AsReadOnly(itemSummaries.ToArray());
    }

    /// <summary>Independent loot facts in resolver order.</summary>
    public IReadOnlyList<LootAward> Awards { get; }

    /// <summary>Totals grouped by item ID in first-award order.</summary>
    public IReadOnlyList<ItemRewardSummary> ItemSummaries { get; }
}

/// <summary>Application result for one confirmed terminal battle request.</summary>
public enum BattleCompletionDisposition
{
    PartyDefeat,
    VictoryRewardsApplied,
    AlreadyCleared,
}

/// <summary>Immutable completion-policy result consumed by the composition root.</summary>
public sealed record BattleCompletionResult
{
    private BattleCompletionResult(
        BattleCompletionDisposition disposition,
        VictoryRewardResult? rewards)
    {
        Disposition = disposition;
        Rewards = rewards;
    }

    public BattleCompletionDisposition Disposition { get; }

    public VictoryRewardResult? Rewards { get; }

    public static BattleCompletionResult ForDefeat() =>
        new(BattleCompletionDisposition.PartyDefeat, rewards: null);

    public static BattleCompletionResult ForAlreadyCleared() =>
        new(BattleCompletionDisposition.AlreadyCleared, rewards: null);

    public static BattleCompletionResult ForAppliedVictory(VictoryRewardResult rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        return new(BattleCompletionDisposition.VictoryRewardsApplied, rewards);
    }
}
