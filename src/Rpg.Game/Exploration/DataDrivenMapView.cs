using Godot;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Maps;
using RpgGame.Core.Presentation;

namespace RpgGame.Exploration;

/// <summary>Generic placeholder presentation for any authored ASCII exploration map.</summary>
public partial class DataDrivenMapView : Node2D, IExplorationMapView
{
    private const int TileSize = PixelPerfectGeometry.NativeTileSize;
    private const float EncounterMarkerMaxSize = TileSize * 2.0f;
    private static readonly Vector2I TestRoomGuideTile = new(7, 4);
    private const string TestRoomMapId = "map.prologue.test-room";

    private MapQueryService _map = null!;
    private IReadOnlyDictionary<string, Texture2D> _encounterTextureByMarkerId =
        new Dictionary<string, Texture2D>(StringComparer.Ordinal);
    private IReadOnlySet<string> _clearedEncounterFlags = new HashSet<string>(StringComparer.Ordinal);

    public string MapId => _map.MapId;
    public int MapPixelWidth => _map.Width * TileSize;
    public int MapPixelHeight => _map.Height * TileSize;
    public Vector2I GuideTile => MapId == TestRoomMapId ? TestRoomGuideTile : new Vector2I(-1, -1);

    public void Initialize(MapQueryService map, IContentCatalog content)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        ArgumentNullException.ThrowIfNull(content);
        _encounterTextureByMarkerId = LoadEncounterTextures(map, content);
        QueueRedraw();
    }

    public Vector2 TileToWorld(Vector2I tile) => TileToLocal(tile);

    public bool IsWalkable(Vector2I tile) => _map.IsPassable(tile.X, tile.Y);

    public bool TryGetEncounterAt(
        Vector2I tile,
        out MapEncounterMarkerDefinition? marker)
    {
        if (_map.TryGetEncounterAt(tile.X, tile.Y, out marker)
            && marker is not null
            && !_clearedEncounterFlags.Contains(marker.ClearedFlagId))
        {
            return true;
        }

        marker = null;
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
    private static Rect2 CalculateEncounterTextureRectangle(
    Texture2D texture,
    Vector2 tileCenter)
    {
        Vector2 sourceSize = texture.GetSize();

        if (sourceSize.X <= 0.0f || sourceSize.Y <= 0.0f)
        {
            return new Rect2(tileCenter, Vector2.Zero);
        }

        // Never allow an exploration encounter actor to exceed 32x32 native pixels.
        // Smaller true pixel-art assets remain at their authored size.
        float scale = Mathf.Min(
            1.0f,
            Mathf.Min(
                EncounterMarkerMaxSize / sourceSize.X,
                EncounterMarkerMaxSize / sourceSize.Y));

        // Round destination dimensions and coordinates so scaled sprites remain
        // aligned to the native pixel grid.
        Vector2 drawSize = new(
            Mathf.Max(1.0f, Mathf.Round(sourceSize.X * scale)),
            Mathf.Max(1.0f, Mathf.Round(sourceSize.Y * scale)));

        Vector2 topLeft = new(
            Mathf.Round(tileCenter.X - (drawSize.X * 0.5f)),
            Mathf.Round(tileCenter.Y + (TileSize * 0.5f) - drawSize.Y));

        return new Rect2(topLeft, drawSize);
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
            Rect2 rectangle = new(new Vector2(x * TileSize, y * TileSize),
                new Vector2(TileSize, TileSize));
            DrawRect(rectangle, _map.GetSymbol(x, y) == '#' ? wall : ((x + y) % 2 == 0 ? floor : alternateFloor));
        }

        // Draw shared tile edges once. Outlining every tile makes adjacent borders overlap and
        // creates the thicker center lines visible in the placeholder map.
        for (int x = 0; x <= _map.Width; x++)
        {
            float position = x * TileSize;
            DrawLine(
                new Vector2(position, 0),
                new Vector2(position, _map.Height * TileSize),
                grid,
                1.0f);
        }

        for (int y = 0; y <= _map.Height; y++)
        {
            float position = y * TileSize;
            DrawLine(
                new Vector2(0, position),
                new Vector2(_map.Width * TileSize, position),
                grid,
                1.0f);
        }

        foreach (MapEncounterMarkerDefinition marker in _map.EncounterMarkers)
        {

            if (_clearedEncounterFlags.Contains(marker.ClearedFlagId)) continue;
            Vector2 center = TileToLocal(new Vector2I(marker.X, marker.Y));
            if (marker.DialogueId is not null
                && _encounterTextureByMarkerId.TryGetValue(marker.Id, out Texture2D? texture))
            {
                DrawTextureRect(
                    texture,
                    CalculateEncounterTextureRectangle(texture, center),
                    tile: false);

                continue;
            }

            Vector2[] diamond = [center + Vector2.Up * 5, center + Vector2.Right * 5,
                center + Vector2.Down * 5, center + Vector2.Left * 5];
            DrawColoredPolygon(diamond, new Color(0.82f, 0.23f, 0.36f));
            DrawPolyline([diamond[0], diamond[1], diamond[2], diamond[3], diamond[0]], Colors.White, 1.0f);
        }

        foreach (MapTransitionDefinition transition in _map.TransitionMarkers)
        {
            Vector2 center = TileToLocal(new Vector2I(transition.SourceCell.X, transition.SourceCell.Y));
            DrawCircle(center, 5.0f, new Color(0.30f, 0.85f, 0.95f));
            DrawCircle(center, 5.0f, Colors.White, false, 1.0f);
        }
    }

    private static Vector2 TileToLocal(Vector2I tile) => new(
        (tile.X + 0.5f) * TileSize,
        (tile.Y + 0.5f) * TileSize);

    private static IReadOnlyDictionary<string, Texture2D> LoadEncounterTextures(
        MapQueryService map,
        IContentCatalog content)
    {
        var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        foreach (MapEncounterMarkerDefinition marker in map.EncounterMarkers)
        {
            if (marker.DialogueId is null)
            {
                continue;
            }

            EncounterDefinition encounter = content.GetRequired<EncounterDefinition>(marker.EncounterId);
            EncounterEnemyDefinition? firstEnemy = encounter.EnemyGroup.FirstOrDefault();
            if (firstEnemy is null)
            {
                continue;
            }

            EnemyDefinition enemy = content.GetRequired<EnemyDefinition>(firstEnemy.EnemyId);
            if (string.IsNullOrWhiteSpace(enemy.PresentationId))
            {
                continue;
            }

            string assetName = enemy.PresentationId[(enemy.PresentationId.LastIndexOf('.') + 1)..];
            string path = $"res://game/assets/enemies/{assetName}/battle.png";
            if (ResourceLoader.Load<Texture2D>(path) is Texture2D texture)
            {
                textures.Add(marker.Id, texture);
            }
            else
            {
                GD.PushWarning($"Encounter marker '{marker.Id}' could not load '{path}'.");
            }
        }

        return textures;
    }
}
