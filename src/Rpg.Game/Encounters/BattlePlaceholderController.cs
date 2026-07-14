using Godot;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using RpgGame.Input;

namespace RpgGame.Encounters;

/// <summary>
/// Displays one validated encounter and requests a return without simulating combat.
/// </summary>
/// <remarks>
/// This screen deliberately receives no GameSession. It cannot change HP, rewards, flags,
/// or any other campaign fact. Its only jobs are to present the encounter and validated
/// formation placements passed by GameRoot, then translate existing logical input actions
/// into a typed return request.
/// Milestone 3 will replace this proof screen with an actual battle presentation.
/// </remarks>
public partial class BattlePlaceholderController : Control
{
    private Label _encounterLabel = null!;
    private Label _battlefieldLabel = null!;
    private BattleFormationView _formationView = null!;
    private Label _formationDetailsLabel = null!;
    private Label _returnHintLabel = null!;
    private InputBindingService? _inputBindings;
    private string? _encounterId;
    private bool _returnRequested;

    /// <summary>Asks GameRoot to reconstruct exploration from its existing session.</summary>
    public event EventHandler<EncounterReturnRequestedEventArgs>? ReturnRequested;

    public override void _Ready()
    {
        _encounterLabel = GetNode<Label>("Margin/VBox/EncounterId");
        _battlefieldLabel = GetNode<Label>("Margin/VBox/BattlefieldId");
        _formationView = GetNode<BattleFormationView>("Margin/VBox/FormationView");
        _formationDetailsLabel = GetNode<Label>("Margin/VBox/FormationDetails");
        _returnHintLabel = GetNode<Label>("Margin/VBox/ReturnHint");
        SetProcessUnhandledInput(false);
    }

    /// <summary>
    /// Supplies the resolved encounter, validated transient placements, and control profile.
    /// </summary>
    public void Initialize(
        EncounterDefinition encounter,
        IReadOnlyList<FormationPlacement> enemyPlacements,
        IReadOnlyList<FormationPlacement> partyPlacements,
        InputBindingService inputBindings)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        ArgumentNullException.ThrowIfNull(enemyPlacements);
        ArgumentNullException.ThrowIfNull(partyPlacements);
        ArgumentNullException.ThrowIfNull(inputBindings);
        if (_inputBindings is not null)
        {
            throw new InvalidOperationException("Battle placeholder is already initialized.");
        }

        _encounterId = encounter.Id;
        _inputBindings = inputBindings;
        _inputBindings.BindingsChanged += OnBindingsChanged;

        _encounterLabel.Text = $"Encounter ID: {encounter.Id}";
        _battlefieldLabel.Text =
            $"Battlefield ID: {encounter.BattlefieldId ?? "(none)"}";
        _formationView.Initialize(enemyPlacements, partyPlacements);
        _formationDetailsLabel.Text = FormatFormationDetails(
            enemyPlacements,
            partyPlacements);
        RefreshReturnHint();
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
        if (_returnRequested
            || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent
            || (!keyEvent.IsActionPressed(GameInputActions.Interact)
                && !keyEvent.IsActionPressed(GameInputActions.Menu)))
        {
            return;
        }

        // We inspect only stable logical actions—not a Key value—so remapped confirmation
        // and cancellation work here immediately. The guard prevents a duplicated input
        // event from requesting more than one scene replacement.
        _returnRequested = true;
        SetProcessUnhandledInput(false);
        GetViewport().SetInputAsHandled();
        string encounterId = _encounterId
            ?? throw new InvalidOperationException("Battle placeholder is not initialized.");
        ReturnRequested?.Invoke(
            this,
            new EncounterReturnRequestedEventArgs(new EncounterReturnRequest(encounterId)));
    }

    private void OnBindingsChanged(object? sender, EventArgs eventArgs) => RefreshReturnHint();

    private void RefreshReturnHint()
    {
        InputBindingService bindings = _inputBindings
            ?? throw new InvalidOperationException("Battle placeholder is not initialized.");
        _returnHintLabel.Text =
            $"Return with Interact / Confirm [{bindings.FormatBindings(GameInputActions.Interact)}]"
            + System.Environment.NewLine
            + $"or Menu / Cancel [{bindings.FormatBindings(GameInputActions.Menu)}].";
    }

    private static string FormatFormationDetails(
        IReadOnlyList<FormationPlacement> enemyPlacements,
        IReadOnlyList<FormationPlacement> partyPlacements)
    {
        IReadOnlyDictionary<string, string> enemyLabels = BuildDisplayLabels(enemyPlacements);
        IReadOnlyDictionary<string, string> partyLabels = BuildDisplayLabels(partyPlacements);

        IEnumerable<string> enemyLines = enemyPlacements.Select(placement =>
            $"{enemyLabels[placement.InstanceId]}: {placement.DefinitionId} @ "
            + $"{FormationSlotId.Format(placement.Anchor)}, "
            + $"{placement.Footprint.Rows}×{placement.Footprint.Columns}");
        IEnumerable<string> partyLines = partyPlacements.Select(placement =>
            $"{partyLabels[placement.InstanceId]}: {placement.DefinitionId} @ "
            + FormationSlotId.Format(placement.Anchor));

        return "Validated placements: "
            + string.Join("    ", enemyLines.Concat(partyLines));
    }

    private static IReadOnlyDictionary<string, string> BuildDisplayLabels(
        IReadOnlyList<FormationPlacement> placements)
    {
        Dictionary<string, int> totalByDefinitionId = placements
            .GroupBy(placement => placement.DefinitionId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Count(),
                StringComparer.Ordinal);
        var seenByDefinitionId = new Dictionary<string, int>(StringComparer.Ordinal);
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (FormationPlacement placement in placements)
        {
            string shortName = ShortDefinitionName(placement.DefinitionId);
            int seenCount = seenByDefinitionId.GetValueOrDefault(placement.DefinitionId) + 1;
            seenByDefinitionId[placement.DefinitionId] = seenCount;
            labels.Add(
                placement.InstanceId,
                totalByDefinitionId[placement.DefinitionId] > 1
                    ? $"{shortName} #{seenCount}"
                    : shortName);
        }

        return labels;
    }

    private static string ShortDefinitionName(string definitionId)
    {
        int start = definitionId.LastIndexOf('.') + 1;
        return start <= 0 || start >= definitionId.Length
            ? definitionId
            : definitionId[start..];
    }
}
