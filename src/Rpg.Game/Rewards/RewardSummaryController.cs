using Godot;
using RpgGame.Core.Rewards;
using RpgGame.Input;

namespace RpgGame.Rewards;

/// <summary>Presents already-applied victory item totals and one confirmed continue action.</summary>
public partial class RewardSummaryController : Control
{
    private VBoxContainer _rewardLines = null!;
    private Button _continueButton = null!;
    private Label _inputHint = null!;

    private InputBindingService? _inputBindings;
    private bool _initialized;
    private bool _continueRequested;

    public event EventHandler<RewardSummaryContinueRequestedEventArgs>? ContinueRequested;

    public override void _Ready()
    {
        _rewardLines = GetNode<VBoxContainer>("Window/Margin/Summary/RewardLines");
        _continueButton = GetNode<Button>("Window/Margin/Summary/Continue");
        _inputHint = GetNode<Label>("Window/Margin/Summary/InputHint");
        _continueButton.Pressed += RequestContinue;
        SetProcessUnhandledInput(false);
    }

    public void Initialize(
        IReadOnlyList<ItemRewardSummary> itemSummaries,
        InputBindingService inputBindings)
    {
        ArgumentNullException.ThrowIfNull(itemSummaries);
        ArgumentNullException.ThrowIfNull(inputBindings);
        if (_initialized)
        {
            throw new InvalidOperationException("Reward summary is already initialized.");
        }

        if (itemSummaries.Any(summary => summary is null))
        {
            throw new ArgumentException(
                "Reward summaries cannot contain a null entry.",
                nameof(itemSummaries));
        }

        _initialized = true;
        _inputBindings = inputBindings;
        _inputBindings.BindingsChanged += OnBindingsChanged;

        if (itemSummaries.Count == 0)
        {
            AddRewardLine(new Label
            {
                Text = "No items found.",
                HorizontalAlignment = HorizontalAlignment.Center,
            });
        }
        else
        {
            foreach (ItemRewardSummary summary in itemSummaries)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(summary.ItemId);
                if (summary.Quantity <= 0)
                {
                    throw new InvalidDataException(
                        $"Reward summary quantity for '{summary.ItemId}' must be positive.");
                }

                AddRewardLine(summary);
            }
        }

        RefreshInputHint();
        SetProcessUnhandledInput(true);
        _continueButton.GrabFocus();
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
        if (!_initialized
            || _continueRequested
            || @event is not InputEventKey { Pressed: true, Echo: false } keyEvent
            || !keyEvent.IsActionPressed(GameInputActions.Interact))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        RequestContinue();
    }

    private void AddRewardLine(ItemRewardSummary summary)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        int separator = summary.ItemId.LastIndexOf('.') + 1;
        Texture2D? icon = ResourceLoader.Load<Texture2D>(
            $"res://game/assets/items/{summary.ItemId[separator..]}.png");
        if (icon is not null)
        {
            row.AddChild(new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(48, 48),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
        }

        var label = new Label
        {
            Text = $"{PlaceholderItemName(summary.ItemId)} ×{summary.Quantity}",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 15);
        row.AddChild(label);
        _rewardLines.AddChild(row);
    }

    private void AddRewardLine(Control line) => _rewardLines.AddChild(line);

    private void RequestContinue()
    {
        if (!_initialized || _continueRequested)
        {
            return;
        }

        _continueRequested = true;
        _continueButton.Disabled = true;
        SetProcessUnhandledInput(false);
        ContinueRequested?.Invoke(
            this,
            new RewardSummaryContinueRequestedEventArgs(
                new RewardSummaryContinueRequest()));
    }

    private void OnBindingsChanged(object? sender, EventArgs eventArgs) => RefreshInputHint();

    private void RefreshInputHint()
    {
        if (_inputBindings is not null)
        {
            _inputHint.Text =
                $"Continue [{_inputBindings.FormatBindings(GameInputActions.Interact)}].";
        }
    }

    private static string PlaceholderItemName(string itemId)
    {
        int start = itemId.LastIndexOf('.') + 1;
        string shortName = start <= 0 || start >= itemId.Length
            ? itemId
            : itemId[start..];
        return string.Join(
            " ",
            shortName.Split('-', StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }
}

/// <summary>Typed request raised once when the player confirms the applied reward summary.</summary>
public sealed record RewardSummaryContinueRequest;

public sealed class RewardSummaryContinueRequestedEventArgs : EventArgs
{
    public RewardSummaryContinueRequestedEventArgs(RewardSummaryContinueRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        Request = request;
    }

    public RewardSummaryContinueRequest Request { get; }
}
