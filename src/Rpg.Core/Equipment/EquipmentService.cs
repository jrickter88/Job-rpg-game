using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Equipment;

/// <summary>Validates and publishes persistent actor equipment selections.</summary>
public sealed class EquipmentService
{
    private readonly IContentCatalog _content;
    private readonly IGameSession _session;

    public EquipmentService(IContentCatalog content, IGameSession session)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>Equips an owned inventory item into its compatible authored slot.</summary>
    public void EquipItem(string actorId, string equipmentItemId, string slotId)
    {
        ActorProgressState progress = GetRequiredProgress(actorId);
        EquipmentDefinition equipment = ResolveEquipmentByItemId(equipmentItemId);

        if (!EquipmentSlotIds.IsCompatible(equipment.SlotId, slotId))
        {
            throw new ArgumentException(
                $"Equipment item '{equipmentItemId}' is compatible with '{equipment.SlotId}', not '{slotId}'.",
                nameof(slotId));
        }

        ValidateSlot(slotId);

        if (!_session.Current.Inventory.TryGetValue(equipmentItemId, out int quantity)
            || quantity <= 0)
        {
            throw new InvalidOperationException(
                $"Equipment item '{equipmentItemId}' is not owned in campaign inventory.");
        }

        var equipped = new Dictionary<string, string>(progress.EquippedItems, StringComparer.Ordinal)
        {
            [slotId] = equipmentItemId,
        };
        _session.UpdateActorProgress(actorId, progress with { EquippedItems = equipped });
    }

    /// <summary>Clears one equipped slot. Clearing an empty slot is an intentional no-op.</summary>
    public void UnequipItem(string actorId, string slotId)
    {
        ActorProgressState progress = GetRequiredProgress(actorId);
        ValidateSlot(slotId);
        if (!progress.EquippedItems.ContainsKey(slotId))
        {
            return;
        }

        var equipped = new Dictionary<string, string>(progress.EquippedItems, StringComparer.Ordinal);
        equipped.Remove(slotId);
        _session.UpdateActorProgress(actorId, progress with { EquippedItems = equipped });
    }

    private ActorProgressState GetRequiredProgress(string actorId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        return _session.Current.ActorProgress.TryGetValue(actorId, out ActorProgressState? progress)
            ? progress
            : throw new KeyNotFoundException($"Actor progress for '{actorId}' does not exist.");
    }

    private EquipmentDefinition ResolveEquipmentByItemId(string equipmentItemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(equipmentItemId);
        _content.GetRequired<ItemDefinition>(equipmentItemId);
        EquipmentDefinition[] matches = _content.GetAll<EquipmentDefinition>()
            .Where(equipment => string.Equals(
                equipment.ItemId,
                equipmentItemId,
                StringComparison.Ordinal))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new ArgumentException(
                $"Item '{equipmentItemId}' is not equipment.",
                nameof(equipmentItemId)),
            _ => throw new InvalidDataException(
                $"Item '{equipmentItemId}' has multiple equipment definitions."),
        };
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
