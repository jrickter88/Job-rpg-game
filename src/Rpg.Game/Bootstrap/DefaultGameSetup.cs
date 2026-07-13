using RpgGame.Core.State;

namespace RpgGame.Bootstrap;

/// <summary>
/// Game-specific starting content choices kept out of the reusable new-game factory.
/// </summary>
public static class DefaultGameSetup
{
    /// <summary>Creates a fresh request with a unique save lineage.</summary>
    public static NewGameRequest CreateRequest() => new()
    {
        SaveId = Guid.NewGuid().ToString("N"),
        StartingMapId = "map.prologue.test-room",
        StartingX = 4,
        StartingY = 4,
        StartingFacing = "south",
        StartingActorIds = ["actor.hero.aria"],
    };
}
