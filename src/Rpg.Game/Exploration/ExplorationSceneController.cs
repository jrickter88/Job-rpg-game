using Godot;
using RpgGame.Core.Combat;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Maps;
using RpgGame.Core.Presentation;
using RpgGame.Core.State;
using RpgGame.Display;
using RpgGame.Encounters;
using RpgGame.Input;
using RpgGame.Localization;

namespace RpgGame.Exploration;

/// <summary>
/// Coordinates keyboard intent, authored map collision, persistent state, and dialogue UI.
/// </summary>
/// <remarks>
/// Godot concerns stop here: keys become logical tile requests, the room accepts or rejects
/// them, and only accepted coordinates/facing are submitted to <see cref="IGameSession"/>.
/// Reconstructing this scene therefore needs no hidden Node state—only content and GameState.
/// </remarks>
public partial class ExplorationSceneController : Node2D
{
	private IExplorationMapView _room = null!;
	private Camera2D _camera = null!;
	private PlayerMarkerView _player = null!;
	private TestGuideNpc _guide = null!;
	private DialoguePanel _dialogue = null!;
	private ControlsPanel _controlsPanel = null!;
	private DisplaySettingsPanel _displaySettingsPanel = null!;
	private SoundSettingsPanel _soundSettingsPanel = null!;
	private GameMenuPanel _gameMenuPanel = null!;
	private CharacterEquipmentPanel _equipmentPanel = null!;
	private Label _instructions = null!;
	private Label _developmentStatus = null!;
	private IContentCatalog? _content;
	private LocalizedTextCatalog? _text;
	private IGameSession? _session;
	private IRandomSource? _randomSource;
	private MapQueryService? _mapQuery;
	private IExplorationDevelopmentCommands? _developmentCommands;
	private InputBindingService? _inputBindings;
	private bool _developmentCommandInProgress;
	private bool _encounterTransitionRequested;
	private bool _animateNextPlayerPosition;
	private bool _playerMovementInProgress;
	private bool _readyForInput;
	private string? _heldMovementAction;
	private Vector2I _heldMovementDelta;
	private string _heldMovementFacing = string.Empty;
	private double _movementRepeatTimer;
	private MapEncounterMarkerDefinition? _pendingEncounterAfterDialogue;
	private static readonly RandomEncounterResolver RandomEncounterResolver = new();

	private const double MovementInitialDelaySeconds = 0.18;

	/// <summary>Requests reconstruction by the composition root without adding navigation.</summary>
	public event EventHandler? ReloadRequested;

	/// <summary>
	/// Requests a direct handoff for a fixed or random encounter selected during exploration.
	/// The owner decides which scene to show; exploration never searches for GameRoot.
	/// </summary>
	public event EventHandler<EncounterLaunchRequestedEventArgs>? EncounterRequested;
	public event EventHandler<MapTransitionRequestedEventArgs>? TransitionRequested;

	public override void _Ready()
	{
		Node2D mapNode = GetNode<Node2D>("Map");
		_camera = GetNode<Camera2D>("Camera");
		_room = mapNode as IExplorationMapView
			?? throw new InvalidOperationException("Exploration map node must implement IExplorationMapView.");
		_player = GetNode<PlayerMarkerView>("Player");
		_guide = GetNode<TestGuideNpc>("Guide");
		_camera.PositionSmoothingEnabled = false;
		_camera.Zoom = Vector2.One;
		_camera.Enabled = true;
		_dialogue = GetNode<DialoguePanel>("Interface/Dialogue");
		_controlsPanel = GetNode<ControlsPanel>("Interface/Controls");
		_displaySettingsPanel = GetNode<DisplaySettingsPanel>("Interface/DisplaySettings");
		_soundSettingsPanel = GetNode<SoundSettingsPanel>("Interface/SoundSettings");
		_gameMenuPanel = GetNode<GameMenuPanel>("Interface/GameMenu");
		_equipmentPanel = GetNode<CharacterEquipmentPanel>("Interface/Equipment");
		_instructions = GetNode<Label>("Interface/Instructions");
		_developmentStatus = GetNode<Label>("Interface/DevelopmentStatus");
		SetProcessUnhandledInput(false);
		SetProcess(false);
		_readyForInput = false;
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
		IRandomSource randomSource,
		LocalizedTextCatalog text,
		bool resumeHeldMovement = true)
	{
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(session);
		ArgumentNullException.ThrowIfNull(developmentCommands);
		ArgumentNullException.ThrowIfNull(inputBindings);
		ArgumentNullException.ThrowIfNull(displaySettings);
		ArgumentNullException.ThrowIfNull(randomSource);
		ArgumentNullException.ThrowIfNull(text);

		if (_session is not null)
		{
			throw new InvalidOperationException("The exploration scene is already initialized.");
		}

		_content = content;
		_text = text;
		_session = session;
		_randomSource = randomSource;
		_developmentCommands = developmentCommands;
		_inputBindings = inputBindings;
		_session.StateChanged += OnSessionStateChanged;
		_inputBindings.BindingsChanged += OnBindingsChanged;
		_controlsPanel.Initialize(_inputBindings);
		_displaySettingsPanel.Initialize(displaySettings);
		_soundSettingsPanel.Initialize(displaySettings);
		_gameMenuPanel.EquipmentRequested += OnEquipmentRequested;
		_gameMenuPanel.SaveSlotRequested += OnSaveSlotRequested;
		_gameMenuPanel.ControlsRequested += OnControlsRequested;
		_gameMenuPanel.DisplayRequested += OnDisplayRequested;
		_gameMenuPanel.SoundRequested += OnSoundRequested;
		_equipmentPanel.Initialize(_content, _session, text);
		_mapQuery = new MapQueryService(
			_content.GetRequired<MapDefinition>(_session.Current.Location.MapId));
		_room.Initialize(_mapQuery, _content);
		ConfigureCamera();
		RefreshInstructionText();
		ApplyAuthoritativeState();
		SetProcessUnhandledInput(true);
		SetProcess(true);
		_readyForInput = true;
		if (resumeHeldMovement)
		{
			ResumeHeldMovementIfPressed();
		}
	}

	public override void _Process(double delta)
	{
		if (_heldMovementAction is null
			|| _session is null
			|| _encounterTransitionRequested
			|| _dialogue.IsOpen
			|| _controlsPanel.IsOpen
			|| _gameMenuPanel.Visible
			|| _equipmentPanel.IsOpen
			|| !global::Godot.Input.IsActionPressed(_heldMovementAction))
		{
			ClearHeldMovement();
			return;
		}

		if (_playerMovementInProgress)
		{
			UpdateCamera(_player.Position);
			return;
		}

		_movementRepeatTimer -= delta;
		if (_movementRepeatTimer > 0)
		{
			return;
		}

		TryMove(_heldMovementDelta, _heldMovementFacing);
	}

	/// <summary>
	/// Displays feedback for temporary room reconstruction and development commands.
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
		_readyForInput = false;
		SetProcessUnhandledInput(false);
		SetProcess(false);

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
			_gameMenuPanel.SaveSlotRequested -= OnSaveSlotRequested;
			_gameMenuPanel.ControlsRequested -= OnControlsRequested;
		_gameMenuPanel.DisplayRequested -= OnDisplayRequested;
			_gameMenuPanel.SoundRequested -= OnSoundRequested;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!_readyForInput
			|| _session is null
			|| _encounterTransitionRequested
			|| @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		// The controls panel captures rebinding input in _Input before this unhandled-input
		// phase. UI navigation that remains unconsumed must still never move James.
		if (_controlsPanel.IsOpen || _gameMenuPanel.Visible || _equipmentPanel.IsOpen
			|| _displaySettingsPanel.IsOpen || _soundSettingsPanel.IsOpen)
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

		if (_dialogue.IsOpen)
		{
			if (keyEvent.IsActionPressed(GameInputActions.Interact))
			{
				_dialogue.Advance();
				LaunchPendingEncounterAfterDialogue();
				GetViewport().SetInputAsHandled();
			}
			else if (keyEvent.IsActionPressed(GameInputActions.Menu))
			{
				_dialogue.Close();
				LaunchPendingEncounterAfterDialogue();
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
			BeginHeldMovement(keyEvent, delta, facing);
			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.Interact))
		{
			TryInteract();
			GetViewport().SetInputAsHandled();
		}
	}

	private void BeginHeldMovement(InputEventKey keyEvent, Vector2I delta, string facing)
	{
		string action = GetMovementAction(keyEvent);
		BeginHeldMovement(action, delta, facing);
	}

	private void BeginHeldMovement(string action, Vector2I delta, string facing)
	{
		_heldMovementAction = action;
		_heldMovementDelta = delta;
		_heldMovementFacing = facing;
		_movementRepeatTimer = MovementInitialDelaySeconds;
		_player.SetFacing(facing);

		// Direction changes are rendered immediately, but the logical step already in
		// progress owns movement until its tween completes. The held direction remains
		// queued in these fields and is consumed by OnPlayerMovementCompleted.
		if (!_playerMovementInProgress)
		{
			TryMove(delta, facing);
		}
	}

	private void ResumeHeldMovementIfPressed()
	{
		(string Action, Vector2I Delta, string Facing)[] directions =
		[
			(GameInputActions.MoveUp, Vector2I.Up, "north"),
			(GameInputActions.MoveRight, Vector2I.Right, "east"),
			(GameInputActions.MoveDown, Vector2I.Down, "south"),
			(GameInputActions.MoveLeft, Vector2I.Left, "west"),
		];

		foreach ((string action, Vector2I delta, string facing) in directions)
		{
			if (global::Godot.Input.IsActionPressed(action))
			{
				BeginHeldMovement(action, delta, facing);
				break;
			}
		}
	}

	private void ClearHeldMovement()
	{
		_heldMovementAction = null;
		_heldMovementDelta = Vector2I.Zero;
		_heldMovementFacing = string.Empty;
		_movementRepeatTimer = 0;
	}

	private void TryMove(Vector2I delta, string facing)
	{
		if (_playerMovementInProgress)
		{
			return;
		}

		IGameSession session = RequireSession();
		MapLocationState location = session.Current.Location;
		var currentTile = new Vector2I(location.X, location.Y);
		Vector2I requestedTile = currentTile + delta;
		bool blockedByEncounterActor = _room.TryGetEncounterAt(
			requestedTile,
			out MapEncounterMarkerDefinition? requestedEncounter)
			&& requestedEncounter?.DialogueId is not null;
		bool canEnter = _room.IsWalkable(requestedTile)
			&& requestedTile != _room.GuideTile
			&& !blockedByEncounterActor;
		Vector2I acceptedTile = canEnter ? requestedTile : currentTile;
		bool moved = acceptedTile != currentTile;
		_animateNextPlayerPosition = moved;
		_playerMovementInProgress = moved;

		// Facing changes even when a wall blocks movement, matching classic JRPG controls
		// and allowing the player to turn toward an adjacent NPC before interacting.
		// Keep the existing location record so movement only changes the fields it owns.
		session.UpdateLocation(location with
		{
			X = acceptedTile.X,
			Y = acceptedTile.Y,
			Facing = facing,
		});

		// Enter-the-tile markers trigger only on a successful step. Dialogue-backed actors use
		// interaction instead. Applying saved state and returning from battle never call here.
		if (moved && !_encounterTransitionRequested
			&& _room.TryGetEncounterAt(
				acceptedTile,
				out MapEncounterMarkerDefinition? marker)
			&& marker is not null
			&& marker.DialogueId is null)
		{
			LaunchEncounter(marker.EncounterId, marker.ClearedFlagId);
			return;
		}

		if (!moved || _encounterTransitionRequested)
		{
			return;
		}

		_player.AdvanceStepAnimation();

		if (_room.TryGetTransitionAt(acceptedTile, out MapTransitionDefinition? transition)
			&& transition is not null)
		{
			_encounterTransitionRequested = true;
			SetProcessUnhandledInput(false);
			TransitionRequested?.Invoke(this,
				new MapTransitionRequestedEventArgs(new MapTransitionRequest(transition.Id)));
			return;
		}

		string? randomEncounterId = RandomEncounterResolver.Resolve(
			RequireMapQuery().RandomEncounters,
			RequireRandomSource());
		if (randomEncounterId is not null)
		{
			LaunchEncounter(randomEncounterId, clearanceFlagId: null);
		}
	}

	private void TryInteract()
	{
		IGameSession session = RequireSession();
		MapLocationState location = session.Current.Location;
		var playerTile = new Vector2I(location.X, location.Y);
		Vector2I targetTile = playerTile + FacingToOffset(location.Facing);

		if (_room.GuideTile.X >= 0 && _guide.Visible && targetTile == _room.GuideTile)
		{
			ExplorationInteractionResult result = _guide.Interact(session);
			DialogueDefinition dialogue = RequireContent()
				.GetRequired<DialogueDefinition>(result.DialogueId);
			_dialogue.ShowDialogue(dialogue, RequireText());
			return;
		}

		if (_room.TryGetEncounterAt(targetTile, out MapEncounterMarkerDefinition? marker)
			&& marker?.DialogueId is not null)
		{
			ClearHeldMovement();
			_pendingEncounterAfterDialogue = marker;
			DialogueDefinition dialogue = RequireContent()
				.GetRequired<DialogueDefinition>(marker.DialogueId);
			_dialogue.ShowDialogue(dialogue, RequireText());
		}
	}

	private void LaunchPendingEncounterAfterDialogue()
	{
		if (_dialogue.IsOpen || _pendingEncounterAfterDialogue is null)
		{
			return;
		}

		MapEncounterMarkerDefinition marker = _pendingEncounterAfterDialogue;
		_pendingEncounterAfterDialogue = null;
		LaunchEncounter(marker.EncounterId, marker.ClearedFlagId);
	}

	private void LaunchEncounter(string encounterId, string? clearanceFlagId)
	{
		_encounterTransitionRequested = true;
		ClearHeldMovement();
		SetProcessUnhandledInput(false);
		EncounterRequested?.Invoke(
			this,
			new EncounterLaunchRequestedEventArgs(
				new EncounterLaunchRequest(encounterId, _room.MapId, clearanceFlagId)));
	}

	private void OnSessionStateChanged(object? sender, EventArgs eventArgs) =>
		ApplyAuthoritativeState();

	private void OnBindingsChanged(object? sender, EventArgs eventArgs) =>
		RefreshInstructionText();

    private void RefreshInstructionText()
    {
        InputBindingService bindings = RequireInputBindings();

        _instructions.Text =
            $"Move U[{bindings.FormatBindings(GameInputActions.MoveUp)}] "
            + $"R[{bindings.FormatBindings(GameInputActions.MoveRight)}]"
            + System.Environment.NewLine
            + $"Move D[{bindings.FormatBindings(GameInputActions.MoveDown)}] "
            + $"L[{bindings.FormatBindings(GameInputActions.MoveLeft)}]"
            + System.Environment.NewLine
            + $"Interact[{bindings.FormatBindings(GameInputActions.Interact)}] "
            + $"Menu[{bindings.FormatBindings(GameInputActions.Menu)}]"
            + System.Environment.NewLine
            + $"Equipment[{bindings.FormatBindings(GameInputActions.Equipment)}]  "
            + "R rebuild";
    }

    private void OnEquipmentRequested(object? sender, EventArgs eventArgs)
	{
		_gameMenuPanel.Close();
		_equipmentPanel.Open();
	}

	private void OnSaveSlotRequested(
		object? sender,
		SaveSlotRequestedEventArgs eventArgs)
	{
		if (eventArgs.IsLoad)
		{
			_ = LoadSlotAsync(eventArgs.SlotId);
		}
		else
		{
			_ = SaveSlotAsync(eventArgs.SlotId);
		}
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

	private void OnSoundRequested(object? sender, EventArgs eventArgs)
	{
		_gameMenuPanel.Close();
		_soundSettingsPanel.Open();
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

		Vector2 targetPosition = _room.TileToWorld(tile);
		if (_animateNextPlayerPosition)
		{
			_animateNextPlayerPosition = false;
			_player.AnimateTo(targetPosition, OnPlayerMovementCompleted);
		}
		else
		{
			_player.Position = targetPosition;
			UpdateCamera(targetPosition);
		}
		_player.SetFacing(location.Facing);
		_guide.Visible = _room.GuideTile.X >= 0;
		_guide.Position = _room.GuideTile.X >= 0
			? _room.TileToWorld(_room.GuideTile)
			: Vector2.Zero;
		_guide.RefreshFromState(session);
		_room.SetClearedEncounterFlags(session.Current.EventFlags
			.Where(flag => flag.Value)
			.Select(flag => flag.Key)
			.ToHashSet(StringComparer.Ordinal));
	}

	private void OnPlayerMovementCompleted()
	{
		_playerMovementInProgress = false;
		UpdateCamera(_player.Position);
		if (_heldMovementAction is null
			|| _session is null
			|| !global::Godot.Input.IsActionPressed(_heldMovementAction))
		{
			ClearHeldMovement();
			return;
		}

		TryMove(_heldMovementDelta, _heldMovementFacing);
	}

	private void ConfigureCamera()
	{
		CameraLimits limits = PixelPerfectGeometry.CalculateCameraLimits(
			_room.MapPixelWidth / PixelPerfectGeometry.NativeTileSize,
			_room.MapPixelHeight / PixelPerfectGeometry.NativeTileSize);
		_camera.LimitLeft = limits.Left;
		_camera.LimitTop = limits.Top;
		_camera.LimitRight = limits.Right;
		_camera.LimitBottom = limits.Bottom;
		_camera.LimitSmoothed = false;
		_camera.PositionSmoothingEnabled = false;
	}

	private void UpdateCamera(Vector2 targetPosition)
	{
		PixelPoint center = PixelPerfectGeometry.CalculateCameraCenter(
			_room.MapPixelWidth,
			_room.MapPixelHeight,
			PixelPerfectGeometry.SnapToPixel(targetPosition.X, targetPosition.Y));
		_camera.Position = new Vector2(center.X, center.Y);
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

	private static string GetMovementAction(InputEventKey keyEvent)
	{
		if (keyEvent.IsActionPressed(GameInputActions.MoveUp))
		{
			return GameInputActions.MoveUp;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveRight))
		{
			return GameInputActions.MoveRight;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveDown))
		{
			return GameInputActions.MoveDown;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveLeft))
		{
			return GameInputActions.MoveLeft;
		}

		throw new InvalidOperationException("Movement action was not present on the key event.");
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

	private LocalizedTextCatalog RequireText() => _text
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private IRandomSource RequireRandomSource() => _randomSource
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private MapQueryService RequireMapQuery() => _mapQuery
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private IExplorationDevelopmentCommands RequireDevelopmentCommands() =>
		_developmentCommands
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private InputBindingService RequireInputBindings() => _inputBindings
		?? throw new InvalidOperationException("ExplorationSceneController is not initialized.");

	private async Task SaveSlotAsync(string slotId)
	{
		if (_developmentCommandInProgress)
		{
			ShowDevelopmentStatus("A save/load operation is already running.");
			return;
		}

		_developmentCommandInProgress = true;
		IExplorationDevelopmentCommands commands = RequireDevelopmentCommands();
		ShowDevelopmentStatus($"Saving {slotId}...");

		try
		{
			await commands.SaveSlotAsync(slotId);
			ShowDevelopmentStatus($"Saved {slotId}.");
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

	private async Task LoadSlotAsync(string slotId)
	{
		if (_developmentCommandInProgress)
		{
			ShowDevelopmentStatus("A save/load operation is already running.");
			return;
		}

		_developmentCommandInProgress = true;
		IExplorationDevelopmentCommands commands = RequireDevelopmentCommands();
		ShowDevelopmentStatus($"Loading {slotId}...");

		try
		{
			bool loaded = await commands.LoadSlotAsync(slotId);
			if (loaded)
			{
				// GameRoot replaced this controller and reconstructed a fresh presentation.
				// Do not touch this scene after the successful handoff.
				return;
			}

			ShowDevelopmentStatus(loaded
				? $"Loaded {slotId}."
				: $"No save exists in {slotId}.",
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
