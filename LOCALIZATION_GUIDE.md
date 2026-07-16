# Localization Guide

## Folder Layout

The base English locale lives under `game/localization/en/`. The runtime and validator load
every JSON file beneath that folder recursively:

```text
game/localization/en/
  common.json
  maps/prologue.json
  items/equipment.json
  items/consumables.json
  dialogue/prologue/test-room-guide.json
```

Use one file per useful scope. The path organizes authoring; the text key is the identity.

## Bundle Format

Each file contains:

```json
{
  "schemaVersion": 1,
  "locale": "en",
  "texts": {
    "item.iron-sword.description": "A dependable blade."
  }
}
```

Keys and values must be nonblank. Duplicate keys across files are rejected, and every file
under the English locale must declare `locale` as `en`. Loading order never selects a winner.

## Content References

Items, maps, abilities, classes, enemies, quests, statistics, and other content records refer
to text with stable fields such as `displayNameKey` and `descriptionKey`. Add the referenced key
to the scoped English bundle before running validation.

Dialogue content stores structure and order only:

```json
{
  "schemaVersion": 2,
  "id": "dialogue.prologue.test-room-guide",
  "speakerNameKey": "dialogue.prologue.test-room-guide.speaker",
  "lineTextKeys": [
    "dialogue.prologue.test-room-guide.line.001"
  ]
}
```

Dialogue prose belongs in a matching `dialogue/` localization bundle. Do not put literal
player-facing lines in dialogue content.

## Validation And Fallbacks

Run:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

The validator checks bundle syntax, locale, duplicate keys, blank entries, and missing base
locale references. A runtime lookup that still misses uses `??missing.key??` so unfinished text
is obvious during development.

Mod-owned localization is deferred. Mods cannot replace base keys or rely on a separate mod
locale bundle yet.
