namespace RpgGame.Core.Persistence;

/// <summary>
/// Prevents logical slot names from becoming path traversal or invalid filenames.
/// </summary>
public static class SaveSlotId
{
    /// <summary>Returns the slot unchanged when it contains only safe portable characters.</summary>
    public static string RequireValid(string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);

        if (slotId.Length > 64
            || slotId.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            throw new ArgumentException(
                "Save slot IDs may contain only ASCII letters, digits, '-' and '_' (max 64).",
                nameof(slotId));
        }

        return slotId;
    }
}
