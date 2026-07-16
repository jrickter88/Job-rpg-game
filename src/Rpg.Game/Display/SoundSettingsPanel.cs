using Godot;
using RpgGame.Input;

namespace RpgGame.Display;

public partial class SoundSettingsPanel : PanelContainer
{
    private CheckButton _battleMusic = null!;
    private HSlider _volume = null!;
    private Label _volumeValue = null!;
    private HSlider _overworldVolume = null!;
    private Label _overworldVolumeValue = null!;
    private Button _close = null!;
    private DisplaySettingsService? _settings;
    public bool IsOpen => Visible;

    public override void _Ready()
    {
        _battleMusic = GetNode<CheckButton>("Margin/VBox/BattleMusic");
        _volume = GetNode<HSlider>("Margin/VBox/BattleMusicVolume");
        _volumeValue = GetNode<Label>("Margin/VBox/BattleMusicVolumeValue");
        _overworldVolume = GetNode<HSlider>("Margin/VBox/OverworldMusicVolume");
        _overworldVolumeValue = GetNode<Label>("Margin/VBox/OverworldMusicVolumeValue");
        _close = GetNode<Button>("Margin/VBox/Close");
        Visible = false;
    }

    public void Initialize(DisplaySettingsService settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _settings.BattleMusicChanged += OnSettingsChanged;
        _settings.OverworldMusicChanged += OnSettingsChanged;
        _battleMusic.Toggled += value => RequireSettings().SetBattleMusicEnabled(value);
        _volume.ValueChanged += value => RequireSettings().SetBattleMusicVolumePercent((int)value);
        _overworldVolume.ValueChanged += value => RequireSettings().SetOverworldMusicVolumePercent((int)value);
        _close.Pressed += Close;
        Refresh();
    }

    public override void _ExitTree()
    {
        if (_settings is not null) _settings.BattleMusicChanged -= OnSettingsChanged;
        if (_settings is not null) _settings.OverworldMusicChanged -= OnSettingsChanged;
    }

    public void Open() { Refresh(); Visible = true; _battleMusic.GrabFocus(); }
    public void Close() => Visible = false;

    public override void _Input(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Echo: false } key
            && key.IsActionPressed(GameInputActions.Menu))
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        DisplaySettingsService settings = RequireSettings();
        _battleMusic.ButtonPressed = settings.BattleMusicEnabled;
        _volume.Value = settings.BattleMusicVolumePercent;
        _volumeValue.Text = $"{settings.BattleMusicVolumePercent}%";
        _overworldVolume.Value = settings.OverworldMusicVolumePercent;
        _overworldVolumeValue.Text = $"{settings.OverworldMusicVolumePercent}%";
    }

    private void OnSettingsChanged(object? sender, EventArgs args) => Refresh();
    private DisplaySettingsService RequireSettings() => _settings
        ?? throw new InvalidOperationException("Sound settings panel is not initialized.");
}
