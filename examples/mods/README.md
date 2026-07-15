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

Successful combined validation loads 23 definitions. With this example enabled, the final
starting pool is Chronoguard, White Mage, and Vanguard.

The example version is `2.0.0` because moving from data API 2 to 3 is a breaking authoring
contract change. The manifest uses data API `3`. API `2` introduced canonical encounter
anchors such as
`formation.enemy.r1.c0`; API `1` abstract formation names are no longer accepted. API `3`
requires enemy schema 2 and moves embedded enemy drops into namespaced `loot-tables/`
records referenced by `lootTableId`. Enemy records may still omit `formationFootprint` and
receive the deterministic `1 × 1` default.
Milestone 3.05 is also additive: abilities that omit `abilityKindId` remain direct Skills,
and mods only need `magic-disciplines/` records when they actually author Magic abilities.
Ability target/ruleset strings are code-owned contracts rather than extension hooks. The
example Temporal Guard uses the supported `target.self` + `rules.defense.guard` contract and
its required `damage-reduction` parameter; see `ABILITY_AUTHORING_GUIDE.md` before adding more.
See `LOOT_TABLE_AUTHORING_GUIDE.md` before adding a mod enemy with item drops.

To try discovery in a development build, copy the entire `mod.example.starter-pack` folder into
the Godot project's `user://mods` folder. The startup output will report one enabled data mod.
