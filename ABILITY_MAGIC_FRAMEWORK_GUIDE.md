# Ability and magic framework guide

## Purpose

Milestone 3.05 gives the project vocabulary for future battle commands without implementing
the battle menu or spell effects yet.

The model is:

```text
Ability
├── Skill
│   └── direct executable command
└── Magic
	└── executable spell shown inside one or more unlocked Magic Disciplines
```

That means Guard can appear directly as a command, while a future spell can appear inside a
container such as a spellbook category. The framework does not create White Magic, Black
Magic, Cure, Fire, or any other concrete base-game school yet.

For step-by-step content work, use `ABILITY_AUTHORING_GUIDE.md` for direct Skills and
`MAGIC_AUTHORING_GUIDE.md` for spells. Use `ABILITY_RULESET_DEVELOPER_GUIDE.md` only when the
ability needs a genuinely new trusted C# behavior.

## Content pieces

`AbilityDefinition` now has two additive fields:

| Field | Meaning |
|---|---|
| `abilityKindId` | `ability-kind.skill` or `ability-kind.magic`; omitted means Skill. |
| `magicDisciplineIds` | The magic containers that may show this ability when it is Magic. |

`MagicDisciplineDefinition` is a separate content category under `magic-disciplines/`.
It is a non-executable menu container. It has display text, but no targeting rule, cost,
ruleset, numeric parameters, or combat behavior.

`ClassDefinition` now has two different unlock lists:

| Field | Grants |
|---|---|
| `abilityUnlocks` | One individual Skill or Magic ability. |
| `magicDisciplineUnlocks` | Access to one non-executable Magic container. |

Keep those concepts separate. If a class unlocks a discipline, that does not automatically
teach every spell in it.

## Availability rule

A party actor may use a Magic ability only when both are true:

- the actor has learned the individual Magic ability from actor starting abilities or class
  ability unlocks; and
- the actor's class has unlocked at least one magic discipline listed by that ability.

Skills do not require discipline access.

For example, if James has learned a future `ability.example.cure` but his class has not
unlocked that spell's discipline, Cure is not executable. If his class has unlocked the
discipline but he has not learned Cure, the discipline container can appear empty.

## Snapshot projection

`AbilityAvailabilityResolver` produces a `PartyAbilityAvailability`:

| Property | Meaning |
|---|---|
| `DirectSkillIds` | Learned Skill abilities shown directly as commands. |
| `MagicDisciplines` | Unlocked containers, each with learned matching spell IDs. |
| `ExecutableAbilityIds` | Direct Skills followed by spells discovered through disciplines. |

The flat executable list never contains discipline IDs. A future spell command will still use
the spell ability ID, not the discipline ID.

Direct Skills and discipline spell lists are the authoritative structure. The flat executable
view is derived inside `PartyAbilityAvailability`; callers cannot provide a third list that
quietly disagrees with a future menu.

`CombatantSnapshot.AbilityIds` remains as the compatibility flat executable list. Party
combatants also expose `PartyAbilityAvailability`, `DirectSkillIds`, and `MagicDisciplines`.
Enemies continue to use their authored flat `EnemyDefinition.AbilityIds`; enemy magic
discipline access and spellbook AI are deferred.

## Ordering and duplicates

The resolver preserves author intent:

1. actor `startingAbilityIds`;
2. class `abilityUnlocks` at or below the actor's level;
3. class `magicDisciplineUnlocks` at or below the actor's level.

Duplicate IDs inside one actor, enemy, equipment, class, or spell list are content errors.
When the same legal ability is granted by two different sources—such as an actor-intrinsic
grant followed by its class—the resolver preserves the first occurrence using ordinal stable-ID
comparison. A multi-discipline Magic ability appears inside every matching unlocked discipline,
but only once in `ExecutableAbilityIds`.

## Code-owned execution contracts

Ability kind and menu organization are data-driven, but executable behavior is deliberately
closed. Current content may select:

| Targeting ID | Ruleset ID | Parameter contract |
|---|---|---|
| `target.self` | `rules.defense.guard` | `damage-reduction` in `(0, 1]` |
| `target.enemy.single` | `rules.damage.physical` | `power > 0` |

Unknown target/ruleset IDs and unknown, missing, or out-of-range parameters fail validation.
This prevents a JSON typo or mod-authored method name from becoming a delayed runtime failure.
It does not yet execute Guard or damage; command resolution remains deferred.

Cost fields are also not executable yet. New abilities should use null/zero until mutable
current resource state is implemented; `stat.max-mp` is not current MP.

## Compatibility

Existing ability JSON omits `abilityKindId`, so it still loads as `ability-kind.skill`.
Existing classes omit `magicDisciplineUnlocks`, so they simply unlock no magic containers.
No base content needs to be rewritten, no save fields are added, and the mod data API does
not change.

The follow-up contract hardening accepts the target/ruleset pairs already used by base content
and the checked-in example mod. Arbitrary custom behavior strings were never executable mod
hooks; they now fail early with actionable diagnostics instead of surviving until runtime.

## Validation

The content validator rejects:

- blank, null, or unsupported ability-kind IDs;
- Skills with magic discipline IDs;
- Magic abilities with no discipline IDs;
- blank, duplicate, missing, or wrong-category magic discipline references;
- invalid magic discipline records;
- null, duplicate, below-level-one, missing, or wrong-category class discipline unlocks;
- unsupported targeting/ruleset IDs, incompatible target/ruleset pairs, and missing, extra,
  or out-of-range ruleset parameters;
- duplicate actor starting abilities, equipment grants, and enemy ability IDs.

Validation aggregates independent problems and publishes no catalog until the full pack is
valid.

## Explicitly deferred

This milestone does not implement concrete magic schools, concrete spells, MP, costs beyond
the existing authored fields, spell execution, battle menus, Silence, Reflect, Hybrid
abilities, spell combinations, learned-ability persistence, secondary classes, enemy
spellbook access, or enemy AI changes.
