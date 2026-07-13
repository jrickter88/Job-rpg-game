using System.Text.RegularExpressions;

namespace RpgGame.Core.Content;

public static partial class ContentId
{
    public static bool IsValid(string? id) =>
        id is not null && ContentIdPattern().IsMatch(id);

    public static string RequireValid(string id, string? parameterName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id, parameterName);

        if (!IsValid(id))
        {
            throw new ArgumentException(
                "Content IDs must be lowercase, dot-separated names with optional kebab-case words.",
                parameterName);
        }

        return id;
    }

    [GeneratedRegex(
        "^[a-z][a-z0-9]*(?:\\.[a-z][a-z0-9]*(?:-[a-z0-9]+)*)+$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ContentIdPattern();
}

