# Content schema

For a friend-friendly workflow and examples, start with `CONTENT_AUTHORING_GUIDE.md`. This file
is the strict field, ID, and validation reference behind that guide.

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
| `schemaVersion` | integer | Required explicitly in JSON. Starts at `1`; increment only when this record category needs migration. |
| `id` | string | Permanent, globally unique, stable ID. |

The initial directory-to-type mapping is. Each category may contain authoring subdirectories;
the top-level directory still determines the definition type.

| Directory | Definition |
|---|---|
| `actors/` | `ActorDefinition` |
| `classes/` | `ClassDefinition` |
| `dialogues/` | `DialogueDefinition` |
| `statistics/` | `StatisticDefinition` |
| `items/` and its subdirectories | `ItemDefinition` |
| `equipment/` and its subdirectories | `EquipmentDefinition` |
| `loot-tables/` | `LootTableDefinition` |
| `abilities/` | `AbilityDefinition` |
| `magic-disciplines/` | `MagicDisciplineDefinition` |
| `enemies/` | `EnemyDefinition` |
| `encounters/` | `EncounterDefinition` |
| `quests/` | `QuestDefinition` |
| `starting-class-rules/` | `StartingClassRuleDefinition` |
| `status-effects/` | `StatusEffectDefinition` |
| `maps/` | `MapDefinition` |

### Map and transition

`maps/` records own stable exploration map identities and named logical spawn points. A spawn
contains only tile coordinates and facing; it never contains a Godot scene path. The composition
root maps supported IDs to scenes.

Each map may contain a `transitions` array. A transition contains an ID, one `sourceCell`, a
`destinationMapId`, and a `destinationSpawnId`. The validator requires the destination map and
spawn to exist and requires the source cell to be authored over `T`. The exploration controller
evaluates the map-owned source cell after a successful step and sends the typed transition request
to `GameRoot`, which updates `GameState.Location` before replacing the disposable map scene.

`MapDefinition` records also require `width`, `height`, and `rows`. Each row is an ASCII string
using only `#` (blocked), `.` (passable), `E` (passable encounter tile), and `T` (passable
transition tile). `encounters` contains map-owned marker objects with `x`, `y`, `encounterId`,
and `clearedFlagId`. Coordinates are zero-based with `Rows[y][x]`.

## Stable IDs

Canonical IDs match:

```regex
^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)+$
```

Use a category followed by useful namespaces and a specific name:

```text
actor.hero.james
class.magic.black-mage
dialogue.prologue.test-room-guide
stat.max-hp
item.consumable.potion
equipment.weapon.iron-sword
loot-table.forest.green-slime
ability.black-magic.fire
magic-discipline.black
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
| `mod.example.starter-pack` | `class.example.starter-pack.chronoguard`, `ability.example.starter-pack.temporal-guard`, `loot-table.example.starter-pack.clockwork-slime` |

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
Knight in one save and a White Mage in another without changing `actor.hero.james`.

### Class

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key. |
| `baseStatisticBonuses` | object of ID → integer | Additive bonuses; keys reference statistics. |
| `abilityUnlocks` | array | Entries contain `level` and `abilityId`. |
| `magicDisciplineUnlocks` | array | Entries contain `level` and `magicDisciplineId`. Grants container access, not spells. |

```json
{
  "schemaVersion": 1,
  "id": "class.magic.white-mage",
  "displayNameKey": "class.white-mage.name",
  "baseStatisticBonuses": { "stat.max-mp": 8, "stat.defense": 1 },
  "abilityUnlocks": [
    { "level": 1, "abilityId": "ability.restoration.aegis" }
  ],
  "magicDisciplineUnlocks": [
    { "level": 1, "magicDisciplineId": "magic-discipline.restoration" }
  ]
}
```

`abilityUnlocks` grants individual executable abilities. `magicDisciplineUnlocks` grants
access to non-executable Magic containers. A class does not learn every spell in a discipline
just because it can open that container.

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
    "class.martial.knight",
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

Milestone 5.3A supports one speaker localization key and an ordered list of line localization
keys. This remains a linear exchange, not a branching conversation or cutscene language.

| Field | Type | Notes |
|---|---|---|
| `speakerNameKey` | string | Required localization key for the speaker label. |
| `lineTextKeys` | string array | At least one nonblank localization key, displayed in authored order. |

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

The referenced text belongs in the base locale bundle, for example
`game/localization/en/dialogue/prologue/test-room-guide.json`. Do not add conditions,
choices, portraits, actions, arbitrary commands, or resource paths to this record. The
exploration scene selects the record through its stable dialogue ID.

### Localization bundles

Locale text is authored beneath `game/localization/{locale}/` and loaded recursively. Every
file contains schemaVersion 1, a matching locale, and a texts dictionary of stable key-to-string
entries. File paths are organizational only. Duplicate keys across files, blank keys or values,
malformed records, and missing base-locale references are validation errors. Runtime lookup uses
`??missing.key??` as a development fallback. Data-mod localization is deferred and base keys
cannot be replaced.

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
statistics. Milestone 3.0 copies resolved `stat.max-hp` into separate transient current HP
when it creates an initial combat snapshot; current MP remains deferred. A future AI selector
may reference a stable statistic ID, but AI-profile content is not defined yet.

### Item and equipment

An equipment record decorates an item record instead of inheriting from it in JSON.
This keeps common inventory/shop data in exactly one place.

| Item field | Type | Notes |
|---|---|---|
| `displayNameKey`, `descriptionKey` | string | Localization keys. |
| `buyPrice`, `sellPrice` | integer | Nonnegative base values. |
| `maxStack` | integer, optional | Defaults to `99` for ordinary items. Unique items always use an effective maximum of `1`. |
| `unique` | boolean, optional | Defaults to `false`. Use for one-of-a-kind story or quest items; it forces an effective max stack of `1`. |

| Equipment field | Type | Notes |
|---|---|---|
| `itemId` | ID | Unique reference to its item record. |
| `slotId` | ID | Stable game-owned equipment slot. |
| `statisticModifiers` | object of ID → integer | Keys reference statistics. |
| `attack` | integer, optional | Nonnegative direct weapon offensive value. Omit it for armor, shields, and accessories; omitted means `0`. A positive value is legal only for `slot.weapon.*`. |
| `weaponDamagePercentages` | object of damage-type ID → integer, optional | Omit it for non-weapons. A nonempty profile is legal only for `slot.weapon.*`; every value is positive and the total is exactly `100`. |
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
  "statisticModifiers": {},
  "attack": 4,
  "weaponDamagePercentages": { "damage-type.slash": 100 },
  "grantedAbilityIds": [],
  "specialEffectIds": []
}
```

The supported base-game save slots are `slot.weapon.main-hand`, `slot.weapon.off-hand`,
`slot.armor.body`, `slot.armor.feet`, `slot.armor.helm`, `slot.accessory.one`, and
`slot.accessory.two`. They are stable save keys, not display labels. Equipment authored with
`slot.accessory` is compatible with either accessory save slot; all other equipment selects one
specific slot.

Weapon profiles may mix Slash, Energy, Fire, Ice, and Lightning. Milestone 4.8 activates only
an equipped weapon's single 100% profile for intrinsic `ability.command.attack`; a mixed profile
remains valid content but cannot be equipped for battle until multi-component damage exists.
Omission remains compatible and leaves Attack on its authored/legacy damage type.

`attack` is weapon damage, while `statisticModifiers` remains the separate future mechanism for
armor/accessory-style statistic bonuses. Basic weapons must use `attack`, not `stat.strength`.
`specialEffectIds` is an optional unique list of reserved `equipment-effect.*` IDs. It is
presentation data only until a later code-owned effect contract assigns behavior to an ID.

### Loot table

A loot table owns reusable item-drop authoring separately from enemy combat data. Tables are
not player-facing and therefore need no display-name key.

| Field | Type | Notes |
|---|---|---|
| `entries` | array | Required, including when empty. Every entry is an independent future roll. |

Each entry contains:

| Entry field | Type | Notes |
|---|---|---|
| `itemId` | ID | References an `ItemDefinition`. |
| `chance` | decimal | Required probability from `0` through `1`, inclusive. |
| `minQuantity` | integer | Required; at least `1`. |
| `maxQuantity` | integer | Required; at least `minQuantity`. |

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

Entries retain authored order and are independent; repeated item IDs are legal. A future
resolver may therefore succeed on more than one entry for the same item and aggregate the
awards afterward. An empty `entries` array is legal and disables all drops for every enemy
that references the table. Explicit JSON `null` is invalid. Milestone 3.06 only loads and
validates these records—it does not roll chance, grant inventory, or define victory behavior.
See `LOOT_TABLE_AUTHORING_GUIDE.md`.

### Ability

| Field | Type | Notes |
|---|---|---|
| `displayNameKey`, `descriptionKey` | string | Localization keys. |
| `abilityKindId` | ID | Optional; defaults to `ability-kind.skill`. Supported values are `ability-kind.skill` and `ability-kind.magic`. |
| `magicDisciplineIds` | ID array | Empty for Skills; required and nonempty for Magic. References `magic-disciplines/`. |
| `targetingId` | ID | Closed code-owned target contract; currently `target.self`, `target.enemy.single`, or `target.ally.single`. |
| `costStatisticId` | ID or null | Null/zero for no cost, or `stat.max-mp` to spend transient current MP. |
| `costAmount` | integer | Nonnegative amount from the selected supported resource pool. |
| `rulesetId` | ID | Selects one supported code-owned behavior; currently `rules.defense.guard`, `rules.damage.physical`, or `rules.healing.flat`. |
| `damageTypeId` | ID or null | Optional code-owned type for a damage ruleset: `damage-type.slash`, `damage-type.blunt`, `damage-type.energy`, `damage-type.fire`, `damage-type.ice`, or `damage-type.lightning`. Omitted legacy physical damage defaults to Energy. |
| `numericParameters` | object of string → number | Exact required keys and ranges are owned by the selected ruleset. Extra keys are errors. |

```json
{
  "schemaVersion": 1,
  "id": "ability.restoration.aegis",
  "displayNameKey": "ability.aegis.name",
  "descriptionKey": "ability.aegis.description",
  "abilityKindId": "ability-kind.magic",
  "magicDisciplineIds": ["magic-discipline.restoration"],
  "targetingId": "target.self",
  "costStatisticId": null,
  "costAmount": 0,
  "rulesetId": "rules.defense.guard",
  "numericParameters": { "damage-reduction": 0.35 }
}
```

`rulesetId` is a constrained escape hatch for game rules, not a generic scripting
language. JSON may select and tune only a contract implemented by the current build. A new
string cannot create behavior. Add a ruleset only for a demonstrated family of abilities,
then add its stable constants, target compatibility, exact parameter validation, core behavior,
and focused tests together. See `ABILITY_RULESET_DEVELOPER_GUIDE.md`.

Current authored contracts are:

| Targeting ID | Ruleset ID | Required numeric parameters |
|---|---|---|
| `target.self` | `rules.defense.guard` | `damage-reduction` greater than `0` and at most `1` |
| `target.enemy.single` | `rules.damage.physical` | `power` greater than `0` |
| `target.ally.single` | `rules.healing.flat` | `power` positive whole number no greater than `Int32.MaxValue` |

Physical damage uses `target.enemy.single` plus `rules.damage.physical`. For integer HP, its
deterministic calculation is:

```text
rawDamage = max(1, attacker Strength + authored power - defender Defense)
typedDamage = modifier == -100
    ? 0
    : max(1, floor(rawDamage * (100 + modifier) / 100))
roundedDamage = typedDamage
appliedDamage = min(roundedDamage, target CurrentHp)
```

The signed target modifier is read from the matching enemy damage type: positive values are
weaknesses, negative values are resistances, omitted values are neutral, and `-100` is
immunity. Reaching zero current HP
marks the transient combatant defeated. `rules.healing.flat` uses a living same-side target and
adds its positive whole-number `power`, clamped to the target's resolved maximum HP. Current HP
and current MP are runtime combat state, not ability or save/content fields. Guard remains a
validated authoring contract but is not executed yet.

Milestone 3.12 does not add an AI field or change this schema. Its basic enemy planner scans an
enemy's existing `abilityIds` in authored order and selects the first cost-free ability using
the currently executable physical contract. Target choice and Speed ordering are runtime core
rules, not additional ability or enemy JSON properties.

Milestone 4.5 supports only null/zero costs and `stat.max-mp` costs. The authored ID selects the
MP resource family; the mutable value is the combat snapshot's separate `CurrentMp`, while
`stat.max-mp` remains its immutable maximum statistic. Other cost IDs remain invalid until their
resource pools are explicitly implemented.

An omitted `abilityKindId` remains compatible and means `ability-kind.skill`. Skills appear
directly as executable commands and must not list magic disciplines. Magic abilities are
ordinary executable ability IDs, but a party actor may use one only when the actor has learned
that specific ability and has access to at least one listed magic discipline. Discipline IDs
are never submitted as `CombatCommand.AbilityId`.

### Magic discipline

| Field | Type | Notes |
|---|---|---|
| `displayNameKey`, `descriptionKey` | string | Localization keys for the future menu container. |

```json
{
  "schemaVersion": 1,
  "id": "magic-discipline.restoration",
  "displayNameKey": "magic-discipline.restoration.name",
  "descriptionKey": "magic-discipline.restoration.description"
}
```

A magic discipline is a non-executable authored container such as a future spellbook category.
It has no targeting rule, cost, ruleset, effect, or command behavior. Milestone 3.05 defines
the category but intentionally adds no concrete base-game disciplines or spells.

### Status effect

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key for future status presentation. |
| `descriptionKey` | string or null | Optional localization key. |
| `stackingRuleId` | ID | Closed rule: `refresh-duration`, `ignore-if-present`, or `replace`. |
| `defaultDuration` | integer | Positive timeline-time duration. |
| `durationUnitId` | ID | Currently only `timeline-time`. |
| `effectKindIds` | ID array | Closed behavior selectors; current hook is `status-effect.modify-speed-percent`. |
| `speedPercentModifier` | integer | Nonzero only with the speed-modifier effect kind; bounded by validation. |

Status definitions are declarative and cannot contain scripts, formulas, reflection names, or
runtime code. Active status instances live only in transient `CombatSnapshot` state. Milestone
5.1 adds the category and validation contract but no production status records; test fixtures may
author status definitions in memory. Status resistance, immunity, random hit chance, stat/damage
hooks, ticking effects, and player-facing status content remain deferred.

### Enemy

| Field | Type | Notes |
|---|---|---|
| `displayNameKey` | string | Localization key. |
| `level` | integer | At least `1`. |
| `statistics` | object of ID → integer | Keys reference statistics. |
| `abilityIds` | ID array | References abilities. |
| `damageTypePercentModifiers` | object of damage-type ID → integer | Optional sparse signed percentages. Positive is weakness, negative is resistance, `-100` is immunity, and omission is neutral. Values below `-100` are invalid. |
| `formationFootprint` | object | Optional rectangular `rows` and `columns`; omitted means `1 × 1`, but explicit `null` is invalid. |
| `lootTableId` | ID or null | Required member in enemy schema 2; references `loot-tables/`, or explicit null means no item drops. |

```json
{
  "schemaVersion": 2,
  "id": "enemy.forest.green-slime",
  "displayNameKey": "enemy.green-slime.name",
  "level": 1,
  "statistics": { "stat.max-hp": 22, "stat.strength": 3 },
  "abilityIds": ["ability.enemy.tackle"],
  "damageTypePercentModifiers": { "damage-type.fire": 50 },
  "lootTableId": "loot-table.forest.green-slime"
}
```

`formationFootprint.rows` and `.columns` must each be from `1` through `4`. The
footprint is authored on the enemy species rather than repeated in each encounter. It
extends from an encounter's top-front anchor toward increasing rows and increasing depth
columns. Explicit JSON `null` is invalid; omit the property to select the compatible
`1 × 1` default. Footprints are definition data, never save data or sprite-derived values.
The content DTO converts to the core `FormationFootprint` without clamping either value;
catalog publication occurs only after the authored dimensions pass validation.

Enemy schema version `2` requires authors to write `lootTableId`, even when its value is null.
This prevents a forgotten property from silently becoming a no-loot enemy. The retired
schema-1 `loot` array is not accepted; its entries move unchanged into a standalone schema-1
loot-table record. Loot tables are reusable definition data and never become per-battle or
save state.

Damage types are closed code-owned IDs, not a new content category. See
`MILESTONE_4_3_GUIDE.md` for formula, weapon-profile, and compatibility details.

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
- missing/wrong-category loot-table and item references, null loot entries, and invalid
  loot chances or quantity ranges;
- null, nonpositive, or oversized enemy footprints, malformed encounter anchors,
  footprints outside the grid, overlapping placements, or
  quest objective IDs duplicated within a quest;
- unknown ability rulesets/targeting modes, missing or extra ruleset parameters, and
  unsupported slots/flags where their registries exist;
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

Additive fields must have safe defaults. `formationFootprint` was such a field when introduced:
omitting it still produces `1 × 1` in current enemy schema 2. Breaking public shapes require
an explicit compatibility decision. Milestone 2.75 raised the mod `gameApiVersion` to `2` for
canonical encounter slots. Milestone 3.06 deliberately raises it to `3`, raises enemy schema
to `2`, and rejects the retired embedded `loot` array in favor of standalone loot tables.
This pre-release clean break updates all checked-in content rather than carrying two reward
formats into future gameplay. It changes neither `SaveFormatVersion` nor existing campaign
state because definitions and unrolled drop possibilities are not save data. Never infer
identity from filename changes. Status effects, shops, dialogue, and cutscene schemas will be
added only when their first playable use case is built.

Milestone 4.3 adds optional damage-type fields without changing schema versions or mod API 3.
Omitted enemy modifier maps are neutral, omitted weapon profiles remain compatible, and
omitted legacy physical-ability types resolve as Energy. Explicit null maps are invalid.

The later API 4 class-identity change retires `class.martial.vanguard` in favor of
`class.martial.knight`. It intentionally rejects API-3 mods, while save format 2 migrates
existing campaign class selections during load.

Every JSON record must write `schemaVersion` even when it is `1`. The C# default exists for
hand-built tests and tools only; accepting a missing JSON version would make future migrations
unable to distinguish old content from an authoring omission.
