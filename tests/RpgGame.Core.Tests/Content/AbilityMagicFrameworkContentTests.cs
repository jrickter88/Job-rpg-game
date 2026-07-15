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
                  "targetingId": "target.test.single",
                  "costStatisticId": null,
                  "costAmount": 0,
                  "rulesetId": "rules.test.placeholder",
                  "numericParameters": {}
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
        IReadOnlyList<string>? magicDisciplineIds = null)
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
              "targetingId": "target.test.single",
              "costStatisticId": null,
              "costAmount": 0,
              "rulesetId": "rules.test.placeholder",
              "numericParameters": {}
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
