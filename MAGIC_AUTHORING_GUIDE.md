# Magic authoring guide

## The three records involved

A usable party spell needs three separate facts:

1. A `MagicDisciplineDefinition` creates a stable spellbook/menu container.
2. An `AbilityDefinition` creates the individual executable spell.
3. A class (or actor plus class) grants both the spell and access to at least one discipline
   listed by that spell.

This separation supports replayable class builds and multi-discipline spells without turning
a discipline into an executable command. Unlocking a discipline never teaches every spell in
it, and learning a spell without a matching discipline does not make it executable.

The current code validates and resolves this structure. A learned, cost-free Magic ability can
reuse the same single-enemy damage formula as Attack and assign Fire, Ice, Lightning, Slash, or
Energy through `damageTypeId`. It still uses Strength and Defense until a dedicated magical
stat formula is designed. Guard effects, healing, MP spending, and a visible Magic menu remain
deferred.

## Step 1: create a magic discipline

Create `game/content/magic-disciplines/restoration.json`:

```json
{
  "schemaVersion": 1,
  "id": "magic-discipline.restoration",
  "displayNameKey": "magic-discipline.restoration.name",
  "descriptionKey": "magic-discipline.restoration.description"
}
```

The discipline is only a named container. It has no cost, target, power, damage type, or
effect. Future UI can use its stable ID to open the appropriate spell list.

## Step 2: create one Magic ability

Until a dedicated healing or magical-damage ruleset exists, use a currently supported contract.
This example creates a free defensive spell using the Guard-style ruleset:

`game/content/abilities/aegis.json`

```json
{
  "schemaVersion": 1,
  "id": "ability.restoration.aegis",
  "displayNameKey": "ability.aegis.name",
  "descriptionKey": "ability.aegis.description",
  "abilityKindId": "ability-kind.magic",
  "magicDisciplineIds": [
    "magic-discipline.restoration"
  ],
  "targetingId": "target.self",
  "costStatisticId": null,
  "costAmount": 0,
  "rulesetId": "rules.defense.guard",
  "numericParameters": {
    "damage-reduction": 0.35
  }
}
```

Do not write `rules.damage.magic`, `target.ally.single`, or another plausible-looking ID until
that behavior is implemented and registered in C#. Strict rejection now prevents content that
looks valid but has no executable meaning later.

An elemental attack spell may currently reuse `target.enemy.single` plus
`rules.damage.physical`, include a positive `power`, and set `damageTypeId` to
`damage-type.fire`, `damage-type.ice`, or `damage-type.lightning`. The damage type controls
enemy affinity; it does not change the Strength/Defense formula. See
`MILESTONE_4_3_GUIDE.md`.

Do not charge `stat.max-mp`. Maximum MP is definition/statistic data, while current MP will be
mutable battle state. Leave cost null/zero until the resource milestone defines that boundary.

## Step 3: grant container access and the individual spell

For a base-game class you own, add both unlocks:

```json
{
  "schemaVersion": 1,
  "id": "class.magic.white-mage",
  "displayNameKey": "class.white-mage.name",
  "baseStatisticBonuses": {
    "stat.max-mp": 8,
    "stat.defense": 1
  },
  "abilityUnlocks": [
    {
      "level": 1,
      "abilityId": "ability.restoration.aegis"
    }
  ],
  "magicDisciplineUnlocks": [
    {
      "level": 1,
      "magicDisciplineId": "magic-discipline.restoration"
    }
  ]
}
```

At level 1, the class can open Restoration and has learned Aegis, so Aegis appears in that
container and in the flat executable ability-ID projection.

If the ability unlock were level 3 and discipline unlock level 1, the container would be empty
until level 3. If the discipline were never unlocked, the learned spell would remain unavailable.

## Multi-discipline spells

A spell may list more than one discipline:

```json
"magicDisciplineIds": [
  "magic-discipline.restoration",
  "magic-discipline.nature"
]
```

If a class has access to both, the spell appears in both containers but only once in
`ExecutableAbilityIds`. Use this for genuine cross-school organization, not as a substitute
for class grants. Duplicate discipline IDs in one spell are errors.

## Actor-intrinsic magic

An actor may learn a spell through `startingAbilityIds`, but the current class must still grant
matching discipline access. This is useful for a character-specific spell that should follow
the hero between compatible classes. James should remain class-neutral unless the story truly
requires an intrinsic ability.

## Community mod limitation

A data mod may add namespaced disciplines, spells, and classes. It cannot add executable C# or
patch an existing vanilla class's unlock arrays. To distribute a new spell today, grant it from
the mod's own class. A future explicit composition record can be designed if extending vanilla
class progression becomes a proven modding requirement.

## Validate the complete relationship

Run both content passes because namespace and dependency mistakes often appear only when mods
are combined with base content:

```powershell
dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content examples/mods

dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
```

## Common mistakes

| Symptom | Cause |
|---|---|
| Magic has no listed discipline | `ability.magic-missing-disciplines` |
| A Skill lists a discipline | `ability.skill-has-magic-disciplines` |
| The spell ID is learned but not executable | The current class has no matching discipline unlock. |
| The discipline menu is empty | Access is unlocked, but no eligible learned spell lists that discipline. |
| The same spell appears in two menus | Legal multi-discipline behavior; the executable flat list still contains it once. |
| A plausible new ruleset is rejected | JSON cannot create code behavior; add a trusted ruleset contract first. |

## Checklist

- Create the discipline record before referencing it.
- Mark the spell `ability-kind.magic`.
- List at least one real `magic-discipline.` ID.
- Use a currently supported target/ruleset/parameter contract.
- Keep costs null/zero until current resources exist.
- Grant the individual spell through `abilityUnlocks` or `startingAbilityIds`.
- Grant at least one matching container through `magicDisciplineUnlocks`.
- Validate base content, base plus mods, and headless tests.
