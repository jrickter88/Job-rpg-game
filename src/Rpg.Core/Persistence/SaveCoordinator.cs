using RpgGame.Core.Mods;
using RpgGame.Core.State;

namespace RpgGame.Core.Persistence;

/// <summary>
/// Application-facing save use case that creates metadata and returns scene-independent state.
/// </summary>
public sealed class SaveCoordinator
{
    private readonly ISaveStore _store;
    private readonly string _gameVersion;
    private readonly IReadOnlyList<ModReference> _enabledMods;
    private readonly TimeProvider _timeProvider;

    public SaveCoordinator(
        ISaveStore store,
        string gameVersion,
        IEnumerable<ModReference>? enabledMods = null,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentException.ThrowIfNullOrWhiteSpace(gameVersion);
        _gameVersion = gameVersion;
        _enabledMods = NormalizeModReferences(enabledMods ?? []);
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
            // Create new DTOs instead of exposing the coordinator's list to serializers or
            // callers that may retain and mutate a reference after SaveAsync returns.
            EnabledMods = _enabledMods
                .Select(mod => new ModReference { Id = mod.Id, Version = mod.Version })
                .ToList(),
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
        if (envelope is null)
        {
            return null;
        }

        EnsureRequiredModsAvailable(envelope.EnabledMods);
        return envelope.State;
    }

    private void EnsureRequiredModsAvailable(IReadOnlyList<ModReference>? requiredMods)
    {
        if (requiredMods is null)
        {
            throw new InvalidDataException("Save field 'enabledMods' cannot be null.");
        }

        IReadOnlyList<ModReference> normalizedRequiredMods;
        try
        {
            normalizedRequiredMods = NormalizeModReferences(requiredMods);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "Save contains invalid or duplicate data-mod metadata.",
                exception);
        }

        var enabledById = _enabledMods.ToDictionary(mod => mod.Id, StringComparer.Ordinal);
        foreach (ModReference requiredMod in normalizedRequiredMods)
        {
            if (!enabledById.TryGetValue(requiredMod.Id, out ModReference? installedMod))
            {
                throw new MissingSaveModException(requiredMod.Id);
            }

            if (!string.Equals(
                    requiredMod.Version,
                    installedMod.Version,
                    StringComparison.Ordinal))
            {
                throw new IncompatibleSaveModVersionException(
                    requiredMod.Id,
                    requiredMod.Version,
                    installedMod.Version);
            }
        }

        // Extra currently enabled mods are allowed. A future profile UI can give the player
        // exact-set control; Milestone 1.5 only guarantees that everything the save requires
        // is present at the same version.
    }

    private static IReadOnlyList<ModReference> NormalizeModReferences(
        IEnumerable<ModReference> modReferences)
    {
        var normalized = new List<ModReference>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (ModReference? modReference in modReferences)
        {
            if (modReference is null)
            {
                throw new ArgumentException("Data-mod metadata cannot contain null entries.");
            }

            if (!ModIdentity.IsValidId(modReference.Id))
            {
                throw new ArgumentException(
                    $"'{modReference.Id}' is not a valid stable data-mod ID.");
            }

            if (!ModIdentity.IsValidVersion(modReference.Version))
            {
                throw new ArgumentException(
                    $"'{modReference.Version}' is not a valid version for '{modReference.Id}'.");
            }

            if (!seenIds.Add(modReference.Id))
            {
                throw new ArgumentException(
                    $"Data-mod metadata contains duplicate ID '{modReference.Id}'.");
            }

            normalized.Add(new ModReference
            {
                Id = modReference.Id,
                Version = modReference.Version,
            });
        }

        return normalized.OrderBy(mod => mod.Id, StringComparer.Ordinal).ToArray();
    }
}
