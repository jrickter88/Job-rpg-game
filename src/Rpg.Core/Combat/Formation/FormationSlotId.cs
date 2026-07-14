using System.Globalization;
using System.Text.RegularExpressions;

namespace RpgGame.Core.Combat.Formation;

/// <summary>Single parser/formatter for canonical coordinate-based formation slot IDs.</summary>
public static partial class FormationSlotId
{
    /// <summary>
    /// Parses only encounter-owned enemy slots such as formation.enemy.r1.c0.
    /// </summary>
    /// <remarks>
    /// Party IDs, legacy abstract names, leading zeros, and coordinates outside 4 × 4 are
    /// rejected. Callers never need to split this compatibility-sensitive string themselves.
    /// </remarks>
    public static bool TryParseEnemy(string? slotId, out FormationCell cell)
    {
        if (slotId is null)
        {
            cell = default;
            return false;
        }

        Match match = EnemySlotPattern().Match(slotId);
        if (!match.Success
            || !int.TryParse(
                match.Groups[1].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int row)
            || !int.TryParse(
                match.Groups[2].Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int column))
        {
            cell = default;
            return false;
        }

        var candidate = new FormationCell(BattleSide.Enemy, row, column);
        if (!BattleFormationRules.Contains(candidate)
            || !string.Equals(slotId, Format(candidate), StringComparison.Ordinal))
        {
            cell = default;
            return false;
        }

        cell = candidate;
        return true;
    }

    /// <summary>Formats any valid enemy or party cell using the canonical stable form.</summary>
    public static string Format(FormationCell cell)
    {
        if (!BattleFormationRules.Contains(cell))
        {
            throw new ArgumentOutOfRangeException(
                nameof(cell),
                cell,
                "A formation slot can be formatted only for an in-bounds cell.");
        }

        string side = cell.Side switch
        {
            BattleSide.Enemy => "enemy",
            BattleSide.Party => "party",
            _ => throw new ArgumentOutOfRangeException(
                nameof(cell),
                cell.Side,
                "Unknown battle side."),
        };
        return $"formation.{side}.r{cell.Row}.c{cell.Column}";
    }

    [GeneratedRegex(
        "^formation\\.enemy\\.r([0-9])\\.c([0-9])$",
        RegexOptions.CultureInvariant)]
    private static partial Regex EnemySlotPattern();
}
