# Adams Haven — Farm Foundation

This implementation follows `S:\AI\Game\FOUNDATION.md` §5.4. The wiki is a useful status summary; the foundation document remains authoritative when wording differs.

The contained implementation sequence is tracked in [`FARM-MILESTONES.md`](FARM-MILESTONES.md).

## Repository boundary

Farm and battle live in the same GitHub repository, but they are separate Unity slices. Farm code is isolated under `Assets/Scripts/Farm` and the `Game.Farm` assembly; it references only `Game.Data` and does not reference `Game.Battle`. The farm scene is `Assets/Scenes/Farm.unity`, separate from `Assets/Scenes/Battle.unity`. The only cross-system shape is the small `FarmBattleReport` contract, which allows a future battle adapter to report completed fights without moving battle implementation into the farm module.

## Core loop

1. Clear the 16×16 authored clearing with the correct level-gated tool.
2. Till soil, optionally apply fertilizer, plant a seed, and water it.
3. Let the crop mature through real time or completed battles, whichever finishes first.
4. Harvest produce into inventory.
5. Transfer produce to the future Market for sale or Kitchen for dishes.
6. Use strong, limited-time dish buffs before difficult battles.

The farm does not currently have stamina. The authoritative design lists stamina as an unresolved question, so the foundation does not silently choose an answer.

## Growth model

`FarmSimulation` is presentation-free and deterministic apart from its injected `IFarmClock`. The starter Haven Turnip uses the design target of 600 seconds and can alternatively finish after three battles. Water is required and activates growth; dry time and dry battles are not banked. Basic fertilizer reduces the timer and adds one harvest item.

Time is client-authoritative. The save stores Unix timestamps and deliberately performs no clock validation, matching the design decision that timer manipulation is accepted.

The current state stores one unclaimed mature harvest per plot. Whether offline time should accumulate multiple harvests remains an explicit design question rather than an accidental rule.

## Town and dungeon boundaries

The same simulation supports `Town`, `Dungeon`, and `Both` crop legality. The 16×16 scene is a town plot; Dungeon Glowroot is included as a tested dungeon-only content example but correctly rejects the home plot.

The farm scene uses the authored pixel-art clearing layout plate `farm-layout-clearing-03` as its main environment map. `farm-layout-clearing-01` and `farm-layout-clearing-02` remain staged as alternate layouts for future farm areas; all are visual plates around the same 16×16 playable field.

`FarmTownRequest` is the outgoing town boundary. Only produce can be transferred, and the request names either `SellProduce` or `Cook`. Market pricing, recipes, production queues, dish creation, and combat-buff activation belong to the town/economy layers and are intentionally not duplicated inside the farm.

`FarmTownGateway` is the farm-side adapter for that boundary. The future Market calls `SellProduce`, and the future Kitchen calls `CookProduce`; both receive a normal farm action result after the produce is consumed.

This preserves the one-directional economy:

`Farm → Food → Market/Kitchen → Buffs → Battle`

Town buildings do not currently grant farm stat bonuses; that would contradict the current “buildings unlock verbs” rule unless the design explicitly changes.

## Battle boundary

Battle resolution submits a `FarmBattleReport`. Its completed-battle count advances every eligible watered crop, including endless-mode fights. Optional reward stacks enter the same save inventory boundary for later replacement by the project-wide `MaterialDefinition` economy.

The farm scene’s `B` key submits a temporary one-battle report so the connection can be exercised before `BattleController` is wired to the shared save/session service.

## Save boundary

`FarmSaveData` owns plot state, timestamps, battle count, inventory, farm level, and player position. `FarmSaveRepository` currently persists JSON to `Application.persistentDataPath/adams-haven-farm.json` after successful actions. This is the local half of the planned Android/PC cross-save model; transport and conflict resolution remain undecided.

## Controls

| Input | Action |
|---|---|
| WASD / arrows | Move and face |
| 1–5 | Hoe, watering can, scythe, axe, pickaxe |
| E / Space / Enter | Use selected tool on the facing tile |
| Q | Change selected seed |
| P | Plant |
| F | Apply fertilizer |
| R | Harvest |
| B | Temporary completed-battle test |
| Click/tap | Move or use the selected tool on an obstacle |

Touch controls expose movement, tool/seed selection, fertilizing, planting, and harvesting.

## Verification

`FarmSimulationTests` covers real-time growth, water stalling, fertilizer speed/yield, battle-count growth, town/dungeon crop legality, Market/Kitchen transfers, and obstacle tool/level requirements. The complete EditMode suite passes 132/132 after this foundation.

## Next integration slices

1. Replace the `B` test hook with a shared session/save service called by real battle completion.
2. Replace battle’s placeholder material and potion inventories with the existing `MaterialDefinition`/`DropTable` economy and this shared inventory boundary.
3. Build Market and Kitchen verbs, recipes, timed production, dishes, and battle buff consumption.
4. Create authored `CropDefinition` and `MaterialDefinition` assets; `FarmContentAuthoring` already converts crop assets into runtime rules.
5. Add dungeon plot presentation while reusing the tested farm simulation.
6. Decide stamina, offline multi-harvest accumulation, quality storage, and cross-save transport before expanding those mechanics.
