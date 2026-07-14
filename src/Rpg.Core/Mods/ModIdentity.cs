using System.Text.RegularExpressions;
using RpgGame.Core.Content;

namespace RpgGame.Core.Mods;

/// <summary>
/// Owns the stable identifiers and version strings used by the data-mod boundary.
/// </summary>
/// <remarks>
/// Mods intentionally use a narrower convention than ordinary content. An ID such as
/// <c>mod.jrickter.starter-pack</c> includes both an author namespace and a mod slug, making
/// accidental collisions far less likely when players install community content together.
/// </remarks>
public static partial class ModIdentity
{
    public const string IdPrefix = "mod.";

    /// <summary>Returns whether an ID follows <c>mod.author.mod-name</c>.</summary>
    public static bool IsValidId(string? id)
    {
        if (!ContentId.IsValid(id) || !id!.StartsWith(IdPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        // ContentId has already verified the characters. Requiring three split elements
        // guarantees at least one author segment and one mod-name segment after "mod".
        return id!.Split('.').Length >= 3;
    }

    /// <summary>
    /// Returns whether a release string is a simple Semantic Version such as
    /// <c>1.2.0</c> or <c>1.2.0-beta.1</c>.
    /// </summary>
    public static bool IsValidVersion(string? version) =>
        version is not null && SemanticVersionPattern().IsMatch(version);

    /// <summary>
    /// Converts <c>mod.author.name</c> into the content namespace
    /// <c>author.name</c> used after a category prefix.
    /// </summary>
    public static string GetContentNamespace(string modId)
    {
        if (!IsValidId(modId))
        {
            throw new ArgumentException(
                "Mod IDs must use the stable form 'mod.author.mod-name'.",
                nameof(modId));
        }

        return modId[IdPrefix.Length..];
    }

    [GeneratedRegex(
        "^(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)\\.(0|[1-9][0-9]*)(?:-[0-9A-Za-z-]+(?:\\.[0-9A-Za-z-]+)*)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();
}
