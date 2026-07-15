# Milestone 4.3 - Typed damage and percentage affinities

## Purpose

This milestone adds one shared, extensible damage vocabulary to abilities, weapons, enemy
definitions, immutable combat snapshots, and damage events. It supports strategic enemy
weaknesses and resistances without making content responsible for formulas.

The code-owned stable IDs are:

| Category | ID |
|---|---|
| Weapon/physical | `damage-type.slash` |
| Weapon/physical | `damage-type.energy` |
| Elemental | `damage-type.fire` |
| Elemental | `damage-type.ice` |
| Elemental | `damage-type.lightning` |

`DamageTypeIds` owns this closed list. Content and data mods may select these IDs but cannot
create a mechanic by inventing another string. A future type is additive: add its permanent
constant, validator support, documentation, and focused tests together.

## Ability authoring

A damage ability may declare one `damageTypeId`:

```json
{
  "rulesetId": "rules.damage.physical",
  "damageTypeId": "damage-type.fire",
  "numericParameters": { "power": 8 }
}
```

The existing `rules.damage.physical` name still describes the only executable damage formula,
which currently uses Strength and Defense. A Skill or Magic ability may select any supported
damage type while reusing that formula. A dedicated magical-stat formula remains a future
ruleset decision; do not invent `rules.damage.magic` in JSON.

New damage abilities should author the type explicitly. Omitted legacy physical-damage content
resolves as `damage-type.energy` for compatibility. Non-damage rulesets must leave
`damageTypeId` null or omitted.

## Enemy affinities

Enemies author sparse signed whole-percent modifiers:

```json
"damageTypePercentModifiers": {
  "damage-type.fire": 50,
  "damage-type.ice": -75,
  "damage-type.slash": 20,
  "damage-type.lightning": -100
}
```

- `50` means 50% additional Fire damage: weak against Fire.
- `-75` means 75% less Ice damage: strong against Ice.
- `20` means 20% additional Slash damage.
- `-100` means immunity and produces zero damage.
- An omitted type means neutral (`0`).
- Values below `-100` are invalid. Positive weaknesses intentionally have no artificial cap.

The enemy map is copied into each immutable `CombatantSnapshot` when battle state is created.
Combat never re-reads the enemy definition while resolving an action.

## Deterministic calculation

For modifier `m`, the resolver calculates:

```text
baseDamage = max(1, attacker Strength + authored power - defender Defense)

if m == -100:
    typedDamage = 0
else:
    typedDamage = max(1, floor(baseDamage * (100 + m) / 100))

appliedDamage = min(typedDamage, target CurrentHp)
```

The percentage is applied before the single final floor. Any nonimmune accepted hit retains
the existing one-damage minimum. The remaining-HP clamp still prevents negative HP. The
implementation compares against the lethal threshold before multiplication so extreme valid
decimal power and integer weakness values cannot overflow.

`DamageApplied` now reports `DamageTypeId` and `DamagePercentModifier` with the authoritative
amount and HP transition. Godot displays the type and weakness, resistance, or immunity from
that event; it does not repeat the formula.

## Weapon profiles

Weapon equipment may author a composition whose positive whole percentages total exactly 100:

```json
"weaponDamagePercentages": {
  "damage-type.slash": 70,
  "damage-type.fire": 30
}
```

Only `slot.weapon.*` equipment may declare a nonempty profile. The checked-in iron sword is
100% Slash. Mixed components use separate map entries; duplicate type keys are not meaningful
in a JSON object.

This milestone deliberately does not apply weapon profiles. The campaign has inventory stacks
but no equipment ownership, equipped slots, or active weapon selection. Applying an inventory
item as though it were equipped would violate campaign ownership. Empty legacy profiles remain
valid and reserve an Energy fallback for the later equipment-activation milestone.

## Compatibility

- `AbilityDefinition.DamageTypeId`, `EquipmentDefinition.WeaponDamagePercentages`, and
  `EnemyDefinition.DamageTypePercentModifiers` are additive fields with safe defaults.
- The content schema version and mod `gameApiVersion` remain unchanged.
- Omitted legacy ability types resolve as Energy; omitted enemy maps are neutral; omitted
  weapon maps remain valid for future Energy fallback.
- Explicit JSON null collection maps are invalid.
- No save field or migration is added. Combat snapshots and damage events are transient.

## Explicitly deferred

- persistent equipment ownership, equip/unequip use cases, and active weapon selection;
- splitting one executed weapon attack into multiple typed damage components;
- magical attack/defense statistics or a new magical-damage ruleset;
- party resistance equipment, armor affinities, status-based affinity changes, and buffs;
- critical hits, random variance, damage-over-time, absorption, reflection, and healing;
- affinity inspection UI, bestiary discovery, icons, animation, and sound;
- mod-defined damage types or scriptable damage formulas.

## Local validation

Run from the repository root:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content examples/mods
dotnet build RpgGame.sln
& "D:\Godot\Godot_v4.7-stable_mono_win64_console.exe" --headless --editor --path . --quit
```
