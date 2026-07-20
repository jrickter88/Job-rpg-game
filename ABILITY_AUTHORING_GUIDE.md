# Ability and Skill authoring guide

## Read this first

An **ability** is the general content record for something a combatant may eventually execute.
A **Skill** is one kind of ability that appears directly in the future command menu. Magic is
the other implemented kind and is covered in `MAGIC_AUTHORING_GUIDE.md`.

The repository can load, validate, grant, and place ability IDs into a combat snapshot.
Milestone 3.10 can also execute one narrow family in pure core tests: a free
`target.enemy.single` or `target.combatant.single` + `rules.damage.physical` ability. There is still no Godot battle menu,
animation, MP use, Guard execution, or automatic turn flow. Adding JSON selects only behavior
the trusted C# resolver already supports; it never creates a new mechanic by itself.

## What can be authored without new C#

The game accepts only code-owned target and ruleset contracts that it understands:

| Targeting ID | Ruleset ID | Required parameters | Legal values |
|---|---|---|---|
| `target.self` | `rules.defense.guard` | `damage-reduction` | Greater than `0` and at most `1`; validated but not executed yet |
| `target.enemy.single` | `rules.damage.physical` | `power` | Greater than `0`; executable with supported MP costs |
| `target.combatant.single` | `rules.damage.physical` | `power` | Greater than `0`; may select any living combatant, including the actor |
| `target.ally.single` | `rules.healing.flat` | `power` | Positive whole-number HP restoration; executable with supported MP costs |

For example, `0.25` means a 25% reduction. The Guard authoring contract is validated now so
bad data cannot reach its future resolver. A physical-damage ability also selects one supported
`damageTypeId`: Slash, Energy, Fire, Ice, or Lightning. The resolver calculates
`max(1, Strength + power - Defense)`, applies the target's signed percentage modifier for that
type, floors once, and clamps applied damage to remaining HP. New damage abilities should
author the type explicitly; omitted legacy physical-damage definitions use Blunt.

A new ability may reuse one of these rows and choose new tuning values. A new target mode or a
genuinely different effect—healing, poison status, stealing, resurrection, and so on—
requires a small trusted C# implementation and tests. Do not invent a JSON string such as
`rules.damage.fire`; Fire is a `damageTypeId`, while the ruleset owns the formula. The validator
rejects unsupported behavior IDs on purpose. See
`ABILITY_RULESET_DEVELOPER_GUIDE.md` for that workflow.

Example damage members:

```json
"rulesetId": "rules.damage.physical",
"damageTypeId": "damage-type.slash",
"numericParameters": { "power": 4 }
```

### Add a battle spell animation

Magic abilities may opt into a battle animation by setting `battleAnimationId` to a stable
`animation.*` ID. The ID is presentation-only and does not create a new combat behavior. The
Godot catalog in `src/Rpg.Game/Encounters/BattleSpellAnimationCatalog.cs` maps that ID to the
uploaded spritesheet, frame region, timing, and scale.

For a new Ice or Lightning animation:

1. Add the approved spritesheet under `game/assets/`.
2. Add one catalog entry with its asset path and frame geometry.
3. Add the matching `battleAnimationId` to the ability JSON, for example
   `"battleAnimationId": "animation.spell.ice"`.

If the asset and catalog entry are ready, ask Codex to wire the new ID; no combat resolver,
targeting, damage, or magic-discipline code needs to change.

See `MILESTONE_4_3_GUIDE.md` for affinity percentages and rounding.

## Add one direct Skill

Create one JSON file under `game/content/abilities/heroes/` for a hero/class ability or
`game/content/abilities/enemies/` for an enemy ability. The filename and folder are for
organization; the permanent `id` is the identity used by content, runtime state, mods, and
future saves. The loader discovers ability files recursively.

Example: `game/content/abilities/heroes/shield-focus.json`

```json
{
  "schemaVersion": 1,
  "id": "ability.knight.shield-focus",
  "displayNameKey": "ability.shield-focus.name",
  "descriptionKey": "ability.shield-focus.description",
  "abilityKindId": "ability-kind.skill",
  "magicDisciplineIds": [],
  "targetingId": "target.self",
  "costStatisticId": null,
  "costAmount": 0,
  "rulesetId": "rules.defense.guard",
  "numericParameters": {
    "damage-reduction": 0.25
  }
}
```

`abilityKindId` may be omitted because Skill is the compatibility default. Writing it while
learning is often clearer. A Skill must have no `magicDisciplineIds`.

Use `costStatisticId: null` and `costAmount: 0` for a free ability. Milestone 4.5 also supports
`costStatisticId: "stat.max-mp"` with a nonnegative amount; that stable ID selects the separate
transient CurrentMp pool and does not lower the maximum statistic. Other resource IDs are not
implemented.

## Decide who learns it

### First class-kit examples

Milestone 4.6 uses class-owned level-one unlocks for the first real kits. Knight's class ID
`class.martial.knight` grants the direct Skill
`ability.knight.power-strike`. Black Mage grants the individual Fire, Ice, and Lightning spell
IDs through `abilityUnlocks`; it also grants the separate Black Magic container through
`magicDisciplineUnlocks`. A Magic spell needs both facts before it becomes executable.

Milestone 4.7 adds `ability.white-magic.cure`: White Mage grants both the White Magic container
and this learned Cure spell. Cure uses `target.ally.single` with `rules.healing.flat`; its
positive whole-number `power` is the deterministic flat HP restoration before max-HP clamping.

Creating an ability record does not grant it to anyone. Choose the owner that matches the game
design.

### Learn from a class level

Add an entry to that class's `abilityUnlocks` array:

```json
"abilityUnlocks": [
  {
    "level": 1,
    "abilityId": "ability.knight.shield-focus"
  }
]
```

This is the normal choice for a class Skill. Level must be at least `1`. Each ability may
appear only once in one class table.

### Make it intrinsic to a hero

Add its ID to an actor's `startingAbilityIds`:

```json
"startingAbilityIds": [
  "ability.hero.focus"
]
```

Use this only when the ability belongs to that character regardless of selected class. James
is intentionally class-neutral, so class abilities normally belong on the class record.
`ability.command.attack` is James's class-independent basic combat command. Milestone 4.6 adds
class-owned level-one grants; future class abilities should continue to use class records rather
than changing James's identity record.

### Give it to an enemy

Add its ID to the enemy's `abilityIds` array. Enemy ability order is authored order. Duplicate
IDs are rejected because they would create ambiguous AI/menu behavior.

### Give it through equipment

An equipment record may list the ID in `grantedAbilityIds`. Equipment behavior is not active
yet, so this is only a validated future relationship. Do not also hard-code the same grant in
gameplay code.

## Base-game authoring versus data mods

As the game developer, you may edit a base class under `game/content/classes/` to teach it a
new base ability. A community data mod may add its own namespaced ability and its own class,
then connect those records. Current additive mods cannot patch a vanilla class or actor. That
is a deliberate conflict-avoidance boundary, not a filename trick.

For a mod with manifest ID `mod.alex.arcane-pack`, valid owned IDs include:

```text
ability.alex.arcane-pack.shield-focus
class.alex.arcane-pack.warder
```

See `MODDING.md` before creating mod content.

## Validate the result

From the repository root in PowerShell:

```powershell
dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content

dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
```

For the checked-in example mods too:

```powershell
dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content examples/mods
```

Do not commit until these commands return exit code `0`.

## Common validation problems

| Problem code | Meaning | Typical fix |
|---|---|---|
| `ability.targeting-unsupported` | The target string has no code-owned behavior. | Use a supported targeting ID or implement a new contract in C#. |
| `ability.ruleset-unsupported` | The ruleset string has no code-owned behavior. | Reuse a supported ruleset or follow the developer guide. |
| `ability.ruleset-targeting-mismatch` | The ruleset cannot use that target mode. | Use the target listed in the contract table. |
| `ability.parameter-missing` | A required tuning value is absent. | Add the exact required key. |
| `ability.parameter-unsupported` | A parameter is misspelled or belongs to another ruleset. | Remove it or correct its name. |
| `ability.parameter-out-of-range` | The tuning value is unsafe for that ruleset. | Choose a legal value. |
| `reference.missing` | A grant points to an ID that did not load. | Check the ID and ensure its JSON file is included. |
| `reference.wrong-category` | An ability field points to another content category. | Use an ID beginning with `ability.`. |
| `actor.duplicate-starting-ability` | One actor grant is repeated. | Keep the first entry and remove the duplicate. |
| `enemy.duplicate-ability` | One enemy ability is repeated. | Keep one authored occurrence. |

## Checklist

- The JSON file is in `abilities/heroes/` or `abilities/enemies/` and contains
  explicit `schemaVersion: 1`.
- The permanent ID starts with `ability.` and uses lowercase stable-ID syntax.
- Skill/Magic kind, discipline list, targeting, ruleset, and parameters agree.
- Cost is null/zero or a nonnegative `stat.max-mp` amount.
- At least one actor, class, enemy, or future equipment source grants the ability.
- Base and base-plus-mod content validation both pass.
- A focused test is added when a new C# ruleset or target contract is introduced.
## Damage Variance

Damage abilities may optionally author `damageVariance` with inclusive `minimumPercent` and
`maximumPercent` bounds. Omitted magic abilities use 80-120; omitted physical abilities use the
equipped weapon context when available, otherwise 95-105.
