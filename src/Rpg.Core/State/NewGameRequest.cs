namespace RpgGame.Core.State;

/// <summary>
/// Game-specific starting choices supplied to the reusable new-game factory.
/// </summary>
public sealed record NewGameRequest
{
    /// <summary>Unique identity assigned to this new playthrough.</summary>
    public required string SaveId { get; init; }

    /// <summary>Stable initial map ID.</summary>
    public required string StartingMapId { get; init; }

    /// <summary>Starting horizontal tile coordinate.</summary>
    public int StartingX { get; init; }

    /// <summary>Starting vertical tile coordinate.</summary>
    public int StartingY { get; init; }

    /// <summary>Initial logical facing direction.</summary>
    public string StartingFacing { get; init; } = "south";

    /// <summary>Actor definition IDs in initial active-party order.</summary>
    public List<string> StartingActorIds { get; init; } = [];
}
