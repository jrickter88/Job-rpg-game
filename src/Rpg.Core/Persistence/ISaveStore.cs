namespace RpgGame.Core.Persistence;

/// <summary>
/// Platform persistence port for reading and writing versioned save documents.
/// </summary>
/// <remarks>
/// Core code describes what must be stored but does not assume Windows paths, Godot's
/// <c>user://</c> location, cloud storage, or a test filesystem. Composition code chooses
/// the platform path; the JSON file implementation performs verified temporary writes.
/// </remarks>
public interface ISaveStore
{
    /// <summary>
    /// Loads a slot, returning null when it has never been created. Corrupt or unsupported
    /// saves should produce explicit errors rather than masquerading as an empty slot.
    /// </summary>
    Task<SaveEnvelope?> LoadAsync(string slotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a fully prepared envelope to one logical slot. The cancellation token keeps
    /// shutdown or UI cancellation from requiring Godot dependencies in core code.
    /// </summary>
    Task SaveAsync(
        string slotId,
        SaveEnvelope save,
        CancellationToken cancellationToken = default);
}
