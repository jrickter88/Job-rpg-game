using System.Text.Json.Nodes;

namespace RpgGame.Core.Persistence;

/// <summary>
/// Applies an unbroken chain of adjacent JSON migrations to the current save format.
/// </summary>
public sealed class SaveMigrationRunner
{
    private readonly int _currentVersion;
    private readonly IReadOnlyDictionary<int, ISaveMigration> _migrationBySourceVersion;

    public SaveMigrationRunner(int currentVersion, IEnumerable<ISaveMigration> migrations)
    {
        if (currentVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(currentVersion));
        }

        ArgumentNullException.ThrowIfNull(migrations);
        _currentVersion = currentVersion;

        var migrationBySourceVersion = new Dictionary<int, ISaveMigration>();
        foreach (ISaveMigration migration in migrations)
        {
            if (migration.ToVersion != migration.FromVersion + 1)
            {
                throw new ArgumentException(
                    $"Migration {migration.GetType().Name} must advance exactly one version.",
                    nameof(migrations));
            }

            if (!migrationBySourceVersion.TryAdd(migration.FromVersion, migration))
            {
                throw new ArgumentException(
                    $"More than one migration starts at version {migration.FromVersion}.",
                    nameof(migrations));
            }
        }

        _migrationBySourceVersion = migrationBySourceVersion;
    }

    /// <summary>
    /// Returns a migrated clone, leaving the caller's parsed document untouched.
    /// </summary>
    public JsonObject MigrateToCurrent(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        JsonObject current = (JsonObject)source.DeepClone();
        int version = ReadVersion(current);

        if (version > _currentVersion)
        {
            throw new NotSupportedException(
                $"Save format {version} is newer than supported format {_currentVersion}.");
        }

        while (version < _currentVersion)
        {
            if (!_migrationBySourceVersion.TryGetValue(version, out ISaveMigration? migration))
            {
                throw new NotSupportedException(
                    $"No save migration exists from format {version} to {version + 1}.");
            }

            current = migration.Migrate(current)
                ?? throw new InvalidDataException(
                    $"Migration {migration.GetType().Name} returned null JSON.");

            int migratedVersion = ReadVersion(current);
            if (migratedVersion != migration.ToVersion)
            {
                throw new InvalidDataException(
                    $"Migration {migration.GetType().Name} declared version {migration.ToVersion} "
                    + $"but produced version {migratedVersion}.");
            }

            version = migratedVersion;
        }

        return current;
    }

    private static int ReadVersion(JsonObject source)
    {
        if (!source.TryGetPropertyValue("saveFormatVersion", out JsonNode? node)
            || node is null)
        {
            throw new InvalidDataException("Save JSON is missing integer saveFormatVersion.");
        }

        try
        {
            return node.GetValue<int>();
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            throw new InvalidDataException("saveFormatVersion must be an integer.", exception);
        }
    }
}
