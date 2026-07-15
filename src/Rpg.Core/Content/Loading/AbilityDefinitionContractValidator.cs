using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Content.Loading;

/// <summary>
/// One ability-specific semantic problem before source-file context is attached.
/// </summary>
/// <remarks>
/// The production <see cref="ContentValidator"/> adds the owning file path. Keeping only the
/// ability contract here prevents this focused rule from knowing about loader IO or catalog
/// publication.
/// </remarks>
internal sealed record AbilityContractProblem(
    string JsonPath,
    string Code,
    string Message);

/// <summary>
/// Validates the closed, code-owned targeting, ruleset, and numeric-parameter contracts.
/// </summary>
/// <remarks>
/// A stable-looking string is not enough for executable behavior. Accepting an unknown ID
/// would let a typo pass content validation and fail only when a player selected the ability.
/// Data mods may select and tune supported contracts, but cannot create executable behavior by
/// inventing another string. This class validates definitions only; it never applies an effect.
/// </remarks>
internal static class AbilityDefinitionContractValidator
{
    public static IReadOnlyList<AbilityContractProblem> Validate(
        AbilityDefinition ability,
        IReadOnlyDictionary<string, decimal> numericParameters)
    {
        ArgumentNullException.ThrowIfNull(ability);
        ArgumentNullException.ThrowIfNull(numericParameters);

        var problems = new List<AbilityContractProblem>();
        bool targetingHasValidShape = HasStablePrefix(ability.TargetingId, "target.");
        bool rulesetHasValidShape = HasStablePrefix(ability.RulesetId, "rules.");

        if (targetingHasValidShape && !IsSupportedTargeting(ability.TargetingId))
        {
            Add(problems, "$.targetingId", "ability.targeting-unsupported",
                $"Unsupported targeting ID '{ability.TargetingId}'. Supported values are "
                + $"'{AbilityTargetingIds.Self}' and '{AbilityTargetingIds.SingleEnemy}'.");
        }

        if (!rulesetHasValidShape)
        {
            // ContentValidator already reports the syntax or prefix error. Returning avoids
            // noisy follow-on messages for a value that cannot identify any ruleset.
            return problems;
        }

        switch (ability.RulesetId)
        {
            case AbilityRulesetIds.Guard:
                ValidateRulesetTargeting(
                    problems,
                    ability,
                    targetingHasValidShape,
                    AbilityTargetingIds.Self);
                ValidateGuardParameters(problems, numericParameters);
                break;
            case AbilityRulesetIds.PhysicalDamage:
                ValidateRulesetTargeting(
                    problems,
                    ability,
                    targetingHasValidShape,
                    AbilityTargetingIds.SingleEnemy);
                ValidatePhysicalDamageParameters(problems, numericParameters);
                break;
            default:
                Add(problems, "$.rulesetId", "ability.ruleset-unsupported",
                    $"Unsupported ruleset ID '{ability.RulesetId}'. Supported values are "
                    + $"'{AbilityRulesetIds.Guard}' and '{AbilityRulesetIds.PhysicalDamage}'.");
                break;
        }

        return problems;
    }

    private static void ValidateRulesetTargeting(
        ICollection<AbilityContractProblem> problems,
        AbilityDefinition ability,
        bool targetingHasValidShape,
        string requiredTargetingId)
    {
        // An unknown target already has its own diagnostic. Only add a compatibility problem
        // when both IDs are recognized, which keeps the final error list actionable.
        if (targetingHasValidShape
            && IsSupportedTargeting(ability.TargetingId)
            && !string.Equals(
                ability.TargetingId,
                requiredTargetingId,
                StringComparison.Ordinal))
        {
            Add(problems, "$.targetingId", "ability.ruleset-targeting-mismatch",
                $"Ruleset '{ability.RulesetId}' requires targeting ID "
                + $"'{requiredTargetingId}', not '{ability.TargetingId}'.");
        }
    }

    private static void ValidateGuardParameters(
        ICollection<AbilityContractProblem> problems,
        IReadOnlyDictionary<string, decimal> numericParameters)
    {
        ValidateOnlyKnownParameters(
            problems,
            numericParameters,
            AbilityRulesetIds.Guard,
            AbilityNumericParameterIds.DamageReduction);

        if (!numericParameters.TryGetValue(
                AbilityNumericParameterIds.DamageReduction,
                out decimal reduction))
        {
            Add(problems, "$.numericParameters", "ability.parameter-missing",
                $"Ruleset '{AbilityRulesetIds.Guard}' requires numeric parameter "
                + $"'{AbilityNumericParameterIds.DamageReduction}'.");
        }
        else if (reduction <= 0m || reduction > 1m)
        {
            Add(problems,
                $"$.numericParameters.{AbilityNumericParameterIds.DamageReduction}",
                "ability.parameter-out-of-range",
                $"'{AbilityNumericParameterIds.DamageReduction}' must be greater than 0 "
                + $"and no greater than 1; found {reduction}.");
        }
    }

    private static void ValidatePhysicalDamageParameters(
        ICollection<AbilityContractProblem> problems,
        IReadOnlyDictionary<string, decimal> numericParameters)
    {
        ValidateOnlyKnownParameters(
            problems,
            numericParameters,
            AbilityRulesetIds.PhysicalDamage,
            AbilityNumericParameterIds.Power);

        if (!numericParameters.TryGetValue(
                AbilityNumericParameterIds.Power,
                out decimal power))
        {
            Add(problems, "$.numericParameters", "ability.parameter-missing",
                $"Ruleset '{AbilityRulesetIds.PhysicalDamage}' requires numeric parameter "
                + $"'{AbilityNumericParameterIds.Power}'.");
        }
        else if (power <= 0m)
        {
            Add(problems,
                $"$.numericParameters.{AbilityNumericParameterIds.Power}",
                "ability.parameter-out-of-range",
                $"'{AbilityNumericParameterIds.Power}' must be greater than 0; found {power}.");
        }
    }

    private static void ValidateOnlyKnownParameters(
        ICollection<AbilityContractProblem> problems,
        IReadOnlyDictionary<string, decimal> numericParameters,
        string rulesetId,
        params string[] supportedParameterIds)
    {
        var supported = new HashSet<string>(supportedParameterIds, StringComparer.Ordinal);
        foreach (string parameterId in numericParameters.Keys)
        {
            if (!supported.Contains(parameterId))
            {
                Add(problems, $"$.numericParameters.{parameterId}",
                    "ability.parameter-unsupported",
                    $"Ruleset '{rulesetId}' does not support numeric parameter "
                    + $"'{parameterId}'. Supported parameters: "
                    + $"{string.Join(", ", supportedParameterIds.Select(id => $"'{id}'"))}.");
            }
        }
    }

    private static bool IsSupportedTargeting(string? targetingId) => targetingId is
        AbilityTargetingIds.Self or
        AbilityTargetingIds.SingleEnemy;

    private static bool HasStablePrefix(string? value, string prefix) =>
        ContentId.IsValid(value)
        && value!.StartsWith(prefix, StringComparison.Ordinal);

    private static void Add(
        ICollection<AbilityContractProblem> problems,
        string jsonPath,
        string code,
        string message) => problems.Add(new AbilityContractProblem(jsonPath, code, message));
}
