using System.Text.Json;
using RpgGame.Core.Persistence;
using Xunit;

namespace RpgGame.Core.Tests.Persistence;

public sealed class SaveCompatibilityTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    [Fact]
    public void UnknownStateFields_SurviveLoadAndResave()
    {
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

        SaveEnvelope save = JsonSerializer.Deserialize<SaveEnvelope>(json, JsonOptions)
            ?? throw new InvalidOperationException("The fixture did not deserialize.");

        Dictionary<string, JsonElement> extensionData = save.State.ExtensionData
            ?? throw new InvalidOperationException("Unknown state fields were not retained.");

        Assert.Contains("futureField", extensionData);

        string rewritten = JsonSerializer.Serialize(save, JsonOptions);
        using JsonDocument document = JsonDocument.Parse(rewritten);

        int futureValue = document.RootElement
            .GetProperty("state")
            .GetProperty("futureField")
            .GetProperty("value")
            .GetInt32();

        Assert.Equal(42, futureValue);
    }
}
