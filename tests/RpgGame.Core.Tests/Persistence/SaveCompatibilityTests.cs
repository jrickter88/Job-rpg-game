using System.Text.Json;
using RpgGame.Core.Persistence;
using Xunit;

namespace RpgGame.Core.Tests.Persistence;

/// <summary>
/// Executable compatibility guarantees for serialized save documents.
/// </summary>
public sealed class SaveCompatibilityTests
{
    /// <summary>
    /// Proves that an older game version can load and re-save a newer document without
    /// silently deleting a state field it does not understand.
    /// </summary>
    [Fact]
    public void UnknownStateFields_SurviveLoadAndResave()
    {
        // This hand-written fixture deliberately contains "futureField", which has no
        // matching C# property on GameState. JsonExtensionData should capture it.
        const string json = """
            {
              "saveFormatVersion": 1,
              "gameVersion": "0.0.1",
              "savedAtUtc": "2026-07-13T12:00:00Z",
              "state": {
                "schemaVersion": 1,
                "saveId": "test-save",
                "location": {
                  "mapId": "map.test-room",
                  "x": 3,
                  "y": 5,
                  "facing": "south"
                },
                "activePartyActorIds": [],
                "actorProgress": {},
                "eventFlags": {},
                "futureField": { "value": 42 }
              }
            }
            """;

        // Exercise the production serializer, including its raw-JSON migration boundary,
        // rather than duplicating serializer options inside the test.
        var serializer = new SaveJsonSerializer();
        SaveEnvelope save = serializer.Deserialize(json);

        // Milestone 1 saves predate data mods. The additive list must receive its safe
        // default without requiring a format-version migration.
        Assert.Empty(save.EnabledMods);

        // Verify the unknown field was captured during deserialization.
        Dictionary<string, JsonElement> extensionData = save.State.ExtensionData
            ?? throw new InvalidOperationException("Unknown state fields were not retained.");

        Assert.Contains("futureField", extensionData);

        // Serialize and parse again to verify the field is written back at its original JSON
        // level, not merely retained in memory or nested under an "extensionData" property.
        string rewritten = serializer.Serialize(save);
        using JsonDocument document = JsonDocument.Parse(rewritten);

        int futureValue = document.RootElement
            .GetProperty("state")
            .GetProperty("futureField")
            .GetProperty("value")
            .GetInt32();

        Assert.Equal(42, futureValue);
    }
}
