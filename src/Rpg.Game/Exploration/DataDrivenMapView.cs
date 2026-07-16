using Godot;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Maps;

namespace RpgGame.Exploration;

/// <summary>Generic placeholder presentation for any authored ASCII exploration map.</summary>
public partial class DataDrivenMapView : Node2D, IExplorationMapView
{
    private const int TileSize = 48;
    private static readonly Vector2 DrawingOrigin = new(96, 136);
    private static readonly Vector2I TestRoomGuideTile = new(7, 4);
    private const string TestRoomMapId = "map.prologue.test-room";

    private MapQueryService _map = null!;
    private IReadOnlySet<string> _clearedEncounterFlags = new HashSet<string>(StringComparer.Ordinal);

    public string MapId => _map.MapId;
    public Vector2I GuideTile => MapId == TestRoomMapId ? TestRoomGuideTile : new Vector2I(-1, -1);

    public void Initialize(MapQueryService map)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        QueueRedraw();
    }

    public Vector2 TileToWorld(Vector2I tile) => DrawingOrigin + new Vector2(
        (tile.X + 0.5f) * TileSize,
        (tile.Y + 0.5f) * TileSize);

    public bool IsWalkable(Vector2I tile) => _map.IsPassable(tile.X, tile.Y);

    public bool TryGetEncounterAt(Vector2I tile, out string encounterId)
    {
        if (_map.TryGetEncounterAt(tile.X, tile.Y, out MapEncounterMarkerDefinition? marker)
            && marker is not null
            && !_clearedEncounterFlags.Contains(marker.ClearedFlagId))
        {
            encounterId = marker.EncounterId;
            return true;
        }

        encounterId = string.Empty;
        return false;
    }

    public bool TryGetTransitionAt(Vector2I tile, out MapTransitionDefinition? transition) =>
        _map.TryGetTransitionAt(tile.X, tile.Y, out transition);

    public void SetClearedEncounterFlags(IReadOnlySet<string> clearedFlagIds)
    {
        ArgumentNullException.ThrowIfNull(clearedFlagIds);
        _clearedEncounterFlags = new HashSet<string>(clearedFlagIds, StringComparer.Ordinal);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_map is null)
        {
            return;
        }

        Color floor = new(0.16f, 0.19f, 0.25f);
        Color alternateFloor = new(0.18f, 0.21f, 0.28f);
        Color wall = new(0.34f, 0.38f, 0.48f);
        Color grid = new(0.08f, 0.10f, 0.14f);

        for (int y = 0; y < _map.Height; y++)
        for (int x = 0; x < _map.Width; x++)
        {
            Rect2 rectangle = new(DrawingOrigin + new Vector2(x * TileSize, y * TileSize),
                new Vector2(TileSize, TileSize));
            DrawRect(rectangle, _map.GetSymbol(x, y) == '#' ? wall : ((x + y) % 2 == 0 ? floor : alternateFloor));
        }

        // Draw shared tile edges once. Outlining every tile makes adjacent borders overlap and
        // creates the thicker center lines visible in the placeholder map.
        for (int x = 0; x <= _map.Width; x++)
        {
            float position = DrawingOrigin.X + x * TileSize;
            DrawLine(
                new Vector2(position, DrawingOrigin.Y),
                new Vector2(position, DrawingOrigin.Y + _map.Height * TileSize),
                grid,
                1.0f);
        }

        for (int y = 0; y <= _map.Height; y++)
        {
            float position = DrawingOrigin.Y + y * TileSize;
            DrawLine(
                new Vector2(DrawingOrigin.X, position),
                new Vector2(DrawingOrigin.X + _map.Width * TileSize, position),
                grid,
                1.0f);
        }

        foreach (MapEncounterMarkerDefinition marker in _map.EncounterMarkers)
        {
            if (_clearedEncounterFlags.Contains(marker.ClearedFlagId)) continue;
            Vector2 center = TileToWorld(new Vector2I(marker.X, marker.Y));
            Vector2[] diamond = [center + Vector2.Up * 15, center + Vector2.Right * 15,
                center + Vector2.Down * 15, center + Vector2.Left * 15];
            DrawColoredPolygon(diamond, new Color(0.82f, 0.23f, 0.36f));
            DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], Colors.White, 2.0f);
        }

        foreach (MapTransitionDefinition transition in _map.TransitionMarkers)
        {
            Vector2 center = TileToWorld(new Vector2I(transition.SourceCell.X, transition.SourceCell.Y));
            DrawCircle(center, 10.0f, new Color(0.30f, 0.85f, 0.95f));
            DrawCircle(center, 10.0f, Colors.White, false, 2.0f);
        }
    }
}
