# Milestone 5.0 - Deterministic ATB Timeline Initiative

## Model

Combat is a deterministic wait-mode timeline. `CombatSnapshot.TimelineTime` is transient and
each `CombatantSnapshot.NextActionTime` is an absolute integer position. The ready actor is the
living combatant with the lowest next action time. Ties use higher effective Speed, Party before
Enemy, and ordinal instance ID.

After a successful action, the actor is rescheduled with a smoothed integer delay:

```text
ActionDelay = max(1, 1000 / (max(1, EffectiveSpeed) + 4))
```

The current implementation clamps Speed to at least one and rejects missing Speed as malformed
combat state. The timeline advances to the ready actor's action time only after command legality
and effect resolution succeed. A rejected command therefore changes no HP, MP, timeline time, or
next action time.

Opening initiative uses `max(0, 100 - Speed * 5)` as each combatant's initial next action time.
This keeps ordinary speed differences close together while allowing a truly exceptional speed to
take a second turn before a slow actor's first turn.

## Wait-mode behavior

The timeline never advances while a player command menu or target selector is open. The Godot
battle controller displays the ready party actor's menu. Once confirmed, it resolves one ordinary
`CombatCommand`, reschedules that actor, and processes ready enemy actors through the same
`CombatTimelineResolver` until a party actor is ready again or the battle ends.

## Preview

`TurnOrderPreviewService` forecasts eight upcoming actor instances using current living actors,
their next action times, and normal speed delays. It does not mutate the snapshot and does not
predict damage, target choices, deaths, MP, or status changes. It is an initiative forecast, not a
guarantee. `BattleController` presents the forecast and rebuilds it after every action.

## Future extension point

Future Haste, Slow, Stop, Stun, Delay Strike, and Quick rules should modify or replace an actor's
next action time through a core timeline operation, then let the preview recalculate. They should
not add clocks, frame-time polling, or UI-owned scheduling. A full status framework and production
turn-manipulation content remain deferred.

## Compatibility and deferrals

Timeline fields are transient combat state and are not added to `GameState` or saves. The legacy
`Round` property and `CombatRoundResolver` remain as compatibility surface for older headless
callers while the live battle path uses `CombatTimelineResolver`; new battle code must use the
timeline interface. No content or mod API version changes are required.

Deferred: real-time ATB bars, active mode, live enemy actions during menus, Haste/Slow/Stop/Stun,
status durations, poison/regen ticks, production delay/quick abilities, boss scripting, combos,
new classes, animation, sound, shops, inventory UI, equipment UI changes, and new maps.

## Validation

The implementation was validated with:

```text
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj --no-restore
Passed! - Failed: 0, Passed: 369, Skipped: 0, Total: 369

dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
Content validation passed: 41 definitions loaded

dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content examples/mods
Content validation passed: 44 definitions loaded

dotnet build RpgGame.sln --no-restore
Build succeeded. 0 Warning(s), 0 Error(s).

D:\Godot\Godot_v4.7-stable_mono_win64.exe --headless --editor --path . --quit
Exit code: 0
```

No interactive Godot playtest or screenshot capture was run in this ticket. The recommended
manual check is to start the fixed slime encounter, confirm James's command menu pauses the
battle, watch the Turn Order forecast refresh after each action, and continue until victory and
the existing reward/clearance handoff complete.
