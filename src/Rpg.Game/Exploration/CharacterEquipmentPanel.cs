using Godot;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Equipment;
using RpgGame.Core.State;
using RpgGame.Input;
using RpgGame.Localization;

namespace RpgGame.Exploration;

/// <summary>Disposable character equipment screen backed by read-only core projections.</summary>
public partial class CharacterEquipmentPanel : PanelContainer
{
    private Label _title = null!;
    private Label _character = null!;
    private RichTextLabel _comparison = null!;
    private Label _detail = null!;
    private Label _status = null!;
    private Label _choicesTitle = null!;
    private VBoxContainer _slots = null!;
    private VBoxContainer _choices = null!;
    private IContentCatalog? _content;
    private IGameSession? _session;
    private EquipmentScreenProjectionResolver? _screenResolver;
    private EquipmentService? _equipmentService;
    private LocalizedTextCatalog? _text;
    private readonly List<Button> _buttons = [];
    private readonly List<Button> _slotButtons = [];
    private readonly List<Button> _choiceButtons = [];
    private readonly Dictionary<Button, Action> _actionsByButton = [];
    private Button? _focusedButton;
    private string? _actorId;
    private string? _selectedSlotId;

    public bool IsOpen => Visible;

    public override void _Ready()
    {
        _title = GetNode<Label>("Margin/VBox/Title");
        _character = GetNode<Label>("Margin/VBox/Character");
        _comparison = GetNode<RichTextLabel>("Margin/VBox/Lower/StatsColumn/Comparison");
        _detail = GetNode<Label>("Margin/VBox/Lower/StatsColumn/Detail");
        _status = GetNode<Label>("Margin/VBox/Status");
        _choicesTitle = GetNode<Label>("Margin/VBox/Lower/ChoicesColumn/ChoicesTitle");
        _slots = GetNode<VBoxContainer>("Margin/VBox/Slots");
        _choices = GetNode<VBoxContainer>("Margin/VBox/Lower/ChoicesColumn/Choices");
        Visible = false;
    }

    public void Initialize(IContentCatalog content, IGameSession session, LocalizedTextCatalog text)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _screenResolver = new EquipmentScreenProjectionResolver(content);
        _equipmentService = new EquipmentService(content, session);
        _text = text ?? throw new ArgumentNullException(nameof(text));
        session.StateChanged += OnSessionStateChanged;
    }

    public override void _ExitTree()
    {
        if (_session is not null) _session.StateChanged -= OnSessionStateChanged;
    }

    public void Open()
    {
        _actorId = RequireSession().Current.ActivePartyActorIds.FirstOrDefault()
            ?? throw new InvalidOperationException("The active party has no actor to equip.");
        _selectedSlotId = null;
        Visible = true;
        Refresh();
    }

    public void Close() { Visible = false; _selectedSlotId = null; }

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent) return;
        if (keyEvent.IsActionPressed(GameInputActions.Equipment))
        {
            Close();
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.Menu))
        {
            if (_selectedSlotId is null) Close(); else { _selectedSlotId = null; Refresh(); }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.MoveUp) || keyEvent.IsActionPressed(GameInputActions.MoveLeft)) { CycleFocus(-1); GetViewport().SetInputAsHandled(); return; }
        if (keyEvent.IsActionPressed(GameInputActions.MoveDown) || keyEvent.IsActionPressed(GameInputActions.MoveRight)) { CycleFocus(1); GetViewport().SetInputAsHandled(); return; }
        if (keyEvent.IsActionPressed(GameInputActions.Interact) && _focusedButton is not null && _actionsByButton.TryGetValue(_focusedButton, out Action? action)) { action(); GetViewport().SetInputAsHandled(); }
    }

    private void Refresh()
    {
        EquipmentScreenModel screen = RequireScreenResolver().Resolve(RequireSession().Current, _actorId!);
        _title.Text = $"Equipment - {DisplayActorName(screen.ActorId)}";
        _character.Text = $"{DisplayActorName(screen.ActorId)}    Class: {DisplayClassName(screen.ClassId)}    Level: {screen.Level}";
        ShowComparison(screen.CurrentStats, screen.CurrentStats);
        ClearButtons();
        RenderSlots(screen);
        if (_selectedSlotId is null)
        {
            _choicesTitle.Text = "Choose a slot above";
            _detail.Text = "Effects: Select equipment to inspect it.";
            AddChoiceButton("Close Equipment", Close);
            SetStatus("Confirm a slot to browse equipment. I closes this screen.");
        }
        else
        {
            RenderChoices(screen.Slots.Single(slot => slot.SlotId == _selectedSlotId));
            SetStatus("Highlight an option to preview. Confirm equips; Cancel returns to slots.");
        }

        (_selectedSlotId is null ? _slotButtons : _choiceButtons).FirstOrDefault()?.GrabFocus();
    }

    private void RenderSlots(EquipmentScreenModel screen)
    {
        foreach (EquipmentSlotScreenModel slot in screen.Slots)
        {
            string slotId = slot.SlotId;
            string selected = slotId == _selectedSlotId ? "> " : "  ";
            AddSlotButton(
                $"{selected}{DisplaySlotName(slotId),-10} {DisplayItemName(slot.EquippedItem)}",
                () => { _selectedSlotId = slotId; Refresh(); },
                () => ShowItemDetail(slot.EquippedItem));
        }
    }

    private void RenderChoices(EquipmentSlotScreenModel slot)
    {
        _choicesTitle.Text = $"Choose {DisplaySlotName(slot.SlotId)}";
        if (slot.EquippedItem is not null)
        {
            AddChoiceButton(
                "Unequip",
                () => TryUnequip(slot.SlotId),
                () => ShowPreview(slot.SlotId, null));
        }

        if (slot.CompatibleOwnedItems.Count == 0) _detail.Text = "No compatible equipment owned for this slot.";
        foreach (EquipmentItemDetail item in slot.CompatibleOwnedItems)
        {
            EquipmentItemDetail captured = item;
            string marker = slot.EquippedItem?.ItemId == item.ItemId ? " [Equipped]" : string.Empty;
            AddChoiceButton(
                $"{DisplayItemName(item)}{CompactHint(item)}{marker}",
                () => TryEquip(item.ItemId, slot.SlotId),
                () => ShowPreview(slot.SlotId, captured.ItemId));
        }
        AddChoiceButton("Back", () => { _selectedSlotId = null; Refresh(); });
    }

    private void ShowPreview(string slotId, string? itemId)
    {
        EquipmentPreviewModel preview = RequireScreenResolver().PreviewEquipmentChange(RequireSession().Current, _actorId!, slotId, itemId);
        ShowComparison(preview.Current.CurrentStats, preview.PreviewStats);
        ShowItemDetail(preview.CandidateItem);
    }

    private void ShowComparison(EquipmentStatValues current, EquipmentStatValues preview) => _comparison.Text =
        "[color=#59e7ff]Current -> Preview[/color]" + System.Environment.NewLine
        + ComparisonLine("HP", current.MaximumHp, preview.MaximumHp, "MP", current.MaximumMp, preview.MaximumMp)
        + ComparisonLine("STR", current.Strength, preview.Strength, "INT", current.Intelligence, preview.Intelligence)
        + ComparisonLine("DEF", current.Defense, preview.Defense, "SPR", current.Spirit, preview.Spirit)
        + ComparisonLine("SPD", current.Speed, preview.Speed, "ATK", current.WeaponAttack, preview.WeaponAttack);

    private void ShowItemDetail(EquipmentItemDetail? item)
    {
        if (item is null)
        {
            _detail.Text = "Description: Empty slot.\nSpecial effects: None";
            return;
        }

        string effects = item.SpecialEffectIds.Count == 0
            ? "None"
            : string.Join(", ", item.SpecialEffectIds.Select(ShortName));
        _detail.Text = $"Description: {RequireText().Resolve(item.DescriptionKey)}"
            + $"{System.Environment.NewLine}Special effects: {effects}";
    }

    private void TryEquip(string itemId, string slotId)
    {
        try { RequireEquipmentService().EquipItem(_actorId!, itemId, slotId); SetStatus($"Equipped {DisplayItemName(itemId)}."); }
        catch (Exception exception) { Refresh(); SetStatus($"Equipment change failed: {exception.Message}", true); }
    }

    private void TryUnequip(string slotId)
    {
        try { RequireEquipmentService().UnequipItem(_actorId!, slotId); SetStatus($"Unequipped {DisplaySlotName(slotId)}."); }
        catch (Exception exception) { Refresh(); SetStatus($"Equipment change failed: {exception.Message}", true); }
    }

    private void AddSlotButton(string text, Action action, Action? focused = null) =>
        AddButton(_slots, _slotButtons, text, action, focused);

    private void AddChoiceButton(string text, Action action, Action? focused = null) =>
        AddButton(_choices, _choiceButtons, text, action, focused);

    private void AddButton(
        VBoxContainer container,
        List<Button> group,
        string text,
        Action action,
        Action? focused = null)
    {
        var button = new Button
        {
            Text = text,
            CustomMinimumSize = new Vector2(0.0f, 10.0f),
            Alignment = HorizontalAlignment.Left,
            Flat = true,
        };
        button.AddThemeFontSizeOverride("font_size", 5);
        button.Pressed += action;
        button.FocusEntered += () => { _focusedButton = button; focused?.Invoke(); };
        container.AddChild(button);
        _buttons.Add(button);
        group.Add(button);
        _actionsByButton.Add(button, action);
    }

    private void ClearButtons()
    {
        foreach (Button button in _buttons)
        {
            button.GetParent()?.RemoveChild(button);
            button.QueueFree();
        }

        _buttons.Clear();
        _slotButtons.Clear();
        _choiceButtons.Clear();
        _actionsByButton.Clear();
        _focusedButton = null;
    }

    private void CycleFocus(int direction)
    {
        if (_buttons.Count == 0) return;
        int index = _focusedButton is null ? 0 : _buttons.IndexOf(_focusedButton);
        _buttons[(index + direction + _buttons.Count) % _buttons.Count].GrabFocus();
    }

    private void OnSessionStateChanged(object? sender, EventArgs eventArgs) { if (Visible) Refresh(); }
    private static string ComparisonLine(string left, int leftCurrent, int leftPreview, string right, int rightCurrent, int rightPreview) =>
        $"{ComparisonValue(left, leftCurrent, leftPreview)}    {ComparisonValue(right, rightCurrent, rightPreview)}{System.Environment.NewLine}";
    private static string ComparisonValue(string label, int current, int preview)
    {
        int delta = preview - current;
        string color = delta switch { > 0 => "#72e58b", < 0 => "#ff7b72", _ => "#c9d1d9" };
        string deltaText = delta > 0 ? $"+{delta}" : delta.ToString();
        return $"{label} {current} -> [color={color}]{preview} ({deltaText})[/color]";
    }
    private static string CompactHint(EquipmentItemDetail item) => item.Attack > 0 ? $"  Attack +{item.Attack}" : string.Empty;
    private static string DisplaySlotName(string slotId) => slotId switch
    {
        EquipmentSlotIds.MainHandWeapon => "Weapon",
        EquipmentSlotIds.OffHandWeapon => "Off Hand",
        EquipmentSlotIds.BodyArmor => "Body",
        EquipmentSlotIds.FeetArmor => "Feet",
        EquipmentSlotIds.HelmArmor => "Helm",
        EquipmentSlotIds.AccessoryOne => "Accessory 1",
        EquipmentSlotIds.AccessoryTwo => "Accessory 2",
        _ => slotId,
    };
    private string DisplayItemName(EquipmentItemDetail? item) => item is null ? "None" : ShortName(item.DisplayNameKey);
    private string DisplayItemName(string itemId) => ShortName(RequireContent().GetRequired<ItemDefinition>(itemId).DisplayNameKey);
    private string DisplayActorName(string actorId) => ShortName(RequireContent().GetRequired<ActorDefinition>(actorId).DisplayNameKey);
    private string DisplayClassName(string classId) => ShortName(RequireContent().GetRequired<ClassDefinition>(classId).DisplayNameKey);
    private static string ShortName(string key) => string.Join(" ", key.Split('.')[1..^1].Select(part => char.ToUpperInvariant(part[0]) + part[1..].Replace('-', ' ')));
    private void SetStatus(string message, bool isError = false) { _status.Text = message; _status.Modulate = isError ? new Color(1.0f, .45f, .45f) : new Color(.75f, .9f, 1.0f); }
    private IContentCatalog RequireContent() => _content ?? throw new InvalidOperationException("CharacterEquipmentPanel is not initialized.");
    private IGameSession RequireSession() => _session ?? throw new InvalidOperationException("CharacterEquipmentPanel is not initialized.");
    private EquipmentScreenProjectionResolver RequireScreenResolver() => _screenResolver ?? throw new InvalidOperationException("CharacterEquipmentPanel is not initialized.");
    private EquipmentService RequireEquipmentService() => _equipmentService ?? throw new InvalidOperationException("CharacterEquipmentPanel is not initialized.");
    private LocalizedTextCatalog RequireText() => _text ?? throw new InvalidOperationException("CharacterEquipmentPanel is not initialized.");
}
