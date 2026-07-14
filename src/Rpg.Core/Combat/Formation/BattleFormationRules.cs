namespace RpgGame.Core.Combat.Formation;

/// <summary>
/// Small code-owned battlefield dimensions and rectangular-placement rules.
/// </summary>
/// <remarks>
/// This is intentionally not a tactical grid engine. It answers only the bounds, occupied
/// cells, duplicate identity, and overlap questions required by the fixed battle slice.
/// </remarks>
public static class BattleFormationRules
{
    public const int RowCount = 4;
    public const int EnemyColumnCount = 4;
    public const int PartyColumnCount = 2;

    /// <summary>Returns the fixed number of side-relative columns for one battle side.</summary>
    public static int GetColumnCount(BattleSide side) => side switch
    {
        BattleSide.Enemy => EnemyColumnCount,
        BattleSide.Party => PartyColumnCount,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unknown battle side."),
    };

    /// <summary>True when the row and side-relative column identify a real cell.</summary>
    public static bool Contains(FormationCell cell) =>
        cell.Row >= 0
        && cell.Row < RowCount
        && cell.Column >= 0
        && cell.Column < GetColumnCount(cell.Side);

    /// <summary>
    /// Enumerates a placement from its top-front anchor in row-major order.
    /// </summary>
    /// <remarks>
    /// A 2 × 2 enemy at row 1, column 0 yields (1,0), (1,1), (2,0), (2,1).
    /// Bounds are validated separately so diagnostics can include cells that left the grid.
    /// </remarks>
    public static IReadOnlyList<FormationCell> GetOccupiedCells(
        FormationPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        if (!HasPositiveFootprint(placement.Footprint))
        {
            throw new ArgumentOutOfRangeException(
                nameof(placement),
                placement.Footprint,
                "A formation footprint must have at least one row and one column.");
        }

        var cells = new List<FormationCell>(
            placement.Footprint.Rows * placement.Footprint.Columns);
        for (int rowOffset = 0; rowOffset < placement.Footprint.Rows; rowOffset++)
        {
            for (int columnOffset = 0;
                 columnOffset < placement.Footprint.Columns;
                 columnOffset++)
            {
                cells.Add(new FormationCell(
                    placement.Anchor.Side,
                    placement.Anchor.Row + rowOffset,
                    placement.Anchor.Column + columnOffset));
            }
        }

        return cells;
    }

    /// <summary>
    /// Aggregates every invalid footprint, boundary, duplicate-ID, and same-side overlap.
    /// </summary>
    public static IReadOnlyList<FormationProblem> ValidatePlacements(
        IEnumerable<FormationPlacement> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);
        FormationPlacement[] orderedPlacements = placements.ToArray();
        var problems = new List<FormationProblem>();
        var firstPlacementByInstanceId = new Dictionary<string, FormationPlacement>(
            StringComparer.Ordinal);
        var validPlacements = new List<ValidatedPlacement>();

        foreach (FormationPlacement placement in orderedPlacements)
        {
            ArgumentNullException.ThrowIfNull(placement);

            if (!firstPlacementByInstanceId.TryAdd(placement.InstanceId, placement))
            {
                problems.Add(new FormationProblem(
                    FormationProblemKind.DuplicateInstanceId,
                    placement.InstanceId,
                    placement.InstanceId,
                    []));
            }

            if (!HasPositiveFootprint(placement.Footprint))
            {
                problems.Add(new FormationProblem(
                    FormationProblemKind.InvalidFootprint,
                    placement.InstanceId,
                    null,
                    []));
                continue;
            }

            IReadOnlyList<FormationCell> occupiedCells = GetOccupiedCells(placement);
            FormationCell[] outsideCells = occupiedCells
                .Where(cell => !Contains(cell))
                .OrderBy(cell => cell.Row)
                .ThenBy(cell => cell.Column)
                .ToArray();
            if (outsideCells.Length > 0)
            {
                problems.Add(new FormationProblem(
                    FormationProblemKind.OutOfBounds,
                    placement.InstanceId,
                    null,
                    outsideCells));
                continue;
            }

            validPlacements.Add(new ValidatedPlacement(placement, occupiedCells));
        }

        // Pairwise comparison is tiny at the supported party/encounter sizes and reports all
        // conflicts deterministically, including two large rectangles sharing several cells.
        for (int firstIndex = 0; firstIndex < validPlacements.Count; firstIndex++)
        {
            ValidatedPlacement first = validPlacements[firstIndex];
            for (int secondIndex = firstIndex + 1;
                 secondIndex < validPlacements.Count;
                 secondIndex++)
            {
                ValidatedPlacement second = validPlacements[secondIndex];
                if (first.Placement.Anchor.Side != second.Placement.Anchor.Side)
                {
                    continue;
                }

                FormationCell[] overlap = first.Cells
                    .Intersect(second.Cells)
                    .OrderBy(cell => cell.Row)
                    .ThenBy(cell => cell.Column)
                    .ToArray();
                if (overlap.Length > 0)
                {
                    problems.Add(new FormationProblem(
                        FormationProblemKind.Overlap,
                        second.Placement.InstanceId,
                        first.Placement.InstanceId,
                        overlap));
                }
            }
        }

        return problems;
    }

    private static bool HasPositiveFootprint(FormationFootprint footprint) =>
        footprint.Rows > 0 && footprint.Columns > 0;

    private sealed record ValidatedPlacement(
        FormationPlacement Placement,
        IReadOnlyList<FormationCell> Cells);
}
