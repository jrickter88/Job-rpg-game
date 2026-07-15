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
