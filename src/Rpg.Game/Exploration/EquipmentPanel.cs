using Godot;
using RpgGame.Core.Combat;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Equipment;
using RpgGame.Core.State;
using RpgGame.Input;

namespace RpgGame.Exploration;

/// <summary>Disposable exploration UI for the authoritative persistent equipment state.</summary>
public partial class EquipmentPanel : PanelContainer
{
    private Label _title = null!;
    private Label _statistics = null!;
    private Label _status = null!;
    private VBoxContainer _actions = null!;
    private IContentCatalog? _content;
    private IGameSession? _session;
    private EquipmentMenuProjectionResolver? _projectionResolver;
    private EquipmentService? _equipmentService;
    private readonly List<Button> _actionButtons = [];
    private readonly Dictionary<Button, Action> _actionByButton = [];
    private Button? _focusedButton;
    private string? _actorId;
    private string? _selectedSlotId;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        _title = GetNode<Label>("Margin/VBox/Title");
        _statistics = GetNode<Label>("Margin/VBox/Statistics");
        _status = GetNode<Label>("Margin/VBox/Status");
        _actions = GetNode<VBoxContainer>("Margin/VBox/Actions");
        Visible = false;
    }

    public void Initialize(IContentCatalog content, IGameSession session)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(session);
        if (_session is not null)
        {
            throw new InvalidOperationException("EquipmentPanel is already initialized.");
        }

        _content = content;
        _session = session;
        _projectionResolver = new EquipmentMenuProjectionResolver(content);
        _equipmentService = new EquipmentService(content, session);
        _session.StateChanged += OnSessionStateChanged;
    }

    public override void _ExitTree()
    {
        if (_session is not null)
        {
            _session.StateChanged -= OnSessionStateChanged;
        }
    }

    public void Open()
    {
        IGameSession session = RequireSession();
        _actorId = session.Current.ActivePartyActorIds.FirstOrDefault()
            ?? throw new InvalidOperationException("The active party has no actor to equip.");
        _selectedSlotId = null;
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        _selectedSlotId = null;
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.Menu))
        {
            if (_selectedSlotId is null)
            {
                Close();
            }
            else
            {
                _selectedSlotId = null;
                Refresh();
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.MoveUp))
        {
            CycleFocus(-1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.MoveDown))
        {
            CycleFocus(1);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.Interact)
            && _focusedButton is not null
            && _actionByButton.TryGetValue(_focusedButton, out Action? action))
        {
            action();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        string actorId = _actorId ?? throw new InvalidOperationException("Equipment actor is missing.");
        EquipmentMenuProjection projection = RequireProjectionResolver().Resolve(
            RequireSession().Current,
            actorId);
        _title.Text = $"Equipment - {DisplayActorName(actorId)}";
        _statistics.Text = BuildStatistics(actorId);
        SetStatus(_selectedSlotId is null
            ? "Select a slot. Menu / Cancel closes equipment."
            : "Select equipment, Unequip, or Back.");

        if (_selectedSlotId is null)
        {
            RenderSlots(projection);
            FocusFirstAction();
            return;
        }

        EquipmentSlotProjection slot = projection.Slots.Single(candidate =>
            string.Equals(candidate.SlotId, _selectedSlotId, StringComparison.Ordinal));
        RenderSlotChoices(slot);
        FocusFirstAction();
    }

    private void RenderSlots(EquipmentMenuProjection projection)
    {
        ClearActions();
        foreach (EquipmentSlotProjection slot in projection.Slots)
        {
            string slotId = slot.SlotId;
            AddActionButton(
                $"{DisplaySlotName(slotId)}: {DisplayItemName(slot.EquippedItemId)}",
                () =>
                {
                    _selectedSlotId = slotId;
                    Refresh();
                });
        }

        AddActionButton("Back", Close);
    }

    private void RenderSlotChoices(EquipmentSlotProjection slot)
    {
        ClearActions();
        _title.Text = $"Equipment - {DisplayActorName(_actorId!)} - {DisplaySlotName(slot.SlotId)}";
        if (slot.EquippedItemId is not null)
        {
            AddActionButton("Unequip", () => TryUnequip(slot.SlotId));
        }

        if (slot.CompatibleOwnedItemIds.Count == 0)
        {
            SetStatus("No compatible equipment owned for this slot.");
        }

        foreach (string itemId in slot.CompatibleOwnedItemIds)
        {
            string capturedItemId = itemId;
            AddActionButton(DisplayItemName(itemId), () => TryEquip(capturedItemId, slot.SlotId));
        }

        AddActionButton("Back", () =>
        {
            _selectedSlotId = null;
            Refresh();
        });
    }

    private void TryEquip(string itemId, string slotId)
    {
        try
        {
            RequireEquipmentService().EquipItem(_actorId!, itemId, slotId);
            SetStatus($"Equipped {DisplayItemName(itemId)}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Equipment change failed: {exception.Message}", isError: true);
        }
    }

    private void TryUnequip(string slotId)
    {
        try
        {
            RequireEquipmentService().UnequipItem(_actorId!, slotId);
            SetStatus($"Unequipped {DisplaySlotName(slotId)}.");
        }
        catch (Exception exception)
        {
            SetStatus($"Equipment change failed: {exception.Message}", isError: true);
        }
    }

    private string BuildStatistics(string actorId)
    {
        ActorProgressState progress = RequireSession().Current.ActorProgress[actorId];
        IReadOnlyDictionary<string, int> statistics = new CombatStatisticResolver(RequireContent())
            .ResolvePartyActor(progress);
        int weaponAttack = ResolveWeaponAttack(progress);
        return $"Strength: {statistics[CombatStatisticIds.Strength]}    "
            + $"Defense: {statistics[CombatStatisticIds.Defense]}    "
            + $"Weapon Attack: {weaponAttack}{System.Environment.NewLine}"
            + $"Max HP: {statistics[CombatStatisticIds.MaxHp]}    "
            + $"Max MP: {statistics.GetValueOrDefault(CombatStatisticIds.MaxMp)}";
    }

    private int ResolveWeaponAttack(ActorProgressState progress)
    {
        if (!progress.EquippedItems.TryGetValue(
                EquipmentSlotIds.MainHandWeapon,
                out string? itemId))
        {
            return 0;
        }

        return RequireContent().GetAll<EquipmentDefinition>()
            .Single(equipment => string.Equals(equipment.ItemId, itemId, StringComparison.Ordinal))
            .Attack;
    }

    private void AddActionButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(360.0f, 38.0f) };
        button.Pressed += action;
        button.FocusEntered += () => _focusedButton = button;
        _actions.AddChild(button);
        _actionButtons.Add(button);
        _actionByButton.Add(button, action);
    }

    private void ClearActions()
    {
        foreach (Button button in _actionButtons)
        {
            _actions.RemoveChild(button);
            button.QueueFree();
        }

        _actionButtons.Clear();
        _actionByButton.Clear();
        _focusedButton = null;
    }

    private void CycleFocus(int direction)
    {
        if (_actionButtons.Count == 0)
        {
            return;
        }

        int index = _focusedButton is null ? 0 : _actionButtons.IndexOf(_focusedButton);
        int next = (index + direction + _actionButtons.Count) % _actionButtons.Count;
        _actionButtons[next].GrabFocus();
    }

    private void FocusFirstAction() => _actionButtons.FirstOrDefault()?.GrabFocus();

    private void OnSessionStateChanged(object? sender, EventArgs eventArgs)
    {
        if (Visible)
        {
            Refresh();
        }
    }

    private void SetStatus(string message, bool isError = false)
    {
        _status.Text = message;
        _status.Modulate = isError
            ? new Color(1.0f, 0.45f, 0.45f)
            : new Color(0.75f, 0.9f, 1.0f);
    }

    private string DisplayItemName(string? itemId) => itemId is null
        ? "None"
        : ShortName(RequireContent().GetRequired<ItemDefinition>(itemId).DisplayNameKey);

    private string DisplayActorName(string actorId) => ShortName(
        RequireContent().GetRequired<ActorDefinition>(actorId).DisplayNameKey);

    private static string DisplaySlotName(string slotId) => slotId switch
    {
        EquipmentSlotIds.MainHandWeapon => "Weapon",
        _ => slotId,
    };

    private static string ShortName(string stableKey)
    {
        string[] parts = stableKey.Split('.');
        IEnumerable<string> nameParts = parts.Length > 2 ? parts[1..^1] : parts;
        return string.Join(
            " ",
            nameParts.Select(part => char.ToUpperInvariant(part[0]) + part[1..].Replace('-', ' ')));
    }

    private IContentCatalog RequireContent() => _content
        ?? throw new InvalidOperationException("EquipmentPanel is not initialized.");

    private IGameSession RequireSession() => _session
        ?? throw new InvalidOperationException("EquipmentPanel is not initialized.");

    private EquipmentMenuProjectionResolver RequireProjectionResolver() => _projectionResolver
        ?? throw new InvalidOperationException("EquipmentPanel is not initialized.");

    private EquipmentService RequireEquipmentService() => _equipmentService
        ?? throw new InvalidOperationException("EquipmentPanel is not initialized.");
}
