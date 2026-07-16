namespace RpgGame.Core.Combat;

/// <summary>Stable identifiers for command-validation failures a caller may handle.</summary>
/// <remarks>
/// These codes describe trusted runtime rules, not content IDs and not presentation text. A
/// future battle UI may use them to refresh stale choices while logs retain the detailed
/// exception message.
/// </remarks>
public static class CombatCommandProblemCodes
{
    public const string BattleAlreadyEnded = "combat.command.battle-already-ended";
    public const string ActorMissing = "combat.command.actor-missing";
    public const string ActorDefeated = "combat.command.actor-defeated";
    public const string AbilityNotOwned = "combat.command.ability-not-owned";
    public const string AbilityMissing = "combat.command.ability-missing";
    public const string TargetCountInvalid = "combat.command.target-count-invalid";
    public const string TargetMissing = "combat.command.target-missing";
    public const string TargetDefeated = "combat.command.target-defeated";
    public const string TargetSameSide = "combat.command.target-same-side";
    public const string AbilityContractUnsupported = "combat.command.ability-contract-unsupported";
    public const string AbilityCostUnsupported = "combat.command.ability-cost-unsupported";
    public const string AbilityResourceInsufficient = "combat.command.ability-resource-insufficient";
}

/// <summary>Indicates that a combat command is not legal for the supplied snapshot.</summary>
/// <remarks>
/// Invalid player or AI intent is separate from malformed published content. The stable
/// <see cref="ProblemCode"/> lets a caller distinguish rejection causes without parsing the
/// human-readable message.
/// </remarks>
public sealed class CombatCommandValidationException : InvalidOperationException
{
    public CombatCommandValidationException(string problemCode, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(problemCode);
        ProblemCode = problemCode;
    }

    public string ProblemCode { get; }
}
