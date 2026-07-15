# Data modding contract

## Scope of Milestone 1.5

The game supports **data-only, loose-folder mods**. A mod may add records in the same JSON
categories as the base game: actors, classes, statistics, items, equipment, loot tables,
abilities, magic disciplines, enemies, encounters, quests, dialogues, and starting-class
rules. Those
definitions enter the same typed catalog and pass the same strict validation as built-in
content.

This milestone does **not** load C# assemblies, native libraries, GDScript, executable hooks,
PCK/ZIP packages, Steam Workshop items, URLs, or arbitrary behavior expressions. It does not
add a mod browser, enable/disable screen, hot reload, or gameplay. Every immediate valid
package beneath the mods directory is enabled for that process.

This narrow boundary gives community authors useful class, ability, item, enemy, encounter,
and quest data without turning the first playable milestones into a plugin platform or
exposing players to community code execution.

Dialogue JSON uses the same namespace validation, but Milestone 2 exposes no mod-owned maps,
NPC placement, scene resources, or presentation hooks that could select a new dialogue. That
presentation integration remains explicitly unsupported even though the data record itself is
safe to parse and validate.

## Package layout

Each mod is one immediate child folder whose name exactly matches its stable manifest ID:

```text
user://mods/
└── mod.example.starter-pack/
    ├── manifest.json
    └── content/
        ├── abilities/
        │   └── temporal-guard.json
        ├── classes/
        │   └── chronoguard.json
        └── starting-class-rules/
            └── class-pool.json
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
  "version": "2.0.0",
  "gameApiVersion": 3,
  "dependencies": []
}
```

| Field | Meaning |
|---|---|
| `schemaVersion` | Shape of `manifest.json`; Milestone 1.5 supports exactly `1`. |
| `id` | Permanent lowercase ID in `mod.author.mod-name` form. It must equal the folder name. |
| `name` | Nonblank human-facing name. It is never used as identity. |
| `version` | Author-controlled Semantic Version such as `1.0.0` or `1.1.0-beta.1`. |
| `gameApiVersion` | Integer data contract supported by the game; currently exactly `3`. |
| `dependencies` | Stable IDs of mods that must be installed and loaded first. |

`gameApiVersion` is separate from the executable's build string. Ordinary game releases do
not invalidate all mods; this integer changes only when the public data contract becomes
incompatible. A mod's own `version` should change whenever its published data changes.

### Data API 2 formation change

Milestone 2.75 raises the public data API from `1` to `2`. API 1 documented encounter
`slotId` values only as general `formation.*` stable keys, so accepting names such as
`formation.left` and then silently changing their meaning would break encounter mods. API 2
instead requires canonical enemy coordinates such as `formation.enemy.r1.c0`. A mod manifest
had to opt into that contract by declaring `gameApiVersion: 2`.

This is not a save-format change. Formation anchors and enemy footprints are content used to
build a transient battle arrangement; they are not stored in `GameState` or the save envelope.
The manifest schema remains `1`; API 3 later changes only the enemy content-record version.

### Data API 3 loot-table change

Milestone 3.06 raises the public data API from `2` to `3`. API-2 enemy records embedded a
`loot` array beside statistics and abilities. API 3 moves those entries into independently
addressable `loot-tables/` records and requires enemy schema version `2` with one explicit
nullable `lootTableId`. This is an intentional pre-release clean break: carrying two drop
formats into future reward gameplay would create permanent ambiguity and duplicate code.

This is still not a save-format change. Loot tables describe immutable possibilities, not
items already awarded to a campaign. API-2 mods must move each embedded array into a
namespaced table, update each enemy reference, use enemy `schemaVersion: 2`, and declare
`gameApiVersion: 3`. The manifest's own `schemaVersion` remains `1`.

## Record ownership and references

Remove the leading `mod.` from the manifest ID to obtain the mod's content namespace. For
`mod.example.starter-pack`, each owned ID must begin with its category plus
`example.starter-pack`:

```text
class.example.starter-pack.chronoguard
ability.example.starter-pack.temporal-guard
item.example.starter-pack.time-shard
enemy.example.starter-pack.clockwork-slime
loot-table.example.starter-pack.clockwork-slime
magic-discipline.example.starter-pack.runes
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

## Authoring enemy formations

An enemy may declare its rectangular battlefield size:

```json
{
  "schemaVersion": 2,
  "id": "enemy.example.starter-pack.clockwork-slime",
  "displayNameKey": "enemy.example.starter-pack.clockwork-slime.name",
  "level": 1,
  "statistics": {},
  "abilityIds": [],
  "formationFootprint": { "rows": 2, "columns": 2 },
  "lootTableId": null
}
```

Omitting `formationFootprint` means `1 × 1`; explicit `null`, zero, negative values, and
dimensions larger than `4 × 4` are errors. An encounter anchors that enemy with a canonical
coordinate:

```json
{
  "schemaVersion": 1,
  "id": "encounter.example.starter-pack.clockwork-slime-01",
  "enemyGroup": [
    {
      "enemyId": "enemy.example.starter-pack.clockwork-slime",
      "slotId": "formation.enemy.r1.c0"
    }
  ],
  "battlefieldId": "battlefield.example.starter-pack.workshop",
  "musicCueId": null
}
```

Rows and columns are zero-based. Row `0` is top and column `0` is front. The anchor is
the top-front occupied cell, so this `2 × 2` enemy occupies rows `1–2` and columns `0–1`.
Validation rejects malformed anchors, any footprint that leaves the enemy 4 × 4 grid, and
overlap between enemies. See `CONTENT_SCHEMA.md` and `MILESTONE_2_75_GUIDE.md` for the full
coordinate convention.

Milestone 2.8 originally added `formationFootprint` compatibly. Current API-3/schema-2 enemies
still may omit that member and receive the same deterministic `1 × 1` value as an explicit
`{ "rows": 1, "columns": 1 }`. The later schema/API bump is caused by standalone loot, not
by footprint behavior. See `MILESTONE_2_8_GUIDE.md` for the focused geometry contract.

## Authoring enemy loot tables

Place reusable tables beneath `content/loot-tables/`. A table owned by the example mod uses
the same namespace rule as every other mod record:

```json
{
  "schemaVersion": 1,
  "id": "loot-table.example.starter-pack.clockwork-slime",
  "entries": [
    {
      "itemId": "item.consumable.potion",
      "chance": 0.25,
      "minQuantity": 1,
      "maxQuantity": 2
    }
  ]
}
```

Its enemy uses schema version 2 and references the table by ID:

```json
{
  "schemaVersion": 2,
  "id": "enemy.example.starter-pack.clockwork-slime",
  "displayNameKey": "enemy.example.starter-pack.clockwork-slime.name",
  "level": 1,
  "statistics": {},
  "abilityIds": [],
  "lootTableId": "loot-table.example.starter-pack.clockwork-slime"
}
```

Several mod enemies may share one table. A mod table may reference base items, the mod's own
items, or items from a directly declared dependency. A mod cannot redeclare a vanilla table
ID to replace its contents; base-record patching and conflict resolution remain deliberately
unsupported. Use `lootTableId: null` for an enemy with no item drops. See
`LOOT_TABLE_AUTHORING_GUIDE.md` for entry rules and validation commands.

## Combat statistic resolution

Valid mod-defined statistics and namespaced actor, class, or enemy values flow through the
same pure statistic resolver as base content. Every loaded statistic is included by stable ID;
an omitted value uses that statistic's authored default, and a current class bonus is additive.
Milestone 2.85 adds no content fields and does not change `gameApiVersion` or the save format.

Future data-authored AI targeting may refer to statistic IDs—for example, a code-owned
"lowest statistic" selector parameterized with `stat.author.mod-name.magic-defense`. This
milestone defines no AI-profile record, targeting behavior, or executable hook. Community
scripts, assemblies, reflection-selected methods, and unrestricted expressions remain
unsupported.

## Ability and magic framework

Milestone 3.05 adds an additive ability classification. Existing abilities and mods that omit
`abilityKindId` still load as `ability-kind.skill`, so records such as Guard, Tackle, and
older mod abilities remain direct executable commands.

Mods may define `magic-disciplines/` records and Magic abilities:

```json
{
  "schemaVersion": 1,
  "id": "magic-discipline.example.starter-pack.runes",
  "displayNameKey": "magic-discipline.example.starter-pack.runes.name",
  "descriptionKey": "magic-discipline.example.starter-pack.runes.description"
}
```

```json
{
  "schemaVersion": 1,
  "id": "ability.example.starter-pack.rune-ward",
  "displayNameKey": "ability.example.starter-pack.rune-ward.name",
  "descriptionKey": "ability.example.starter-pack.rune-ward.description",
  "abilityKindId": "ability-kind.magic",
  "magicDisciplineIds": ["magic-discipline.example.starter-pack.runes"],
  "targetingId": "target.self",
  "costStatisticId": null,
  "costAmount": 0,
  "rulesetId": "rules.defense.guard",
  "numericParameters": { "damage-reduction": 0.25 }
}
```

Classes grant individual abilities through `abilityUnlocks` and grant access to magic
containers through `magicDisciplineUnlocks`. Both are required for a player character to use
a Magic ability: the class/actor must learn the spell and the class must unlock at least one
discipline listed by that spell. Unlocking a discipline does not automatically grant every
spell in that discipline.

Targeting and rulesets are closed code-owned contracts. Mods may reuse and tune the supported
combinations documented in `ABILITY_AUTHORING_GUIDE.md`; they cannot create behavior by
inventing a `target.*`, `rules.*`, or numeric-parameter key. Unknown IDs, missing parameters,
extra parameters, and illegal ranges prevent catalog publication. Executable scripts and
assemblies remain outside the data-mod boundary.

Current additive mods also cannot patch a vanilla class's `abilityUnlocks` or
`magicDisciplineUnlocks`. A mod that distributes a spell should grant it from a class owned by
that mod. Extending vanilla progression needs a future explicit composition contract rather
than order-dependent JSON replacement.

This remains data-only. It does not add scripts, spell effects, MP, battle menus, enemy
spellbook AI, or save data. Because omitted ability kinds remain Skills and the category was
additive, Milestone 3.05 did not itself change the then-current `gameApiVersion`; the later
API-3 change is solely the loot contract described above. Neither ability classification nor
loot definitions change the save format.
The stricter ruleset validation does not make previously unsupported custom behavior executable;
it reports those unusable IDs at load time instead of allowing a later runtime failure.

## Changing the new-game class pool

The base game includes Vanguard, Black Mage, and White Mage. Availability is not stored on
James or as an `availableAtStart` Boolean inside each class. That approach would let a mod add
its own starting class, but it could not remove a vanilla choice because mods may not replace
vanilla files.

Instead, add a namespaced record beneath `content/starting-class-rules/`:

```json
{
  "schemaVersion": 1,
  "id": "newgame.class-rule.example.starter-pack.class-pool",
  "includeClassIds": [
    "class.example.starter-pack.chronoguard"
  ],
  "excludeClassIds": [
    "class.magic.black-mage"
  ]
}
```

The resolver combines every base and mod record as:

```text
all included classes - all excluded classes
```

In this example, Chronoguard is added and vanilla Black Mage is removed. Exclusion always
wins—even if a different rule includes the same ID—so the result does not change with folder
enumeration or load order. A mod can reference vanilla classes freely. It may reference a
class from another mod only when that mod is listed as a direct manifest dependency.

Do not exclude every included class: validation rejects an empty starting pool. These rules
control only new-game availability. They do not delete class definitions, alter existing
saves, unlock later classes, or implement a class-selection screen.

This same resolved pool is a clean future input for a seeded randomizer: the randomizer can
choose among legal IDs without rewriting James or duplicating mod-conflict rules. Actual
randomizer selection is deliberately deferred until gameplay needs it.

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
