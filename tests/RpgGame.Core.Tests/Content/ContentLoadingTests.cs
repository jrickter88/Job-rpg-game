using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using Xunit;

namespace RpgGame.Core.Tests.Content;

/// <summary>End-to-end and failure-reporting tests for the authored JSON pipeline.</summary>
public sealed class ContentLoadingTests
{
    /// <summary>
    /// Loads every checked-in JSON record and proves all eleven content categories made it
    /// through parsing, identity checks, semantic checks, and cross-reference validation.
    /// </summary>
    [Fact]
    public void FixturePack_LoadsAsOneValidatedCatalog()
    {
        var catalog = TestContent.LoadCatalog();

        Assert.Equal(19, catalog.Count);
        Assert.Single(catalog.GetAll<ActorDefinition>());
        Assert.Equal(3, catalog.GetAll<ClassDefinition>().Count);
        Assert.Single(catalog.GetAll<DialogueDefinition>());
        Assert.Equal(5, catalog.GetAll<StatisticDefinition>().Count);
        Assert.Equal(2, catalog.GetAll<ItemDefinition>().Count);
        Assert.Single(catalog.GetAll<EquipmentDefinition>());
        Assert.Equal(2, catalog.GetAll<AbilityDefinition>().Count);
        Assert.Empty(catalog.GetAll<MagicDisciplineDefinition>());
        Assert.Single(catalog.GetAll<EnemyDefinition>());
        Assert.Single(catalog.GetAll<EncounterDefinition>());
        Assert.Single(catalog.GetAll<QuestDefinition>());
        Assert.Single(catalog.GetAll<StartingClassRuleDefinition>());

        ActorDefinition actor = catalog.GetRequired<ActorDefinition>("actor.hero.james");
        Assert.Equal("actor.james.name", actor.DisplayNameKey);

        // Milestone 2.75 uses this exact checked-in record at the scene boundary. Keeping the
        // proof here confirms the production loader—not a duplicated placeholder list—owns
        // the enemy placements and presentation lookup key.
        EncounterDefinition encounter = catalog.GetRequired<EncounterDefinition>(
            "encounter.forest.slimes-01");
        Assert.Equal("battlefield.forest.day", encounter.BattlefieldId);
        Assert.Collection(
            encounter.EnemyGroup,
            enemy =>
            {
                Assert.Equal("enemy.forest.green-slime", enemy.EnemyId);
                Assert.Equal("formation.enemy.r1.c0", enemy.SlotId);
            },
            enemy =>
            {
                Assert.Equal("enemy.forest.green-slime", enemy.EnemyId);
                Assert.Equal("formation.enemy.r2.c0", enemy.SlotId);
            });

        EnemyDefinition slime = catalog.GetRequired<EnemyDefinition>(
            "enemy.forest.green-slime");
        Assert.Equal(1, slime.FormationFootprint.Rows);
        Assert.Equal(1, slime.FormationFootprint.Columns);
    }

    /// <summary>
    /// A loader pass must report independent problems together so a solo content author
    /// does not have to rerun the game after fixing each individual file.
    /// </summary>
    [Fact]
    public void InvalidPack_AggregatesProblemsAndPublishesNoCatalog()
    {
        ContentDocument[] documents =
        [
            new("actors/broken-hero.json", """
                {
                  "schemaVersion": 1,
                  "id": "actor.hero.broken",
                  "displayNameKey": "actor.broken.name",
                  "baseStatistics": {},
                  "startingAbilityIds": []
                }
                """),
            new("actors/wrong-category.json", """
                {
                  "schemaVersion": 1,
                  "id": "actor.hero.wrong-category",
                  "displayNameKey": "actor.wrong-category.name",
                  "baseStatistics": {},
                  "startingAbilityIds": []
                }
                """),
            new("dialogues/broken.json", """
                {
                  "schemaVersion": 1,
                  "id": "dialogue.test.broken",
                  "speakerName": " ",
                  "lines": []
                }
                """),
            new("starting-class-rules/broken.json", """
                {
                  "schemaVersion": 1,
                  "id": "newgame.class-rule.base.broken",
                  "includeClassIds": [
                    "class.missing.class",
                    "item.test.duplicate"
                  ],
                  "excludeClassIds": []
                }
                """),
            new("items/first.json", """
                {
                  "schemaVersion": 1,
                  "id": "item.test.duplicate",
                  "displayNameKey": "item.test.name",
                  "descriptionKey": "item.test.description",
                  "buyPrice": 0,
                  "sellPrice": 0,
                  "maxStack": 1
                }
                """),
            new("items/second.json", """
                {
                  "schemaVersion": 1,
                  "id": "item.test.duplicate",
                  "displayNameKey": "item.test-again.name",
                  "descriptionKey": "item.test-again.description",
                  "buyPrice": 0,
                  "sellPrice": 0,
                  "maxStack": 1
                }
                """),
            new("mystery/unknown.json", "{}"),
            new("items/null-id.json", """
                {
                  "schemaVersion": 1,
                  "id": null,
                  "displayNameKey": "item.null.name",
                  "descriptionKey": "item.null.description",
                  "buyPrice": 0,
                  "sellPrice": 0,
                  "maxStack": 1
                }
                """),
        ];

        ContentLoadResult result = new JsonContentLoader().Load(
            new MemoryContentSource(ContentSourceIds.Base, documents));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Catalog);
        Assert.Contains(result.Problems, problem => problem.Code == "reference.missing");
        Assert.Contains(result.Problems, problem => problem.Code == "reference.wrong-category");
        Assert.Contains(result.Problems, problem => problem.Code == "id.duplicate");
        Assert.Contains(result.Problems, problem => problem.Code == "category.unknown");
        Assert.Contains(result.Problems, problem => problem.Code == "id.invalid");
        Assert.Contains(result.Problems, problem => problem.Code == "dialogue.no-lines");
        Assert.Contains(
            result.Problems,
            problem => problem.FilePath.EndsWith("dialogues/broken.json", StringComparison.Ordinal)
                && problem.Code == "value.blank");
    }

    /// <summary>
    /// Community records may reference base content, but they may not impersonate built-in
    /// IDs or another author's namespace. This also keeps duplicate behavior unambiguous.
    /// </summary>
    [Fact]
    public void ModRecord_OutsideManifestNamespace_IsRejectedWithSourceInDiagnostic()
    {
        ContentDocument[] documents =
        [
            new("items/potion.json", """
                {
                  "schemaVersion": 1,
                  "id": "item.consumable.potion",
                  "displayNameKey": "item.potion.name",
                  "descriptionKey": "item.potion.description",
                  "buyPrice": 10,
                  "sellPrice": 5,
                  "maxStack": 99
                }
                """),
        ];

        ContentLoadResult result = new JsonContentLoader().Load(
            new MemoryContentSource("mod.example.test", documents));

        ContentProblem problem = Assert.Single(
            result.Problems,
            problem => problem.Code == "id.wrong-namespace");
        Assert.Equal("mod.example.test/items/potion.json", problem.FilePath);
        Assert.Contains("item.example.test.", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CrossModReference_RequiresDirectManifestDependency()
    {
        ContentDocument item = new("items/sword.json", """
            {
              "schemaVersion": 1,
              "id": "item.example.library.sword",
              "displayNameKey": "item.example.library.sword.name",
              "descriptionKey": "item.example.library.sword.description",
              "buyPrice": 10,
              "sellPrice": 5,
              "maxStack": 1
            }
            """);
        ContentDocument equipment = new("equipment/sword.json", """
            {
              "schemaVersion": 1,
              "id": "equipment.example.addon.sword",
              "itemId": "item.example.library.sword",
              "slotId": "slot.weapon.main-hand",
              "statisticModifiers": {},
              "grantedAbilityIds": []
            }
            """);
        ContentDocument baseClass = new("classes/test.json", """
            {
              "schemaVersion": 1,
              "id": "class.test.starting",
              "displayNameKey": "class.test.starting.name",
              "baseStatisticBonuses": {},
              "abilityUnlocks": []
            }
            """);
        ContentDocument startingClassRule = new("starting-class-rules/default.json", """
            {
              "schemaVersion": 1,
              "id": "newgame.class-rule.base.test",
              "includeClassIds": ["class.test.starting"],
              "excludeClassIds": []
            }
            """);

        var loader = new JsonContentLoader();
        ContentLoadResult undeclared = loader.Load(
        [
            new MemoryContentSource(ContentSourceIds.Base, [baseClass, startingClassRule]),
            new MemoryContentSource("mod.example.library", [item]),
            new MemoryContentSource("mod.example.addon", [equipment]),
        ]);
        ContentLoadResult declared = loader.Load(
        [
            new MemoryContentSource(ContentSourceIds.Base, [baseClass, startingClassRule]),
            new MemoryContentSource("mod.example.library", [item]),
            new MemoryContentSource(
                "mod.example.addon",
                [equipment],
                ["mod.example.library"]),
        ]);

        Assert.Contains(
            undeclared.Problems,
            problem => problem.Code == "reference.undeclared-mod-dependency");
        Assert.True(declared.IsSuccess, string.Join(Environment.NewLine, declared.Problems));
    }

    [Fact]
    public void StartingClassRule_ContradictionAndEmptyPool_AreRejected()
    {
        ContentDocument classDefinition = new("classes/test.json", """
            {
              "schemaVersion": 1,
              "id": "class.test.starting",
              "displayNameKey": "class.test.starting.name",
              "baseStatisticBonuses": {},
              "abilityUnlocks": []
            }
            """);
        ContentDocument contradictoryRule = new("starting-class-rules/default.json", """
            {
              "schemaVersion": 1,
              "id": "newgame.class-rule.base.test",
              "includeClassIds": ["class.test.starting"],
              "excludeClassIds": ["class.test.starting"]
            }
            """);

        ContentLoadResult result = new JsonContentLoader().Load(
            new MemoryContentSource(
                ContentSourceIds.Base,
                [classDefinition, contradictoryRule]));

        Assert.Contains(
            result.Problems,
            problem => problem.Code == "starting-class-rule.contradictory");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "starting-class-pool.empty");
    }

    [Fact]
    public void EnemyFootprint_OmittedFromJson_DefaultsToOneByOne()
    {
        ContentDocument enemy = new("enemies/default-footprint.json", """
            {
              "schemaVersion": 1,
              "id": "enemy.test.default-footprint",
              "displayNameKey": "enemy.test.default-footprint.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "loot": []
            }
            """);

        ContentLoadResult result = LoadFormationDocuments(enemy);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        EnemyDefinition loaded = result.Catalog!.GetRequired<EnemyDefinition>(
            "enemy.test.default-footprint");
        Assert.Equal(1, loaded.FormationFootprint.Rows);
        Assert.Equal(1, loaded.FormationFootprint.Columns);
    }

    [Fact]
    public void EnemyFootprint_ExplicitOneByOne_LoadsExactly()
    {
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.explicit-single",
            "explicit-single.json",
            "{ \"rows\": 1, \"columns\": 1 }");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        EnemyDefinition loaded = result.Catalog!.GetRequired<EnemyDefinition>(
            "enemy.test.explicit-single");
        Assert.Equal(1, loaded.FormationFootprint.Rows);
        Assert.Equal(1, loaded.FormationFootprint.Columns);
    }

    [Fact]
    public void EnemyFootprint_ValidTwoByTwo_LoadsExactly()
    {
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.large",
            "large.json",
            "{ \"rows\": 2, \"columns\": 2 }");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        EnemyDefinition loaded = result.Catalog!.GetRequired<EnemyDefinition>(
            "enemy.test.large");
        Assert.Equal(2, loaded.FormationFootprint.Rows);
        Assert.Equal(2, loaded.FormationFootprint.Columns);
    }

    [Fact]
    public void EnemyFootprint_ExplicitNull_IsRejectedAtExactPath()
    {
        ContentDocument enemy = new("enemies/null-footprint.json", """
            {
              "schemaVersion": 1,
              "id": "enemy.test.null-footprint",
              "displayNameKey": "enemy.test.null-footprint.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "formationFootprint": null,
              "loot": []
            }
            """);

        ContentLoadResult result = LoadFormationDocuments(enemy);

        ContentProblem problem = Assert.Single(
            result.Problems,
            candidate => candidate.Code == "enemy.footprint-null");
        Assert.Equal("base/enemies/null-footprint.json", problem.FilePath);
        Assert.Equal("$.formationFootprint", problem.JsonPath);
        Assert.Contains("omit it", problem.Message, StringComparison.Ordinal);
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void EnemyFootprint_ZeroRows_IsRejected()
    {
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.zero-rows",
            "zero-rows.json",
            "{ \"rows\": 0, \"columns\": 1 }");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        AssertFootprintProblem(
            result,
            "enemy.footprint-rows-invalid",
            "$.formationFootprint.rows",
            "base/enemies/zero-rows.json");
    }

    [Fact]
    public void EnemyFootprint_ZeroColumns_IsRejected()
    {
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.zero-columns",
            "zero-columns.json",
            "{ \"rows\": 1, \"columns\": 0 }");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        AssertFootprintProblem(
            result,
            "enemy.footprint-columns-invalid",
            "$.formationFootprint.columns",
            "base/enemies/zero-columns.json");
    }

    [Fact]
    public void EnemyFootprint_NegativeDimensions_AggregateBothProblems()
    {
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.negative",
            "negative.json",
            "{ \"rows\": -2, \"columns\": -3 }");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        Assert.Collection(
            result.Problems.Where(problem => problem.Code.StartsWith(
                "enemy.footprint-",
                StringComparison.Ordinal)),
            problem =>
            {
                Assert.Equal("enemy.footprint-columns-invalid", problem.Code);
                Assert.Equal("$.formationFootprint.columns", problem.JsonPath);
            },
            problem =>
            {
                Assert.Equal("enemy.footprint-rows-invalid", problem.Code);
                Assert.Equal("$.formationFootprint.rows", problem.JsonPath);
            });
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void EnemyFootprint_MoreThanFormationRows_IsRejected()
    {
        int invalidRows = BattleFormationRules.RowCount + 1;
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.too-tall",
            "too-tall.json",
            $"{{ \"rows\": {invalidRows}, \"columns\": 1 }}");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        AssertFootprintProblem(
            result,
            "enemy.footprint-rows-invalid",
            "$.formationFootprint.rows",
            "base/enemies/too-tall.json");
    }

    [Fact]
    public void EnemyFootprint_MoreThanEnemyFormationColumns_IsRejected()
    {
        int invalidColumns = BattleFormationRules.EnemyColumnCount + 1;
        ContentDocument enemy = CreateEnemyDocument(
            "enemy.test.too-wide",
            "too-wide.json",
            $"{{ \"rows\": 1, \"columns\": {invalidColumns} }}");

        ContentLoadResult result = LoadFormationDocuments(enemy);

        AssertFootprintProblem(
            result,
            "enemy.footprint-columns-invalid",
            "$.formationFootprint.columns",
            "base/enemies/too-wide.json");
    }

    [Fact]
    public void ModEnemy_OmittedFootprint_DefaultsToOneByOne()
    {
        const string modId = "mod.example.footprint-pack";
        ContentDocument modEnemy = CreateEnemyDocument(
            "enemy.example.footprint-pack.default",
            "default.json");

        ContentLoadResult result = new JsonContentLoader().Load(
        [
            new MemoryContentSource(ContentSourceIds.Base, CreateRequiredBaseDocuments()),
            new MemoryContentSource(modId, [modEnemy]),
        ]);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        EnemyDefinition loaded = result.Catalog!.GetRequired<EnemyDefinition>(
            "enemy.example.footprint-pack.default");
        Assert.Equal(1, loaded.FormationFootprint.Rows);
        Assert.Equal(1, loaded.FormationFootprint.Columns);
    }

    [Fact]
    public void EnemyFootprint_ConversionPreservesRowsAndColumns()
    {
        var authored = new EnemyFootprintDefinition
        {
            Rows = 2,
            Columns = 3,
        };

        FormationFootprint footprint = authored.ToFormationFootprint();

        Assert.Equal(2, footprint.Rows);
        Assert.Equal(3, footprint.Columns);
    }

    [Fact]
    public void EncounterFormation_InvalidSlotsBoundsAndOverlap_AggregateDiagnostics()
    {
        ContentDocument enemy = new("enemies/large.json", """
            {
              "schemaVersion": 1,
              "id": "enemy.test.large",
              "displayNameKey": "enemy.test.large.name",
              "level": 1,
              "statistics": {},
              "abilityIds": [],
              "formationFootprint": { "rows": 2, "columns": 2 },
              "loot": []
            }
            """);
        ContentDocument encounter = new("encounters/invalid-formation.json", """
            {
              "schemaVersion": 1,
              "id": "encounter.test.invalid-formation",
              "enemyGroup": [
                { "enemyId": "enemy.test.large", "slotId": "formation.enemy.r3.c0" },
                { "enemyId": "enemy.test.large", "slotId": "formation.enemy.r0.c3" },
                { "enemyId": "enemy.test.large", "slotId": "formation.enemy.r1.c0" },
                { "enemyId": "enemy.test.large", "slotId": "formation.enemy.r2.c1" },
                { "enemyId": "enemy.test.large", "slotId": "formation.left" }
              ],
              "battlefieldId": null,
              "musicCueId": null
            }
            """);

        ContentLoadResult result = LoadFormationDocuments(enemy, encounter);

        Assert.False(result.IsSuccess);
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "formation.out-of-bounds"
                && problem.JsonPath == "$.enemyGroup[0].slotId");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "formation.out-of-bounds"
                && problem.JsonPath == "$.enemyGroup[1].slotId");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "formation.overlap"
                && problem.JsonPath == "$.enemyGroup[3].slotId");
        Assert.Contains(
            result.Problems,
            problem => problem.Code == "formation.slot-invalid"
                && problem.JsonPath == "$.enemyGroup[4].slotId");
        Assert.Null(result.Catalog);
    }

    private static ContentLoadResult LoadFormationDocuments(
        params ContentDocument[] featureDocuments)
    {
        ContentDocument[] documents = [.. CreateRequiredBaseDocuments(), .. featureDocuments];
        return new JsonContentLoader().Load(
            new MemoryContentSource(ContentSourceIds.Base, documents));
    }

    private static ContentDocument[] CreateRequiredBaseDocuments()
    {
        ContentDocument classDefinition = new("classes/test.json", """
            {
              "schemaVersion": 1,
              "id": "class.test.starting",
              "displayNameKey": "class.test.starting.name",
              "baseStatisticBonuses": {},
              "abilityUnlocks": []
            }
            """);
        ContentDocument startingClassRule = new("starting-class-rules/default.json", """
            {
              "schemaVersion": 1,
              "id": "newgame.class-rule.base.test",
              "includeClassIds": ["class.test.starting"],
              "excludeClassIds": []
            }
            """);
        return [classDefinition, startingClassRule];
    }

    /// <summary>
    /// Builds one in-memory enemy record while keeping each test focused on only the optional
    /// footprint member. A null fragment means the JSON member is genuinely omitted.
    /// </summary>
    private static ContentDocument CreateEnemyDocument(
        string id,
        string fileName,
        string? footprintJson = null)
    {
        string footprintMember = footprintJson is null
            ? string.Empty
            : $",{Environment.NewLine}  \"formationFootprint\": {footprintJson}";
        return new ContentDocument(
            $"enemies/{fileName}",
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "displayNameKey": "{{id}}.name",
              "level": 1,
              "statistics": {},
              "abilityIds": []{{footprintMember}},
              "loot": []
            }
            """);
    }

    private static void AssertFootprintProblem(
        ContentLoadResult result,
        string expectedCode,
        string expectedJsonPath,
        string expectedFilePath)
    {
        ContentProblem problem = Assert.Single(
            result.Problems,
            candidate => candidate.Code == expectedCode);
        Assert.Equal(expectedFilePath, problem.FilePath);
        Assert.Equal(expectedJsonPath, problem.JsonPath);
        Assert.Null(result.Catalog);
    }

    /// <summary>Minimal source double keeps failure scenarios free from temporary-file IO.</summary>
    private sealed class MemoryContentSource(
        string sourceId,
        IReadOnlyList<ContentDocument> documents,
        IReadOnlyCollection<string>? declaredDependencyIds = null)
        : IContentSource
    {
        public string SourceId => sourceId;

        public IReadOnlyCollection<string> DeclaredDependencyIds { get; } =
            declaredDependencyIds ?? [];

        public IReadOnlyList<ContentDocument> ReadAll() => documents;
    }
}
