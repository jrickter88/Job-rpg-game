# Example data mod

`mod.example.starter-pack` is a deliberately tiny authoring fixture. It proves that a mod can
add a class and ability, reference base-game statistics, and validate without adding scripts,
assemblies, scenes, or gameplay code. Its starting-class rule adds Chronoguard to the
new-game pool and removes the vanilla Black Mage choice, demonstrating both supported
directions without overwriting a vanilla record.

Validate the base pack and this example together from the repository root:

```powershell
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content examples/mods
```

Successful combined validation loads 22 definitions. With this example enabled, the final
starting pool is Chronoguard, White Mage, and Vanguard.

The manifest uses data API `2`. This version requires encounter mods to use canonical enemy
anchors such as `formation.enemy.r1.c0`; API `1` abstract formation names are no longer
accepted. Milestone 2.8 remains additive: enemy records may omit `formationFootprint` and
still load with the deterministic `1 × 1` default, so the data API remains version `2`.
Milestone 3.05 is also additive: abilities that omit `abilityKindId` remain direct Skills,
and mods only need `magic-disciplines/` records when they actually author Magic abilities.
Ability target/ruleset strings are code-owned contracts rather than extension hooks. The
example Temporal Guard uses the supported `target.self` + `rules.defense.guard` contract and
its required `damage-reduction` parameter; see `ABILITY_AUTHORING_GUIDE.md` before adding more.

To try discovery in a development build, copy the entire `mod.example.starter-pack` folder into
the Godot project's `user://mods` folder. The startup output will report one enabled data mod.
