using Godot;
using RpgGame.Adapters.Content;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Loading;
using RpgGame.Core.Persistence;
using RpgGame.Core.State;

namespace RpgGame.Bootstrap;

/// <summary>
/// Godot composition root for scene-facing adapters and narrowly scoped application services.
/// </summary>
/// <remarks>
/// Godot creates this Node from <c>game/scenes/bootstrap/GameRoot.tscn</c>, which is the
/// project's configured main scene. It constructs the validated content catalog, campaign
/// session, and save coordinator. Future scene controllers should receive only the narrow
/// service interfaces they actually use.
///
/// Gameplay behavior does not belong in this node. Keeping the root intentionally boring
/// prevents it from becoming an unrestricted global GameManager over time.
///
/// Godot requires scripts deriving from GodotObject to be <c>partial</c>; its source
/// generators supply the engine-facing portion of the class during compilation.
/// </remarks>
public partial class GameRoot : Node
{
    private const string GameVersion = "0.1.0-milestone1";

    /// <summary>Validated immutable content available after <see cref="_Ready"/>.</summary>
    public IContentCatalog Content { get; private set; } = null!;

    /// <summary>Scene-independent owner of the currently active campaign.</summary>
    public IGameSession Session { get; private set; } = null!;

    /// <summary>Save/load application service using Godot's writable user directory.</summary>
    public SaveCoordinator Saves { get; private set; } = null!;

    /// <summary>
    /// Godot calls this after the node enters the scene tree. Startup is deliberately
    /// synchronous while the fixture pack is small, making failures deterministic and visible.
    /// </summary>
    public override void _Ready()
    {
        try
        {
            InitializeApplicationServices();
            GD.Print(
                $"Milestone 1 ready: loaded {Content.Count} definitions; "
                + $"new game {Session.Current.SaveId} starts at {Session.Current.Location.MapId}.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Application startup failed:{Environment.NewLine}{exception}");
            GetTree().Quit(1);
        }
    }

    /// <summary>
    /// Replaces the active campaign with a newly constructed default game.
    /// </summary>
    /// <remarks>
    /// A future title-screen controller can call this method after the player confirms
    /// "New Game." Keeping creation here for now demonstrates the complete use case without
    /// introducing a title screen or global service locator during this milestone.
    /// </remarks>
    public void StartNewGame()
    {
        EnsureInitialized(Content, nameof(Content));
        EnsureInitialized(Session, nameof(Session));

        GameState initialState = new NewGameFactory(Content).Create(
            DefaultGameSetup.CreateRequest());
        Session.ReplaceState(initialState);
    }

    /// <summary>
    /// Saves the authoritative campaign snapshot to a logical slot such as
    /// <c>slot_1</c>. The slot is validated before it becomes a filename.
    /// </summary>
    public Task SaveCurrentGameAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized(Session, nameof(Session));
        EnsureInitialized(Saves, nameof(Saves));
        return Saves.SaveAsync(slotId, Session.Current, cancellationToken);
    }

    /// <summary>
    /// Loads a slot into the persistent session. Returns false when the slot has never been
    /// saved; malformed or incompatible saves intentionally throw a visible error.
    /// </summary>
    public async Task<bool> LoadGameAsync(
        string slotId,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized(Session, nameof(Session));
        EnsureInitialized(Saves, nameof(Saves));

        GameState? loadedState = await Saves.LoadAsync(slotId, cancellationToken);
        if (loadedState is null)
        {
            return false;
        }

        Session.ReplaceState(loadedState);
        return true;
    }

    private void InitializeApplicationServices()
    {
        var loader = new JsonContentLoader();
        ContentLoadResult contentResult = loader.Load(
            new GodotContentSource("res://game/content"));

        if (!contentResult.IsSuccess)
        {
            string details = string.Join(
                Environment.NewLine,
                contentResult.Problems.Select(problem => problem.ToString()));
            throw new InvalidDataException(
                $"Content validation failed with {contentResult.Problems.Count} problem(s):"
                + Environment.NewLine
                + details);
        }

        Content = contentResult.Catalog!;

        Session = new GameSession();

        string saveDirectory = ProjectSettings.GlobalizePath("user://saves");
        var serializer = new SaveJsonSerializer();
        var store = new JsonFileSaveStore(saveDirectory, serializer);
        Saves = new SaveCoordinator(store, GameVersion);

        // Creating the initial session last guarantees every dependency used by the public
        // Milestone 1 methods is ready before StateChanged can notify future listeners.
        StartNewGame();
    }

    private static void EnsureInitialized(object? service, string serviceName)
    {
        if (service is null)
        {
            throw new InvalidOperationException(
                $"{serviceName} is unavailable until GameRoot._Ready has initialized services.");
        }
    }
}
