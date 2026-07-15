# Milestone 4.2 guide - victory rewards and reward summary

## Confirmed-victory timing

Rewards are an application handoff from a confirmed terminal battle, not a combat rule. The
player first reaches a core-authored `PartyVictory`, then confirms the result in
`BattleController`. Only that confirmation creates a `BattleCompletionRequest` and gives
`GameRoot` an opportunity to apply rewards.

```text
Final CombatSnapshot
    -> confirmed BattleCompletionRequest
    -> BattleCompletionService
    -> VictoryRewardService
    -> LootResolver exactly once
    -> InventoryService.AddItems atomically
    -> encounter-clearance flag
    -> RewardSummaryController
    -> confirmed exploration reconstruction
```

Loot is not rolled when combat first becomes terminal, when presentation refreshes, when the
summary is drawn, when Continue is confirmed, during reconstruction, or during save/load.

## Defeated enemy identity and ordering

`BattleCompletionRequest.FromFinalSnapshot` derives defeated enemies from the authoritative
final `CombatSnapshot`. It selects defeated enemy combatants only, preserves combatant order,
stores stable definition IDs, and preserves duplicates. The fixed two-slime victory therefore
passes `enemy.forest.green-slime` twice, causing two independent evaluations of the same table.

The request defensively copies the list and rejects null or blank entries. A defeat request may
name enemies defeated before the party lost, but completion policy never passes that list to
the reward service.

## Atomic reward application

`VictoryRewardService` is plain .NET. It calls `ILootResolver.Resolve` once, preserves every
ordered raw `LootAward`, converts awards to `InventoryAddition` values, and builds an aggregated
`ItemRewardSummary` list in first-award order. Aggregation is only for presentation; raw awards
remain independent facts.

`InventoryService.AddItems` validates the whole batch and current inventory, resolves every
item, combines repeated additions, uses checked arithmetic, enforces `ItemDefinition.MaxStack`,
builds a complete replacement, and calls `IGameSession.UpdateInventory` at most once. Empty
awards publish no inventory update. Invalid IDs, nonpositive quantities, arithmetic overflow,
malformed current stacks, and stack overflow fail without a partial grant.

There is no clamp, second stack, discarded excess, mailbox, or overflow prompt. Player-facing
overflow resolution remains deferred.

## Clearance and duplicate protection

`BattleCompletionService` owns the headless confirmed-completion policy. Party defeat returns
without resolving loot, changing inventory, or setting clearance. For victory it first checks
the stable clearance flag, applies rewards, then sets the flag only after reward application
succeeds. An already-cleared request returns without calling the resolver.

`GameRoot` repeats the cleared check before invoking the service. This composition-level guard
protects against stale or duplicated scene requests, while the service-level guard keeps the
policy directly testable. Reward failure leaves the fixed encounter uncleared and inventory
unchanged.

## Reward-summary ownership

`RewardSummaryController` is a disposable Godot presentation between battle and exploration.
It receives only immutable `ItemRewardSummary` values and `InputBindingService`. It never
receives session state, content definitions, inventory, loot resolution, or randomness.

Matching item IDs appear once with their total quantity. Stable IDs use the current placeholder
short-name formatting. An empty summary displays `No items found.` The current Interact /
Confirm binding is shown, and mouse or bound-key confirmation raises one typed continue request.
Duplicate requests are suppressed. Confirming never rerolls or reapplies rewards.

`GameRoot` explicitly owns the three current gameplay presentations: exploration, battle, and
reward summary. No scene stack, route registry, navigator, autoload, service locator, or global
event bus was introduced.

## Randomness lifetime

`SystemRandomSource` is the production `IRandomSource` adapter and wraps the platform random
generator without loot-specific behavior. `GameRoot` creates it once for the application
lifetime. Automated tests continue to inject scripted sources.

## Save compatibility

Accepted rewards are already in `GameState.Inventory`, and clearance is already in
`GameState.EventFlags`, before exploration returns. The existing save pipeline therefore
preserves both when the player later saves. The summary scene, raw awards, pending rewards, and
combat snapshot are transient and are not saved.

No content JSON, content schema, mod data API, `GameState.SchemaVersion`, or
`SaveJsonSerializer.CurrentFormatVersion` changed. No migration or autosave was added.

## Automated coverage

Headless tests cover atomic multi-item batches, duplicate aggregation, order, exact capacity,
overflow and invalid-later rollback, notification counts, one resolver call, raw and summary
ordering, immutable results, empty rewards, deterministic application, final-snapshot enemy
identity, defeat bypass, duplicate victory protection, clearance-after-inventory ordering, and
reward-failure rollback.

## Manual test procedure

1. Start or load an uncleared campaign and enter the fixed slime encounter.
2. Defeat both slimes and confirm the victory result.
3. Verify the reward summary appears before exploration.
4. Verify item totals appear, or `No items found.` when chance rolls produce no awards.
5. Confirm the summary and verify exploration returns with the marker cleared.
6. Press the confirm key again or reconstruct the room and verify rewards are not granted again.
7. Press K to save to `slot_1`, then L to load it.
8. Verify the encounter remains cleared after load.
9. Exercise a party-defeat path when available and verify it returns directly without rewards
   or clearance.

The authored slime table is chance-based, so a manual victory may legitimately find no items.
Scripted automated tests prove successful-drop behavior independently of that run.

## Local validation

Run from the repository root in PowerShell:

```powershell
dotnet test tests\RpgGame.Core.Tests\RpgGame.Core.Tests.csproj

dotnet run `
    --project tools\content-validation\RpgGame.ContentValidation.csproj `
    -- game\content

dotnet run `
    --project tools\content-validation\RpgGame.ContentValidation.csproj `
    -- game\content examples\mods

dotnet build RpgGame.sln

& "D:\Godot\Godot_v4.7-stable_mono_win64.exe" `
    --headless `
    --editor `
    --path . `
    --quit

if ($LASTEXITCODE -ne 0) {
    throw "Godot validation failed with exit code $LASTEXITCODE"
}
```

## Deferred scope

Milestone 4.2 excludes experience, gold, inventory menus, item use, battle items, equipment,
shops, autosave, multiple stacks, overflow storage or prompts, loot rerolls, rarity, weighted
groups, stealing, conditional drops, boss/quest rewards, animations, sound, localization,
generic navigation, battle save/resume, and content/save schema changes.
