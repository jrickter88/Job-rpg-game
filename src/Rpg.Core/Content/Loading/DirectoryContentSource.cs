namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Plain .NET content source used by tests, command-line tools, and loose-file development.
/// </summary>
/// <remarks>
/// Exported Godot games may pack <c>res://</c> files into a PCK, so runtime Godot startup uses
/// a separate FileAccess/DirAccess adapter. Both adapters feed identical ContentDocument data
/// into the same loader.
/// </remarks>
public sealed class DirectoryContentSource : IContentSource
{
    private readonly string _rootDirectory;

    public DirectoryContentSource(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <inheritdoc />
    public IReadOnlyList<ContentDocument> ReadAll()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Content directory '{_rootDirectory}' does not exist.");
        }

        return Directory
            .EnumerateFiles(_rootDirectory, "*.json", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new ContentDocument(
                Path.GetRelativePath(_rootDirectory, path).Replace('\\', '/'),
                File.ReadAllText(path)))
            .ToArray();
    }
}
