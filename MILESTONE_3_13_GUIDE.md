# Milestone 3.13 guide — battle outcome

## What this milestone adds

Milestone 3.13 gives the pure combat core one authoritative answer to a small question: is the
battle still running, did the party win, or did the party lose?

```csharp
BattleOutcome outcome = snapshot.Outcome;
```

The closed values are:

| Value | Meaning |
|---|---|
| `InProgress` | Both the party and enemy side still have at least one living combatant. |
| `PartyVictory` | The party has a living combatant and the enemy side has none. |
| `PartyDefeat` | The enemy side has a living combatant and the party has none. |

This milestone itself was headless functionality. Milestone 3.14 now presents the derived
outcome in Godot, and Milestone 3.15 translates a confirmed victory into one campaign flag.
The outcome rules documented here remain pure core behavior.

## Why outcome is derived

`CombatSnapshot` already owns every combatant's current HP. Storing another mutable or
constructor-supplied outcome would create two possible sources of truth:

```text
enemy HP says zero       outcome says InProgress
```

Instead, `CombatSnapshot.Outcome` checks which `BattleSide` still has a combatant above zero.
Every immutable replacement snapshot therefore reports the matching outcome automatically.
There is no outcome setter and no string status for a caller to mistype.

Current rules damage only one target at a time, so they cannot defeat both sides in the same
action. A manually constructed snapshot with both sides defeated is treated as malformed and
throws `InvalidDataException`. Choosing victory or defeat arbitrarily would hide the bad state,
and adding a Draw value would expand the requested game design without a real rule that needs
it.

## Typed end event and event order

When an accepted action defeats the final living member of a side, `CombatResolver` returns:

```text
DamageApplied
CombatantDefeated
BattleEnded(PartyVictory or PartyDefeat)
```

That order describes cause before result. A future Godot coordinator can animate the HP loss,
show the defeated combatant, and only then present victory or defeat. It does not need to
repeat damage or outcome calculations.

Defeating one enemy while another enemy remains alive emits `DamageApplied` and
`CombatantDefeated`, but not `BattleEnded`. `BattleEnded` rejects `InProgress` and undefined
enum values at construction because an end event must carry a real terminal outcome.

`CombatRoundResolver` does not manufacture a duplicate end event. It aggregates the event from
the terminal action, observes the terminal replacement snapshot, and stops pending commands.

## Commands after battle end

Both supported resolution entry points defend their boundary:

- `CombatResolver.Resolve` rejects another individual action with
  `combat.command.battle-already-ended`;
- `CombatRoundResolver.ResolveRound` rejects another round with
  `combat.round.battle-already-ended`.

These checks happen before actor, target, or command-collection validation. A caller therefore
receives the actual reason the request is invalid, and cannot resolve a terminal snapshot to
emit another `BattleEnded` event.

`EnemyCommandPlanner` remains an intent-selection helper rather than an execution entry point.
The action and round boundaries remain authoritative even if a caller planned commands too
early or retained a stale command.

## Ownership and compatibility

Battle outcome belongs to the transient `CombatSnapshot` in `Rpg.Core`:

- it is not authored JSON content;
- it is not a field in `GameState` or a save file;
- it contains no Godot type;
- it does not grant loot, experience, gold, or items;
- it does not clear an encounter or set a campaign flag.

Consequently this milestone requires no content schema version, mod data-API version, save
format version, or migration change. Base content, data mods, and existing saves are unchanged.
A later application milestone must explicitly translate `BattleEnded` into campaign progress
and presentation; this core event does not silently do either job.

## Automated coverage

Focused headless tests prove:

- a fresh battle reports `InProgress`;
- defeating the final enemy reports `PartyVictory`;
- defeating the final party member reports `PartyDefeat`;
- the terminal event follows damage and combatant defeat;
- defeating a nonfinal enemy does not end the battle;
- individual actions and complete rounds reject terminal snapshots;
- terminal rounds stop pending actions and retain their current round number;
- `BattleEnded` rejects nonterminal and unknown values;
- a malformed both-sides-defeated snapshot does not choose an arbitrary outcome.

## Local validation

Run from the repository root in PowerShell, stopping after any failure:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content examples/mods

dotnet build RpgGame.sln

& "D:\Godot\Godot_v4.7-stable_mono_win64.exe" `
    --headless `
    --editor `
    --path . `
    --quit

if ($LASTEXITCODE -ne 0) {
    throw "Godot validation failed with exit code $LASTEXITCODE"
}
```

Each command must finish with exit code `0`. The later playable-battle manual checks are listed
in `MILESTONE_3_14_GUIDE.md` and `MILESTONE_3_15_GUIDE.md`.

## Explicitly deferred

- playable Godot battle controls and HP presentation;
- campaign victory flags and encounter clearing;
- loot rolls, inventory mutation, rewards, experience, and gold;
- battle save/resume;
- Guard, class skills, current MP, status effects, and additional ability rulesets;
- draws, escape, surrender, scripted battle endings, and simultaneous defeat rules.
