using RpgGame.Core.Content;
using RpgGame.Core.Content.Loading;

namespace RpgGame.Core.Tests;

/// <summary>
/// Test-only access to the checked-in fixture pack. Searching upward instead of assuming
/// a working directory lets the same tests run from Visual Studio, <c>dotnet test</c>, or CI.
/// </summary>
internal static class TestContent
{
    public static ContentCatalog LoadCatalog()
    {
        string contentDirectory = Path.Combine(FindRepositoryRoot(), "game", "content");
        ContentLoadResult result = new JsonContentLoader().Load(
            new DirectoryContentSource(contentDirectory));

        if (!result.IsSuccess)
        {
            string details = string.Join(Environment.NewLine, result.Problems);
            throw new InvalidOperationException(
                $"The checked-in content pack is invalid:{Environment.NewLine}{details}");
        }

        return result.Catalog!;
    }

    private static string FindRepositoryRoot()
    {
        string[] startingPoints = [Directory.GetCurrentDirectory(), AppContext.BaseDirectory];

        foreach (string startingPoint in startingPoints)
        {
            DirectoryInfo? directory = new(startingPoint);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing project.godot.");
    }
}
