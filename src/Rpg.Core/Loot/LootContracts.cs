using RpgGame.Core.Combat;

namespace RpgGame.Core.Loot;

/// <summary>
/// Pure boundary for resolving authored enemy loot into ordered award facts.
/// </summary>
/// <remarks>
/// Resolution reads immutable content and injected randomness only. It does not mutate campaign
/// inventory, inspect a battle scene, aggregate duplicate items, or decide how awards appear.
/// </remarks>
public interface ILootResolver
{
	LootResolution Resolve(
		IReadOnlyList<string> defeatedEnemyDefinitionIds,
		IRandomSource random);
}

/// <summary>Immutable ordered result of evaluating loot tables for defeated enemies.</summary>
public sealed record LootResolution
{
	public LootResolution(IReadOnlyList<LootAward> awards)
	{
		ArgumentNullException.ThrowIfNull(awards);
		if (awards.Any(award => award is null))
		{
			throw new ArgumentException(
				"A loot resolution cannot contain a null award.",
				nameof(awards));
		}

		Awards = Array.AsReadOnly(awards.ToArray());
	}

	/// <summary>Independent successful entries, in supplied enemy and authored entry order.</summary>
	public IReadOnlyList<LootAward> Awards { get; }
}

/// <summary>One successful loot-table entry for one defeated enemy definition.</summary>
public sealed record LootAward(
	string EnemyDefinitionId,
	string LootTableId,
	string ItemId,
	int Quantity);
