namespace RpgGame.Core.Combat;

/// <summary>
/// Pure command-processing boundary for one deterministic combat state transition.
/// </summary>
/// <remarks>
/// Implementations receive all authoritative input and return data describing the outcome.
/// They must not reference Godot, Nodes, animations, audio, or UI. That separation allows
/// combat rules to run in tests, AI simulations, or balancing tools without rendering.
/// </remarks>
public interface ICombatResolver
{
	/// <summary>
	/// Applies one validated intent to the current snapshot and returns the next snapshot
	/// plus ordered domain events that presentation can animate.
	/// </summary>
	CombatResolution Resolve(CombatSnapshot current, CombatCommand command);
}

/// <summary>
/// Injectable random-number boundary used by rules that require chance.
/// </summary>
/// <remarks>
/// Production can use a seeded generator while tests supply a predictable fake. Combat
/// should never call a global random API because that would make failures hard to reproduce.
/// </remarks>
public interface IRandomSource
{
	/// <summary>
	/// Returns an integer in the half-open range [<paramref name="minInclusive"/>,
	/// <paramref name="maxExclusive"/>), matching standard .NET range semantics.
	/// </summary>
	int Next(int minInclusive, int maxExclusive);
}

/// <summary>
/// Complete authoritative rules snapshot needed to process the next combat command.
/// </summary>
/// <param name="Round">Current logical round number; exact turn semantics are deferred.</param>
/// <param name="Combatants">Combatants in explicit deterministic order.</param>
public sealed record CombatSnapshot(
	int Round,
	IReadOnlyList<CombatantSnapshot> Combatants);

/// <summary>
/// Rules-facing state for one party member or enemy instance in a specific battle.
/// </summary>
/// <param name="InstanceId">
/// Unique battle-local ID, allowing two enemies created from the same definition.
/// </param>
/// <param name="DefinitionId">Actor or enemy content ID from which this instance came.</param>
/// <param name="Statistics">Current authoritative values keyed by statistic ID.</param>
public sealed record CombatantSnapshot(
	string InstanceId,
	string DefinitionId,
	IReadOnlyDictionary<string, int> Statistics);

/// <summary>
/// Player- or AI-authored intent to use one ability on explicitly selected targets.
/// </summary>
/// <param name="ActingCombatantId">Battle-local instance performing the action.</param>
/// <param name="AbilityId">Stable ability definition ID.</param>
/// <param name="TargetCombatantIds">Battle-local targets in intentional order.</param>
/// <remarks>
/// A future resolver validates legality, costs, targeting, and turn ownership. Presentation
/// builds commands but cannot directly subtract HP or otherwise author an outcome.
/// </remarks>
public sealed record CombatCommand(
	string ActingCombatantId,
	string AbilityId,
	IReadOnlyList<string> TargetCombatantIds);

/// <summary>
/// Atomic output of resolving one command.
/// </summary>
/// <param name="Next">Authoritative snapshot after all rules are applied.</param>
/// <param name="Events">
/// Ordered facts describing what happened, consumed by UI/animation and useful in tests.
/// </param>
public sealed record CombatResolution(
	CombatSnapshot Next,
	IReadOnlyList<CombatEvent> Events);

/// <summary>
/// Presentation adapters will translate concrete domain events into visuals and sound.
/// Concrete event types are intentionally deferred until combat is implemented.
/// </summary>
/// <remarks>
/// Future examples might be DamageApplied or CombatantDefeated. A typed hierarchy avoids
/// fragile string messages while preventing core events from containing animation details.
/// </remarks>
public abstract record CombatEvent;
