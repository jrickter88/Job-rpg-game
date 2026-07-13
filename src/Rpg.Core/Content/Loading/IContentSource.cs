namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Platform boundary that supplies raw authored JSON without exposing filesystem or Godot APIs
/// to the loader and validator.
/// </summary>
public interface IContentSource
{
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
