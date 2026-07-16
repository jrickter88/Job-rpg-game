# Milestone 4.5 - Current MP and ability cost payment

## Runtime state

`CombatantSnapshot` now carries immutable replacement-state `CurrentMp` beside `CurrentHp`.
`MaximumMp` is read from resolved `stat.max-mp`, defaulting to zero when absent. At battle
creation, `CurrentMp` initializes to that maximum. Every snapshot enforces:

```text
0 <= CurrentMp <= MaximumMp
```

`WithCurrentMp` preserves placement, statistics, ability availability, HP, and affinity maps.
Combat snapshots remain transient and are not saved.

## Supported costs

For this milestone, an ability may use only:

- `costStatisticId: null` with `costAmount: 0`; or
- `costStatisticId: "stat.max-mp"` with a nonnegative `costAmount`.

The content ID identifies the MP resource family for compatibility; it does not mean that the
maximum statistic itself changes. The mutable pool is `CurrentMp`. Unsupported resource IDs
fail command validation clearly.

Before applying an ability, `CombatResolver` validates affordability. A rejected command does
not change HP or MP. A successful MP-costing command replaces the actor with its reduced MP in
the same immutable resolution as the effect and emits:

```csharp
ResourceSpent(
    string CombatantId,
    string AbilityId,
    string ResourceStatisticId,
    int Amount,
    int PreviousValue,
    int CurrentValue)
```

`ResourceSpent` precedes the effect event, such as `DamageApplied`. Enemy planning uses the
same affordability predicate as player commands, so there is no AI-only payment path.

## Presentation and deferrals

The battle screen presents current/maximum MP and records authoritative resource-spending
events in its log. It does not mutate MP itself.

No new content, spells, magic formula, healing, status effect, save field, out-of-battle MP,
restoration, or additional resource family is added here. Future resource pools should extend
the closed cost-resource mapping and snapshot invariants rather than treating arbitrary
statistics as mutable values.
