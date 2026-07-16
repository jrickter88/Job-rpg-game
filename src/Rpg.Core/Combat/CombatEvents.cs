namespace RpgGame.Core.Combat;

/// <summary>
/// Reports the authoritative HP change produced by one accepted damage ability.
/// </summary>
/// <remarks>
/// Presentation can animate from <see cref="PreviousHp"/> to <see cref="CurrentHp"/> without
/// repeating the damage formula. <see cref="Amount"/> is applied damage after remaining-HP
/// clamping, so it never reports more damage than the target actually lost. The selected damage
/// type and target modifier are included so presentation can report affinity without deriving it.
/// </remarks>
public sealed record DamageApplied(
    string ActingCombatantId,
    string TargetCombatantId,
    string AbilityId,
    string DamageTypeId,
    int DamagePercentModifier,
    int Amount,
    int PreviousHp,
    int CurrentHp) : CombatEvent;

/// <summary>Reports the authoritative deduction from one transient combat resource pool.</summary>
public sealed record ResourceSpent(
    string CombatantId,
    string AbilityId,
    string ResourceStatisticId,
    int Amount,
    int PreviousValue,
    int CurrentValue) : CombatEvent;

/// <summary>Reports that one combatant reached zero HP during the resolved action.</summary>
/// <remarks>
/// This is a fact about one combatant. When the defeated combatant was the last living member of
/// its side, a separate <see cref="BattleEnded"/> follows it to describe the battle outcome.
/// </remarks>
public sealed record CombatantDefeated(string CombatantId) : CombatEvent;

/// <summary>Reports the one terminal outcome caused by the just-resolved action.</summary>
/// <remarks>
/// This event is emitted after the damage and combatant-defeated facts that caused it. It remains
/// presentation-neutral and battle-local: victory rewards, encounter clearing, campaign flags,
/// save changes, animation, and sound belong to later application or Godot milestones.
/// </remarks>
public sealed record BattleEnded : CombatEvent
{
    public BattleEnded(BattleOutcome outcome)
    {
        if (outcome == BattleOutcome.InProgress || !Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "A battle-ended event requires PartyVictory or PartyDefeat.");
        }

        Outcome = outcome;
    }

    public BattleOutcome Outcome { get; }
}
