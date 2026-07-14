using System.Diagnostics.CodeAnalysis;

namespace RpgGame.Core.Content;

/// <summary>
/// Read-only, validated index of all game-specific content available to RPG rules.
/// </summary>
/// <remarks>
/// The Godot adapter loads JSON and constructs this catalog once during application
/// startup. Rules depend on this interface rather than file paths or Godot
/// resources, which keeps them headless and testable. Generic methods preserve the
/// expected definition category at compile time.
/// </remarks>
public interface IContentCatalog
{
    /// <summary>Total number of definitions across every loaded category.</summary>
    int Count { get; }

    /// <summary>
    /// Gets every definition in one category. The returned collection cannot be edited
    /// through this interface.
    /// </summary>
    IReadOnlyCollection<TDefinition> GetAll<TDefinition>()
        where TDefinition : ContentDefinition;

    /// <summary>
    /// Gets one required definition, throwing a descriptive content error if it is absent.
    /// Use this after startup validation has proven that a reference must exist.
    /// </summary>
    TDefinition GetRequired<TDefinition>(string id)
        where TDefinition : ContentDefinition;

    /// <summary>
    /// Attempts a lookup without throwing. The annotation tells nullable analysis that
    /// <paramref name="definition"/> is non-null whenever the method returns true.
    /// </summary>
    bool TryGet<TDefinition>(
        string id,
        [NotNullWhen(true)] out TDefinition? definition)
        where TDefinition : ContentDefinition;
}
