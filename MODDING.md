# Data modding contract

## Scope of Milestone 1.5

The game supports **data-only, loose-folder mods**. A mod may add records in the same nine
JSON categories as the base game: actors, classes, statistics, items, equipment, abilities,
enemies, encounters, and quests. Those definitions enter the same typed catalog and pass the
same strict validation as built-in content.

This milestone does **not** load C# assemblies, native libraries, GDScript, executable hooks,
PCK/ZIP packages, Steam Workshop items, URLs, or arbitrary behavior expressions. It does not
add a mod browser, enable/disable screen, hot reload, or gameplay. Every immediate valid
package beneath the mods directory is enabled for that process.

This narrow boundary gives community authors useful class, ability, item, enemy, encounter,
and quest data without turning the first playable milestones into a plugin platform or
exposing players to community code execution.

## Package layout

Each mod is one immediate child folder whose name exactly matches its stable manifest ID:

```text
user://mods/
└── mod.example.starter-pack/
    ├── manifest.json
    └── content/
        ├── abilities/
        │   └── temporal-guard.json
        └── classes/
            └── chronoguard.json
```

`user://` is Godot's per-user writable data location, not a directory in the repository. In
the editor, use **Project → Open User Data Folder** (wording may vary slightly by Godot
version), create `mods`, and copy the complete mod folder into it. Keeping community files
outside `res://` means an exported game's built-in pack can remain read-only.

The checked-in `examples/mods/mod.example.starter-pack` package is a working template. It is
not loaded automatically from the repository; it exists for documentation and validation.

## Manifest schema version 1

Every property is explicit. Unknown fields, missing required fields, wrong types, JSON `null`
where an array/string is expected, and unsupported versions are errors.

```json
{
  "schemaVersion": 1,
  "id": "mod.example.starter-pack",
  "name": "Example Starter Pack",
  "version": "1.0.0",
  "gameApiVersion": 1,
  "dependencies": []
}
```

| Field | Meaning |
|---|---|
| `schemaVersion` | Shape of `manifest.json`; Milestone 1.5 supports exactly `1`. |
| `id` | Permanent lowercase ID in `mod.author.mod-name` form. It must equal the folder name. |
| `name` | Nonblank human-facing name. It is never used as identity. |
| `version` | Author-controlled Semantic Version such as `1.0.0` or `1.1.0-beta.1`. |
| `gameApiVersion` | Integer data contract supported by the game; currently exactly `1`. |
| `dependencies` | Stable IDs of mods that must be installed and loaded first. |

`gameApiVersion` is separate from the executable's build string. Ordinary game releases do
not invalidate all mods; this integer changes only when the public data contract becomes
incompatible. A mod's own `version` should change whenever its published data changes.

## Record ownership and references

Remove the leading `mod.` from the manifest ID to obtain the mod's content namespace. For
`mod.example.starter-pack`, each owned ID must begin with its category plus
`example.starter-pack`:

```text
class.example.starter-pack.chronoguard
ability.example.starter-pack.temporal-guard
item.example.starter-pack.time-shard
enemy.example.starter-pack.clockwork-slime
```

A mod cannot declare `item.consumable.potion` or otherwise replace base content. It also
cannot declare another author's namespace. Duplicate IDs are always validation errors, so
installation or filesystem order never chooses a winner.

A record may reference:

- another record in the same mod;
- a base-game record, such as `stat.defense`;
- a record from a mod named in `dependencies`.

The validator proves that every reference exists, has the right content category, and—when
it crosses from one mod to another—that the target mod is listed as a direct dependency.

## Deterministic loading and failure behavior

Startup performs these steps:

1. inspect each immediate folder beneath `user://mods`;
2. parse and validate every manifest;
3. reject missing dependencies, duplicate IDs, self-dependencies, and dependency cycles;
4. sort mods topologically, using ordinal mod ID as the tie-breaker;
5. load built-in content first, then each mod's `content/` directory in that order;
6. run parsing, identity, semantic, and cross-reference validation over the combined set;
7. publish one immutable catalog only when the entire installation is valid.

Diagnostics include the source ID, relative file, JSON path, message, and stable problem code:

```text
mod.example.starter-pack/items/bad.json $.id: Mod 'mod.example.starter-pack'
must declare items IDs beginning with 'item.example.starter-pack.'. [id.wrong-namespace]
```

There is no partial-mod fallback in this foundation. If an installed package is malformed,
startup reports all collected problems and stops rather than running with an unpredictable
subset of content.

## Saves and mod versions

Each save envelope records the stable ID and exact version of every enabled data mod. Loading
requires every recorded mod at that same version:

- a missing requirement raises `MissingSaveModException`;
- a different version raises `IncompatibleSaveModVersionException`;
- an older save with no `enabledMods` field defaults to an empty list and still loads;
- additional currently enabled mods are allowed for now.

Exact versions are conservative. The game cannot yet know whether a mod update removed an ID
stored by a campaign, so it refuses to guess. A later mod-profile/load UI can help players
select the matching set and can introduce explicit author-declared save compatibility after
real versioning cases exist.

## Authoring and validation workflow

1. Copy `examples/mods/mod.example.starter-pack` and rename its root folder.
2. Choose a permanent `mod.author.mod-name` ID; update the folder and manifest together.
3. Give every record the namespace derived from that ID.
4. Use only fields and categories documented in `CONTENT_SCHEMA.md`.
5. Declare every mod whose records you reference in `dependencies`.
6. Validate base and mods together from the repository root:

```powershell
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content path\to\mods
```

7. Run the nonvisual regression suite:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
```

8. Copy the complete package folder into the Godot `user://mods` folder and run the project.
   The Output panel reports the number of loaded definitions and enabled data mods.

## Deliberate next decisions

Do not add these opportunistically while building gameplay:

- enable/disable profiles and a recovery UI for incompatible saves;
- cross-mod dependency-version ranges;
- localization or presentation-asset packs;
- ZIP/PCK packaging, signatures, download sources, or Workshop publishing;
- base-data patches and deterministic conflict resolution;
- scripts, assemblies, native code, or a general effect language.

Each needs its own security, compatibility, authoring, and player-experience decision. The
current contract is enough to grow hundreds of additive JSON definitions while keeping a
single-developer codebase understandable.

## Party-size support

The game supports four heroes total. Mods may add actor definitions and allow players to use
different heroes, but they do not increase party size. Keeping this as a code-owned rule lets
future party menus, recruitment, saves, and combat share one tested expectation without a
global setting or mod-conflict policy.
