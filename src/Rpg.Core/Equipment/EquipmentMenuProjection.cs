using System.Collections.ObjectModel;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Equipment;

/// <summary>Read-only equipment choices for one actor, derived from authoritative campaign state.</summary>
public sealed class EquipmentMenuProjectionResolver
{
    private readonly IContentCatalog _content;

    public EquipmentMenuProjectionResolver(IContentCatalog content)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
    }

    public EquipmentMenuProjection Resolve(GameState state, string actorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        if (!state.ActorProgress.TryGetValue(actorId, out ActorProgressState? progress))
        {
            throw new KeyNotFoundException($"Actor progress for '{actorId}' does not exist.");
        }

        var slots = new List<EquipmentSlotProjection>(EquipmentSlotIds.Supported.Count);
        foreach (string slotId in EquipmentSlotIds.Supported)
        {
            progress.EquippedItems.TryGetValue(slotId, out string? equippedItemId);
            string[] compatibleOwnedItemIds = _content.GetAll<EquipmentDefinition>()
                .Where(equipment => EquipmentSlotIds.IsCompatible(equipment.SlotId, slotId)
                    && state.Inventory.TryGetValue(equipment.ItemId, out int quantity)
                    && quantity > 0)
                .Select(equipment => equipment.ItemId)
                .Order(StringComparer.Ordinal)
                .ToArray();
            slots.Add(new EquipmentSlotProjection(slotId, equippedItemId, compatibleOwnedItemIds));
        }

        return new EquipmentMenuProjection(actorId, slots);
    }
}

/// <summary>Immutable equipment menu data with supported slots in deterministic order.</summary>
public sealed record EquipmentMenuProjection
{
    public EquipmentMenuProjection(string actorId, IReadOnlyList<EquipmentSlotProjection> slots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentNullException.ThrowIfNull(slots);
        ActorId = actorId;
        Slots = Array.AsReadOnly(slots.ToArray());
    }

    public string ActorId { get; }

    public IReadOnlyList<EquipmentSlotProjection> Slots { get; }
}

/// <summary>One supported slot, its current item, and owned compatible candidates.</summary>
public sealed record EquipmentSlotProjection
{
    public EquipmentSlotProjection(
        string slotId,
        string? equippedItemId,
        IReadOnlyList<string> compatibleOwnedItemIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentNullException.ThrowIfNull(compatibleOwnedItemIds);
        SlotId = slotId;
        EquippedItemId = equippedItemId;
        CompatibleOwnedItemIds = Array.AsReadOnly(compatibleOwnedItemIds.ToArray());
    }

    public string SlotId { get; }

    public string? EquippedItemId { get; }

    public IReadOnlyList<string> CompatibleOwnedItemIds { get; }
}
