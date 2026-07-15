using System.Text;
using System.Text.Json;
using Godot;

namespace RpgGame.Input;

/// <summary>
/// Loads, validates, applies, and persists the player's keyboard bindings.
/// </summary>
/// <remarks>
/// This application-lifetime service owns user preferences, not campaign state. The file is
/// therefore stored under <c>user://settings</c> rather than inside a save slot. Godot's
/// <see cref="InputMap"/> remains the runtime input API; this service only gives it validated
/// events and provides a small editing surface to the controls panel.
/// </remarks>
public sealed class InputBindingService
{
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private Dictionary<string, List<Key>> _bindings = new(StringComparer.Ordinal);

    public InputBindingService(string settingsPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
        _settingsPath = Path.GetFullPath(settingsPath);
    }

    /// <summary>Raised after a successful rebind or reset so open UI can refresh.</summary>
    public event EventHandler? BindingsChanged;

    /// <summary>
    /// Nonfatal explanation when a malformed or future settings file was ignored.
    /// </summary>
    public string? LoadWarning { get; private set; }

    /// <summary>Loads saved preferences when valid, otherwise safely applies defaults.</summary>
    public void Initialize()
    {
        _bindings = CreateDefaultBindings();
        LoadWarning = null;

        if (File.Exists(_settingsPath))
        {
            try
            {
                string json = File.ReadAllText(_settingsPath, Encoding.UTF8);
                BindingSettingsFile? file = JsonSerializer.Deserialize<BindingSettingsFile>(
                    json,
                    JsonOptions);
                _bindings = ResolveFile(file);
            }
            catch (Exception exception) when (
                exception is IOException
                    or UnauthorizedAccessException
                    or JsonException
                    or InvalidDataException)
            {
                // A corrupt preference must never prevent the game from starting. We retain
                // the file for diagnosis and avoid overwriting it until the player changes a
                // binding or explicitly resets controls.
                LoadWarning = $"Controls settings were ignored: {exception.Message}";
                _bindings = CreateDefaultBindings();
            }
        }

        ApplyToInputMap();
    }

    /// <summary>Returns a copy so UI code cannot mutate bindings without validation.</summary>
    public IReadOnlyList<Key> GetBindings(string actionId)
    {
        RequireKnownAction(actionId);
        return _bindings[actionId].ToArray();
    }

    /// <summary>
    /// Formats every current key for one logical action for player-facing instructions.
    /// </summary>
    /// <remarks>
    /// Both exploration and the playable battle use this method so presentation never
    /// duplicates knowledge of concrete keys or silently shows stale defaults after a rebind.
    /// </remarks>
    public string FormatBindings(string actionId) =>
        string.Join(" / ", GetBindings(actionId).Select(DisplayKey));

    /// <summary>
    /// Replaces one keyboard slot, rejecting duplicates and rolling back on write failure.
    /// </summary>
    public bool TryRebind(string actionId, int bindingIndex, Key key, out string message)
    {
        RequireKnownAction(actionId);
        List<Key> actionBindings = _bindings[actionId];
        if (bindingIndex < 0 || bindingIndex >= actionBindings.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(bindingIndex));
        }

        if (!IsBindableKey(key))
        {
            message = key is Key.R or Key.K or Key.L
                ? $"{DisplayKey(key)} is reserved for the current developer controls."
                : "That key cannot be assigned.";
            return false;
        }

        Key previousKey = actionBindings[bindingIndex];
        if (previousKey == key)
        {
            message = $"{DisplayKey(key)} is already assigned there.";
            return true;
        }

        if (TryFindBinding(key, out string conflictingActionId))
        {
            string displayName = GetDefinition(conflictingActionId).DisplayName;
            message = $"{DisplayKey(key)} is already assigned to {displayName}.";
            return false;
        }

        actionBindings[bindingIndex] = key;
        ApplyToInputMap();

        try
        {
            SaveFile();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            actionBindings[bindingIndex] = previousKey;
            ApplyToInputMap();
            message = $"Could not save controls: {exception.Message}";
            return false;
        }

        BindingsChanged?.Invoke(this, EventArgs.Empty);
        message = $"Assigned {DisplayKey(key)} to {GetDefinition(actionId).DisplayName}.";
        return true;
    }

    /// <summary>Restores every current action and persists the resulting preference file.</summary>
    public bool TryResetDefaults(out string message)
    {
        Dictionary<string, List<Key>> previous = CloneBindings(_bindings);
        _bindings = CreateDefaultBindings();
        ApplyToInputMap();

        try
        {
            SaveFile();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _bindings = previous;
            ApplyToInputMap();
            message = $"Could not save controls: {exception.Message}";
            return false;
        }

        BindingsChanged?.Invoke(this, EventArgs.Empty);
        message = "Default controls restored.";
        return true;
    }

    /// <summary>Friendly label shared by the controls screen and status messages.</summary>
    public static string DisplayKey(Key key) => key switch
    {
        Key.Up => "Up Arrow",
        Key.Right => "Right Arrow",
        Key.Down => "Down Arrow",
        Key.Left => "Left Arrow",
        Key.Escape => "Esc",
        Key.Enter => "Enter",
        Key.KpEnter => "Numpad Enter",
        Key.Space => "Space",
        _ => key.ToString(),
    };

    private void ApplyToInputMap()
    {
        foreach (GameInputActionDefinition definition in GameInputActions.Definitions)
        {
            if (!InputMap.HasAction(definition.Id))
            {
                InputMap.AddAction(definition.Id);
            }

            InputMap.ActionEraseEvents(definition.Id);
            foreach (Key key in _bindings[definition.Id])
            {
                // Physical keycodes are recommended for game actions: a player's chosen key
                // position remains stable if the operating-system keyboard layout changes.
                // Godot performs the event/action match; exploration sees only the action ID.
                var inputEvent = new InputEventKey { PhysicalKeycode = key };
                InputMap.ActionAddEvent(definition.Id, inputEvent);
            }
        }
    }

    private Dictionary<string, List<Key>> ResolveFile(BindingSettingsFile? file)
    {
        if (file is null)
        {
            throw new InvalidDataException("Controls settings must contain one JSON object.");
        }

        if (file.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported controls schema {file.SchemaVersion}; expected {CurrentSchemaVersion}.");
        }

        if (file.Bindings is null)
        {
            throw new InvalidDataException("Controls field 'bindings' cannot be null.");
        }

        var resolved = new Dictionary<string, List<Key>>(StringComparer.Ordinal);
        var usedKeys = new HashSet<Key>();

        foreach (GameInputActionDefinition definition in GameInputActions.Definitions)
        {
            List<string>? keyNames;
            if (!file.Bindings.TryGetValue(definition.Id, out keyNames))
            {
                // Adding a new logical action is an additive settings change. Older profiles
                // receive that action's defaults without losing their existing preferences.
                keyNames = definition.DefaultKeys.Select(key => key.ToString()).ToList();
            }

            if (keyNames is null || keyNames.Count != definition.DefaultKeys.Count)
            {
                throw new InvalidDataException(
                    $"Action '{definition.Id}' must contain {definition.DefaultKeys.Count} bindings.");
            }

            var keys = new List<Key>(keyNames.Count);
            foreach (string? keyName in keyNames)
            {
                if (string.IsNullOrWhiteSpace(keyName)
                    || !Enum.TryParse(keyName, ignoreCase: true, out Key key)
                    || !IsBindableKey(key))
                {
                    throw new InvalidDataException(
                        $"Action '{definition.Id}' contains invalid key '{keyName}'.");
                }

                if (!usedKeys.Add(key))
                {
                    throw new InvalidDataException(
                        $"Key '{DisplayKey(key)}' is assigned more than once.");
                }

                keys.Add(key);
            }

            resolved.Add(definition.Id, keys);
        }

        return resolved;
    }

    private void SaveFile()
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new IOException("Controls settings path has no parent directory.");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = _settingsPath + $".{Guid.NewGuid():N}.tmp";
        var file = new BindingSettingsFile
        {
            SchemaVersion = CurrentSchemaVersion,
            Bindings = _bindings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(key => key.ToString()).ToList(),
                StringComparer.Ordinal),
        };
        string json = JsonSerializer.Serialize(file, JsonOptions) + System.Environment.NewLine;

        try
        {
            File.WriteAllText(temporaryPath, json, Encoding.UTF8);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private bool TryFindBinding(Key key, out string actionId)
    {
        foreach ((string candidateActionId, List<Key> keys) in _bindings)
        {
            if (keys.Contains(key))
            {
                actionId = candidateActionId;
                return true;
            }
        }

        actionId = string.Empty;
        return false;
    }

    // R/K/L belong to the temporary room rebuild/save/load proof. Keeping them out of the
    // player profile prevents one keypress from triggering both gameplay and a developer tool.
    private static bool IsBindableKey(Key key) =>
        (long)key > 0 && key is not (Key.R or Key.K or Key.L);

    private static Dictionary<string, List<Key>> CreateDefaultBindings() =>
        GameInputActions.Definitions.ToDictionary(
            definition => definition.Id,
            definition => definition.DefaultKeys.ToList(),
            StringComparer.Ordinal);

    private static Dictionary<string, List<Key>> CloneBindings(
        IReadOnlyDictionary<string, List<Key>> source) =>
        source.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToList(),
            StringComparer.Ordinal);

    private static GameInputActionDefinition GetDefinition(string actionId) =>
        GameInputActions.Definitions.First(definition =>
            string.Equals(definition.Id, actionId, StringComparison.Ordinal));

    private static void RequireKnownAction(string actionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        if (!GameInputActions.Definitions.Any(definition =>
                string.Equals(definition.Id, actionId, StringComparison.Ordinal)))
        {
            throw new ArgumentException($"Unknown input action '{actionId}'.", nameof(actionId));
        }
    }

    private sealed record BindingSettingsFile
    {
        public int SchemaVersion { get; init; } = CurrentSchemaVersion;

        public Dictionary<string, List<string>>? Bindings { get; init; } =
            new(StringComparer.Ordinal);
    }
}
