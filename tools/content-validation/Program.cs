using RpgGame.Core.Content.Loading;

// This process intentionally has one responsibility: run the production content loader,
// print every actionable problem, and expose success/failure through its exit code for CI.
if (args.Length > 1)
{
    Console.Error.WriteLine("Usage: content-validation [path-to-content-directory]");
    return 2;
}

string requestedPath = args.Length == 1
    ? args[0]
    : Path.Combine("game", "content");
string contentDirectory = Path.GetFullPath(requestedPath);

var loader = new JsonContentLoader();
ContentLoadResult result = loader.Load(new DirectoryContentSource(contentDirectory));

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
    + $"'{contentDirectory}'.");
return 0;
