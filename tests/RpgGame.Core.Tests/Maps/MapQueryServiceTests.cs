using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Maps;
using Xunit;

namespace RpgGame.Core.Tests.Maps;

public sealed class MapQueryServiceTests
{
    [Fact]
    public void QueriesSymbolsPassabilitySpawnsMarkersAndTransitions()
    {
        var map = new MapDefinition
        {
            Id = "map.test.query",
            DisplayNameKey = "map.test.query.name",
            Width = 5,
            Height = 3,
            Rows = ["#####", "#E.T#", "#####"],
            Spawns = [new MapSpawnDefinition { Id = "spawn.test", X = 1, Y = 1 }],
            Encounters =
            [
                new MapEncounterMarkerDefinition
                {
                    Id = "encounter-marker.test.slime",
                    X = 1,
                    Y = 1,
                    EncounterId = "encounter.test.slime",
                    ClearedFlagId = "flag.encounter.test.slime.cleared",
                },
            ],
        };
        var transition = new MapTransitionDefinition
        {
            Id = "transition.test.out",
            SourceCell = new MapCellDefinition { X = 3, Y = 1 },
            DestinationMapId = "map.test.destination",
            DestinationSpawnId = "spawn.test.destination",
        };
        map = map with { Transitions = [transition] };
        var query = new MapQueryService(map);

        Assert.True(query.IsInside(3, 1));
        Assert.False(query.IsInside(5, 1));
        Assert.Equal('E', query.GetSymbol(1, 1));
        Assert.False(query.IsPassable(0, 0));
        Assert.True(query.IsPassable(1, 1));
        Assert.True(query.IsPassable(2, 1));
        Assert.True(query.TryGetSpawn("spawn.test", out MapSpawnDefinition? spawn));
        Assert.Equal((1, 1), (spawn!.X, spawn.Y));
        Assert.True(query.TryGetEncounterAt(1, 1, out MapEncounterMarkerDefinition? encounter));
        Assert.Equal("encounter.test.slime", encounter!.EncounterId);
        Assert.True(query.TryGetTransitionAt(3, 1, out MapTransitionDefinition? foundTransition));
        Assert.Equal(transition.Id, foundTransition!.Id);
        Assert.False(query.TryGetEncounterAt(2, 1, out _));
    }
}
