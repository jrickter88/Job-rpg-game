using Godot;
using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using RpgGame.Input;

namespace RpgGame.Encounters;

/// <summary>
/// Collects the first playable battle's Attack choice and presents authoritative core results.
/// </summary>
/// <remarks>
/// This Godot controller owns buttons, focus, labels, and event-log text. It never calculates
/// damage, changes HP directly, orders actions, or decides victory. Those facts arrive through
/// <see cref="ICombatRoundResolver"/>, replacement <see cref="CombatSnapshot"/> instances, and
/// typed <see cref="CombatEvent"/> values. The controller also receives no GameSession, so it
/// cannot accidentally clear an encounter before GameRoot handles a confirmed terminal result.
/// </remarks>
public partial class BattleController : Control
{
    private const string AttackAbilityId = "ability.command.attack";

    private Label _encounterLabel = null!;
    private Label _battlefieldLabel = null!;
    private BattleFormationView _formationView = null!;
    private VBoxContainer _partyStatus = null!;
    private VBoxContainer _enemyStatus = null!;
    private Label _phaseLabel = null!;
    private Button _attackButton = null!;
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

    private InputBindingService? _inputBindings;
    private ICombatRoundResolver? _roundResolver;
    private IEnemyCommandPlanner? _enemyPlanner;
    private CombatSnapshot? _snapshot;
    private string? _encounterId;
    private string? _selectedTargetId;
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
        _attackButton = GetNode<Button>("Margin/VBox/CommandArea/CommandRow/Attack");
        _targetPrompt = GetNode<Label>("Margin/VBox/CommandArea/CommandRow/TargetPrompt");
        _targetButtons = GetNode<HBoxContainer>(
            "Margin/VBox/CommandArea/CommandRow/Targets");
        _eventLog = GetNode<RichTextLabel>("Margin/VBox/EventLog");
        _resultLabel = GetNode<Label>("Margin/VBox/ResultRow/Result");
        _continueButton = GetNode<Button>("Margin/VBox/ResultRow/Continue");
        _inputHint = GetNode<Label>("Margin/VBox/InputHint");

        _attackButton.Pressed += BeginTargetSelection;
        _continueButton.Pressed += RequestCompletion;
        SetProcessUnhandledInput(false);
    }

    /// <summary>
    /// Injects the transient state and pure rule boundaries after scene instantiation.
    /// </summary>
    public void Initialize(
        EncounterDefinition encounter,
        CombatSnapshot initialSnapshot,
        ICombatRoundResolver roundResolver,
        IEnemyCommandPlanner enemyPlanner,
        InputBindingService inputBindings)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(initialSnapshot);
        ArgumentNullException.ThrowIfNull(roundResolver);
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

        CombatantSnapshot partyActor = RequireSingleLivingPartyActor(initialSnapshot);
        if (!partyActor.AbilityIds.Contains(AttackAbilityId, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                $"Party combatant '{partyActor.InstanceId}' does not own required command "
                + $"'{AttackAbilityId}'.");
        }

        _encounterId = encounter.Id;
        _snapshot = initialSnapshot;
        _roundResolver = roundResolver;
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
                .ToArray());

        CreateCombatantControls(initialSnapshot);
        AppendLog($"Battle started. Round {initialSnapshot.Round}.");
        _phase = BattleInputPhase.Command;
        RefreshPresentation();
        _attackButton.GrabFocus();
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

        if (_phase == BattleInputPhase.Command)
        {
            if (keyEvent.IsActionPressed(GameInputActions.Interact))
            {
                GetViewport().SetInputAsHandled();
                BeginTargetSelection();
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
            ResolveSelectedAttack();
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

            if (combatant.Side != BattleSide.Enemy)
            {
                continue;
            }

            string instanceId = combatant.InstanceId;
            var targetButton = new Button
            {
                CustomMinimumSize = new Vector2(150.0f, 34.0f),
            };
            targetButton.Pressed += () => SelectTargetAndResolve(instanceId);
            targetButton.FocusEntered += () => SelectFocusedTarget(instanceId);
            _targetButtons.AddChild(targetButton);
            _targetButtonByInstanceId.Add(instanceId, targetButton);
        }
    }

    private void BeginTargetSelection()
    {
        if (_phase != BattleInputPhase.Command)
        {
            return;
        }

        string[] livingTargets = GetLivingEnemyIds();
        if (livingTargets.Length == 0)
        {
            throw new InvalidOperationException(
                "An in-progress battle must have at least one living enemy target.");
        }

        _phase = BattleInputPhase.TargetSelection;
        string selectedTargetId = livingTargets[0];
        _selectedTargetId = selectedTargetId;
        AppendLog("Choose an enemy target.");
        RefreshPresentation();
        _targetButtonByInstanceId[selectedTargetId].GrabFocus();
    }

    private void CancelTargetSelection()
    {
        if (_phase != BattleInputPhase.TargetSelection)
        {
            return;
        }

        _selectedTargetId = null;
        _phase = BattleInputPhase.Command;
        RefreshPresentation();
        _attackButton.GrabFocus();
    }

    private void CycleTarget(int offset)
    {
        string[] livingTargets = GetLivingEnemyIds();
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
        string selectedTargetId = livingTargets[nextIndex];
        _selectedTargetId = selectedTargetId;
        RefreshPresentation();
        _targetButtonByInstanceId[selectedTargetId].GrabFocus();
    }

    private void SelectFocusedTarget(string instanceId)
    {
        if (_phase == BattleInputPhase.TargetSelection
            && !RequireSnapshot().GetRequiredCombatant(instanceId).IsDefeated)
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

        _selectedTargetId = instanceId;
        ResolveSelectedAttack();
    }

    private void ResolveSelectedAttack()
    {
        if (_phase != BattleInputPhase.TargetSelection || _selectedTargetId is null)
        {
            return;
        }

        CombatSnapshot current = RequireSnapshot();
        CombatantSnapshot partyActor = RequireSingleLivingPartyActor(current);
        CombatantSnapshot target = current.GetRequiredCombatant(_selectedTargetId);
        if (target.Side != BattleSide.Enemy || target.IsDefeated)
        {
            throw new InvalidOperationException(
                $"Selected target '{target.InstanceId}' is not a living enemy.");
        }

        _phase = BattleInputPhase.Resolving;
        RefreshPresentation();

        var commands = new List<CombatCommand>
        {
            new(partyActor.InstanceId, AttackAbilityId, [target.InstanceId]),
        };
        commands.AddRange(current.Combatants
            .Where(combatant => combatant.Side == BattleSide.Enemy && !combatant.IsDefeated)
            .Select(combatant => RequireEnemyPlanner().Plan(current, combatant.InstanceId)));

        CombatResolution resolution = RequireRoundResolver().ResolveRound(current, commands);
        CombatSnapshot next = resolution.Next;
        _snapshot = next;
        AppendEvents(resolution.Events);

        if (next.Outcome == BattleOutcome.InProgress)
        {
            _selectedTargetId = null;
            _phase = BattleInputPhase.Command;
            RefreshPresentation();
            _attackButton.GrabFocus();
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

        _selectedTargetId = null;
        _phase = BattleInputPhase.Completed;
        RefreshPresentation();
        _continueButton.GrabFocus();
    }

    private void AppendEvents(IReadOnlyList<CombatEvent> events)
    {
        foreach (CombatEvent combatEvent in events)
        {
            switch (combatEvent)
            {
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
                        + $"({damage.PreviousHp} → {damage.CurrentHp} HP).");
                    break;

                case CombatantDefeated defeated:
                    AppendLog($"{DisplayName(defeated.CombatantId)} was defeated.");
                    break;

                case BattleEnded ended:
                    AppendLog(ended.Outcome == BattleOutcome.PartyVictory
                        ? "Party victory."
                        : "Party defeat.");
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
            string defeatedSuffix = combatant.IsDefeated ? " — defeated" : string.Empty;
            _hpLabelByInstanceId[combatant.InstanceId].Text =
                $"{DisplayName(combatant.InstanceId)}: "
                + $"{combatant.CurrentHp}/{combatant.MaximumHp} HP{defeatedSuffix}";

            if (_targetButtonByInstanceId.TryGetValue(
                    combatant.InstanceId,
                    out Button? targetButton))
            {
                targetButton.Text =
                    $"{DisplayName(combatant.InstanceId)} "
                    + $"{combatant.CurrentHp}/{combatant.MaximumHp}";
                targetButton.Disabled = combatant.IsDefeated
                    || _phase != BattleInputPhase.TargetSelection;
            }
        }

        _attackButton.Disabled = _phase != BattleInputPhase.Command;
        _targetPrompt.Visible = _phase == BattleInputPhase.TargetSelection;
        _targetButtons.Visible = _phase == BattleInputPhase.TargetSelection;
        _continueButton.Visible = _phase == BattleInputPhase.Completed;
        _resultLabel.Visible = _phase == BattleInputPhase.Completed;

        _phaseLabel.Text = _phase switch
        {
            BattleInputPhase.Command => $"Round {snapshot.Round}: choose a command.",
            BattleInputPhase.TargetSelection => "Attack: choose a living enemy.",
            BattleInputPhase.Resolving => "Resolving the round...",
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
                $"Choose Attack, then confirm "
                + $"[{bindings.FormatBindings(GameInputActions.Interact)}].",
            BattleInputPhase.TargetSelection =>
                $"Change target with movement; confirm "
                + $"[{bindings.FormatBindings(GameInputActions.Interact)}], cancel "
                + $"[{bindings.FormatBindings(GameInputActions.Menu)}].",
            BattleInputPhase.Completed =>
                $"Continue [{bindings.FormatBindings(GameInputActions.Interact)}].",
            _ => string.Empty,
        };
    }

    private string[] GetLivingEnemyIds() => RequireSnapshot().Combatants
        .Where(combatant => combatant.Side == BattleSide.Enemy && !combatant.IsDefeated)
        .Select(combatant => combatant.InstanceId)
        .ToArray();

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

    private ICombatRoundResolver RequireRoundResolver() => _roundResolver
        ?? throw new InvalidOperationException("Battle scene is not initialized.");

    private IEnemyCommandPlanner RequireEnemyPlanner() => _enemyPlanner
        ?? throw new InvalidOperationException("Battle scene is not initialized.");

    private InputBindingService RequireInputBindings() => _inputBindings
        ?? throw new InvalidOperationException("Battle scene is not initialized.");

    private enum BattleInputPhase
    {
        Uninitialized,
        Command,
        TargetSelection,
        Resolving,
        Completed,
    }
}
