using System.Text.Json;
using System.Text.Json.Serialization;
using RpgGame.Core.State;

namespace RpgGame.Core.Persistence;

public sealed record SaveEnvelope
{
    public int SaveFormatVersion { get; init; } = 1;

    public string GameVersion { get; init; } = "0.0.0";

    public DateTimeOffset SavedAtUtc { get; init; } = DateTimeOffset.UtcNow;

    public required GameState State { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; init; }
}

