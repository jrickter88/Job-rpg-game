using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Inventory;

/// <summary>
/// Provides content-aware quantity queries and atomic campaign inventory mutations.
/// </summary>
public sealed class InventoryService
{
    private readonly IContentCatalog _content;
    private readonly IGameSession _session;

    public InventoryService(IContentCatalog content, IGameSession session)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>Returns the owned quantity for one item, treating an absent stack as zero.</summary>
    public int GetQuantity(string itemId)
    {
        _ = ResolveItem(itemId);
        IReadOnlyDictionary<string, int> inventory = ValidateCurrentInventory();
        return inventory.TryGetValue(itemId, out int quantity) ? quantity : 0;
    }

    /// <summary>Adds a positive quantity without exceeding the item's authored stack limit.</summary>
    public void AddItem(string itemId, int quantity) =>
        AddItems([new InventoryAddition(itemId, quantity)]);

    /// <summary>
    /// Adds an ordered batch atomically, combining repeated item IDs before one publication.
    /// </summary>
    public void AddItems(IReadOnlyList<InventoryAddition> additions)
    {
        ArgumentNullException.ThrowIfNull(additions);
        if (additions.Any(addition => addition is null))
        {
            throw new ArgumentException(
                "Inventory additions cannot contain a null entry.",
                nameof(additions));
        }

        if (additions.Count == 0)
        {
            return;
        }

        IReadOnlyDictionary<string, int> currentInventory = ValidateCurrentInventory();
        var requestedByItemId = new Dictionary<string, int>(StringComparer.Ordinal);
        var itemById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);
        var itemOrder = new List<string>();

        foreach (InventoryAddition addition in additions)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(addition.ItemId);
            ValidateRequestedQuantity(addition.Quantity);
            ItemDefinition item = ResolveItem(addition.ItemId);

            if (!requestedByItemId.TryGetValue(item.Id, out int requestedQuantity))
            {
                requestedByItemId.Add(item.Id, addition.Quantity);
                itemById.Add(item.Id, item);
                itemOrder.Add(item.Id);
                continue;
            }

            try
            {
                requestedByItemId[item.Id] = checked(requestedQuantity + addition.Quantity);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    $"Cannot add item '{item.Id}': combined requested quantity exceeds the "
                    + "supported integer range.",
                    exception);
            }
        }

        Dictionary<string, int> replacement = CopyInventory(currentInventory);
        foreach (string itemId in itemOrder)
        {
            ItemDefinition item = itemById[itemId];
            int requestedQuantity = requestedByItemId[itemId];
            int currentQuantity = currentInventory.TryGetValue(itemId, out int owned) ? owned : 0;

            int updatedQuantity;
            try
            {
                updatedQuantity = checked(currentQuantity + requestedQuantity);
            }
            catch (OverflowException exception)
            {
                throw CreateAddException(
                    item,
                    currentQuantity,
                    requestedQuantity,
                    "The resulting quantity exceeds the supported integer range.",
                    exception);
            }

            if (updatedQuantity > item.MaxStack)
            {
                throw CreateAddException(
                    item,
                    currentQuantity,
                    requestedQuantity,
                    "The resulting quantity would exceed the maximum stack.");
            }

            replacement[item.Id] = updatedQuantity;
        }

        _session.UpdateInventory(replacement);
    }

    /// <summary>Removes a positive quantity and deletes a stack when none remain.</summary>
    public void RemoveItem(string itemId, int quantity)
    {
        ValidateRequestedQuantity(quantity);
        ItemDefinition item = ResolveItem(itemId);
        IReadOnlyDictionary<string, int> currentInventory = ValidateCurrentInventory();

        if (!currentInventory.TryGetValue(item.Id, out int currentQuantity))
        {
            throw new InvalidOperationException(
                $"Cannot remove item '{item.Id}': current quantity is 0, "
                + $"requested quantity is {quantity}.");
        }

        if (quantity > currentQuantity)
        {
            throw new InvalidOperationException(
                $"Cannot remove item '{item.Id}': current quantity is {currentQuantity}, "
                + $"requested quantity is {quantity}.");
        }

        int updatedQuantity = currentQuantity - quantity;
        Dictionary<string, int> replacement = CopyInventory(currentInventory);
        if (updatedQuantity == 0)
        {
            replacement.Remove(item.Id);
        }
        else
        {
            replacement[item.Id] = updatedQuantity;
        }

        _session.UpdateInventory(replacement);
    }

    private ItemDefinition ResolveItem(string itemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(itemId);
        return _content.GetRequired<ItemDefinition>(itemId);
    }

    private IReadOnlyDictionary<string, int> ValidateCurrentInventory()
    {
        IReadOnlyDictionary<string, int>? inventory = _session.Current.Inventory;
        if (inventory is null)
        {
            throw new InvalidDataException("Current inventory cannot be null.");
        }

        foreach ((string itemId, int quantity) in inventory)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                throw new InvalidDataException("Current inventory contains a blank item ID.");
            }

            ItemDefinition item;
            try
            {
                item = _content.GetRequired<ItemDefinition>(itemId);
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidDataException(
                    $"Current inventory ID '{itemId}' does not resolve to an ItemDefinition.",
                    exception);
            }

            if (quantity < 1 || quantity > item.MaxStack)
            {
                throw new InvalidDataException(
                    $"Current inventory item '{itemId}' has quantity {quantity}; "
                    + $"the valid range is 1 through {item.MaxStack}.");
            }
        }

        return inventory;
    }

    private static Dictionary<string, int> CopyInventory(
        IReadOnlyDictionary<string, int> inventory)
    {
        var copy = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach ((string itemId, int quantity) in inventory)
        {
            copy.Add(itemId, quantity);
        }

        return copy;
    }

    private static void ValidateRequestedQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                quantity,
                "Inventory mutation quantity must be positive.");
        }
    }

    private static InvalidOperationException CreateAddException(
        ItemDefinition item,
        int currentQuantity,
        int requestedQuantity,
        string reason,
        Exception? innerException = null)
    {
        string message = $"Cannot add item '{item.Id}': current quantity is {currentQuantity}, "
            + $"requested quantity is {requestedQuantity}, maximum stack is {item.MaxStack}. "
            + reason;

        return innerException is null
            ? new InvalidOperationException(message)
            : new InvalidOperationException(message, innerException);
    }
}
