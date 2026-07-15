using RpgGame.Core.Combat;
using RpgGame.Core.Inventory;
using RpgGame.Core.Loot;

namespace RpgGame.Core.Rewards;

/// <summary>Resolves one victory's loot and atomically grants it to campaign inventory.</summary>
public sealed class VictoryRewardService
{
    private readonly ILootResolver _lootResolver;
    private readonly InventoryService _inventory;

    public VictoryRewardService(ILootResolver lootResolver, InventoryService inventory)
    {
        _lootResolver = lootResolver ?? throw new ArgumentNullException(nameof(lootResolver));
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
    }

    public VictoryRewardResult Apply(
        IReadOnlyList<string> defeatedEnemyDefinitionIds,
        IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(defeatedEnemyDefinitionIds);
        ArgumentNullException.ThrowIfNull(random);

        LootResolution resolution = _lootResolver.Resolve(defeatedEnemyDefinitionIds, random)
            ?? throw new InvalidDataException("Loot resolution returned null.");
        var additions = new List<InventoryAddition>(resolution.Awards.Count);
        var quantitiesByItemId = new Dictionary<string, int>(StringComparer.Ordinal);
        var summaryItemOrder = new List<string>();

        foreach (LootAward award in resolution.Awards)
        {
            if (string.IsNullOrWhiteSpace(award.ItemId))
            {
                throw new InvalidDataException("A loot award contains a blank item ID.");
            }

            if (award.Quantity <= 0)
            {
                throw new InvalidDataException(
                    $"Loot award for '{award.ItemId}' must be positive; received "
                    + $"{award.Quantity}.");
            }

            additions.Add(new InventoryAddition(award.ItemId, award.Quantity));
            if (!quantitiesByItemId.TryGetValue(award.ItemId, out int currentQuantity))
            {
                quantitiesByItemId.Add(award.ItemId, award.Quantity);
                summaryItemOrder.Add(award.ItemId);
                continue;
            }

            try
            {
                quantitiesByItemId[award.ItemId] = checked(currentQuantity + award.Quantity);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException(
                    $"Victory reward quantity for '{award.ItemId}' exceeds the supported "
                    + "integer range.",
                    exception);
            }
        }

        ItemRewardSummary[] summaries = summaryItemOrder
            .Select(itemId => new ItemRewardSummary(itemId, quantitiesByItemId[itemId]))
            .ToArray();

        _inventory.AddItems(additions);
        return new VictoryRewardResult(resolution.Awards, summaries);
    }
}
