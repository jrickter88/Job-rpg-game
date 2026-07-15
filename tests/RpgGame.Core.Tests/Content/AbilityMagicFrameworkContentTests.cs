using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using Xunit;

namespace RpgGame.Core.Tests.Content;

public sealed class AbilityMagicFrameworkContentTests
{
    [Fact]
    public void FixturePack_ExistingAbilitiesDefaultToSkillsWithoutConcreteDisciplines()
    {
        var catalog = TestContent.LoadCatalog();

        Assert.Empty(catalog.GetAll<MagicDisciplineDefinition>());
        AbilityDefinition guard = catalog.GetRequired<AbilityDefinition>(
            "ability.vanguard.guard");
        AbilityDefinition tackle = catalog.GetRequired<AbilityDefinition>(
            "ability.enemy.tackle");

        Assert.Equal(AbilityKindIds.Skill, guard.AbilityKindId);
        Assert.Empty(guard.MagicDisciplineIds);
        Assert.Equal(AbilityKindIds.Skill, tackle.AbilityKindId);
        Assert.Empty(tackle.MagicDisciplineIds);
    }

    [Fact]
    public void MagicDisciplineDefinition_LoadsAsTypedCatalogCategory()
    {
        ContentLoadResult result = LoadMagicDocuments(
            DisciplineDocument("magic-discipline.test.restoration"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        MagicDisciplineDefinition discipline = result.Catalog!
            .GetRequired<MagicDisciplineDefinition>("magic-discipline.test.restoration");
        Assert.Equal("magic-discipline.test.restoration.name", discipline.DisplayNameKey);
    }

    [Fact]
    public void SkillWithMagicDiscipline_IsRejected()
    {
        ContentLoadResult result = LoadMagicDocuments(
            DisciplineDocument("magic-discipline.test.restoration"),
            AbilityDocument(
                "ability.test.guard",
                abilityKindId: AbilityKindIds.Skill,
                magicDisciplineIds: ["magic-discipline.test.restoration"]));

        AssertProblem(result, "ability.skill-has-magic-disciplines");
    }

    [Fact]
    public void MagicRequiresAtLeastOneDiscipline()
    {
        ContentLoadResult result = LoadMagicDocuments(
            AbilityDocument(
                "ability.test.cure",
                abilityKindId: AbilityKindIds.Magic,
                magicDisciplineIds: []));

        AssertProblem(result, "ability.magic-missing-disciplines");
    }

    [Fact]
    public void UnsupportedAbilityKind_IsRejected()
    {
        ContentLoadResult result = LoadMagicDocuments(
            AbilityDocument(
                "ability.test.weird",
                abilityKindId: "ability-kind.hybrid",
                magicDisciplineIds: []));

        AssertProblem(result, "ability.kind-unsupported");
    }

    [Fact]
    public void MagicDisciplineReferences_MissingWrongCategoryBlankAndDuplicate_AreRejected()
    {
        ContentLoadResult result = LoadMagicDocuments(
            DisciplineDocument("magic-discipline.test.restoration"),
            AbilityDocument(
                "ability.test.bad-magic",
                abilityKindId: AbilityKindIds.Magic,
                magicDisciplineIds:
                [
                    "magic-discipline.test.missing",
                    "ability.test.not-discipline",
                    " ",
                    "magic-discipline.test.restoration",
                    "magic-discipline.test.restoration",
                ]),
            AbilityDocument("ability.test.not-discipline"));

        Assert.Contains(result.Problems, problem => problem.Code == "reference.missing");
        Assert.Contains(result.Problems, problem => problem.Code == "reference.wrong-category");
        Assert.Contains(result.Problems, problem => problem.Code == "reference.invalid-id");
        Assert.Contains(result.Problems, problem => problem.Code == "ability.duplicate-magic-discipline");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void ClassMagicDisciplineUnlocks_AreValidated()
    {
        ContentLoadResult result = LoadMagicDocuments(
            DisciplineDocument("magic-discipline.test.restoration"),
            new("classes/bad-class.json", """
                {
                  "schemaVersion": 1,
                  "id": "class.test.bad",
                  "displayNameKey": "class.test.bad.name",
                  "baseStatisticBonuses": {},
                  "abilityUnlocks": [],
                  "magicDisciplineUnlocks": [
                    { "level": 0, "magicDisciplineId": "magic-discipline.test.restoration" },
                    { "level": 1, "magicDisciplineId": "magic-discipline.test.missing" },
                    { "level": 1, "magicDisciplineId": "ability.test.not-discipline" },
                    { "level": 1, "magicDisciplineId": "magic-discipline.test.restoration" }
                  ]
                }
                """),
            AbilityDocument("ability.test.not-discipline"));

        Assert.Contains(result.Problems, problem => problem.Code == "value.too-small");
        Assert.Contains(result.Problems, problem => problem.Code == "reference.missing");
        Assert.Contains(result.Problems, problem => problem.Code == "reference.wrong-category");
        Assert.Contains(result.Problems, problem => problem.Code == "class.duplicate-magic-discipline");
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void NullCollections_AreRejectedForMagicFields()
    {
        ContentLoadResult result = LoadMagicDocuments(
            new("abilities/null-disciplines.json", """
                {
                  "schemaVersion": 1,
                  "id": "ability.test.null-disciplines",
                  "displayNameKey": "ability.test.null-disciplines.name",
                  "descriptionKey": "ability.test.null-disciplines.description",
                  "abilityKindId": "ability-kind.magic",
                  "magicDisciplineIds": null,
                  "targetingId": "target.self",
                  "costStatisticId": null,
                  "costAmount": 0,
                  "rulesetId": "rules.defense.guard",
                  "numericParameters": { "damage-reduction": 0.5 }
                }
                """),
            new("classes/null-discipline-unlocks.json", """
                {
                  "schemaVersion": 1,
                  "id": "class.test.null-discipline-unlocks",
                  "displayNameKey": "class.test.null-discipline-unlocks.name",
                  "baseStatisticBonuses": {},
                  "abilityUnlocks": [],
                  "magicDisciplineUnlocks": null
                }
                """));

        Assert.Equal(2, result.Problems.Count(problem => problem.Code == "value.null"));
        Assert.Null(result.Catalog);
    }

    [Fact]
    public void AbilityExecutionContract_UnknownTargetingAndRuleset_AreRejected()
    {
        ContentLoadResult result = LoadMagicDocuments(
            AbilityDocument(
                "ability.test.unknown-targeting",
                targetingId: "target.test.unknown"),
            AbilityDocument(
                "ability.test.unknown-ruleset",
                rulesetId: "rules.test.unknown"));

        AssertProblem(result, "ability.targeting-unsupported");
        AssertProblem(result, "ability.ruleset-unsupported");
    }

    [Fact]
    public void AbilityExecutionContract_TargetCompatibilityAndParameters_AreValidated()
    {
        ContentLoadResult result = LoadMagicDocuments(
            AbilityDocument(
                "ability.test.guard-wrong-target",
                targetingId: AbilityTargetingIds.SingleEnemy),
            AbilityDocument(
                "ability.test.guard-missing-parameter",
                numericParametersJson: "{}"),
            AbilityDocument(
                "ability.test.guard-extra-parameter",
                numericParametersJson:
                    "{ \"damage-reduction\": 0.5, \"misspelled-value\": 1 }"),
            AbilityDocument(
                "ability.test.physical-invalid-power",
                targetingId: AbilityTargetingIds.SingleEnemy,
                rulesetId: AbilityRulesetIds.PhysicalDamage,
                numericParametersJson: "{ \"power\": 0 }"));

        AssertProblem(result, "ability.ruleset-targeting-mismatch");
        AssertProblem(result, "ability.parameter-missing");
        AssertProblem(result, "ability.parameter-unsupported");
        AssertProblem(result, "ability.parameter-out-of-range");
    }

    [Fact]
    public void AbilityGrantLists_DuplicateReferences_AreRejectedConsistently()
    {
        const string abilityId = "ability.test.guard";
        ContentLoadResult result = LoadMagicDocuments(
            AbilityDocument(abilityId),
            new("actors/duplicate-ability.json", $$"""
                {
                  "schemaVersion": 1,
                  "id": "actor.test.duplicate-ability",
                  "displayNameKey": "actor.test.duplicate-ability.name",
                  "baseStatistics": {},
                  "startingAbilityIds": ["{{abilityId}}", "{{abilityId}}"]
                }
                """),
            new("enemies/duplicate-ability.json", $$"""
                {
                  "schemaVersion": 2,
                  "id": "enemy.test.duplicate-ability",
                  "displayNameKey": "enemy.test.duplicate-ability.name",
                  "level": 1,
                  "statistics": {},
                  "abilityIds": ["{{abilityId}}", "{{abilityId}}"],
                  "lootTableId": null
                }
                """),
            new("items/test-charm.json", """
                {
                  "schemaVersion": 1,
                  "id": "item.test.charm",
                  "displayNameKey": "item.test.charm.name",
                  "descriptionKey": "item.test.charm.description",
                  "buyPrice": 0,
                  "sellPrice": 0,
                  "maxStack": 1
                }
                """),
            new("equipment/test-charm.json", $$"""
                {
                  "schemaVersion": 1,
                  "id": "equipment.test.charm",
                  "itemId": "item.test.charm",
                  "slotId": "slot.test.charm",
                  "statisticModifiers": {},
                  "grantedAbilityIds": ["{{abilityId}}", "{{abilityId}}"]
                }
                """));

        AssertProblem(result, "actor.duplicate-starting-ability");
        AssertProblem(result, "enemy.duplicate-ability");
        AssertProblem(result, "equipment.duplicate-granted-ability");
    }

    [Fact]
    public void JsonRecordWithoutSchemaVersion_IsRejected()
    {
        ContentLoadResult result = LoadMagicDocuments(
            new ContentDocument("abilities/missing-schema-version.json", """
                {
                  "id": "ability.test.missing-schema-version",
                  "displayNameKey": "ability.test.missing-schema-version.name",
                  "descriptionKey": "ability.test.missing-schema-version.description",
                  "targetingId": "target.self",
                  "costStatisticId": null,
                  "costAmount": 0,
                  "rulesetId": "rules.defense.guard",
                  "numericParameters": { "damage-reduction": 0.5 }
                }
                """));

        AssertProblem(result, "json.invalid");
    }

    private static ContentLoadResult LoadMagicDocuments(params ContentDocument[] documents)
    {
        ContentDocument[] allDocuments = [.. RequiredBaseDocuments(), .. documents];
        return new JsonContentLoader().Load(
            new MemoryContentSource(ContentSourceIds.Base, allDocuments));
    }

    private static ContentDocument[] RequiredBaseDocuments()
    {
        ContentDocument classDefinition = new("classes/test.json", """
            {
              "schemaVersion": 1,
              "id": "class.test.starting",
              "displayNameKey": "class.test.starting.name",
              "baseStatisticBonuses": {},
              "abilityUnlocks": [],
              "magicDisciplineUnlocks": []
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

    private static ContentDocument DisciplineDocument(string id) => new(
        $"magic-disciplines/{id.Split('.')[^1]}.json",
        $$"""
        {
          "schemaVersion": 1,
          "id": "{{id}}",
          "displayNameKey": "{{id}}.name",
          "descriptionKey": "{{id}}.description"
        }
        """);

    private static ContentDocument AbilityDocument(
        string id,
        string? abilityKindId = null,
        IReadOnlyList<string>? magicDisciplineIds = null,
        string targetingId = AbilityTargetingIds.Self,
        string rulesetId = AbilityRulesetIds.Guard,
        string numericParametersJson = "{ \"damage-reduction\": 0.5 }")
    {
        string abilityKindMember = abilityKindId is null
            ? string.Empty
            : $",{Environment.NewLine}  \"abilityKindId\": \"{abilityKindId}\"";
        string disciplineMember = magicDisciplineIds is null
            ? string.Empty
            : $",{Environment.NewLine}  \"magicDisciplineIds\": [{string.Join(", ", magicDisciplineIds.Select(id => $"\"{id}\""))}]";

        return new ContentDocument(
            $"abilities/{id.Split('.')[^1]}.json",
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "displayNameKey": "{{id}}.name",
              "descriptionKey": "{{id}}.description"{{abilityKindMember}}{{disciplineMember}},
              "targetingId": "{{targetingId}}",
              "costStatisticId": null,
              "costAmount": 0,
              "rulesetId": "{{rulesetId}}",
              "numericParameters": {{numericParametersJson}}
            }
            """);
    }

    private static void AssertProblem(ContentLoadResult result, string code)
    {
        Assert.Contains(result.Problems, problem => problem.Code == code);
        Assert.Null(result.Catalog);
    }

    private sealed class MemoryContentSource(
        string sourceId,
        IReadOnlyList<ContentDocument> documents)
        : IContentSource
    {
        public string SourceId => sourceId;

        public IReadOnlyList<ContentDocument> ReadAll() => documents;
    }
}
