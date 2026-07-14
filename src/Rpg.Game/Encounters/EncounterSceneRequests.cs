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

/// <summary>
/// Transient request to leave the placeholder for the encounter currently being shown.
/// </summary>
public sealed record EncounterReturnRequest(string EncounterId);

/// <summary>Typed event payload raised when the placeholder asks its owner to return.</summary>
public sealed class EncounterReturnRequestedEventArgs : EventArgs
{
    public EncounterReturnRequestedEventArgs(EncounterReturnRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public EncounterReturnRequest Request { get; }
}
