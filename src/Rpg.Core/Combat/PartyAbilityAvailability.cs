namespace RpgGame.Core.Combat;

/// <summary>
/// Party-side command availability projected from learned abilities and class discipline access.
/// </summary>
/// <remarks>
/// This is not a battle menu widget. It is pure data a future UI can read to show direct
/// Skills beside nested Magic containers. The complete executable list remains available for
/// command validation and compatibility with the Milestone 3.0 flat ability view.
/// </remarks>
public sealed record PartyAbilityAvailability
{
    public PartyAbilityAvailability(
        IReadOnlyList<string> directSkillIds,
        IReadOnlyList<MagicDisciplineAvailability> magicDisciplines,
        IReadOnlyList<string> executableAbilityIds)
    {
        ArgumentNullException.ThrowIfNull(directSkillIds);
        ArgumentNullException.ThrowIfNull(magicDisciplines);
        ArgumentNullException.ThrowIfNull(executableAbilityIds);

        if (directSkillIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Direct Skill IDs cannot be blank.", nameof(directSkillIds));
        }

        if (magicDisciplines.Any(discipline => discipline is null))
        {
            throw new ArgumentException(
                "Magic discipline availability cannot contain null entries.",
                nameof(magicDisciplines));
        }

        if (executableAbilityIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Executable ability IDs cannot be blank.",
                nameof(executableAbilityIds));
        }

        DirectSkillIds = Array.AsReadOnly(directSkillIds.ToArray());
        MagicDisciplines = Array.AsReadOnly(magicDisciplines.ToArray());
        ExecutableAbilityIds = Array.AsReadOnly(executableAbilityIds.ToArray());
    }

    /// <summary>Learned Skill abilities shown directly as commands.</summary>
    public IReadOnlyList<string> DirectSkillIds { get; }

    /// <summary>Unlocked magic containers and the learned spells visible inside each one.</summary>
    public IReadOnlyList<MagicDisciplineAvailability> MagicDisciplines { get; }

    /// <summary>
    /// Every executable ability ID in command-validation order. Discipline IDs are excluded.
    /// </summary>
    public IReadOnlyList<string> ExecutableAbilityIds { get; }
}

/// <summary>One unlocked magic container and the learned Magic abilities it can show.</summary>
public sealed record MagicDisciplineAvailability
{
    public MagicDisciplineAvailability(string magicDisciplineId, IReadOnlyList<string> spellAbilityIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicDisciplineId);
        ArgumentNullException.ThrowIfNull(spellAbilityIds);
        if (spellAbilityIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Spell ability IDs cannot be blank.",
                nameof(spellAbilityIds));
        }

        MagicDisciplineId = magicDisciplineId;
        SpellAbilityIds = Array.AsReadOnly(spellAbilityIds.ToArray());
    }

    /// <summary>Stable ID of the non-executable magic-discipline content record.</summary>
    public string MagicDisciplineId { get; }

    /// <summary>Learned Magic ability IDs visible inside this container.</summary>
    public IReadOnlyList<string> SpellAbilityIds { get; }
}
