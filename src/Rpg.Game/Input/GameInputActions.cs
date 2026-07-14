using Godot;

namespace RpgGame.Input;

/// <summary>
/// Stable logical actions understood by the game's presentation layer.
/// </summary>
/// <remarks>
/// Gameplay asks whether an action occurred; it does not ask whether W, Escape, or another
/// concrete key was pressed. These IDs also identify entries in the user's controls file, so
/// they must not be casually renamed after release.
/// </remarks>
public static class GameInputActions
{
    public const string MoveUp = "game.move-up";
    public const string MoveRight = "game.move-right";
    public const string MoveDown = "game.move-down";
    public const string MoveLeft = "game.move-left";
    public const string Interact = "game.interact";
    public const string Menu = "game.menu";

    /// <summary>
    /// The small current action catalog, including two keyboard choices for movement/menu
    /// and three for interaction. Add a new action here only when gameplay actually uses it.
    /// </summary>
    public static IReadOnlyList<GameInputActionDefinition> Definitions { get; } =
    [
        new(MoveUp, "Move Up", [Key.W, Key.Up]),
        new(MoveRight, "Move Right", [Key.D, Key.Right]),
        new(MoveDown, "Move Down", [Key.S, Key.Down]),
        new(MoveLeft, "Move Left", [Key.A, Key.Left]),
        new(Interact, "Interact / Confirm", [Key.E, Key.Space, Key.Enter, Key.KpEnter]),
        new(Menu, "Menu / Cancel", [Key.Escape, Key.Tab]),
    ];
}

/// <summary>One remappable logical action and its keyboard defaults.</summary>
public sealed record GameInputActionDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<Key> DefaultKeys);
