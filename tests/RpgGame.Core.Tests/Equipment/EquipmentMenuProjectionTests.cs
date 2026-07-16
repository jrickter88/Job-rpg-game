using RpgGame.Core.Content.Definitions;
using RpgGame.Core.Equipment;
using RpgGame.Core.State;
using RpgGame.Core.Tests.Combat;
using Xunit;

namespace RpgGame.Core.Tests.Equipment;

public sealed class EquipmentMenuProjectionTests
{
    [Fact]
    public void Resolve_ListsSupportedSlotCurrentEquipmentAndOnlyOwnedCompatibleItems()
    {
        const string actorId = "actor.hero.james";
        const string swordItemId = "item.test.sword";
        const string armorItemId = "item.test.armor";
        const string potionItemId = "item.test.potion";
        var content = new TestCatalog(
            Item(swordItemId), Item(armorItemId), Item(potionItemId),
            Equipment("equipment.test.sword", swordItemId, EquipmentSlotIds.MainHandWeapon),
            Equipment("equipment.test.armor", armorItemId, "slot.armor.body"));
        var state = new GameState
        {
            SaveId = "projection-test",
            Inventory = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [swordItemId] = 1,
                [armorItemId] = 1,
                [potionItemId] = 1,
            },
            ActorProgress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal)
            {
                [actorId] = new ActorProgressState
                {
                    ActorId = actorId,
                    ClassId = "class.test",
                    EquippedItems = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        [EquipmentSlotIds.MainHandWeapon] = swordItemId,
                    },
                },
            },
        };

        EquipmentMenuProjection result = new EquipmentMenuProjectionResolver(content)
            .Resolve(state, actorId);

        EquipmentSlotProjection slot = Assert.Single(result.Slots);
        Assert.Equal(EquipmentSlotIds.MainHandWeapon, slot.SlotId);
        Assert.Equal(swordItemId, slot.EquippedItemId);
        Assert.Equal([swordItemId], slot.CompatibleOwnedItemIds);
    }

    [Fact]
    public void Resolve_UnknownActorFailsClearly()
    {
        Assert.Throws<KeyNotFoundException>(() => new EquipmentMenuProjectionResolver(
            new TestCatalog()).Resolve(new GameState { SaveId = "projection-test" }, "actor.hero.unknown"));
    }

    private static ItemDefinition Item(string id) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
    };

    private static EquipmentDefinition Equipment(string id, string itemId, string slotId) => new()
    {
        Id = id,
        ItemId = itemId,
        SlotId = slotId,
    };
}
