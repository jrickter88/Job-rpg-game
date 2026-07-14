using System.Text.Json;
using System.Text.Json.Serialization;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Mods;

namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Deserializes, aggregates errors, validates cross-record references, and publishes a catalog.
/// </summary>
public sealed class JsonContentLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateSerializerOptions();

    /// <summary>
    /// Loads an entire source in deterministic path order. A bad file does not stop the pass,
    /// allowing the author to fix several related problems after one run.
    /// </summary>
    public ContentLoadResult Load(IContentSource source) => Load([source]);

    /// <summary>
    /// Loads built-in content and dependency-ordered mod sources into one validated catalog.
    /// Sources cannot replace records: globally duplicate IDs remain errors.
    /// </summary>
    public ContentLoadResult Load(IEnumerable<IContentSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        IContentSource[] sourceList = sources.ToArray();
        var problems = new List<ContentProblem>();
        var sourceDocuments = new List<SourceDocument>();
        var loadedSourceOrder = new List<string>();
        var seenSourceIds = new HashSet<string>(StringComparer.Ordinal);
        var dependencyIdsBySource = new Dictionary<string, IReadOnlySet<string>>(
            StringComparer.Ordinal);

        foreach (IContentSource source in sourceList)
        {
            if (source is null)
            {
                problems.Add(new ContentProblem(
                    "<content-source>",
                    "$",
                    "source.null",
                    "Content source collections cannot contain null entries."));
                continue;
            }

            if (!IsValidSourceId(source.SourceId))
            {
                problems.Add(new ContentProblem(
                    "<content-source>",
                    "$",
                    "source.id-invalid",
                    $"Source ID '{source.SourceId}' must be 'base' or a valid mod ID."));
                continue;
            }

            if (!seenSourceIds.Add(source.SourceId))
            {
                problems.Add(new ContentProblem(
                    $"{source.SourceId}/<content-source>",
                    "$",
                    "source.id-duplicate",
                    $"Content source '{source.SourceId}' was supplied more than once."));
                continue;
            }

            loadedSourceOrder.Add(source.SourceId);
            dependencyIdsBySource[source.SourceId] = source.DeclaredDependencyIds.ToHashSet(
                StringComparer.Ordinal);

            try
            {
                sourceDocuments.AddRange(source.ReadAll().Select(
                    document => new SourceDocument(source.SourceId, document)));
            }
            catch (Exception exception)
            {
                problems.Add(new ContentProblem(
                    $"{source.SourceId}/<content-source>",
                    "$",
                    "source.read",
                    exception.Message));
            }
        }

        var loaded = new List<LoadedContent>();
        var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);

        if (sourceDocuments.Count == 0 && problems.Count == 0)
        {
            problems.Add(new ContentProblem(
                "<content-source>",
                "$",
                "content.empty",
                "No JSON content records were found."));
        }

        // Source order comes from the dependency planner; path order makes each source stable.
        // SelectMany preserves that source order without depending on directory enumeration.
        IEnumerable<SourceDocument> orderedDocuments = loadedSourceOrder
            .SelectMany(sourceId => sourceDocuments
                .Where(item => string.Equals(item.SourceId, sourceId, StringComparison.Ordinal))
                .OrderBy(item => item.Document.RelativePath, StringComparer.Ordinal));

        foreach (SourceDocument sourceDocument in orderedDocuments)
        {
            ContentDocument document = sourceDocument.Document;
            string relativePath = NormalizePath(document.RelativePath);
            string diagnosticPath = $"{sourceDocument.SourceId}/{relativePath}";
            string category = relativePath.Split('/', 2)[0];

            if (!TryDeserialize(category, document.Json, out ContentDefinition? definition, out JsonException? error))
            {
                string message = error is null
                    ? $"Unknown content category folder '{category}'."
                    : error.Message;
                string code = error is null ? "category.unknown" : "json.invalid";
                string jsonPath = error?.Path ?? "$";

                problems.Add(new ContentProblem(diagnosticPath, jsonPath, code, message));
                continue;
            }

            if (definition is null)
            {
                problems.Add(new ContentProblem(
                    diagnosticPath,
                    "$",
                    "json.null",
                    "A content file must contain one JSON object, not null."));
                continue;
            }

            ValidateIdentity(
                definition,
                category,
                sourceDocument.SourceId,
                diagnosticPath,
                problems);

            // System.Text.Json enforces that required properties are present, but .NET 8 can
            // still assign an explicit JSON null to a non-nullable reference property. Do not
            // let a null ID reach Dictionary and turn an authoring error into a loader crash.
            if (definition.Id is null)
            {
                continue;
            }

            if (!firstPathById.TryAdd(definition.Id, diagnosticPath))
            {
                problems.Add(new ContentProblem(
                    diagnosticPath,
                    "$.id",
                    "id.duplicate",
                    $"ID '{definition.Id}' was already declared by '{firstPathById[definition.Id]}'."));
                continue;
            }

            loaded.Add(new LoadedContent(sourceDocument.SourceId, diagnosticPath, definition));
        }

        // Build a temporary catalog from every uniquely identified record so reference errors
        // can still be reported even when unrelated files have identity/parse problems.
        var candidateCatalog = new ContentCatalog(loaded.Select(item => item.Definition));
        problems.AddRange(ContentValidator.Validate(
            loaded,
            candidateCatalog,
            dependencyIdsBySource));

        IReadOnlyList<ContentProblem> orderedProblems = problems
            .OrderBy(problem => problem.FilePath, StringComparer.Ordinal)
            .ThenBy(problem => problem.JsonPath, StringComparer.Ordinal)
            .ThenBy(problem => problem.Code, StringComparer.Ordinal)
            .ToArray();

        return orderedProblems.Count == 0
            ? new ContentLoadResult(candidateCatalog, orderedProblems)
            : new ContentLoadResult(null, orderedProblems);
    }

    private static bool TryDeserialize(
        string category,
        string json,
        out ContentDefinition? definition,
        out JsonException? error)
    {
        try
        {
            definition = category switch
            {
                "abilities" => JsonSerializer.Deserialize<AbilityDefinition>(json, SerializerOptions),
                "actors" => JsonSerializer.Deserialize<ActorDefinition>(json, SerializerOptions),
                "classes" => JsonSerializer.Deserialize<ClassDefinition>(json, SerializerOptions),
                "encounters" => JsonSerializer.Deserialize<EncounterDefinition>(json, SerializerOptions),
                "enemies" => JsonSerializer.Deserialize<EnemyDefinition>(json, SerializerOptions),
                "equipment" => JsonSerializer.Deserialize<EquipmentDefinition>(json, SerializerOptions),
                "items" => JsonSerializer.Deserialize<ItemDefinition>(json, SerializerOptions),
                "quests" => JsonSerializer.Deserialize<QuestDefinition>(json, SerializerOptions),
                "statistics" => JsonSerializer.Deserialize<StatisticDefinition>(json, SerializerOptions),
                _ => null,
            };

            error = null;
            return IsKnownCategory(category);
        }
        catch (JsonException exception)
        {
            definition = null;
            error = exception;
            return false;
        }
    }

    private static void ValidateIdentity(
        ContentDefinition definition,
        string category,
        string sourceId,
        string diagnosticPath,
        ICollection<ContentProblem> problems)
    {
        if (definition.SchemaVersion != 1)
        {
            problems.Add(new ContentProblem(
                diagnosticPath,
                "$.schemaVersion",
                "schema.unsupported",
                $"Schema version {definition.SchemaVersion} is unsupported; expected 1."));
        }

        if (!ContentId.IsValid(definition.Id))
        {
            problems.Add(new ContentProblem(
                diagnosticPath,
                "$.id",
                "id.invalid",
                $"'{definition.Id}' is not a canonical stable content ID."));
            return;
        }

        string expectedPrefix = category switch
        {
            "abilities" => "ability.",
            "actors" => "actor.",
            "classes" => "class.",
            "encounters" => "encounter.",
            "enemies" => "enemy.",
            "equipment" => "equipment.",
            "items" => "item.",
            "quests" => "quest.",
            "statistics" => "stat.",
            _ => string.Empty,
        };

        if (!definition.Id.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            problems.Add(new ContentProblem(
                diagnosticPath,
                "$.id",
                "id.wrong-category",
                $"ID '{definition.Id}' must begin with '{expectedPrefix}' in {category}/."));
            return;
        }

        if (!string.Equals(sourceId, ContentSourceIds.Base, StringComparison.Ordinal))
        {
            string modNamespace = ModIdentity.GetContentNamespace(sourceId);
            string requiredPrefix = $"{expectedPrefix}{modNamespace}.";

            if (!definition.Id.StartsWith(requiredPrefix, StringComparison.Ordinal))
            {
                problems.Add(new ContentProblem(
                    diagnosticPath,
                    "$.id",
                    "id.wrong-namespace",
                    $"Mod '{sourceId}' must declare {category} IDs beginning with '{requiredPrefix}'."));
            }
        }
    }

    private static bool IsValidSourceId(string? sourceId) =>
        string.Equals(sourceId, ContentSourceIds.Base, StringComparison.Ordinal)
        || ModIdentity.IsValidId(sourceId);

    private static bool IsKnownCategory(string category) => category is
        "abilities" or
        "actors" or
        "classes" or
        "encounters" or
        "enemies" or
        "equipment" or
        "items" or
        "quests" or
        "statistics";

    private static string NormalizePath(string relativePath) =>
        relativePath.Replace('\\', '/').TrimStart('/');

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = false,
            ReadCommentHandling = JsonCommentHandling.Skip,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        // Populate the reflection-based resolver before freezing the options. The explicit
        // argument is required by .NET 8 when no source-generated resolver was assigned.
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}

/// <summary>Internal pairing retained so validation errors can name their source file.</summary>
internal sealed record LoadedContent(
    string SourceId,
    string RelativePath,
    ContentDefinition Definition);

/// <summary>Associates a raw document with the pack that supplied it.</summary>
internal sealed record SourceDocument(string SourceId, ContentDocument Document);
