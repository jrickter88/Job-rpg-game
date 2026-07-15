namespace RpgGame.Core.Inventory;

/// <summary>One requested positive addition to a stable item-definition stack.</summary>
public sealed record InventoryAddition(string ItemId, int Quantity);
