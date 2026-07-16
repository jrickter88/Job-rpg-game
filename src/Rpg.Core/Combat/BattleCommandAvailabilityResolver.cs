using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Combat.Formation;

namespace RpgGame.Core.Combat;

/// <summary>
/// Projects one party combatant's learned Skill and Magic structure into commands the current
/// resolver can execute from its present transient state.
/// </summary>
/// <remarks>
/// This is pure data for a battle presentation, not a Godot menu. It keeps UI from offering an
/// ability that lacks an implemented contract or enough current MP while preserving authored
/// direct-Skill, discipline, and spell ordering.
/// </remarks>
public sealed class BattleCommandAvailabilityResolver
{
    private readonly IContentCatalog _content;

    public BattleCommandAvailabilityResolver(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public BattleCommandAvailability Resolve(CombatantSnapshot partyCombatant)
    {
        ArgumentNullException.ThrowIfNull(partyCombatant);
        if (partyCombatant.Side != BattleSide.Party)
        {
            throw new ArgumentException(
                $"Combatant '{partyCombatant.InstanceId}' is not on the party side.",
                nameof(partyCombatant));
        }

        PartyAbilityAvailability availability = partyCombatant.PartyAbilityAvailability
            ?? throw new InvalidDataException(
                $"Party combatant '{partyCombatant.InstanceId}' has no structured ability "
                + "availability.");

        IReadOnlyList<string> directAbilityIds = ProjectAbilityIds(
            partyCombatant,
            availability.DirectSkillIds);
        var magicDisciplines = new List<MagicBattleCommandAvailability>(
            availability.MagicDisciplines.Count);
        foreach (MagicDisciplineAvailability discipline in availability.MagicDisciplines)
        {
            magicDisciplines.Add(new MagicBattleCommandAvailability(
                discipline.MagicDisciplineId,
                ProjectAbilityIds(partyCombatant, discipline.SpellAbilityIds)));
        }

        return new BattleCommandAvailability(directAbilityIds, magicDisciplines);
    }

    private IReadOnlyList<string> ProjectAbilityIds(
        CombatantSnapshot actor,
        IReadOnlyList<string> abilityIds)
    {
        var result = new List<string>(abilityIds.Count);
        foreach (string abilityId in abilityIds)
        {
            AbilityDefinition ability = _content.GetRequired<AbilityDefinition>(abilityId);
            if (CombatAbilityExecutionSupport.IsCurrentlyUsable(actor, ability))
            {
                result.Add(abilityId);
            }
        }

        return result;
    }
}

/// <summary>Ordered executable command groups for one party combatant.</summary>
public sealed record BattleCommandAvailability
{
    public BattleCommandAvailability(
        IReadOnlyList<string> directAbilityIds,
        IReadOnlyList<MagicBattleCommandAvailability> magicDisciplines)
    {
        ArgumentNullException.ThrowIfNull(directAbilityIds);
        ArgumentNullException.ThrowIfNull(magicDisciplines);
        DirectAbilityIds = Array.AsReadOnly(directAbilityIds.ToArray());
        MagicDisciplines = Array.AsReadOnly(magicDisciplines.ToArray());
    }

    public IReadOnlyList<string> DirectAbilityIds { get; }

    /// <summary>
    /// Every unlocked Magic discipline, including containers with no currently usable spell.
    /// </summary>
    public IReadOnlyList<MagicBattleCommandAvailability> MagicDisciplines { get; }
}

/// <summary>One Magic submenu and its ordered currently executable spell IDs.</summary>
public sealed record MagicBattleCommandAvailability
{
    public MagicBattleCommandAvailability(
        string magicDisciplineId,
        IReadOnlyList<string> spellAbilityIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(magicDisciplineId);
        ArgumentNullException.ThrowIfNull(spellAbilityIds);
        MagicDisciplineId = magicDisciplineId;
        SpellAbilityIds = Array.AsReadOnly(spellAbilityIds.ToArray());
    }

    public string MagicDisciplineId { get; }

    public IReadOnlyList<string> SpellAbilityIds { get; }
}
