namespace RpgGame.Core.State;

/// <summary>
/// In-memory owner of the active campaign snapshot across Godot scene transitions.
/// </summary>
/// <remarks>
/// This service deliberately does not know how scenes work or where saves live. Application
/// use cases replace state here, and scene controllers observe <see cref="StateChanged"/> to
/// refresh presentation. More focused mutation methods will arrive with their features.
/// </remarks>
public sealed class GameSession : IGameSession
{
    private GameState? _current;

    /// <inheritdoc />
    public bool HasActiveGame => _current is not null;

    /// <inheritdoc />
    public GameState Current => _current
        ?? throw new InvalidOperationException("No game is active. Start or load a game first.");

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public void ReplaceState(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        _current = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
