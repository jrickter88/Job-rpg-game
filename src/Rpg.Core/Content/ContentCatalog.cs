using System.Diagnostics.CodeAnalysis;

namespace RpgGame.Core.Content;

/// <summary>
/// Immutable, typed in-memory index created after all authored content passes validation.
/// </summary>
/// <remarks>
/// The catalog stores definitions by both their concrete C# type and stable string ID.
/// Consumers therefore cannot accidentally request an enemy as an item merely because the
/// strings happen to match. Construction is internal so an invalid/partial catalog cannot
/// bypass <see cref="Loading.JsonContentLoader"/>.
/// </remarks>
public sealed class ContentCatalog : IContentCatalog
{
    private readonly Dictionary<Type, Dictionary<string, ContentDefinition>> _definitionsByType;

    internal ContentCatalog(IEnumerable<ContentDefinition> definitions)
    {
        _definitionsByType = definitions
            .GroupBy(definition => definition.GetType())
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(
                    definition => definition.Id,
                    definition => definition,
                    StringComparer.Ordinal));

        Count = _definitionsByType.Values.Sum(category => category.Count);
    }

    /// <inheritdoc />
    public int Count { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<TDefinition> GetAll<TDefinition>()
        where TDefinition : ContentDefinition
    {
        if (!_definitionsByType.TryGetValue(typeof(TDefinition), out var definitions))
        {
            return Array.Empty<TDefinition>();
        }

        // Return a new ordered array so callers cannot cast back to and mutate our index.
        return definitions.Values
            .Cast<TDefinition>()
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
    }

    /// <inheritdoc />
    public TDefinition GetRequired<TDefinition>(string id)
        where TDefinition : ContentDefinition
    {
        if (TryGet<TDefinition>(id, out var definition))
        {
            return definition;
        }

        throw new KeyNotFoundException(
            $"Content definition '{id}' was not found as {typeof(TDefinition).Name}.");
    }

    /// <inheritdoc />
    public bool TryGet<TDefinition>(
        string id,
        [NotNullWhen(true)] out TDefinition? definition)
        where TDefinition : ContentDefinition
    {
        if (_definitionsByType.TryGetValue(typeof(TDefinition), out var definitions)
            && definitions.TryGetValue(id, out var untypedDefinition))
        {
            definition = (TDefinition)untypedDefinition;
            return true;
        }

        definition = null;
        return false;
    }
}
