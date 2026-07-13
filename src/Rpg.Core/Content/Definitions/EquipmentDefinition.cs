namespace RpgGame.Core.Content.Definitions;

public sealed record EquipmentDefinition : ContentDefinition
{
    public required string ItemId { get; init; }

    public required string SlotId { get; init; }

    public Dictionary<string, int> StatisticModifiers { get; init; } = [];

    public List<string> GrantedAbilityIds { get; init; } = [];
}

