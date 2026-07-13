namespace RpgGame.Core.State;

/// <summary>
/// Narrow application service that owns the active state across scene changes.
/// </summary>
/// <remarks>
/// The implementation lives for the application's lifetime, but consumers receive this
/// small interface rather than a global bag of managers. Feature use cases will perform
/// validated state transitions; the Milestone 1 contract supports new-game and restored-state
/// replacement without introducing gameplay behavior.
/// </remarks>
public interface IGameSession
{
    /// <summary>Whether a new or loaded campaign is currently active.</summary>
    bool HasActiveGame { get; }

    /// <summary>Current authoritative campaign snapshot.</summary>
    GameState Current { get; }

    /// <summary>
    /// Raised after authoritative session state changes. Scene/UI subscribers should
    /// re-read <see cref="Current"/> rather than treating an individual Node as truth.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Replaces the whole campaign snapshot when starting or restoring a game.
    /// Normal feature mutations will use narrower use cases rather than this method.
    /// </summary>
    void ReplaceState(GameState state);
}
