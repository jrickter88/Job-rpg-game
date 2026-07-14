namespace RpgGame.Core.Combat.Formation;

/// <summary>The side-relative grid on which one combatant is placed.</summary>
public enum BattleSide
{
    Enemy,
    Party,
}

/// <summary>
/// One logical formation cell. Rows increase downward; columns increase backward from the
/// opposing side, so column zero means "front" for both enemies and party members.
/// </summary>
public readonly record struct FormationCell(BattleSide Side, int Row, int Column);

/// <summary>
/// Rectangular cell count occupied by one combatant, expressed as rows by columns.
/// </summary>
public readonly record struct FormationFootprint(int Rows, int Columns)
{
    public static FormationFootprint SingleCell { get; } = new(1, 1);
}

/// <summary>
/// One battle-local combatant placement using content identity and logical coordinates only.
/// </summary>
/// <param name="InstanceId">Deterministic battle-local identity such as enemy-0.</param>
/// <param name="DefinitionId">Stable actor or enemy content ID.</param>
/// <param name="Anchor">
/// Top-front occupied cell. The footprint extends toward increasing rows and columns.
/// </param>
/// <param name="Footprint">Rectangular number of occupied rows and columns.</param>
public sealed record FormationPlacement(
    string InstanceId,
    string DefinitionId,
    FormationCell Anchor,
    FormationFootprint Footprint);

/// <summary>Machine-readable reason that a set of formation placements is invalid.</summary>
public enum FormationProblemKind
{
    InvalidFootprint,
    OutOfBounds,
    DuplicateInstanceId,
    Overlap,
}

/// <summary>
/// Pure validation result that content tooling can translate into file/JSON diagnostics.
/// </summary>
/// <param name="Kind">Rule that failed.</param>
/// <param name="InstanceId">Placement for which the problem is reported.</param>
/// <param name="ConflictingInstanceId">Other placement involved, when applicable.</param>
/// <param name="Cells">Relevant cells in deterministic row/column order.</param>
public sealed record FormationProblem(
    FormationProblemKind Kind,
    string InstanceId,
    string? ConflictingInstanceId,
    IReadOnlyList<FormationCell> Cells);
