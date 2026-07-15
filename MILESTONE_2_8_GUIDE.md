# Milestone 2.8 guide — enemy footprint content

## Purpose

Milestone 2.8 lets an enemy definition say how many logical battlefield cells that enemy
occupies. It connects authored JSON to the rectangular `FormationFootprint` value already
owned by the Godot-free formation rules.

This milestone is about content only. It does not decide where an enemy appears, draw a
battle grid, scale a sprite, select targets, or add combat behavior.

## What a footprint represents

A footprint is a rectangle measured in rows by columns:

| Rows | Columns | Meaning |
|---:|---:|---|
| `1` | `1` | Normal enemy occupying one cell |
| `1` | `2` | Wide/deep enemy occupying two columns |
| `2` | `1` | Tall enemy occupying two rows |
| `2` | `2` | Large enemy occupying four cells |

`rows` is the enemy's vertical cell count. `columns` is its depth cell count from the front
of the enemy side toward its rear. These numbers are sizes, not coordinates. For example,
`rows: 2` does not mean row index 2; it means the rectangle is two rows tall.

Only solid rectangles are supported. Irregular masks, holes, rotation, and a footprint that
changes during battle are deliberately outside the current rules.

## JSON field and compatible default

Enemy JSON uses the optional `formationFootprint` object. The repository keeps this existing
field name unchanged in current data API 3; the later API bump affects loot ownership, not
formation geometry.

An explicit normal enemy can write:

```json
{
  "schemaVersion": 2,
  "id": "enemy.example.normal",
  "displayNameKey": "enemy.example.normal.name",
  "level": 1,
  "statistics": {},
  "abilityIds": [],
  "formationFootprint": {
    "rows": 1,
    "columns": 1
  },
  "lootTableId": null
}
```

A large enemy can write:

```json
{
  "schemaVersion": 2,
  "id": "enemy.example.large",
  "displayNameKey": "enemy.example.large.name",
  "level": 1,
  "statistics": {},
  "abilityIds": [],
  "formationFootprint": {
    "rows": 2,
    "columns": 2
  },
  "lootTableId": null
}
```

An older or ordinary enemy may omit the entire member:

```json
{
  "schemaVersion": 2,
  "id": "enemy.example.compatible",
  "displayNameKey": "enemy.example.compatible.name",
  "level": 1,
  "statistics": {},
  "abilityIds": [],
  "lootTableId": null
}
```

Omission creates an `EnemyFootprintDefinition` whose `Rows` and `Columns` both default to
`1`. The checked-in green slime intentionally uses this form as a production compatibility
example.

Omission and explicit `1 × 1` mean the same thing. Explicit JSON `null` does not: it is
treated as an authoring mistake and prevents the catalog from being published.

## Validation limits

The enemy battlefield already owns four rows and four enemy-side columns through
`BattleFormationRules.RowCount` and `BattleFormationRules.EnemyColumnCount`. Content
validation reads those constants rather than maintaining another pair of magic numbers.

The production validator applies these rules:

- `rows` must be at least `1` and no greater than the formation row count;
- `columns` must be at least `1` and no greater than the enemy-side column count;
- `formationFootprint` may be omitted but may not be explicitly `null`;
- invalid values are reported; they are never changed or clamped;
- all independent content problems are collected before the load returns;
- any problem prevents publication of `IContentCatalog`.

Diagnostics identify the content source/file, exact JSON path, stable code, and explanation:

| Code | JSON path | Meaning |
|---|---|---|
| `enemy.footprint-null` | `$.formationFootprint` | The object was explicitly null |
| `enemy.footprint-rows-invalid` | `$.formationFootprint.rows` | Rows are below 1 or above the formation limit |
| `enemy.footprint-columns-invalid` | `$.formationFootprint.columns` | Columns are below 1 or above the enemy-side limit |

## Content DTO and core geometry

Two similarly shaped types remain separate because they have different jobs:

| Type | Job | Lifetime |
|---|---|---|
| `EnemyFootprintDefinition` | Holds immutable values authored in enemy JSON | Content catalog |
| `FormationFootprint` | Supplies rows and columns to pure formation rules | Temporary battle construction/rules |

`EnemyFootprintDefinition.ToFormationFootprint()` is the one explicit conversion between
them. It copies both integers exactly. The method does not validate or repair data because
`JsonContentLoader` and `ContentValidator` must reject bad authoring before publishing the
catalog. Keeping one conversion avoids repeating `new FormationFootprint(rows, columns)` in
each future consumer without making the content DTO inherit from runtime battle state.

Both types live in `Rpg.Core`, which remains ordinary .NET and has no Godot dependency.
After a successful load, callers can retrieve an `EnemyDefinition` through
`IContentCatalog` and receive the same deterministic authored/default footprint.

## Why this is content, not save state

The footprint describes an enemy species, like its base level or ability IDs. Every campaign
using that same enabled content pack should interpret the enemy the same way. It is therefore
immutable definition data loaded into the application-lifetime catalog.

Save state records facts unique to one campaign, such as James's location, chosen class, and
event flags. Copying enemy dimensions into `GameState` would duplicate content, make mod
compatibility harder, and create stale values when a content package changes. Milestone 2.8
does not increment the save format and adds no migration.

## Data-mod compatibility

At Milestone 2.8 this was an additive enemy-schema change:

- existing base enemies without `formationFootprint` still load as `1 × 1`;
- existing mod enemies without it also load as `1 × 1`;
- the then-current enemy `schemaVersion` remained `1`;
- the then-current mod `gameApiVersion` remained `2`;
- save `SaveFormatVersion` remains unchanged;
- no existing enemy record must be rewritten.

The default is deterministic and does not depend on filenames, display names, sprites,
source order, or whether the record came from the base game or a mod.

Milestone 3.06 later moves embedded drops into standalone loot tables. Current enemy records
therefore use schema version `2`, require an explicit nullable `lootTableId`, and require mod
data API `3`. That later compatibility change does not alter footprint behavior: omitting
`formationFootprint` still means `1 × 1`. Current examples in this guide use the latest enemy
shape so they can be copied safely. See `LOOT_TABLE_AUTHORING_GUIDE.md` for the new drop data.

## What remains outside Milestone 2.8

This content milestone does not add or change:

- encounter coordinates or slot IDs;
- placement, bounds-at-an-anchor, or overlap behavior;
- party placement;
- Godot battle-grid rendering or sprite scaling;
- irregular shapes or rotation;
- target selection, ranges, area effects, or movement;
- Attack, Guard, HP, damage, turns, victory, defeat, or rewards;
- persistent battle state or save fields.

The current repository may already demonstrate some formation presentation from Milestone
2.75. That code is not extended by this milestone. Milestone 2.8 only guarantees that enemy
content supplies a validated core footprint for later consumers.

## Automated proof

The headless content tests prove:

1. omitted base-enemy footprints become `1 × 1`;
2. explicit `1 × 1` and valid `2 × 2` values load unchanged;
3. zero, negative, and greater-than-formation dimensions prevent catalog publication;
4. explicit JSON `null` produces an exact enemy-footprint diagnostic;
5. a mod enemy that omits the field remains compatible;
6. content-to-core conversion preserves rows and columns.

No Godot test is added because this milestone has no Godot behavior. The normal solution
build and headless Godot editor import remain integration checks for accidental compile or
project-import regressions.

## Validation commands

From the repository root in PowerShell:

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

Do not mark the roadmap milestone implemented or commit it until all five commands return
exit code `0` and the manual diff review confirms that no excluded gameplay or Godot work was
introduced.
