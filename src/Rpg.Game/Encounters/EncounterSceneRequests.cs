using RpgGame.Core.Combat;

namespace RpgGame.Encounters;

/// <summary>
/// Transient request to present one validated encounter definition.
/// </summary>
/// <remarks>
/// The stable encounter ID is enough to cross the scene boundary. This request is not
/// campaign progress, so it deliberately does not belong in GameState or a save file.
/// GameRoot resolves the ID through IContentCatalog before it removes exploration.
/// </remarks>
public sealed record EncounterLaunchRequest(string EncounterId);

/// <summary>Typed event payload raised when exploration enters an encounter tile.</summary>
public sealed class EncounterLaunchRequestedEventArgs : EventArgs
{
    public EncounterLaunchRequestedEventArgs(EncounterLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public EncounterLaunchRequest Request { get; }
}

/// <summary>Terminal battle result crossing from disposable presentation to its owner.</summary>
/// <remarks>
/// The battle scene reports only stable encounter identity and the core-authored outcome.
/// GameRoot decides whether that result changes campaign state and which scene appears next.
/// </remarks>
public sealed record BattleCompletionRequest
{
    public BattleCompletionRequest(string encounterId, BattleOutcome outcome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(encounterId);
        if (outcome == BattleOutcome.InProgress || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A battle completion request requires PartyVictory or PartyDefeat.");
        }

        EncounterId = encounterId;
        Outcome = outcome;
    }

    public string EncounterId { get; }

    public BattleOutcome Outcome { get; }
}

/// <summary>Typed event payload raised after the player confirms a terminal battle result.</summary>
public sealed class BattleCompletionRequestedEventArgs : EventArgs
{
    public BattleCompletionRequestedEventArgs(BattleCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public BattleCompletionRequest Request { get; }
}
