namespace RpgGame.Core.State;

/// <summary>
/// Small, game-owned safety rule for party size.
/// </summary>
/// <remarks>
/// The game intentionally supports one party containing at most four heroes. Mods may add
/// alternative actor definitions, classes, and abilities, but they do not change this limit.
/// Keeping one rule avoids separate roster/active-party concepts before the game needs them.
/// </remarks>
public static class PartyRules
{
    /// <summary>Largest party supported by campaign rules, saves, combat, and future UI.</summary>
    public const int MaximumPartyMembers = 4;

    /// <summary>
    /// Validates a party count at a use-case boundary such as new game or recruitment.
    /// </summary>
    public static void ValidateMemberCount(int memberCount, string? parameterName = null)
    {
        if (memberCount is < 1 or > MaximumPartyMembers)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                memberCount,
                $"Party must contain between 1 and {MaximumPartyMembers} heroes.");
        }
    }
}
