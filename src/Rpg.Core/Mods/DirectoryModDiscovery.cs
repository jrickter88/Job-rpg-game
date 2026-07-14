using System.Text.Json;
using System.Text.Json.Serialization;

namespace RpgGame.Core.Mods;

/// <summary>
/// Discovers, validates, and dependency-orders loose-folder data mods.
/// </summary>
/// <remarks>
/// Only immediate children of the configured mods directory are considered packages. Each
/// package must contain <c>manifest.json</c> and <c>content/</c>. Discovery never loads code,
/// follows an authored executable path, or modifies the installation directory.
/// </remarks>
public sealed class DirectoryModDiscovery
{
    public const int SupportedManifestSchemaVersion = 1;
    public const int SupportedGameApiVersion = 1;

    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Returns no mods when the root does not exist, which makes a first unmodded launch a
    /// normal success. Once packages exist, every package must validate before any is enabled.
    /// </summary>
    public ModDiscoveryResult Discover(string modsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modsDirectory);
        string root = Path.GetFullPath(modsDirectory);

        if (!Directory.Exists(root))
        {
            return new ModDiscoveryResult([], []);
        }

        var problems = new List<ModProblem>();
        var candidates = new List<DiscoveredMod>();

        string[] packageDirectories;
        try
        {
            packageDirectories = Directory
                .EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ModDiscoveryResult(
                [],
                [new ModProblem(root, "$", "mods.read", exception.Message)]);
        }

        foreach (string packageDirectory in packageDirectories)
        {
            TryReadPackage(packageDirectory, candidates, problems);
        }

        ValidateUniqueIds(candidates, problems);

        // Dependency checks need the complete set of valid manifests. They deliberately run
        // after per-package validation so an absent or malformed dependency produces a clear
        // problem instead of a sorting exception.
        IReadOnlyList<DiscoveredMod> orderedMods = ValidateAndOrderDependencies(
            candidates,
            problems);

        IReadOnlyList<ModProblem> orderedProblems = problems
            .OrderBy(problem => problem.FilePath, StringComparer.Ordinal)
            .ThenBy(problem => problem.JsonPath, StringComparer.Ordinal)
            .ThenBy(problem => problem.Code, StringComparer.Ordinal)
            .ToArray();

        return orderedProblems.Count == 0
            ? new ModDiscoveryResult(orderedMods, orderedProblems)
            : new ModDiscoveryResult([], orderedProblems);
    }

    private static void TryReadPackage(
        string packageDirectory,
        ICollection<DiscoveredMod> candidates,
        ICollection<ModProblem> problems)
    {
        string manifestPath = Path.Combine(packageDirectory, "manifest.json");
        string displayManifestPath = manifestPath.Replace('\\', '/');

        if (!File.Exists(manifestPath))
        {
            problems.Add(new ModProblem(
                displayManifestPath,
                "$",
                "manifest.missing",
                "Every mod folder must contain manifest.json."));
            return;
        }

        ModManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ModManifest>(
                File.ReadAllText(manifestPath),
                SerializerOptions);
        }
        catch (Exception exception) when (
            exception is JsonException or IOException or UnauthorizedAccessException)
        {
            string jsonPath = exception is JsonException jsonException
                ? jsonException.Path ?? "$"
                : "$";
            string code = exception is JsonException
                ? "manifest.invalid-json"
                : "manifest.read";
            problems.Add(new ModProblem(
                displayManifestPath,
                jsonPath,
                code,
                exception.Message));
            return;
        }

        if (manifest is null)
        {
            problems.Add(new ModProblem(
                displayManifestPath,
                "$",
                "manifest.null",
                "A manifest must contain one JSON object, not null."));
            return;
        }

        int problemCountBeforeValidation = problems.Count;
        ValidateManifest(manifest, packageDirectory, displayManifestPath, problems);

        string contentDirectory = Path.Combine(packageDirectory, "content");
        if (!Directory.Exists(contentDirectory))
        {
            problems.Add(new ModProblem(
                contentDirectory.Replace('\\', '/'),
                "$",
                "content.missing",
                "A data mod must contain a content directory."));
        }

        if (problems.Count == problemCountBeforeValidation)
        {
            candidates.Add(new DiscoveredMod(
                manifest,
                Path.GetFullPath(packageDirectory),
                Path.GetFullPath(contentDirectory)));
        }
    }

    private static void ValidateManifest(
        ModManifest manifest,
        string packageDirectory,
        string manifestPath,
        ICollection<ModProblem> problems)
    {
        if (manifest.SchemaVersion != SupportedManifestSchemaVersion)
        {
            problems.Add(new ModProblem(
                manifestPath,
                "$.schemaVersion",
                "manifest.schema-unsupported",
                $"Manifest schema {manifest.SchemaVersion} is unsupported; expected {SupportedManifestSchemaVersion}."));
        }

        if (!ModIdentity.IsValidId(manifest.Id))
        {
            problems.Add(new ModProblem(
                manifestPath,
                "$.id",
                "manifest.id-invalid",
                "A mod ID must use the stable lowercase form 'mod.author.mod-name'."));
        }
        else
        {
            string folderName = Path.GetFileName(Path.TrimEndingDirectorySeparator(packageDirectory));
            if (!string.Equals(folderName, manifest.Id, StringComparison.Ordinal))
            {
                problems.Add(new ModProblem(
                    manifestPath,
                    "$.id",
                    "manifest.folder-mismatch",
                    $"Folder '{folderName}' must exactly match mod ID '{manifest.Id}'."));
            }
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
        {
            problems.Add(new ModProblem(
                manifestPath,
                "$.name",
                "manifest.name-empty",
                "A mod name cannot be blank."));
        }

        if (!ModIdentity.IsValidVersion(manifest.Version))
        {
            problems.Add(new ModProblem(
                manifestPath,
                "$.version",
                "manifest.version-invalid",
                "Version must use Semantic Version form such as '1.0.0' or '1.0.0-beta.1'."));
        }

        if (manifest.GameApiVersion != SupportedGameApiVersion)
        {
            problems.Add(new ModProblem(
                manifestPath,
                "$.gameApiVersion",
                "manifest.api-unsupported",
                $"Game API {manifest.GameApiVersion} is unsupported; expected {SupportedGameApiVersion}."));
        }

        if (manifest.Dependencies is null)
        {
            problems.Add(new ModProblem(
                manifestPath,
                "$.dependencies",
                "manifest.dependencies-null",
                "dependencies must be an array, not null."));
            return;
        }

        var seenDependencies = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < manifest.Dependencies.Count; index++)
        {
            string? dependencyId = manifest.Dependencies[index];
            string jsonPath = $"$.dependencies[{index}]";

            if (!ModIdentity.IsValidId(dependencyId))
            {
                problems.Add(new ModProblem(
                    manifestPath,
                    jsonPath,
                    "dependency.id-invalid",
                    $"'{dependencyId}' is not a valid mod ID."));
                continue;
            }

            if (!seenDependencies.Add(dependencyId!))
            {
                problems.Add(new ModProblem(
                    manifestPath,
                    jsonPath,
                    "dependency.duplicate",
                    $"Dependency '{dependencyId}' is listed more than once."));
            }

            if (string.Equals(dependencyId, manifest.Id, StringComparison.Ordinal))
            {
                problems.Add(new ModProblem(
                    manifestPath,
                    jsonPath,
                    "dependency.self",
                    "A mod cannot depend on itself."));
            }
        }
    }

    private static void ValidateUniqueIds(
        IReadOnlyList<DiscoveredMod> candidates,
        ICollection<ModProblem> problems)
    {
        var firstRootById = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (DiscoveredMod candidate in candidates)
        {
            if (!firstRootById.TryAdd(candidate.Manifest.Id, candidate.RootDirectory))
            {
                problems.Add(new ModProblem(
                    Path.Combine(candidate.RootDirectory, "manifest.json").Replace('\\', '/'),
                    "$.id",
                    "manifest.id-duplicate",
                    $"Mod ID '{candidate.Manifest.Id}' is already installed at '{firstRootById[candidate.Manifest.Id]}'."));
            }
        }
    }

    private static IReadOnlyList<DiscoveredMod> ValidateAndOrderDependencies(
        IReadOnlyList<DiscoveredMod> candidates,
        ICollection<ModProblem> problems)
    {
        // Duplicate manifests are already errors. Grouping avoids throwing here while the
        // discovery pass finishes collecting everything actionable.
        Dictionary<string, DiscoveredMod> byId = candidates
            .GroupBy(candidate => candidate.Manifest.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        foreach (DiscoveredMod candidate in byId.Values)
        {
            for (int index = 0; index < candidate.Manifest.Dependencies.Count; index++)
            {
                string dependencyId = candidate.Manifest.Dependencies[index];
                if (!byId.ContainsKey(dependencyId))
                {
                    problems.Add(new ModProblem(
                        Path.Combine(candidate.RootDirectory, "manifest.json").Replace('\\', '/'),
                        $"$.dependencies[{index}]",
                        "dependency.missing",
                        $"Required mod '{dependencyId}' is not installed and valid."));
                }
            }
        }

        if (problems.Count > 0)
        {
            return [];
        }

        var dependencyCount = byId.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Manifest.Dependencies.Count,
            StringComparer.Ordinal);
        var dependentsById = byId.Keys.ToDictionary(
            id => id,
            _ => new List<string>(),
            StringComparer.Ordinal);

        foreach (DiscoveredMod candidate in byId.Values)
        {
            foreach (string dependencyId in candidate.Manifest.Dependencies)
            {
                dependentsById[dependencyId].Add(candidate.Manifest.Id);
            }
        }

        var ready = new SortedSet<string>(
            dependencyCount.Where(pair => pair.Value == 0).Select(pair => pair.Key),
            StringComparer.Ordinal);
        var ordered = new List<DiscoveredMod>(byId.Count);

        while (ready.Count > 0)
        {
            string nextId = ready.Min!;
            ready.Remove(nextId);
            ordered.Add(byId[nextId]);

            foreach (string dependentId in dependentsById[nextId].Order(StringComparer.Ordinal))
            {
                dependencyCount[dependentId]--;
                if (dependencyCount[dependentId] == 0)
                {
                    ready.Add(dependentId);
                }
            }
        }

        if (ordered.Count != byId.Count)
        {
            string cycleIds = string.Join(
                ", ",
                dependencyCount
                    .Where(pair => pair.Value > 0)
                    .Select(pair => pair.Key)
                    .Order(StringComparer.Ordinal));
            problems.Add(new ModProblem(
                "<mods>",
                "$.dependencies",
                "dependency.cycle",
                $"Mod dependencies contain a cycle involving: {cycleIds}."));
            return [];
        }

        return ordered;
    }

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
