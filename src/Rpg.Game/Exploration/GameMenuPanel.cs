using Godot;
using RpgGame.Input;

namespace RpgGame.Exploration;

/// <summary>Small exploration-local menu that routes to existing controls or equipment panels.</summary>
public partial class GameMenuPanel : PanelContainer
{
    private Button _equipmentButton = null!;
    private Button _controlsButton = null!;
    private Button _displayButton = null!;
    private Button _soundButton = null!;
    private Button _closeButton = null!;
    private readonly List<Button> _buttons = [];
    private readonly Dictionary<Button, Action> _actionByButton = [];
    private Button? _focusedButton;

    public event EventHandler? EquipmentRequested;

    public event EventHandler? ControlsRequested;

    public event EventHandler? DisplayRequested;
    public event EventHandler? SoundRequested;

    public override void _Ready()
    {
        _equipmentButton = GetNode<Button>("Margin/VBox/Equipment");
        _controlsButton = GetNode<Button>("Margin/VBox/Controls");
        _displayButton = GetNode<Button>("Margin/VBox/Display");
        _soundButton = GetNode<Button>("Margin/VBox/Sound");
        _closeButton = GetNode<Button>("Margin/VBox/Close");
        AddButton(_equipmentButton, () => EquipmentRequested?.Invoke(this, EventArgs.Empty));
        AddButton(_controlsButton, () => ControlsRequested?.Invoke(this, EventArgs.Empty));
        AddButton(_displayButton, () => DisplayRequested?.Invoke(this, EventArgs.Empty));
        AddButton(_soundButton, () => SoundRequested?.Invoke(this, EventArgs.Empty));
        AddButton(_closeButton, Close);
        Visible = false;
    }

    public void Open()
    {
        Visible = true;
        _equipmentButton.GrabFocus();
    }

    public void Close() => Visible = false;

    public override void _Input(InputEvent @event)
    {
        if (!Visible || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (keyEvent.IsActionPressed(GameInputActions.Menu))
        {
            Close();
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

    private void AddButton(Button button, Action action)
    {
        button.Pressed += action;
        button.FocusEntered += () => _focusedButton = button;
        _buttons.Add(button);
        _actionByButton.Add(button, action);
    }

    private void CycleFocus(int direction)
    {
        int index = _focusedButton is null ? 0 : _buttons.IndexOf(_focusedButton);
        int next = (index + direction + _buttons.Count) % _buttons.Count;
        _buttons[next].GrabFocus();
    }
}
