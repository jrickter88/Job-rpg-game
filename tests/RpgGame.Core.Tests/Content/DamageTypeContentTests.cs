using RpgGame.Core.Combat;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Content.Loading;
using Xunit;

namespace RpgGame.Core.Tests.Content;

public sealed class DamageTypeContentTests
{
    [Fact]
    public void FixturePack_AuthorsBaseDamageTypesAndIronSwordProfileExplicitly()
    {
        var catalog = TestContent.LoadCatalog();

        Assert.Equal(
            DamageTypeIds.Blunt,
            catalog.GetRequired<AbilityDefinition>("ability.command.attack").DamageTypeId);
        Assert.Equal(
            DamageTypeIds.Energy,
            catalog.GetRequired<AbilityDefinition>("ability.enemy.tackle").DamageTypeId);
        EquipmentDefinition sword = catalog.GetRequired<EquipmentDefinition>(
            "equipment.weapon.iron-sword");
        Assert.Equal(
            new[] { KeyValuePair.Create(DamageTypeIds.Slash, 100) },
            sword.WeaponDamagePercentages.ToArray());
        Assert.Equal(4, sword.Attack);
        Assert.False(sword.StatisticModifiers.ContainsKey(CombatStatisticIds.Strength));
        Assert.Contains(DamageTypeIds.Blunt, DamageTypeIds.Supported);
    }

    [Fact]
    public void ElementalMagicDamageType_LoadsWithPhysicalDamageContract()
    {
        ContentLoadResult result = Load(
            new("magic-disciplines/elemental.json", """
                {
                  "schemaVersion": 1,
                  "id": "magic-discipline.test.elemental",
                  "displayNameKey": "magic-discipline.test.elemental.name",
                  "descriptionKey": "magic-discipline.test.elemental.description"
                }
                """),
            DamageAbility(
                "ability.test.fire",
                DamageTypeIds.Fire,
                abilityKindMember: """
                  "abilityKindId": "ability-kind.magic",
                  "magicDisciplineIds": ["magic-discipline.test.elemental"],
                """));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        Assert.Equal(
            DamageTypeIds.Fire,
            result.Catalog!.GetRequired<AbilityDefinition>("ability.test.fire").DamageTypeId);
    }

    [Fact]
    public void AbilityDamageType_UnknownOrUnusedIdIsRejected()
    {
        ContentLoadResult result = Load(
            DamageAbility("ability.test.poison", "damage-type.poison"),
            new("abilities/typed-guard.json", """
                {
                  "schemaVersion": 1,
                  "id": "ability.test.typed-guard",
                  "displayNameKey": "ability.test.typed-guard.name",
                  "descriptionKey": "ability.test.typed-guard.description",
                  "targetingId": "target.self",
                  "costStatisticId": null,
                  "costAmount": 0,
                  "rulesetId": "rules.defense.guard",
                  "damageTypeId": "damage-type.fire",
                  "numericParameters": { "damage-reduction": 0.5 }
                }
                """));

        AssertProblem(result, "damage-type.unsupported");
        AssertProblem(result, "ability.damage-type-unused");
    }

    [Fact]
    public void MixedWeaponProfileAndVariableEnemyModifiers_LoadWithoutAggregation()
    {
        ContentLoadResult result = Load(
            ItemDocument(),
            EquipmentDocument(
                "slot.weapon.main-hand",
                "{ \"damage-type.slash\": 70, \"damage-type.blunt\": 30 }"),
            EnemyDocument(
                "{ \"damage-type.fire\": 50, \"damage-type.ice\": -75, "
                + "\"damage-type.slash\": 20, \"damage-type.lightning\": -100 }"));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Problems));
        EquipmentDefinition equipment = result.Catalog!.GetRequired<EquipmentDefinition>(
            "equipment.test.weapon");
        Assert.Equal(70, equipment.WeaponDamagePercentages[DamageTypeIds.Slash]);
        Assert.Equal(30, equipment.WeaponDamagePercentages[DamageTypeIds.Blunt]);

        EnemyDefinition enemy = result.Catalog.GetRequired<EnemyDefinition>("enemy.test.target");
        Assert.Equal(50, enemy.DamageTypePercentModifiers[DamageTypeIds.Fire]);
        Assert.Equal(-75, enemy.DamageTypePercentModifiers[DamageTypeIds.Ice]);
        Assert.Equal(20, enemy.DamageTypePercentModifiers[DamageTypeIds.Slash]);
        Assert.Equal(-100, enemy.DamageTypePercentModifiers[DamageTypeIds.Lightning]);
    }

    [Theory]
    [InlineData(
        "slot.weapon.main-hand",
        "{ \"damage-type.slash\": 90 }",
        "equipment.weapon-damage-total-invalid")]
    [InlineData(
        "slot.weapon.main-hand",
        "{ \"damage-type.slash\": 0, \"damage-type.fire\": 100 }",
        "equipment.weapon-damage-percentage-invalid")]
    [InlineData(
        "slot.weapon.main-hand",
        "{ \"damage-type.poison\": 100 }",
        "damage-type.unsupported")]
    [InlineData(
        "slot.armor.body",
        "{ \"damage-type.slash\": 100 }",
        "equipment.weapon-damage-on-nonweapon")]
    public void WeaponDamageProfile_InvalidAuthoredContractIsRejected(
        string slotId,
        string profileJson,
        string expectedProblemCode)
    {
        ContentLoadResult result = Load(
            ItemDocument(),
            EquipmentDocument(slotId, profileJson));

        AssertProblem(result, expectedProblemCode);
    }

    [Theory]
    [InlineData("slot.weapon.main-hand", -1, "equipment.attack-negative")]
    [InlineData("slot.armor.body", 1, "equipment.attack-on-nonweapon")]
    public void WeaponAttack_InvalidAuthoredContractIsRejected(
        string slotId,
        int attack,
        string expectedProblemCode)
    {
        ContentLoadResult result = Load(ItemDocument(), EquipmentDocument(slotId, "{}", attack));

        AssertProblem(result, expectedProblemCode);
    }

    [Fact]
    public void EnemyDamageModifiers_UnknownTypeAndValueBelowImmunityAreRejected()
    {
        ContentLoadResult result = Load(EnemyDocument(
            "{ \"damage-type.poison\": 50, \"damage-type.fire\": -101 }"));

        AssertProblem(result, "damage-type.unsupported");
        AssertProblem(result, "enemy.damage-modifier-below-immunity");
    }

    [Fact]
    public void ExplicitNullDamageMapsAreRejectedWhileOmissionRemainsCompatible()
    {
        ContentLoadResult result = Load(
            ItemDocument(),
            EquipmentDocument("slot.weapon.main-hand", "null"),
            EnemyDocument("null"),
            DamageAbility("ability.test.legacy", damageTypeId: null));

        Assert.Equal(2, result.Problems.Count(problem => problem.Code == "value.null"));
        Assert.Null(result.Catalog);
    }

    private static ContentLoadResult Load(params ContentDocument[] documents) =>
        new JsonContentLoader().Load(new MemoryContentSource(
            ContentSourceIds.Base,
            [.. RequiredBaseDocuments(), .. documents]));

    private static ContentDocument[] RequiredBaseDocuments() =>
    [
        new("classes/test.json", """
            {
              "schemaVersion": 1,
              "id": "class.test.starting",
              "displayNameKey": "class.test.starting.name",
              "baseStatisticBonuses": {},
              "abilityUnlocks": [],
              "magicDisciplineUnlocks": []
            }
            """),
        new("starting-class-rules/default.json", """
            {
              "schemaVersion": 1,
              "id": "newgame.class-rule.base.test",
              "includeClassIds": ["class.test.starting"],
              "excludeClassIds": []
            }
            """),
    ];

    private static ContentDocument DamageAbility(
        string id,
        string? damageTypeId,
        string abilityKindMember = "")
    {
        string damageTypeMember = damageTypeId is null
            ? string.Empty
            : $"  \"damageTypeId\": \"{damageTypeId}\",{Environment.NewLine}";
        return new ContentDocument(
            $"abilities/{id.Split('.')[^1]}.json",
            $$"""
            {
              "schemaVersion": 1,
              "id": "{{id}}",
              "displayNameKey": "{{id}}.name",
              "descriptionKey": "{{id}}.description",
            {{abilityKindMember}}
              "targetingId": "target.enemy.single",
              "costStatisticId": null,
              "costAmount": 0,
              "rulesetId": "rules.damage.physical",
            {{damageTypeMember}}  "numericParameters": { "power": 4 }
            }
            """);
    }

    private static ContentDocument ItemDocument() => new("items/test-weapon.json", """
        {
          "schemaVersion": 1,
          "id": "item.test.weapon",
          "displayNameKey": "item.test.weapon.name",
          "descriptionKey": "item.test.weapon.description",
          "buyPrice": 0,
          "sellPrice": 0,
          "maxStack": 1
        }
        """);

    private static ContentDocument EquipmentDocument(
        string slotId,
        string profileJson,
        int attack = 0) => new(
        "equipment/test-weapon.json",
        $$"""
        {
          "schemaVersion": 1,
          "id": "equipment.test.weapon",
          "itemId": "item.test.weapon",
          "slotId": "{{slotId}}",
          "statisticModifiers": {},
          "attack": {{attack}},
          "weaponDamagePercentages": {{profileJson}},
          "grantedAbilityIds": []
        }
        """);

    private static ContentDocument EnemyDocument(string modifiersJson) => new(
        "enemies/test-target.json",
        $$"""
        {
          "schemaVersion": 2,
          "id": "enemy.test.target",
          "displayNameKey": "enemy.test.target.name",
          "level": 1,
          "statistics": {},
          "abilityIds": [],
          "damageTypePercentModifiers": {{modifiersJson}},
          "lootTableId": null
        }
        """);

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
