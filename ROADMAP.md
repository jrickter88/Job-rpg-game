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

### Remaining first-playable work

- Implement one deterministic battle resolver with Attack, Guard, HP, speed-based turn
  order, victory, and defeat—no generalized effect scripting.
- Add one hero, one class, two abilities, one enemy, and one fixed encounter.
- Add a minimal command menu and battle presentation that consumes domain events.
- Return battle outcome to the campaign session and set one victory flag.
- Add deterministic rules tests, including boundary and defeat cases.

Exit criteria: exploration can enter a battle, complete it, return to the map, and
retain the result after save/load.

## Milestone 4 — Small vertical slice

- Add a three-character party shell, inventory stacks, equipment, item rewards, a shop,
  and one short quest.
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
player controls one hero, chooses **Attack** or **Guard**, defeats one enemy, returns to
the room, sees the trigger remain cleared, and can save/reload that result. This slice
proves the four riskiest seams—content loading, scene-independent state, pure combat,
and scene presentation—without building the full inventory, class, dialogue, or quest
systems first.
