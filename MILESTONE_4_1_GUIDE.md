# Milestone 4.1 guide - deterministic loot resolution

## What this milestone adds

Milestone 4.1 turns already validated loot-table definitions into transient, ordered award
facts. It does not change campaign state.

```text
defeated enemy definition IDs + loot-table content + IRandomSource
    -> LootResolution(LootAward[])
```

`ILootResolver` and `LootResolver` live in the Godot-free core. The resolver reads immutable
content through `IContentCatalog` and consumes caller-injected randomness. It does not receive
`IGameSession`, `GameState`, `InventoryService`, scene nodes, or presentation controls.

## Resolution order

For every supplied defeated enemy definition ID, in supplied order, the resolver:

1. resolves an `EnemyDefinition`;
2. skips it when `LootTableId` is null;
3. resolves its `LootTableDefinition`;
4. validates and evaluates each authored entry in list order;
5. resolves the entry's `ItemDefinition` before rolling it;
6. adds one `LootAward` when the independent chance succeeds.

Two defeated instances of the same enemy definition evaluate the same table twice. Repeated
entries for the same item also remain separate award facts. This milestone deliberately does
not aggregate matching item IDs, calculate stack capacity, or write inventory.

## Random contract

The existing `IRandomSource.Next(minInclusive, maxExclusive)` is the sole randomness boundary.
For an intermediate decimal chance, `LootResolver` requests an integer in `0..999999` and
succeeds when that value is less than `chance * 1,000,000`. Chance `0` and chance `1` are
handled without a random call.

Successful variable quantities use an inclusive range. A fixed quantity does not consume a
quantity roll. The resolver supports an inclusive maximum of `int.MaxValue` without overflow.
The caller owns the concrete random implementation; tests use scripted values, and a future
randomizer can provide a seeded implementation.

## Result model and validation

Each `LootAward` records the enemy definition ID, loot-table ID, item ID, and quantity. A
`LootResolution` defensively copies its award list into a read-only collection. Source content
is never modified.

Normal startup content validation already guarantees typed references and legal chance/range
values. The resolver additionally fails clearly when hand-built or malformed state contains
blank IDs, missing/wrong-category references, null entries, out-of-range chances, or invalid
quantity ranges. It never silently repairs authored data.

## Compatibility

Loot resolution is transient and has no save field. No content schema, mod data API,
`GameState` schema, save format version, or migration changes. Existing loot-table JSON remains
the authoring contract. Data-mod enemies and tables participate automatically once they are in
the resolved catalog and use their stable namespaced IDs.

## Relationship to victory and inventory

Milestone 4.1 still answers only: what independent awards did these defeated definitions
produce? It does not know whether a battle was confirmed, whether an encounter should clear,
whether an inventory stack has room, or what UI should be shown.

Milestone 4.2 now owns that application seam. A confirmed `PartyVictory` carries final-snapshot
enemy definition IDs to `VictoryRewardService`, which calls this resolver once, preserves its
raw awards, submits one atomic inventory batch, and derives separate first-occurrence summary
totals. `BattleCompletionService` sets clearance only after that application succeeds.

## Automated coverage

Focused headless tests prove zero/guaranteed/intermediate chances, inclusive quantities, fixed
quantity roll suppression, empty/null tables, repeated enemies and entries, ordering,
missing/wrong-category failures, source-definition immutability, result immutability, and
repeatable scripted sequences.

## Local validation

Run from the repository root in PowerShell:

```powershell
dotnet test tests/RpgGame.Core.Tests/RpgGame.Core.Tests.csproj

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content

dotnet run `
    --project tools/content-validation/RpgGame.ContentValidation.csproj `
    -- game/content examples/mods

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

The resolver itself continues to exclude inventory mutation, battle completion policy, reward
UI, encounter clearance, experience, gold, item overflow handling, aggregation, save changes,
loot rarity, weighted pools, guaranteed-drop groups, stealing, and conditional drops. The 4.2
application layer composes it without expanding this boundary.
