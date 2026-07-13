namespace RpgGame.Core.State;

/// <summary>
/// Narrow application service that owns state across scene changes.
/// </summary>
public interface IGameSession
{
    GameState Current { get; }

    event EventHandler? StateChanged;

    void ReplaceState(GameState state);
}

