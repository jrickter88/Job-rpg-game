using RpgGame.Core.Combat;
using RpgGame.Core.State;

namespace RpgGame.Encounters;

/// <summary>
/// Owns the two stable IDs and the one campaign handoff for the fixed test-room encounter.
/// </summary>
/// <remarks>
/// This is intentionally not a generalized encounter-progress framework. The game has one
/// encounter trigger, one clearance fact, and one concrete victory rule. Keeping that mapping in
/// one game-specific application helper prevents the exploration scene and composition root from
/// duplicating persistent IDs while leaving reusable combat rules free of campaign knowledge.
/// </remarks>
public static class TestRoomEncounterProgress
{
    public const string EncounterId = "encounter.forest.slimes-01";

    public const string ClearedFlagId = "flag.encounter.forest.slimes-01.cleared";

    /// <summary>Reads the fixed persistent clearance fact; an absent flag means not cleared.</summary>
    public static bool IsCleared(IGameSession session, string encounterId)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireSupportedEncounter(encounterId);
        return session.GetEventFlag(ClearedFlagId);
    }

    /// <summary>
    /// Applies the terminal battle result to campaign state. Only victory clears the encounter.
    /// </summary>
    /// <remarks>
    /// Defeat deliberately performs no session mutation. The caller still reconstructs
    /// exploration, but stepping off and back onto the marker can start a fresh battle.
    /// </remarks>
    public static void ApplyOutcome(
        IGameSession session,
        string encounterId,
        BattleOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(session);
        RequireSupportedEncounter(encounterId);
        if (outcome == BattleOutcome.InProgress || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Campaign handoff requires PartyVictory or PartyDefeat.");
        }

        if (outcome == BattleOutcome.PartyVictory)
        {
            session.SetEventFlag(ClearedFlagId);
        }
    }

    private static void RequireSupportedEncounter(string encounterId)
    {
        if (!string.Equals(encounterId, EncounterId, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Campaign handoff supports only fixed encounter '{EncounterId}', but received "
                + $"'{encounterId}'.",
                nameof(encounterId));
        }
    }
}
