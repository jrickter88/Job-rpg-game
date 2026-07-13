namespace RpgGame.Core.Content;

/// <summary>
/// Base contract for every addressable game-content record.
/// </summary>
public abstract record ContentDefinition
{
    public int SchemaVersion { get; init; } = 1;

    public required string Id { get; init; }
}

