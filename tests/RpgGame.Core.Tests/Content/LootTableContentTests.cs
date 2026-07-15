using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using Xunit;

namespace RpgGame.Core.Tests.Content;

/// <summary>
/// Executable authoring contract for standalone loot-table content.
/// </summary>
/// <remarks>
/// These tests deliberately stop at loading and validation. Random rolls, victory rewards,
/// and inventory mutation belong to later gameplay milestones and must not leak into this
/// content-foundation suite.
/// </remarks>
public sealed class LootTableContentTests
{
    [Fact]
    public void ValidTableAndEnemyReference_LoadAsTypedCatalogDefinitions()
    {
        ContentLoadResult result = LoadBase(
            ItemDocument(),
            LootTableDocument(),
            EnemyDocument("loot-table.test.green-slime"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));

        LootTableDefinition table = result.Catalog!.GetRequired<LootTableDefinition>(
            "loot-table.test.green-slime");
        LootEntryDefinition entry = Assert.Single(table.Entries);
        Assert.Equal("item.test.potion", entry.ItemId);
        Assert.Equal(0.125m, entry.Chance);
        Assert.Equal(1, entry.MinQuantity);
        Assert.Equal(2, entry.MaxQuantity);

        EnemyDefinition enemy = result.Catalog.GetRequired<EnemyDefinition>(
            "enemy.test.green-slime");
        Assert.Equal(table.Id, enemy.LootTableId);
    }

    [Fact]
    public void EmptyTable_AndExplicitNoLootEnemy_AreLegal()
    {
        var emptyTable = new ContentDocument("loot-tables/empty.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.empty",
              "entries": []
            }
            """);

        ContentLoadResult result = LoadBase(emptyTable, EnemyDocument(lootTableId: null));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        Assert.Empty(result.Catalog!
            .GetRequired<LootTableDefinition>("loot-table.test.empty")
            .Entries);
        Assert.Null(result.Catalog
            .GetRequired<EnemyDefinition>("enemy.test.green-slime")
            .LootTableId);
    }

    [Fact]
    public void MultipleEnemies_CanShareOneReusableTable()
    {
        ContentLoadResult result = LoadBase(
            ItemDocument(),
            LootTableDocument(),
            EnemyDocument(
                "loot-table.test.green-slime",
                "enemy.test.green-slime",
                "green-slime.json"),
            EnemyDocument(
                "loot-table.test.green-slime",
                "enemy.test.blue-slime",
                "blue-slime.json"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal(
            "loot-table.test.green-slime",
            result.Catalog!
                .GetRequired<EnemyDefinition>("enemy.test.green-slime")
                .LootTableId);
        Assert.Equal(
            "loot-table.test.green-slime",
            result.Catalog
                .GetRequired<EnemyDefinition>("enemy.test.blue-slime")
                .LootTableId);
    }

    [Fact]
    public void ZeroAndGuaranteedChances_WithRepeatedItems_AreLegal()
    {
        var table = new ContentDocument("loot-tables/boundaries.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.boundaries",
              "entries": [
                {
                  "itemId": "item.test.potion",
                  "chance": 0,
                  "minQuantity": 1,
                  "maxQuantity": 1
                },
                {
                  "itemId": "item.test.potion",
                  "chance": 1,
                  "minQuantity": 1,
                  "maxQuantity": 2
                }
              ]
            }
            """);

        ContentLoadResult result = LoadBase(ItemDocument(), table);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        LootTableDefinition loaded = result.Catalog!
            .GetRequired<LootTableDefinition>("loot-table.test.boundaries");
        Assert.Equal(2, loaded.Entries.Count);
        Assert.Equal(0m, loaded.Entries[0].Chance);
        Assert.Equal(1m, loaded.Entries[1].Chance);
    }

    [Theory]
    [InlineData("loot-table.test.missing", "reference.missing")]
    [InlineData("item.test.potion", "reference.wrong-category")]
    [InlineData("not a stable id", "reference.invalid-id")]
    public void EnemyLootTableReference_MustBeCanonicalTypedAndPresent(
        string lootTableId,
        string expectedCode)
    {
        ContentLoadResult result = LoadBase(ItemDocument(), EnemyDocument(lootTableId));

        ContentProblem problem = Assert.Single(
            result.Problems,
            candidate => candidate.Code == expectedCode);
        Assert.Equal("base/enemies/green-slime.json", problem.FilePath);
        Assert.Equal("$.lootTableId", problem.JsonPath);
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void LootItemReferences_MissingAndWrongCategory_Aggregate()
    {
        var table = new ContentDocument("loot-tables/bad-items.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.bad-items",
              "entries": [
                {
                  "itemId": "item.test.missing",
                  "chance": 0.5,
                  "minQuantity": 1,
                  "maxQuantity": 1
                },
                {
                  "itemId": "enemy.test.not-an-item",
                  "chance": 0.5,
                  "minQuantity": 1,
                  "maxQuantity": 1
                }
              ]
            }
            """);

        ContentLoadResult result = LoadBase(table);

        Assert.Contains(
            result.Problems,
            problem => problem.Code == "reference.missing"
                && problem.JsonPath == "$.entries[0].itemId");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "reference.wrong-category"
                && problem.JsonPath == "$.entries[1].itemId");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void ChancesOutsideInclusiveRange_AggregateAtExactPaths()
    {
        var table = new ContentDocument("loot-tables/bad-chances.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.bad-chances",
              "entries": [
                {
                  "itemId": "item.test.potion",
                  "chance": -0.01,
                  "minQuantity": 1,
                  "maxQuantity": 1
                },
                {
                  "itemId": "item.test.potion",
                  "chance": 1.01,
                  "minQuantity": 1,
                  "maxQuantity": 1
                }
              ]
            }
            """);

        ContentLoadResult result = LoadBase(ItemDocument(), table);

        Assert.Equal(
            new[] { "$.entries[0].chance", "$.entries[1].chance" },
            result.Problems
                .Where(problem => problem.Code == "loot-table.chance-out-of-range")
                .Select(problem => problem.JsonPath)
                .ToArray());
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void NonpositiveMinimumAndMaximumBelowMinimum_AreRejected()
    {
        var table = new ContentDocument("loot-tables/bad-quantities.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.bad-quantities",
              "entries": [
                {
                  "itemId": "item.test.potion",
                  "chance": 1,
                  "minQuantity": 0,
                  "maxQuantity": 0
                },
                {
                  "itemId": "item.test.potion",
                  "chance": 1,
                  "minQuantity": 3,
                  "maxQuantity": 2
                }
              ]
            }
            """);

        ContentLoadResult result = LoadBase(ItemDocument(), table);

        Assert.Contains(
            result.Problems,
            problem => problem.Code == "value.too-small"
                && problem.JsonPath == "$.entries[0].minQuantity");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "value.too-small"
                && problem.JsonPath == "$.entries[1].maxQuantity");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void ExplicitNullEntryCollectionsAndEntries_AreRejectedWithoutStoppingValidation()
    {
        var nullCollection = new ContentDocument("loot-tables/null-collection.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.null-collection",
              "entries": null
            }
            """);
        var nullEntry = new ContentDocument("loot-tables/null-entry.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.null-entry",
              "entries": [null]
            }
            """);

        ContentLoadResult result = LoadBase(nullCollection, nullEntry);

        Assert.Contains(
            result.Problems,
            problem => problem.Code == "value.null"
                && problem.FilePath == "base/loot-tables/null-collection.json"
                && problem.JsonPath == "$.entries");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "value.null"
                && problem.FilePath == "base/loot-tables/null-entry.json"
                && problem.JsonPath == "$.entries[0]");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void MissingRequiredEntryMembers_AreRejectedDuringStrictJsonLoading()
    {
        var table = new ContentDocument("loot-tables/missing-chance.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.test.missing-chance",
              "entries": [
                {
                  "itemId": "item.test.potion",
                  "minQuantity": 1,
                  "maxQuantity": 1
                }
              ]
            }
            """);

        ContentLoadResult result = LoadBase(ItemDocument(), table);

        Assert.Contains(result.Problems, problem => problem.Code == "json.invalid");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void LegacyInlineEnemyLoot_IsRejectedInsteadOfSilentlyIgnored()
    {
        var enemy = new ContentDocument("enemies/legacy-loot.json", """
            {
              "schemaVersion": 2,
              "id": "enemy.test.legacy-loot",
              "displayNameKey": "enemy.test.legacy-loot.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "lootTableId": null,
              "loot": []
            }
            """);

        ContentLoadResult result = LoadBase(enemy);

        Assert.Contains(result.Problems, problem => problem.Code == "json.invalid");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void EnemyMissingExplicitLootTableDecision_IsRejectedDuringStrictJsonLoading()
    {
        var enemy = new ContentDocument("enemies/missing-loot-table-id.json", """
            {
              "schemaVersion": 2,
              "id": "enemy.test.missing-loot-table-id",
              "displayNameKey": "enemy.test.missing-loot-table-id.name",
              "level": 1,
              "statistics": {},
              "abilityIds": []
            }
            """);

        ContentLoadResult result = LoadBase(enemy);

        Assert.Contains(result.Problems, problem => problem.Code == "json.invalid");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void EnemySchemaVersionOne_IsRejectedWithCategorySpecificExpectation()
    {
        var enemy = new ContentDocument("enemies/old-schema.json", """
            {
              "schemaVersion": 1,
              "id": "enemy.test.old-schema",
              "displayNameKey": "enemy.test.old-schema.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "lootTableId": null
            }
            """);

        ContentLoadResult result = LoadBase(enemy);

        ContentProblem problem = Assert.Single(
            result.Problems,
            candidate => candidate.Code == "schema.unsupported");
        Assert.Equal("$.schemaVersion", problem.JsonPath);
        Assert.Contains("expected 2", problem.Message, StringComparison.Ordinal);
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void ModEnemy_CanReferenceItsNamespacedTableAndBaseItem()
    {
        const string modId = "mod.example.loot-pack";
        var modTable = new ContentDocument("loot-tables/slime.json", """
            {
              "schemaVersion": 1,
              "id": "loot-table.example.loot-pack.slime",
              "entries": [
                {
                  "itemId": "item.test.potion",
                  "chance": 0.25,
                  "minQuantity": 1,
                  "maxQuantity": 1
                }
              ]
            }
            """);
        var modEnemy = new ContentDocument("enemies/slime.json", """
            {
              "schemaVersion": 2,
              "id": "enemy.example.loot-pack.slime",
              "displayNameKey": "enemy.example.loot-pack.slime.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "lootTableId": "loot-table.example.loot-pack.slime"
            }
            """);

        ContentLoadResult result = new JsonContentLoader().Load(
        [
            new MemoryContentSource(
                ContentSourceIds.Base,
                [.. RequiredBaseDocuments(), ItemDocument()]),
            new MemoryContentSource(modId, [modTable, modEnemy]),
        ]);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        Assert.NotNull(result.Catalog!.GetRequired<LootTableDefinition>(
            "loot-table.example.loot-pack.slime"));
        Assert.Equal(
            "loot-table.example.loot-pack.slime",
            result.Catalog.GetRequired<EnemyDefinition>(
                "enemy.example.loot-pack.slime").LootTableId);
    }

    private static ContentLoadResult LoadBase(params ContentDocument[] featureDocuments)
    {
        ContentDocument[] documents = [.. RequiredBaseDocuments(), .. featureDocuments];
        return new JsonContentLoader().Load(
            new MemoryContentSource(ContentSourceIds.Base, documents));
    }

    private static ContentDocument[] RequiredBaseDocuments() =>
    [
        new ContentDocument("classes/test.json", """
            {
              "schemaVersion": 1,
              "id": "class.test.starting",
              "displayNameKey": "class.test.starting.name",
              "baseStatisticBonuses": {},
              "abilityUnlocks": []
            }
            """),
        new ContentDocument("starting-class-rules/default.json", """
            {
              "schemaVersion": 1,
              "id": "newgame.class-rule.base.test",
              "includeClassIds": ["class.test.starting"],
              "excludeClassIds": []
            }
            """),
    ];

    private static ContentDocument ItemDocument() => new("items/potion.json", """
        {
          "schemaVersion": 1,
          "id": "item.test.potion",
          "displayNameKey": "item.test.potion.name",
          "descriptionKey": "item.test.potion.description",
          "buyPrice": 10,
          "sellPrice": 5,
          "maxStack": 99
        }
        """);

    private static ContentDocument LootTableDocument() => new("loot-tables/green-slime.json", """
        {
          "schemaVersion": 1,
          "id": "loot-table.test.green-slime",
          "entries": [
            {
              "itemId": "item.test.potion",
              "chance": 0.125,
              "minQuantity": 1,
              "maxQuantity": 2
            }
          ]
        }
        """);

    private static ContentDocument EnemyDocument(
        string? lootTableId,
        string enemyId = "enemy.test.green-slime",
        string fileName = "green-slime.json")
    {
        string lootTableJson = lootTableId is null ? "null" : $"\"{lootTableId}\"";
        return new ContentDocument($"enemies/{fileName}", $$"""
            {
              "schemaVersion": 2,
              "id": "{{enemyId}}",
              "displayNameKey": "{{enemyId}}.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "lootTableId": {{lootTableJson}}
            }
            """);
    }

    /// <summary>
    /// In-memory source keeps validation tests deterministic and avoids filesystem setup.
    /// </summary>
    private sealed class MemoryContentSource(
        string sourceId,
        IReadOnlyList<ContentDocument> documents,
        IReadOnlyCollection<string>? declaredDependencyIds = null)
        : IContentSource
    {
        public string SourceId { get; } = sourceId;

        public IReadOnlyCollection<string> DeclaredDependencyIds { get; } =
            declaredDependencyIds ?? Array.Empty<string>();

        public IReadOnlyList<ContentDocument> ReadAll() => documents;
    }
}
