namespace RpgGame.Core.Combat;

/// <summary>
/// Pure rules boundary. Implementations must not reference Godot, nodes, animations, or UI.
/// </summary>
public interface ICombatResolver
{
    CombatResolution Resolve(CombatSnapshot current, CombatCommand command);
}

public interface IRandomSource
{
    int Next(int minInclusive, int maxExclusive);
}

public sealed record CombatSnapshot(
    int Round,
    IReadOnlyList<CombatantSnapshot> Combatants);

public sealed record CombatantSnapshot(
    string InstanceId,
    string DefinitionId,
    IReadOnlyDictionary<string, int> Statistics);

public sealed record CombatCommand(
    string ActingCombatantId,
    string AbilityId,
    IReadOnlyList<string> TargetCombatantIds);

public sealed record CombatResolution(
    CombatSnapshot Next,
    IReadOnlyList<CombatEvent> Events);

/// <summary>
/// Presentation adapters will translate concrete domain events into visuals and sound.
/// Concrete event types are intentionally deferred until combat is implemented.
/// </summary>
public abstract record CombatEvent;

