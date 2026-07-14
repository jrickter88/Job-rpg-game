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

    public IReadOnlyList<string> ResolvePartyActor(ActorProgressState progress)
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

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (string abilityId in startingAbilityIds)
        {
            AddValidatedAbility(abilityId, actor.Id, seen, result);
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
                AddValidatedAbility(unlock.AbilityId, classDefinition.Id, seen, result);
            }
        }

        return Array.AsReadOnly(result.ToArray());
    }

    private void AddValidatedAbility(
        string abilityId,
        string sourceId,
        ISet<string> seen,
        ICollection<string> result)
    {
        if (string.IsNullOrWhiteSpace(abilityId))
        {
            throw new InvalidDataException(
                $"Ability source '{sourceId}' contains a blank ability ID.");
        }

        _content.GetRequired<AbilityDefinition>(abilityId);
        if (seen.Add(abilityId))
        {
            result.Add(abilityId);
        }
    }
}
