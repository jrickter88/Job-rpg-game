using System.Diagnostics.CodeAnalysis;
using RpgGame.Core.Combat;
using RpgGame.Core.Combat.Formation;
using RpgGame.Core.Content;
using RpgGame.Core.Content.Definitions;
using RpgGame.Core.State;

namespace RpgGame.Core.Tests.Combat;

/// <summary>
/// Shared checked-in-content fixture for Milestone 3.0 tests. It performs no command
/// resolution; it only assembles the explicit inputs to <see cref="CombatSnapshotFactory"/>.
/// </summary>
internal static class CombatTestFixture
{
    public const string JamesId = "actor.hero.james";
    public const string VanguardId = "class.martial.vanguard";
    public const string BlackMageId = "class.magic.black-mage";
    public const string EncounterId = "encounter.forest.slimes-01";
    public const string GreenSlimeId = "enemy.forest.green-slime";
    public const string GuardId = "ability.vanguard.guard";
    public const string TackleId = "ability.enemy.tackle";

    public static FixedBattle CreateFixedBattle(string classId = VanguardId)
    {
        ContentCatalog content = TestContent.LoadCatalog();
        GameState campaign = new NewGameFactory(content).Create(new NewGameRequest
        {
            SaveId = "combat-snapshot-test",
            StartingMapId = "map.prologue.test-room",
            StartingX = 3,
            StartingY = 4,
            StartingPartyMembers =
            [
                new StartingPartyMemberRequest
                {
                    ActorId = JamesId,
                    ClassId = classId,
                },
            ],
        });
        EncounterDefinition encounter = content.GetRequired<EncounterDefinition>(EncounterId);
        IReadOnlyList<FormationPlacement> enemies =
            new EncounterFormationBuilder(content).Build(encounter);
        IReadOnlyList<FormationPlacement> party = PartyFormationBuilder.Build(
            campaign.ActivePartyActorIds);
        CombatSnapshot snapshot = new CombatSnapshotFactory(content).Create(
            campaign,
            encounter,
            enemies,
            party);

        return new FixedBattle(content, campaign, encounter, enemies, party, snapshot);
    }

    public static AbilityDefinition Ability(string id) => new()
    {
        Id = id,
        DisplayNameKey = $"{id}.name",
        DescriptionKey = $"{id}.description",
        TargetingId = AbilityTargetingIds.Self,
        RulesetId = AbilityRulesetIds.Guard,
        NumericParameters = new Dictionary<string, decimal>(StringComparer.Ordinal)
        {
            [AbilityNumericParameterIds.DamageReduction] = 0.5m,
        },
    };
}

internal sealed record FixedBattle(
    ContentCatalog Content,
    GameState Campaign,
    EncounterDefinition Encounter,
    IReadOnlyList<FormationPlacement> EnemyPlacements,
    IReadOnlyList<FormationPlacement> PartyPlacements,
    CombatSnapshot Snapshot);

/// <summary>
/// Deliberately small hand-built catalog used to exercise defensive boundaries that the
/// production content validator normally guarantees before gameplay receives content.
/// </summary>
internal sealed class TestCatalog : IContentCatalog
{
    private readonly IReadOnlyList<ContentDefinition> _definitions;

    public TestCatalog(params ContentDefinition[] definitions)
    {
        _definitions = definitions.ToArray();
    }

    public int Count => _definitions.Count;

    public IReadOnlyCollection<TDefinition> GetAll<TDefinition>()
        where TDefinition : ContentDefinition => _definitions
            .OfType<TDefinition>()
            .OrderBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();

    public TDefinition GetRequired<TDefinition>(string id)
        where TDefinition : ContentDefinition => TryGet(id, out TDefinition? definition)
        ? definition
        : throw new KeyNotFoundException(
            $"Content definition '{id}' was not found as {typeof(TDefinition).Name}.");

    public bool TryGet<TDefinition>(
        string id,
        [NotNullWhen(true)] out TDefinition? definition)
        where TDefinition : ContentDefinition
    {
        definition = _definitions.OfType<TDefinition>().FirstOrDefault(candidate =>
            string.Equals(candidate.Id, id, StringComparison.Ordinal));
        return definition is not null;
    }
}
