using RpgGame.Core.Combat;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Equipment;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Combat;

public sealed class EquipmentAttackTests
{
    [Fact]
    public void BasicAttack_EquippedIronSwordAddsAttackAndUsesSlashWithoutIncreasingStrength()
    {
        FixedBattle unarmed = CombatTestFixture.CreateFixedBattle();
        FixedBattle armed = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot armedSnapshot = CreateSnapshotWithIronSword(armed);

        int unarmedStrength = unarmed.Snapshot.GetRequiredCombatant("party-0")
            .Statistics[CombatStatisticIds.Strength];
        CombatantSnapshot armedJames = armedSnapshot.GetRequiredCombatant("party-0");
        Assert.Equal(unarmedStrength, armedJames.Statistics[CombatStatisticIds.Strength]);
        Assert.Equal(4, armedJames.EquippedWeaponAttack);
        Assert.Equal(DamageTypeIds.Slash, armedJames.EquippedWeaponDamageTypeId);

        DamageApplied unarmedDamage = Assert.IsType<DamageApplied>(new CombatResolver(unarmed.Content)
            .Resolve(unarmed.Snapshot, Attack("party-0", "enemy-0")).Events.Single());
        Assert.Equal(DamageTypeIds.Blunt, unarmedDamage.DamageTypeId);
        DamageApplied armedDamage = Assert.IsType<DamageApplied>(new CombatResolver(armed.Content)
            .Resolve(armedSnapshot, Attack("party-0", "enemy-0")).Events.Single());

        Assert.Equal(unarmedDamage.Amount + 4, armedDamage.Amount);
        Assert.Equal(DamageTypeIds.Slash, armedDamage.DamageTypeId);
    }

    [Fact]
    public void BasicAttack_EquippedWeaponProfileOverridesItsAuthoredDamageType()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot armed = CreateSnapshotWithIronSword(battle);
        AbilityDefinition authoredFireAttack = new()
        {
            Id = CombatTestFixture.AttackId,
            DisplayNameKey = "ability.test.attack.name",
            DescriptionKey = "ability.test.attack.description",
            TargetingId = AbilityTargetingIds.SingleEnemy,
            RulesetId = AbilityRulesetIds.PhysicalDamage,
            DamageTypeId = DamageTypeIds.Fire,
            NumericParameters = new Dictionary<string, decimal>(StringComparer.Ordinal)
            {
                [AbilityNumericParameterIds.Power] = 4,
            },
        };

        DamageApplied damage = Assert.IsType<DamageApplied>(new CombatResolver(
            new TestCatalog(authoredFireAttack)).Resolve(armed, Attack("party-0", "enemy-0"))
            .Events.Single());

        Assert.Equal(DamageTypeIds.Slash, damage.DamageTypeId);
    }

    [Fact]
    public void EnemyTackleAndPowerStrike_DoNotReceiveJamesWeaponAttack()
    {
        FixedBattle battle = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot armed = CreateSnapshotWithIronSword(battle);
        var resolver = new CombatResolver(battle.Content);

        DamageApplied unarmedTackle = Assert.IsType<DamageApplied>(resolver.Resolve(
            battle.Snapshot,
            new CombatCommand("enemy-0", CombatTestFixture.TackleId, ["party-0"])).Events.Single());
        DamageApplied armedTackle = Assert.IsType<DamageApplied>(resolver.Resolve(
            armed,
            new CombatCommand("enemy-0", CombatTestFixture.TackleId, ["party-0"])).Events.Single());

        Assert.Equal(unarmedTackle.Amount, armedTackle.Amount);

        FixedBattle knight = CombatTestFixture.CreateFixedBattle();
        CombatSnapshot armedKnight = CreateSnapshotWithIronSword(knight);
        DamageApplied unarmedPowerStrike = Assert.IsType<DamageApplied>(new CombatResolver(knight.Content)
            .Resolve(knight.Snapshot, new CombatCommand(
                "party-0", "ability.knight.power-strike", ["enemy-0"]))
            .Events.OfType<DamageApplied>().Single());
        DamageApplied armedPowerStrike = Assert.IsType<DamageApplied>(new CombatResolver(knight.Content)
            .Resolve(armedKnight, new CombatCommand(
                "party-0", "ability.knight.power-strike", ["enemy-0"]))
            .Events.OfType<DamageApplied>().Single());
        Assert.Equal(unarmedPowerStrike.Amount, armedPowerStrike.Amount);
    }

    private static CombatSnapshot CreateSnapshotWithIronSword(FixedBattle battle)
    {
        ActorProgressState james = battle.Campaign.ActorProgress[CombatTestFixture.JamesId] with
        {
            EquippedItems = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [EquipmentSlotIds.MainHandWeapon] = "item.equipment.iron-sword",
            },
        };
        GameState campaign = battle.Campaign with
        {
            Inventory = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["item.equipment.iron-sword"] = 1,
            },
            ActorProgress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal)
            {
                [CombatTestFixture.JamesId] = james,
            },
        };
        return new CombatSnapshotFactory(battle.Content).Create(
            campaign,
            battle.Encounter,
            battle.EnemyPlacements,
            battle.PartyPlacements);
    }

    private static CombatCommand Attack(string actorId, string targetId) => new(
        actorId,
        CombatTestFixture.AttackId,
        [targetId]);
}
