using RpgGame.Core.Combat.Formation;

namespace RpgGame.Core.Combat;

/// <summary>Terminal battle facts carried from the final snapshot to application ownership.</summary>
public sealed record BattleCompletionRequest
{
    public BattleCompletionRequest(
        string encounterId,
        BattleOutcome outcome,
        IReadOnlyList<string> defeatedEnemyDefinitionIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        ArgumentNullException.ThrowIfNull(defeatedEnemyDefinitionIds);
        if (outcome == BattleOutcome.InProgress || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A battle completion request requires PartyVictory or PartyDefeat.");
        }

        if (defeatedEnemyDefinitionIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Defeated enemy definition IDs cannot be null or blank.",
                nameof(defeatedEnemyDefinitionIds));
        }

        EncounterId = encounterId;
        Outcome = outcome;
        DefeatedEnemyDefinitionIds = Array.AsReadOnly(
            defeatedEnemyDefinitionIds.ToArray());
    }

    public string EncounterId { get; }

    public BattleOutcome Outcome { get; }

    /// <summary>Defeated enemy definition IDs in authoritative combatant order.</summary>
    public IReadOnlyList<string> DefeatedEnemyDefinitionIds { get; }

    /// <summary>Creates completion data only from an authoritative terminal snapshot.</summary>
    public static BattleCompletionRequest FromFinalSnapshot(
        string encounterId,
        CombatSnapshot finalSnapshot)
    {
        ArgumentNullException.ThrowIfNull(finalSnapshot);
        return new BattleCompletionRequest(
            encounterId,
            finalSnapshot.Outcome,
            finalSnapshot.Combatants
                .Where(combatant =>
                    combatant.Side == BattleSide.Enemy && combatant.IsDefeated)
                .Select(combatant => combatant.DefinitionId)
                .ToArray());
    }
}
