# Adding a new ability behavior safely

## When this guide applies

Use this guide only when a new ability cannot be expressed by tuning an existing code-owned
ruleset. Creating a new spell or Skill usually needs JSON only. Creating healing, poison,
revival, stealing, an area target, or another genuinely new rule family needs trusted core
code and tests.

The project intentionally does not load scripts, C# type names, assemblies, or expressions
from ability JSON. That boundary keeps community data deterministic, testable, and safer.

## Current ownership

| Concern | Owner |
|---|---|
| Ability name, kind, cost declaration, ruleset choice, and numeric tuning | `AbilityDefinition` JSON |
| Supported stable target/ruleset/parameter IDs | `AbilityContentIds.cs` |
| Content contract and actionable diagnostics | `ContentValidator` |
| Learned Skill/Magic availability | `AbilityAvailabilityResolver` |
| Future state changes and domain events | Plain .NET combat resolver |
| Animation, sound, menus, and target highlights | Godot presentation under `src/Rpg.Game` |

Do not make a Godot node calculate damage or let a ruleset locate scene objects.

## Implementation sequence

1. Write down the exact gameplay rule and its legal targets before adding an ID.
2. Reuse an existing target mode when possible. Add a constant to `AbilityTargetingIds` only
   when target legality actually differs.
3. Add one permanent constant to `AbilityRulesetIds` and only the numeric keys the behavior
   genuinely needs to `AbilityNumericParameterIds`.
4. Extend `AbilityDefinitionContractValidator` with:
   - the compatible targeting IDs;
   - required numeric keys;
   - rejection of unknown keys;
   - inclusive/exclusive numeric ranges;
   - stable, actionable diagnostic codes.
5. Add content tests for a valid definition, every invalid boundary, missing parameters,
   extra parameters, and target incompatibility.
6. When command resolution exists, implement the effect in `src/Rpg.Core/Combat`. Receive an
   explicit `CombatCommand`, return a new snapshot plus typed domain events, and inject any
   randomness through `IRandomSource`.
7. Add deterministic resolver tests before connecting Godot presentation.
8. Let `src/Rpg.Game` translate domain events into visuals. Never put node paths, animations,
   sounds, or resource paths into the core ruleset.
9. Update `CONTENT_SCHEMA.md`, `ABILITY_AUTHORING_GUIDE.md`, and mod documentation in the same
   change. Decide whether the stricter public content contract requires a mod data-API change.

## Parameter design rules

- Prefer one clearly named value over several overlapping knobs.
- Use lowercase kebab-case keys.
- Give every required key a legal range and test its boundaries.
- Reject extra keys; silently ignored typos are expensive to find in hundreds of files.
- Do not put content IDs, Boolean switches, formulas, method names, or mini-expressions into
  `numericParameters`.
- If an ability family needs structured data, add a small typed DTO only after the concrete
  behavior proves that shape.

## Example review questions

Before accepting `rules.healing.restore-hp`, answer:

- Is healing different enough from an existing rule to require code?
- Does it target self, one ally, all allies, or defeated allies?
- Is `power` an authored integer/decimal, and what range is legal?
- Which resolved statistic influences the formula?
- Does it spend a mutable resource, and where is current resource state owned?
- What typed event lets Godot animate the result without calculating it again?
- How are zero healing, maximum-HP clamping, defeat state, and invalid targets tested?

If those answers are not yet known, keep the ruleset deferred rather than creating a placeholder
ID that future code must support forever.

## What not to build

- A generic effect graph or universal component system.
- Reflection that maps a JSON string to a C# class or method.
- Embedded C#, Lua, expressions, or mod assemblies.
- Presentation callbacks in core combat code.
- A parameter dictionary whose unknown keys are ignored.
- Save data containing ruleset implementations or derived content definitions.

The goal is a small library of proven JRPG behaviors, not a general-purpose game engine.
