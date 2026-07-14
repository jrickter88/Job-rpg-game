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
- the fixture pack contains 15 records across all nine initial categories;
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

Exit criteria: the example mod adds a class and ability to the same validated catalog,
invalid dependencies/namespaces prevent startup, and save loading detects missing or changed
required mods without launching Godot.

Explicitly excluded: gameplay, mod menus/profiles, hot reload, scripts or assemblies, PCK/ZIP
loading, Steam Workshop, network downloads, signatures, base-record overrides, and a general
behavior language. See `MODDING.md` for the supported contract and installation workflow.

## Milestone 2 — Exploration interaction slice

- Create one small tile map and tile-based player movement with collision.
- Add map entry points and reconstruct the map from `GameState.Location`.
- Add one interactable using a narrow interaction interface/signal.
- Introduce the smallest dialogue schema that supports one NPC exchange.
- Set one event flag and prove it survives a map reload and save/load.

Exit criteria: the player can move through one test room, interact once, leave/reload,
and see persistent state reflected correctly.

## Milestone 3 — First playable battle slice

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
- Add keyboard/controller navigation, placeholder sound, and presentation polish.

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
