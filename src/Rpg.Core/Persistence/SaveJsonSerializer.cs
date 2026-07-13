using System.Text.Json;
using System.Text.Json.Nodes;

namespace RpgGame.Core.Persistence;

/// <summary>
/// Owns the canonical JSON settings and migration-before-deserialization save pipeline.
/// </summary>
public sealed class SaveJsonSerializer
{
    /// <summary>Current file format written by this build.</summary>
    public const int CurrentFormatVersion = 1;

    private readonly JsonSerializerOptions _options;
    private readonly SaveMigrationRunner _migrationRunner;

    public SaveJsonSerializer(IEnumerable<ISaveMigration>? migrations = null)
    {
        _options = CreateOptions();
        _migrationRunner = new SaveMigrationRunner(
            CurrentFormatVersion,
            migrations ?? Array.Empty<ISaveMigration>());
    }

    /// <summary>Serializes one current-format envelope as readable camelCase JSON.</summary>
    public string Serialize(SaveEnvelope save)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (save.SaveFormatVersion != CurrentFormatVersion)
        {
            throw new InvalidOperationException(
                $"Cannot write save format {save.SaveFormatVersion}; expected {CurrentFormatVersion}.");
        }

        return JsonSerializer.Serialize(save, _options);
    }

    /// <summary>
    /// Parses raw JSON, migrates it to the current shape, then creates strongly typed state.
    /// </summary>
    public SaveEnvelope Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(
                json,
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                });
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Save file contains invalid JSON.", exception);
        }

        if (parsed is not JsonObject root)
        {
            throw new InvalidDataException("Save file must contain one top-level JSON object.");
        }

        JsonObject migrated = _migrationRunner.MigrateToCurrent(root);

        try
        {
            return migrated.Deserialize<SaveEnvelope>(_options)
                ?? throw new InvalidDataException("Save JSON deserialized to null.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Save JSON does not match the current schema.", exception);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
        };
        // Populate the reflection-based resolver before freezing the options. The explicit
        // argument is required by .NET 8 when no source-generated resolver was assigned.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
