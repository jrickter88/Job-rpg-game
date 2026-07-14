using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.State;

/// <summary>
/// Creates a valid initial campaign snapshot from immutable actor/class content.
/// </summary>
public sealed class NewGameFactory
{
    private static readonly HashSet<string> ValidFacings =
        new(StringComparer.Ordinal) { "north", "east", "south", "west" };

    private readonly IContentCatalog _content;

    public NewGameFactory(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>
    /// Builds fresh actor progress, active-party order, location, and empty event flags.
    /// Invalid setup is rejected here rather than becoming a corrupt save later.
    /// </summary>
    public GameState Create(NewGameRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SaveId);

        if (!ContentId.IsValid(request.StartingMapId)
            || !request.StartingMapId.StartsWith("map.", StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Starting map '{request.StartingMapId}' must be a canonical map.* ID.",
                nameof(request));
        }

        if (!ValidFacings.Contains(request.StartingFacing))
        {
            throw new ArgumentException(
                $"Starting facing '{request.StartingFacing}' is not north/east/south/west.",
                nameof(request));
        }

        PartyRules.ValidateMemberCount(request.StartingActorIds.Count, nameof(request));

        var progress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal);
        foreach (string actorId in request.StartingActorIds)
        {
            if (progress.ContainsKey(actorId))
            {
                throw new ArgumentException(
                    $"Starting actor '{actorId}' is listed more than once.",
                    nameof(request));
            }

            ActorDefinition actor = _content.GetRequired<ActorDefinition>(actorId);
            _content.GetRequired<ClassDefinition>(actor.StartingClassId);

            progress.Add(actorId, new ActorProgressState
            {
                ActorId = actor.Id,
                ClassId = actor.StartingClassId,
                Level = actor.StartingLevel,
                Experience = 0,
            });
        }

        return new GameState
        {
            SaveId = request.SaveId,
            Location = new MapLocationState
            {
                MapId = request.StartingMapId,
                X = request.StartingX,
                Y = request.StartingY,
                Facing = request.StartingFacing,
            },
            ActivePartyActorIds = [.. request.StartingActorIds],
            ActorProgress = progress,
            EventFlags = new Dictionary<string, bool>(StringComparer.Ordinal),
        };
    }
}
