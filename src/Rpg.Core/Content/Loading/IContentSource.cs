namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Platform boundary that supplies raw authored JSON without exposing filesystem or Godot APIs
/// to the loader and validator.
/// </summary>
public interface IContentSource
{
    /// <summary>
    /// Stable origin used in diagnostics and namespace enforcement. The built-in pack uses
    /// <see cref="ContentSourceIds.Base"/>; a mod uses its manifest ID.
    /// </summary>
    string SourceId { get; }

    /// <summary>
    /// Direct mod IDs this source is allowed to reference. The base source and simple test
    /// sources default to none; loose mod sources copy the validated manifest list.
    /// </summary>
    IReadOnlyCollection<string> DeclaredDependencyIds => Array.Empty<string>();

    /// <summary>
    /// Reads every JSON document beneath the logical content root. Paths must be relative,
    /// slash-separated, and begin with a known category folder such as <c>actors/</c>.
    /// </summary>
    IReadOnlyList<ContentDocument> ReadAll();
}

/// <summary>
/// Raw source document before its category is known or its JSON has been deserialized.
/// </summary>
/// <param name="RelativePath">Diagnostic/category path relative to the content root.</param>
/// <param name="Json">Complete UTF-8 JSON text.</param>
public sealed record ContentDocument(string RelativePath, string Json);

/// <summary>Reserved identities understood by the content-loading boundary.</summary>
public static class ContentSourceIds
{
    /// <summary>The game-owned content shipped beneath <c>res://game/content</c>.</summary>
    public const string Base = "base";
}
