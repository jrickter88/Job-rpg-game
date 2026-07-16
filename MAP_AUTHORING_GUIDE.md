# Map Authoring Guide

Map authoring is consolidated in [`CONTENT_AUTHORING_GUIDE.md`](CONTENT_AUTHORING_GUIDE.md).
Use that guide for ASCII rows, passability symbols, spawns, encounter markers, transitions, and
the validation workflow. A map is now playable through the one generic exploration scene: add
only its validated JSON definition, localization key, and transitions from existing map JSON.
Never add a Godot scene path to map content; passability and placeholders are derived from the
ASCII logic rows, while final art remains a later presentation concern.

The complete strict JSON contract is in [`CONTENT_SCHEMA.md`](CONTENT_SCHEMA.md).
