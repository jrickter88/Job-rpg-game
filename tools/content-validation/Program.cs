using RpgGame.Core.Content.Loading;
using RpgGame.Core.Mods;

// This process intentionally has one responsibility: run the production content loader,
// print every actionable problem, and expose success/failure through its exit code for CI.
if (args.Length > 2)
{
    Console.Error.WriteLine(
        "Usage: content-validation [path-to-content-directory] [path-to-mods-directory]");
    return 2;
}

string requestedPath = args.Length >= 1
    ? args[0]
    : Path.Combine("game", "content");
string contentDirectory = Path.GetFullPath(requestedPath);
string localizationDirectory = Path.Combine(
    Directory.GetParent(contentDirectory)?.FullName
        ?? throw new InvalidOperationException("Content directory has no parent directory."),
    "localization",
    "en");

if (!Directory.Exists(localizationDirectory))
{
    Console.Error.WriteLine(
        $"Localization directory '{localizationDirectory}' does not exist.");
    return 1;
}

var localizationDocuments = Directory
    .EnumerateFiles(localizationDirectory, "*.json", SearchOption.AllDirectories)
    .OrderBy(path => path, StringComparer.Ordinal)
    .Select(path => new LocalizationBundleDocument(
        Path.GetRelativePath(localizationDirectory, path).Replace('\\', '/'),
        File.ReadAllText(path)))
    .ToArray();
LocalizationLoadResult localizationResult = new LocalizationBundleLoader().Load(
    "en",
    localizationDocuments);

if (!localizationResult.IsSuccess)
{
    Console.Error.WriteLine(
        $"Localization validation failed with {localizationResult.Problems.Count} problem(s):");
    foreach (LocalizationProblem problem in localizationResult.Problems)
    {
        Console.Error.WriteLine($"  {problem}");
    }

    return 1;
}

var loader = new JsonContentLoader();
var sources = new List<IContentSource>
{
    new DirectoryContentSource(ContentSourceIds.Base, contentDirectory),
};

IReadOnlyList<DiscoveredMod> mods = [];
if (args.Length == 2)
{
    string modsDirectory = Path.GetFullPath(args[1]);
    ModDiscoveryResult modResult = new DirectoryModDiscovery().Discover(modsDirectory);

    if (!modResult.IsSuccess)
    {
        Console.Error.WriteLine(
            $"Data-mod validation failed with {modResult.Problems.Count} problem(s):");

        foreach (ModProblem problem in modResult.Problems)
        {
            Console.Error.WriteLine($"  {problem}");
        }

        return 1;
    }

    mods = modResult.Mods;
    sources.AddRange(mods.Select(mod =>
        new DirectoryContentSource(
            mod.Manifest.Id,
            mod.ContentDirectory,
            mod.Manifest.Dependencies)));
}

ContentLoadResult result = loader.Load(sources, localizationResult.Catalog);

if (!result.IsSuccess)
{
    Console.Error.WriteLine(
        $"Content validation failed with {result.Problems.Count} problem(s):");

    foreach (ContentProblem problem in result.Problems)
    {
        Console.Error.WriteLine($"  {problem}");
    }

    return 1;
}

Console.WriteLine(
    $"Content validation passed: {result.Catalog!.Count} definitions loaded from "
    + $"'{contentDirectory}' with {mods.Count} data mod(s).");
return 0;
