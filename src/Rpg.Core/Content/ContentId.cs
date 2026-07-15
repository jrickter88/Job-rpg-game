using System.Text.RegularExpressions;

namespace RpgGame.Core.Content;

/// <summary>
/// Central validation helper for stable content IDs such as
/// <c>ability.black-magic.fire</c> or <c>enemy.forest.green-slime</c>.
/// </summary>
/// <remarks>
/// Keeping one canonical rule prevents subtly different ID checks in loaders, tools,
/// tests, and gameplay code. The class is <c>partial</c> because the source generator
/// creates the optimized regular-expression implementation at compile time.
/// </remarks>
public static partial class ContentId
{
    /// <summary>
    /// Returns whether <paramref name="id"/> follows the repository's canonical format.
    /// This non-throwing form is useful when collecting several validation errors.
    /// </summary>
    public static bool IsValid(string? id) =>
        id is not null && ContentIdPattern().IsMatch(id);

    /// <summary>
    /// Returns a valid ID unchanged, or throws at an API boundary when the ID is bad.
    /// </summary>
    /// <param name="id">Candidate stable ID.</param>
    /// <param name="parameterName">Optional caller parameter name for a useful exception.</param>
    public static string RequireValid(string id, string? parameterName = null)
    {
        // Treat null, empty, and whitespace-only input as the same programmer error.
        ArgumentException.ThrowIfNullOrWhiteSpace(id, parameterName);

        if (!IsValid(id))
        {
            throw new ArgumentException(
                "Content IDs must be lowercase, dot-separated names with optional kebab-case words.",
                parameterName);
        }

        return id;
    }

    // GeneratedRegex avoids repeatedly parsing the expression at runtime. The pattern
    // requires lowercase dot-separated segments and allows kebab-case within a segment.
    [GeneratedRegex(
        "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*(?:\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ContentIdPattern();
}
