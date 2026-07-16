using Godot;
using RpgGame.Core.Content.Loading;

namespace RpgGame.Adapters.Content;

/// <summary>Reads recursive localization bundles from Godot's virtual filesystem.</summary>
public sealed class GodotLocalizationSource
{
    private readonly string _rootPath;

    public GodotLocalizationSource(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = rootPath.TrimEnd('/');
    }

    public IReadOnlyList<LocalizationBundleDocument> ReadAll()
    {
        if (!DirAccess.DirExistsAbsolute(_rootPath))
        {
            throw new DirectoryNotFoundException(
                $"Localization path '{_rootPath}' does not exist.");
        }

        var documents = new List<LocalizationBundleDocument>();
        ReadDirectory(_rootPath, string.Empty, documents);
        return documents
            .OrderBy(document => document.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private void ReadDirectory(
        string directoryPath,
        string relativeDirectory,
        ICollection<LocalizationBundleDocument> documents)
    {
        foreach (string fileName in DirAccess.GetFilesAt(directoryPath)
                     .Where(file => file.EndsWith(".json", StringComparison.Ordinal)))
        {
            string relativePath = string.IsNullOrEmpty(relativeDirectory)
                ? fileName
                : $"{relativeDirectory}/{fileName}";
            string path = $"{directoryPath}/{fileName}";
            documents.Add(new LocalizationBundleDocument(
                relativePath,
                global::Godot.FileAccess.GetFileAsString(path)));
        }

        foreach (string childDirectory in DirAccess.GetDirectoriesAt(directoryPath))
        {
            string childRelativeDirectory = string.IsNullOrEmpty(relativeDirectory)
                ? childDirectory
                : $"{relativeDirectory}/{childDirectory}";
            ReadDirectory(
                $"{directoryPath}/{childDirectory}",
                childRelativeDirectory,
                documents);
        }
    }
}
