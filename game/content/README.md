# Content source

Content is authored as UTF-8 JSON, one top-level record per file. Category folders
are scanned recursively at startup and by the command-line validator. The exact
contract and stable ID rules are defined in `CONTENT_SCHEMA.md`.

The records here are a deliberately tiny fixture pack, not production content. Together
they exercise every implemented category, cross-record references, new-game creation,
and save/load tests. They should remain small as real content is introduced separately.

The base pack currently contains 20 definitions and no concrete magic-discipline records yet.
James is class-neutral; the
`starting-class-rules/default.json` record makes Vanguard, Black Mage, and White Mage legal
new-game choices. Until a class-selection screen exists, the bootstrap selects the first
stable ID in the resolved pool solely so the nonvisual startup demonstration can run.
The additional dialogue record supplies the two placeholder lines used by the test-room guide.
The fixed slime encounter uses canonical enemy formation anchors, and enemy records may
declare rectangular `formationFootprint` dimensions. The checked-in green slime deliberately
omits that optional member to prove old/base records still receive the safe `1 × 1` default.
Existing abilities also omit `abilityKindId`, which proves they remain direct Skills through
the compatible `ability-kind.skill` default. Their target, ruleset, and numeric parameters use
the closed contracts documented in `ABILITY_AUTHORING_GUIDE.md`; arbitrary behavior strings no
longer pass validation. Green-slime drops live in
`loot-tables/green-slime.json`; the enemy references that reusable definition rather than
embedding reward data beside its combat statistics.

Before adding content, use:

- `ABILITY_AUTHORING_GUIDE.md` for a direct Skill or general ability record;
- `MAGIC_AUTHORING_GUIDE.md` for disciplines, spells, and the two-part class unlock;
- `ABILITY_RULESET_DEVELOPER_GUIDE.md` only when a genuinely new behavior needs C#.
- `LOOT_TABLE_AUTHORING_GUIDE.md` for enemy drop tables and enemy/table references.

Run validation without opening Godot:

```sh
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

Community data records belong in packages outside this base folder. See `MODDING.md` and the
`examples/mods` fixture; mods are combined with this pack only after both contracts validate.
