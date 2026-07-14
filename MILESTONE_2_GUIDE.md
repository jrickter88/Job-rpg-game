# Milestone 2 guide

Milestone 2 turns the nonvisual foundation into the first controllable game screen. James can
walk one tile per key press, collide with walls, face and speak to one guide, and rebuild the
room without losing his position or the fact that the conversation occurred.

## Controls

| Input | Result |
|---|---|
| Current Move bindings | Face a direction and attempt one tile of movement |
| Current Interact / Confirm bindings | Interact with the tile James faces; advance dialogue |
| Current Menu / Cancel bindings | Close dialogue or open/close the controls panel |
| Current Interact or Menu binding in the placeholder | Return to a reconstructed test room |
| R | Free and reconstruct the room from the current in-memory `GameState` |
| K | Quick-save the authoritative `GameState` to `slot_1` |
| L | Load `slot_1` into the authoritative session, if the file exists |

The default gameplay bindings remain WASD/arrows, E/Space/Enter/Numpad Enter, and Escape/Tab. Milestone 2.2
lets the player remap them from the controls panel and persists the choices separately from
campaign saves. See `CONTROLS_GUIDE.md` for the ownership, file format, recovery path, and
future action workflow.

From the initial tile `(4, 4)`, press Right twice to reach `(6, 4)`, press Right once more to
face the guide without entering the occupied tile, then press E. The guide changes from orange
to green after `flag.test-room.npc-spoken-to` becomes true. Press R to prove that this visual
change and James's location are reconstructed rather than remembered by the old Nodes. The
green status line confirms that reconstruction happened even though the restored room should
otherwise look identical.

Milestone 2.1 adds temporary developer controls for manually proving the existing save
pipeline. Press K, move James or speak to the guide, and then press L. Loading restores the
position, facing, and flag values captured when K was pressed. These shortcuts always use
`slot_1`; they are not a player-facing save menu and will be replaced when menu work is in
scope. Pressing L before K reports that no save exists instead of changing the campaign.

## Ownership: what lives where

| Concern | Owner | Reason |
|---|---|---|
| Map ID, James's tile/facing, event flag | `GameState` through `IGameSession` | Must survive scenes and save/load |
| Content ID and two ordered dialogue lines | `DialogueDefinition` in `IContentCatalog` | Shared immutable authored data |
| Logical input actions, pixel positions, wall/NPC occupancy | Godot exploration scene | Engine and map-presentation concerns |
| Concrete keyboard bindings | `InputBindingService` and user settings | Shared player preference, not campaign progress |
| Current dialogue line and panel visibility | `DialoguePanel` | Temporary UI state; closing/reloading may discard it |

`GameSession.UpdateLocation` validates the stable map ID, nonnegative coordinates, and logical
facing before publishing a replacement snapshot. `SetEventFlag` clones the flag dictionary,
updates one stable `flag.*` key, and publishes. Both raise `StateChanged`; listeners re-read
`Current` instead of trusting a cached scene value.

The map decides whether a requested tile is walkable because collision is authored with the
map and currently represented by Godot-facing `Vector2I` values. Core never sees a Node,
pixel, key code, color, or wall layout. After the room accepts a move, only the resulting
logical coordinate crosses into the session.

## Scene composition

`GameRoot` remains the composition root. After content, mods, saves, and the initial campaign
exist, it loads `TestRoom.tscn`, adds the scene, and explicitly calls:

```csharp
scene.Initialize(Content, Session, this, InputBindings);
```

The controller does not search the scene tree for application services. R raises a typed C#
event to its owner; `GameRoot` frees that one child and instantiates it again. K and L use the
small `IExplorationDevelopmentCommands` boundary, whose implementation delegates to the
existing `SaveCurrentGameAsync` and `LoadGameAsync` methods. This is direct composition for
one map, not a global navigator disguised as a manager.

The exploration feature contains focused scripts with narrow jobs:

- `TestRoomView` draws the 12×9 grid and answers fixed wall/encounter queries;
- `PlayerMarkerView` draws James and his facing dot;
- `TestGuideNpc` owns the stable flag/dialogue IDs and returns a typed interaction result;
- `DialoguePanel` displays one speaker and one validated line at a time;
- `ControlsPanel` edits the application-lifetime keyboard profile.

## Dialogue boundary

`DialogueDefinition` deliberately contains only `speakerName` and `lines`. The content loader
enforces the `dialogue.*` ID prefix, strict JSON fields, a nonblank speaker, at least one line,
and nonblank line text. The record contains no branches, conditions, commands, portraits,
resource paths, or code expressions.

Literal placeholder text is intentional for this milestone. A localization pipeline should
be designed once real writing and language requirements exist rather than introducing keys
that no working translator can currently resolve.

## Save compatibility

No save-format migration was needed: `MapLocationState` and `EventFlags` already existed in
the version-1 state. Movement uses a C# `with` expression so unknown future location fields in
`JsonExtensionData` remain attached instead of being erased. The save round-trip test now
moves James and sets the NPC flag through the same session API before writing the file.

## Current limits

- Only `map.prologue.test-room` can be presented.
- The room layout is a fixed procedural tile grid, not an authored TileSet asset.
- Movement is immediate; there is no step animation or held-key repeat.
- The guide is the only interactable and uses one dialogue for every interaction.
- There is no map-to-map transition, player-facing save menu, dialogue choice, localization,
  gamepad support, combat resolution, random encounter system, inventory, quest behavior,
  audio, or art.

These are deliberate slice boundaries. Milestone 3 should add the first deterministic battle
and return its persistent outcome to this exploration state without expanding Milestone 2
into a general-purpose engine.

Milestone 2.5 now adds one visible fixed marker and a non-combat encounter placeholder. It
proves only the scene handoff and return; there is still no combat outcome, encounter-cleared
flag, random encounter system, or general navigation framework. See `MILESTONE_2_5_GUIDE.md`.

Milestone 2.75 replaces the placeholder's flat placement list with validated 4 × 4 enemy and
4 × 2 party grids. It still cannot mutate the campaign or resolve combat. See
`MILESTONE_2_75_GUIDE.md`.

## Validation

From the repository root:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content
dotnet run --project tools/content-validation/RpgGame.ContentValidation.csproj -- game/content examples/mods
dotnet build RpgGame.sln
godot --headless --editor --path . --quit
```

Then run the project in the Godot .NET editor and perform the interaction/R/K/L walkthrough
from the Controls section. The base content count is 19; base plus the example mod is 22.
