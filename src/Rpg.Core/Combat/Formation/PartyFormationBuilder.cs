using RpgGame.Core.State;

namespace RpgGame.Core.Combat.Formation;

/// <summary>Creates the temporary Milestone 2.75 party arrangement from active-party order.</summary>
public static class PartyFormationBuilder
{
    /// <summary>
    /// Places party index 0..3 in row 0..3 of front column zero, one cell each.
    /// </summary>
    public static IReadOnlyList<FormationPlacement> Build(
        IReadOnlyList<string> activePartyActorIds)
    {
        ArgumentNullException.ThrowIfNull(activePartyActorIds);
        PartyRules.ValidateMemberCount(activePartyActorIds.Count, nameof(activePartyActorIds));

        var placements = new List<FormationPlacement>(activePartyActorIds.Count);
        for (int index = 0; index < activePartyActorIds.Count; index++)
        {
            string actorId = activePartyActorIds[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
            placements.Add(new FormationPlacement(
                $"party-{index}",
                actorId,
                new FormationCell(BattleSide.Party, index, 0),
                FormationFootprint.SingleCell));
        }

        IReadOnlyList<FormationProblem> problems =
            BattleFormationRules.ValidatePlacements(placements);
        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "The fixed party formation is invalid: "
                + string.Join(
                    "; ",
                    problems.Select(problem =>
                        $"{problem.InstanceId} {problem.Kind}")));
        }

        return placements;
    }
}
