namespace RpgGame.Core.Mods;

/// <summary>One actionable manifest, installation, or dependency validation failure.</summary>
/// <param name="FilePath">Manifest or directory associated with the failure.</param>
/// <param name="JsonPath">JSON path for authored fields, or <c>$</c> for directory errors.</param>
/// <param name="Code">Stable machine-readable problem category.</param>
/// <param name="Message">Explanation intended for a mod author or player.</param>
public sealed record ModProblem(
    string FilePath,
    string JsonPath,
    string Code,
    string Message)
{
    public override string ToString() => $"{FilePath} {JsonPath}: {Message} [{Code}]";
}

/// <summary>
/// All-or-nothing result from discovering and ordering a loose-folder mod installation.
/// </summary>
public sealed record ModDiscoveryResult(
    IReadOnlyList<DiscoveredMod> Mods,
    IReadOnlyList<ModProblem> Problems)
{
    public bool IsSuccess => Problems.Count == 0;
}
