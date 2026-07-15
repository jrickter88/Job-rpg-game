using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Combat;

/// <summary>
/// Resolves the abilities one party actor may use from intrinsic actor grants followed by
/// level-eligible grants from the class selected in this campaign.
/// </summary>
public sealed class AbilityAvailabilityResolver
{
    private readonly IContentCatalog _content;

    public AbilityAvailabilityResolver(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

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
            AddValidatedAbility(abilityId, actor.Id, seenAbilities, learnedAbilities);
        }

        foreach (AbilityUnlockDefinition unlock in unlocks)
        {
            if (unlock is null)
            {
                throw new InvalidDataException(
                    $"Class '{classDefinition.Id}' contains a null ability unlock.");
            }

            if (unlock.Level <= progress.Level)
            {
                AddValidatedAbility(unlock.AbilityId, classDefinition.Id, seenAbilities, learnedAbilities);
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

            if (unlock.Level <= progress.Level)
            {
                AddValidatedMagicDiscipline(
                    unlock.MagicDisciplineId,
                    classDefinition.Id,
                    seenDisciplines,
                    unlockedDisciplineIds);
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
        List<string> executableAbilityIds = BuildExecutableAbilityIds(
            directSkillIds,
            magicDisciplines);

        return new PartyAbilityAvailability(
            directSkillIds,
            magicDisciplines,
            executableAbilityIds);
    }

    private void AddValidatedAbility(
        string abilityId,
        string sourceId,
        ISet<string> seen,
        ICollection<AbilityDefinition> result)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            throw new InvalidDataException(
                $"Ability source '{sourceId}' contains a blank ability ID.");
        }

        AbilityDefinition ability = _content.GetRequired<AbilityDefinition>(abilityId);
        if (seen.Add(abilityId))
        {
            result.Add(ability);
        }
    }

    private void AddValidatedMagicDiscipline(
        string magicDisciplineId,
        string sourceId,
        ISet<string> seen,
        ICollection<string> result)
    {
        if (string.IsNullOrWhiteSpace(magicDisciplineId))
        {
            throw new InvalidDataException(
                $"Magic discipline source '{sourceId}' contains a blank magic-discipline ID.");
        }

        _content.GetRequired<MagicDisciplineDefinition>(magicDisciplineId);
        if (seen.Add(magicDisciplineId))
        {
            result.Add(magicDisciplineId);
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

    private static List<string> BuildExecutableAbilityIds(
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

        return result;
    }
}
