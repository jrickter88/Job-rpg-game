using Godot;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;
using RpgGame.Display;
using RpgGame.Encounters;
using RpgGame.Input;
using RpgGame.Localization;

namespace RpgGame.Exploration;

/// <summary>
/// Coordinates keyboard intent, the one room's collision, persistent state, and dialogue UI.
/// </summary>
/// <remarks>
/// Godot concerns stop here: keys become logical tile requests, the room accepts or rejects
/// them, and only accepted coordinates/facing are submitted to <see cref="IGameSession"/>.
/// Reconstructing this scene therefore needs no hidden Node state—only content and GameState.
/// </remarks>
public partial class ExplorationSceneController : Node2D
{
	private IExplorationMapView _room = null!;
	private Node2D _mapNode = null!;
	private PlayerMarkerView _player = null!;
	private TestGuideNpc _guide = null!;
	private DialoguePanel _dialogue = null!;
	private ControlsPanel _controlsPanel = null!;
	private DisplaySettingsPanel _displaySettingsPanel = null!;
	private GameMenuPanel _gameMenuPanel = null!;
	private CharacterEquipmentPanel _equipmentPanel = null!;
	private Label _instructions = null!;
	private Label _developmentStatus = null!;
	private IContentCatalog? _content;
	private IGameSession? _session;
	private IExplorationDevelopmentCommands? _developmentCommands;
	private InputBindingService? _inputBindings;
	private bool _developmentCommandInProgress;
	private bool _encounterTransitionRequested;

	/// <summary>Requests reconstruction by the composition root without adding navigation.</summary>
	public event EventHandler? ReloadRequested;

	/// <summary>
	/// Requests a direct handoff to one encounter after an accepted step enters its tile.
	/// The owner decides which scene to show; exploration never searches for GameRoot.
	/// </summary>
	public event EventHandler<EncounterLaunchRequestedEventArgs>? EncounterRequested;
	public event EventHandler<MapTransitionRequestedEventArgs>? TransitionRequested;

	public override void _Ready()
	{
		_mapNode = GetNode<Node2D>("Map");
        _room = _mapNode as IExplorationMapView
            ?? throw new InvalidOperationException("Exploration map node must implement IExplorationMapView.");
		_player = GetNode<PlayerMarkerView>("Player");
		_guide = GetNode<TestGuideNpc>("Guide");
		_dialogue = GetNode<DialoguePanel>("Interface/Dialogue");
		_controlsPanel = GetNode<ControlsPanel>("Interface/Controls");
		_displaySettingsPanel = GetNode<DisplaySettingsPanel>("Interface/DisplaySettings");
		_gameMenuPanel = GetNode<GameMenuPanel>("Interface/GameMenu");
		_equipmentPanel = GetNode<CharacterEquipmentPanel>("Interface/Equipment");
		_instructions = GetNode<Label>("Interface/Instructions");
		_developmentStatus = GetNode<Label>("Interface/DevelopmentStatus");
		SetProcessUnhandledInput(false);
	}

	/// <summary>
	/// Explicitly injects application-lifetime services after the PackedScene is instantiated.
	/// The scene never searches the global tree for GameRoot or an autoload.
	/// </summary>
	public void Initialize(
		IContentCatalog content,
		IGameSession session,
		IExplorationDevelopmentCommands developmentCommands,
		InputBindingService inputBindings,
		DisplaySettingsService displaySettings,
		LocalizedTextCatalog text)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(session);
		ArgumentNullException.ThrowIfNull(developmentCommands);
		ArgumentNullException.ThrowIfNull(inputBindings);
		ArgumentNullException.ThrowIfNull(displaySettings);
		ArgumentNullException.ThrowIfNull(text);

		if (_session is not null)
		{
			throw new InvalidOperationException("The exploration scene is already initialized.");
		}

        _content = content;
		_session = session;
		_developmentCommands = developmentCommands;
		_inputBindings = inputBindings;
		_session.StateChanged += OnSessionStateChanged;
		_inputBindings.BindingsChanged += OnBindingsChanged;
		_controlsPanel.Initialize(_inputBindings);
		_displaySettingsPanel.Initialize(displaySettings);
		_gameMenuPanel.EquipmentRequested += OnEquipmentRequested;
		_gameMenuPanel.ControlsRequested += OnControlsRequested;
		_gameMenuPanel.DisplayRequested += OnDisplayRequested;
        _equipmentPanel.Initialize(_content, _session, text);
        _room.Initialize(new RpgGame.Core.Maps.MapQueryService(
            _content.GetRequired<MapDefinition>(_room.MapId)));
		RefreshInstructionText();
		ApplyAuthoritativeState();
		SetProcessUnhandledInput(true);
	}

	/// <summary>
	/// Displays feedback for temporary room reconstruction and quick-slot commands.
	/// </summary>
	public void ShowDevelopmentStatus(string message, bool isError = false)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(message);
		_developmentStatus.Text = $"Status: {message}";
		_developmentStatus.Modulate = isError
			? new Color(1.0f, 0.45f, 0.45f)
			: new Color(0.55f, 1.0f, 0.65f);
	}

	public override void _ExitTree()
	{
		if (_session is not null)
		{
			_session.StateChanged -= OnSessionStateChanged;
		}

		if (_inputBindings is not null)
		{
			_inputBindings.BindingsChanged -= OnBindingsChanged;
		}

		if (_gameMenuPanel is not null)
		{
			_gameMenuPanel.EquipmentRequested -= OnEquipmentRequested;
			_gameMenuPanel.ControlsRequested -= OnControlsRequested;
			_gameMenuPanel.DisplayRequested -= OnDisplayRequested;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_session is null
			|| _encounterTransitionRequested
			|| @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		// The controls panel captures rebinding input in _Input before this unhandled-input
		// phase. UI navigation that remains unconsumed must still never move James.
		if (_controlsPanel.IsOpen || _gameMenuPanel.Visible || _equipmentPanel.IsOpen)
		{
			return;
		}

		// R is intentionally used instead of F6: Godot commonly reserves F6 in the
		// editor for "Run Current Scene", which can prevent the running game from
		// receiving this development-only reconstruction command.
		if (MatchesKey(keyEvent, Key.R))
		{
			GetViewport().SetInputAsHandled();
			if (_developmentCommandInProgress)
			{
				ShowDevelopmentStatus("Wait for the current save/load operation to finish.");
				return;
			}

			ReloadRequested?.Invoke(this, EventArgs.Empty);
			return;
		}

		if (MatchesKey(keyEvent, Key.K))
		{
			GetViewport().SetInputAsHandled();
			_ = SaveQuickSlotAsync();
			return;
		}

		if (MatchesKey(keyEvent, Key.L))
		{
			GetViewport().SetInputAsHandled();
			_ = LoadQuickSlotAsync();
			return;
		}

		if (_dialogue.IsOpen)
		{
			if (keyEvent.IsActionPressed(GameInputActions.Interact))
			{
				_dialogue.Advance();
				GetViewport().SetInputAsHandled();
			}
			else if (keyEvent.IsActionPressed(GameInputActions.Menu))
			{
				_dialogue.Close();
				GetViewport().SetInputAsHandled();
			}

			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.Equipment))
		{
			_equipmentPanel.Open();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.Menu))
		{
			_gameMenuPanel.Open();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (TryGetMovement(keyEvent, out Vector2I delta, out string facing))
		{
			// A successful step may synchronously ask GameRoot to remove this scene. Mark the
			// input handled while this Node still owns a viewport, then perform the move.
			GetViewport().SetInputAsHandled();
			TryMove(delta, facing);
			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.Interact))
		{
			TryInteract();
			GetViewport().SetInputAsHandled();
		}
	}

	private void TryMove(Vector2I delta, string facing)
	{
		IGameSession session = RequireSession();
		MapLocationState location = session.Current.Location;
		var currentTile = new Vector2I(location.X, location.Y);
		Vector2I requestedTile = currentTile + delta;
		bool canEnter = _room.IsWalkable(requestedTile)
			&& requestedTile != _room.GuideTile;
		Vector2I acceptedTile = canEnter ? requestedTile : currentTile;
		bool moved = acceptedTile != currentTile;

		// Facing changes even when a wall blocks movement, matching classic JRPG controls
		// and allowing the player to turn toward an adjacent NPC before interacting.
		// `with` preserves unknown future location fields held by JsonExtensionData. Creating
		// a brand-new DTO here would make ordinary movement erase data written by a newer build.
		session.UpdateLocation(location with
		{
			X = acceptedTile.X,
			Y = acceptedTile.Y,
			Facing = facing,
		});

		// Trigger only on the edge created by a successful step. ApplyAuthoritativeState,
		// save/load, R reconstruction, and returning from battle merely render the
		// saved tile and never call this code, so standing on the marker cannot auto-launch.
		if (moved && !_encounterTransitionRequested
			&& _room.TryGetEncounterAt(acceptedTile, out string encounterId))
		{
			_encounterTransitionRequested = true;
			SetProcessUnhandledInput(false);
			EncounterRequested?.Invoke(
				this,
				new EncounterLaunchRequestedEventArgs(new EncounterLaunchRequest(encounterId, _room.MapId)));
			return;
		}

		if (!moved || _encounterTransitionRequested)
		{
			return;
		}

        if (_room.TryGetTransitionAt(acceptedTile, out MapTransitionDefinition? transition)
            && transition is not null)
		{
			_encounterTransitionRequested = true;
			SetProcessUnhandledInput(false);
			TransitionRequested?.Invoke(this,
				new MapTransitionRequestedEventArgs(new MapTransitionRequest(transition.Id)));
		}
	}

	private void TryInteract()
	{
		IGameSession session = RequireSession();
		MapLocationState location = session.Current.Location;
		var playerTile = new Vector2I(location.X, location.Y);
		Vector2I targetTile = playerTile + FacingToOffset(location.Facing);

		if (targetTile != _guide.TilePosition)
		{
			return;
		}

		ExplorationInteractionResult result = _guide.Interact(session);
		DialogueDefinition dialogue = RequireContent()
			.GetRequired<DialogueDefinition>(result.DialogueId);
		_dialogue.ShowDialogue(dialogue);
	}

	private void OnSessionStateChanged(object? sender, EventArgs eventArgs) =>
		ApplyAuthoritativeState();

	private void OnBindingsChanged(object? sender, EventArgs eventArgs) =>
		RefreshInstructionText();

	private void RefreshInstructionText()
	{
		InputBindingService bindings = RequireInputBindings();
		_instructions.Text =
			$"Move: U[{bindings.FormatBindings(GameInputActions.MoveUp)}] "
			+ $"R[{bindings.FormatBindings(GameInputActions.MoveRight)}] "
			+ $"D[{bindings.FormatBindings(GameInputActions.MoveDown)}] "
			+ $"L[{bindings.FormatBindings(GameInputActions.MoveLeft)}]"
			+ System.Environment.NewLine
			+ $"Interact[{bindings.FormatBindings(GameInputActions.Interact)}]    "
			+ $"Equipment[{bindings.FormatBindings(GameInputActions.Equipment)}]    "
			+ $"Menu[{bindings.FormatBindings(GameInputActions.Menu)}]    "
			+ "Developer: R rebuild, K save, L load";
	}

	private void OnEquipmentRequested(object? sender, EventArgs eventArgs)
	{
		_gameMenuPanel.Close();
		_equipmentPanel.Open();
	}

	private void OnControlsRequested(object? sender, EventArgs eventArgs)
	{
		_gameMenuPanel.Close();
		_controlsPanel.Open();
	}

	private void OnDisplayRequested(object? sender, EventArgs eventArgs)
	{
		_gameMenuPanel.Close();
		_displaySettingsPanel.Open();
	}

	private void ApplyAuthoritativeState()
	{
		IGameSession session = RequireSession();
		MapLocationState location = session.Current.Location;
		if (!string.Equals(location.MapId, _room.MapId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				$"Map view '{_room.MapId}' cannot present map '{location.MapId}'.");
		}

		var tile = new Vector2I(location.X, location.Y);
		if (!_room.IsWalkable(tile) || tile == _room.GuideTile)
		{
			throw new InvalidOperationException(
				$"Saved tile ({tile.X}, {tile.Y}) is not walkable in map '{_room.MapId}'.");
		}

		_player.Position = _room.TileToWorld(tile);
		_player.SetFacing(location.Facing);
		_guide.Visible = _room.GuideTile.X >= 0;
		_guide.Position = _room.GuideTile.X >= 0
			? _room.TileToWorld(_room.GuideTile)
			: Vector2.Zero;
		_guide.RefreshFromState(session);
		_room.SetEncounterCleared(TestRoomEncounterProgress.IsCleared(
			session,
			_room.MapId == TestRoomView.MapId
				? TestRoomView.EncounterId
				: TestForestView.EncounterId));
	}

	private static bool TryGetMovement(
		InputEventKey keyEvent,
		out Vector2I delta,
		out string facing)
	{
		if (keyEvent.IsActionPressed(GameInputActions.MoveUp))
		{
			delta = Vector2I.Up;
			facing = "north";
			return true;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveRight))
		{
			delta = Vector2I.Right;
			facing = "east";
			return true;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveDown))
		{
			delta = Vector2I.Down;
			facing = "south";
			return true;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveLeft))
		{
			delta = Vector2I.Left;
			facing = "west";
			return true;
		}

		delta = Vector2I.Zero;
		facing = string.Empty;
		return false;
	}

	/// <summary>
	/// Accepts both the localized keycode and physical keyboard position. This keeps the
	/// development shortcuts reliable across keyboard layouts and with either letter case.
	/// </summary>
	private static bool MatchesKey(InputEventKey keyEvent, Key expected) =>
		keyEvent.Keycode == expected || keyEvent.PhysicalKeycode == expected;

	private static Vector2I FacingToOffset(string facing) => facing switch
	{
		"north" => Vector2I.Up,
		"east" => Vector2I.Right,
		"west" => Vector2I.Left,
		_ => Vector2I.Down,
	};

	private IGameSession RequireSession() => _session
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private IContentCatalog RequireContent() => _content
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private IExplorationDevelopmentCommands RequireDevelopmentCommands() =>
		_developmentCommands
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private InputBindingService RequireInputBindings() => _inputBindings
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private async Task SaveQuickSlotAsync()
	{
		if (_developmentCommandInProgress)
		{
			ShowDevelopmentStatus("A save/load operation is already running.");
			return;
		}

		_developmentCommandInProgress = true;
		IExplorationDevelopmentCommands commands = RequireDevelopmentCommands();
		ShowDevelopmentStatus($"Saving {commands.QuickSlotId}...");

		try
		{
			await commands.SaveQuickSlotAsync();
			ShowDevelopmentStatus($"Saved {commands.QuickSlotId}.");
		}
		catch (Exception exception)
		{
			// This is a development surface, so the concrete exception message is useful.
			// Production save UI will translate failures into player-facing categories.
			ShowDevelopmentStatus($"Save failed: {exception.Message}", isError: true);
			GD.PushError($"Quick save failed:{System.Environment.NewLine}{exception}");
		}
		finally
		{
			_developmentCommandInProgress = false;
		}
	}

	private async Task LoadQuickSlotAsync()
	{
		if (_developmentCommandInProgress)
		{
			ShowDevelopmentStatus("A save/load operation is already running.");
			return;
		}

		_developmentCommandInProgress = true;
		IExplorationDevelopmentCommands commands = RequireDevelopmentCommands();
		ShowDevelopmentStatus($"Loading {commands.QuickSlotId}...");

		try
		{
			bool loaded = await commands.LoadQuickSlotAsync();
			if (loaded)
			{
				// Dialogue progress belongs to the disposable UI, not GameState. Keeping an
				// old panel open after restoring a save would display stale presentation.
				_dialogue.Close();
			}

			ShowDevelopmentStatus(loaded
				? $"Loaded {commands.QuickSlotId}."
				: $"No save exists in {commands.QuickSlotId}.",
				isError: !loaded);
		}
		catch (Exception exception)
		{
			ShowDevelopmentStatus($"Load failed: {exception.Message}", isError: true);
			GD.PushError($"Quick load failed:{System.Environment.NewLine}{exception}");
		}
		finally
		{
			_developmentCommandInProgress = false;
		}
	}
}
