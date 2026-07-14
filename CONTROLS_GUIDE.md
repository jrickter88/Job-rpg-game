# Controls and remapping guide

The controls foundation separates **what the player intends** from **which keyboard key they
press**. Exploration asks Godot whether `game.move-up` or `game.interact` happened. It never
checks W, an arrow, E, or Space directly. This lets the player change keys without changing
movement, dialogue, campaign state, or map code.

## Default controls

| Action | Stable action ID | Default keyboard bindings |
|---|---|---|
| Move Up | `game.move-up` | W, Up Arrow |
| Move Right | `game.move-right` | D, Right Arrow |
| Move Down | `game.move-down` | S, Down Arrow |
| Move Left | `game.move-left` | A, Left Arrow |
| Interact / Confirm | `game.interact` | E, Space, Enter, Numpad Enter |
| Menu / Cancel | `game.menu` | Escape, Tab |

R, K, and L remain fixed development shortcuts for room reconstruction, quick-save, and
quick-load. They are deliberately unavailable in the remapping screen so one key cannot
trigger both gameplay and a developer command. Those shortcuts will disappear when their
manual proof role is no longer needed.

The Milestone 2.75 battle-formation placeholder accepts either the current
**Interact / Confirm** or **Menu / Cancel** action to return to exploration. It does not add
a separate return key, so any player remapping is honored automatically. The placeholder
prints the current bindings on screen using the same formatting method as the exploration HUD.

## How a player changes controls

1. Press the current **Menu / Cancel** binding. Initially this is Escape or Tab.
2. Select one of the binding buttons with the mouse or keyboard focus.
3. Press the replacement keyboard key.
4. The change is validated, applied immediately, and written to user settings.
5. Select **Reset Defaults** to restore the table above.

A key cannot be assigned to two actions. The panel reports the conflicting action instead of
silently making one key perform two unrelated commands. While waiting for a replacement, click
the selected binding button again to cancel capture.

## Where preferences live

Godot resolves this logical path for the current operating system:

```text
user://settings/controls.json
```

Use **Project → Open User Data Folder** in Godot to find it. The file is separate from:

- `GameState`, because controls are not campaign progress;
- save slots, because every campaign should share the player's preferences;
- `game/content`, because key bindings are not authored or moddable RPG definitions;
- `project.godot`, because changing a preference must not modify the installed project.

The file has its own `schemaVersion` and stable action IDs. Newly added actions receive their
defaults when an older profile does not contain them. A malformed or unsupported file is left
in place for diagnosis while the game safely starts with defaults. Resetting or changing a
binding writes a new valid file atomically through a temporary file.

If a player ever loses access to the menu, deleting `controls.json` restores defaults at the
next launch.

## Code flow

1. `GameRoot` creates one application-lifetime `InputBindingService` using the globalized
   `user://settings/controls.json` path.
2. The service loads validated key names or defaults and registers physical-key events in
   Godot `InputMap`, keeping game controls stable across operating-system keyboard layouts.
3. `ControlsPanel` edits the service through its narrow rebind/reset methods.
4. `ExplorationSceneController` calls `InputEvent.IsActionPressed` with stable action IDs.
5. Accepted movement still updates `GameSession`; controls never become campaign state.
6. `BattlePlaceholderController` reads the existing Interact/Menu actions and raises a typed
   return request; it never compares a concrete keycode.

`GameInputActions.Definitions` is intentionally a short explicit catalog. When a real new
action arrives—such as opening an inventory menu—add its stable ID, display name, and defaults
there, then consume the logical action in the owning scene. Do not add speculative actions for
systems that do not exist.

## Current limits

- This first pass remaps keyboard keys only.
- Each action has a fixed number of keyboard slots matching its defaults.
- Key combinations, mouse buttons, controller buttons, analog deadzones, and per-device
  profiles are deferred until those input devices are part of a playable milestone.
- There is no separate settings scene yet; the test room presents the controls panel through
  the current Menu / Cancel action.

These limits keep the feature small while establishing the boundary future menus and battle
scenes can reuse.
