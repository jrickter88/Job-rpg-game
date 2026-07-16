using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Content.Loading;

/// <summary>
/// One explicit description of a top-level JSON content category.
/// </summary>
/// <remarks>
/// Folder name, stable-ID prefix, supported schema version, concrete definition type, and
/// deserialization previously lived in several independent switches. That made adding or
/// evolving a category error-prone: a record could deserialize but still fail reference
/// validation, or validate an ID prefix but remain invisible to the loader. Keeping those
/// mechanical facts together gives a future category one obvious registration point.
/// Semantic validation still belongs in <see cref="ContentValidator"/> because each category
/// has different rules.
/// </remarks>
internal sealed record ContentCategoryDescriptor(
    string FolderName,
    string IdPrefix,
    int SupportedSchemaVersion,
    Type DefinitionType,
    Func<string, JsonSerializerOptions, ContentDefinition?> Deserialize);

/// <summary>
/// Closed registry for the content categories implemented by this game build.
/// </summary>
/// <remarks>
/// This is intentionally an explicit list, not runtime type scanning or a plugin system.
/// Data mods can add records to supported categories, but they cannot introduce executable
/// C# types or alter loader behavior.
/// </remarks>
internal static class ContentCategoryRegistry
{
    private static readonly ContentCategoryDescriptor[] Categories =
    [
        Create<AbilityDefinition>("abilities", "ability."),
        Create<ActorDefinition>("actors", "actor."),
        Create<ClassDefinition>("classes", "class."),
        Create<DialogueDefinition>("dialogues", "dialogue.", supportedSchemaVersion: 2),
        Create<EncounterDefinition>("encounters", "encounter."),
        Create<EnemyDefinition>("enemies", "enemy.", supportedSchemaVersion: 2),
        Create<EquipmentDefinition>("equipment", "equipment."),
        Create<ItemDefinition>("items", "item."),
        Create<LootTableDefinition>("loot-tables", "loot-table."),
        Create<MapDefinition>("maps", "map."),
        Create<MagicDisciplineDefinition>("magic-disciplines", "magic-discipline."),
        Create<QuestDefinition>("quests", "quest."),
        Create<StartingClassRuleDefinition>("starting-class-rules", "newgame.class-rule."),
        Create<StatisticDefinition>("statistics", "stat."),
        Create<StatusEffectDefinition>("status-effects", "status."),
    ];

    private static readonly IReadOnlyDictionary<string, ContentCategoryDescriptor>
        CategoriesByFolder = Categories.ToDictionary(
            category => category.FolderName,
            StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<Type, ContentCategoryDescriptor>
        CategoriesByDefinitionType = Categories.ToDictionary(
            category => category.DefinitionType);

    /// <summary>Finds the category selected by the first folder in a content path.</summary>
    public static bool TryGetByFolder(
        string folderName,
        [NotNullWhen(true)] out ContentCategoryDescriptor? category) =>
        CategoriesByFolder.TryGetValue(folderName, out category);

    /// <summary>
    /// Gets the stable-ID prefix for a definition type used by a typed content reference.
    /// </summary>
    public static string GetRequiredIdPrefix<TDefinition>()
        where TDefinition : ContentDefinition
    {
        if (CategoriesByDefinitionType.TryGetValue(
                typeof(TDefinition),
                out ContentCategoryDescriptor? category))
        {
            return category.IdPrefix;
        }

        throw new InvalidOperationException(
            $"No content category is registered for {typeof(TDefinition).Name}.");
    }

    private static ContentCategoryDescriptor Create<TDefinition>(
        string folderName,
        string idPrefix,
        int supportedSchemaVersion = 1)
        where TDefinition : ContentDefinition => new(
            folderName,
            idPrefix,
            supportedSchemaVersion,
            typeof(TDefinition),
            static (json, options) => JsonSerializer.Deserialize<TDefinition>(json, options));
}
