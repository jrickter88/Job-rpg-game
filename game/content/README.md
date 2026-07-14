# Content source

Content is authored as UTF-8 JSON, one top-level record per file. Category folders
are scanned recursively at startup and by the command-line validator. The exact
contract and stable ID rules are defined in `CONTENT_SCHEMA.md`.

The records here are a deliberately tiny fixture pack, not production content. Together
they exercise every Milestone 1 category, cross-record references, new-game creation,
and save/load tests. They should remain small as real content is introduced separately.

Run validation without opening Godot:

```sh
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
```

Community data records belong in packages outside this base folder. See `MODDING.md` and the
`examples/mods` fixture; mods are combined with this pack only after both contracts validate.
