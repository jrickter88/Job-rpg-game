using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Combat.Formation;

/// <summary>Builds deterministic enemy placements from validated encounter content.</summary>
public sealed class EncounterFormationBuilder
{
    private readonly IContentCatalog _content;

    public EncounterFormationBuilder(IContentCatalog content)
    {
        ArgumentNullException.ThrowIfNull(content);
        _content = content;
    }

    /// <summary>
    /// Preserves authored encounter order and assigns enemy-0, enemy-1, and so on.
    /// </summary>
    public IReadOnlyList<FormationPlacement> Build(EncounterDefinition encounter)
    {
        ArgumentNullException.ThrowIfNull(encounter);
        var placements = new List<FormationPlacement>(encounter.EnemyGroup.Count);

        for (int index = 0; index < encounter.EnemyGroup.Count; index++)
        {
            EncounterEnemyDefinition source = encounter.EnemyGroup[index]
                ?? throw new InvalidDataException(
                    $"Encounter '{encounter.Id}' contains a null enemy placement at {index}.");
            EnemyDefinition enemy = _content.GetRequired<EnemyDefinition>(source.EnemyId);
            if (!FormationSlotId.TryParseEnemy(source.SlotId, out FormationCell anchor))
            {
                throw new InvalidDataException(
                    $"Encounter '{encounter.Id}' has invalid enemy slot '{source.SlotId}'.");
            }

            EnemyFootprintDefinition authoredFootprint = enemy.FormationFootprint
                ?? throw new InvalidDataException(
                    $"Enemy '{enemy.Id}' has a null formation footprint.");
            placements.Add(new FormationPlacement(
                $"enemy-{index}",
                enemy.Id,
                anchor,
                authoredFootprint.ToFormationFootprint()));
        }

        IReadOnlyList<FormationProblem> problems =
            BattleFormationRules.ValidatePlacements(placements);
        if (problems.Count > 0)
        {
            throw new InvalidDataException(
                $"Encounter '{encounter.Id}' produced an invalid formation: "
                + string.Join(
                    "; ",
                    problems.Select(problem =>
                        $"{problem.InstanceId} {problem.Kind}")));
        }

        return placements;
    }
}
