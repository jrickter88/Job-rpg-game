using RpgGame.Core.State;

namespace RpgGame.Core.Persistence;

/// <summary>
/// Application-facing save use case that creates metadata and returns scene-independent state.
/// </summary>
public sealed class SaveCoordinator
{
    private readonly ISaveStore _store;
    private readonly string _gameVersion;
    private readonly TimeProvider _timeProvider;

    public SaveCoordinator(
        ISaveStore store,
        string gameVersion,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        _gameVersion = gameVersion;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Wraps and stores the supplied authoritative state.</summary>
    public Task SaveAsync(
        string slotId,
        GameState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var envelope = new SaveEnvelope
        {
            SaveFormatVersion = SaveJsonSerializer.CurrentFormatVersion,
            GameVersion = _gameVersion,
            SavedAtUtc = _timeProvider.GetUtcNow(),
            State = state,
        };

        return _store.SaveAsync(slotId, envelope, cancellationToken);
    }

    /// <summary>Loads a slot and returns only campaign state, or null for an unused slot.</summary>
    public async Task<GameState?> LoadAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        SaveEnvelope? envelope = await _store.LoadAsync(slotId, cancellationToken)
            .ConfigureAwait(false);
        return envelope?.State;
    }
}
