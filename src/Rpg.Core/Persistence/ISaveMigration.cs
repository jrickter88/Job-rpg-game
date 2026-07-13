using System.Text.Json.Nodes;

namespace RpgGame.Core.Persistence;

/// <summary>
/// One explicit, ordered transformation between adjacent save-format versions.
/// </summary>
public interface ISaveMigration
{
    int FromVersion { get; }

    int ToVersion { get; }

    JsonObject Migrate(JsonObject source);
}

