using RpgGame.Core.Equipment;
using RpgGame.Core.State;
using Xunit;

namespace RpgGame.Core.Tests.Equipment;

public sealed class EquipmentScreenProjectionTests
{
    private const string JamesId = "actor.hero.james";
    private const string SwordItemId = "item.equipment.iron-sword";
    private const string WoodenShieldItemId = "item.equipment.wooden-shield";
    private const string LeatherArmorItemId = "item.equipment.leather-armor";
    private const string LeatherBootsItemId = "item.equipment.leather-boots";
    private const string LeatherHelmItemId = "item.equipment.leather-helm";
    private const string PowerRingItemId = "item.equipment.power-ring";
    private const string SpiritCharmItemId = "item.equipment.spirit-charm";

    [Fact]
    public void Resolve_ShowsAllSlotsAndSeparatesWeaponAttackFromStrength()
    {
        GameState state = CreateState();

        EquipmentScreenModel screen = new EquipmentScreenProjectionResolver(TestContent.LoadCatalog())
            .Resolve(state, JamesId);

        Assert.Equal(JamesId, screen.ActorId);
        Assert.Equal(
            [
                EquipmentSlotIds.MainHandWeapon,
                EquipmentSlotIds.OffHandWeapon,
                EquipmentSlotIds.BodyArmor,
                EquipmentSlotIds.FeetArmor,
                EquipmentSlotIds.HelmArmor,
                EquipmentSlotIds.AccessoryOne,
                EquipmentSlotIds.AccessoryTwo,
            ],
            screen.Slots.Select(slot => slot.SlotId));
        Assert.All(screen.Slots, slot => Assert.Null(slot.EquippedItem));
        Assert.Equal(7, screen.CurrentStats.Intelligence);
        Assert.Equal(7, screen.CurrentStats.Spirit);
        Assert.Equal(0, screen.CurrentStats.WeaponAttack);
    }

    [Fact]
    public void PreviewEquipmentChange_IronSwordRaisesWeaponAttackWithoutMutatingStateOrStrength()
    {
        GameState state = CreateState();
        var resolver = new EquipmentScreenProjectionResolver(TestContent.LoadCatalog());

        EquipmentPreviewModel preview = resolver.PreviewEquipmentChange(
            state,
            JamesId,
            EquipmentSlotIds.MainHandWeapon,
            SwordItemId);

        Assert.Equal(0, preview.Current.CurrentStats.WeaponAttack);
        Assert.Equal(4, preview.PreviewStats.WeaponAttack);
        Assert.Equal(preview.Current.CurrentStats.Strength, preview.PreviewStats.Strength);
        Assert.Equal(SwordItemId, preview.CandidateItem!.ItemId);
        Assert.Empty(state.ActorProgress[JamesId].EquippedItems);
    }

    [Fact]
    public void PreviewEquipmentChange_UnequipReturnsWeaponAttackToZero()
    {
        GameState state = CreateState(SwordItemId);

        EquipmentPreviewModel preview = new EquipmentScreenProjectionResolver(TestContent.LoadCatalog())
            .PreviewEquipmentChange(state, JamesId, EquipmentSlotIds.MainHandWeapon, null);

        Assert.Equal(4, preview.Current.CurrentStats.WeaponAttack);
        Assert.Equal(0, preview.PreviewStats.WeaponAttack);
        Assert.Equal(SwordItemId, state.ActorProgress[JamesId].EquippedItems[EquipmentSlotIds.MainHandWeapon]);
    }

    [Fact]
    public void PreviewEquipmentChange_ConfirmedEquipMatchesProjectedStats()
    {
        GameState state = CreateState();
        var content = TestContent.LoadCatalog();
        var resolver = new EquipmentScreenProjectionResolver(content);
        EquipmentPreviewModel preview = resolver.PreviewEquipmentChange(
            state, JamesId, EquipmentSlotIds.MainHandWeapon, SwordItemId);
        var session = new GameSession();
        session.ReplaceState(state);

        new EquipmentService(content, session).EquipItem(
            JamesId, SwordItemId, EquipmentSlotIds.MainHandWeapon);

        Assert.Equal(
            preview.PreviewStats,
            resolver.Resolve(session.Current, JamesId).CurrentStats);
    }

    [Theory]
    [InlineData(EquipmentSlotIds.BodyArmor, LeatherArmorItemId)]
    [InlineData(EquipmentSlotIds.OffHandWeapon, WoodenShieldItemId)]
    [InlineData(EquipmentSlotIds.FeetArmor, LeatherBootsItemId)]
    [InlineData(EquipmentSlotIds.HelmArmor, LeatherHelmItemId)]
    [InlineData(EquipmentSlotIds.AccessoryOne, PowerRingItemId)]
    [InlineData(EquipmentSlotIds.AccessoryTwo, SpiritCharmItemId)]
    public void PreviewEquipmentChange_EachAdditionalStarterSlotHasCompatibleOwnedItem(
        string slotId,
        string itemId)
    {
        GameState state = CreateState();
        var resolver = new EquipmentScreenProjectionResolver(TestContent.LoadCatalog());

        EquipmentScreenModel screen = resolver.Resolve(state, JamesId);
        EquipmentSlotScreenModel slot = screen.Slots.Single(candidate => candidate.SlotId == slotId);
        EquipmentPreviewModel preview = resolver.PreviewEquipmentChange(state, JamesId, slotId, itemId);

        Assert.Contains(itemId, slot.CompatibleOwnedItems.Select(item => item.ItemId));
        Assert.Equal(itemId, preview.CandidateItem!.ItemId);
        Assert.NotEqual(preview.Current.CurrentStats, preview.PreviewStats);
        Assert.Empty(state.ActorProgress[JamesId].EquippedItems);
    }

    [Fact]
    public void AccessoryEquipment_IsAvailableInEitherAccessorySlot()
    {
        GameState state = CreateState();
        var resolver = new EquipmentScreenProjectionResolver(TestContent.LoadCatalog());

        EquipmentScreenModel screen = resolver.Resolve(state, JamesId);

        Assert.All(
            screen.Slots.Where(slot => slot.SlotId is EquipmentSlotIds.AccessoryOne
                or EquipmentSlotIds.AccessoryTwo),
            slot => Assert.Contains(
                PowerRingItemId,
                slot.CompatibleOwnedItems.Select(item => item.ItemId)));
    }

    private static GameState CreateState(string? equippedItemId = null)
    {
        var equipped = new Dictionary<string, string>(StringComparer.Ordinal);
        if (equippedItemId is not null)
        {
            equipped[EquipmentSlotIds.MainHandWeapon] = equippedItemId;
        }

        return new GameState
        {
            SaveId = "equipment-screen-test",
            Inventory = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [SwordItemId] = 1,
                [WoodenShieldItemId] = 1,
                [LeatherArmorItemId] = 1,
                [LeatherBootsItemId] = 1,
                [LeatherHelmItemId] = 1,
                [PowerRingItemId] = 1,
                [SpiritCharmItemId] = 1,
            },
            ActivePartyActorIds = [JamesId],
            ActorProgress = new Dictionary<string, ActorProgressState>(StringComparer.Ordinal)
            {
                [JamesId] = new ActorProgressState
                {
                    ActorId = JamesId,
                    ClassId = "class.martial.knight",
                    EquippedItems = equipped,
                },
            },
        };
    }
}
