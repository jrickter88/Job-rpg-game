using Godot;
using RpgGame.Core.Content.Loading;

namespace RpgGame.Adapters.Content;

/// <summary>
/// Reads authored JSON through Godot's virtual filesystem, which works with <c>res://</c>
/// paths in the editor and with files included in an exported PCK.
/// </summary>
public sealed class GodotContentSource : IContentSource
{
    private readonly string _rootPath;

    public GodotContentSource(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = rootPath.TrimEnd('/');
    }

    /// <inheritdoc />
    public IReadOnlyList<ContentDocument> ReadAll()
    {
        if (!DirAccess.DirExistsAbsolute(_rootPath))
        {
            throw new DirectoryNotFoundException($"Godot content path '{_rootPath}' does not exist.");
        }

        var documents = new List<ContentDocument>();
        ReadDirectory(_rootPath, documents);

        return documents
            .OrderBy(document => document.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private void ReadDirectory(string directoryPath, ICollection<ContentDocument> documents)
    {
        foreach (string fileName in DirAccess.GetFilesAt(directoryPath))
        {
            if (!fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string fullPath = $"{directoryPath}/{fileName}";
            string json = Godot.FileAccess.GetFileAsString(fullPath);
            if (Godot.FileAccess.GetOpenError() != Error.Ok)
            {
                throw new IOException($"Godot could not read '{fullPath}'.");
            }

            string relativePath = fullPath[(_rootPath.Length + 1)..];
            documents.Add(new ContentDocument(relativePath, json));
        }

        foreach (string childDirectory in DirAccess.GetDirectoriesAt(directoryPath))
        {
            ReadDirectory($"{directoryPath}/{childDirectory}", documents);
        }
    }
}
