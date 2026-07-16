# Localization Bundles

English text is split into scoped files beneath the en/ folder. The runtime recursively loads
every JSON file in that locale folder and merges the texts dictionaries. Text keys are stable
identities; file paths are organization only.

Recommended layout:

game/localization/en/
  common.json
  maps/prologue.json
  items/equipment.json
  items/consumables.json
  dialogue/prologue/test-room-guide.json

Each file declares schemaVersion, locale, and a texts dictionary. Duplicate keys across files
are errors. Missing keys are rejected by base-content validation and display as
??missing.key?? at runtime when a lookup still slips through.

Edit the scoped file that owns the text. Item descriptions belong in items/equipment.json or
items/consumables.json; map names belong in maps/prologue.json; dialogue prose belongs in the
matching dialogue/ file. Dialogue content records contain ordered localization keys, not
player-facing prose.
