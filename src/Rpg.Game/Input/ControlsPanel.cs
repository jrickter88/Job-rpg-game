using Godot;

namespace RpgGame.Input;

/// <summary>Small player-facing keyboard remapping panel for currently implemented actions.</summary>
public partial class ControlsPanel : PanelContainer
{
    private VBoxContainer _bindingRows = null!;
    private Label _statusLabel = null!;
    private Button _resetButton = null!;
    private Button _closeButton = null!;
    private InputBindingService? _bindings;
    private readonly Dictionary<(string ActionId, int Index), Button> _bindingButtons = [];
    private string? _captureActionId;
    private int _captureIndex = -1;

    /// <summary>Whether exploration should currently ignore gameplay actions.</summary>
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        _bindingRows = GetNode<VBoxContainer>("Margin/VBox/BindingRows");
        _statusLabel = GetNode<Label>("Margin/VBox/Status");
        _resetButton = GetNode<Button>("Margin/VBox/Footer/Reset");
        _closeButton = GetNode<Button>("Margin/VBox/Footer/Close");

        _resetButton.Pressed += ResetDefaults;
        _closeButton.Pressed += ClosePanel;
        Visible = false;
    }

    /// <summary>Injects the application-lifetime binding service and creates action rows.</summary>
    public void Initialize(InputBindingService bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        if (_bindings is not null)
        {
            throw new InvalidOperationException("ControlsPanel is already initialized.");
        }

        _bindings = bindings;
        _bindings.BindingsChanged += OnBindingsChanged;
        BuildRows();
        RefreshButtons();
    }

    public override void _ExitTree()
    {
        if (_bindings is not null)
        {
            _bindings.BindingsChanged -= OnBindingsChanged;
        }
    }

    /// <summary>Opens the panel and places keyboard focus on the first binding.</summary>
    public void Open()
    {
        RequireBindings();
        Visible = true;
        CancelCapture();
        SetStatus("Choose a binding, then press the replacement key.");
        _bindingButtons.Values.FirstOrDefault()?.GrabFocus();
    }

    /// <summary>Closes the panel without changing campaign or dialogue state.</summary>
    public void ClosePanel()
    {
        CancelCapture();
        Visible = false;
    }

    /// <summary>
    /// Captures keys before exploration sees them. While waiting, every keyboard key is a
    /// valid candidate; clicking the selected binding again cancels capture.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (!Visible
            || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent)
        {
            return;
        }

        if (_captureActionId is not null)
        {
            Key key = keyEvent.PhysicalKeycode != Key.None
                ? keyEvent.PhysicalKeycode
                : keyEvent.Keycode;
            InputBindingService bindings = RequireBindings();
            bool changed = bindings.TryRebind(
                _captureActionId,
                _captureIndex,
                key,
                out string message);
            SetStatus(message, isError: !changed);
            if (changed)
            {
                CancelCapture(clearStatus: false);
            }

            GetViewport().SetInputAsHandled();
            return;
        }

        if (@event.IsActionPressed(GameInputActions.Menu))
        {
            ClosePanel();
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildRows()
    {
        foreach (GameInputActionDefinition definition in GameInputActions.Definitions)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            var actionLabel = new Label
            {
                Text = definition.DisplayName,
                CustomMinimumSize = new Vector2(220.0f, 0.0f),
            };
            row.AddChild(actionLabel);

            for (int index = 0; index < definition.DefaultKeys.Count; index++)
            {
                int capturedIndex = index;
                string capturedActionId = definition.Id;
                var button = new Button
                {
                    CustomMinimumSize = new Vector2(135.0f, 36.0f),
                };
                button.Pressed += () => BeginCapture(capturedActionId, capturedIndex);
                row.AddChild(button);
                _bindingButtons.Add((capturedActionId, capturedIndex), button);
            }

            _bindingRows.AddChild(row);
        }
    }

    private void RefreshButtons()
    {
        InputBindingService bindings = RequireBindings();
        foreach (GameInputActionDefinition definition in GameInputActions.Definitions)
        {
            IReadOnlyList<Key> keys = bindings.GetBindings(definition.Id);
            for (int index = 0; index < keys.Count; index++)
            {
                _bindingButtons[(definition.Id, index)].Text =
                    InputBindingService.DisplayKey(keys[index]);
            }
        }
    }

    private void BeginCapture(string actionId, int bindingIndex)
    {
        if (string.Equals(_captureActionId, actionId, StringComparison.Ordinal)
            && _captureIndex == bindingIndex)
        {
            CancelCapture();
            SetStatus("Rebinding cancelled.");
            return;
        }

        _captureActionId = actionId;
        _captureIndex = bindingIndex;
        GameInputActionDefinition definition = GameInputActions.Definitions.First(candidate =>
            string.Equals(candidate.Id, actionId, StringComparison.Ordinal));
        SetStatus(
            $"Press a new key for {definition.DisplayName}. Click this binding again to cancel.");
    }

    private void CancelCapture(bool clearStatus = true)
    {
        _captureActionId = null;
        _captureIndex = -1;
        if (clearStatus)
        {
            SetStatus("Choose a binding, then press the replacement key.");
        }
    }

    private void ResetDefaults()
    {
        bool reset = RequireBindings().TryResetDefaults(out string message);
        SetStatus(message, isError: !reset);
        CancelCapture(clearStatus: false);
    }

    private void OnBindingsChanged(object? sender, EventArgs eventArgs) => RefreshButtons();

    private void SetStatus(string message, bool isError = false)
    {
        _statusLabel.Text = message;
        _statusLabel.Modulate = isError
            ? new Color(1.0f, 0.45f, 0.45f)
            : new Color(0.75f, 0.9f, 1.0f);
    }

    private InputBindingService RequireBindings() => _bindings
        ?? throw new InvalidOperationException("ControlsPanel is not initialized.");
}
