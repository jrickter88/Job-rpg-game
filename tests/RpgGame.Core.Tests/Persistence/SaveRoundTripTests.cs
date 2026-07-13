using RpgGame.Core.Persistence;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Persistence;

/// <summary>Filesystem-level tests for the complete save and load use case.</summary>
public sealed class SaveRoundTripTests
{
    /// <summary>
    /// Satisfies the Milestone 1 exit criterion: content creates a campaign, the campaign
    /// survives a real JSON file round trip, and a later write preserves a backup.
    /// </summary>
    [Fact]
    public async Task FixtureContent_NewGame_SaveAndLoad_PreservesEquivalentState()
    {
        string saveDirectory = Path.Combine(
            Path.GetTempPath(),
            "RpgGame.Tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            GameState original = new NewGameFactory(TestContent.LoadCatalog()).Create(
                new NewGameRequest
                {
                    SaveId = "round-trip-campaign",
                    StartingMapId = "map.prologue.test-room",
                    StartingX = 2,
                    StartingY = 3,
                    StartingFacing = "east",
                    StartingActorIds = ["actor.hero.aria"],
                });
            original.EventFlags["flag.test.introduction-seen"] = true;

            var serializer = new SaveJsonSerializer();
            var store = new JsonFileSaveStore(saveDirectory, serializer);
            var coordinator = new SaveCoordinator(store, "test-build");

            await coordinator.SaveAsync("slot_1", original);
            GameState loaded = await coordinator.LoadAsync("slot_1")
                ?? throw new InvalidOperationException("The saved slot unexpectedly disappeared.");

            AssertEquivalent(original, loaded);

            // A second successful write copies the old primary to slot_1.json.bak before
            // replacing it, providing a last-known-good recovery file.
            loaded.EventFlags["flag.test.saved-twice"] = true;
            await coordinator.SaveAsync("slot_1", loaded);

            Assert.True(File.Exists(Path.Combine(saveDirectory, "slot_1.json")));
            Assert.True(File.Exists(Path.Combine(saveDirectory, "slot_1.json.bak")));
        }
        finally
        {
            if (Directory.Exists(saveDirectory))
            {
                Directory.Delete(saveDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ReturnsNullForUnusedSlot()
    {
        string saveDirectory = Path.Combine(
            Path.GetTempPath(),
            "RpgGame.Tests",
            Guid.NewGuid().ToString("N"));
        var store = new JsonFileSaveStore(saveDirectory, new SaveJsonSerializer());

        SaveEnvelope? result = await store.LoadAsync("unused");

        Assert.Null(result);
    }

    private static void AssertEquivalent(GameState expected, GameState actual)
    {
        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.SaveId, actual.SaveId);
        Assert.Equal(expected.Location, actual.Location);
        Assert.Equal(expected.ActivePartyActorIds, actual.ActivePartyActorIds);
        Assert.Equal(expected.EventFlags, actual.EventFlags);
        Assert.Equal(expected.ActorProgress.Keys.Order(), actual.ActorProgress.Keys.Order());

        foreach (string actorId in expected.ActorProgress.Keys)
        {
            Assert.Equal(expected.ActorProgress[actorId], actual.ActorProgress[actorId]);
        }
    }
}
