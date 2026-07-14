namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Plain .NET content source used by tests, command-line tools, and loose data mods.
/// </summary>
/// <remarks>
/// Exported Godot games may pack built-in <c>res://</c> files, so those records use a separate
/// FileAccess/DirAccess adapter. Community folders beneath <c>user://mods</c> remain ordinary
/// files. Both adapters feed identical ContentDocument data into the same loader.
/// </remarks>
public sealed class DirectoryContentSource : IContentSource
{
    private readonly string _rootDirectory;

    /// <summary>
    /// Creates a source for the built-in content pack. Explicit source IDs should be used for
    /// mods so their records can be namespaced and their errors can name the responsible mod.
    /// </summary>
    public DirectoryContentSource(string rootDirectory)
        : this(ContentSourceIds.Base, rootDirectory, [])
    {
    }

    public DirectoryContentSource(
        string sourceId,
        string rootDirectory,
        IEnumerable<string>? declaredDependencyIds = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        SourceId = sourceId;
        DeclaredDependencyIds = (declaredDependencyIds ?? [])
            .Order(StringComparer.Ordinal)
            .ToArray();
        _rootDirectory = Path.GetFullPath(rootDirectory);
    }

    /// <inheritdoc />
    public string SourceId { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<string> DeclaredDependencyIds { get; }

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
