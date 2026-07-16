using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Maps;

/// <summary>Godot-free queries over one authored ASCII passability map.</summary>
public sealed class MapQueryService
{
    private readonly MapDefinition _map;
    private readonly IReadOnlyDictionary<(int X, int Y), MapEncounterMarkerDefinition> _encounters;
    private readonly IReadOnlyDictionary<(int X, int Y), MapTransitionDefinition> _transitions;

    public MapQueryService(MapDefinition map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _encounters = map.Encounters.ToDictionary(marker => (marker.X, marker.Y));
        _transitions = map.Transitions
            .ToDictionary(transition => (transition.SourceCell.X, transition.SourceCell.Y));
    }

    public string DisplayNameKey => _map.DisplayNameKey;
    public string MapId => _map.Id;
    public int Width => _map.Width;
    public int Height => _map.Height;
    public IReadOnlyCollection<MapEncounterMarkerDefinition> EncounterMarkers => _map.Encounters;
    public IReadOnlyCollection<MapTransitionDefinition> TransitionMarkers => _transitions.Values.ToArray();

    public bool IsInside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public char GetSymbol(int x, int y)
    {
        if (!IsInside(x, y))
        {
            throw new ArgumentOutOfRangeException(nameof(x), $"Tile ({x}, {y}) is outside map '{_map.Id}'.");
        }
        return _map.Rows[y][x];
    }

    public bool IsPassable(int x, int y) => IsInside(x, y) && GetSymbol(x, y) != '#';

    public bool TryGetSpawn(string spawnId, out MapSpawnDefinition? spawn)
    {
        spawn = _map.Spawns.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, spawnId, StringComparison.Ordinal));
        return spawn is not null;
    }

    public bool TryGetEncounterAt(int x, int y, out MapEncounterMarkerDefinition? marker) =>
        _encounters.TryGetValue((x, y), out marker);

    public bool TryGetTransitionAt(int x, int y, out MapTransitionDefinition? transition) =>
        _transitions.TryGetValue((x, y), out transition);
}
