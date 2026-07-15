namespace RpgGame.Core.Content.Definitions;

/// <summary>
/// Defines a non-executable magic command container such as a future spellbook category.
/// </summary>
/// <remarks>
/// A discipline is authored content because classes and spells need stable IDs to reference
/// it. It is not an ability: it has no targeting, cost, ruleset, or combat command behavior.
/// Selecting a discipline in a future UI will only open the spell list inside it.
/// </remarks>
public sealed record MagicDisciplineDefinition : ContentDefinition
{
    /// <summary>Localization key for the container name shown in a future Magic menu.</summary>
    public required string DisplayNameKey { get; init; }

    /// <summary>Localization key for help text describing this magic container.</summary>
    public required string DescriptionKey { get; init; }
}
