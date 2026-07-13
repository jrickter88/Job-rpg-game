namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Result of loading the complete content pack. A catalog is published only if every problem
/// was resolved; gameplay never receives a knowingly partial database.
/// </summary>
public sealed class ContentLoadResult
{
    internal ContentLoadResult(ContentCatalog? catalog, IReadOnlyList<ContentProblem> problems)
    {
        Catalog = catalog;
        Problems = problems;
    }

    /// <summary>Validated immutable catalog, or null when any problem exists.</summary>
    public ContentCatalog? Catalog { get; }

    /// <summary>All parse/reference/range problems discovered in this pass.</summary>
    public IReadOnlyList<ContentProblem> Problems { get; }

    /// <summary>True only when a complete catalog is available.</summary>
    public bool IsSuccess => Catalog is not null && Problems.Count == 0;
}
