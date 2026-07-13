namespace RpgGame.Core.Content.Definitions;

public sealed record EncounterDefinition : ContentDefinition
{
    public List<EncounterEnemyDefinition> EnemyGroup { get; init; } = [];

    public string? BattlefieldId { get; init; }

    public string? MusicCueId { get; init; }
}

public sealed record EncounterEnemyDefinition
{
    public required string EnemyId { get; init; }

    public required string SlotId { get; init; }
}

