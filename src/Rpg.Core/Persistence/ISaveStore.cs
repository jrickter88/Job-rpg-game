namespace RpgGame.Core.Persistence;

/// <summary>
/// Persistence port. A Godot adapter will choose paths and perform atomic file replacement.
/// </summary>
public interface ISaveStore
{
    Task<SaveEnvelope?> LoadAsync(string slotId, CancellationToken cancellationToken = default);

    Task SaveAsync(
        string slotId,
        SaveEnvelope save,
        CancellationToken cancellationToken = default);
}

