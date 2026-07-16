# Roadmap

The roadmap grows the game through playable slices. It is not a promise to build every
system before the game can be tested.

## Milestone 0 — Architecture foundation (this repository)

Deliverables:

- Godot 4 C# project and pure .NET core project;
- stable-ID content contracts for the requested initial categories;
- scene-independent save/state DTOs and migration boundary;
- presentation-independent combat command/result boundary;
- headless test project and initial compatibility tests;
- repository, schema, coding, and architecture documentation.

Explicitly excluded: movement, combat behavior, inventory behavior, dialogue,
cutscenes, shops, quests, graphical assets, and production content.

## Milestone 1 — Content pipeline and session shell (implemented)

- Implement JSON loading and aggregated validation for the nine initial categories.
- Add a small command-line content-validation entry point.
- Implement the session service, new-game factory, JSON serializer options, one save
  migration harness, and atomic local save storage.
- Add only enough fixture content to exercise validation and state creation.
- Add CI commands for core tests, content validation, build, and Godot import.

Exit criteria: a headless test can load the complete fixture pack, create a new game,
save it, load it, and prove equivalent state without opening a scene.

Implementation notes:

- one shared loader serves ordinary directories and Godot's `res://` filesystem;
- validation aggregates errors and withholds the catalog if any record is invalid;
- the original fixture pack contained 15 records across all nine initial categories;
- `GameSession`, `NewGameFactory`, `SaveCoordinator`, and `JsonFileSaveStore` complete
  the scene-independent new-game/save/load flow;
- the save serializer preserves unknown fields and owns the ordered migration boundary;
- headless tests exercise the complete exit criterion using a real temporary save file.

This milestone intentionally adds no title screen, save-slot UI, movement, combat,
inventory behavior, dialogue, map art, or other gameplay presentation.
See `MILESTONE_1_GUIDE.md` for the runtime flow, file-by-file responsibilities, and
local validation instructions.

## Milestone 1.5 — Data-mod foundation (implemented)

- Discover loose-folder packages beneath `user://mods` using a strict data-only manifest.
- Validate stable mod IDs, Semantic Versions, one integer game data-API version, folder
  layout, dependency presence, and dependency cycles.
- Load base content plus dependency-ordered mod content through the production validator.
- Reserve one record namespace per mod and reject base-record replacement or ambiguous
  duplicate IDs.
- Store required mod ID/version pairs in save envelopes and reject missing or mismatched
  requirements on load while preserving older unmodded saves.
- Extend the command-line validator and tests with one checked-in example data mod.
- Centralize and test the game's four-hero party maximum without adding party gameplay.
- Separate actor identity from the campaign's selected class, add three vanilla starting
  choices, and let additive mod rules include or exclude vanilla/mod classes deterministically.

Exit criteria: the example mod adds a class and ability to the same validated catalog,
changes the resolved starting-class pool without overwriting vanilla records,
invalid dependencies/namespaces prevent startup, and save loading detects missing or changed
required mods without launching Godot.

Explicitly excluded: a class-selection screen, class unlock gameplay, randomizer behavior,
gameplay, mod menus/profiles, hot reload, scripts or assemblies, PCK/ZIP
loading, Steam Workshop, network downloads, signatures, base-record overrides, and a general
behavior language. See `MODDING.md` for the supported contract and installation workflow.

## Milestone 2 — Exploration interaction slice (implemented)

- Create one small tile map and tile-based player movement with collision.
- Add map entry points and reconstruct the map from `GameState.Location`.
- Add one interactable using a narrow interaction interface/signal.
- Introduce the smallest dialogue schema that supports one NPC exchange.
- Set one event flag and prove it survives a map reload and save/load.

Exit criteria: the player can move through one test room, interact once, leave/reload,
and see persistent state reflected correctly.

Implementation notes:

- `TestRoomView` owns one fixed 12×9 logical tile grid and draws placeholder tiles without art;
- James moves exactly one tile per non-echo key press and cannot enter wall or NPC tiles;
- `ExplorationSceneController` converts accepted Godot input into narrow `GameSession`
  location/flag mutations;
- the guide NPC returns one typed interaction result selecting a validated linear dialogue;
- `flag.test-room.npc-spoken-to` changes the NPC view and survives R reconstruction/save-load;
- no general navigator, authored map format, branching dialogue, or localization layer was added.

See `MILESTONE_2_GUIDE.md` for controls, ownership, reconstruction, and validation details.

### Milestone 2.1 — Manual persistence proof (implemented)

- Make room reconstruction visibly confirm success while preserving in-memory state.
- Add K/L development shortcuts that save and load `slot_1` through the existing coordinator.
- Show success, unused-slot, and exception feedback in the test room.
- Keep these controls behind a narrow injected presentation interface; do not add a save menu,
  new persistence format, global service, or scene-owned campaign state.

Exit criteria: R visibly confirms reconstruction, K writes the current location and event
flags, and L visibly restores them in the running test room.

### Milestone 2.2 — Remappable keyboard controls (implemented)

- Define stable logical actions for movement, interaction/confirmation, and menu/cancel.
- Load validated player bindings from `user://settings/controls.json` and apply them through
  Godot `InputMap`, falling back safely when preferences are missing or malformed.
- Add a small in-game controls panel with immediate rebinding, duplicate rejection, persisted
  changes, reset defaults, and current-binding feedback.
- Keep controls independent from campaign saves, game content, data mods, and `Rpg.Core`.
- Keep R/K/L development proof shortcuts fixed and outside the player binding profile.

Exit criteria: a player can replace movement, interaction, or menu keyboard keys, use the new
bindings immediately, restart the game, and retain those preferences across campaigns.

### Milestone 2.5 — Fixed encounter handoff (implemented)

- Add one fixed encounter trigger to the test room using the validated
  `encounter.forest.slimes-01` definition.
- Raise a typed encounter request after accepted movement enters the trigger.
- Let `GameRoot` replace exploration with a temporary battle-placeholder scene.
- Return to a newly reconstructed exploration scene using the authoritative session state.
- Preserve remappable controls, persistence, and existing exploration behavior.

Exit criteria: James can enter the fixed encounter placeholder, return to a reconstructed
test room at the correct location with existing flags and campaign state intact, avoid an
immediate reconstruction retrigger, and deliberately enter the same encounter again.

Explicitly excluded: combat resolution, HP, Attack, Guard, turn order, victory, defeat,
rewards, AI, random encounters, encounter clearing, a general navigator, and battle saves.
See `MILESTONE_2_5_GUIDE.md` for the exact handoff and manual test route.

### Milestone 2.75 — Battle formation foundation (implemented)

- Define a four-row by four-column enemy formation and a four-row by
  two-column party formation.
- Give encounter enemies canonical formation anchors and authored rectangular
  footprints.
- Validate placement bounds and overlap through pure core rules.
- Render both formations in the battle placeholder.
- Place the active party deterministically without adding persistent formation choices.

Exit criteria: the fixed encounter renders from validated content in the enemy grid,
the current party renders in the party grid, and headless tests prove that multi-cell
enemy footprints cannot overlap or leave the battlefield.

Explicitly excluded: combat commands, targeting, movement, range, row bonuses,
party formation editing, HP, turns, victory, defeat, and rewards. See
`MILESTONE_2_75_GUIDE.md` for coordinates, ownership, validation, and the manual proof.

### Milestone 2.8 — Enemy footprint content (implemented)

- Allow enemy definitions to declare rectangular formation footprints.
- Default omitted footprints to one row by one column.
- Validate positive dimensions against the four-by-four enemy formation.
- Preserve existing base and mod enemy content through an additive default.

Exit criteria: base and mod enemies load with deterministic footprints, and headless tests
prove valid large footprints load while invalid dimensions prevent catalog publication.

Explicitly excluded: encounter coordinates, placement overlap, battle-grid rendering, party
placement, targeting, combat commands, HP, turns, and rewards. See
`MILESTONE_2_8_GUIDE.md` for the authored JSON contract, validation codes, conversion
boundary, and compatibility rationale.

### Milestone 2.85 — Combat statistic resolution (implemented)

- Resolve party combat statistics from actor bases plus the campaign's current class bonuses.
- Resolve enemy combat statistics from authored enemy values.
- Fill omitted statistics from registered statistic defaults.
- Validate derived values against statistic ranges and return immutable deterministic results.
- Preserve stable statistic-ID lookup for future data-authored enemy targeting rules.

Exit criteria: headless tests prove that current party and enemy definitions resolve into
complete, immutable statistic maps and that newly registered statistics automatically
participate without constructing combat state or opening Godot.

Explicitly excluded: current HP/MP, combat snapshots, commands, turns, targeting, enemy AI,
damage, Guard, victory, defeat, rewards, level growth, equipment, statuses, and battle UI.
See `MILESTONE_2_85_GUIDE.md` for the formulas, ownership rules, defensive checks, and
future-AI boundary.

## Milestone 3 — First playable battle slice

### Milestone 3.0 — Initial combat state (implementation review pending)

- Construct transient battle snapshots from campaign progress, encounter content,
  formation placement, and resolved combat statistics.
- Initialize current HP from `stat.max-hp`.
- Resolve party abilities from actor starting abilities and current-class unlocks.
- Preserve deterministic battle-local identity, formation placement, and immutable collections.

Exit criteria: headless tests describe a deterministic initial snapshot containing James
and both green-slime instances with starting HP, statistics, abilities, and formation data.

Explicitly excluded: damage, targeting, commands, Guard resolution, enemy AI, turn order,
victory, defeat, rewards, battle UI, and campaign result handling.

### Milestone 3.05 — Ability and magic framework (implemented)

- Distinguish direct Skills from Magic abilities.
- Add authored magic-discipline containers and class discipline access.
- Project party availability into direct Skills, discipline spell lists,
  and executable ability IDs.
- Preserve existing abilities as Skills through an additive default.
- Validate closed target/ruleset/parameter contracts and derive the flat executable list from
  authoritative Skill/Magic structure.

Exit criteria: focused tests prove that Skills remain direct commands, Magic requires
both individual learning and matching discipline access, multi-discipline spells are
supported, and existing base content remains compatible without defining concrete magic schools.

Explicitly excluded: concrete spell content, battle menus, MP, spell execution,
Silence, Reflect, Hybrid abilities, and combination recipes.

### Milestone 3.06 — Loot-table content foundation (implementation review pending)

- Move item-drop entries out of enemy records into reusable `loot-tables/` definitions.
- Give each enemy one explicit nullable `lootTableId` reference.
- Validate table/item references, independent chances, and inclusive quantity ranges.
- Raise enemy schema to version 2 and the data-mod API to version 3 rather than silently
  accepting the retired embedded `loot` shape.
- Document base and mod authoring with focused nonvisual regression tests.

Exit criteria: the green slime references a typed standalone loot table, valid base and mod
tables publish through `IContentCatalog`, invalid tables withhold the catalog with actionable
diagnostics, and legacy inline enemy loot is rejected deliberately.

Explicitly excluded: random loot rolls, victory rewards, inventory mutation, gold, experience,
stealing, weighted pools, conditional drops, encounter overrides, reward UI, randomizers, and
save-format changes.

### Milestone 3.10 — Single-action physical combat (implementation review pending)

- Let reusable combatant runtime state represent defeat at zero current HP while keeping
  initial snapshot creation positive.
- Add one intrinsic `ability.command.attack` Skill so James can attack with any starting class.
- Validate one explicit actor/ability/single-target command against the current snapshot.
- Resolve the existing free, single-enemy physical-damage contract deterministically.
- Return a new immutable snapshot plus typed damage and defeat events.

Exit criteria: headless tests prove the input snapshot never mutates, damage follows the
documented Strength + power - Defense formula with a one-damage minimum, lethal damage clamps
at zero HP, defeated actors and targets are rejected, and unrelated combatant state/order is
preserved.

Explicitly excluded: Guard execution, current MP or resource payment, rounds, speed ordering,
enemy AI, victory, defeat outcomes, loot rolls, rewards, Godot battle UI, campaign-state
changes, encounter clearing, and battle saves. See `MILESTONE_3_10_GUIDE.md`.

### Milestone 3.12 — Complete deterministic rounds (implementation review pending)

- Require exactly one ordinary command from every combatant alive at round start.
- Order commands by Speed descending, then ordinal battle-local instance ID.
- Skip a pending actor defeated by an earlier action and stop when either side is defeated.
- Advance the round number only when both sides survive the complete command set.
- Add a deterministic enemy planner using the first executable authored ability and the
  lowest-current-HP living party target, with instance ID as the tie-breaker.

Exit criteria: headless tests prove input command order cannot affect action order, Speed ties
are stable, defeated actors lose pending actions, terminal sides stop later actions, base
slimes plan ordinary Tackle commands, and nonterminal results advance exactly one round.

Explicitly excluded: Godot command menus, automatic party-command collection, Guard, class
skills, current MP/resource payment, target retargeting, advanced AI, randomness, victory or
defeat campaign outcomes, encounter clearing, loot rolls, rewards, and battle saves. See
`MILESTONE_3_12_GUIDE.md`.

### Milestone 3.13 — Battle outcome (implementation review pending)

- Derive one closed `BattleOutcome` from the living combatants in each immutable snapshot.
- Distinguish `InProgress`, `PartyVictory`, and `PartyDefeat` without duplicating outcome state.
- Emit one typed `BattleEnded` event after the damage and defeat facts that end a battle.
- Reject single actions and complete rounds submitted after a terminal outcome.

Exit criteria: headless tests prove both terminal outcomes, stable event order, no terminal
event for defeating a nonfinal combatant, rejection through both combat resolution entry
points after battle end, and explicit failure for malformed both-sides-defeated snapshots.

Explicitly excluded: Godot battle presentation, campaign flags, encounter clearing, rewards,
loot rolls, experience, gold, battle saves, new abilities, Guard, current MP, statuses, and
changes to content or save schemas. See `MILESTONE_3_13_GUIDE.md`.

### Milestone 3.14 — Godot playable battle (implementation review pending)

- Replace the return-only formation placeholder with a playable `BattleController` scene.
- Display authoritative current/maximum HP for James and both slimes.
- Collect James's existing `ability.command.attack` and an explicit living enemy target.
- Use `EnemyCommandPlanner` and `CombatRoundResolver` for every resolved round.
- Present typed damage, defeat, and battle-ended events in a readable event log.
- Require confirmation after `PartyVictory` or `PartyDefeat` before leaving battle.

Exit criteria: the player can select Attack, choose either living slime, watch HP and the event
log update from core results, and reach a confirmed terminal outcome without Godot calculating
damage, initiative, defeat, or victory.

Explicitly excluded: Guard, class abilities, magic, items, escape, rewards, animation, sound,
current MP, status effects, battle save/resume, controller support, and polished visuals. See
`MILESTONE_3_14_GUIDE.md`.

### Milestone 3.15 — Campaign handoff (implementation review pending)

- Translate the confirmed terminal battle request at `GameRoot`, not inside the battle scene.
- On victory, set `flag.encounter.forest.slimes-01.cleared` and reconstruct exploration.
- Hide and suppress the fixed marker whenever that authoritative flag is true.
- On defeat, reconstruct exploration without setting the clearance flag.
- Preserve the clearance fact through the existing save/load pipeline.

Exit criteria: victory returns James to the test room and permanently suppresses the fixed
encounter through reconstruction and save/load; defeat returns without clearing it and permits
a deliberate retry after stepping off and back onto the marker.

Explicitly excluded: generic encounter-progress records, random encounters, rewards, loot
resolution, experience, gold, inventory mutation, map navigation, and battle persistence. See
`MILESTONE_3_15_GUIDE.md`.

## Milestone 4 — Small vertical slice

### Milestone 4.0 — Persistent inventory stacks

- Add one persistent item stack per stable item ID to `GameState`.
- Validate quantities against `ItemDefinition.MaxStack`.
- Add narrow add, remove, and quantity-query use cases.
- Preserve inventory through new-game and save/load flows.

Exit criteria: headless tests prove inventory additions and removals are atomic, stack limits
are enforced, unrelated campaign state is preserved, and old saves without inventory load as
empty inventories.

Explicitly excluded: loot rolls, victory rewards, item-use effects, equipment, shops, gold,
sorting UI, battle items, and inventory presentation. See `MILESTONE_4_0_GUIDE.md`.

### Milestone 4.1 - Deterministic loot resolution

- Resolve defeated enemy definition IDs through their reusable loot tables.
- Evaluate independent entries in authored order using injected deterministic randomness.
- Return ordered typed item awards without changing campaign state or inventory.

Exit criteria: headless tests prove chance and inclusive quantity boundaries, duplicate award
preservation, deterministic scripted rolls, input ordering, immutable results, and clear
missing/wrong-category failures.

Explicitly excluded: inventory mutation, victory handoff, reward UI, encounter changes,
experience, gold, stack overflow handling, aggregation, save changes, weighted pools, stealing,
and conditional drops. See `MILESTONE_4_1_GUIDE.md`.

### Milestone 4.2 - Victory rewards and reward summary

- Carry ordered defeated enemy definitions through confirmed battle completion.
- Resolve loot exactly once after confirmed party victory.
- Apply all item awards atomically through the persistent inventory boundary.
- Set encounter clearance only after successful reward application.
- Present an aggregated reward summary before returning to exploration.

Exit criteria: defeating the fixed slimes can produce deterministic loot awards, accepted
awards enter persistent inventory exactly once, defeat grants nothing, duplicate completion
cannot farm rewards, and the player confirms a reward summary before exploration resumes.

Explicitly excluded: experience, gold, inventory UI, item use, equipment, shops, autosave,
reward animation, overflow-resolution UI, and loot-table schema changes. See
`MILESTONE_4_2_GUIDE.md`.

### Milestone 4.3 - Typed damage and percentage affinities

- Add code-owned Slash, Energy, Fire, Ice, and Lightning damage IDs.
- Let damage abilities select one type and enemies author sparse signed percentage modifiers.
- Copy enemy affinities into immutable battle snapshots and apply them deterministically.
- Include authoritative type/reaction facts in damage events and the battle log.
- Validate mixed weapon damage profiles while equipment activation remains deferred.

Exit criteria: headless tests prove neutral damage, variable weakness and resistance values,
immunity, final rounding, immutable snapshot projection, explicit compatibility defaults, and
weapon profiles composed from supported types totaling exactly 100.

Explicitly excluded: equipment ownership or activation, weapon attack splitting, magical
statistics, statuses, affinity inspection UI, critical hits, absorption, reflection, and
mod-defined damage types. See `MILESTONE_4_3_GUIDE.md`.

### Milestone 4.4 - Data-driven battle command menu

- Project an acting party combatant's direct Skills and unlocked Magic disciplines into the
  battle menu.
- Select authored ability IDs, enter authored supported target modes, and submit ordinary
  combat commands through the existing round resolver.
- Preserve deterministic focus order and remappable input; use temporary stable-ID-derived
  names.

Exit criteria: the battle controller no longer has a required Attack-specific command path and
can select every currently executable, affordable direct Skill or nested Magic ability.

Explicitly excluded: new abilities, magic disciplines, Guard execution, healing, items,
equipment, multi-party command queues, command icons, localization, and new target modes. See
`MILESTONE_4_4_GUIDE.md`.

### Milestone 4.5 - Current MP and ability cost payment

- Add transient current MP to combatant snapshots and initialize it from resolved maximum MP.
- Support null/zero costs and `stat.max-mp` costs, rejecting unsupported or unaffordable
  commands without state changes.
- Deduct MP atomically with an ability effect, emit `ResourceSpent`, and present MP in battle.

Exit criteria: an affordable MP-costing ability spends MP exactly once, emits an authoritative
event before its effect, and an unaffordable command changes neither HP nor MP.

Explicitly excluded: new spell content, magic damage formulas, healing, MP recovery,
out-of-battle resources, save persistence, and additional mutable resource families. See
`MILESTONE_4_5_GUIDE.md`.

Remaining vertical-slice work will then:

- Add a three-character party shell, equipment, item rewards, a shop, and one short quest.
- Add a limited set of common ability effects and status effects based on actual slice
  content.
- Add one short scripted cutscene and map transition.
- Extend menu navigation to controllers, then add placeholder sound and presentation polish.

Exit criteria: a 10–15 minute start-to-finish slice includes exploration, dialogue, a
quest, a shop/equipment choice, two encounters, a boss, and reliable save/load.

## Later expansion gates

Only after the vertical slice is fun and stable:

- class learning/equipping and multi-class combinations;
- richer status timing, elemental interactions, and enemy AI;
- scalable encounter tables and more map tooling;
- quest journal, party formation, equipment comparison, and shop polish;
- dialogue/cutscene authoring improvements driven by actual writing volume;
- localization pipeline, accessibility, performance budgets, and platform exports.

## Concise first-playable proposal

Build one gray-box room containing a save point and a single fixed enemy trigger. The
player controls one hero, uses **Attack** to defeat one enemy, returns to the room, sees
the trigger remain cleared, and can save/reload that result. This slice
proves the four riskiest seams—content loading, scene-independent state, pure combat,
and scene presentation—without building the full inventory, class, dialogue, or quest
systems first.
