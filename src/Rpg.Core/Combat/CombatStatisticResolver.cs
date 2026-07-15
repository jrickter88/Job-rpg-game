using System.Collections.ObjectModel;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Combat;

/// <summary>
/// Resolves the complete set of starting combat statistics for one party actor or enemy.
/// </summary>
/// <remarks>
/// This is deliberately a small, pure calculation boundary. It combines immutable content
/// with the actor's selected campaign class, but it never reads files, mutates <see cref="GameState"/>,
/// creates current HP, or knows anything about Godot, targeting, commands, or enemy AI.
/// </remarks>
public sealed class CombatStatisticResolver
{
	private readonly IContentCatalog _content;

	/// <summary>Creates a resolver over one already validated application content catalog.</summary>
	public CombatStatisticResolver(IContentCatalog content)
	{
		_content = content ?? throw new ArgumentNullException(nameof(content));
	}

	/// <summary>
	/// Resolves actor bases plus the class currently selected by this campaign's progress.
	/// </summary>
	/// <remarks>
	/// Level and experience are intentionally not part of the formula yet. They are accepted
	/// campaign data, but a later progression milestone must define their actual growth rules.
	/// </remarks>
	public IReadOnlyDictionary<string, int> ResolvePartyActor(ActorProgressState progress)
	{
		ArgumentNullException.ThrowIfNull(progress);
		ArgumentException.ThrowIfNullOrWhiteSpace(progress.ActorId, nameof(progress.ActorId));
		ArgumentException.ThrowIfNullOrWhiteSpace(progress.ClassId, nameof(progress.ClassId));

		if (progress.Level < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(progress.Level),
				progress.Level,
				$"Actor progress for '{progress.ActorId}' must have a level of at least 1.");
		}

		ActorDefinition actor = _content.GetRequired<ActorDefinition>(progress.ActorId);
		ClassDefinition classDefinition = _content.GetRequired<ClassDefinition>(progress.ClassId);
		StatisticDefinition[] statistics = GetOrderedStatistics();
		var registeredStatisticIds = CreateRegisteredStatisticIdSet(statistics);

		IReadOnlyDictionary<string, int> actorValues = RequireStatisticMap(
			actor.BaseStatistics,
			$"Actor '{actor.Id}' BaseStatistics");
		IReadOnlyDictionary<string, int> classBonuses = RequireStatisticMap(
			classDefinition.BaseStatisticBonuses,
			$"Class '{classDefinition.Id}' BaseStatisticBonuses");

		RejectUnknownStatisticIds(
			actorValues,
			registeredStatisticIds,
			$"Actor '{actor.Id}' BaseStatistics");
		RejectUnknownStatisticIds(
			classBonuses,
			registeredStatisticIds,
			$"Class '{classDefinition.Id}' BaseStatisticBonuses");

		var resolved = new SortedDictionary<string, int>(StringComparer.Ordinal);
		foreach (StatisticDefinition statistic in statistics)
		{
			int baseValue = actorValues.TryGetValue(statistic.Id, out int authoredBase)
				? authoredBase
				: statistic.DefaultValue;
			int classBonus = classBonuses.TryGetValue(statistic.Id, out int authoredBonus)
				? authoredBonus
				: 0;

			// Use a wider intermediate so two valid Int32 inputs cannot wrap before the
			// authored statistic range is checked.
			long calculatedValue = (long)baseValue + classBonus;
			ValidatePartyValue(
				actor.Id,
				classDefinition.Id,
				statistic,
				calculatedValue);
			resolved.Add(statistic.Id, (int)calculatedValue);
		}

		return AsReadOnly(resolved);
	}

	/// <summary>
	/// Resolves one enemy's explicit authored values, using each registered statistic's
	/// default whenever the enemy omits that statistic.
	/// </summary>
	/// <remarks>
	/// Enemy level, formation footprint, encounter position, and difficulty do not modify
	/// these values. Those rules have not been designed and must not be guessed here.
	/// </remarks>
	public IReadOnlyDictionary<string, int> ResolveEnemy(string enemyDefinitionId)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(enemyDefinitionId);

		EnemyDefinition enemy = _content.GetRequired<EnemyDefinition>(enemyDefinitionId);
		StatisticDefinition[] statistics = GetOrderedStatistics();
		var registeredStatisticIds = CreateRegisteredStatisticIdSet(statistics);
		IReadOnlyDictionary<string, int> enemyValues = RequireStatisticMap(
			enemy.Statistics,
			$"Enemy '{enemy.Id}' Statistics");

		RejectUnknownStatisticIds(
			enemyValues,
			registeredStatisticIds,
			$"Enemy '{enemy.Id}' Statistics");

		var resolved = new SortedDictionary<string, int>(StringComparer.Ordinal);
		foreach (StatisticDefinition statistic in statistics)
		{
			int calculatedValue = enemyValues.TryGetValue(statistic.Id, out int authoredValue)
				? authoredValue
				: statistic.DefaultValue;
			ValidateEnemyValue(enemy.Id, statistic, calculatedValue);
			resolved.Add(statistic.Id, calculatedValue);
		}

		return AsReadOnly(resolved);
	}

	private StatisticDefinition[] GetOrderedStatistics() =>
		_content
			.GetAll<StatisticDefinition>()
			.OrderBy(statistic => statistic.Id, StringComparer.Ordinal)
			.ToArray();

	private static HashSet<string> CreateRegisteredStatisticIdSet(
		IReadOnlyList<StatisticDefinition> statistics)
	{
		var ids = new HashSet<string>(StringComparer.Ordinal);
		foreach (StatisticDefinition statistic in statistics)
		{
			if (!ids.Add(statistic.Id))
			{
				throw new InvalidDataException(
					$"The content catalog contains duplicate statistic ID '{statistic.Id}'.");
			}
		}

		return ids;
	}

	private static IReadOnlyDictionary<string, int> RequireStatisticMap(
		IReadOnlyDictionary<string, int>? values,
		string sourceDescription) =>
		values ?? throw new InvalidDataException(
			$"{sourceDescription} cannot be null in a published content catalog.");

	private static void RejectUnknownStatisticIds(
		IReadOnlyDictionary<string, int> sourceValues,
		IReadOnlySet<string> registeredStatisticIds,
		string sourceDescription)
	{
		// Sort before reporting so a manually constructed catalog with several bad keys
		// fails the same way on every machine and runtime.
		foreach (string statisticId in sourceValues.Keys.Order(StringComparer.Ordinal))
		{
			if (!registeredStatisticIds.Contains(statisticId))
			{
				throw new InvalidDataException(
					$"{sourceDescription} contains unknown statistic ID '{statisticId}'.");
			}
		}
	}

	private static void ValidatePartyValue(
		string actorId,
		string classId,
		StatisticDefinition statistic,
		long calculatedValue)
	{
		if (calculatedValue < statistic.MinimumValue
			|| calculatedValue > statistic.MaximumValue)
		{
			throw new InvalidDataException(
				$"Actor '{actorId}' with class '{classId}' resolves statistic "
				+ $"'{statistic.Id}' to {calculatedValue}, outside its inclusive legal "
				+ $"range {statistic.MinimumValue}..{statistic.MaximumValue}.");
		}
	}

	private static void ValidateEnemyValue(
		string enemyId,
		StatisticDefinition statistic,
		int calculatedValue)
	{
		if (calculatedValue < statistic.MinimumValue
			|| calculatedValue > statistic.MaximumValue)
		{
			throw new InvalidDataException(
				$"Enemy '{enemyId}' resolves statistic '{statistic.Id}' to "
				+ $"{calculatedValue}, outside its inclusive legal range "
				+ $"{statistic.MinimumValue}..{statistic.MaximumValue}.");
		}
	}

	private static IReadOnlyDictionary<string, int> AsReadOnly(
		SortedDictionary<string, int> values)
	{
		// The mutable sorted dictionary stays private. ReadOnlyDictionary blocks mutation
		// even when a caller casts the returned interface to IDictionary.
		return new ReadOnlyDictionary<string, int>(values);
	}
}
