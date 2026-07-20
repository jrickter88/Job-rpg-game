using RpgGame.Core.Combat;
using RpgGame.Core.Content.Definitions;

namespace RpgGame.Core.Maps;

/// <summary>Resolves one successful exploration step against a map-owned encounter table.</summary>
public sealed class RandomEncounterResolver
{
	public const int EncounterRollUpperBound = 256;

	public string? Resolve(
		MapRandomEncounterDefinition? definition,
		IRandomSource random)
	{
		ArgumentNullException.ThrowIfNull(random);
		if (definition is null)
		{
			return null;
		}

		if (definition.Rate is < 0 or >= EncounterRollUpperBound)
		{
			throw new ArgumentOutOfRangeException(
				nameof(definition),
				definition.Rate,
				$"Random encounter rate must be from 0 through {EncounterRollUpperBound - 1}.");
		}

		if (definition.Entries is null || definition.Entries.Count == 0)
		{
			throw new ArgumentException(
				"A random encounter table must contain at least one entry.",
				nameof(definition));
		}

		int totalWeight = 0;
		foreach (MapRandomEncounterEntryDefinition? entry in definition.Entries)
		{
			if (entry is null)
			{
				throw new ArgumentException(
					"A random encounter table cannot contain null entries.",
					nameof(definition));
			}

			ArgumentException.ThrowIfNullOrWhiteSpace(entry.EncounterId);
			if (entry.Weight <= 0)
			{
				throw new ArgumentOutOfRangeException(
					nameof(definition),
					entry.Weight,
					"Random encounter weights must be positive.");
			}

			totalWeight = checked(totalWeight + entry.Weight);
		}

		int encounterRoll = random.Next(0, EncounterRollUpperBound);
		if (encounterRoll >= definition.Rate)
		{
			return null;
		}

		int selectionRoll = random.Next(0, totalWeight);
		int upperBound = 0;
		foreach (MapRandomEncounterEntryDefinition entry in definition.Entries)
		{
			upperBound += entry.Weight;
			if (selectionRoll < upperBound)
			{
				return entry.EncounterId;
			}
		}

		throw new InvalidOperationException("Weighted encounter selection produced no entry.");
	}
}
