# Loot-table authoring guide

## What this foundation does

Milestone 3.06 separates enemy combat definitions from item-drop authoring. An enemy stores
one stable `lootTableId`; the referenced JSON record stores the possible items, probabilities,
and quantity ranges.

Milestone 4.1 resolves these tables into ordered transient award facts through the pure-core
`LootResolver`. Milestone 4.2 calls it exactly once after accepted party victory, applies the
resulting item batch atomically, and shows aggregated totals. The resolver itself still does
**not** grant inventory, show UI, save items, or decide when a result is accepted. Keeping this
content boundary separate prevents reward application from depending on enemy JSON layout or
Godot scenes.

## The three records involved

| Record | Responsibility | Example ID |
|---|---|---|
| Item | Defines the stable item identity | `item.consumable.potion` |
| Loot table | Defines independent chances and quantities | `loot-table.forest.green-slime` |
| Enemy | Selects one table, or explicitly selects no table | `enemy.forest.green-slime` |

The relationship always uses IDs:

```text
enemy.lootTableId -> loot-table record -> entry.itemId -> item record
```

Filenames, folder order, display names, sprites, and scene nodes are never identity.

## Step 1: make sure the item exists

A loot entry must reference an existing item record. The base potion lives beneath
`game/content/items/` and has this stable ID:

```json
{
  "schemaVersion": 1,
  "id": "item.consumable.potion",
  "displayNameKey": "item.potion.name",
  "descriptionKey": "item.potion.description",
  "buyPrice": 30,
  "sellPrice": 15,
  "maxStack": 99
}
```

Do not put an item filename or display name in a loot table.

## Step 2: create the loot table

Create one JSON file beneath `game/content/loot-tables/`. The filename is for organization;
the permanent `id` is what enemies reference.

```json
{
  "schemaVersion": 1,
  "id": "loot-table.forest.green-slime",
  "entries": [
    {
      "itemId": "item.consumable.potion",
      "chance": 0.125,
      "minQuantity": 1,
      "maxQuantity": 1
    }
  ]
}
```

Every entry must explicitly include all four fields.

| Field | Legal values | Meaning |
|---|---|---|
| `itemId` | Existing `item.*` ID | Item that could be awarded |
| `chance` | Decimal from `0` through `1` | `0.125` means 12.5%; `1` means guaranteed |
| `minQuantity` | Integer at least `1` | Lowest successful quantity |
| `maxQuantity` | Integer at least `minQuantity` | Highest successful quantity |

Entries are independent. If a table contains three entries, a future resolver will make three
separate chance checks in authored order. More than one may succeed. Repeating an item ID is
legal and represents separate chances; a future reward result will aggregate quantities.

## Step 3: reference the table from an enemy

Current enemy records use schema version `2`. `lootTableId` must appear explicitly:

```json
{
  "schemaVersion": 2,
  "id": "enemy.forest.green-slime",
  "displayNameKey": "enemy.green-slime.name",
  "level": 1,
  "statistics": {
    "stat.max-hp": 22,
    "stat.strength": 3,
    "stat.defense": 2,
    "stat.speed": 2
  },
  "abilityIds": ["ability.enemy.tackle"],
  "lootTableId": "loot-table.forest.green-slime"
}
```

For an enemy that drops no items, write a real JSON null:

```json
"lootTableId": null
```

Do not omit the member. Requiring it makes “this enemy intentionally has no drops” different
from “the author forgot to decide.”

## Sharing and disabling tables

Several enemies may reference the same table. For example, multiple slime colors could all
use `loot-table.forest.common-slime`. Editing that one table then rebalances the entire group
without touching their HP, abilities, footprints, or encounter placements.

An empty table is also legal:

```json
{
  "schemaVersion": 1,
  "id": "loot-table.forest.common-slime",
  "entries": []
}
```

This temporarily disables drops for every referencing enemy. A zero-chance entry is legal for
the same authoring reason, although deleting long-unused entries keeps production data clearer.

## Migrating an old inline enemy

Data API 2 placed entries directly on the enemy:

```json
"loot": [
  {
    "itemId": "item.consumable.potion",
    "chance": 0.125,
    "minQuantity": 1,
    "maxQuantity": 1
  }
]
```

For data API 3:

1. Copy that array to the `entries` member of a new schema-1 loot-table record.
2. Give the table a permanent `loot-table.*` ID.
3. Change the enemy to `schemaVersion: 2`.
4. Delete the enemy's `loot` member.
5. Add the enemy's `lootTableId` member.
6. Change the mod manifest to `gameApiVersion: 3`.
7. Run base-plus-mod validation.

The strict loader rejects the old `loot` property. It does not silently discard drops or
guess a generated table ID.

## Authoring tables in a data mod

For mod ID `mod.alex.monster-pack`, owned table IDs begin with:

```text
loot-table.alex.monster-pack.
```

Example:

```json
{
  "schemaVersion": 1,
  "id": "loot-table.alex.monster-pack.crystal-slime",
  "entries": [
    {
      "itemId": "item.alex.monster-pack.crystal-shard",
      "chance": 0.4,
      "minQuantity": 1,
      "maxQuantity": 3
    }
  ]
}
```

A mod table may reference:

- a base-game item;
- an item owned by the same mod;
- an item owned by a mod listed directly in `dependencies`.

A mod cannot redeclare `loot-table.forest.green-slime` to replace vanilla drops. Duplicate
IDs remain errors because “last mod wins” would make outcomes depend on installation order.
Vanilla-loot replacement, selected loot profiles, and randomizer mappings need a later explicit
composition design.

## Runtime resolution boundary

Milestone 4.1's pure-.NET resolver:

1. receive defeated enemy definition IDs;
2. resolve their table IDs through `IContentCatalog`;
3. receive randomness through `IRandomSource`;
4. rolls entries independently in supplied enemy and authored entry order;
5. returns one typed item/quantity award per successful entry without aggregating duplicates;
6. leaves the Milestone 4.2 campaign/inventory use case to apply those awards.

The resolver must not find Godot nodes, play animations, update controls, mutate a scene, or
touch `GameState`/inventory. `VictoryRewardService` consumes its typed output; it does not alter
table semantics or aggregate the resolver's raw facts. The loot table must not contain C# method
names, scripts, formulas, or reflection-selected types. A future randomizer can work with the
same stable table IDs; its seed or generated mapping would be campaign state, while the original
tables remain definition data.

## Common validation failures

| Problem | Typical cause | Fix |
|---|---|---|
| `category.unknown` | File is not beneath `loot-tables/` | Move it to the documented folder |
| `id.wrong-category` | Table ID does not begin with `loot-table.` | Correct the permanent ID before release |
| `id.wrong-namespace` | Mod table lacks the mod namespace | Match the manifest-derived namespace |
| `reference.missing` | Table or item ID does not exist | Add the record or correct the ID |
| `reference.wrong-category` | Enemy points to an item, or entry points to an enemy | Reference the expected content category |
| `loot-table.chance-out-of-range` | Chance is below `0` or above `1` | Use an inclusive probability |
| `value.too-small` | Quantity is nonpositive or maximum is below minimum | Correct the inclusive range |
| `value.null` | `entries` or an array entry is JSON null | Use an array and real entry objects |
| `schema.unsupported` | Enemy still uses schema 1 | Migrate it to schema 2 |
| `json.invalid` | Required field is missing or retired `loot` is present | Match the current strict schema |

## Validation checklist

Run these from the repository root after editing loot content:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content examples/mods

dotnet build RpgGame.sln
```

Before committing, confirm:

- every table has an explicit schema version and permanent ID;
- every entry has all four required fields;
- every item ID exists;
- every current enemy uses schema 2 and explicitly writes `lootTableId`;
- every mod manifest declares data API 3;
- no enemy still contains an inline `loot` array;
- no reward timing, inventory mutation, or Godot presentation was embedded in content or the
  resolver; those concerns remain in the documented 4.2 application/presentation owners.
