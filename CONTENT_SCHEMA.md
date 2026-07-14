# Content schema

## Format

Content definitions are UTF-8 JSON with one top-level record per file. The folder
selects the C# definition type; a `type` discriminator is therefore unnecessary.
Property names use `camelCase`. Arrays retain author order only when order has gameplay
meaning.

The loader is intentionally strict: unknown JSON properties, wrong value types, and
missing required properties are errors. JSON comments and trailing commas are accepted for
author convenience. Validation is all-or-nothing—the runtime catalog is published only when
every record parses and every implemented semantic/reference check succeeds.

Every top-level record contains:

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | integer | Starts at `1`; increment only when this record category needs migration. |
| `id` | string | Permanent, globally unique, stable ID. |

The initial directory-to-type mapping is:

| Directory | Definition |
|---|---|
| `actors/` | `ActorDefinition` |
| `classes/` | `ClassDefinition` |
| `statistics/` | `StatisticDefinition` |
| `items/` | `ItemDefinition` |
| `equipment/` | `EquipmentDefinition` |
| `abilities/` | `AbilityDefinition` |
| `enemies/` | `EnemyDefinition` |
| `encounters/` | `EncounterDefinition` |
| `quests/` | `QuestDefinition` |

## Stable IDs

Canonical IDs match:

```regex
^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)+$
```

Use a category followed by useful namespaces and a specific name:

```text
actor.hero.james
class.magic.black-mage
stat.max-hp
item.consumable.potion
equipment.weapon.iron-sword
ability.black-magic.fire
enemy.forest.green-slime
encounter.forest.slimes-01
quest.prologue.first-steps
```

Rules:

- The ID, not the filename or display name, is identity.
- Never rename or reuse an ID after it appears in a released build or save.
- Use a migration/alias when content truly must be replaced.
- References must point to the expected category.
- Embedded value objects do not need global IDs unless save data or another record
  addresses them. Quest objective IDs are stable within their parent for this reason.
- Localization keys, music cues, battlefields, equipment slots, event flags,
  targeting modes, and code-owned rulesets also use stable namespaced strings, even
  though they are not all top-level content records.

### Data-mod namespaces

A mod manifest ID uses `mod.author.mod-name`. Records owned by that mod must place the
same namespace between the category and record name:

| Manifest ID | Valid record examples |
|---|---|
| `mod.example.starter-pack` | `class.example.starter-pack.chronoguard`, `ability.example.starter-pack.temporal-guard` |

This is enforced by the production loader. Mod records cannot reuse or replace base-game
IDs, and all IDs remain globally unique. References may point to base content or a declared
dependency, but the referenced record is still selected only by stable ID. See `MODDING.md`
for the manifest and folder contract.

## Record fields

### Actor

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key. |
| `startingClassId` | ID | Must reference a class. |
| `startingLevel` | integer | At least `1`. |
| `baseStatistics` | object of ID → integer | Keys reference statistics. |
| `startingAbilityIds` | ID array | References abilities. |

```json
{
  "schemaVersion": 1,
  "id": "actor.hero.james",
  "displayNameKey": "actor.james.name",
  "startingClassId": "class.martial.vanguard",
  "startingLevel": 1,
  "baseStatistics": { "stat.max-hp": 84, "stat.strength": 9 },
  "startingAbilityIds": ["ability.vanguard.guard"]
}
```

### Class

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key. |
| `baseStatisticBonuses` | object of ID → integer | Additive bonuses; keys reference statistics. |
| `abilityUnlocks` | array | Entries contain `level` and `abilityId`. |

```json
{
  "schemaVersion": 1,
  "id": "class.magic.black-mage",
  "displayNameKey": "class.black-mage.name",
  "baseStatisticBonuses": { "stat.magic": 4 },
  "abilityUnlocks": [
    { "level": 1, "abilityId": "ability.black-magic.fire" }
  ]
}
```

### Statistic

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key. |
| `minimumValue` | integer | Inclusive content-level bound. |
| `maximumValue` | integer | Inclusive content-level bound. |
| `defaultValue` | integer | Must fall within the bounds. |

```json
{
  "schemaVersion": 1,
  "id": "stat.strength",
  "displayNameKey": "stat.strength.name",
  "minimumValue": 0,
  "maximumValue": 999,
  "defaultValue": 1
}
```

### Item and equipment

An equipment record decorates an item record instead of inheriting from it in JSON.
This keeps common inventory/shop data in exactly one place.

| Item field | Type | Notes |
|---|---|---|
| `displayNameKey`, `descriptionKey` | string | Localization keys. |
| `buyPrice`, `sellPrice` | integer | Nonnegative base values. |
| `maxStack` | integer | At least `1`. |

| Equipment field | Type | Notes |
|---|---|---|
| `itemId` | ID | Unique reference to its item record. |
| `slotId` | ID | Stable game-owned equipment slot. |
| `statisticModifiers` | object of ID → integer | Keys reference statistics. |
| `grantedAbilityIds` | ID array | References abilities. |

```json
{
  "schemaVersion": 1,
  "id": "item.equipment.iron-sword",
  "displayNameKey": "item.iron-sword.name",
  "descriptionKey": "item.iron-sword.description",
  "buyPrice": 120,
  "sellPrice": 60,
  "maxStack": 1
}
```

```json
{
  "schemaVersion": 1,
  "id": "equipment.weapon.iron-sword",
  "itemId": "item.equipment.iron-sword",
  "slotId": "slot.weapon.main-hand",
  "statisticModifiers": { "stat.strength": 3 },
  "grantedAbilityIds": []
}
```

### Ability

| Field | Type | Notes |
|---|---|---|
| `displayNameKey`, `descriptionKey` | string | Localization keys. |
| `targetingId` | ID | Code-owned targeting rule. |
| `costStatisticId` | ID or null | Statistic/resource spent, if any. |
| `costAmount` | integer | Nonnegative. |
| `rulesetId` | ID | Selects one small code-owned behavior. |
| `numericParameters` | object of string → number | Values consumed by that ruleset and explicitly validated. |

```json
{
  "schemaVersion": 1,
  "id": "ability.black-magic.fire",
  "displayNameKey": "ability.fire.name",
  "descriptionKey": "ability.fire.description",
  "targetingId": "target.enemy.single",
  "costStatisticId": "stat.mp",
  "costAmount": 4,
  "rulesetId": "rules.damage.magic",
  "numericParameters": { "power": 18 }
}
```

`rulesetId` is a constrained escape hatch for game rules, not a generic scripting
language. Add a ruleset only for a demonstrated family of abilities, and validate its
known parameters.

### Enemy

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key. |
| `level` | integer | At least `1`. |
| `statistics` | object of ID → integer | Keys reference statistics. |
| `abilityIds` | ID array | References abilities. |
| `loot` | array | `itemId`, chance from `0` to `1`, and inclusive quantity range. |

```json
{
  "schemaVersion": 1,
  "id": "enemy.forest.green-slime",
  "displayNameKey": "enemy.green-slime.name",
  "level": 1,
  "statistics": { "stat.max-hp": 22, "stat.strength": 3 },
  "abilityIds": ["ability.enemy.tackle"],
  "loot": [
    { "itemId": "item.consumable.potion", "chance": 0.125, "minQuantity": 1, "maxQuantity": 1 }
  ]
}
```

### Encounter

| Field | Type | Notes |
|---|---|---|
| `enemyGroup` | array | Entries contain `enemyId` and a unique formation `slotId`. |
| `battlefieldId` | ID or null | Presentation lookup, never a resource path. |
| `musicCueId` | ID or null | Presentation lookup, never an audio path. |

```json
{
  "schemaVersion": 1,
  "id": "encounter.forest.slimes-01",
  "enemyGroup": [
    { "enemyId": "enemy.forest.green-slime", "slotId": "formation.left" },
    { "enemyId": "enemy.forest.green-slime", "slotId": "formation.right" }
  ],
  "battlefieldId": "battlefield.forest.day",
  "musicCueId": "music.battle.normal"
}
```

### Quest

| Field | Type | Notes |
|---|---|---|
| `displayNameKey`, `descriptionKey` | string | Localization keys. |
| `objectives` | array | Stable local `id`, `kind`, `targetId`, and `requiredCount`. |
| `rewards` | array | `itemId` and positive `quantity`. |
| `completionFlagId` | ID or null | Set by the future quest application service. |

```json
{
  "schemaVersion": 1,
  "id": "quest.prologue.first-steps",
  "displayNameKey": "quest.first-steps.name",
  "descriptionKey": "quest.first-steps.description",
  "objectives": [
    { "id": "reach-gate", "kind": "objective.reach", "targetId": "map.prologue.gate", "requiredCount": 1 }
  ],
  "rewards": [
    { "itemId": "item.consumable.potion", "quantity": 2 }
  ],
  "completionFlagId": "flag.quest.first-steps.completed"
}
```

## Validation policy

The content validator milestone must report the file and JSON path for:

- JSON parse/type failures;
- missing required fields or unsupported schema versions;
- invalid or duplicate top-level IDs;
- missing references and wrong-category references;
- impossible ranges, negative prices/costs, and invalid probabilities;
- duplicate encounter slots or quest objective IDs;
- unknown rulesets, targeting modes, parameters, slots, and flags where registries exist;
- orphaned equipment records and duplicate equipment for one `itemId`.

Warnings should be reserved for suspicious but legal values. Anything that would crash
or corrupt state is an error.

Run the exact loader and validator used by the tests and Godot startup with:

```sh
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

To validate base content with an installed or example mod directory:

```sh
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content examples/mods
```

The tool prints every problem in deterministic file/path/code order and returns a nonzero
process exit code, making it suitable for local authoring and future CI.

## Evolution policy

Additive fields must have safe defaults. A breaking category change increments that
category's `schemaVersion` and adds an explicit content migration before old files are
removed. Never infer identity from filename changes. Status effects, shops, dialogue,
and cutscene schemas will be added only when their first playable use case is built.
