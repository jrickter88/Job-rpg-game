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
| `dialogues/` | `DialogueDefinition` |
| `statistics/` | `StatisticDefinition` |
| `items/` | `ItemDefinition` |
| `equipment/` | `EquipmentDefinition` |
| `abilities/` | `AbilityDefinition` |
| `enemies/` | `EnemyDefinition` |
| `encounters/` | `EncounterDefinition` |
| `quests/` | `QuestDefinition` |
| `starting-class-rules/` | `StartingClassRuleDefinition` |

## Stable IDs

Canonical IDs match:

```regex
^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)+$
```

Use a category followed by useful namespaces and a specific name:

```text
actor.hero.james
class.magic.black-mage
dialogue.prologue.test-room-guide
stat.max-hp
item.consumable.potion
equipment.weapon.iron-sword
ability.black-magic.fire
enemy.forest.green-slime
encounter.forest.slimes-01
quest.prologue.first-steps
newgame.class-rule.base.default
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
| `baseStatistics` | object of ID → integer | Keys reference statistics. |
| `startingAbilityIds` | ID array | Actor-intrinsic abilities; references abilities. |

```json
{
  "schemaVersion": 1,
  "id": "actor.hero.james",
  "displayNameKey": "actor.james.name",
  "baseStatistics": { "stat.max-hp": 84, "stat.strength": 9 },
  "startingAbilityIds": []
}
```

An actor is identity, not a campaign's current build. The selected class and level are
written to `ActorProgressState` when a new game is created. Consequently James can be a
Vanguard in one save and a White Mage in another without changing `actor.hero.james`.

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

### Starting-class rule

Starting classes are composed through additive rules instead of a Boolean on each class.
This matters for data mods: mods cannot overwrite vanilla class files, but a rule owned by a
mod can still include or exclude a vanilla class by stable ID.

| Field | Type | Notes |
|---|---|---|
| `includeClassIds` | ID array | Class IDs contributed to the selection pool. |
| `excludeClassIds` | ID array | Class IDs removed from the final pool. |

```json
{
  "schemaVersion": 1,
  "id": "newgame.class-rule.base.default",
  "includeClassIds": [
    "class.martial.vanguard",
    "class.magic.black-mage",
    "class.magic.white-mage"
  ],
  "excludeClassIds": []
}
```

The final pool is `union(all includeClassIds) minus union(all excludeClassIds)`.
Exclusion wins globally, independent of filesystem or dependency order. Every referenced
class must exist and belong to the correct category. At least one rule must exist and the
resolved pool cannot be empty. Duplicate IDs within one list and including/excluding the
same ID in one record are validation errors.

### Dialogue

Milestone 2 supports only one speaker and an ordered list of literal placeholder lines. This
is intentionally not a branching conversation or cutscene language.

| Field | Type | Notes |
|---|---|---|
| `speakerName` | string | Nonblank placeholder text shown above the dialogue. |
| `lines` | string array | At least one nonblank line, displayed in authored order. |

```json
{
  "schemaVersion": 1,
  "id": "dialogue.prologue.test-room-guide",
  "speakerName": "Test Room Guide",
  "lines": [
    "Hello, James. This room remembers what happens here."
  ]
}
```

Literal text is a documented temporary Milestone 2 choice because localization is explicitly
deferred. Do not add conditions, choices, portraits, actions, arbitrary commands, or resource
paths to this record. The exploration scene selects the record through its stable dialogue ID.

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

Statistic IDs form one extensible namespace shared by actor bases, class bonuses, enemy
values, equipment modifiers, and future code-owned targeting rules. Runtime combat statistic
resolution enumerates the loaded `StatisticDefinition` records rather than a hard-coded enum,
so a valid mod statistic automatically participates. When an actor or enemy omits a registered
statistic, its `defaultValue` is used; an actor's current class bonus is then added. The final
derived value must remain inside the inclusive range.

This clarification adds no JSON field and does not make current HP or current MP authored
statistics. Those mutable resources belong to future transient battle state. A future AI
selector may reference a stable statistic ID, but AI-profile content is not defined yet.

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
| `formationFootprint` | object | Optional rectangular `rows` and `columns`; omitted means `1 × 1`, but explicit `null` is invalid. |
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

`formationFootprint.rows` and `.columns` must each be from `1` through `4`. The
footprint is authored on the enemy species rather than repeated in each encounter. It
extends from an encounter's top-front anchor toward increasing rows and increasing depth
columns. Explicit JSON `null` is invalid; omit the property to select the compatible
`1 × 1` default. Footprints are definition data, never save data or sprite-derived values.
The content DTO converts to the core `FormationFootprint` without clamping either value;
catalog publication occurs only after the authored dimensions pass validation.

### Encounter

| Field | Type | Notes |
|---|---|---|
| `enemyGroup` | array | Ordered entries contain `enemyId` and a canonical enemy formation anchor `slotId`. |
| `battlefieldId` | ID or null | Presentation lookup, never a resource path. |
| `musicCueId` | ID or null | Presentation lookup, never an audio path. |

```json
{
  "schemaVersion": 1,
  "id": "encounter.forest.slimes-01",
  "enemyGroup": [
    { "enemyId": "enemy.forest.green-slime", "slotId": "formation.enemy.r1.c0" },
    { "enemyId": "enemy.forest.green-slime", "slotId": "formation.enemy.r2.c0" }
  ],
  "battlefieldId": "battlefield.forest.day",
  "musicCueId": "music.battle.normal"
}
```

Enemy slot IDs use exactly `formation.enemy.r<row>.c<column>`, where both coordinates
are zero-based from `0` through `3`. Row `0` is the top; column `0` is the front, closest
to the party. The slot is the enemy footprint's top-front cell. For example, a `2 × 2`
enemy at `formation.enemy.r1.c0` occupies `(r1,c0)`, `(r1,c1)`, `(r2,c0)`, and
`(r2,c1)`. Every occupied cell must remain in the enemy 4 × 4 grid, and two entries may
not overlap. Array order is preserved and produces deterministic battle-local IDs
`enemy-0`, `enemy-1`, and so on.

Party coordinates use the same side-relative meaning on a separate 4 × 2 grid, but they
are not encounter content in Milestone 2.75. The active party is placed temporarily from
party order and is not persisted.

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
- null, nonpositive, or oversized enemy footprints, malformed encounter anchors,
  footprints outside the grid, overlapping placements, or
  quest objective IDs duplicated within a quest;
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

Additive fields must have safe defaults. `formationFootprint` is such a field: enemy schema
version `1` remains valid when it is omitted, including existing mod records authored before
Milestone 2.8. A breaking category shape change increments
that category's `schemaVersion` and adds an explicit content migration before old files are
removed. The canonical encounter-slot restriction is instead a public data-contract change,
so Milestone 2.75 raises the mod `gameApiVersion` to `2`; it does not alter record schema or
save format. Never infer identity from filename changes. Status effects, shops, dialogue,
and cutscene schemas will be added only when their first playable use case is built.
