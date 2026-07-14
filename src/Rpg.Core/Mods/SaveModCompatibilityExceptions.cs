namespace RpgGame.Core.Mods;

/// <summary>
/// Raised when a save requires a data mod that is not available in the current installation.
/// A dedicated type lets a future load-game screen present recovery instructions without
/// parsing an exception message.
/// </summary>
public sealed class MissingSaveModException : Exception
{
    public MissingSaveModException(string modId)
        : base($"Save requires data mod '{modId}', but that mod is not currently enabled.")
    {
        ModId = modId;
    }

    public string ModId { get; }
}

/// <summary>
/// Raised when the installed mod has the correct stable ID but a different authored version.
/// Exact matching is conservative: silent data changes are more dangerous than a clear error.
/// </summary>
public sealed class IncompatibleSaveModVersionException : Exception
{
    public IncompatibleSaveModVersionException(
        string modId,
        string requiredVersion,
        string installedVersion)
        : base(
            $"Save requires data mod '{modId}' version '{requiredVersion}', "
            + $"but version '{installedVersion}' is enabled.")
    {
        ModId = modId;
        RequiredVersion = requiredVersion;
        InstalledVersion = installedVersion;
    }

    public string ModId { get; }

    public string RequiredVersion { get; }

    public string InstalledVersion { get; }
}
