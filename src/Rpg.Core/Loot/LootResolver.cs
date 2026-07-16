using RpgGame.Core.Combat;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Loot;

/// <summary>
/// Evaluates validated loot tables deterministically from supplied enemy IDs and randomness.
/// </summary>
public sealed class LootResolver : ILootResolver
{
	private const int ChanceRollUpperBound = 1_000_000;

	private readonly IContentCatalog _content;

	public LootResolver(IContentCatalog content)
	{
		_content = content ?? throw new ArgumentNullException(nameof(content));
	}

	/// <inheritdoc />
	public LootResolution Resolve(
		IReadOnlyList<string> defeatedEnemyDefinitionIds,
		IRandomSource random)
	{
		ArgumentNullException.ThrowIfNull(defeatedEnemyDefinitionIds);
		ArgumentNullException.ThrowIfNull(random);

		var awards = new List<LootAward>();
		foreach (string enemyDefinitionId in defeatedEnemyDefinitionIds)
		{
			ArgumentException.ThrowIfNullOrWhiteSpace(enemyDefinitionId);
			EnemyDefinition enemy = _content.GetRequired<EnemyDefinition>(enemyDefinitionId);
			if (enemy.LootTableId is null)
			{
				continue;
			}

			LootTableDefinition table = _content.GetRequired<LootTableDefinition>(
				enemy.LootTableId);
			IReadOnlyList<LootEntryDefinition> entries = table.Entries
				?? throw new InvalidDataException(
					$"Loot table '{table.Id}' has a null entry collection.");

			foreach (LootEntryDefinition? entry in entries)
			{
				if (entry is null)
				{
					throw new InvalidDataException(
						$"Loot table '{table.Id}' contains a null entry.");
				}

				ValidateEntry(table, entry);
				ItemDefinition item = _content.GetRequired<ItemDefinition>(entry.ItemId);
				if (!RollSucceeds(entry.Chance, random))
				{
					continue;
				}

				awards.Add(new LootAward(
					enemy.Id,
					table.Id,
					item.Id,
					RollQuantity(entry, random)));
			}
		}

		return new LootResolution(awards);
	}

	private static void ValidateEntry(LootTableDefinition table, LootEntryDefinition entry)
	{
		if (string.IsNullOrWhiteSpace(entry.ItemId))
		{
			throw new InvalidDataException(
				$"Loot table '{table.Id}' contains a blank item ID.");
		}

		if (entry.Chance < 0m || entry.Chance > 1m)
		{
			throw new InvalidDataException(
				$"Loot table '{table.Id}' entry '{entry.ItemId}' has chance {entry.Chance}; "
				+ "the valid range is 0 through 1.");
		}

		if (entry.MinQuantity < 1 || entry.MaxQuantity < entry.MinQuantity)
		{
			throw new InvalidDataException(
				$"Loot table '{table.Id}' entry '{entry.ItemId}' has invalid inclusive "
				+ $"quantity range {entry.MinQuantity}..{entry.MaxQuantity}.");
		}
	}

	private static bool RollSucceeds(decimal chance, IRandomSource random)
	{
		if (chance == 0m)
		{
			return false;
		}

		if (chance == 1m)
		{
			return true;
		}

		int roll = random.Next(0, ChanceRollUpperBound);
		return roll < chance * ChanceRollUpperBound;
	}

	private static int RollQuantity(LootEntryDefinition entry, IRandomSource random)
	{
		if (entry.MinQuantity == entry.MaxQuantity)
		{
			return entry.MinQuantity;
		}

		if (entry.MaxQuantity == int.MaxValue)
		{
			// Entry quantities are positive, so shifting the inclusive range down by one
			// avoids an overflowing max-exclusive upper bound.
			return random.Next(entry.MinQuantity - 1, int.MaxValue) + 1;
		}

		return random.Next(entry.MinQuantity, entry.MaxQuantity + 1);
	}
}
