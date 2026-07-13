using System.Text.Json;
using System.Text.Json.Serialization;
using RpgGame.Core.Content.Definitions;

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
    public ContentLoadResult Load(IContentSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        IReadOnlyList<ContentDocument> documents;
        try
        {
            documents = source.ReadAll();
        }
        catch (Exception exception)
        {
            return new ContentLoadResult(
                null,
                [new ContentProblem("<content-source>", "$", "source.read", exception.Message)]);
        }

        var problems = new List<ContentProblem>();
        var loaded = new List<LoadedContent>();
        var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);

        if (documents.Count == 0)
        {
            problems.Add(new ContentProblem(
                "<content-source>",
                "$",
                "content.empty",
                "No JSON content records were found."));
        }

        foreach (ContentDocument document in documents.OrderBy(
                     document => document.RelativePath,
                     StringComparer.Ordinal))
        {
            string relativePath = NormalizePath(document.RelativePath);
            string category = relativePath.Split('/', 2)[0];

            if (!TryDeserialize(category, document.Json, out ContentDefinition? definition, out JsonException? error))
            {
                string message = error is null
                    ? $"Unknown content category folder '{category}'."
                    : error.Message;
                string code = error is null ? "category.unknown" : "json.invalid";
                string jsonPath = error?.Path ?? "$";

                problems.Add(new ContentProblem(relativePath, jsonPath, code, message));
                continue;
            }

            if (definition is null)
            {
                problems.Add(new ContentProblem(
                    relativePath,
                    "$",
                    "json.null",
                    "A content file must contain one JSON object, not null."));
                continue;
            }

            ValidateIdentity(definition, category, relativePath, problems);

            // System.Text.Json enforces that required properties are present, but .NET 8 can
            // still assign an explicit JSON null to a non-nullable reference property. Do not
            // let a null ID reach Dictionary and turn an authoring error into a loader crash.
            if (definition.Id is null)
            {
                continue;
            }

            if (!firstPathById.TryAdd(definition.Id, relativePath))
            {
                problems.Add(new ContentProblem(
                    relativePath,
                    "$.id",
                    "id.duplicate",
                    $"ID '{definition.Id}' was already declared by '{firstPathById[definition.Id]}'."));
                continue;
            }

            loaded.Add(new LoadedContent(relativePath, definition));
        }

        // Build a temporary catalog from every uniquely identified record so reference errors
        // can still be reported even when unrelated files have identity/parse problems.
        var candidateCatalog = new ContentCatalog(loaded.Select(item => item.Definition));
        problems.AddRange(ContentValidator.Validate(loaded, candidateCatalog));

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
        string relativePath,
        ICollection<ContentProblem> problems)
    {
        if (definition.SchemaVersion != 1)
        {
            problems.Add(new ContentProblem(
                relativePath,
                "$.schemaVersion",
                "schema.unsupported",
                $"Schema version {definition.SchemaVersion} is unsupported; expected 1."));
        }

        if (!ContentId.IsValid(definition.Id))
        {
            problems.Add(new ContentProblem(
                relativePath,
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
                relativePath,
                "$.id",
                "id.wrong-category",
                $"ID '{definition.Id}' must begin with '{expectedPrefix}' in {category}/."));
        }
    }

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
internal sealed record LoadedContent(string RelativePath, ContentDefinition Definition);
