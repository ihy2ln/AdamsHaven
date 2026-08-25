# Adams Haven — Farm Milestones

This is the contained workstream for the farm simulator. `F` identifiers are
farm milestones and are intentionally separate from the battle M-series. A
milestone is complete only when its exit criteria are met in the farm scene and
its tests/docs are updated.

## Status at a glance

| ID | Outcome | Status | Depends on |
|---|---|---|---|
| F0 | Farm/battle boundary and code ownership | Done | — |
| F1 | Playable 16×16 top-down clearing | Done | F0 |
| F2 | Complete first crop loop | Done | F1 |
| F3 | Farm progression, obstacles, and local save | Done | F2 |
| F4 | Farm presentation and authored flora pass | In progress | F1 |
| F5 | Real battle-completion growth bridge | Next | F3 |
| F6 | Market and Kitchen handoff loop | Planned | F3, F5 |
| F7 | Authored crop/content catalog | Planned | F2, F6 |
| F8 | Farm rules decision pass | Planned | F3, F7 |
| F9 | Expansion plots and dungeon farming | Planned | F7, F8 |
| F10 | Cross-save and release hardening | Planned | F5, F6, F9 |

## Milestone cards

### F0 — Boundary lock — Done

Keep the farm in `Game.Farm`, under `Assets/Scripts/Farm`, with its own scene and
save boundary. Farm may consume a small battle report contract, but it must not
reference or modify the battle implementation.

**Exit gate:** farm and battle compile as separate slices; farm changes leave
`Assets/Scripts/Battle` and `Assets/Scenes/Battle.unity` untouched.

### F1 — Playable clearing — Done

Deliver the top-down 16×16 authored clearing, movement, camera, tile targeting,
obstacle collision, and farm-only HUD.

**Exit gate:** a player can open `Farm.unity`, move around the whole field, and
target a tile with keyboard or pointer/touch input.

### F2 — First crop loop — Done

Deliver one complete crop: clear/till → fertilize (optional) → plant → water →
grow by time or eligible battles → harvest. Keep the simulation deterministic
behind an injected clock.

**Exit gate:** tests cover growth timing, dry-crop stalling, fertilizer speed and
yield, mature harvest, and invalid actions.

### F3 — Progression and local persistence — Done

Deliver level-gated tools, obstacle clearing XP, farm inventory, player position,
plot state, timestamps, and JSON save/load.

**Exit gate:** close/reopen the farm and retain progress; invalid tool level and
obstacle actions are rejected without corrupting the save.

### F4 — Presentation and flora pass — In progress

Finish the authored map presentation: dirt is the playable ground, tilled/wet
states are overlays, camera framing keeps plants and rocks readable, and the
provided flora sheets are separated into reusable transparent sprites.

**Exit gate:** the field reads as one continuous clearing, no opaque placeholder
grid squares remain, and representative separated flora sprites appear in the
farm scene at a useful scale.

### F5 — Real battle bridge — Next

Replace the temporary `B` key completion test with a shared session service that
submits a `FarmBattleReport` when a real battle ends. Keep the farm adapter
one-directional and farm-owned.

**Exit gate:** winning an eligible battle advances watered crops exactly once;
retry, flee, defeat, and scene reload do not award duplicate growth.

### F6 — Market and Kitchen loop — Planned

Implement the town-side receivers for the existing `FarmTownRequest`: sell
produce, choose a recipe, consume ingredients, wait for production, and receive a
dish. Farm remains responsible only for producing and transferring ingredients.

**Exit gate:** one harvested crop can travel Farm → Market or Farm → Kitchen;
inventory is consumed once and the result is visible in the receiving system.

### F7 — Authored crop and content catalog — Planned

Create authored `CropDefinition` and `MaterialDefinition` assets for a small,
curated set: fast crop, battle-grown crop, repeat harvest crop, and a dungeon-only
crop. Add stage art and explicit town/dungeon legality.

**Exit gate:** content is data-authored rather than hard-coded, every crop has a
testable growth/harvest rule, and adding a crop does not require changing the
simulation code.

### F8 — Farm rules decision pass — Planned

Resolve the deliberately open rules before expanding scope: stamina, offline
growth, multiple mature harvests, crop quality, save conflict policy, and whether
town buildings grant farm bonuses.

**Exit gate:** each decision is written down with a testable rule; unresolved
questions do not silently become implementation assumptions.

### F9 — Expansion plots and dungeon farming — Planned

Add a second overgrown/home expansion and a dungeon plot presentation while
reusing the same simulation and legality rules. Do not duplicate farm mechanics
for each presentation.

**Exit gate:** a plot can be selected/opened, has its own allowed crops and
obstacles, and saves without overwriting another plot.

### F10 — Cross-save and release hardening — Planned

Move the local save behind the shared PC/Android transport boundary, define
versioning/conflict behavior, and perform a farm-only release pass for touch
controls, performance, migration, and recovery from malformed saves.

**Exit gate:** a farm save migrates across versions, survives the target device
round trip, and the farm test checklist passes on the release build.

## Brainstorm guardrails

- Work on one `F` milestone at a time; ideas that do not serve its exit gate go
  into a parking list rather than implementation.
- Do not add town pricing, recipes, or battle buffs inside `Game.Farm`.
- Do not edit battle scene/code while completing farm milestones.
- Prefer one playable vertical slice over broad placeholder systems.
