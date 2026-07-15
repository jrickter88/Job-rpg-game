# Ability and Skill authoring guide

## Read this first

An **ability** is the general content record for something a combatant may eventually execute.
A **Skill** is one kind of ability that appears directly in the future command menu. Magic is
the other implemented kind and is covered in `MAGIC_AUTHORING_GUIDE.md`.

The repository can currently load, validate, grant, and place ability IDs into an initial
combat snapshot. It does **not** execute those abilities yet. Adding JSON now proves that the
content and learning relationships are correct; it does not create damage, animation, MP use,
or a visible battle command before those later milestones exist.

## What can be authored without new C#

The game accepts only code-owned target and ruleset contracts that it understands:

| Targeting ID | Ruleset ID | Required parameters | Legal values |
|---|---|---|---|
| `target.self` | `rules.defense.guard` | `damage-reduction` | Greater than `0` and at most `1` |
| `target.enemy.single` | `rules.damage.physical` | `power` | Greater than `0` |

For example, `0.25` means a 25% reduction. The exact effect execution is still deferred, but
the authoring contract is validated now so bad data cannot reach the future resolver.

A new ability may reuse one of these rows and choose new tuning values. A new target mode or a
genuinely different effect—healing, poison, fire damage, stealing, resurrection, and so on—
requires a small trusted C# implementation and tests. Do not invent a JSON string such as
`rules.damage.fire`; the validator rejects unsupported behavior IDs on purpose. See
`ABILITY_RULESET_DEVELOPER_GUIDE.md` for that workflow.

## Add one direct Skill

Create one JSON file under `game/content/abilities/`. The filename is for organization; the
permanent `id` is the identity used by content, runtime state, mods, and future saves.

Example: `game/content/abilities/shield-focus.json`

```json
{
  "schemaVersion": 1,
  "id": "ability.vanguard.shield-focus",
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

Keep `costStatisticId` null and `costAmount` zero for now. The schema retains those fields,
but current MP/resource ownership and cost payment have not been implemented. Referencing
`stat.max-mp` would describe a maximum statistic, not mutable current MP.

## Decide who learns it

Creating an ability record does not grant it to anyone. Choose the owner that matches the game
design.

### Learn from a class level

Add an entry to that class's `abilityUnlocks` array:

```json
"abilityUnlocks": [
  {
    "level": 1,
    "abilityId": "ability.vanguard.shield-focus"
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

- The JSON file is in `abilities/` and contains explicit `schemaVersion: 1`.
- The permanent ID starts with `ability.` and uses lowercase stable-ID syntax.
- Skill/Magic kind, discipline list, targeting, ruleset, and parameters agree.
- Cost is null/zero until current resources are implemented.
- At least one actor, class, enemy, or future equipment source grants the ability.
- Base and base-plus-mod content validation both pass.
- A focused test is added when a new C# ruleset or target contract is introduced.
