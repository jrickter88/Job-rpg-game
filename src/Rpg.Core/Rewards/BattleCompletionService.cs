using RpgGame.Core.Combat;
using RpgGame.Core.State;

namespace RpgGame.Core.Rewards;

/// <summary>
/// Applies confirmed victory rewards once and publishes clearance only after they succeed.
/// </summary>
public sealed class BattleCompletionService
{
    private readonly VictoryRewardService _victoryRewards;
    private readonly IGameSession _session;

    public BattleCompletionService(
        VictoryRewardService victoryRewards,
        IGameSession session)
    {
        _victoryRewards = victoryRewards
            ?? throw new ArgumentNullException(nameof(victoryRewards));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public BattleCompletionResult Complete(
        BattleCompletionRequest request,
        string clearanceFlagId,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(clearanceFlagId);
        ArgumentNullException.ThrowIfNull(random);

        if (request.Outcome == BattleOutcome.PartyDefeat)
        {
            return BattleCompletionResult.ForDefeat();
        }

        if (_session.GetEventFlag(clearanceFlagId))
        {
            return BattleCompletionResult.ForAlreadyCleared();
        }

        VictoryRewardResult rewards = _victoryRewards.Apply(
            request.DefeatedEnemyDefinitionIds,
            random);
        _session.SetEventFlag(clearanceFlagId);
        return BattleCompletionResult.ForAppliedVictory(rewards);
    }
}
