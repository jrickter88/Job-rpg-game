using System.Text.RegularExpressions;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Content.Loading;

/// <summary>
/// Cross-record and semantic validation kept separate from JSON parsing for focused tests.
/// </summary>
internal sealed class ContentValidator
{
    private static readonly Regex LocalIdPattern = new(
        "^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyList<LoadedContent> _loaded;
    private readonly ContentCatalog _catalog;
    private readonly List<ContentProblem> _problems = [];
    private readonly Dictionary<string, string> _pathById;
    private readonly Dictionary<string, string> _sourceById;
    private readonly IReadOnlyDictionary<string, IReadOnlySet<string>> _dependencyIdsBySource;

    private ContentValidator(
        IReadOnlyList<LoadedContent> loaded,
        ContentCatalog catalog,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencyIdsBySource)
    {
        _loaded = loaded;
        _catalog = catalog;
        _pathById = loaded.ToDictionary(
            item => item.Definition.Id,
            item => item.RelativePath,
            StringComparer.Ordinal);
        _sourceById = loaded.ToDictionary(
            item => item.Definition.Id,
            item => item.SourceId,
            StringComparer.Ordinal);
        _dependencyIdsBySource = dependencyIdsBySource;
    }

    public static IReadOnlyList<ContentProblem> Validate(
        IReadOnlyList<LoadedContent> loaded,
        ContentCatalog catalog,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencyIdsBySource)
    {
        var validator = new ContentValidator(loaded, catalog, dependencyIdsBySource);
        validator.ValidateAll();
        return validator._problems;
    }

    private void ValidateAll()
    {
        foreach (LoadedContent item in _loaded)
        {
            switch (item.Definition)
            {
                case AbilityDefinition ability:
                    ValidateAbility(item, ability);
                    break;
                case ActorDefinition actor:
                    ValidateActor(item, actor);
                    break;
                case ClassDefinition classDefinition:
                    ValidateClass(item, classDefinition);
                    break;
                case DialogueDefinition dialogue:
                    ValidateDialogue(item, dialogue);
                    break;
                case EncounterDefinition encounter:
                    ValidateEncounter(item, encounter);
                    break;
                case EnemyDefinition enemy:
                    ValidateEnemy(item, enemy);
                    break;
                case EquipmentDefinition equipment:
                    ValidateEquipment(item, equipment);
                    break;
                case ItemDefinition itemDefinition:
                    ValidateItem(item, itemDefinition);
                    break;
                case MagicDisciplineDefinition magicDiscipline:
                    ValidateMagicDiscipline(item, magicDiscipline);
                    break;
                case QuestDefinition quest:
                    ValidateQuest(item, quest);
                    break;
                case StartingClassRuleDefinition startingClassRule:
                    ValidateStartingClassRule(item, startingClassRule);
                    break;
                case StatisticDefinition statistic:
                    ValidateStatistic(item, statistic);
                    break;
            }
        }

        ValidateEquipmentItemUniqueness();
        ValidateStartingClassPool();
    }

    private void ValidateActor(LoadedContent item, ActorDefinition actor)
    {
        RequireNonBlank(item, "$.displayNameKey", actor.DisplayNameKey);
        ValidateStatisticMap(
            item,
            "$.baseStatistics",
            RequireMap(item, "$.baseStatistics", actor.BaseStatistics));

        IReadOnlyList<string> startingAbilityIds = RequireList(
            item,
            "$.startingAbilityIds",
            actor.StartingAbilityIds);
        for (int index = 0; index < startingAbilityIds.Count; index++)
        {
            RequireReference<AbilityDefinition>(
                item,
                $"$.startingAbilityIds[{index}]",
                startingAbilityIds[index]);
        }
    }

    private void ValidateMagicDiscipline(
        LoadedContent item,
        MagicDisciplineDefinition magicDiscipline)
    {
        RequireNonBlank(item, "$.displayNameKey", magicDiscipline.DisplayNameKey);
        RequireNonBlank(item, "$.descriptionKey", magicDiscipline.DescriptionKey);
    }

    /// <summary>
    /// Validates one additive contribution to the new-game class pool. Multiple records
    /// may include or exclude the same class because independent mods must be composable;
    /// duplicate entries inside one record are almost certainly an authoring mistake.
    /// </summary>
    private void ValidateStartingClassRule(
        LoadedContent item,
        StartingClassRuleDefinition rule)
    {
        IReadOnlyList<string> includeClassIds = RequireList(
            item,
            "$.includeClassIds",
            rule.IncludeClassIds);
        IReadOnlyList<string> excludeClassIds = RequireList(
            item,
            "$.excludeClassIds",
            rule.ExcludeClassIds);

        var included = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < includeClassIds.Count; index++)
        {
            string classId = includeClassIds[index];
            RequireReference<ClassDefinition>(item, $"$.includeClassIds[{index}]", classId);
            if (!included.Add(classId))
            {
                Add(item, $"$.includeClassIds[{index}]", "starting-class-rule.duplicate-include",
                    $"Class '{classId}' is included more than once by this rule.");
            }
        }

        var excluded = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < excludeClassIds.Count; index++)
        {
            string classId = excludeClassIds[index];
            RequireReference<ClassDefinition>(item, $"$.excludeClassIds[{index}]", classId);
            if (!excluded.Add(classId))
            {
                Add(item, $"$.excludeClassIds[{index}]", "starting-class-rule.duplicate-exclude",
                    $"Class '{classId}' is excluded more than once by this rule.");
            }

            if (included.Contains(classId))
            {
                Add(item, $"$.excludeClassIds[{index}]", "starting-class-rule.contradictory",
                    $"Class '{classId}' cannot be both included and excluded by one rule.");
            }
        }
    }

    private void ValidateClass(LoadedContent item, ClassDefinition classDefinition)
    {
        RequireNonBlank(item, "$.displayNameKey", classDefinition.DisplayNameKey);
        ValidateStatisticMap(
            item,
            "$.baseStatisticBonuses",
            RequireMap(item, "$.baseStatisticBonuses", classDefinition.BaseStatisticBonuses));

        var seenAbilities = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<AbilityUnlockDefinition> abilityUnlocks = RequireList(
            item,
            "$.abilityUnlocks",
            classDefinition.AbilityUnlocks);
        for (int index = 0; index < abilityUnlocks.Count; index++)
        {
            string path = $"$.abilityUnlocks[{index}]";
            AbilityUnlockDefinition? unlock = abilityUnlocks[index];
            if (unlock is null)
            {
                Add(item, path, "value.null", "Array entries cannot be null.");
                continue;
            }

            RequireAtLeast(item, $"{path}.level", unlock.Level, 1);
            RequireReference<AbilityDefinition>(item, $"{path}.abilityId", unlock.AbilityId);

            if (!seenAbilities.Add(unlock.AbilityId))
            {
                Add(item, $"{path}.abilityId", "class.duplicate-ability",
                    $"Ability '{unlock.AbilityId}' is unlocked more than once by this class.");
            }
        }

        var seenMagicDisciplines = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<MagicDisciplineUnlockDefinition> magicDisciplineUnlocks = RequireList(
            item,
            "$.magicDisciplineUnlocks",
            classDefinition.MagicDisciplineUnlocks);
        for (int index = 0; index < magicDisciplineUnlocks.Count; index++)
        {
            string path = $"$.magicDisciplineUnlocks[{index}]";
            MagicDisciplineUnlockDefinition? unlock = magicDisciplineUnlocks[index];
            if (unlock is null)
            {
                Add(item, path, "value.null", "Array entries cannot be null.");
                continue;
            }

            RequireAtLeast(item, $"{path}.level", unlock.Level, 1);
            RequireReference<MagicDisciplineDefinition>(
                item,
                $"{path}.magicDisciplineId",
                unlock.MagicDisciplineId);

            if (!seenMagicDisciplines.Add(unlock.MagicDisciplineId))
            {
                Add(item, $"{path}.magicDisciplineId", "class.duplicate-magic-discipline",
                    $"Magic discipline '{unlock.MagicDisciplineId}' is unlocked more than once by this class.");
            }
        }
    }

    private void ValidateDialogue(LoadedContent item, DialogueDefinition dialogue)
    {
        RequireNonBlank(item, "$.speakerName", dialogue.SpeakerName);

        IReadOnlyList<string> lines = RequireList(item, "$.lines", dialogue.Lines);
        if (lines.Count == 0)
        {
            Add(item, "$.lines", "dialogue.no-lines",
                "A dialogue must contain at least one line.");
        }

        for (int index = 0; index < lines.Count; index++)
        {
            RequireNonBlank(item, $"$.lines[{index}]", lines[index]);
        }
    }

    private void ValidateStatistic(LoadedContent item, StatisticDefinition statistic)
    {
        RequireNonBlank(item, "$.displayNameKey", statistic.DisplayNameKey);

        if (statistic.MinimumValue > statistic.MaximumValue)
        {
            Add(item, "$.minimumValue", "range.inverted",
                "minimumValue cannot be greater than maximumValue.");
        }

        if (statistic.DefaultValue < statistic.MinimumValue
            || statistic.DefaultValue > statistic.MaximumValue)
        {
            Add(item, "$.defaultValue", "range.default",
                "defaultValue must fall within the inclusive minimum/maximum range.");
        }
    }

    private void ValidateItem(LoadedContent item, ItemDefinition itemDefinition)
    {
        RequireNonBlank(item, "$.displayNameKey", itemDefinition.DisplayNameKey);
        RequireNonBlank(item, "$.descriptionKey", itemDefinition.DescriptionKey);
        RequireAtLeast(item, "$.buyPrice", itemDefinition.BuyPrice, 0);
        RequireAtLeast(item, "$.sellPrice", itemDefinition.SellPrice, 0);
        RequireAtLeast(item, "$.maxStack", itemDefinition.MaxStack, 1);
    }

    private void ValidateEquipment(LoadedContent item, EquipmentDefinition equipment)
    {
        RequireReference<ItemDefinition>(item, "$.itemId", equipment.ItemId);
        RequireStableKey(item, "$.slotId", equipment.SlotId, "slot.");
        ValidateStatisticMap(
            item,
            "$.statisticModifiers",
            RequireMap(item, "$.statisticModifiers", equipment.StatisticModifiers));

        IReadOnlyList<string> grantedAbilityIds = RequireList(
            item,
            "$.grantedAbilityIds",
            equipment.GrantedAbilityIds);
        for (int index = 0; index < grantedAbilityIds.Count; index++)
        {
            RequireReference<AbilityDefinition>(
                item,
                $"$.grantedAbilityIds[{index}]",
                grantedAbilityIds[index]);
        }
    }

    private void ValidateAbility(LoadedContent item, AbilityDefinition ability)
    {
        RequireNonBlank(item, "$.displayNameKey", ability.DisplayNameKey);
        RequireNonBlank(item, "$.descriptionKey", ability.DescriptionKey);
        RequireStableKey(item, "$.targetingId", ability.TargetingId, "target.");
        RequireStableKey(item, "$.rulesetId", ability.RulesetId, "rules.");
        RequireAtLeast(item, "$.costAmount", ability.CostAmount, 0);

        if (string.IsNullOrWhiteSpace(ability.AbilityKindId))
        {
            Add(item, "$.abilityKindId", "ability.kind-invalid",
                "abilityKindId must be a supported nonblank ability-kind ID.");
        }
        else if (ability.AbilityKindId is not AbilityKindIds.Skill and not AbilityKindIds.Magic)
        {
            Add(item, "$.abilityKindId", "ability.kind-unsupported",
                $"Unsupported ability kind '{ability.AbilityKindId}'. Supported values are "
                + $"'{AbilityKindIds.Skill}' and '{AbilityKindIds.Magic}'.");
        }

        IReadOnlyList<string> magicDisciplineIds = RequireList(
            item,
            "$.magicDisciplineIds",
            ability.MagicDisciplineIds);
        ValidateAbilityMagicDisciplines(item, ability, magicDisciplineIds);

        if (ability.CostStatisticId is not null)
        {
            RequireReference<StatisticDefinition>(
                item,
                "$.costStatisticId",
                ability.CostStatisticId);
        }
        else if (ability.CostAmount != 0)
        {
            Add(item, "$.costAmount", "ability.cost-without-statistic",
                "costAmount must be zero when costStatisticId is null.");
        }

        IReadOnlyDictionary<string, decimal> numericParameters = RequireMap(
            item,
            "$.numericParameters",
            ability.NumericParameters);
        foreach (string parameterName in numericParameters.Keys)
        {
            if (!LocalIdPattern.IsMatch(parameterName))
            {
                Add(item, $"$.numericParameters.{parameterName}", "parameter.invalid-name",
                    $"Numeric parameter '{parameterName}' must use lowercase kebab-case.");
            }
        }
    }

    private void ValidateAbilityMagicDisciplines(
        LoadedContent item,
        AbilityDefinition ability,
        IReadOnlyList<string> magicDisciplineIds)
    {
        if (ability.AbilityKindId == AbilityKindIds.Skill && magicDisciplineIds.Count > 0)
        {
            Add(item, "$.magicDisciplineIds", "ability.skill-has-magic-disciplines",
                "A Skill ability must not reference magic disciplines.");
        }

        if (ability.AbilityKindId == AbilityKindIds.Magic && magicDisciplineIds.Count == 0)
        {
            Add(item, "$.magicDisciplineIds", "ability.magic-missing-disciplines",
                "A Magic ability must reference at least one magic discipline.");
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < magicDisciplineIds.Count; index++)
        {
            string disciplineId = magicDisciplineIds[index];
            string path = $"$.magicDisciplineIds[{index}]";
            RequireReference<MagicDisciplineDefinition>(item, path, disciplineId);

            if (!seen.Add(disciplineId))
            {
                Add(item, path, "ability.duplicate-magic-discipline",
                    $"Magic discipline '{disciplineId}' is listed more than once by this ability.");
            }
        }
    }

    private void ValidateEnemy(LoadedContent item, EnemyDefinition enemy)
    {
        RequireNonBlank(item, "$.displayNameKey", enemy.DisplayNameKey);
        RequireAtLeast(item, "$.level", enemy.Level, 1);
        ValidateStatisticMap(
            item,
            "$.statistics",
            RequireMap(item, "$.statistics", enemy.Statistics));

        IReadOnlyList<string> abilityIds = RequireList(item, "$.abilityIds", enemy.AbilityIds);
        for (int index = 0; index < abilityIds.Count; index++)
        {
            RequireReference<AbilityDefinition>(item, $"$.abilityIds[{index}]", abilityIds[index]);
        }

        ValidateEnemyFootprint(item, enemy);

        IReadOnlyList<LootEntryDefinition> lootEntries = RequireList(item, "$.loot", enemy.Loot);
        for (int index = 0; index < lootEntries.Count; index++)
        {
            string path = $"$.loot[{index}]";
            LootEntryDefinition? loot = lootEntries[index];
            if (loot is null)
            {
                Add(item, path, "value.null", "Array entries cannot be null.");
                continue;
            }

            RequireReference<ItemDefinition>(item, $"{path}.itemId", loot.ItemId);

            if (loot.Chance < 0m || loot.Chance > 1m)
            {
                Add(item, $"{path}.chance", "loot.invalid-chance",
                    "Loot chance must be between 0 and 1 inclusive.");
            }

            RequireAtLeast(item, $"{path}.minQuantity", loot.MinQuantity, 1);
            RequireAtLeast(item, $"{path}.maxQuantity", loot.MaxQuantity, loot.MinQuantity);
        }
    }

    private void ValidateEncounter(LoadedContent item, EncounterDefinition encounter)
    {
        IReadOnlyList<EncounterEnemyDefinition> enemyGroup = RequireList(
            item,
            "$.enemyGroup",
            encounter.EnemyGroup);
        if (enemyGroup.Count == 0)
        {
            Add(item, "$.enemyGroup", "encounter.empty", "An encounter must contain an enemy.");
        }

        var formationPlacements = new List<FormationPlacement>(enemyGroup.Count);
        var encounterIndexByInstanceId = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int index = 0; index < enemyGroup.Count; index++)
        {
            string path = $"$.enemyGroup[{index}]";
            EncounterEnemyDefinition? placement = enemyGroup[index];
            if (placement is null)
            {
                Add(item, path, "value.null", "Array entries cannot be null.");
                continue;
            }

            EnemyDefinition? enemy = RequireReference<EnemyDefinition>(
                item,
                $"{path}.enemyId",
                placement.EnemyId);
            bool slotIsValid = FormationSlotId.TryParseEnemy(
                placement.SlotId,
                out FormationCell anchor);
            if (!slotIsValid)
            {
                Add(item, $"{path}.slotId", "formation.slot-invalid",
                    $"Enemy slot '{placement.SlotId}' must use canonical form "
                    + "'formation.enemy.r<0-3>.c<0-3>'.");
            }

            if (enemy is null || !slotIsValid)
            {
                continue;
            }

            if (!TryCreateEnemyFootprint(enemy, out FormationFootprint footprint))
            {
                // The enemy's own source file already contains the actionable footprint
                // diagnostic. Skip this placement so encounter validation can continue
                // without repeating the same authoring error at every reference site.
                continue;
            }

            string instanceId = $"enemy-{index}";
            formationPlacements.Add(new FormationPlacement(
                instanceId,
                enemy.Id,
                anchor,
                footprint));
            encounterIndexByInstanceId.Add(instanceId, index);
        }

        foreach (FormationProblem problem in
                 BattleFormationRules.ValidatePlacements(formationPlacements))
        {
            if (!encounterIndexByInstanceId.TryGetValue(
                    problem.InstanceId,
                    out int encounterIndex))
            {
                continue;
            }

            FormationPlacement placement = formationPlacements.First(candidate =>
                string.Equals(
                    candidate.InstanceId,
                    problem.InstanceId,
                    StringComparison.Ordinal));
            string path = $"$.enemyGroup[{encounterIndex}]";
            switch (problem.Kind)
            {
                case FormationProblemKind.InvalidFootprint:
                    Add(item, $"{path}.enemyId", "formation.footprint-invalid",
                        $"Enemy '{placement.DefinitionId}' has nonpositive footprint "
                        + $"{placement.Footprint.Rows} × {placement.Footprint.Columns}.");
                    break;
                case FormationProblemKind.OutOfBounds:
                    Add(item, $"{path}.slotId", "formation.out-of-bounds",
                        $"Anchor {FormationSlotId.Format(placement.Anchor)} with footprint "
                        + $"{placement.Footprint.Rows} × {placement.Footprint.Columns} "
                        + $"leaves the enemy formation at {DescribeCells(problem.Cells)}.");
                    break;
                case FormationProblemKind.Overlap:
                    int conflictingIndex = encounterIndexByInstanceId[
                        problem.ConflictingInstanceId!];
                    Add(item, $"{path}.slotId", "formation.overlap",
                        $"Enemy placement {encounterIndex} overlaps placement "
                        + $"{conflictingIndex} at {DescribeCells(problem.Cells)}.");
                    break;
                case FormationProblemKind.DuplicateInstanceId:
                    Add(item, path, "formation.instance-id-duplicate",
                        $"Battle-local instance ID '{problem.InstanceId}' is duplicated.");
                    break;
            }
        }

        if (encounter.BattlefieldId is not null)
        {
            RequireStableKey(item, "$.battlefieldId", encounter.BattlefieldId, "battlefield.");
        }

        if (encounter.MusicCueId is not null)
        {
            RequireStableKey(item, "$.musicCueId", encounter.MusicCueId, "music.");
        }
    }

    private void ValidateEnemyFootprint(LoadedContent item, EnemyDefinition enemy)
    {
        EnemyFootprintDefinition? footprint = enemy.FormationFootprint;
        if (footprint is null)
        {
            Add(item, "$.formationFootprint", "enemy.footprint-null",
                "formationFootprint cannot be null; omit it to use the 1 × 1 default.");
            return;
        }

        if (footprint.Rows < 1 || footprint.Rows > BattleFormationRules.RowCount)
        {
            Add(item, "$.formationFootprint.rows", "enemy.footprint-rows-invalid",
                $"Footprint rows must be from 1 through {BattleFormationRules.RowCount}; "
                + $"found {footprint.Rows}.");
        }

        if (footprint.Columns < 1
            || footprint.Columns > BattleFormationRules.EnemyColumnCount)
        {
            Add(item, "$.formationFootprint.columns", "enemy.footprint-columns-invalid",
                "Footprint columns must be from 1 through "
                + $"{BattleFormationRules.EnemyColumnCount}; found {footprint.Columns}.");
        }
    }

    private static bool TryCreateEnemyFootprint(
        EnemyDefinition enemy,
        out FormationFootprint footprint)
    {
        EnemyFootprintDefinition? authored = enemy.FormationFootprint;
        if (authored is null
            || authored.Rows < 1
            || authored.Rows > BattleFormationRules.RowCount
            || authored.Columns < 1
            || authored.Columns > BattleFormationRules.EnemyColumnCount)
        {
            footprint = default;
            return false;
        }

        footprint = authored.ToFormationFootprint();
        return true;
    }

    private static string DescribeCells(IEnumerable<FormationCell> cells) =>
        string.Join(
            ", ",
            cells.Select(cell => $"(r{cell.Row},c{cell.Column})"));

    private void ValidateQuest(LoadedContent item, QuestDefinition quest)
    {
        RequireNonBlank(item, "$.displayNameKey", quest.DisplayNameKey);
        RequireNonBlank(item, "$.descriptionKey", quest.DescriptionKey);

        IReadOnlyList<QuestObjectiveDefinition> objectives = RequireList(
            item,
            "$.objectives",
            quest.Objectives);
        if (objectives.Count == 0)
        {
            Add(item, "$.objectives", "quest.no-objectives", "A quest must have an objective.");
        }

        var seenObjectiveIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < objectives.Count; index++)
        {
            string path = $"$.objectives[{index}]";
            QuestObjectiveDefinition? objective = objectives[index];
            if (objective is null)
            {
                Add(item, path, "value.null", "Array entries cannot be null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(objective.Id)
                || !LocalIdPattern.IsMatch(objective.Id))
            {
                Add(item, $"{path}.id", "quest.invalid-objective-id",
                    $"Objective ID '{objective.Id}' must use lowercase kebab-case.");
            }
            else if (!seenObjectiveIds.Add(objective.Id))
            {
                Add(item, $"{path}.id", "quest.duplicate-objective-id",
                    $"Objective ID '{objective.Id}' is duplicated within this quest.");
            }

            RequireStableKey(item, $"{path}.kind", objective.Kind, "objective.");
            RequireStableKey(item, $"{path}.targetId", objective.TargetId, null);
            RequireAtLeast(item, $"{path}.requiredCount", objective.RequiredCount, 1);

            if (objective.Kind == "objective.defeat")
            {
                RequireReference<EnemyDefinition>(item, $"{path}.targetId", objective.TargetId);
            }
            else if (objective.Kind == "objective.collect")
            {
                RequireReference<ItemDefinition>(item, $"{path}.targetId", objective.TargetId);
            }
        }

        IReadOnlyList<QuestRewardDefinition> rewards = RequireList(
            item,
            "$.rewards",
            quest.Rewards);
        for (int index = 0; index < rewards.Count; index++)
        {
            string path = $"$.rewards[{index}]";
            QuestRewardDefinition? reward = rewards[index];
            if (reward is null)
            {
                Add(item, path, "value.null", "Array entries cannot be null.");
                continue;
            }

            RequireReference<ItemDefinition>(item, $"{path}.itemId", reward.ItemId);
            RequireAtLeast(item, $"{path}.quantity", reward.Quantity, 1);
        }

        if (quest.CompletionFlagId is not null)
        {
            RequireStableKey(item, "$.completionFlagId", quest.CompletionFlagId, "flag.");
        }
    }

    private void ValidateStatisticMap(
        LoadedContent item,
        string jsonPath,
        IReadOnlyDictionary<string, int> values)
    {
        foreach ((string statisticId, int value) in values)
        {
            string path = $"{jsonPath}.{statisticId}";
            StatisticDefinition? statistic = RequireReference<StatisticDefinition>(item, path, statisticId);

            if (statistic is not null
                && (value < statistic.MinimumValue || value > statistic.MaximumValue))
            {
                Add(item, path, "statistic.out-of-range",
                    $"Value {value} is outside {statisticId}'s inclusive "
                    + $"{statistic.MinimumValue}..{statistic.MaximumValue} range.");
            }
        }
    }

    private void ValidateEquipmentItemUniqueness()
    {
        foreach (IGrouping<string, EquipmentDefinition> group in _catalog
                     .GetAll<EquipmentDefinition>()
                     .GroupBy(equipment => equipment.ItemId, StringComparer.Ordinal))
        {
            EquipmentDefinition[] definitions = group.ToArray();
            for (int index = 1; index < definitions.Length; index++)
            {
                EquipmentDefinition duplicate = definitions[index];
                Add(PathFor(duplicate), "$.itemId", "equipment.duplicate-item",
                    $"Item '{duplicate.ItemId}' already has equipment behavior defined.");
            }
        }
    }

    private void ValidateStartingClassPool()
    {
        if (_catalog.GetAll<StartingClassRuleDefinition>().Count == 0)
        {
            Add("<content>", "$", "starting-class-pool.missing",
                "At least one starting-class-rules record is required.");
            return;
        }

        if (StartingClassPool.Resolve(_catalog).Count == 0)
        {
            Add("<content>", "$", "starting-class-pool.empty",
                "Starting-class rules resolve to an empty pool; include at least one class that is not excluded.");
        }
    }

    /// <summary>
    /// Converts an explicitly null JSON object to an empty view after recording the error.
    /// This lets validation continue and report independent failures in other records.
    /// </summary>
    private IReadOnlyDictionary<string, TValue> RequireMap<TValue>(
        LoadedContent item,
        string jsonPath,
        IReadOnlyDictionary<string, TValue>? values)
    {
        if (values is not null)
        {
            return values;
        }

        Add(item, jsonPath, "value.null", "Object value cannot be null.");
        return new Dictionary<string, TValue>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Converts an explicitly null JSON array to an empty view after recording the error.
    /// </summary>
    private IReadOnlyList<TValue> RequireList<TValue>(
        LoadedContent item,
        string jsonPath,
        IReadOnlyList<TValue>? values)
    {
        if (values is not null)
        {
            return values;
        }

        Add(item, jsonPath, "value.null", "Array value cannot be null.");
        return Array.Empty<TValue>();
    }

    private TDefinition? RequireReference<TDefinition>(
        LoadedContent item,
        string jsonPath,
        string id)
        where TDefinition : ContentDefinition
    {
        if (!ContentId.IsValid(id))
        {
            Add(item, jsonPath, "reference.invalid-id", $"Reference '{id}' is not a canonical ID.");
            return null;
        }

        string expectedPrefix = ExpectedPrefix<TDefinition>();
        if (!id.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            Add(item, jsonPath, "reference.wrong-category",
                $"Reference '{id}' must identify a {typeof(TDefinition).Name} "
                + $"using the '{expectedPrefix}' prefix.");
            return null;
        }

        if (_catalog.TryGet<TDefinition>(id, out TDefinition? definition))
        {
            ValidateModDependency(item, jsonPath, id);
            return definition;
        }

        Add(item, jsonPath, "reference.missing",
            $"Referenced {typeof(TDefinition).Name} '{id}' does not exist.");
        return null;
    }

    private void ValidateModDependency(LoadedContent item, string jsonPath, string targetId)
    {
        // Base records are validated and shipped independently. A mod may freely reference
        // base content or its own records; cross-mod references must be declared directly in
        // manifest.json so discovery can reject an incomplete installation before gameplay.
        if (string.Equals(item.SourceId, ContentSourceIds.Base, StringComparison.Ordinal)
            || !_sourceById.TryGetValue(targetId, out string? targetSourceId)
            || string.Equals(targetSourceId, ContentSourceIds.Base, StringComparison.Ordinal)
            || string.Equals(targetSourceId, item.SourceId, StringComparison.Ordinal))
        {
            return;
        }

        if (!_dependencyIdsBySource.TryGetValue(
                item.SourceId,
                out IReadOnlySet<string>? dependencyIds)
            || !dependencyIds.Contains(targetSourceId))
        {
            Add(item, jsonPath, "reference.undeclared-mod-dependency",
                $"Reference '{targetId}' is owned by '{targetSourceId}', which must be "
                + $"listed in mod '{item.SourceId}' dependencies.");
        }
    }

    private static string ExpectedPrefix<TDefinition>()
        where TDefinition : ContentDefinition => typeof(TDefinition) switch
        {
            Type type when type == typeof(AbilityDefinition) => "ability.",
            Type type when type == typeof(ActorDefinition) => "actor.",
            Type type when type == typeof(ClassDefinition) => "class.",
            Type type when type == typeof(DialogueDefinition) => "dialogue.",
            Type type when type == typeof(EncounterDefinition) => "encounter.",
            Type type when type == typeof(EnemyDefinition) => "enemy.",
            Type type when type == typeof(EquipmentDefinition) => "equipment.",
            Type type when type == typeof(ItemDefinition) => "item.",
            Type type when type == typeof(MagicDisciplineDefinition) => "magic-discipline.",
            Type type when type == typeof(QuestDefinition) => "quest.",
            Type type when type == typeof(StartingClassRuleDefinition) => "newgame.class-rule.",
            Type type when type == typeof(StatisticDefinition) => "stat.",
            _ => throw new InvalidOperationException(
                $"No content ID prefix is registered for {typeof(TDefinition).Name}."),
        };

    private void RequireStableKey(
        LoadedContent item,
        string jsonPath,
        string value,
        string? expectedPrefix)
    {
        if (!ContentId.IsValid(value))
        {
            Add(item, jsonPath, "key.invalid", $"'{value}' is not a canonical stable key.");
        }
        else if (expectedPrefix is not null
                 && !value.StartsWith(expectedPrefix, StringComparison.Ordinal))
        {
            Add(item, jsonPath, "key.wrong-prefix",
                $"'{value}' must begin with '{expectedPrefix}'.");
        }
    }

    private void RequireNonBlank(LoadedContent item, string jsonPath, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(item, jsonPath, "value.blank", "Value cannot be blank.");
        }
    }

    private void RequireAtLeast(LoadedContent item, string jsonPath, int value, int minimum)
    {
        if (value < minimum)
        {
            Add(item, jsonPath, "value.too-small", $"Value must be at least {minimum}.");
        }
    }

    private string PathFor(ContentDefinition definition) => _pathById[definition.Id];

    private void Add(LoadedContent item, string jsonPath, string code, string message) =>
        Add(item.RelativePath, jsonPath, code, message);

    private void Add(string filePath, string jsonPath, string code, string message) =>
        _problems.Add(new ContentProblem(filePath, jsonPath, code, message));
}
