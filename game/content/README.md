# Content source

Content is authored as UTF-8 JSON, one top-level record per file. Category folders
are scanned recursively at startup and by the command-line validator. The exact
contract and stable ID rules are defined in `CONTENT_SCHEMA.md`.

The records here are a deliberately tiny fixture pack, not production content. Together
they exercise every implemented category, cross-record references, new-game creation,
and save/load tests. They should remain small as real content is introduced separately.

The base pack currently contains 19 definitions. James is class-neutral; the
`starting-class-rules/default.json` record makes Vanguard, Black Mage, and White Mage legal
new-game choices. Until a class-selection screen exists, the bootstrap selects the first
stable ID in the resolved pool solely so the nonvisual startup demonstration can run.
The additional dialogue record supplies the two placeholder lines used by the test-room guide.
The fixed slime encounter uses canonical enemy formation anchors, and enemy records may
declare rectangular `formationFootprint` dimensions. The checked-in green slime deliberately
omits that optional member to prove old/base records still receive the safe `1 × 1` default.

Run validation without opening Godot:

```sh
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

Community data records belong in packages outside this base folder. See `MODDING.md` and the
`examples/mods` fixture; mods are combined with this pack only after both contracts validate.
