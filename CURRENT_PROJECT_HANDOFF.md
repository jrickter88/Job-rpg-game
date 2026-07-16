# Current Project Handoff

> Current update: Milestone 4.9 is implemented locally and intentionally uncommitted for review.
> `main` is at `ba94ca0 Add equipment ownership and weapon attack`; the sections below are retained only as
> a historical Milestone 4.1 record and must not be treated as the current repository state.

## Current Milestone 4.9 summary

> Milestone 4.9 extends this summary with an exploration-local equipment menu. It uses the
> read-only `EquipmentMenuProjectionResolver` and authoritative `EquipmentService`; UI state is
> disposable, while equip selections remain in `GameState`. The existing Menu action opens a
> small menu containing Equipment and Controls. This 4.9 work is uncommitted.
>
> 4.9 validation: solution build succeeds with 0 warnings/errors; focused equipment tests pass
> (7 tests); content validation passes for base (27 definitions) and base plus mods (30); Godot
> 4.7 editor validation and a headless game-start smoke test both exit successfully. The complete
> core suite currently has four expected failures caused by the separate unstaged Black Mage
> content edit that moves Ice and Lightning unlocks to levels 3 and 5 while older tests expect
> all three spells at level 1. That content change is intentionally not part of 4.9.

- `ActorProgressState.EquippedItems` persistently maps slots to owned inventory item IDs;
  omitted old-save maps deserialize empty.
- `EquipmentService` validates owner, inventory, equipment category, and authored slot, then
  updates actor progress through `IGameSession`. Empty-slot unequip is a no-op.
- The supported current slot is `slot.weapon.main-hand`. James starts with Iron Sword owned and
  equipped through bootstrap only until 4.9 adds a menu.
- Iron Sword is Attack 4 with no Strength modifier. Only intrinsic basic Attack uses equipped
  weapon Attack and a single 100% weapon damage profile. Mixed profiles remain unsupported in
  battle; all other skills, magic, healing, and enemy attacks are unaffected.
- Local validation: core tests pass with 348 tests; base and base-plus-mod content validation
  pass (27 and 30 definitions); `dotnet build RpgGame.sln --no-restore` succeeds with 0 warnings
  and 0 errors; Godot 4.7 headless editor validation exits successfully.

## Historical 4.1 record

## 1. Repository state and completed milestone

Repository: `jrickter88/Job-rpg-game`

- Branch: `main`, tracking `origin/main`.
- HEAD and `origin/main`: `03c5bbf`.
- Working tree was clean before this handoff file was created.
- Completed engineering milestone: **Milestone 4.1 - Deterministic loot resolution**.
- The 4.1 code is committed and pushed. Its commit subject is misleadingly inherited as
  `5d9d4ed Add persistent inventory stacks`, despite containing the 4.1 resolver changes.
  Inspect the commit diff, not the subject, when orienting to this milestone.

This handoff file is intentionally the only new uncommitted file after the clean-state check.
Do not commit or push it unless the user explicitly requests that action.

## 2. Implementation summary

Milestone 4.1 adds a pure-core loot resolution seam. Given an explicitly ordered list of
defeated enemy definition IDs and an injected `IRandomSource`, `LootResolver`:

1. resolves each `EnemyDefinition` in supplied order;
2. skips enemies whose `LootTableId` is null;
3. resolves the reusable `LootTableDefinition`;
4. evaluates every entry independently in authored list order;
5. returns one typed `LootAward` per successful entry.

Duplicate item awards are intentionally preserved. Two defeated instances of the same enemy
definition independently evaluate the same table. No inventory, `GameState`, battle scene,
campaign flag, save data, or presentation code is changed by resolution.

## 3. Meaningful files changed

| File | Responsibility |
|---|---|
| `src/Rpg.Core/Loot/LootContracts.cs` | Public `ILootResolver`, immutable `LootResolution`, and `LootAward` contracts. |
| `src/Rpg.Core/Loot/LootResolver.cs` | Pure deterministic resolver implementation and malformed-entry safeguards. |
| `tests/RpgGame.Core.Tests/Loot/LootResolverTests.cs` | Scripted-random unit coverage for resolver behavior and boundaries. |
| `src/Rpg.Core/Combat/CombatContracts.cs` | Documents existing `IRandomSource` as a shared core random boundary now used by loot. |
| `src/Rpg.Core/Content/Definitions/LootTableDefinition.cs` | Updates ownership comments to identify the active resolver. |
| `src/Rpg.Core/Content/Definitions/EnemyDefinition.cs` | Updates loot-table ownership comments to distinguish transient awards from later inventory application. |
| `MILESTONE_4_1_GUIDE.md` | Complete 4.1 design, random contract, compatibility, validation, and deferred scope guide. |
| `MILESTONE_4_0_GUIDE.md` | Updates the 4.0-to-4.1 relationship now that resolution exists. |
| `LOOT_TABLE_AUTHORING_GUIDE.md` | Updates authoring guidance from future resolver language to the implemented resolver boundary. |
| `ARCHITECTURE.md` | Records the pure resolver and its separation from inventory/campaign state. |
| `ROADMAP.md` | Adds the completed Milestone 4.1 roadmap entry and identifies 4.2 as next. |
| `AGENTS.md` | Requires reading `MILESTONE_4_1_GUIDE.md` before altering loot resolution. |
| `*.cs.uid` beside the new Loot source/test files | Godot-generated script identifiers; keep them paired with their C# files. |

## 4. Architecture and ownership decisions

- `LootResolver` lives in `Rpg.Core/Loot` and is plain .NET. It depends only on
  `IContentCatalog` and injected `IRandomSource`.
- `IRandomSource` remains declared in `RpgGame.Core.Combat` as the repository's existing core
  randomness contract. Loot reuses it instead of creating a second incompatible random port.
- Loot resolution returns transient facts only. `InventoryService`, `IGameSession`, `GameState`,
  `GameRoot`, `BattleController`, and Godot scenes have no dependency on this milestone.
- `LootResolution` defensively copies awards to a read-only collection. Source definitions and
  the supplied defeated-ID collection are read only; resolver operations do not mutate them.
- Item definitions are resolved before chance evaluation. Missing or wrong-category table/item
  references fail clearly even when a malformed hand-built test/catalog bypasses production
  content validation.

## 5. Public APIs and data structures

```csharp
public interface ILootResolver
{
    LootResolution Resolve(
        IReadOnlyList<string> defeatedEnemyDefinitionIds,
        IRandomSource random);
}

public sealed record LootResolution
{
    public IReadOnlyList<LootAward> Awards { get; }
}

public sealed record LootAward(
    string EnemyDefinitionId,
    string LootTableId,
    string ItemId,
    int Quantity);
```

Chance contract for intermediate values: request `random.Next(0, 1_000_000)` and award when
the returned value is less than `chance * 1_000_000`. Chance `0` and `1` do not consume a
chance roll. Quantities are inclusive; fixed quantities do not consume a quantity roll.

## 6. Content and save compatibility

- No content JSON records changed.
- No content schema versions changed.
- The mod data API remains `3`.
- No `GameState` or save-envelope fields changed.
- `SaveJsonSerializer.CurrentFormatVersion` remains `1`; no migration was added.
- Base and mod-owned enemies/tables participate automatically when present in the resolved
  catalog and correctly namespaced.

## 7. Tests added

`LootResolverTests` covers:

- zero and guaranteed chance behavior;
- scripted intermediate threshold behavior;
- inclusive minimum and maximum quantity results;
- fixed quantities avoiding unnecessary random calls;
- empty tables and null enemy table IDs;
- independently evaluated duplicate enemy definitions;
- independently preserved repeated item entries;
- supplied enemy order and authored entry order;
- missing and wrong-category enemy/table/item references;
- source-definition immutability and read-only result awards;
- identical scripted sequences producing identical award sequences.

## 8. Validation run and exact results

All required automated validation was run during the 4.1 implementation:

```text
dotnet test tests\RpgGame.Core.Tests\RpgGame.Core.Tests.csproj
Passed!  - Failed: 0, Passed: 272, Skipped: 0, Total: 272, Duration: 141 ms

dotnet run --project tools\content-validation\RpgGame.ContentValidation.csproj -- game\content
Content validation passed: 20 definitions loaded from 'D:\RpgGame\game\content' with 0 data mod(s).

dotnet run --project tools\content-validation\RpgGame.ContentValidation.csproj -- game\content examples\mods
Content validation passed: 23 definitions loaded from 'D:\RpgGame\game\content' with 1 data mod(s).

dotnet build RpgGame.sln
Build succeeded. 0 Warning(s), 0 Error(s).

D:\Godot\Godot_v4.7-stable_mono_win64.exe --headless --editor --path . --quit
Exit code: 0, no output.
```

## 9. Validation not run

No required automated validation command was skipped. No manual interactive gameplay check was
run because Milestone 4.1 intentionally changes no Godot scene, battle-controller behavior, or
player-facing presentation.

## 10. Known limitations and possible defects

- No known functional defect was found by the automated suite.
- Intermediate probabilities use a deliberate one-million-step discrete random range. This is
  deterministic and documented, but it is not a continuous random distribution.
- `IRandomSource` is declared in the Combat namespace even though it is now shared by loot.
  This is an intentional reuse of the established core port, not an indication that the resolver
  depends on combat state.
- Award results intentionally do not carry enemy battle-instance IDs. The contract accepts
  defeated enemy definition IDs, so identical instances produce independently ordered but
  definition-level award facts.

## 11. Explicitly deferred scope

Do not add any of the following as part of 4.1 follow-up work:

- inventory mutation or stack-capacity handling;
- battle-controller, `GameRoot`, encounter-clearance, save, or reward-UI changes;
- experience, gold, item aggregation, item rarity, weighted pools, guaranteed-drop groups,
  stealing, conditional drops, or loot-table schema changes;
- any automatic victory-to-award connection.

## 12. Recommended next milestone

**Milestone 4.2 - Victory rewards and reward summary** should come next. It is the intended
application seam: after confirmed `PartyVictory`, obtain defeated enemy definitions, resolve
loot exactly once, pass accepted awards through `InventoryService`, persist the resulting
`GameState`, and present a reward summary before leaving battle.

4.2 should preserve the 4.1 boundary: `LootResolver` determines what independent awards exist;
the application layer decides when/if those facts are granted and how inventory limits are
handled.

## 13. Warnings for the next agent

- Read `AGENTS.md`, `MILESTONE_4_1_GUIDE.md`, `MILESTONE_4_0_GUIDE.md`,
  `LOOT_TABLE_AUTHORING_GUIDE.md`, and the relevant battle-handoff guides before editing.
- Do not infer 4.1 completion from the latest commit subject. Commit `03c5bbf` contains 4.1 but
  is labeled `5d9d4ed Add persistent inventory stacks`.
- `CURRENT_PROJECT_HANDOFF.md` is intentionally uncommitted after this handoff. Do not stage,
  commit, or push it without explicit user instruction.
- Preserve duplicate `LootAward` entries and supplied/authored order. Aggregation belongs to a
  later explicit decision, not the resolver.
- Do not move campaign mutation into `LootResolver`; keep it headless, deterministic, and
  content-only.
