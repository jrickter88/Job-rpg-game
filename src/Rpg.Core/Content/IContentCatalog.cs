using System.Diagnostics.CodeAnalysis;

namespace RpgGame.Core.Content;

/// <summary>
/// Read-only, validated view of game-specific content available to RPG rules.
/// </summary>
public interface IContentCatalog
{
    IReadOnlyCollection<TDefinition> GetAll<TDefinition>()
        where TDefinition : ContentDefinition;

    TDefinition GetRequired<TDefinition>(string id)
        where TDefinition : ContentDefinition;

    bool TryGet<TDefinition>(
        string id,
        [NotNullWhen(true)] out TDefinition? definition)
        where TDefinition : ContentDefinition;
}

