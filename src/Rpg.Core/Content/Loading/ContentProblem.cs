namespace RpgGame.Core.Content.Loading;

/// <summary>
/// One actionable parse or validation failure tied to its source file and JSON location.
/// </summary>
/// <param name="FilePath">
/// Stable source ID plus relative content path, or the source root for source failures.
/// </param>
/// <param name="JsonPath">JSON path such as <c>$.startingClassId</c>.</param>
/// <param name="Code">Stable machine-readable category for tests and future tooling.</param>
/// <param name="Message">Concise explanation intended for a content author.</param>
public sealed record ContentProblem(
    string FilePath,
    string JsonPath,
    string Code,
    string Message)
{
    public override string ToString() => $"{FilePath} {JsonPath}: {Message} [{Code}]";
}
