using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Combat;

/// <summary>
/// Resolves the abilities one party actor may use from intrinsic actor grants followed by
/// level-eligible grants from the class selected in this campaign.
/// </summary>
/// <remarks>
/// The resolver projects immutable content plus one save's <see cref="ActorProgressState"/>;
/// it does not modify learning state or infer a class from the actor ID. Equipment grants are
/// deliberately absent until equipment exists in campaign/runtime state. Production content
/// is validated at startup, while the defensive checks here make failures from hand-built test
/// or editor catalogs immediate and descriptive.
/// </remarks>
public sealed class AbilityAvailabilityResolver
{
    private readonly IContentCatalog _content;

    public AbilityAvailabilityResolver(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>
    /// Builds the direct-Skill and Magic-container views available at the actor's current level.
    /// </summary>
    /// <remarks>
    /// Authored source order is meaningful: actor-intrinsic abilities come first, followed by
    /// eligible class unlocks. Cross-source duplicates keep the first occurrence. A learned
    /// Magic ability becomes executable only through at least one unlocked matching discipline.
    /// </remarks>
    public PartyAbilityAvailability ResolvePartyActor(ActorProgressState progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentException.ThrowIfNullOrWhiteSpace(progress.ActorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(progress.ClassId);
        if (progress.Level < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(progress),
                progress.Level,
                $"Actor '{progress.ActorId}' must have a level of at least 1.");
        }

        ActorDefinition actor = _content.GetRequired<ActorDefinition>(progress.ActorId);
        ClassDefinition classDefinition = _content.GetRequired<ClassDefinition>(progress.ClassId);
        IReadOnlyList<string> startingAbilityIds = actor.StartingAbilityIds
            ?? throw new InvalidDataException(
                $"Actor '{actor.Id}' has a null starting-ability list.");
        IReadOnlyList<AbilityUnlockDefinition> unlocks = classDefinition.AbilityUnlocks
            ?? throw new InvalidDataException(
                $"Class '{classDefinition.Id}' has a null ability-unlock list.");
        IReadOnlyList<MagicDisciplineUnlockDefinition> disciplineUnlocks =
            classDefinition.MagicDisciplineUnlocks
            ?? throw new InvalidDataException(
                $"Class '{classDefinition.Id}' has a null magic-discipline-unlock list.");

        var learnedAbilities = new List<AbilityDefinition>();
        var seenAbilities = new HashSet<string>(StringComparer.Ordinal);

        foreach (string abilityId in startingAbilityIds)
        {
            AbilityDefinition ability = GetValidatedAbility(abilityId, actor.Id);
            AddFirstOccurrence(ability, seenAbilities, learnedAbilities);
        }

        foreach (AbilityUnlockDefinition unlock in unlocks)
        {
            if (unlock is null)
            {
                throw new InvalidDataException(
                    $"Class '{classDefinition.Id}' contains a null ability unlock.");
            }

            if (unlock.Level < 1)
            {
                throw new InvalidDataException(
                    $"Class '{classDefinition.Id}' unlocks ability '{unlock.AbilityId}' at "
                    + $"invalid level {unlock.Level}; unlock levels must be at least 1.");
            }

            // Resolve every entry, including future-level entries. Production loading already
            // validates the full table, but this defensive boundary also protects tests and
            // editor tools that intentionally assemble an IContentCatalog by hand.
            AbilityDefinition ability = GetValidatedAbility(
                unlock.AbilityId,
                classDefinition.Id);
            if (unlock.Level <= progress.Level)
            {
                AddFirstOccurrence(ability, seenAbilities, learnedAbilities);
            }
        }

        var unlockedDisciplineIds = new List<string>();
        var seenDisciplines = new HashSet<string>(StringComparer.Ordinal);
        foreach (MagicDisciplineUnlockDefinition unlock in disciplineUnlocks)
        {
            if (unlock is null)
            {
                throw new InvalidDataException(
                    $"Class '{classDefinition.Id}' contains a null magic-discipline unlock.");
            }

            if (unlock.Level < 1)
            {
                throw new InvalidDataException(
                    $"Class '{classDefinition.Id}' unlocks magic discipline "
                    + $"'{unlock.MagicDisciplineId}' at invalid level {unlock.Level}; unlock "
                    + "levels must be at least 1.");
            }

            MagicDisciplineDefinition discipline = GetValidatedMagicDiscipline(
                unlock.MagicDisciplineId,
                classDefinition.Id);
            if (unlock.Level <= progress.Level)
            {
                if (seenDisciplines.Add(discipline.Id))
                {
                    unlockedDisciplineIds.Add(discipline.Id);
                }
            }
        }

        List<string> directSkillIds = learnedAbilities
            .Where(ability => string.Equals(
                ability.AbilityKindId,
                AbilityKindIds.Skill,
                StringComparison.Ordinal))
            .Select(ability => ability.Id)
            .ToList();
        List<MagicDisciplineAvailability> magicDisciplines = BuildMagicDisciplines(
            unlockedDisciplineIds,
            learnedAbilities);

        // PartyAbilityAvailability derives its own flat executable view. Keeping that derived
        // value with the structure prevents a future menu and command validator from drifting.
        return new PartyAbilityAvailability(
            directSkillIds,
            magicDisciplines);
    }

    private AbilityDefinition GetValidatedAbility(
        string abilityId,
        string sourceId)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            throw new InvalidDataException(
                $"Ability source '{sourceId}' contains a blank ability ID.");
        }

        AbilityDefinition ability = _content.GetRequired<AbilityDefinition>(abilityId);
        IReadOnlyList<string> magicDisciplineIds = ability.MagicDisciplineIds
            ?? throw new InvalidDataException(
                $"Ability '{ability.Id}' has a null magic-discipline list.");

        switch (ability.AbilityKindId)
        {
            case AbilityKindIds.Skill when magicDisciplineIds.Count > 0:
                throw new InvalidDataException(
                    $"Skill ability '{ability.Id}' cannot reference magic disciplines.");
            case AbilityKindIds.Skill:
                break;
            case AbilityKindIds.Magic when magicDisciplineIds.Count == 0:
                throw new InvalidDataException(
                    $"Magic ability '{ability.Id}' must reference at least one magic discipline.");
            case AbilityKindIds.Magic:
                ValidateMagicAbilityDisciplines(ability, magicDisciplineIds);
                break;
            default:
                throw new InvalidDataException(
                    $"Ability '{ability.Id}' uses unsupported ability kind "
                    + $"'{ability.AbilityKindId}'.");
        }

        return ability;
    }

    private void ValidateMagicAbilityDisciplines(
        AbilityDefinition ability,
        IReadOnlyList<string> magicDisciplineIds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string magicDisciplineId in magicDisciplineIds)
        {
            GetValidatedMagicDiscipline(magicDisciplineId, ability.Id);
            if (!seen.Add(magicDisciplineId))
            {
                throw new InvalidDataException(
                    $"Magic ability '{ability.Id}' references magic discipline "
                    + $"'{magicDisciplineId}' more than once.");
            }
        }
    }

    private MagicDisciplineDefinition GetValidatedMagicDiscipline(
        string magicDisciplineId,
        string sourceId)
    {
        if (string.IsNullOrWhiteSpace(magicDisciplineId))
        {
            throw new InvalidDataException(
                $"Magic discipline source '{sourceId}' contains a blank magic-discipline ID.");
        }

        return _content.GetRequired<MagicDisciplineDefinition>(magicDisciplineId);
    }

    private static void AddFirstOccurrence(
        AbilityDefinition ability,
        ISet<string> seen,
        ICollection<AbilityDefinition> result)
    {
        if (seen.Add(ability.Id))
        {
            result.Add(ability);
        }
    }

    private static List<MagicDisciplineAvailability> BuildMagicDisciplines(
        IReadOnlyList<string> unlockedDisciplineIds,
        IReadOnlyList<AbilityDefinition> learnedAbilities)
    {
        var result = new List<MagicDisciplineAvailability>();
        foreach (string disciplineId in unlockedDisciplineIds)
        {
            List<string> spellIds = learnedAbilities
                .Where(ability => string.Equals(
                    ability.AbilityKindId,
                    AbilityKindIds.Magic,
                    StringComparison.Ordinal))
                .Where(ability => ability.MagicDisciplineIds.Contains(
                    disciplineId,
                    StringComparer.Ordinal))
                .Select(ability => ability.Id)
                .ToList();

            result.Add(new MagicDisciplineAvailability(disciplineId, spellIds));
        }

        return result;
    }

}
