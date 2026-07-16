using System.Collections.ObjectModel;
using RpgGame.Core.Combat;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Equipment;

/// <summary>Builds immutable current and simulated equipment-screen data without mutating campaign state.</summary>
public sealed class EquipmentScreenProjectionResolver
{
    private readonly IContentCatalog _content;
    private readonly CombatStatisticResolver _statistics;

    public EquipmentScreenProjectionResolver(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _statistics = new CombatStatisticResolver(content);
    }

    public EquipmentScreenModel Resolve(GameState state, string actorId)
    {
        ActorProgressState progress = GetProgress(state, actorId);
        return new EquipmentScreenModel(
            actorId,
            progress.ClassId,
            progress.Level,
            BuildStats(progress),
            BuildSlots(state, progress));
    }

    public EquipmentPreviewModel PreviewEquipmentChange(
        GameState state,
        string actorId,
        string slotId,
        string? candidateItemId)
    {
        ActorProgressState progress = GetProgress(state, actorId);
        ValidateSlot(slotId);
        EquipmentItemDetail? candidate = candidateItemId is null
            ? null
            : ResolveOwnedCompatibleItem(state, slotId, candidateItemId);
        var replacement = new Dictionary<string, string>(progress.EquippedItems, StringComparer.Ordinal);
        if (candidateItemId is null)
        {
            replacement.Remove(slotId);
        }
        else
        {
            replacement[slotId] = candidateItemId;
        }

        EquipmentScreenModel current = Resolve(state, actorId);
        EquipmentStatValues preview = BuildStats(progress with { EquippedItems = replacement });
        return new EquipmentPreviewModel(current, slotId, candidate, preview);
    }

    private IReadOnlyList<EquipmentSlotScreenModel> BuildSlots(GameState state, ActorProgressState progress)
    {
        var slots = new List<EquipmentSlotScreenModel>(EquipmentSlotIds.Supported.Count);
        foreach (string slotId in EquipmentSlotIds.Supported)
        {
            progress.EquippedItems.TryGetValue(slotId, out string? equippedItemId);
            EquipmentItemDetail? equipped = equippedItemId is null ? null : ResolveItemDetail(equippedItemId);
            EquipmentItemDetail[] choices = _content.GetAll<EquipmentDefinition>()
                .Where(equipment => EquipmentSlotIds.IsCompatible(equipment.SlotId, slotId)
                    && state.Inventory.TryGetValue(equipment.ItemId, out int quantity)
                    && quantity > 0)
                .OrderBy(equipment => equipment.ItemId, StringComparer.Ordinal)
                .Select(equipment => ResolveItemDetail(equipment.ItemId))
                .ToArray();
            slots.Add(new EquipmentSlotScreenModel(slotId, equipped, choices));
        }

        return slots;
    }

    private EquipmentStatValues BuildStats(ActorProgressState progress)
    {
        IReadOnlyDictionary<string, int> statistics = _statistics.ResolvePartyActor(progress);
        return new EquipmentStatValues(
            statistics[CombatStatisticIds.MaxHp],
            statistics.GetValueOrDefault(CombatStatisticIds.MaxMp),
            statistics[CombatStatisticIds.Strength],
            statistics[CombatStatisticIds.Intelligence],
            statistics[CombatStatisticIds.Defense],
            statistics[CombatStatisticIds.Spirit],
            statistics[CombatStatisticIds.Speed],
            ResolveWeaponAttack(progress));
    }

    private int ResolveWeaponAttack(ActorProgressState progress)
    {
        return progress.EquippedItems.TryGetValue(EquipmentSlotIds.MainHandWeapon, out string? itemId)
            ? ResolveItemDetail(itemId).Attack
            : 0;
    }

    private EquipmentItemDetail ResolveOwnedCompatibleItem(GameState state, string slotId, string itemId)
    {
        if (!state.Inventory.TryGetValue(itemId, out int quantity) || quantity <= 0)
        {
            throw new InvalidOperationException($"Equipment item '{itemId}' is not owned.");
        }

        EquipmentItemDetail detail = ResolveItemDetail(itemId);
        if (!EquipmentSlotIds.IsCompatible(detail.SlotId, slotId))
        {
            throw new ArgumentException(
                $"Equipment item '{itemId}' is compatible with '{detail.SlotId}', not '{slotId}'.",
                nameof(slotId));
        }

        return detail;
    }

    private EquipmentItemDetail ResolveItemDetail(string itemId)
    {
        ItemDefinition item = _content.GetRequired<ItemDefinition>(itemId);
        EquipmentDefinition[] matches = _content.GetAll<EquipmentDefinition>()
            .Where(equipment => string.Equals(equipment.ItemId, itemId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException($"Item '{itemId}' must resolve to exactly one equipment definition.");
        }

        EquipmentDefinition equipment = matches[0];
        return new EquipmentItemDetail(
            item.Id,
            item.DisplayNameKey,
            item.DescriptionKey,
            equipment.SlotId,
            equipment.Attack,
            equipment.StatisticModifiers,
            equipment.WeaponDamagePercentages,
            equipment.SpecialEffectIds);
    }

    private static ActorProgressState GetProgress(GameState state, string actorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return state.ActorProgress.TryGetValue(actorId, out ActorProgressState? progress)
            ? progress
            : throw new KeyNotFoundException($"Actor progress for '{actorId}' does not exist.");
    }

    private static void ValidateSlot(string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (!EquipmentSlotIds.Supported.Contains(slotId, StringComparer.Ordinal))
        {
            throw new ArgumentException($"Unsupported equipment slot '{slotId}'.", nameof(slotId));
        }
    }
}

public sealed record EquipmentScreenModel
{
    public EquipmentScreenModel(
        string actorId,
        string classId,
        int level,
        EquipmentStatValues currentStats,
        IReadOnlyList<EquipmentSlotScreenModel> slots)
    {
        ActorId = actorId;
        ClassId = classId;
        Level = level;
        CurrentStats = currentStats;
        Slots = Array.AsReadOnly(slots.ToArray());
    }

    public string ActorId { get; }
    public string ClassId { get; }
    public int Level { get; }
    public EquipmentStatValues CurrentStats { get; }
    public IReadOnlyList<EquipmentSlotScreenModel> Slots { get; }
}

public sealed record EquipmentSlotScreenModel
{
    public EquipmentSlotScreenModel(
        string slotId,
        EquipmentItemDetail? equippedItem,
        IReadOnlyList<EquipmentItemDetail> compatibleOwnedItems)
    {
        SlotId = slotId;
        EquippedItem = equippedItem;
        CompatibleOwnedItems = Array.AsReadOnly(compatibleOwnedItems.ToArray());
    }

    public string SlotId { get; }
    public EquipmentItemDetail? EquippedItem { get; }
    public IReadOnlyList<EquipmentItemDetail> CompatibleOwnedItems { get; }
}

public sealed record EquipmentPreviewModel(
    EquipmentScreenModel Current,
    string SlotId,
    EquipmentItemDetail? CandidateItem,
    EquipmentStatValues PreviewStats);

public sealed record EquipmentStatValues(
    int MaximumHp,
    int MaximumMp,
    int Strength,
    int Intelligence,
    int Defense,
    int Spirit,
    int Speed,
    int WeaponAttack);

public sealed record EquipmentItemDetail
{
    public EquipmentItemDetail(
        string itemId,
        string displayNameKey,
        string descriptionKey,
        string slotId,
        int attack,
        IReadOnlyDictionary<string, int> statisticModifiers,
        IReadOnlyDictionary<string, int> weaponDamagePercentages,
        IReadOnlyList<string> specialEffectIds)
    {
        ItemId = itemId;
        DisplayNameKey = displayNameKey;
        DescriptionKey = descriptionKey;
        SlotId = slotId;
        Attack = attack;
        StatisticModifiers = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(statisticModifiers, StringComparer.Ordinal));
        WeaponDamagePercentages = new ReadOnlyDictionary<string, int>(
            new Dictionary<string, int>(weaponDamagePercentages, StringComparer.Ordinal));
        SpecialEffectIds = Array.AsReadOnly(specialEffectIds.ToArray());
    }

    public string ItemId { get; }
    public string DisplayNameKey { get; }
    public string DescriptionKey { get; }
    public string SlotId { get; }
    public int Attack { get; }
    public IReadOnlyDictionary<string, int> StatisticModifiers { get; }
    public IReadOnlyDictionary<string, int> WeaponDamagePercentages { get; }
    public IReadOnlyList<string> SpecialEffectIds { get; }
}
