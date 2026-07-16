using Godot;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Maps;

namespace RpgGame.Exploration;

/// <summary>Presentation-only contract shared by the small authored exploration maps.</summary>
public interface IExplorationMapView
{
    string MapId { get; }
    Vector2I GuideTile { get; }
    void Initialize(MapQueryService map);
    Vector2 TileToWorld(Vector2I tile);
    bool IsWalkable(Vector2I tile);
    bool TryGetEncounterAt(Vector2I tile, out string encounterId);
    bool TryGetTransitionAt(Vector2I tile, out MapTransitionDefinition? transition);
    void SetClearedEncounterFlags(IReadOnlySet<string> clearedFlagIds);
}
