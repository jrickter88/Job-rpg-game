using Godot;
using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Rewards;
using RpgGame.Input;

namespace RpgGame.Encounters;

/// <summary>
/// Projects the acting combatant's authored commands and presents authoritative combat results.
/// </summary>
/// <remarks>
/// This Godot controller owns buttons, focus, labels, and event-log text. It never calculates
/// damage, changes HP or MP directly, orders actions, or decides victory. Those facts arrive through
/// <see cref="ICombatTimelineResolver"/>, replacement <see cref="CombatSnapshot"/> instances, and
/// typed <see cref="CombatEvent"/> values. The controller also receives no GameSession, so it
/// cannot accidentally clear an encounter before GameRoot handles a confirmed terminal result.
/// </remarks>
public partial class BattleController : Control
{
	private Label _encounterLabel = null!;
	private Label _battlefieldLabel = null!;
	private TextureRect _battlefieldBackground = null!;
	private Button _gridDebugToggle = null!;
	private Button _battleLogDebugToggle = null!;
	private PanelContainer _battleLogWindow = null!;
	private Button _battleLogCloseButton = null!;
	private VBoxContainer _battleLogHost = null!;
	private BattleFormationView _formationView = null!;
	private VBoxContainer _partyStatus = null!;
	private VBoxContainer _enemyStatus = null!;
	private Label _phaseLabel = null!;
	private Label _turnOrderLabel = null!;
	private VBoxContainer _commandMenu = null!;
	private HBoxContainer _targetRow = null!;
	private Label _targetPrompt = null!;
	private HBoxContainer _targetButtons = null!;
	private RichTextLabel _eventLog = null!;
	private Label _resultLabel = null!;
	private Button _continueButton = null!;
	private Label _inputHint = null!;

	private readonly Dictionary<string, Label> _hpLabelByInstanceId =
		new(StringComparer.Ordinal);
	private readonly Dictionary<string, Button> _targetButtonByInstanceId =
		new(StringComparer.Ordinal);
	private readonly List<Button> _commandButtons = [];
	private readonly Dictionary<Button, Action> _commandActionByButton = [];

	private IContentCatalog? _content;
	private BattleCommandAvailabilityResolver? _commandAvailabilityResolver;
	private InputBindingService? _inputBindings;
	private ICombatTimelineResolver? _timelineResolver;
	private IEnemyCommandPlanner? _enemyPlanner;
	private CombatSnapshot? _snapshot;
	private string? _encounterId;
	private string? _selectedAbilityId;
	private string? _selectedMagicDisciplineId;
	private string? _selectedTargetId;
	private Button? _focusedCommandButton;
	private BattleInputPhase _phase = BattleInputPhase.Uninitialized;
	private bool _completionRequested;
	private bool _showingVictoryRewards;
	private bool _showFormationGrid = true;
	private bool _showBattleLog;
	private static readonly List<string> PersistentBattleLogLines = [];
	private static bool PersistentBattleLogVisible;

	/// <summary>
	/// Raised only after the player confirms a core-authored victory or defeat outcome.
	/// </summary>
	public event EventHandler<BattleCompletionRequestedEventArgs>? CompletionRequested;
	public event EventHandler? VictoryRewardsContinueRequested;

	public void ShowVictoryRewards(VictoryRewardResult rewards)
	{
		ArgumentNullException.ThrowIfNull(rewards);
		_formationView.SetVictoryRewardItems(rewards.Awards);
		_showingVictoryRewards = true;
		_completionRequested = false;
		_resultLabel.Text = rewards.Awards.Count == 0
			? "Victory! No items found."
			: "Victory! Items acquired.";
		_continueButton.Disabled = false;
		_continueButton.Visible = true;
		_continueButton.GrabFocus();
		SetProcessUnhandledInput(true);
	}

	public override void _Ready()
	{
		_encounterLabel = GetNode<Label>("Margin/VBox/EncounterId");
		_battlefieldLabel = GetNode<Label>("Margin/VBox/BattlefieldId");
		_battlefieldBackground = GetNode<TextureRect>("BattlefieldBackground");
		_gridDebugToggle = GetNode<Button>("GridDebugToggle");
		_battleLogDebugToggle = GetNode<Button>("BattleLogDebugToggle");
		_battleLogWindow = GetNode<PanelContainer>("BattleLogWindow");
		_battleLogCloseButton = GetNode<Button>("BattleLogWindow/Margin/VBox/Header/Close");
		_battleLogHost = GetNode<VBoxContainer>("BattleLogWindow/Margin/VBox/LogHost");
		_formationView = GetNode<BattleFormationView>("Margin/VBox/FormationView");
		_partyStatus = GetNode<VBoxContainer>("Margin/VBox/StatusRow/PartyStatus");
		_enemyStatus = GetNode<VBoxContainer>("Margin/VBox/StatusRow/EnemyStatus");
		_phaseLabel = GetNode<Label>("Margin/VBox/CommandArea/Phase");
		_turnOrderLabel = GetNode<Label>("Margin/VBox/TurnOrder");
		_commandMenu = GetNode<VBoxContainer>("Margin/VBox/CommandArea/CommandMenu");
		_targetRow = GetNode<HBoxContainer>("Margin/VBox/CommandArea/TargetRow");
		_targetPrompt = GetNode<Label>("Margin/VBox/CommandArea/TargetRow/TargetPrompt");
		_targetButtons = GetNode<HBoxContainer>("Margin/VBox/CommandArea/TargetRow/Targets");
		_eventLog = GetNode<RichTextLabel>("Margin/VBox/EventLog");
		_resultLabel = GetNode<Label>("Margin/VBox/ResultRow/Result");
		_continueButton = GetNode<Button>("Margin/VBox/ResultRow/Continue");
		_inputHint = GetNode<Label>("Margin/VBox/InputHint");

		_continueButton.Pressed += RequestCompletion;
		_battleLogCloseButton.Pressed += ToggleBattleLog;
		ConfigureLowerBattlePanel();
		_showFormationGrid = false;
		_formationView.SetGridVisible(false);
		SetGridDebugPresentation();
		_showBattleLog = PersistentBattleLogVisible;
		_battleLogWindow.Visible = _showBattleLog;
		_battleLogDebugToggle.Text = _showBattleLog
			? "Hide Log (Debug)"
			: "Show Log (Debug)";
		if (PersistentBattleLogLines.Count > 0)
		{
			_eventLog.AppendText(string.Join(
				System.Environment.NewLine,
				PersistentBattleLogLines)
				+ System.Environment.NewLine);
		}
		SetProcessInput(true);
		SetProcessUnhandledInput(false);
	}

	private void ConfigureLowerBattlePanel()
	{
		var statusRow = (HBoxContainer)_partyStatus.GetParent();
		var stack = (VBoxContainer)statusRow.GetParent();
		var commandArea = (VBoxContainer)_commandMenu.GetParent();

		// Native 320x240 battle HUD. The old implementation expected a
		// 1280x720-style layout and could never fit in the native viewport.
		var lowerPanel = new HBoxContainer
		{
			CustomMinimumSize = new Vector2(0.0f, 72.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
		};

		lowerPanel.AddThemeConstantOverride("separation", 4);

		stack.AddChild(lowerPanel);
		stack.MoveChild(lowerPanel, statusRow.GetIndex());

		var enemyPanel = new VBoxContainer
		{
			CustomMinimumSize = new Vector2(88.0f, 0.0f),
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ExpandFill,
		};

		enemyPanel.AddThemeConstantOverride("separation", 1);
		lowerPanel.AddChild(enemyPanel);

		_enemyStatus.Reparent(enemyPanel);

		// The event log belongs in its optional debug window rather than consuming
		// permanent room in the native battle HUD.
		_eventLog.Reparent(_battleLogHost);

		commandArea.Reparent(lowerPanel);
		_partyStatus.Reparent(lowerPanel);

		statusRow.Visible = false;

		_enemyStatus.CustomMinimumSize = new Vector2(88.0f, 0.0f);
		_enemyStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		commandArea.CustomMinimumSize = new Vector2(104.0f, 0.0f);
		commandArea.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		commandArea.SizeFlagsVertical = Control.SizeFlags.ExpandFill;

		_partyStatus.CustomMinimumSize = new Vector2(88.0f, 0.0f);
		_partyStatus.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

		_eventLog.CustomMinimumSize = Vector2.Zero;
		_eventLog.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		_eventLog.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
	}

	private void ToggleFormationGrid()
	{
		_showFormationGrid = !_showFormationGrid;
		_formationView.SetGridVisible(_showFormationGrid);
		SetGridDebugPresentation();
	}

	private void SetGridDebugPresentation()
	{
		Color debugTextColor = new Color(1.0f, 1.0f, 1.0f, _showFormationGrid ? 1.0f : 0.0f);
		_encounterLabel.Modulate = debugTextColor;
		_battlefieldLabel.Modulate = debugTextColor;
		_gridDebugToggle.Text = _showFormationGrid
			? "Hide Grid (Debug)"
			: "Show Grid (Debug)";
	}

	private void ToggleBattleLog()
	{
		_showBattleLog = !_showBattleLog;
		PersistentBattleLogVisible = _showBattleLog;
		_battleLogWindow.Visible = _showBattleLog;
		_battleLogDebugToggle.Text = _showBattleLog
			? "Hide Log (Debug)"
			: "Show Log (Debug)";
	}

	/// <summary>
	/// Injects the transient state and pure rule boundaries after scene instantiation.
	/// </summary>
	public void Initialize(
		EncounterDefinition encounter,
		IContentCatalog content,
		BattleCommandAvailabilityResolver commandAvailabilityResolver,
		CombatSnapshot initialSnapshot,
		ICombatTimelineResolver timelineResolver,
		IEnemyCommandPlanner enemyPlanner,
		InputBindingService inputBindings)
	{
		ArgumentNullException.ThrowIfNull(encounter);
		ArgumentNullException.ThrowIfNull(content);
		ArgumentNullException.ThrowIfNull(commandAvailabilityResolver);
		ArgumentNullException.ThrowIfNull(initialSnapshot);
		ArgumentNullException.ThrowIfNull(timelineResolver);
		ArgumentNullException.ThrowIfNull(enemyPlanner);
		ArgumentNullException.ThrowIfNull(inputBindings);
		if (_snapshot is not null)
		{
			throw new InvalidOperationException("Battle scene is already initialized.");
		}

		if (initialSnapshot.Outcome != BattleOutcome.InProgress)
		{
			throw new ArgumentException(
				"A playable battle must begin with both sides alive.",
				nameof(initialSnapshot));
		}

		_ = RequireSingleLivingPartyActor(initialSnapshot);

		_encounterId = encounter.Id;
		_content = content;
		_commandAvailabilityResolver = commandAvailabilityResolver;
		_snapshot = initialSnapshot;
		_timelineResolver = timelineResolver;
		_enemyPlanner = enemyPlanner;
		_inputBindings = inputBindings;
		_inputBindings.BindingsChanged += OnBindingsChanged;

		_encounterLabel.Text = $"Encounter: {encounter.Id}";
		_battlefieldLabel.Text = $"Battlefield: {encounter.BattlefieldId ?? "(none)"}";
		_battlefieldBackground.Texture = LoadBattlefieldBackground(encounter.BattlefieldId);
		_formationView.Initialize(
			initialSnapshot.Combatants
				.Where(combatant => combatant.Side == BattleSide.Enemy)
				.Select(combatant => combatant.Placement)
				.ToArray(),
			initialSnapshot.Combatants
				.Where(combatant => combatant.Side == BattleSide.Party)
				.Select(combatant => combatant.Placement)
				.ToArray(),
			content);

		CreateCombatantControls(initialSnapshot);
		AppendLog("Battle started. Wait-mode timeline active.");
		_phase = BattleInputPhase.Resolving;
		AdvanceToReadyActor();
		SetProcessUnhandledInput(true);
	}

	private static Texture2D? LoadBattlefieldBackground(string? battlefieldId)
	{
		if (string.IsNullOrWhiteSpace(battlefieldId))
		{
			return null;
		}

		const string battlefieldPrefix = "battlefield.";
		string assetName = battlefieldId.StartsWith(battlefieldPrefix, StringComparison.Ordinal)
			? battlefieldId[battlefieldPrefix.Length..].Replace('.', '-')
			: battlefieldId.Replace('.', '-');
		string path = $"res://game/assets/battlefields/{assetName}/background.png";
		if (ResourceLoader.Load<Texture2D>(path) is Texture2D texture)
		{
			return texture;
		}

		GD.PushWarning($"Battlefield '{battlefieldId}' has no background asset at '{path}'.");
		return null;
	}

	public override void _ExitTree()
	{
		if (_inputBindings is not null)
		{
			_inputBindings.BindingsChanged -= OnBindingsChanged;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (_completionRequested
			|| @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
		{
			return;
		}

		if (_gridDebugToggle.HasFocus()
			&& keyEvent.IsActionPressed(GameInputActions.Interact))
		{
			ToggleFormationGrid();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_battleLogDebugToggle.HasFocus()
			&& keyEvent.IsActionPressed(GameInputActions.Interact))
		{
			ToggleBattleLog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_showBattleLog
			&& (keyEvent.IsActionPressed(GameInputActions.Interact)
				|| keyEvent.IsActionPressed(GameInputActions.Menu)))
		{
			ToggleBattleLog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_phase is BattleInputPhase.Uninitialized or BattleInputPhase.Resolving)
		{
			return;
		}

		if (_phase == BattleInputPhase.Completed)
		{
			if (keyEvent.IsActionPressed(GameInputActions.Interact))
			{
				GetViewport().SetInputAsHandled();
				RequestCompletion();
			}

			return;
		}

		if (_phase is BattleInputPhase.Command or BattleInputPhase.MagicSelection)
		{
			if (_phase == BattleInputPhase.MagicSelection
				&& keyEvent.IsActionPressed(GameInputActions.Menu))
			{
				GetViewport().SetInputAsHandled();
				ReturnToTopLevelCommands();
				return;
			}

			if (keyEvent.IsActionPressed(GameInputActions.MoveLeft)
				|| keyEvent.IsActionPressed(GameInputActions.MoveUp))
			{
				GetViewport().SetInputAsHandled();
				CycleCommand(-1);
				return;
			}

			if (keyEvent.IsActionPressed(GameInputActions.MoveRight)
				|| keyEvent.IsActionPressed(GameInputActions.MoveDown))
			{
				GetViewport().SetInputAsHandled();
				CycleCommand(1);
				return;
			}

			if (keyEvent.IsActionPressed(GameInputActions.Interact))
			{
				GetViewport().SetInputAsHandled();
				ActivateFocusedCommand();
			}

			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.Menu))
		{
			GetViewport().SetInputAsHandled();
			CancelTargetSelection();
			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveLeft)
			|| keyEvent.IsActionPressed(GameInputActions.MoveUp))
		{
			GetViewport().SetInputAsHandled();
			CycleTarget(-1);
			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.MoveRight)
			|| keyEvent.IsActionPressed(GameInputActions.MoveDown))
		{
			GetViewport().SetInputAsHandled();
			CycleTarget(1);
			return;
		}

		if (keyEvent.IsActionPressed(GameInputActions.Interact))
		{
			GetViewport().SetInputAsHandled();
			ResolveSelectedAbility(_selectedTargetId);
		}
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is not InputEventMouseButton
			{
				Pressed: true,
				ButtonIndex: MouseButton.Left,
			} mouseButton
			)
		{
			return;
		}

		if (_battleLogDebugToggle.GetGlobalRect().HasPoint(mouseButton.Position))
		{
			ToggleBattleLog();
			_battleLogDebugToggle.GrabFocus();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (_showBattleLog && _battleLogCloseButton.GetGlobalRect().HasPoint(mouseButton.Position))
		{
			ToggleBattleLog();
			GetViewport().SetInputAsHandled();
			return;
		}

		if (!_gridDebugToggle.GetGlobalRect().HasPoint(mouseButton.Position))
		{
			return;
		}

		ToggleFormationGrid();
		_gridDebugToggle.GrabFocus();
		GetViewport().SetInputAsHandled();
	}

	private void CreateCombatantControls(CombatSnapshot snapshot)
	{
		foreach (CombatantSnapshot combatant in snapshot.Combatants)
		{
			var hpLabel = new Label
			{
				CustomMinimumSize = new Vector2(0.0f, 12.0f),
				HorizontalAlignment = HorizontalAlignment.Center,
			};

			hpLabel.AddThemeFontSizeOverride("font_size", 8);

			(combatant.Side == BattleSide.Party
				? _partyStatus
				: _enemyStatus).AddChild(hpLabel);

			_hpLabelByInstanceId.Add(combatant.InstanceId, hpLabel);

			string instanceId = combatant.InstanceId;

			var targetButton = new Button
			{
				CustomMinimumSize = new Vector2(52.0f, 16.0f),
			};

			targetButton.AddThemeFontSizeOverride("font_size", 7);
			targetButton.Pressed += () => SelectTargetAndResolve(instanceId);
			targetButton.FocusEntered += () => SelectFocusedTarget(instanceId);

			_targetButtons.AddChild(targetButton);
			_targetButtonByInstanceId.Add(instanceId, targetButton);
		}
	}

	private void ShowTopLevelCommands()
	{
		CombatantSnapshot partyActor = RequireSingleLivingPartyActor(RequireSnapshot());
		BattleCommandAvailability availability = RequireCommandAvailabilityResolver().Resolve(partyActor);
		_selectedMagicDisciplineId = null;
		ClearCommandButtons();

		foreach (string abilityId in availability.DirectAbilityIds)
		{
			AddCommandButton(ShortDefinitionName(abilityId), () => SelectAbility(abilityId));
		}

		foreach (MagicBattleCommandAvailability discipline in availability.MagicDisciplines)
		{
			string disciplineId = discipline.MagicDisciplineId;
			AddCommandButton(
				$"{ShortDefinitionName(disciplineId)} >",
				() => OpenMagicMenu(disciplineId),
				discipline.SpellAbilityIds.Count == 0);
		}

		RefreshPresentation();
		FocusFirstCommand();
	}

	private void OpenMagicMenu(string magicDisciplineId)
	{
		if (_phase != BattleInputPhase.Command)
		{
			return;
		}

		BattleCommandAvailability availability = RequireCommandAvailabilityResolver().Resolve(
			RequireSingleLivingPartyActor(RequireSnapshot()));
		MagicBattleCommandAvailability discipline = availability.MagicDisciplines
			.Single(candidate => string.Equals(
				candidate.MagicDisciplineId,
				magicDisciplineId,
				StringComparison.Ordinal));
		_selectedMagicDisciplineId = magicDisciplineId;
		_phase = BattleInputPhase.MagicSelection;
		ClearCommandButtons();
		foreach (string abilityId in discipline.SpellAbilityIds)
		{
			AddCommandButton(ShortDefinitionName(abilityId), () => SelectAbility(abilityId));
		}

		AddCommandButton("Back", ReturnToTopLevelCommands);
		RefreshPresentation();
		FocusFirstCommand();
	}

	private void ReturnToTopLevelCommands()
	{
		if (_phase != BattleInputPhase.MagicSelection)
		{
			return;
		}

		_phase = BattleInputPhase.Command;
		ShowTopLevelCommands();
	}

	private void AddCommandButton(
		string text,
		Action action,
		bool disabled = false)
	{
		var button = new Button
		{
			CustomMinimumSize = new Vector2(92.0f, 16.0f),
			Text = text,
			Disabled = disabled,
		};

		button.AddThemeFontSizeOverride("font_size", 7);
		button.Pressed += action;
		button.FocusEntered += () => _focusedCommandButton = button;

		_commandMenu.AddChild(button);
		_commandButtons.Add(button);
		_commandActionByButton.Add(button, action);
	}

	private void ClearCommandButtons()
	{
		foreach (Button button in _commandButtons)
		{
			_commandMenu.RemoveChild(button);
			button.QueueFree();
		}

		_commandButtons.Clear();
		_commandActionByButton.Clear();
		_focusedCommandButton = null;
	}

	private void FocusFirstCommand()
	{
		Button? first = _commandButtons.FirstOrDefault(button => !button.Disabled);
		if (first is not null)
		{
			first.GrabFocus();
		}
	}

	private void CycleCommand(int offset)
	{
		Button[] enabled = _commandButtons.Where(button => !button.Disabled).ToArray();
		if (enabled.Length == 0)
		{
			return;
		}

		int currentIndex = Array.IndexOf(enabled, _focusedCommandButton);
		int nextIndex = currentIndex < 0
			? 0
			: (currentIndex + offset + enabled.Length) % enabled.Length;
		enabled[nextIndex].GrabFocus();
	}

	private void ActivateFocusedCommand()
	{
		Button? focused = _focusedCommandButton;
		if (focused is not null && !focused.Disabled
			&& _commandActionByButton.TryGetValue(focused, out Action? action))
		{
			action();
		}
	}

	private void SelectAbility(string abilityId)
	{
		if (_phase is not (BattleInputPhase.Command or BattleInputPhase.MagicSelection))
		{
			return;
		}

		AbilityDefinition ability = RequireContent().GetRequired<AbilityDefinition>(abilityId);
		CombatantSnapshot partyActor = RequireSingleLivingPartyActor(RequireSnapshot());
		_selectedAbilityId = ability.Id;
		if (string.Equals(ability.TargetingId, AbilityTargetingIds.SingleEnemy, StringComparison.Ordinal))
		{
			BeginTargetSelection(BattleSide.Enemy);
			return;
		}

		if (string.Equals(ability.TargetingId, AbilityTargetingIds.SingleCombatant, StringComparison.Ordinal))
		{
			BeginTargetSelection(null);
			return;
		}

		if (string.Equals(ability.TargetingId, AbilityTargetingIds.SingleAlly, StringComparison.Ordinal))
		{
			BeginTargetSelection(BattleSide.Party);
			return;
		}

		if (string.Equals(ability.TargetingId, AbilityTargetingIds.Self, StringComparison.Ordinal))
		{
			ResolveSelectedAbility(partyActor.InstanceId);
			return;
		}

		throw new NotSupportedException(
			$"Battle command UI does not support targeting contract '{ability.TargetingId}'.");
	}

	private void BeginTargetSelection(BattleSide? targetSide)
	{
		if (_phase is not (BattleInputPhase.Command or BattleInputPhase.MagicSelection)
			|| _selectedAbilityId is null)
		{
			return;
		}

		string[] livingTargets = GetLivingTargetIds(targetSide);
		if (livingTargets.Length == 0)
		{
			throw new InvalidOperationException(
				"An in-progress battle must have at least one living legal target.");
		}

		_phase = BattleInputPhase.TargetSelection;
		_selectedTargetId = livingTargets[0];
		AppendLog(targetSide switch
		{
			BattleSide.Enemy => "Choose an enemy target.",
			BattleSide.Party => "Choose an ally target.",
			_ => "Choose a combatant target.",
		});
		RefreshPresentation();
	}

	private void CancelTargetSelection()
	{
		if (_phase != BattleInputPhase.TargetSelection)
		{
			return;
		}

		_selectedTargetId = null;
		_selectedAbilityId = null;
		if (_selectedMagicDisciplineId is null)
		{
			_phase = BattleInputPhase.Command;
			ShowTopLevelCommands();
			return;
		}

		_phase = BattleInputPhase.Command;
		OpenMagicMenu(_selectedMagicDisciplineId);
	}

	private void CycleTarget(int offset)
	{
		string[] livingTargets = GetLivingTargetIds(GetSelectedTargetSide());
		if (livingTargets.Length == 0)
		{
			return;
		}

		int currentIndex = Array.FindIndex(
			livingTargets,
			candidate => string.Equals(candidate, _selectedTargetId, StringComparison.Ordinal));
		int nextIndex = currentIndex < 0
			? 0
			: (currentIndex + offset + livingTargets.Length) % livingTargets.Length;
		_selectedTargetId = livingTargets[nextIndex];
		RefreshPresentation();
	}

	private void SelectFocusedTarget(string instanceId)
	{
		if (_phase == BattleInputPhase.TargetSelection
			&& IsLegalSelectedTarget(RequireSnapshot().GetRequiredCombatant(instanceId)))
		{
			_selectedTargetId = instanceId;
			RefreshPresentation();
		}
	}

	private void SelectTargetAndResolve(string instanceId)
	{
		if (_phase != BattleInputPhase.TargetSelection)
		{
			return;
		}

		CombatantSnapshot target = RequireSnapshot().GetRequiredCombatant(instanceId);
		if (!IsLegalSelectedTarget(target))
		{
			return;
		}

		_selectedTargetId = instanceId;
		ResolveSelectedAbility(instanceId);
	}

	private void ResolveSelectedAbility(string? targetId)
	{
		if (_selectedAbilityId is null || targetId is null)
		{
			return;
		}

		CombatSnapshot current = RequireSnapshot();
		CombatantSnapshot partyActor = CombatTimeline.SelectReadyActor(current, RequireContent());
		if (partyActor.Side != BattleSide.Party)
		{
			return;
		}
		AbilityDefinition ability = RequireContent().GetRequired<AbilityDefinition>(_selectedAbilityId);
		if (string.Equals(ability.TargetingId, AbilityTargetingIds.SingleEnemy, StringComparison.Ordinal))
		{
			if (_phase != BattleInputPhase.TargetSelection)
			{
				return;
			}

			CombatantSnapshot target = current.GetRequiredCombatant(targetId);
			if (target.Side != BattleSide.Enemy || target.IsDefeated)
			{
				throw new InvalidOperationException(
					$"Selected target '{target.InstanceId}' is not a living enemy.");
			}
		}
		else if (string.Equals(ability.TargetingId, AbilityTargetingIds.SingleCombatant, StringComparison.Ordinal))
		{
			if (_phase != BattleInputPhase.TargetSelection)
			{
				return;
			}

			if (current.GetRequiredCombatant(targetId).IsDefeated)
			{
				throw new InvalidOperationException(
					$"Selected target '{targetId}' is defeated.");
			}
		}
		else if (string.Equals(ability.TargetingId, AbilityTargetingIds.Self, StringComparison.Ordinal)
				 && !string.Equals(targetId, partyActor.InstanceId, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("A self-targeted ability must target its acting combatant.");
		}
		else if (string.Equals(ability.TargetingId, AbilityTargetingIds.SingleAlly, StringComparison.Ordinal))
		{
			if (_phase != BattleInputPhase.TargetSelection)
			{
				return;
			}

			CombatantSnapshot target = current.GetRequiredCombatant(targetId);
			if (target.Side != partyActor.Side || target.IsDefeated)
			{
				throw new InvalidOperationException(
					$"Selected target '{target.InstanceId}' is not a living ally.");
			}
		}

		_phase = BattleInputPhase.Resolving;
		RefreshPresentation();

		CombatResolution resolution = RequireTimelineResolver().ResolveNext(
			current,
			new CombatCommand(partyActor.InstanceId, ability.Id, [targetId]));
		CombatSnapshot next = resolution.Next;
		_snapshot = next;
		AppendEvents(resolution.Events);

		if (next.Outcome == BattleOutcome.InProgress)
		{
			_selectedAbilityId = null;
			_selectedMagicDisciplineId = null;
			_selectedTargetId = null;
			_phase = BattleInputPhase.Resolving;
			AdvanceToReadyActor();
			return;
		}

		BattleEnded ended = resolution.Events.OfType<BattleEnded>().SingleOrDefault()
			?? throw new InvalidDataException(
				"A terminal combat resolution did not contain BattleEnded.");
		if (ended.Outcome != next.Outcome)
		{
			throw new InvalidDataException(
				$"BattleEnded reported '{ended.Outcome}', but the replacement snapshot reports "
				+ $"'{next.Outcome}'.");
		}

		_selectedAbilityId = null;
		_selectedMagicDisciplineId = null;
		_selectedTargetId = null;
		_phase = BattleInputPhase.Completed;
		RefreshPresentation();
		_continueButton.GrabFocus();
	}

	private void AdvanceToReadyActor()
	{
		while (true)
		{
			CombatSnapshot current = RequireSnapshot();
			if (current.Outcome != BattleOutcome.InProgress)
			{
				_phase = BattleInputPhase.Completed;
				RefreshPresentation();
				_continueButton.GrabFocus();
				return;
			}

			CombatantSnapshot ready = CombatTimeline.SelectReadyActor(current, RequireContent());
			if (ready.Side == BattleSide.Party)
			{
				_phase = BattleInputPhase.Command;
				ShowTopLevelCommands();
				return;
			}

			_phase = BattleInputPhase.Resolving;
			RefreshPresentation();
			CombatCommand enemyCommand = RequireEnemyPlanner().Plan(current, ready.InstanceId);
			CombatResolution resolution = RequireTimelineResolver().ResolveNext(current, enemyCommand);
			_snapshot = resolution.Next;
			AppendEvents(resolution.Events);
		}
	}

	private void AppendEvents(IReadOnlyList<CombatEvent> events)
	{
		foreach (CombatEvent combatEvent in events)
		{
			switch (combatEvent)
			{
				case ResourceSpent spent:
					AppendLog(
						$"{DisplayName(spent.CombatantId)} spent {spent.Amount} MP for "
						+ $"{ShortDefinitionName(spent.AbilityId)} ({spent.PreviousValue} -> "
						+ $"{spent.CurrentValue} MP).");
					break;

				case HealingApplied healing:
					AppendLog(
						$"{DisplayName(healing.ActingCombatantId)} used "
						+ $"{ShortDefinitionName(healing.AbilityId)} on "
						+ $"{DisplayName(healing.TargetCombatantId)}: restored "
						+ $"{healing.Amount} HP ({healing.PreviousHp} -> "
						+ $"{healing.CurrentHp} HP).");
					break;

				case DamageApplied damage:
					string reaction = damage.DamagePercentModifier switch
					{
						-100 => " (immune)",
						< 0 => $" ({-(long)damage.DamagePercentModifier}% resisted)",
						> 0 => $" ({damage.DamagePercentModifier}% weakness)",
						_ => string.Empty,
					};
					AppendLog(
						$"{DisplayName(damage.ActingCombatantId)} used "
						+ $"{ShortDefinitionName(damage.AbilityId)} on "
						+ $"{DisplayName(damage.TargetCombatantId)}: {damage.Amount} "
						+ $"{ShortDefinitionName(damage.DamageTypeId)} damage{reaction} "
						+ $"({damage.PreviousHp} -> {damage.CurrentHp} HP).");
					break;

				case CombatantDefeated defeated:
					AppendLog($"{DisplayName(defeated.CombatantId)} was defeated.");
					break;

				case BattleEnded ended:
					AppendLog(ended.Outcome == BattleOutcome.PartyVictory
						? "Party victory."
						: "Party defeat.");
					break;

				case StatusApplied applied:
					AppendLog(
						$"{DisplayName(applied.TargetCombatantId)} gained "
						+ $"{ShortDefinitionName(applied.StatusEffectId)}.");
					break;

				case StatusRefreshed refreshed:
					AppendLog(
						$"{DisplayName(refreshed.TargetCombatantId)} refreshed "
						+ $"{ShortDefinitionName(refreshed.StatusEffectId)}.");
					break;

				case StatusIgnored ignored:
					AppendLog(
						$"{DisplayName(ignored.TargetCombatantId)} ignored "
						+ $"{ShortDefinitionName(ignored.StatusEffectId)}.");
					break;

				case StatusRemoved removed:
					AppendLog(
						$"{DisplayName(removed.TargetCombatantId)} lost "
						+ $"{ShortDefinitionName(removed.StatusEffectId)}.");
					break;

				case StatusExpired expired:
					AppendLog(
						$"{DisplayName(expired.TargetCombatantId)}'s "
						+ $"{ShortDefinitionName(expired.StatusEffectId)} expired.");
					break;

				default:
					throw new NotSupportedException(
						$"Battle presentation does not handle event "
						+ $"'{combatEvent.GetType().Name}'.");
			}
		}
	}

	private void RefreshPresentation()
	{
		if (_phase == BattleInputPhase.Uninitialized)
		{
			return;
		}

		CombatSnapshot snapshot = RequireSnapshot();
		_formationView.SetDefeatedCombatants(
			snapshot.Combatants
				.Where(combatant => combatant.IsDefeated)
				.Select(combatant => combatant.InstanceId));
		foreach (CombatantSnapshot combatant in snapshot.Combatants)
		{
			if (combatant.IsDefeated)
			{
				if (_hpLabelByInstanceId.Remove(combatant.InstanceId, out Label? defeatedLabel))
				{
					defeatedLabel.QueueFree();
				}

				continue;
			}

			_hpLabelByInstanceId[combatant.InstanceId].Text =
				combatant.Side == BattleSide.Enemy
					? $"{DisplayName(combatant.InstanceId)}\n"
						+ $"HP {combatant.CurrentHp}/{combatant.MaximumHp}"
					: $"{DisplayName(combatant.InstanceId)}\n"
						+ $"HP {combatant.CurrentHp}/{combatant.MaximumHp}  "
						+ $"MP {combatant.CurrentMp}/{combatant.MaximumMp}";

			if (_targetButtonByInstanceId.TryGetValue(
					combatant.InstanceId,
					out Button? targetButton))
			{
				targetButton.Text = DisplayName(combatant.InstanceId);
				targetButton.Disabled = combatant.IsDefeated
					|| _phase != BattleInputPhase.TargetSelection
					|| !IsLegalSelectedTarget(combatant);
			}
		}

		_commandMenu.Visible = _phase is BattleInputPhase.Command or BattleInputPhase.MagicSelection;
		_targetRow.Visible = false;
		_targetPrompt.Visible = false;
		_targetButtons.Visible = false;
		_formationView.SetTargetedCombatant(
			_phase == BattleInputPhase.TargetSelection ? _selectedTargetId : null);
		_formationView.SetHighlightedCombatant(_phase switch
		{
			BattleInputPhase.TargetSelection => _selectedTargetId,
			BattleInputPhase.Command or BattleInputPhase.MagicSelection =>
				CombatTimeline.SelectReadyActor(snapshot, RequireContent()).InstanceId,
			_ => null,
		});
		_continueButton.Visible = _phase == BattleInputPhase.Completed;
		_resultLabel.Visible = _phase == BattleInputPhase.Completed;

		_phaseLabel.Text = _phase switch
		{
			BattleInputPhase.Command => string.Empty,
			BattleInputPhase.MagicSelection =>
				$"{ShortDefinitionName(_selectedMagicDisciplineId ?? "magic")}: choose a spell.",
			BattleInputPhase.TargetSelection =>
				$"{ShortDefinitionName(_selectedAbilityId ?? "ability")}: choose a living "
				+ $"{TargetSelectionLabel()}.",
			BattleInputPhase.Resolving => "Resolving the next turn...",
			BattleInputPhase.Completed => "Battle ended.",
			_ => string.Empty,
		};
		_resultLabel.Text = snapshot.Outcome switch
		{
			BattleOutcome.PartyVictory => "Victory! Confirm to return to exploration.",
			BattleOutcome.PartyDefeat => "Defeat. Confirm to return to exploration.",
			_ => string.Empty,
		};

		RefreshInputHint();

		TurnOrderPreview preview = new TurnOrderPreviewService(RequireContent()).Create(snapshot);
		_turnOrderLabel.Text = "Turn Order: " + string.Join(
			"  >  ",
			preview.Entries.Select(entry => DisplayName(entry.CombatantInstanceId)));
	}

	private void RequestCompletion()
	{
		if (_phase != BattleInputPhase.Completed || _completionRequested)
		{
			return;
		}

		if (_showingVictoryRewards)
		{
			_completionRequested = true;
			_continueButton.Disabled = true;
			SetProcessUnhandledInput(false);
			VictoryRewardsContinueRequested?.Invoke(this, EventArgs.Empty);
			return;
		}

		CombatSnapshot snapshot = RequireSnapshot();
		_completionRequested = true;
		SetProcessUnhandledInput(false);
		_continueButton.Disabled = true;
		CompletionRequested?.Invoke(
			this,
			new BattleCompletionRequestedEventArgs(
				BattleCompletionRequest.FromFinalSnapshot(
					_encounterId
						?? throw new InvalidOperationException("Battle has no encounter ID."),
					snapshot)));
	}

	private void OnBindingsChanged(object? sender, EventArgs eventArgs) => RefreshInputHint();

	private void RefreshInputHint()
	{
		InputBindingService bindings = RequireInputBindings();
		_inputHint.Text = _phase switch
		{
			BattleInputPhase.Command =>
				$"Choose a command with movement; confirm "
				+ $"[{bindings.FormatBindings(GameInputActions.Interact)}].",
			BattleInputPhase.MagicSelection =>
				$"Choose a spell with movement; confirm "
				+ $"[{bindings.FormatBindings(GameInputActions.Interact)}], back "
				+ $"[{bindings.FormatBindings(GameInputActions.Menu)}].",
			BattleInputPhase.TargetSelection =>
				$"Change target with movement; confirm "
				+ $"[{bindings.FormatBindings(GameInputActions.Interact)}], cancel "
				+ $"[{bindings.FormatBindings(GameInputActions.Menu)}].",
			BattleInputPhase.Completed =>
				$"Continue [{bindings.FormatBindings(GameInputActions.Interact)}].",
			_ => string.Empty,
		};
	}

	private string[] GetLivingTargetIds(BattleSide? side) => RequireSnapshot().Combatants
		.Where(combatant => (side is null || combatant.Side == side) && !combatant.IsDefeated)
		.OrderBy(combatant => combatant.Side == BattleSide.Party ? 1 : 0)
		.Select(combatant => combatant.InstanceId)
		.ToArray();

	private BattleSide? GetSelectedTargetSide()
	{
		AbilityDefinition ability = RequireContent().GetRequired<AbilityDefinition>(
			_selectedAbilityId
				?? throw new InvalidOperationException("No ability is selected for targeting."));
		return ability.TargetingId switch
		{
			AbilityTargetingIds.SingleEnemy => BattleSide.Enemy,
			AbilityTargetingIds.SingleAlly => BattleSide.Party,
			AbilityTargetingIds.SingleCombatant => null,
			_ => throw new InvalidOperationException(
				$"Ability '{ability.Id}' does not use an explicit target selection."),
		};
	}

	private bool IsLegalSelectedTarget(CombatantSnapshot combatant)
	{
		if (combatant.IsDefeated)
		{
			return false;
		}

		BattleSide? targetSide = GetSelectedTargetSide();
		return targetSide is null || combatant.Side == targetSide;
	}

	private string TargetSelectionLabel() => GetSelectedTargetSide() switch
	{
		BattleSide.Enemy => "enemy",
		BattleSide.Party => "ally",
		_ => "combatant",
	};

	private string DisplayName(string instanceId) => _formationView.GetDisplayLabel(instanceId);

	private void AppendLog(string line)
	{
		PersistentBattleLogLines.Add(line);
		_eventLog.AppendText(line + System.Environment.NewLine);
	}

	private static CombatantSnapshot RequireSingleLivingPartyActor(CombatSnapshot snapshot)
	{
		CombatantSnapshot[] livingParty = snapshot.Combatants
			.Where(combatant => combatant.Side == BattleSide.Party && !combatant.IsDefeated)
			.ToArray();
		return livingParty.Length == 1
			? livingParty[0]
			: throw new InvalidOperationException(
				$"The first playable battle supports exactly one living party combatant; "
				+ $"found {livingParty.Length}.");
	}

	private static string ShortDefinitionName(string definitionId)
	{
		int start = definitionId.LastIndexOf('.') + 1;
		string shortName = start <= 0 || start >= definitionId.Length
			? definitionId
			: definitionId[start..];
		return string.Join(
			" ",
			shortName.Split('-', StringSplitOptions.RemoveEmptyEntries)
				.Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
	}

	private CombatSnapshot RequireSnapshot() => _snapshot
		?? throw new InvalidOperationException("Battle scene is not initialized.");

	private IContentCatalog RequireContent() => _content
		?? throw new InvalidOperationException("Battle scene is not initialized.");

	private BattleCommandAvailabilityResolver RequireCommandAvailabilityResolver() => _commandAvailabilityResolver
		?? throw new InvalidOperationException("Battle scene is not initialized.");

	private ICombatTimelineResolver RequireTimelineResolver() => _timelineResolver
		?? throw new InvalidOperationException("Battle scene is not initialized.");

	private IEnemyCommandPlanner RequireEnemyPlanner() => _enemyPlanner
		?? throw new InvalidOperationException("Battle scene is not initialized.");

	private InputBindingService RequireInputBindings() => _inputBindings
		?? throw new InvalidOperationException("Battle scene is not initialized.");

	private enum BattleInputPhase
	{
		Uninitialized,
		Command,
		MagicSelection,
		TargetSelection,
		Resolving,
		Completed,
	}
}
