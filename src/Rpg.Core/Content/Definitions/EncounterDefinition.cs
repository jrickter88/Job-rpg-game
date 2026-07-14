namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Defines one reusable enemy formation plus presentation lookup keys for a battle.
/// </summary>
public sealed record EncounterDefinition : ContentDefinition
{
    /// <summary>Enemy templates and their unique positions in this formation.</summary>
    public List<EncounterEnemyDefinition> EnemyGroup { get; init; } = [];

    /// <summary>
    /// Optional stable presentation key for the battle background/arena. Core rules never
    /// turn this key into a Godot resource path.
    /// </summary>
    public string? BattlefieldId { get; init; }

    /// <summary>Optional stable presentation key for music selection.</summary>
    public string? MusicCueId { get; init; }
}

/// <summary>One enemy placement embedded in an encounter formation.</summary>
public sealed record EncounterEnemyDefinition
{
    /// <summary>Stable ID of the enemy template to instantiate.</summary>
    public required string EnemyId { get; init; }

    /// <summary>
    /// Canonical enemy anchor such as <c>formation.enemy.r1.c0</c>. It identifies the
    /// top-front occupied cell; presentation decides how that logical cell maps to pixels.
    /// </summary>
    public required string SlotId { get; init; }
}
