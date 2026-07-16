# Content Authoring Guide

This is the practical guide for adding items, equipment, maps, enemies, and loot tables. The
complete field-level contract remains in `CONTENT_SCHEMA.md`.

## Workflow

1. Choose a permanent lowercase ID. IDs are save/content identity and must not be renamed after release.
2. Add one UTF-8 JSON file in the correct folder under `game/content/`.
3. Include `schemaVersion` and `id` in every top-level record.
4. Reference other records by stable IDs, never filenames, display names, array positions, or Godot paths.
5. Add localization keys to `game/localization/en.json` for display and description fields.
6. Run:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

Do not put C# scripts, formulas, scene paths, or arbitrary behavior in content JSON.

## Items

Items live in `game/content/items/`. Equipment is a separate record that points to an item with
`itemId`.

```json
{
  "schemaVersion": 1,
  "id": "item.quest.old-key",
  "displayNameKey": "item.old-key.name",
  "descriptionKey": "item.old-key.description",
  "buyPrice": 0,
  "sellPrice": 0,
  "unique": true
}
```

`maxStack` is optional and defaults to `99`. `unique` is optional and defaults to `false`; a
unique item always has an effective max stack of `1`. Use `unique` for one-of-a-kind story or
quest items, not merely because an item is equipment.

## Equipment

Equipment lives in `game/content/equipment/`:

```json
{
  "schemaVersion": 1,
  "id": "equipment.weapon.iron-sword",
  "itemId": "item.equipment.iron-sword",
  "slotId": "slot.weapon.main-hand",
  "statisticModifiers": {},
  "attack": 4,
  "weaponDamagePercentages": { "damage-type.slash": 100 },
  "grantedAbilityIds": []
}
```

Supported authored slots are `slot.weapon.main-hand`, `slot.weapon.off-hand`, `slot.armor.body`,
`slot.armor.feet`, `slot.armor.helm`, and `slot.accessory`. An accessory can be equipped into
either saved accessory slot, `.one` or `.two`; those concrete values are save keys.

`attack` and `weaponDamagePercentages` are optional weapon-only fields. Omit them for armor,
shields, and accessories. Weapon damage profiles must total `100`. All equipment may use
`statisticModifiers`, `grantedAbilityIds`, and reserved `specialEffectIds`.

## Maps

Maps live in `game/content/maps/`. Rows are the gameplay logic layer, separate from visual art:

```json
{
  "schemaVersion": 1,
  "id": "map.test-grove",
  "displayNameKey": "map.test-grove.name",
  "width": 12,
  "height": 5,
  "rows": [
    "############",
    "#..........#",
    "#....E.....#",
    "#.......T..#",
    "############"
  ],
  "spawns": [{ "id": "spawn.start", "x": 1, "y": 1, "facing": "east" }],
  "encounters": []
}
```

Symbols: `#` impassable, `.` passable, `E` passable encounter tile, and `T` passable transition
tile. Coordinates are zero-based: X increases left to right, Y increases top to bottom, and
`rows[y][x]` is the symbol at `[x, y]`. `width` must match every row and `height` must match the
row count.

Spawns contain an ID, X/Y, and facing. Encounter markers contain `id`, X/Y, `encounterId`, and
`clearedFlagId`; their tile should use `E`. Transition definitions live in
The map's `transitions` array contains transition IDs, source cells, destination map IDs, and
destination spawn IDs. Their source cell should use `T`, and their destination map and spawn IDs
must exist. There is no separate transition file to maintain.

Add the transition directly to the map record:

```json
"transitions": [
  {
    "id": "transition.test-grove.to-town",
    "sourceCell": { "x": 8, "y": 3 },
    "destinationMapId": "map.test-town",
    "destinationSpawnId": "spawn.from-grove"
  }
]
```

## Enemies

Enemies live in `game/content/enemies/` and reference a loot table:

```json
{
  "schemaVersion": 2,
  "id": "enemy.grove.wisp",
  "displayNameKey": "enemy.wisp.name",
  "level": 2,
  "statistics": {
    "stat.max-hp": 18,
    "stat.strength": 2,
    "stat.intelligence": 4,
    "stat.defense": 1,
    "stat.spirit": 3,
    "stat.speed": 3
  },
  "abilityIds": ["ability.enemy.tackle"],
  "damageTypePercentModifiers": { "damage-type.fire": 50, "damage-type.slash": 120 },
  "lootTableId": "loot-table.grove.wisp"
}
```

Damage modifiers are percentages of incoming damage: `50` is weak, `100` is normal, and `120`
is resistant/strong. Use supported `damage-type.*` IDs. Statistics and abilities must already
exist and belong to their expected categories.

## Loot Tables

Loot tables live in `game/content/loot-tables/`. They do not mutate inventory:

```json
{
  "schemaVersion": 1,
  "id": "loot-table.grove.wisp",
  "entries": [
    {
      "itemId": "item.consumable.potion",
      "chance": 0.35,
      "minQuantity": 1,
      "maxQuantity": 2
    }
  ]
}
```

Each entry rolls independently in authored order. `chance` is from `0` through `1`; quantity
bounds are inclusive. Repeated item entries are allowed when drops should be independent.

## Common mistakes

- Using an equipment ID where an item ID is required.
- Authoring an accessory as `slot.accessory.one` instead of flexible `slot.accessory`.
- Adding `attack: 0` or `weaponDamagePercentages: {}` to non-weapons.
- Writing a map row with the wrong width or an unsupported symbol.
- Putting an `E` or `T` marker on `#` or on a mismatched row symbol.
- Embedding loot entries inside an enemy instead of using `lootTableId`.
- Renaming an ID after another record or save can reference it.
