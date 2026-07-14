# Coding standards

The repository baseline is Godot 4.7 .NET, .NET 8, and C# 12. Use the matching .NET
edition of the Godot editor; standard editor builds cannot compile C# projects.

## C# baseline

- Target .NET 8 and C# 12, matching the current project files.
- Keep nullable reference types and warnings-as-errors enabled.
- Use file-scoped namespaces and four spaces; follow `.editorconfig`.
- Prefer small immutable records for commands, snapshots, definitions, and events.
- Use concrete `List`/`Dictionary` types on serialized DTOs and read-only interfaces at
  rule boundaries.
- Validate at boundaries. Internal rule code may assume it received validated content
  and legal commands.
- Use `Async` suffixes and accept `CancellationToken` on platform IO.
- Avoid reflection, runtime type scanning, and dependency injection frameworks until a
  measured need exists.

## Naming

- Types, methods, events, and public properties: `PascalCase`.
- Parameters, locals, and private fields: `camelCase`; private fields use `_camelCase`.
- Interfaces use `I`; asynchronous methods end in `Async`.
- `Definition` means immutable content, `State` means persistent/runtime data,
  `Command` means requested intent, and `Event` or `Result` means an outcome.
- IDs use the stable format in `CONTENT_SCHEMA.md`. Never pass display names as IDs.

## Core rules

- `RpgGame.Core` must not import `Godot` or use scene/resource paths.
- Core methods receive all meaningful inputs explicitly. Time, randomness, and storage
  cross interfaces so tests control them.
- Use integer or decimal arithmetic with explicit rounding for gameplay. Do not let
  frame delta or floating-point display interpolation affect authoritative outcomes.
- Sort explicitly before order affects results. Dictionaries are lookup structures,
  not turn-order structures.
- Return state and typed outcomes; do not call UI, animation, audio, or scene code.
- Do not create a universal entity/component system, generic effect language, or event
  bus for hypothetical reuse.

## Godot integration

- Godot scripts live under `src/Rpg.Game`; `.tscn` and other authored resources live
  under `game/`.
- Keep nodes focused on input, presentation, and coordination. Put testable decisions
  in the core.
- Connect signals in the owning scene or coordinator and disconnect according to node
  lifetime. Use typed C# signals/delegates where possible.
- Use exported node references or unique names within an owned scene. Never search the
  entire scene tree for a service.
- Change scenes through one navigator once navigation exists. Do not scatter hardcoded
  `res://` transitions through gameplay nodes.
- Load presentation resources by a presentation catalog keyed by stable IDs, not from
  core definitions containing raw resource paths.
- Do not make nodes persistent merely to preserve data; preserve data in `GameState`.

## Content and serialization

- JSON properties are `camelCase`; files are UTF-8 with a final newline.
- Content records and save DTOs do not serialize Godot objects, delegates, interfaces,
  or runtime node references.
- A new required content field needs a category schema migration or a defensible
  default. New save fields must be optional for older saves.
- Never delete a released save migration. Migration tests use checked-in fixtures once
  the first public save format exists.
- Content loaders aggregate actionable validation errors rather than failing on only
  the first file.
- Validate party size through `PartyRules`. Do not scatter literal `4` checks through future
  recruitment, menus, or combat code.
- Treat a mod manifest ID and version as a compatibility contract. Never derive either
  from display text or silently rewrite it during discovery.
- Data mods contain JSON definitions only. Do not deserialize type names, invoke authored
  paths, load assemblies/native libraries, or introduce a generic expression language.
- Mod ordering is explicit dependency order with ordinal ID tie-breaking. Filesystem
  enumeration order must never affect the combined catalog.

## Testing

- Name tests `MethodOrRule_Condition_ExpectedOutcome`.
- Use arrange/act/assert without comments when the phases are already obvious.
- Use fake or seeded randomness; tests must never be probabilistically flaky.
- Test observable rules and compatibility, not private implementation details.
- Prefer parameterized tests for formula boundaries and content validation cases.
- A Godot scene test does not replace a pure rule test.

## Change size

Keep each change aligned to one playable capability or foundation needed directly by
it. Update architecture/schema documents when a public contract changes. Do not mix
large content drops, refactors, and new mechanics unless they cannot be reviewed
separately.
