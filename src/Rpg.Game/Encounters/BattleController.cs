using Godot;
using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
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

    /// <summary>
    /// Raised only after the player confirms a core-authored victory or defeat outcome.
    /// </summary>
    public event EventHandler<BattleCompletionRequestedEventArgs>? CompletionRequested;

    public override void _Ready()
    {
        _encounterLabel = GetNode<Label>("Margin/VBox/EncounterId");
        _battlefieldLabel = GetNode<Label>("Margin/VBox/BattlefieldId");
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
        SetProcessUnhandledInput(false);
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

    public override void _ExitTree()
    {
        if (_inputBindings is not null)
        {
            _inputBindings.BindingsChanged -= OnBindingsChanged;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_phase is BattleInputPhase.Uninitialized or BattleInputPhase.Resolving
            || _completionRequested
            || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
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

    private void CreateCombatantControls(CombatSnapshot snapshot)
    {
        foreach (CombatantSnapshot combatant in snapshot.Combatants)
        {
            var hpLabel = new Label();
            hpLabel.AddThemeFontSizeOverride("font_size", 15);
            (combatant.Side == BattleSide.Party ? _partyStatus : _enemyStatus).AddChild(hpLabel);
            _hpLabelByInstanceId.Add(combatant.InstanceId, hpLabel);

            string instanceId = combatant.InstanceId;
            var targetButton = new Button
            {
                CustomMinimumSize = new Vector2(110.0f, 28.0f),
            };
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

    private void AddCommandButton(string text, Action action, bool disabled = false)
    {
        var button = new Button
        {
            CustomMinimumSize = new Vector2(150.0f, 28.0f),
            Text = text,
            Disabled = disabled,
        };
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

    private void BeginTargetSelection(BattleSide targetSide)
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
                $"An in-progress battle must have at least one living {targetSide} target.");
        }

        _phase = BattleInputPhase.TargetSelection;
        _selectedTargetId = livingTargets[0];
        AppendLog(targetSide == BattleSide.Enemy
            ? "Choose an enemy target."
            : "Choose an ally target.");
        RefreshPresentation();
        _targetButtonByInstanceId[_selectedTargetId].GrabFocus();
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
        string[] livingTargets = GetLivingTargetIds(RequireSelectedTargetSide());
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
        _targetButtonByInstanceId[_selectedTargetId].GrabFocus();
    }

    private void SelectFocusedTarget(string instanceId)
    {
        if (_phase == BattleInputPhase.TargetSelection
            && !RequireSnapshot().GetRequiredCombatant(instanceId).IsDefeated
            && RequireSnapshot().GetRequiredCombatant(instanceId).Side == RequireSelectedTargetSide())
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
        if (target.IsDefeated)
        {
            return;
        }

        if (target.Side != RequireSelectedTargetSide())
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
        foreach (CombatantSnapshot combatant in snapshot.Combatants)
        {
            string defeatedSuffix = combatant.IsDefeated ? " - defeated" : string.Empty;
            _hpLabelByInstanceId[combatant.InstanceId].Text =
                $"{DisplayName(combatant.InstanceId)}: "
                + $"{combatant.CurrentHp}/{combatant.MaximumHp} HP | "
                + $"{combatant.CurrentMp}/{combatant.MaximumMp} MP{defeatedSuffix}";

            if (_targetButtonByInstanceId.TryGetValue(
                    combatant.InstanceId,
                    out Button? targetButton))
            {
                targetButton.Text =
                    $"{DisplayName(combatant.InstanceId)} "
                    + $"{combatant.CurrentHp}/{combatant.MaximumHp}";
                targetButton.Disabled = combatant.IsDefeated
                    || _phase != BattleInputPhase.TargetSelection
                    || combatant.Side != RequireSelectedTargetSideOrDefault();
            }
        }

        _commandMenu.Visible = _phase is BattleInputPhase.Command or BattleInputPhase.MagicSelection;
        _targetRow.Visible = _phase == BattleInputPhase.TargetSelection;
        _targetPrompt.Visible = _phase == BattleInputPhase.TargetSelection;
        _targetButtons.Visible = _phase == BattleInputPhase.TargetSelection;
        _continueButton.Visible = _phase == BattleInputPhase.Completed;
        _resultLabel.Visible = _phase == BattleInputPhase.Completed;

        _phaseLabel.Text = _phase switch
        {
            BattleInputPhase.Command => $"{DisplayName(CombatTimeline.SelectReadyActor(snapshot, RequireContent()).InstanceId)}'s turn: choose a command.",
            BattleInputPhase.MagicSelection =>
                $"{ShortDefinitionName(_selectedMagicDisciplineId ?? "magic")}: choose a spell.",
            BattleInputPhase.TargetSelection =>
                $"{ShortDefinitionName(_selectedAbilityId ?? "ability")}: choose a living "
                + $"{TargetSideLabel(RequireSelectedTargetSide())}.",
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

    private string[] GetLivingTargetIds(BattleSide side) => RequireSnapshot().Combatants
        .Where(combatant => combatant.Side == side && !combatant.IsDefeated)
        .Select(combatant => combatant.InstanceId)
        .ToArray();

    private BattleSide RequireSelectedTargetSide()
    {
        AbilityDefinition ability = RequireContent().GetRequired<AbilityDefinition>(
            _selectedAbilityId
                ?? throw new InvalidOperationException("No ability is selected for targeting."));
        return ability.TargetingId switch
        {
            AbilityTargetingIds.SingleEnemy => BattleSide.Enemy,
            AbilityTargetingIds.SingleAlly => BattleSide.Party,
            _ => throw new InvalidOperationException(
                $"Ability '{ability.Id}' does not use an explicit target selection."),
        };
    }

    private BattleSide RequireSelectedTargetSideOrDefault() =>
        _selectedAbilityId is null ? BattleSide.Enemy : RequireSelectedTargetSide();

    private static string TargetSideLabel(BattleSide side) => side == BattleSide.Enemy
        ? "enemy"
        : "ally";

    private string DisplayName(string instanceId) => _formationView.GetDisplayLabel(instanceId);

    private void AppendLog(string line)
    {
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
