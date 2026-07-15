namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Defines the authored data for one action that combat or another rules system can use.
/// </summary>
/// <remarks>
/// Definitions choose from a small set of code-owned targeting and ruleset behaviors.
/// They supply tuning values but do not contain animation code or an open-ended scripting
/// language. Presentation assets will be mapped separately by stable ability ID.
/// </remarks>
public sealed record AbilityDefinition : ContentDefinition
{
    /// <summary>Localization key for the short ability name.</summary>
    public required string DisplayNameKey { get; init; }

    /// <summary>Localization key for help text describing the ability.</summary>
    public required string DescriptionKey { get; init; }

    /// <summary>
    /// Code-owned category for how this ability appears to a future battle command UI.
    /// Omitted JSON defaults to a direct Skill for compatibility with existing content.
    /// </summary>
    public string AbilityKindId { get; init; } = AbilityKindIds.Skill;

    /// <summary>
    /// Stable IDs of non-executable spellbook containers that can display this ability.
    /// This must be empty for Skills and nonempty for Magic abilities.
    /// </summary>
    public List<string> MagicDisciplineIds { get; init; } = [];

    /// <summary>
    /// Stable key selecting a code-owned targeting rule.
    /// </summary>
    /// <remarks>
    /// Use a value from <see cref="AbilityTargetingIds"/>. A new string in JSON cannot create
    /// targeting behavior; a new mode needs trusted core code and validation first.
    /// </remarks>
    public required string TargetingId { get; init; }

    /// <summary>
    /// Statistic/resource spent to use the ability, or null when the ability is free.
    /// </summary>
    public string? CostStatisticId { get; init; }

    /// <summary>Amount deducted from <see cref="CostStatisticId"/> when used.</summary>
    public int CostAmount { get; init; }

    /// <summary>
    /// Identifies a small, code-owned rules implementation; this is not a scripting DSL.
    /// </summary>
    /// <remarks>
    /// Use a value from <see cref="AbilityRulesetIds"/>. The content validator checks the
    /// ruleset's target compatibility and exact numeric-parameter contract.
    /// </remarks>
    public required string RulesetId { get; init; }

    /// <summary>
    /// Ruleset-specific tuning values, such as power or accuracy. Each known ruleset must
    /// validate its required keys, accepted keys, and legal ranges so this dictionary does
    /// not become an accidental DSL or a bag of silently ignored typos.
    /// Decimal values make authored percentages/factors explicit and deterministic.
    /// </summary>
    public Dictionary<string, decimal> NumericParameters { get; init; } = [];
}

/// <summary>
/// Stable authored IDs for the supported ability categories.
/// </summary>
/// <remarks>
/// These remain strings rather than a JSON enum so content and mods keep stable IDs that can
/// be extended later without changing save data or serialized enum names.
/// </remarks>
public static class AbilityKindIds
{
    public const string Skill = "ability-kind.skill";

    public const string Magic = "ability-kind.magic";
}
