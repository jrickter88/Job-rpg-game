# Repository guidance

This repository is the foundation of a single-player, 2D, turn-based JRPG made in
Godot 4 with C#. Keep it understandable and productive for one developer. It is not
a general-purpose RPG engine.

## Read before changing code

1. Read `ARCHITECTURE.md` for dependency and ownership rules.
2. Read `CONTENT_SCHEMA.md` before adding or changing content records.
3. Read `CODING_STANDARDS.md` before adding C# or Godot scenes.
4. Check the current milestone and explicit deferrals in `ROADMAP.md`.
5. Read `MILESTONE_1_GUIDE.md` before changing startup, content loading, session, or saves.
6. Read `MODDING.md` before changing mod manifests, discovery, namespaces, or compatibility.
7. Read `STARTING_CLASS_GUIDE.md` before changing actor/class ownership or new-game class choices.
8. Read `MILESTONE_2_GUIDE.md` before changing exploration movement, interaction, or dialogue.
9. Read `CONTROLS_GUIDE.md` before adding actions or changing input/remapping behavior.
10. Read `MILESTONE_2_5_GUIDE.md` before changing encounter triggers or the temporary
    exploration/encounter scene handoff.
11. Read `MILESTONE_2_75_GUIDE.md` before changing battle formation coordinates,
    footprints, encounter slot IDs, or formation presentation.
12. Read `MILESTONE_2_8_GUIDE.md` before changing enemy footprint content, its
    validation diagnostics, or its conversion into core formation geometry.
13. Read `MILESTONE_2_85_GUIDE.md` before changing combat statistic calculation,
    combatant initialization, actor/class/enemy combat values, AI statistic queries,
    or future target ranking.
14. Read `MILESTONE_3_0_GUIDE.md` before changing initial combat snapshots, starting
    current HP, or combatant ability availability.
15. Read `ABILITY_MAGIC_FRAMEWORK_GUIDE.md` before changing Skill/Magic classification,
    magic-discipline definitions, class discipline access, or party ability projection.
16. Read `ABILITY_AUTHORING_GUIDE.md` and `MAGIC_AUTHORING_GUIDE.md` before adding or
    granting abilities, Skills, spells, or magic disciplines.
17. Read `ABILITY_RULESET_DEVELOPER_GUIDE.md` before adding a target mode, ruleset ID,
    numeric parameter contract, or executable ability behavior.
18. Read `LOOT_TABLE_AUTHORING_GUIDE.md` before adding enemy drops, loot-table records,
    or future reward-resolution behavior.

When a requested feature conflicts with those documents, update the relevant design
document in the same change or explain why the exception is temporary.

## Non-negotiable boundaries

- `src/Rpg.Core` must compile as plain .NET and must never reference Godot.
- `src/Rpg.Game` may reference Godot and `RpgGame.Core`. It owns adapters, scene
  controllers, application composition, and presentation coordination.
- `game/` owns game-specific JSON content, maps, scenes, localization, audio, and art.
- Combat and other rules return state changes/domain events. They never play
  animations, locate nodes, or update controls.
- Persistent state lives in `GameState` or feature state owned by it, never in a map,
  combat scene, UI control, or transient node.
- Do not add a service locator or an unrestricted global singleton. Composition-root
  services must have narrow interfaces and explicit lifetime documentation.

## Content rules

- Every top-level content record has a stable lowercase string `id` and a
  `schemaVersion`.
- IDs are permanent once used in a published build or save. Do not recycle them.
- Cross-record relationships use IDs, never file paths, array indexes, display names,
  or Godot node paths.
- Content is UTF-8 JSON, one record per file, in the category documented by
  `CONTENT_SCHEMA.md`.
- A new reference field requires validation for missing targets and invalid categories.
- Prefer explicit fields over an open-ended behavior language. Unique mechanics may
  use a small code-owned ruleset selected by a stable `rulesetId`.
- Ability target modes, rulesets, and numeric parameters are closed code-owned contracts.
  Data may select and tune supported contracts; a new JSON string never creates behavior.
- Data mods are loose JSON packages only. Do not add script, assembly, PCK, remote
  download, Workshop, or arbitrary file-loading support without a later explicit milestone.
- A mod ID uses `mod.author.mod-name`; every record it owns uses the matching namespace
  after its category, such as `ability.author.mod-name.fire`.
- Enemy drops belong in reusable `loot-tables/` records. Enemy definitions reference those
  records by stable ID and never embed item-drop arrays.

## Implementation discipline

- Implement only the current vertical slice. Add abstractions after a second real use
  case appears, unless a boundary is required for deterministic testing or platform IO.
- Prefer feature folders and small collaborators over manager classes.
- Major systems communicate through an owned interface, a typed domain event, or a
  Godot signal at the presentation boundary. Do not use stringly typed event buses.
- A scene may cache its own child-node references. It must not search the global tree
  for another system.
- Keep simulation input explicit and inject randomness behind `IRandomSource`.
- Never depend on iteration order for combat or other deterministic rules.
- Do not hand-edit generated files beneath `.godot/` or commit build/export output.

## Tests and validation

For nonvisual changes, run:

```bash
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

For project integration, when the matching Godot Mono editor is available, run:

```bash
dotnet build RpgGame.sln
godot --headless --editor --path . --quit
```

Add a regression test for every fixed rules or save-migration bug. Content changes
must pass the content validator before they can be merged. If a required
tool is unavailable, run all remaining checks and state exactly what could not run.

## Definition of done

- The dependency rules still hold.
- Stable IDs and save compatibility have been considered.
- Nonvisual behavior has focused automated tests.
- Relevant documentation and schemas match the code.
- No unrelated systems or speculative framework code were introduced.
