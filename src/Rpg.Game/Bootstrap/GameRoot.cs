using Godot;
using RpgGame.Adapters.Content;
using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using RpgGame.Core.Equipment;
using RpgGame.Core.Inventory;
using RpgGame.Core.Loot;
using RpgGame.Core.Mods;
using RpgGame.Core.Persistence;
using RpgGame.Core.Rewards;
using RpgGame.Core.State;
using RpgGame.Display;
using RpgGame.Encounters;
using RpgGame.Exploration;
using RpgGame.Input;
using RpgGame.Localization;
using RpgGame.Rewards;

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
public partial class GameRoot : Node, IExplorationDevelopmentCommands
{
    private const string GameVersion = "0.5.4";
    private const string JamesId = "actor.hero.james";
    private const string IronSwordItemId = "item.equipment.iron-sword";
    private const string WoodenShieldItemId = "item.equipment.wooden-shield";
    private const string LeatherArmorItemId = "item.equipment.leather-armor";
    private const string LeatherBootsItemId = "item.equipment.leather-boots";
    private const string LeatherHelmItemId = "item.equipment.leather-helm";
    private const string PowerRingItemId = "item.equipment.power-ring";
    private const string SpiritCharmItemId = "item.equipment.spirit-charm";
    private const string ExplorationScenePath = "res://game/scenes/exploration/ExplorationMap.tscn";
    private const string BattleScenePath = "res://game/scenes/encounters/Battle.tscn";
    private const string RewardSummaryScenePath =
        "res://game/scenes/rewards/RewardSummary.tscn";
    private const string DevelopmentQuickSlotId = "slot_1";

    // Exactly one transient gameplay presentation is active at a time. Campaign truth is
    // still held only by Session, so replacing this Node never replaces or copies GameState.
    private Node? _activeGameplayScene;
    private IRandomSource? _randomSource;
    private BattleCompletionService? _battleCompletion;

    /// <summary>Validated immutable content available after <see cref="_Ready"/>.</summary>
    public IContentCatalog Content { get; private set; } = null!;

    /// <summary>Scene-independent owner of the currently active campaign.</summary>
    public IGameSession Session { get; private set; } = null!;

    /// <summary>Save/load application service using Godot's writable user directory.</summary>
    public SaveCoordinator Saves { get; private set; } = null!;

    /// <summary>Application-lifetime player preferences applied through Godot InputMap.</summary>
    public InputBindingService InputBindings { get; private set; } = null!;

    public DisplaySettingsService DisplaySettings { get; private set; } = null!;

    /// <summary>Application-lifetime text lookup for the base presentation language.</summary>
    public LocalizedTextCatalog Text { get; private set; } = null!;

    /// <summary>
    /// Validated loose-folder data mods active for this process, in dependency order. The
    /// collection is diagnostic/application metadata; scenes should still request definitions
    /// from <see cref="Content"/> instead of reaching into mod folders.
    /// </summary>
    public IReadOnlyList<ModReference> EnabledMods { get; private set; } = [];

    /// <inheritdoc />
    public string QuickSlotId => DevelopmentQuickSlotId;

    /// <summary>
    /// Godot calls this after the node enters the scene tree. Startup is deliberately
    /// synchronous while the fixture pack is small, making failures deterministic and visible.
    /// </summary>
    public override void _Ready()
    {
        try
        {
            InitializeApplicationServices();
            ShowExploration();
            GD.Print(
                $"Milestone 4.96 ready: loaded {Content.Count} definitions "
                + $"with {EnabledMods.Count} data mod(s); "
                + $"new game {Session.Current.SaveId} starts at {Session.Current.Location.MapId}.");
        }
        catch (Exception exception)
        {
            GD.PushError($"Application startup failed:{System.Environment.NewLine}{exception}");
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
    public void StartNewGame(string? startingClassId = null)
    {
        EnsureInitialized(Content, nameof(Content));
        EnsureInitialized(Session, nameof(Session));

        // Until the title screen supplies a player choice, select the first stable ID from
        // the resolved pool. This is deterministic and remains valid when a mod removes the
        // vanilla Knight choice. It is a bootstrap fallback, not actor configuration.
        string selectedClassId = startingClassId
            ?? StartingClassPool.Resolve(Content).FirstOrDefault()
            ?? throw new InvalidOperationException(
                "Content does not provide any selectable starting classes.");

        GameState initialState = new NewGameFactory(Content).Create(
            DefaultGameSetup.CreateRequest(selectedClassId));
        Session.ReplaceState(initialState);

        // Starter equipment remains ordinary inventory stacks; only the weapon begins equipped.
        var inventory = new InventoryService(Content, Session);
        inventory.AddItems(
        [
            new InventoryAddition(IronSwordItemId, 1),
            new InventoryAddition(WoodenShieldItemId, 1),
            new InventoryAddition(LeatherArmorItemId, 1),
            new InventoryAddition(LeatherBootsItemId, 1),
            new InventoryAddition(LeatherHelmItemId, 1),
            new InventoryAddition(PowerRingItemId, 1),
            new InventoryAddition(SpiritCharmItemId, 1),
        ]);
        new EquipmentService(Content, Session).EquipItem(
            JamesId,
            IronSwordItemId,
            EquipmentSlotIds.MainHandWeapon);
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

        bool mapChanged = !string.Equals(
            Session.Current.Location.MapId,
            loadedState.Location.MapId,
            StringComparison.Ordinal);
        if (mapChanged)
        {
            // StateChanged is synchronous. Detach the current map presenter before replacing
            // state so it cannot attempt to render a loaded location from another map.
            RemoveActiveGameplayScene();
        }

        Session.ReplaceState(loadedState);
        if (mapChanged)
        {
            ShowExploration("Loaded saved map and location.");
        }

        return true;
    }

    /// <inheritdoc />
    public Task SaveQuickSlotAsync(CancellationToken cancellationToken = default) =>
        SaveCurrentGameAsync(DevelopmentQuickSlotId, cancellationToken);

    /// <inheritdoc />
    public Task<bool> LoadQuickSlotAsync(CancellationToken cancellationToken = default) =>
        LoadGameAsync(DevelopmentQuickSlotId, cancellationToken);

    private void InitializeApplicationServices()
    {
        // Mods live in user:// so players can install data without modifying the exported
        // game's read-only res:// pack. GlobalizePath converts that virtual Godot location
        // into the operating-system path consumed by the plain .NET discovery service.
        string modsDirectory = ProjectSettings.GlobalizePath("user://mods");
        ModDiscoveryResult modResult = new DirectoryModDiscovery().Discover(modsDirectory);
        if (!modResult.IsSuccess)
        {
            string details = string.Join(
                System.Environment.NewLine,
                modResult.Problems.Select(problem => problem.ToString()));
            throw new InvalidDataException(
                $"Data-mod validation failed with {modResult.Problems.Count} problem(s):"
                + System.Environment.NewLine
                + details);
        }

        // Base content is always first. Mod discovery has already produced a deterministic
        // topological order in which every dependency precedes the mod that needs it.
        var sources = new List<IContentSource>
        {
            new GodotContentSource(ContentSourceIds.Base, "res://game/content"),
        };
        sources.AddRange(modResult.Mods.Select(mod =>
            new DirectoryContentSource(
                mod.Manifest.Id,
                mod.ContentDirectory,
                mod.Manifest.Dependencies)));

        LocalizationLoadResult localizationResult = new LocalizationBundleLoader().Load(
            "en",
            new GodotLocalizationSource("res://game/localization/en").ReadAll());
        if (!localizationResult.IsSuccess)
        {
            string details = string.Join(
                System.Environment.NewLine,
                localizationResult.Problems.Select(problem => problem.ToString()));
            throw new InvalidDataException(
                $"Localization validation failed with {localizationResult.Problems.Count} problem(s):"
                + System.Environment.NewLine
                + details);
        }

        LocalizationCatalog baseLocalization = localizationResult.Catalog!;
        var loader = new JsonContentLoader();
        ContentLoadResult contentResult = loader.Load(sources, baseLocalization);

        if (!contentResult.IsSuccess)
        {
            string details = string.Join(
                System.Environment.NewLine,
                contentResult.Problems.Select(problem => problem.ToString()));
            throw new InvalidDataException(
                $"Content validation failed with {contentResult.Problems.Count} problem(s):"
                + System.Environment.NewLine
                + details);
        }

        Content = contentResult.Catalog!;
        EnabledMods = modResult.Mods.Select(mod => mod.ToReference()).ToArray();

        Session = new GameSession();

        // Reward services live for the application lifetime but retain only narrow core
        // dependencies. The random adapter is created once and injected at confirmed victory.
        var inventory = new InventoryService(Content, Session);
        var victoryRewards = new VictoryRewardService(new LootResolver(Content), inventory);
        _randomSource = new SystemRandomSource();
        _battleCompletion = new BattleCompletionService(victoryRewards, Session);

        string saveDirectory = ProjectSettings.GlobalizePath("user://saves");
        var serializer = new SaveJsonSerializer();
        var store = new JsonFileSaveStore(saveDirectory, serializer);
        Saves = new SaveCoordinator(store, GameVersion, EnabledMods);

        // Controls are user preferences shared by every campaign. They therefore live beside,
        // not inside, save slots. A malformed file falls back to defaults without blocking play.
        string controlsPath = ProjectSettings.GlobalizePath("user://settings/controls.json");
        InputBindings = new InputBindingService(controlsPath);
        InputBindings.Initialize();
        DisplaySettings = new DisplaySettingsService();
        Text = new LocalizedTextCatalog(baseLocalization);
        if (InputBindings.LoadWarning is not null)
        {
            GD.PushWarning(InputBindings.LoadWarning);
        }

        // Creating the initial session last guarantees every dependency used by the public
        // new-game/save/load methods is ready before StateChanged can notify listeners.
        StartNewGame();
    }

    /// <summary>
    /// Presents a newly instantiated test room reconstructed entirely from GameState.
    /// </summary>
    /// <remarks>
    /// This, <see cref="ShowBattle(EncounterLaunchRequest)"/>, and the reward summary are
    /// direct feature-specific handoffs, not a route registry for arbitrary scene strings.
    /// </remarks>
    private void ShowExploration(string? developmentStatus = null)
    {
        PackedScene packedScene = ResourceLoader.Load<PackedScene>(ExplorationScenePath)
            ?? throw new InvalidOperationException(
                $"Could not load exploration scene '{ExplorationScenePath}'.");
        var scene = packedScene.Instantiate<ExplorationSceneController>();

        RemoveActiveGameplayScene();
        AddChild(scene);
        try
        {
            scene.Initialize(Content, Session, this, InputBindings, DisplaySettings, Text);
            scene.ReloadRequested += OnExplorationReloadRequested;
            scene.EncounterRequested += OnEncounterRequested;
            scene.TransitionRequested += OnTransitionRequested;
            if (developmentStatus is not null)
            {
                scene.ShowDevelopmentStatus(developmentStatus);
            }

            _activeGameplayScene = scene;
        }
        catch
        {
            RemoveChild(scene);
            scene.QueueFree();
            throw;
        }
    }

    /// <summary>
    /// Resolves the fixed encounter and constructs its transient battle before replacing
    /// exploration.
    /// </summary>
    private void ShowBattle(EncounterLaunchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Exploration already suppresses the cleared marker, but the composition boundary
        // repeats this persistent check so a stale or duplicated launch request cannot reopen a
        // completed encounter.
        if (TestRoomEncounterProgress.IsCleared(Session, request.EncounterId))
        {
            ShowExploration("That encounter has already been cleared.");
            return;
        }

        // All lookups and snapshot construction occur before exploration is removed. A bad ID
        // or malformed battle therefore leaves the current scene intact and produces the core's
        // actionable error rather than a blank presentation.
        EncounterDefinition encounter = Content.GetRequired<EncounterDefinition>(
            request.EncounterId);
        IReadOnlyList<FormationPlacement> enemyPlacements =
            new EncounterFormationBuilder(Content).Build(encounter);
        IReadOnlyList<FormationPlacement> partyPlacements = PartyFormationBuilder.Build(
            Session.Current.ActivePartyActorIds);
        CombatSnapshot initialSnapshot = new CombatSnapshotFactory(Content).Create(
            Session.Current,
            encounter,
            enemyPlacements,
            partyPlacements);
        var actionResolver = new CombatResolver(Content, RequireRandomSource());
        var timelineResolver = new CombatTimelineResolver(actionResolver, Content);
        var enemyPlanner = new EnemyCommandPlanner(Content);
        var commandAvailabilityResolver = new BattleCommandAvailabilityResolver(Content);

        PackedScene packedScene = ResourceLoader.Load<PackedScene>(BattleScenePath)
            ?? throw new InvalidOperationException(
                $"Could not load battle scene '{BattleScenePath}'.");
        var scene = packedScene.Instantiate<BattleController>();

        RemoveActiveGameplayScene();
        AddChild(scene);
        try
        {
            scene.Initialize(
                encounter,
                Content,
                commandAvailabilityResolver,
                initialSnapshot,
                timelineResolver,
                enemyPlanner,
                InputBindings);
            scene.CompletionRequested += OnBattleCompletionRequested;
            _activeGameplayScene = scene;
        }
        catch
        {
            RemoveChild(scene);
            scene.QueueFree();
            throw;
        }
    }

    /// <summary>Presents already-applied item totals before exploration is reconstructed.</summary>
    private void ShowRewardSummary(VictoryRewardResult rewards)
    {
        ArgumentNullException.ThrowIfNull(rewards);
        PackedScene packedScene = ResourceLoader.Load<PackedScene>(RewardSummaryScenePath)
            ?? throw new InvalidOperationException(
                $"Could not load reward summary scene '{RewardSummaryScenePath}'.");
        var scene = packedScene.Instantiate<RewardSummaryController>();

        RemoveActiveGameplayScene();
        AddChild(scene);
        try
        {
            scene.Initialize(rewards.ItemSummaries, InputBindings);
            scene.ContinueRequested += OnRewardSummaryContinueRequested;
            _activeGameplayScene = scene;
        }
        catch
        {
            RemoveChild(scene);
            scene.QueueFree();
            throw;
        }
    }

    /// <summary>Disconnects and frees whichever known gameplay presentation is active.</summary>
    private void RemoveActiveGameplayScene()
    {
        if (_activeGameplayScene is null)
        {
            return;
        }

        // Explicit type handling keeps ownership obvious. There is no reflection, scene
        // registry, stack, or generic navigation protocol hidden in this helper.
        if (_activeGameplayScene is ExplorationSceneController exploration)
        {
            exploration.ReloadRequested -= OnExplorationReloadRequested;
            exploration.EncounterRequested -= OnEncounterRequested;
            exploration.TransitionRequested -= OnTransitionRequested;
        }
        else if (_activeGameplayScene is BattleController battle)
        {
            battle.CompletionRequested -= OnBattleCompletionRequested;
        }
        else if (_activeGameplayScene is RewardSummaryController rewardSummary)
        {
            rewardSummary.ContinueRequested -= OnRewardSummaryContinueRequested;
        }

        RemoveChild(_activeGameplayScene);
        _activeGameplayScene.QueueFree();
        _activeGameplayScene = null;
    }

    private void OnEncounterRequested(
        object? sender,
        EncounterLaunchRequestedEventArgs eventArgs) =>
        ShowBattle(eventArgs.Request);

    private void OnTransitionRequested(
        object? sender,
        MapTransitionRequestedEventArgs eventArgs)
    {
        (MapDefinition? sourceMap, MapTransitionDefinition? transition) = Content
            .GetAll<MapDefinition>()
            .SelectMany(map => map.Transitions.Select(candidate => (Map: map, Transition: candidate)))
            .Where(candidate => string.Equals(
                candidate.Transition.Id,
                eventArgs.Request.TransitionId,
                StringComparison.Ordinal))
            .Select(candidate => ((MapDefinition?)candidate.Map, (MapTransitionDefinition?)candidate.Transition))
            .SingleOrDefault();
        if (sourceMap is null || transition is null)
        {
            throw new KeyNotFoundException(
                $"Transition '{eventArgs.Request.TransitionId}' was not found in any map.");
        }
        if (!string.Equals(sourceMap.Id, Session.Current.Location.MapId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Transition '{transition.Id}' belongs to map '{sourceMap.Id}', "
                + $"but the session is on '{Session.Current.Location.MapId}'.");
        }

        MapDefinition destination = Content.GetRequired<MapDefinition>(transition.DestinationMapId);
        MapSpawnDefinition spawn = destination.Spawns.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, transition.DestinationSpawnId, StringComparison.Ordinal))
            ?? throw new InvalidDataException(
                $"Map '{destination.Id}' has no destination spawn '{transition.DestinationSpawnId}'.");

        // Remove the old presenter before publishing the new map location. StateChanged is
        // synchronous; leaving the old room subscribed would make it try to render the
        // destination map and throw before the replacement scene can initialize.
        RemoveActiveGameplayScene();
        Session.UpdateLocation(Session.Current.Location with
        {
            MapId = destination.Id,
            X = spawn.X,
            Y = spawn.Y,
            Facing = spawn.Facing,
        });
        ShowExploration($"Entered {destination.Id}.");
    }

    private void OnBattleCompletionRequested(
        object? sender,
        BattleCompletionRequestedEventArgs eventArgs)
    {
        BattleCompletionRequest request = eventArgs.Request;
        string clearanceFlagId = TestRoomEncounterProgress.GetClearanceFlagId(
            request.EncounterId);

        // This composition-level check is intentionally repeated inside the headless policy.
        // It protects against stale presentation requests before any reward dependency runs.
        if (request.Outcome == BattleOutcome.PartyVictory
            && TestRoomEncounterProgress.IsCleared(Session, request.EncounterId))
        {
            ShowExploration("That encounter has already granted its victory rewards.");
            return;
        }

        BattleCompletionResult completion = RequireBattleCompletion().Complete(
            request,
            clearanceFlagId,
            RequireRandomSource());
        switch (completion.Disposition)
        {
            case BattleCompletionDisposition.PartyDefeat:
                ShowExploration(
                    $"Defeat: {request.EncounterId} remains uncleared.");
                break;

            case BattleCompletionDisposition.VictoryRewardsApplied:
                ShowRewardSummary(completion.Rewards
                    ?? throw new InvalidDataException(
                        "Applied victory completion did not return reward presentation data."));
                break;

            case BattleCompletionDisposition.AlreadyCleared:
                ShowExploration("That encounter has already granted its victory rewards.");
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported battle completion disposition '{completion.Disposition}'.");
        }
    }

    private void OnRewardSummaryContinueRequested(
        object? sender,
        RewardSummaryContinueRequestedEventArgs eventArgs) =>
        ShowExploration("Victory rewards applied; encounter cleared.");

    private void OnExplorationReloadRequested(object? sender, EventArgs eventArgs) =>
        ShowExploration("Room reconstructed from in-memory GameState.");

    private static void EnsureInitialized(object? service, string serviceName)
    {
        if (service is null)
        {
            throw new InvalidOperationException(
                $"{serviceName} is unavailable until GameRoot._Ready has initialized services.");
        }
    }

    private BattleCompletionService RequireBattleCompletion() => _battleCompletion
        ?? throw new InvalidOperationException(
            "Battle completion is unavailable until GameRoot is initialized.");

    private IRandomSource RequireRandomSource() => _randomSource
        ?? throw new InvalidOperationException(
            "Random source is unavailable until GameRoot is initialized.");
}
