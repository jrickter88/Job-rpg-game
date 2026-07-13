using System.Text;

namespace RpgGame.Core.Persistence;

/// <summary>
/// JSON file implementation that writes beside the destination and atomically renames it.
/// </summary>
/// <remarks>
/// The directory is injected, keeping platform path selection outside core. Godot supplies a
/// globalized <c>user://saves</c> path; tests supply a temporary directory. Before replacement,
/// the previous primary is copied to <c>.bak</c> as a last-known-good recovery artifact.
/// </remarks>
public sealed class JsonFileSaveStore : ISaveStore
{
    private readonly string _saveDirectory;
    private readonly SaveJsonSerializer _serializer;

    public JsonFileSaveStore(string saveDirectory, SaveJsonSerializer serializer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(saveDirectory);
        _saveDirectory = Path.GetFullPath(saveDirectory);
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
    }

    /// <inheritdoc />
    public async Task<SaveEnvelope?> LoadAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        string path = GetPrimaryPath(slotId);
        if (!File.Exists(path))
        {
            return null;
        }

        string json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        return _serializer.Deserialize(json);
    }

    /// <inheritdoc />
    public async Task SaveAsync(
        string slotId,
        SaveEnvelope save,
        CancellationToken cancellationToken = default)
    {
        string primaryPath = GetPrimaryPath(slotId);
        string backupPath = primaryPath + ".bak";
        string temporaryPath = primaryPath + $".{Guid.NewGuid():N}.tmp";
        string json = _serializer.Serialize(save);

        Directory.CreateDirectory(_saveDirectory);

        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            // Read the exact bytes back before replacing a known-good primary. This catches a
            // truncated/failed write while recovery is still possible.
            string verificationJson = await File.ReadAllTextAsync(
                    temporaryPath,
                    Encoding.UTF8,
                    cancellationToken)
                .ConfigureAwait(false);
            _serializer.Deserialize(verificationJson);

            if (File.Exists(primaryPath))
            {
                File.Copy(primaryPath, backupPath, overwrite: true);
            }

            // The temporary file is on the same filesystem as the destination, so replacement
            // cannot expose a partially written JSON document.
            File.Move(temporaryPath, primaryPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private string GetPrimaryPath(string slotId)
    {
        string safeSlotId = SaveSlotId.RequireValid(slotId);
        return Path.Combine(_saveDirectory, safeSlotId + ".json");
    }
}
