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
    /// <summary>
    /// Creates one internally consistent projection from its two authoritative menu shapes.
    /// </summary>
    /// <remarks>
    /// Construction is internal because callers should ask <see cref="AbilityAvailabilityResolver"/>
    /// to apply learning and discipline rules. The flat executable list is deliberately derived
    /// here instead of supplied as a third argument; otherwise it could disagree with the Skill
    /// and Magic collections that a future menu displays.
    /// </remarks>
    internal PartyAbilityAvailability(
        IReadOnlyList<string> directSkillIds,
        IReadOnlyList<MagicDisciplineAvailability> magicDisciplines)
    {
        ArgumentNullException.ThrowIfNull(directSkillIds);
        ArgumentNullException.ThrowIfNull(magicDisciplines);

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

        if (directSkillIds.Count != directSkillIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException(
                "Direct Skill IDs cannot contain duplicates.",
                nameof(directSkillIds));
        }

        if (magicDisciplines.Count != magicDisciplines
                .Select(discipline => discipline.MagicDisciplineId)
                .Distinct(StringComparer.Ordinal)
                .Count())
        {
            throw new ArgumentException(
                "Magic discipline availability cannot contain duplicate discipline IDs.",
                nameof(magicDisciplines));
        }

        DirectSkillIds = Array.AsReadOnly(directSkillIds.ToArray());
        MagicDisciplines = Array.AsReadOnly(magicDisciplines.ToArray());
        ExecutableAbilityIds = Array.AsReadOnly(BuildExecutableAbilityIds(
            DirectSkillIds,
            MagicDisciplines));
    }

    /// <summary>Learned Skill abilities shown directly as commands.</summary>
    public IReadOnlyList<string> DirectSkillIds { get; }

    /// <summary>Unlocked magic containers and the learned spells visible inside each one.</summary>
    public IReadOnlyList<MagicDisciplineAvailability> MagicDisciplines { get; }

    /// <summary>
    /// Every executable ability ID in command-validation order. Discipline IDs are excluded.
    /// </summary>
    public IReadOnlyList<string> ExecutableAbilityIds { get; }

    private static string[] BuildExecutableAbilityIds(
        IReadOnlyList<string> directSkillIds,
        IReadOnlyList<MagicDisciplineAvailability> magicDisciplines)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string abilityId in directSkillIds)
        {
            if (seen.Add(abilityId))
            {
                result.Add(abilityId);
            }
        }

        // One spell may belong to several unlocked disciplines. It should appear in each
        // relevant submenu, but command validation needs only one executable stable ID.
        foreach (MagicDisciplineAvailability discipline in magicDisciplines)
        {
            foreach (string spellAbilityId in discipline.SpellAbilityIds)
            {
                if (seen.Add(spellAbilityId))
                {
                    result.Add(spellAbilityId);
                }
            }
        }

        return result.ToArray();
    }
}

/// <summary>One unlocked magic container and the learned Magic abilities it can show.</summary>
public sealed record MagicDisciplineAvailability
{
    internal MagicDisciplineAvailability(
        string magicDisciplineId,
        IReadOnlyList<string> spellAbilityIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicDisciplineId);
        ArgumentNullException.ThrowIfNull(spellAbilityIds);
        if (spellAbilityIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "Spell ability IDs cannot be blank.",
                nameof(spellAbilityIds));
        }

        if (spellAbilityIds.Count != spellAbilityIds.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException(
                "Spell ability IDs cannot contain duplicates.",
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
