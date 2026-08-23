# Battle Vertical Slice — status

**Read this first if picking up a fresh session.** This repo lives at
`S:\AI\Game\test\AI.Game` (moved here from `S:\AI\Game\AI.Game` for a folder cleanup —
`S:\AI\Game\` is now `test\` for from-source projects/testing and `play\` for
ready-to-launch builds; see "How to run it" below). Tracks the `feature/battle-slice`
branch (not merged to `main` yet). Original task brief:
`S:\AI\Game\Foundation\CLAUDE-CODE-PROMPT-Battle-Vertical-Slice.md`. Original full
design: `S:\AI\Game\FOUNDATION.md`. **Both are stale on combat model/camera/art
pipeline** — see "What changed from the original design" below before trusting
anything they say about the battle system specifically. Wiki (if reachable — see
"Known gaps"): https://github.com/ihy2ln/AdamsHaven/wiki, pages: Home, Battle-System,
Art-Pipeline, Roadmap. Same content as this file, more browsable.

## TL;DR current state

A playable, real-art, side-view party battle exists and is unit-tested working:
6 named characters (Kestrel/Sable/Linnet vs Husk/Warden/Stinger) plus 3 bench reserves
(Thorne/Reed/Vesper), auto-battle and a manual/turn-based mode (press `T`), HP bars, hit
VFX, win/lose. Runs in-editor (`Assets/Scenes/Battle.unity`) and as a Windows standalone
dev build. Android APK path exists but **not verified on a physical device** — no adb on
this machine.

M6-M8 layer a reviewable **turn log** (`L`), a genuine **three-panel presentation**
(allies dock left, enemies dock right, the acting unit + its target tween into the empty
centre "stage" for each turn's action), and modern-RPG UX: pause (`Esc`), a settings
panel (battle speed, damage-number/log/auto-mode toggles, volume), and a full multi-step
**undo/redo** stack (`Ctrl+Z`/`Ctrl+Y`) alongside the existing full-battle restart.

M9 adds **frontline succession** (a faction's formation auto-compacts when its frontmost
unit dies — the next unit in line becomes frontline, no gaps), **sub-in/sub-out** (swap
an active unit for one of 3 bench reserves, costs the turn), **reposition** (swap column
with an adjacent ally, costs the turn), **healers that can also attack**, and a **2-map
battle sequence** (win map 1, the wounded party carries its HP into map 2).

M11 (this session) is the milestone where M10's Skill Move content **actually reached
the game**. It had been written entirely as C# inside `BattleAssetBuilder` and never
built into the ScriptableObjects, so every character shipped with an empty `skillMoves`
list -- which showed up in play as three apparently separate bugs (SM icon permanently
greyed out, skills 1-3 missing, mana bar never moving) that were all the same cause.
Fixed, then guarded three ways so it can't recur: an Editor-load auto-rebuild
(`BattleContentGuard`), 7 tests that read the *built* assets rather than in-memory
objects, and a boot-time error naming any character with no Skill Moves. SM also
changed from press-and-hold to a **tap**. See item 8 below.

M10 replaces the old two-skill model with a real **Skill Move (SM)
system**: every unit has a free "BA" (Basic Attack) plus 3 mana-cost Skill Moves,
accessed via the compact SM icon. The manual-mode action menu is
now 4 small icons (**BA / SM / R / S**) anchored right under the acting character
instead of a full-width panel. M10 also gave melee-flavoured attacks a walk-up-to-target
approach instead of both units jumping to generic stage marks -- **since superseded by
M15's move to a single, simpler centre-stage beat for every action, see item 12 below.**
See "What changed" below and [[Battle-System]]. **This session also found and fixed a
real, previously unknown bug** (`BattleBootstrap`/`FarmBootstrap` never actually
attaching a `Camera` component, crashing the very first `Play`) — see item 7 below; this
was root-caused via the project owner's own interactive Editor session, the first time
this project's battle scene had actually been played back interactively rather than only
screenshotted via automation.

M15 (this session) reworks how a turn's action reads visually, after two rounds of the
project owner watching it play out and redirecting: every action now moves only the
acting unit to a shared centre-stage mark (the target never moves), replacing both M10's
melee walk-up-to-target *and* an intermediate "attacker crosses fully onto the target's
side" attempt that didn't survive first contact with actual play. Separately, a
character's body animation (its FMV clip) and a skill's visual identity are now fully
decoupled -- every skill move plays the caster's own swing/cast clip (previously
basic-attack-only), while a new optional `SkillEffect` asset carries the skill-specific
impact visual, in preparation for the project owner's planned "skill orb" system. See
item 12 below and [[Battle-System]].

M16 (this session) is a full modern-turn-based combat pass: elements (a 5-way weakness
cycle plus Light/Dark), crit rate/damage, accuracy/evasion, a Break status triggered by
taking too much damage before your next turn, and an ultimate gauge with one real
ultimate skill authored per archetype (matching that archetype's own element). Landed
alongside the HUD to actually see all of it -- a turn-order strip, a break-progress bar,
sprite darkening while broken, a colour-coded "U" action button, and a Unit Stats
inspector (tap a name, or Pause -> Unit Stats). Also fixed two real bugs found live:
an unbounded FMV-clip wait that could soft-lock the whole battle, and a Unity IMGUI
issue where the ultimate button's own animated colour was eating its clicks. See item 13
below and [[Battle-System]].

M17 (this session) finishes M16's combat pass everywhere it didn't reach. Map 2's
Rotfang/Deadeye/Hexweaver are built by `BuildCustomEnemy`, a separate code path that
hardcoded `ElementType.Neutral` and a null `ultimateSkill`, so the whole element and
ultimate layer was invisible on the only fight with distinct enemies -- they now carry
**Earth/Lightning/Fire** and one ultimate each (**Plague Maw**, **Storm Volley**, **Blood
Chorus**), which also means all 5 elements of the weakness cycle are finally live in an
actual battle instead of just Fire/Wind/Water. Alongside it, the **first real
crit/accuracy/evasion pass**: `BattleWorld.RandomizeTestCombatStats` -- M16's explicitly
temporary "spray a random roll on every unit at battle boot so the systems are visible at
all" hack -- is deleted, replaced by five authored `CombatStats` profiles on the assets
themselves. See item 14 below and [[Combat-Systems]]. **These assets need one interactive
Editor rebuild before they're live** -- see "Known gaps."

M18 (this session) adds a **battle skip**: `N`, or Pause -> "Skip to Next Battle",
forfeits the current fight and moves straight to the next stage, carrying the party,
bench and inventory exactly as they stand. Same code path a victory already used, minus
the winning. Guarded against the one way it can break the game -- skipping with a wiped
party -- see item 15 below.

M19 (this session) adds **escape/flee**, the first of the project owner's named
unstarted systems to land. A new **F** button on the manual action row rolls against a
speed-based chance that climbs with every failed try; the turn is spent either way.
Success ends the battle in a new third outcome, `Escaped` -- not a defeat, but not
progress either: no "Next Battle" button, which is the whole cost of the action and the
only thing separating it from M18's skip. See item 16 below.

M20 (this session) gives escaping and quitting something to actually be *about*. A new
**Q** (Quit) action leaves a fight instantly and always works; **F** (Flee) still rolls.
The difference is the haul: enemies now drop **EXP and materials**, escaping banks them,
quitting bins them. Both restore the party to the HP/MP it walked in with, and both land
at a new **camp screen** -- next battle, rest, stat overview, leave dungeon. Quitting is
free when you've just started and ruinous once you're deep, which is the whole point.
Also lands `Tools/typecheck.sh`, which compiles the project without taking Unity's
lock -- see item 17 below.

## What changed from the original design

1. **Combat model/camera — pivoted at M3.** FOUNDATION.md specifies an isometric
   lane/column tactics grid. After seeing the running scene, the project owner asked
   for a genuine **Darkest Dungeon / Slay the Spire / Chaos Zero Nightmare style side
   view**: static 2D, player party left, enemies right, no isometric camera, no free
   repositioning. Implemented as a **single-lane** `MapDefinition`
   (`laneCount = 1`, `columnCount = 6`) — column *is* the horizontal rank, so the
   data layer barely changed. Player ranks = columns 0(back)–2(front); enemy ranks =
   3(front)–5(back); the two melee front-liners land adjacent (2 vs 3). No
   movement/repositioning in this slice (matches the original brief's "push/pull"
   exclusion).
2. **Roster art — pivoted at M4.** The original brief's own ComfyUI pixel-art
   pipeline (`Tools/ComfyUI/`) had real quality problems (weak chroma-key on one
   character, feet cropped by generation framing). The project owner pointed at a
   **separate pre-existing curated asset library**
   (`S:\AI\ComfyUI_windows_portable\ComfyUI\output\aigame\charcter`) with six named
   characters that happened to map exactly onto the 6-archetype roster. **Do not
   regenerate assets** — the project owner was explicit about this. If more/different
   character art is ever needed, check that library first.
3. **Asset Forge** (`S:\AI\Game Engine\assetforge`, a separate custom-built local
   tool, FastAPI+React on port 8420) is **not** the Unity Asset Store voxel tool
   originally assumed. It's an asset library/editor app. Not used to build the game's
   logic (that's all hand-written C# in this repo) — used only to register the
   processed roster assets for the project owner to browse/re-edit later. If asked to
   "use AssetForge to build the game," the judgment call made this session was: keep
   the proven, tested C# pipeline (`BattleAssetBuilder.cs` etc.) as the actual game
   logic, and use AssetForge only where it's genuinely a better fit (asset
   library/editing) — revisit this if the project owner wants something more drastic.
4. **Presentation split into three panels — M7.** The single-lane rank formation from
   M3 still governs targeting/range, but `BattleLayout` now places player docks and
   enemy docks in two clusters near the screen edges instead of one continuous line,
   leaving a wide empty centre gap. `BattleVisuals.MoveToStage`/`ReturnToDock` tween
   the acting unit and its target into that gap for each turn's cinematic beat, then
   back to their dock — so combat logic still only ever reasons about "who acts on
   whom" via `Column`, and the visual staging is a presentation-only concern layered
   on top.
5. **Pause/settings/undo-redo — M8.** `BattleController` now owns `BattleHistory`
   (snapshots unit HP/MP + the full turn log once per consumed turn, whether it
   resolved or was skipped) and drives pause/speed entirely through `Time.timeScale`
   (every wait in the turn loop and in `BattleVisuals`' stage tweens already runs
   through `WaitForSeconds`/`Time.deltaTime`, so this is a two-line change, not a new
   timing system). `BattleSettings` persists via `PlayerPrefs` — first use of it in
   the project.
6. **Frontline succession, bench/reposition actions, healer attacks, 2-map sequence —
   M9.** `Column` on `BattleUnit` is no longer fixed at construction: `Formation.Compact`
   renumbers a faction's alive units to contiguous columns (anchored at that faction's
   front rank) whenever one dies, so "the next unit in line becomes frontline" instead of
   melee's fixed ±1-column pattern silently missing a gapped formation. `BattleWorld` now
   also tracks a 3-unit player `Bench` (Thorne/Reed/Vesper, reskins of the
   melee/ranged/support archetypes with distinct curated art) that a manual-mode turn can
   swap in for an active unit (`BattleController.SubUnit`, costs the turn — the incoming
   unit joins next round since `TurnOrder` now holds a live reference to
   `BattleWorld.AllUnits` instead of a snapshot copy) or swap columns with an adjacent ally
   (`Reposition`, also costs the turn). Manual mode's turn flow changed from
   "pick a target" straight to "pick an action first" (`ActionPhase.ChooseAction` →
   optionally `ChooseBench` → `ChooseTarget`) to make room for these. Healer-archetype
   units (Linnet, Stinger, and bench healer Vesper) gained `CharacterDefinition
   .secondarySkill`, a low-power attack alongside their heal — auto mode heals when an
   ally needs it, attacks otherwise. `BattleWorld` takes a `mapIndex` + optional
   carry-over unit lists so a second map (`Map_BattleSlice2`, distinct background) loads
   after a map-1 victory with the surviving party's current HP intact
   (`BattleController.OnAdvanceRequested` → `BattleBootstrap.BootMap`).
   `BattleHistory` was extended to snapshot/restore `Column` and active/bench roster
   membership alongside HP/MP, since undo/redo now has to unwind those too. See
   "Known gaps" for a real batchmode-only asset-corruption issue hit while verifying this.
7. **Camera bug found + fixed, Skill Move system, compact action UI, melee movement —
   M10.** The project owner opened the Editor interactively (not automation) for the
   first time this project has been played back that way, and hit an immediate crash:
   `camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>()` in both
   `BattleBootstrap.cs` and `FarmBootstrap.cs` never actually attached a `Camera` --
   Unity 6's component binding can return a non-CLR-null wrapper for "component not
   found," so `??` (plain reference-null check) skipped `AddComponent` entirely.
   `MissingComponentException` on `cam.orthographic = true` in `BattleLayout
   .ApplyBattleCamera`. Fixed by using an explicit `== null` check (which uses Unity's
   overloaded equality, correctly detecting the fake-null) instead of `??` in both files
   -- this bug predates M9 and would have hit any prior session that tried Play, not
   something introduced this session. Once fixed, the rest of this session was a real
   back-and-forth UI/content pass driven by the project owner actually playing the game:
   - **`CharacterDefinition.secondarySkill` (M9) replaced by `skillMoves` (List, up to
     3)** -- `standardSkill` is now always the free "BA" for every archetype (Healers'
     BA became their attack; Heal itself moved into `skillMoves`). `BattleController
     .SkillMoveOptions`/`.BasicAttackSkill` read these; `EffectiveMpCost` scales
     `skill.mpCost` by the new `Settings.MpCostMultiplier` dev slider.
   - **9 new skills**, 3 per archetype, heal/damage primitives only (explicit scope
     call -- no status-effect system yet): Melee gets Second Wind (self-heal)/Rally
     (heal an ally)/Power Strike (heavy hit); Healer gets Heal/Mass Heal (AoE
     heal)/Focus Heal (big single heal); Ranged gets Volley (3-wide AoE)/Snipe (heavy
     single hit)/Barrage (guaranteed full-team AoE, ±5-column area). `BattleController
     .ResolveAction`'s heal branch was generalized to support AoE the same way the
     damage branch already did (`TargetResolver.GetAreaTargets` when
     `pattern.areaOffsets.Count > 1`), needed for Mass Heal.
   - **Compact action menu**: `BattleHud.DrawActionMenu` now draws 4 small icon buttons
     (BA/SM/R/S) anchored at the acting unit's true visual feet -- `DockPosition` is the
     sprite's *pivot*, which is Center (Unity's default import pivot) not Bottom, so the
     anchor steps down `BattleLayout.TargetUnitHeight / 2` first. Horizontally clamped
     (`ClampedLeftX`) so it can't run off-screen for an edge-column unit. SM's own click
     is ignored; a separate `Update()`-driven hold-timer (`SmHoldSeconds = 0.35f`, real
     `Time.unscaledTime` so pause/speed settings don't affect it) opens a skill-list
     popup once the button's been held long enough, listing each Skill Move with its MP
     cost and greying out ones the unit can't currently afford.
   - **Melee approach movement**: `BattleVisuals.MoveToMelee` -- for any attack that
     isn't ranged or a heal/self-buff (`BattleController.IsMeleeAction`), the attacker
     walks to just beside the target (target stays put) instead of both units jumping to
     the generic centre-stage marks `MoveToStage` uses for everything else.
     `ReturnToDock` (unchanged) handles the trip back either way.
   - **HP/MP bars**: shrunk and given a companion MP bar per unit
     (`Definition.maxMp`, default 100, everyone starts full). Found and fixed a real
     rendering bug along the way: `DrawBarFill`'s padding (4px) exceeded the MP bar's
     height (4px), leaving zero pixels for the fill regardless of the underlying value --
     looked exactly like "MP never shows/decreases" even though the data was always
     correct. Padding is 1px/side now, bar height bumped to 6px.
   - **Dev-tuning sliders** (Settings panel): damage dealt (0.25x-5x) / damage received
     (0x-2x) multipliers applied in `ResolveAction`, and an MP-cost multiplier (0x-2x,
     0x = free Skill Moves) for testing skills repeatedly without waiting on regen
     (there isn't any yet -- MP only ever decreases once spent).
8. **Skill Moves actually shipped, content-staleness guard, SM tap -- M11.** Everything
   in item 7's Skill Move system was real, compiled, and unit-tested, and **none of it
   was in the game.** `BattleAssetBuilder` authors the 9 skills in C#, but
   `AI.Game > Battle > Build Assets From Manifest` was never run, so every
   `CharacterDefinition` on disk still had an empty `skillMoves` list. Three symptoms,
   one cause: `BattleHud.DrawActionMenu` enables SM on `SkillMoveOptions.Count > 0`
   (empty -> greyed out forever, with no explanation surfaced anywhere), and
   `BattleController.ChooseAutoSkill` looks in `skillMoves` for a `targetsAllies` entry
   to decide whether a healer heals (empty -> always fell through to the free 0-MP BA,
   so no unit ever spent MP and the bar looked broken).
   **Why nothing caught it:** every existing test builds its subjects in memory via
   `BattleTestHelpers.MakeUnit` (`ScriptableObject.CreateInstance`), so the whole
   22-test suite passed against correct logic while the shipped content was empty.
   Nothing read `Resources/Battle`. Fixed in three layers:
   - **`BattleContentGuard.cs`** (new, `Scripts/Editor/`) -- `[InitializeOnLoad]` hook
     that checks on every Editor load whether `Resources/Battle` matches what the
     builder currently authors and re-runs `BattleAssetBuilder.Build()` if not. Two
     independent staleness checks: a `ContentVersion` stamp in `EditorPrefs` (catches
     "builder C# is newer than disk") and a direct probe of the loaded
     `CharacterDefinition`s (catches "the assets are wrong right now" regardless of the
     stamp -- e.g. after a `git revert` of `Resources/Battle`). **Hard-bails on
     `Application.isBatchMode`**, so it can never fire in the headless path that
     corrupts `m_Script` references (see "Known gaps"). Bump
     `BattleAssetBuilder.ContentVersion` whenever the authored content changes shape.
   - **`BattleAssetContentTests.cs`** (new, 7 tests) -- asserts against the real assets
     via `Resources.Load`. Covers: free BA with a pattern on every character; exactly 3
     distinct Skill Moves each (distinct `skillId` *and* `displayName`, see below);
     every move affordable from a full MP pool with at least one costing MP; healers
     attack with BA and heal from `skillMoves`; melee BAs not flagged `isRanged` (which
     would silently lose the walk-up animation); the 3 AoE patterns really covering more
     than one column; both maps fielding 3 enemies with a background.
   - **`BattleWorld.WarnOnStaleContent`** -- `Debug.LogError` at boot naming any
     character with no Skill Moves. A standalone build has its assets already baked and
     the guard can't help there, so a loud log line is the only available signal.

   Two more real bugs found on the way, both caught by inspecting the rebuilt assets
   rather than by playing:
   - **Every ally-targeting skill displayed as "Heal."** `DrawSkillListPopup` labelled
     rows `skill.targetsAllies ? "Heal" : skill.displayName`, so Kestrel's SM list
     rendered as *Heal (30 MP) / Heal (25 MP) / Power Strike* -- Second Wind and Rally
     both masquerading as Heal -- and Linnet's as *Heal / Heal / Heal*. `targetsAllies`
     means "aims at my side," not "is the heal." Now always `displayName`, and the
     `Skill_SupportBasic` asset was renamed from "Support Basic Attack" to "Heal"
     (`skillId: skill_support_heal`) since it's no longer anyone's basic attack.
   - **Another latent `??`-on-a-`UnityEngine.Object`** -- `LoadBackgroundSprite(...) ??
     manifestBackground` in `BattleAssetBuilder`, the same trap as item 7's camera bug.
     Harmless today (both backgrounds exist) but it would have silently skipped the
     fallback the moment one didn't. Replaced with an `OrFallback<T>` helper using
     Unity's overloaded `== null`.

   **SM is a tap now.** Press-and-hold became `_showSkillList = !_showSkillList`, and
   the whole hold apparatus (`SmHoldSeconds`, `_smButtonRect`, `_smHoldStartTime`, and
   the `Update()` polling behind them) is gone. Holding read as an unresponsive button:
   tapping SM did nothing and nothing on screen hinted that holding was the gesture,
   especially with BA/R/S all being taps. Tapping again closes the list, which is also
   the only way to back out without committing to a skill.
9. **MP economy (a first pass) and FMV chroma-key components -- M12.** Direction from
   the project owner: verify Skill Moves by code-based stats rather than interactive
   play (item 1 below), then build a real MP economy using arbitrary numbers (not a
   tuned balance pass), and build the chroma-key/VideoPlayer plumbing FMV clips need
   even though final clip assets/pipeline aren't chosen yet.
   - **MP economy.** `BattleUnit` gained `SpendMp`/`RestoreMp` (both clamp to
     `[0, MaxMp]`), `RestoreMpFull`, and `RecoverMpAfterBattle`. Four sources, per the
     project owner's design: (1) a small passive trickle from the true BA specifically
     (`BasicAttackMpRegen = 4`, gated on `skill == unit.Definition.standardSkill` so a
     Skill Move that happens to cost 0 MP via the dev-tuning slider can't be farmed for
     it); (2) 25%-50% of the *missing* MP restored per unit when a carried-over roster
     loads map 2 (`BattleWorld`'s existing carry-over path -- deliberately distinct from
     HP, which still does not recover between maps, since the carried wound is the
     point); (3) a full restore hook (`RestoreMpFull`) for a future farm/town "sleep to
     recover" system -- not called by anything yet, since no persistence layer connects
     battle party state to the farm scene; (4) a new Support Skill Move, **Mana Spring**
     (15 MP, restores ~20 MP to an ally at Linnet's base magic), the first skill to use
     a new `SkillDefinition.restoresMana` flag that routes `BattleController
     .ResolveAction`'s ally-targeting branch to `DamageCalculator.ComputeManaRestore`
     instead of `ComputeHeal`. Found and fixed a real bug while adding it:
     `ChooseAutoSkill`'s auto-heal lookup (`skillMoves.FirstOrDefault(s =>
     s.targetsAllies)`) would just as easily have handed auto mode Mana Spring instead
     of the real heal once a second ally-targeting move existed on the same unit --
     fixed by excluding `restoresMana` from that filter.
   - **FMV chroma-key components.** `Assets/Shaders/ChromaKeyVideo.shader` (Unlit,
     discards pixels near `ClipEntry.chromaKey` within `chromaTolerance`, `Cull Off` so
     a facing-flipped quad still renders) and `BattleClipPlayer.cs` (one per unit view,
     built inactive in `BattleVisuals.BuildUnitView`; owns a `VideoPlayer` targeting a
     `RenderTexture` fed into a runtime quad using that shader). `BattleVisuals` exposes
     `HasActionClip`/`PlayActionClip`; `BattleController.PlayImpactBeat` (new, replacing
     a wait that was duplicated verbatim in both turn-flow methods) plays the clip when
     one exists, else falls back to the original flat pause. **Deliberately restricted
     to the true BA only** -- every Skill Move currently shares `clipKey: "basicAttack"`
     (`BattleAssetBuilder.BuildSkillMove`), so playing it for e.g. Power Strike would
     show the wrong (generic melee-swing) clip; Skill Moves keep the sprite+flash/
     impact-FX presentation until they get dedicated clips. This is reachable *today*
     with the 3 real clips M1/M2 already generated (`clip_melee_basic`/`clip_ranged_
     basic`/`clip_heal_basic`, imported and referenced by `Clips_MeleeBasic`/etc.) --
     not just scaffolding for hypothetical future assets.
     **Found, and later fixed, a real, separate bug while building this:** the
     `impactFrames` metadata baked into those Clip assets read as corrupted --
     `Clips_MeleeBasic`'s was `12000000` where the manifest's own source data says `18`;
     `Clips_SupportBasic`'s concatenated two values the same way. `BattleClipPlayer`
     failed safe against it either way (a threshold that's never reached just never
     fires `onImpact`, no crash), so it didn't block anything at the time. **Fixed
     later this session** -- see "Known gaps" for the investigation (root cause not
     fully confirmed; a `JsonUtility` array-parsing edge case is suspected) and the fix
     (hand-corrected to the manifest's real values, guarded by new `ClipMetadataTests`).
   - **Item 1: code-based skill verification, not interactive play.** Two new test
     files, `MpRegenTests.cs` (7 tests: spend/restore clamping, the 25%-50% recovery
     band via 50 rolls, `ComputeManaRestore` scaling) and additions to
     `BattleAssetContentTests.cs` (`SupportUnits_HaveAnAffordableManaRestoreSkill`,
     asserting a real `ComputeManaRestore` value against Linnet's actual base stats,
     plus the Support archetype's expected Skill Move count moving from 3 to 4). All
     pure C#, all safe headless. `BattleAssetBuilder.ContentVersion` bumped to 4.
10. **Battle-carried potions, a real status-effect system, per-turn MP regen -- M13.**
    Direction from the project owner across five areas at once; here's what shipped
    against each:
    - **3 battle potion slots (Hp/Mp/Multi), F-SSS ranked, up to 99 each.** New
      `BattleInventory` (`Scripts/Battle/BattleInventory.cs`) -- exactly 3 fixed slots,
      not a general inventory system. New `PotionDefinition` ScriptableObject
      (`Scripts/Data/Economy/`) reuses the existing `Tier` enum (F..SSS) for rank rather
      than inventing a parallel scale. `PotionCalculator.Potency(Tier)` is a flat
      restore-amount table per rank (20 at F up to 300 at SSS) -- arbitrary numbers, not
      a tuned balance pass, same convention as every other number this session. A Multi
      potion restores the *same* amount to both HP and MP rather than a split/reduced
      figure -- simplest rule, worth revisiting once potions have a real economy to
      balance against. `BattleWorld` owns `Inventory`, seeds a placeholder 5-of-each
      C-rank stock on a fresh battle (`SeedPlaceholderInventory` -- there's no shop/farm
      system yet to source real starting stock from), and carries it through map 1→2
      the same way HP/bench do. New manual-mode action: a 5th icon, **I**, tap to open a
      popup listing all 3 slots (name/rank/count, greyed at 0), tap one, then pick a
      target the same way Heal does (any living ally, not just ones missing HP/MP --
      matches how Heal already lets you "waste" it on a full-HP unit). Free (no MP
      cost, it's a physical item) but costs the turn. `BattleHistory.Capture/Undo/Redo`
      gained an optional `BattleInventory` parameter (defaults to null, so every
      pre-existing call site including every test keeps compiling unchanged) --
      without it, Undo after using a potion would restore HP/MP but leave the count
      spent, an exploitable free-duplicate bug. `BattleController` always passes
      `World.Inventory`, so it's covered in the real game.
    - **A real status-effect system, standard JRPG shape.** New `StatusEffectType` enum
      (`AttackUp/Down`, `DefenseUp/Down`, `Poison`, `Regen`, `Stun`) and
      `StatusEffectInstance` (type + magnitude + remaining turns). `BattleUnit` owns a
      `StatusEffects` list, `ApplyStatus` (refreshes an existing effect of the same type
      in place rather than stacking a second instance -- standard convention, and avoids
      unbounded stacking from repeated casts), `AttackMultiplier`/`DefenseMultiplier`
      (net Up minus Down, folded into `DamageCalculator.ComputeDamage`'s offense/defense
      stats), and `TickStatusEffects` (applies Poison/Regen's flat HP tick, decrements
      every effect's clock, drops expired ones). Ticking happens once at the start of
      the *affected unit's own turn* -- not globally per round -- in
      `BattleController.RunBattle`, alongside the new passive MP regen (same
      per-turn hook point). `IsStunned` is checked *before* ticking so a 1-turn Stun
      skips exactly one turn (checked pre-tick → skip → tick counts 1→0 and removes
      it → next turn acts normally); a unit poisoned to death on its own tick gets the
      same death bookkeeping (`SyncDefeated`, `Formation.Compact`) a combat kill gets,
      just without the animated reflow. `SkillDefinition` gained
      `inflictsStatus`/`statusMagnitude`/`statusDuration`, applied to every unit in
      `hitTargets` up front in `ResolveAction` (before the heal/mana/damage branches,
      since a status effect isn't tied to which of those actually fires). **Retrofit
      onto 5 existing Skill Moves**, additive on top of their already-tuned power/mpCost
      (no rebalancing): Second Wind and Focus Heal also grant Regen (10 HP/turn, 2
      turns); Power Strike also applies Defense Down (-20%, 2 turns); Snipe also applies
      Attack Down (-20%, 2 turns); Barrage also applies Stun (1 turn) to everyone it
      hits -- a full-team AoE stagger, clearly the strongest of the five, flagged as
      worth a second look once there's more content to compare it against. Poison
      exists in the system (implemented, tested) but isn't authored onto any skill yet
      -- ready for whichever future skill wants it. A small HUD addition: each unit's
      roster readout now shows abbreviated status tags with turns remaining (`PSN(2)`,
      `ATK-(1)`, `DEF+(3)`) under its MP bar.
    - **Per-turn passive MP regen**, on top of M12's existing BA-specific trickle:
      `PassiveMpRegenPerTurn = 3`, applied to every unit at the start of its own turn
      regardless of chosen action (even a skipped/stunned one) or faction. Per the
      project owner's framing: small per tick, but real over a long battle.
    - **FMV clips: explicitly deferred**, per the project owner -- "worry about getting
      a lot of clips/assets later after the foundation is laid out." No FMV work this
      session; noted for whenever that's revisited.
    - **Android-first platform priority, stated explicitly for the first time**: must be
      fully playable start-to-finish on Android, then Windows, then iPhone last (not
      started). See "Known gaps" -- this elevates on-device Android verification from
      "whenever adb is available" to the top of the priority list, still blocked on
      `adb` access this session.

    New tests: `StatusEffectTests.cs` (8), `PotionTests.cs` (4), plus 2 more in
    `BattleAssetContentTests.cs` and 1 more in `BattleHistoryTests.cs` (inventory
    undo/redo). All pure C#, all safe headless -- 53 total, 51 passing as of this
    write-up (the 2 new asset-content tests fail until the next interactive rebuild
    writes the potion assets + status-effect fields to disk, same pattern as every
    prior content addition this project has made). `BattleAssetBuilder.ContentVersion`
    bumped to 5.
11. **`impactFrames` re-fix, map-2-only enemy roster, offensive-Skill-Move AI -- M14.**
    - **`impactFrames` regressed and got a real fix.** The M12 hand-fix reverted to the
      exact same corrupted values the very next time the project owner rebuilt (for
      M13's content) -- confirming `BuildClipSet` overwrites `impactFrames` from the
      manifest's `impact_frames` field on every `Build()` run, and that field
      deserializes to the same wrong numbers deterministically, so a hand-edit alone
      could never survive. Root cause still unconfirmed. Fixed by no longer trusting
      that field at all: `BattleAssetBuilder.KnownGoodImpactFrames` authors the 3
      known-correct values directly in C#. `ContentVersion` bumped to 6.
    - **Map 2 gets its own enemy roster.** Requested per the project owner ("more enemy
      types") specifically so the M13 status-effect system has something real to
      exercise it -- map 1's Husk/Warden/Stinger are flat player reskins with no
      offensive Skill Move an AI would ever reach for, so Poison/Attack Down/Defense
      Down had nothing to prove they worked beyond unit tests. **Rotfang** (melee,
      reuses Thorne's art, hp150/atk26/def16/spd8 -- bulkier than Husk) inflicts
      **Poison** via Venom Strike. **Deadeye** (ranged, reuses Reed's art,
      hp100/atk24/spd13 -- a glass cannon, faster than Warden) inflicts **Attack Down**
      via Crippling Shot. **Hexweaver** (support, reuses Vesper's art,
      hp105/mag22/res14 -- bulkier caster than Stinger) inflicts **Defense Down** via
      Weaken, and can still Heal its own side. All 3 reuse Thorne/Reed/Vesper's
      already-imported art+clips (`BuildCustomEnemy`, new -- borrows
      battleSprite/portrait/pixelSprite32/clips directly from an existing
      `CharacterDefinition` instead of going through the manifest-sprite path) --
      **no new art generated**, per the project owner's "worry about assets later"
      direction. `BattleAssetBuilder.BuildMap` no longer hardcodes which characters go
      in a map; it takes an explicit `List<EnemyPlacement>` from its caller now, so map
      1 and map 2 can genuinely differ. `ContentVersion` bumped to 7.
    - **The AI needed to actually use these kits, or they'd be dead content again.**
      `BattleController.ChooseAutoSkill` previously only ever chose BA or the one
      heal carve-out -- "Auto mode never reaches for the other skillMoves entries...
      those stay a manual-only tactical choice," per M9's own doc comment. That meant
      *any* offensive Skill Move (including the M13 status-inflicting retrofits on
      Power Strike/Snipe/Barrage, and now Rotfang/Deadeye/Hexweaver's whole kit) was
      unreachable by any AI-driven turn -- every enemy turn, and every player turn in
      auto mode. Added `OffensiveSkillMoveChance = 0.35f`: a 35% chance per turn to
      reach for an affordable, valid non-heal Skill Move instead of BA. Applies
      symmetrically to both factions, so auto-battling player units now also
      occasionally use Power Strike/Snipe/Barrage instead of only ever basic-attacking
      -- an emergent improvement beyond the original ask, not just an enemy-specific
      hack. Arbitrary chance, not tuned.
    - New tests: `Map2_FieldsADifferentRosterThanMap1`, `Map2Enemies_HaveAFreeBaAndAnOffensiveStatusSkill`,
      `Hexweaver_CanHealItsOwnSide` (all in `BattleAssetContentTests.cs`). The
      `OffensiveSkillMoveChance` AI branch itself isn't unit-tested -- it lives on
      `BattleController`, a `MonoBehaviour` that needs a live scene, matching this
      project's established "orchestration verified by interactive play" split.
      **Status-effect verification explicitly deferred by the project owner** until
      these new enemies can be played against -- "we can't really check the status
      effect step but we can check that at another time after we get more enemy
      types."
12. **Centre-stage-only movement, body/skill-effect decoupling -- M15.** The project
    owner explicitly ruled out rigged skeletal models for now ("keep the original
    path") and asked instead for two presentation changes, refined twice by watching the
    result in the project owner's own live Editor session:
    - **Movement, after two live iterations.** The first attempt (per "the aggressing
      unit will crossover to the aggressed side") had any offensive action's attacker
      cross fully onto the target's dock side (`BattleVisuals.MoveToAggressedSide`, a
      generalization of M10's melee approach to ranged skills too). A screenshot from
      the project owner's own play session showed the attacker travelling much further
      than intended, and the follow-up direction was simpler: "make the aggressor move
      to the middle" -- `BattleLayout.StagePosition`/`MoveToStage` already did exactly
      that (it's the same "meet near centre, each on your own faction's side" mark M7's
      three-panel layout introduced), so the fix was to route every action through it
      instead of introducing a new method. A third refinement ("only the
      attacking/action unit will move to the center") removed even the target's half of
      that tween -- `MoveToStage` now only moves the actor; the target stays on its dock
      for the whole beat. Net result: `MoveToAggressedSide`, M10's `MoveToMelee`, and
      `BattleController.IsMeleeAction` are all deleted -- every action (melee, ranged,
      ally-targeting alike) now shares one movement path.
    - **Body animation vs. skill effect, fully decoupled.** Prompted by the project
      owner's plan to eventually add per-character attack/special animations and let
      "skill orbs" grant animations to units. `BattleVisuals.HasActionClip` no longer
      restricts FMV clip playback to `skill == unit.Definition.standardSkill` -- every
      Skill Move now plays the caster's own body clip (swing/cast/shoot) too, not just
      the plain basic attack, since `clipKey` was already identical across every skill on
      a unit (`BattleAssetBuilder` has set it to `"basicAttack"` uniformly since M10 --
      this gate was the only thing stopping Skill Moves from using it). A skill's own
      visual identity moved to a new field instead: `SkillDefinition.effect`, an optional
      reference to a new `SkillEffect` asset (`Scripts/Data/Combat/SkillEffect.cs`, a
      standalone impact-flipbook asset -- same sheet+frame-rect shape `MapDefinition`
      already used for its generic impact FX, but skill-specific and shareable across
      skills/orbs rather than one-per-map). `BattleVisuals.PlayImpactFx` now takes the
      acting skill and uses `skill.effect` when authored, falling back to the map's
      generic impact sheet otherwise -- true of every skill today, since no `SkillEffect`
      assets exist yet, so this is pure plumbing ahead of content, same pattern as every
      other "provided later" system in this project (FMV clips, potions, etc.).
    - **No new `ContentVersion` bump.** Nothing here required rebuilding
      `Resources/Battle` -- `SkillDefinition.effect` is a new optional field that
      defaults to null on every existing asset, and `clipKey` values were already
      correct; only the code reading them changed.
13. **Elements, crit/accuracy, Break, ultimate gauge -- M16.** A full request for
    "modern AAA turn-based" mechanics, built as a testable math/data layer first, then
    the HUD to actually see it, in the same session.
    - **Elements.** `ElementType` gains `Lightning` (7 elements + Neutral). New
      `ElementChart.cs` (pure C#): a 5-way cycle (Fire>Wind>Earth>Lightning>Water>Fire)
      plus a mutual Light/Dark rivalry, 1.5x/0.67x multipliers. `SkillDefinition.element`
      already existed (for the unconnected roll-pool system) but was unused by every
      Skill Move -- now every archetype's `CharacterDefinition.element` and all of its
      Skill Moves/ultimate carry a real element (Melee=Fire, Ranged=Wind, Support=Water),
      previously always Neutral.
    - **Crit/accuracy.** `StatBlock` gains `critRate`/`critDamage`/`accuracy`/`evasion`.
      All default to 0 -- deliberately harmless differently for each: 0 `critRate` never
      crits, but 0 `accuracy` is treated as "always hits" (`DamageCalculator.HitChance`),
      not "always misses", so pre-M16 content can't suddenly start whiffing. `ComputeDamage`
      takes a pre-rolled `isCrit` bool rather than rolling internally, staying 100%
      deterministic/testable -- the actual `UnityEngine.Random` rolls live in
      `BattleController.ResolveAction`, same split M14's `OffensiveSkillMoveChance`
      already established (pure math tested, the roll itself verified by play).
    - **Break.** New `StatusEffectType.Break` -- taking >=30% of a unit's own max HP in
      damage since its last turn triggers it right as the next turn comes up: skips that
      turn like Stun (`BattleUnit.IsIncapacitated` now covers both) and adds bonus
      damage taken while active (folded into `DefenseMultiplier`). A red break-progress
      bar (HUD) shows the buildup before it triggers; the sprite darkens while active
      (`BattleVisuals.SyncStatusTint`) -- added after "I couldn't tell if a unit was
      broken."
    - **Ultimate gauge + skills.** `BattleUnit.CurrentUltimateCharge` (0-100) fills from
      a per-action grant plus a smaller per-turn trickle, mirroring the MP economy's
      exact shape. One ultimate authored per archetype in `BattleAssetBuilder`
      (`ultimateSkillByArchetype`) matching that archetype's element: **Inferno Blade**
      (Melee/Fire, heavy hit + Defense Down), **Gale Storm** (Ranged/Wind, full-team AoE
      + Attack Down), **Tidal Renewal** (Support/Water, full-team heal + Regen).
      Gauge-gated, not MP-gated -- `ResolveAction` drains the gauge to 0 for whichever
      skill `== Definition.ultimateSkill` instead of spending MP. Auto mode always uses
      it the instant it's ready. `ContentVersion` bumped to 8.
    - **Random test stats.** No character had real crit/accuracy numbers authored at
      this point (a real balance pass was future work), so
      `BattleWorld.RandomizeTestCombatStats` rolled a random-but-playable value per unit
      at battle boot -- mutating each unit's own private `CharacterInstance`, never the
      shared `CharacterDefinition` asset, so it couldn't leak state across units or
      battles. Explicitly temporary per its own doc comment; **replaced in M17 by
      authored `CombatStats` profiles and deleted** (item 14 below).
    - **HUD additions.** A turn-order strip under the title (small battle-sprite icons,
      right-to-left, rightmost = next to act, numbered 1-7 -- refined from an initial
      centre-fan layout after the project owner asked for a clearer direction). A "U"
      action button between SM and R, colour-coded like the rest of the row. A Unit
      Stats inspector -- tap a name in the roster for one unit, or Pause -> "Unit Stats"
      for everyone -- both with a "Max Ultimate (Test)" button so ultimates can be
      tested without playing through a whole battle to charge the gauge.
    - **Two real bugs found and fixed live.** (1) `BattleClipPlayer`'s FMV-clip wait
      loops had no timeout -- a stuck `VideoPlayer.Prepare()` (or a playback that never
      reports finished) could hang the entire turn coroutine, and therefore the whole
      battle, recoverable only via Undo. Found chasing a real soft-lock report on
      Kestrel's ultimate; both loops now cap at 5 real seconds
      (`Time.realtimeSinceStartup`, immune to `Time.timeScale`) and log a warning
      instead of hanging forever. (2) The "U" button needed multiple presses to
      register -- its ready-state glow was animating `GUI.backgroundColor` every single
      repaint, a known Unity IMGUI class of bug where a control's unstable paint state
      can eat the click landing on it. Fixed by moving the animation to a separate,
      non-interactive `GUI.DrawTexture` layer drawn behind the button, leaving the
      button's own paint fully static like every other action icon.
    - New tests: `ElementChartTests.cs`, `UltimateGaugeTests.cs`, plus additions to
      `DamageCalculatorTests.cs` (crit, elemental weakness/resist, hit chance) and
      `StatusEffectTests.cs` (Break). `BattleAssetContentTests.cs` gains 2 tests
      confirming every archetype's element and ultimate actually landed on disk.
14. **Map-2 elements/ultimates and the first real stat pass -- M17.** Two of M16's own
    named gaps, closed. Both are content-layer changes: `ContentVersion` bumped to 9, no
    new systems.
    - **Map 2 joins the combat systems.** `BuildCustomEnemy` -- the path M14 added for
      enemies that aren't archetype reskins -- hardcoded `def.element =
      ElementType.Neutral` and never set `ultimateSkill` at all, so M16's whole element
      and ultimate layer silently skipped the only fight in the game with a distinct
      roster. It now takes both as parameters. New `BattleAssetBuilder.Map2EnemyElement`
      assigns **Rotfang = Earth, Deadeye = Lightning, Hexweaver = Fire**, picked as a set
      rather than for flavour: the party is Fire/Wind/Water, so Rotfang is weak to
      Sable's Wind and Hexweaver to Linnet's Water, while Deadeye is deliberately the one
      enemy nothing in the party answers elementally -- it's strong *into* Linnet in both
      directions (Lightning beats Water), reading as "the sniper that specifically
      threatens your healer" and paying for it with the roster's lowest HP and defense.
      Side effect worth naming: **all 5 elements of the cycle are now carried by real
      built content**, where through M16 only Fire/Wind/Water were, making Earth's and
      Lightning's rows in `ElementChart` reachable in play for the first time. Light/Dark
      are still unused by any content (a deliberate no-op 1x either way).
    - **Three enemy ultimates**, built exactly like the archetype ones (mpCost 0,
      gauge-gated by `ResolveAction`, element matching the caster): **Plague Maw**
      (Rotfang/Earth, heavy single hit + a stronger Poison), **Storm Volley**
      (Deadeye/Lightning, full-party AoE + Attack Down, reusing Gale Storm's own barrage
      pattern asset), **Blood Chorus** (Hexweaver/Fire, full-side heal + Regen, mirroring
      Tidal Renewal). These matter more in practice than the player-side ultimates for
      *seeing* the system work: `ChooseAutoSkill` already fires an ultimate for any unit
      that has one, so a plain auto-battle of map 2 now throws enemy ultimates with no
      manual input at all. Storm Volley in particular is the clearest read available on
      "elements are live" -- one cast hits all three party members at three different
      multipliers. Plague Maw is authored at power 2.6 rather than Inferno Blade's 3.2 on
      purpose: Rotfang already has the game's highest attack (26) and biggest crit
      multiplier (1.80), and at 3.2 a crit into a Broken Kestrel exceeds her entire HP
      bar -- an enemy ultimate that deletes a full-health front-liner outright is a coin
      flip, not a difficulty spike.
    - **Authored crit/accuracy/evasion, replacing the random rolls.**
      `BattleWorld.RandomizeTestCombatStats` is **deleted**. M16 shipped it as an
      explicitly temporary hack -- every `StatBlock` on disk had all four combat fields
      at 0, so it sprayed a seeded random roll over each unit at battle boot purely so
      crits and misses would be visible at all. The real numbers now live on the assets,
      as five named profiles in a new `CombatStats` struct (`Data/Core/StatBlock.cs`,
      alongside `StatBlock` itself) applied via `StatBlock.WithCombatStats`: **Bruiser**
      (melee archetype), **Skirmisher** (ranged), **Caster** (support + Hexweaver),
      **Brute** (Rotfang), **Sniper** (Deadeye).
    - **The shape of that pass**, since the numbers themselves are arbitrary in this
      project's usual sense: accuracy sits high (0.92-0.98) and evasion low (0.02-0.10),
      so the single worst matchup in the game -- Rotfang's 0.92 into Deadeye's 0.10 --
      still lands ~82% of the time. A turn-based battle where a fifth of your turns
      evaporate reads as broken rather than tactical, so misses are rare punctuation, not
      a resource cost you plan around. Crit rate is where units actually differ (0.08 on
      a caster up to 0.25 on Deadeye), with crit damage moving inversely -- the brute
      crits least often and hardest. Crit fields survive `StatBlock`'s `*` and
      `RollVariance` untouched (they're percentages, not scaling quantities), so tier and
      level growth don't drift them.
    - **`BattleContentGuard`'s content probe now covers all of this too.** Its
      read-only "are the assets on disk actually wrong right now" check gained the same
      three assertions -- a Neutral element, a null `ultimateSkill`, unauthored
      crit/accuracy. All three are the failure mode this guard was built for: content
      that looks fine (the battle runs, nothing errors) while silently never triggering
      the system it belongs to, exactly like M10's empty `skillMoves`. The version stamp
      alone would have caught M17's own rebuild; this catches a later git revert of
      `Resources/Battle`, or a half-failed build.
    - New tests: 6 in `BattleAssetContentTests.cs` -- map-2 elements, map-2 ultimates,
      elemental distinctness across the map-2 roster, cycle-element coverage across all
      built content, authored combat stats on all 12 combatants, and
      `NoMatchupInTheGame_MissesMoreThanAFifthOfTheTime`, which walks all 132
      attacker/target pairs and asserts `HitChance >= 0.8`. That last one is the whole
      accuracy design encoded as a test: it fails the moment someone retunes one unit's
      evasion up without checking what it does to the least accurate attacker.
15. **Battle skip -- M18.** `N`, or Pause -> "Skip to Next Battle", abandons the current
    fight and boots the next stage with the party, bench and inventory carried over as
    they stand. It reuses `AdvanceToNextMap` -- the exact path the victory banner's "Next
    Battle" button already took -- so a skipped battle and a won one hand off identically;
    the only thing skipping adds is that it can happen mid-turn.
    - **Two real details behind a deceptively small feature.** `AdvanceToNextMap` now
      stops the turn coroutine before firing the event. On the victory path that's a
      no-op (`RunBattle` has already returned), but a mid-battle skip lands inside a live
      turn, and the listener rebuilds the entire scene -- Unity defers `Destroy` to end of
      frame, so without the stop the rest of that turn would still resolve against a world
      being replaced.
    - **The wipe guard is the part that matters.** `CanSkipToNextMap` requires a living
      party member, not for politeness but because `BattleWorld`'s carry-over drops the
      dead: skipping on a wipe boots the next map with *zero* player units, and
      `PlayerDefeated` is `PlayerUnits.Any() && PlayerUnits.All(dead)` -- **false when
      there are none at all**. `IsOver` never becomes true, and `RunBattle` loops enemy
      turns forever with nothing to target. A soft-lock, not a defeat screen. The victory
      path could never reach it (you can't win by dying), which is precisely why it only
      became reachable once skipping existed. A lost battle fails the same check for free,
      which is the right answer -- Restart is the way out of a defeat, not Skip.
    - **No confirmation prompt**, deliberately unlike "Restart Whole Battle". Skipping
      *advances* and keeps everything you're carrying, so the only loss is this battle's
      undo history. The button greys out when unavailable and `N` has no other meaning, so
      there's nothing to explain.
    - New tests: `BattleWorldTests.cs` (new file, 3 tests) pins the carry-over contract
      the skip leans on -- the soft-lock hazard above, wounded survivors keeping their
      exact HP and compacting into columns 0..n, and the last map having nothing to
      advance to. The soft-lock test exists specifically so nobody deletes the guard as a
      redundant-looking null check.
16. **Escape/flee -- M19.** The first of the "modern AAA" meta-systems the project owner
    named as unstarted. Taken next specifically because M18 had just built its machinery:
    "leave this battle mid-fight and hand off cleanly" was already a solved path.
    - **`EscapeCalculator.cs`** (new, pure C#, testable headlessly -- same split
      `DamageCalculator` established: the math here, the `UnityEngine.Random` roll on
      `BattleController`). Chance = a 50% base, shifted by up to +/-25% on how much
      faster your side is than theirs, plus **+15% per failed attempt this battle**,
      clamped to 95%. Only living units count on either side, so a lone fast survivor
      reads as fast rather than being averaged against corpses.
    - **Speed is compared as a ratio, not a difference**, clamped to [0.5x, 2x]. "Twice
      as fast" should mean the same thing at 5-vs-10 as at 50-vs-100, and this slice's
      single-digit speeds (8-13) would barely register under a difference-based formula.
    - **The escalation is the part that matters.** Without it, a slow party facing a fast
      enemy can be locked into a fight it can't win and can't leave, burning turns on a
      roll that never improves. With it, fleeing costs turns rather than being a coin
      flip you can lose forever -- pinned by
      `EnoughFailedAttempts_ReachTheCapEvenAtTheWorstSpeedDisadvantage`.
    - **`BattleOutcome.Escaped`**, a genuine third terminal state rather than a flavour
      of defeat. The party is intact; they just left. The banner reads ESCAPED and offers
      only Restart -- **deliberately no "Next Battle" even when the party is alive and a
      next map exists.** Fleeing is not progress. That's the entire cost of the action,
      and the only thing distinguishing it from M18's skip, which is a dev convenience
      rather than a move in the game.
    - **Live odds on screen.** The action row shows `Flee 50%` under it, climbing
      visibly with each failure (`+1`, `+2`...). It's the one action whose outcome is a
      coin flip, so the number belongs in front of the player before they spend the turn,
      not in the log afterward. `EscapeChanceNow` is the same call `ResolveEscape` rolls
      against, so what's shown is what's used.
    - **`MapDefinition.forbidEscape`** -- the standard "you can't run from a boss" rule,
      and the hook the boss-phase system will want. Defaults to false, so **no
      `ContentVersion` bump was needed** (same reasoning as M15's `SkillDefinition
      .effect`): every existing map still allows escape. No content sets it yet; a test
      pins that, so flipping one becomes a visible decision.
    - **Auto mode never flees, and enemies can't.** `ChooseEscape` is reachable only from
      the manual action row and `ChooseAutoSkill` has no escape branch. Auto mode is a
      "play it out for me" convenience -- a unit that decides to end your run for you is
      not that. Enemies fleeing would need the chance computed from the acting unit's own
      side rather than the player's; deliberately not built, since nothing wants it yet.
    - **Undo doesn't roll back the escalating bonus.** `BattleHistory` snapshots unit
      HP/MP/column and the log; threading a scalar through it for this is a wider change
      than the feature justifies, and the failure mode is benign in the only direction
      that matters -- undoing a failed escape and retrying keeps the accumulated bonus, so
      the player is never trapped, only occasionally let off easy.
    - New tests: `EscapeTests.cs` (new file, 9 tests) covers the formula, the ratio
      property, the escalation reaching the cap, both clamps, dead-unit exclusion and
      empty-side safety. One of them,
      `TheWorstFirstAttemptInTheGame_IsStillBetterThanEvens`, exists to say out loud that
      `MinChance` **doesn't bind today** -- the speed term can only ever subtract 12.5%,
      so the real worst case is 37.5%, and MinChance is a guard against a future retune
      rather than a working floor. Plus one content test that neither shipped map forbids
      escape.
17. **The escape/quit economy and the camp screen -- M20.** M19 shipped a Flee button
    with nothing behind it: leaving a fight cost you nothing but the fight, so "escape"
    and "quit" would have been two words for the same button. The project owner's spec
    closed that -- escaping keeps what the fight earned, quitting forfeits it, and both
    return the party to its entry stats.
    - **`BattleRewards.cs`** (new, pure C#): EXP plus three material kinds, accrued per
      enemy killed. Both death paths credit it -- the killing blow and the poison tick --
      so *how* something died never changes what it pays.
    - **This is knowingly a parallel, throwaway material model, and that's the most
      important thing to know about it.** `Game.Data` already holds the real one:
      `MaterialDefinition` (tier/age/island/rarity/category, sell value, stack size) and
      `DropTable` (guaranteed + chance entries, unlock gating, dungeon level), designed
      from FOUNDATION.md and never wired to battle. Using it properly means authoring
      `Mat_*`/`Drops_*` ScriptableObjects plus a per-enemy table reference -- an asset
      build, which this environment can only do through an interactive Editor session,
      and which would have blocked M20 outright. So drops are **derived from the
      dropper's own stats** instead and the three `MaterialKind` values are stand-ins.
      When the battle/economy boundary gets built, `BattleRewards` should be **replaced**
      by `DropTable` lookups, not extended. The seam is deliberately narrow: `Award()` is
      the only thing that decides what a kill is worth.
    - **Entry-state restore.** `BattleWorld` snapshots every player-side unit's HP/MP at
      construction -- after between-map MP recovery, so it's the state you actually walk
      in with. `RestoreEntryState()` puts it back, and **deliberately un-kills anyone who
      died**: a withdrawal undoes the fight, and a fight you undid didn't kill anyone.
      Status effects and ultimate charge clear for the same reason.
    - **`ChosenAction.Quit` / `BattleOutcome.Quit`.** Both exits funnel through one
      `LeaveBattle(outcome, keepRewards)`, so the entire design is a single bool. The
      action row shows both stakes side by side -- `F: flee 65%, keep haul  |  Q: quit,
      lose 34 EXP, 2 Hide` -- because a choice you can only evaluate after committing
      isn't a choice.
    - **Undo is closed off once you've left** (`HasLeftBattle`). Escaping and quitting
      mutate state `BattleHistory` doesn't model -- restored HP/MP, cleared statuses, a
      banked or binned ledger -- so rewinding into a battle you've walked out of would
      rebuild a half-correct world. Victory and defeat are unaffected.
    - **`CampScreen.cs`** (new): the four things the project owner asked for -- which
      battle is next and who's in it (read from the map's own placements, not
      hardcoded), Rest, a stat overview, and Leave dungeon. Booted by `BattleBootstrap`
      into the same scene rather than a scene of its own, because `BootMap` already tore
      the scene down and rebuilt it, and `Battle.unity` isn't even in the build settings.
      **Rest costs 3 materials** -- the only sink in the game, and the thing that makes
      escaping-with-your-mats buy something concrete.
    - **Leaving a fight does not clear it.** Camp offers the *same* battle again, since
      withdrawing from a fight isn't beating it. "Leave dungeon" has no home to go to yet
      (`Farm.unity` exists and is the only scene in the build settings, but nothing
      connects the two), so it currently restarts the run and says so in the log.
    - **A defeat loses the haul too**, same as quitting -- `Pending` is only banked on a
      victory or a successful escape. Not something the project owner specified; it's the
      reading that makes the escape decision matter under pressure.
    - New tests: `BattleRewardsTests.cs` (9) on the ledger, plus 2 in `BattleWorldTests`
      on entry-state restore covering both the active party and the bench.
18. **`Tools/typecheck.sh` -- M20.** Compiles every C# source in the project **without
    opening or locking Unity**, by driving Unity's own bundled Roslyn compiler at the
    sources with Unity's reference assemblies. This closes a real hole: `-runTests` can't
    run while an Editor has the project open, and M19 and M20 were both written entirely
    blind during a long playtest session because of it. Now the compiler is always
    available. It does **not** run tests -- assertions can still fail, and anything
    needing `Resources/` or a live scene is untouched -- so `-runTests` is still the real
    gate. Verified to actually fail on a deliberate error, not just print "clean". The
    fiddly part is corelibs: Unity's engine DLLs are netstandard2.1 but Unity's NUnit is
    net472 and resolves `[Test]` through mscorlib; referencing either alone fails and
    referencing both collides, so it uses netstandard.dll plus the netfx *shim* mscorlib,
    which type-forwards instead of defining.

## Roster

| Unit | Role | Element | Faction | BA (free) | Skill Moves (mana, tap SM) | Ultimate (tap U, gauge-gated) |
|---|---|---|---|---|---|---|
| **Kestrel** | Melee | Fire | Player | Melee Basic Attack, 1 col | Second Wind (self-heal + Regen, 30MP) / Rally (heal ally, 25MP) / Power Strike (heavy hit + Defense Down, 35MP) | Inferno Blade (heavy hit + Defense Down) |
| **Sable** | Ranged | Wind | Player | Ranged Basic Attack, any col | Volley (3-wide AoE, 25MP) / Snipe (heavy hit + Attack Down, 30MP) / Barrage (full-team AoE + Stun, 45MP) | Gale Storm (full-team AoE + Attack Down) |
| **Linnet** | Support | Water | Player | Support Strike (low power attack) | Heal (20MP) / Mass Heal (AoE heal, 35MP) / Focus Heal (big heal + Regen, 30MP) / Mana Spring (restore ally MP, 15MP) | Tidal Renewal (full-team heal + Regen) |
| **Husk** | Melee | Fire | Enemy (map 1) | same as Kestrel's archetype | same as Kestrel's archetype | same as Kestrel's archetype |
| **Warden** | Ranged | Wind | Enemy (map 1) | same as Sable's archetype | same as Sable's archetype | same as Sable's archetype |
| **Stinger** | Support | Water | Enemy (map 1) | same as Linnet's archetype | same as Linnet's archetype | same as Linnet's archetype |
| **Rotfang** | Melee | Neutral | Enemy (map 2 only) | Bite, 1 col | Venom Strike (heavy hit + Poison, 25MP) | none authored |
| **Deadeye** | Ranged | Neutral | Enemy (map 2 only) | Snipe Shot, any col | Crippling Shot (heavy hit + Attack Down, 25MP) | none authored |
| **Hexweaver** | Support | Neutral | Enemy (map 2 only) | Hex Bolt (low power attack) | Heal (20MP) / Weaken (Defense Down, 25MP) | none authored |

**Bench reserves (player)** — sub in for any active player unit via the manual-mode Sub
action, same archetype stats/BA/Skill Moves as their active counterpart, distinct art:
**Thorne** (Melee, reskin of Kestrel's archetype), **Reed** (Ranged, reskin of Sable's),
**Vesper** (Support, reskin of Linnet's).

**Rotfang/Deadeye/Hexweaver (M14) reuse Thorne/Reed/Vesper's art+clips directly** --
`BattleAssetBuilder.BuildCustomEnemy` borrows the Sprite/ClipSet references from the
already-built bench `CharacterDefinition`s rather than expecting new generated PNGs,
per the project owner's "worry about assets later" direction. Their stats/kits are
hand-authored, not archetype reskins like Husk/Warden/Stinger -- each is a genuine step
up (bulkier melee, faster ranged glass cannon, bulkier caster) and each exists
specifically to give the M13 status-effect system something an AI actually uses (see
"What changed" item 11) -- auto mode previously never reached for a non-heal Skill
Move at all.

All 10 non-BA skills, plus the relocated Heal and M12's Mana Spring, live in
`CharacterDefinition.skillMoves`; `standardSkill` is always BA. 5 of the 10 also carry
a status effect on top of their original heal/damage (M13, see item 10 above) --
heal/damage primitives *plus* status effects now, not primitives-only.

**MP economy (M12+M13), not a tuned one.** Sources: a small trickle (+4) from a unit's
own BA, a smaller passive trickle (+3) every turn regardless of action, 25%-50% of
missing MP restored per unit between maps 1 and 2 (HP still doesn't recover there --
see `BattleWorld`'s carry-over doc), Mana Spring as an active targeted top-up, and now
an MP potion (Item action). Still no potion/item *economy* (no shop/farm system to
source real stock, drop rates, or prices from) -- see "Known gaps."

**Battle potions (M13).** 3 fixed slots -- Hp/Mp/Multi -- each holding a ranked
(F-SSS) `PotionDefinition` stacked up to 99. A fresh battle seeds a placeholder 5 of
each C-rank potion (`BattleWorld.SeedPlaceholderInventory`); carries over between maps
1 and 2. Tap **I** in manual mode, pick a slot, pick a target (any living ally). Free,
costs the turn.

## Milestones — all done

| # | Milestone | Tag |
|---|---|---|
| M0 | Branch, `.gitignore`, folder scaffold, docs stub | `v0.4.0-m0-scaffold` |
| M1 | ComfyUI pipeline (workflows, manifest, generate.py, postprocess.py) | `v0.4.0-m1-comfyui-pipeline` |
| M2 | Generated assets + import automation + ScriptableObject build | `v0.4.0-m2-assets` |
| M3 | Side-view battle logic, scene, EditMode tests | `v0.4.0-m3-battle-logic` |
| M4 | Curated HD roster art, hit FX, manual/turn-based mode | `v0.4.0-m4-roster-and-manual-mode` |
| M5 | Android build (APK builds; on-device unverified) | *(not yet tagged — see below)* |
| M6 | Turn log (`BattleLog`, scrollable review panel, `L` to open) | *(not yet tagged)* |
| M7 | Three-panel layout: docked ally/enemy rosters + centre-stage cinematic action | *(not yet tagged)* |
| M8 | Pause, settings, multi-step undo/redo, dock-spacing bugfix | *(not yet tagged)* |
| M9 | Frontline succession, bench sub-in/out, reposition, healer attacks, 2-map sequence | *(not yet tagged)* |
| M10 | Camera bug fix, Skill Move system (9 skills), compact action UI, melee movement | *(not yet tagged)* |
| M11 | Skill Moves built into the assets, content guard + asset-level tests, SM tap, duplicate-label fix | *(not yet tagged)* |
| M12 | MP economy (BA trickle, between-map recovery, Mana Spring), FMV chroma-key components, code-based Skill Move tests | *(not yet tagged)* |
| M13 | Battle potions (3 slots, F-SSS rank), standard JRPG status effects, per-turn MP regen, Android-first platform priority | *(not yet tagged)* |
| M14 | `impactFrames` re-fix (authored in C#), map-2-only enemy roster (Rotfang/Deadeye/Hexweaver), offensive-Skill-Move AI | *(not yet tagged)* |
| M15 | Centre-stage-only movement (crossover attempts reverted after live testing), body animation/skill effect decoupling (`SkillEffect`) | *(not yet tagged)* |
| M16 | Elements/weakness chart, crit/accuracy, Break status, ultimate gauge + one skill per archetype, turn-order strip, unit stats HUD, clip-hang + IMGUI click-eating fixes | *(not yet tagged)* |
| M17 | Map-2 enemy elements + ultimates (the `BuildCustomEnemy` gap), authored crit/accuracy/evasion profiles replacing the temporary random rolls | *(not yet tagged)* |
| M18 | Battle skip (`N` / Pause menu) -- forfeit the current fight, carry the party to the next stage | *(not yet tagged)* |
| M19 | Escape/flee -- speed-based chance with per-failure escalation, `Escaped` outcome, `forbidEscape` map hook | *(not yet tagged)* |
| M20 | Escape/quit economy (EXP + materials, entry-stat restore), camp screen, `Tools/typecheck.sh` | *(not yet tagged)* |

Each of M0-M2's commits has a `NOTES.md` snapshot under
`AI.Game Commits/battle-slice/<milestone>/` and a zip under `releases/zips/`. That
per-milestone full-tree snapshot step was dropped from M3 onward (M4's status note
already flagged it as skipped for time) — it duplicated the whole `Unity/` tree per
milestone for state git history already gives you for free. M6/M7 follow the M3-M5
precedent: commit + docs update, no snapshot/zip.

## Controls

`T` toggle auto/manual mode · `L` open/close the turn log · `Esc` pause ·
`Ctrl+Z`/`Ctrl+Y` undo/redo last turn · `R` restart after the battle ends · `N` skip
this battle and move to the next stage (M18 -- also in the pause menu; unavailable on
the last map or after a wipe) · `?` keybind legend. Manual mode: a player unit's turn opens a small 5-icon menu under their feet,
all tapped -- **BA** (free basic attack), **SM** (opens the mana-cost Skill Move
list; tap again to close it), **U** (ultimate, needs a full gauge), **R** (Reposition),
**S** (Sub), **I** (opens the potion list -- Hp/Mp/Multi, tap again to close it), **F**
(Flee, M19 -- rolls against the live odds shown under the row, and costs the turn either
way), **Q** (Quit, M20 -- leave the fight instantly, forfeiting everything it earned)
-- then click a highlighted target on the field (BA/SM/Reposition/Item) or pick from
the popup (SM's list, Sub's bench, Item's potion slots). U and F resolve immediately with
no target pick.

## How to run it

- **In Editor:** open `Unity/Assets/Scenes/Battle.unity`, press Play. If the scene or
  `Resources/Battle/*` assets don't exist yet, run
  `AI.Game → Battle → Create Battle Scene` first (also rebuilds the data assets).
- **Windows standalone:** `AI.Game → Battle → Build Windows Standalone (dev)` →
  outputs straight to `S:\AI\Game\play\windows\AI.Game-Battle.exe` (**not** into this
  repo — `play\` is the ready-to-launch sibling of `test\AI.Game\`, deliberately kept
  separate so a from-source build and something you'd hand someone to just play never
  live in the same tree; see `BuildBattleStandalone.cs`'s doc comment).
- **Android APK:** `AI.Game → Battle → Build Android APK` → outputs to
  `S:\AI\Game\play\android\AI.Game-Battle-v0.4.0-debug.apk` (24.8MB, IL2CPP, ARM64,
  min API 26, package `com.aigame.aigame`). Built successfully this session but
  **never installed on a device** — no adb here. Needs `adb install -r` + a manual
  play-through on whatever machine/session has Android platform tools.
- **Headless verification** (what this session actually used, since driving the
  Editor GUI directly wasn't available): with Unity **closed** (batchmode can't run
  alongside an open Editor on the same project — same lockfile),
  ```
  "S:\AI\Game Engine\Unity\UnityEditors\Editor\6000.5.7f1\Editor\Unity.exe" -batchmode -quit -nographics -projectPath "S:\AI\Game\test\AI.Game\Unity" -executeMethod Game.EditorTools.BuildBattleStandalone.Build -logFile "S:\AI\Game\test\AI.Game\Unity\batchmode-build.log"
  ```
  and for tests: same but `-runTests -testPlatform EditMode -testResults <path>.xml`
  instead of `-executeMethod` -- **drop `-quit` for the test invocation**, confirmed
  this session: with `-quit` present Unity finishes the asset refresh and exits
  before the test runner ever starts (no `test-results.xml` is written, exit code 0,
  looks like success but nothing ran); the test runner quits the process itself once
  done. To actually *see* the standalone build running, launch the exe then
  screenshot its window via `PrintWindow` (Win32 API through PowerShell) rather than
  a plain screen capture — the exe isn't a "known installed app" so the usual
  computer-use tools can't target it by name.

## Known gaps

- **`ProjectSettings.asset` still shows the pre-rename "AI.Game Farm"/`com.aigame.farm`
  values.** The fix (all three of `FarmAutoSetup.cs`/`FarmBatchSetup.cs`/
  `BuildAndroid.cs` now agree on "Adams Haven"/`com.adamshaven.game`) is committed, but
  `FarmAutoSetup`'s `[InitializeOnLoad]` guard only re-applies once per Editor session
  (`SessionState`-gated, unlike `BattleContentGuard`'s content check, which re-fires on
  every script recompile) -- resolves automatically the next time the Editor restarts.
- **Resolved same session: M17's content is built and committed (content v9).** It
  shipped code-only at first -- the rebuild can't be done headlessly (it means
  `AssetDatabase.CreateAsset`/`SaveAssets()` after a script change, the documented
  corruption path below, and M17 adds 3 brand-new `SkillDefinition` assets, the
  highest-risk version of it). The project owner then opened the Editor mid-session and
  `BattleContentGuard` did it automatically off the v8->v9 stamp mismatch, no menu click.
  Verified on disk before committing: every character carries a non-Neutral element, an
  `ultimateSkill` and authored crit/accuracy/evasion; the 3 new ultimate assets exist
  with sane GUIDs; **zero `m_Script: {fileID: 0}` corruption.** That's another point for
  the "interactive rebuilds are safe, headless ones aren't" hypothesis, and a fairly
  strong one -- this pass created new ScriptableObjects after a script change, the exact
  shape that corrupted everything in M9.
- **M18 is verified live; M19 and M20 compile but have never been run.** M18's skip was
  picked up by the project owner's own Editor session (no compile errors, and the log
  shows map 1 booting then map 2 booting seconds later with no exceptions -- the skip
  working). M19 and M20 landed while that Editor was open, so `-batchmode -runTests`
  could not run at all (shared lockfile) and the queued attempt timed out after 45
  minutes. They are **type-checked clean** via `Tools/typecheck.sh` (item 18 above) --
  which is genuinely new coverage, not a fudge: it catches every compile error across
  Game.Data/Game.Battle/Game.Tests. But **type-checking is not testing.** ~113 tests
  including 11 new ones have never executed, and nothing in M19 or M20 has been played.
  One `-runTests` pass once Unity is closed is the outstanding gate.
- **M20's materials are a parallel model to the one the project already has.** See item
  17 -- `MaterialDefinition`/`DropTable` exist in `Game.Data`, fully designed, unwired.
  `BattleRewards` should be *replaced* by them rather than grown, and `Award()` is the
  single seam where that swap happens. Flagged loudly because a placeholder economy that
  quietly becomes the real one is how projects end up with two of everything.
- **Camp's "Leave dungeon" has nowhere to go.** `Farm.unity` exists and is the only
  scene in the build settings, but nothing connects the battle slice to it, so leaving
  restarts the run and logs why. That connection is the farm/battle boundary the
  README's last "Natural next steps" item is about.
- **M17's numbers are a first pass by reasoning, not by play.** The crit/accuracy/evasion
  profiles (`CombatStats`) and the 3 enemy ultimates were tuned against the stat tables
  and the damage formula, not against a real battle -- `Plague Maw`'s power was already
  cut from 3.2 to 2.6 on that basis alone (a crit into a Broken Kestrel exceeded her
  whole HP bar). Worth watching in the first live map-2 fight specifically: whether
  Blood Chorus makes Hexweaver's side unkillable (a ~48 HP heal on all three, roughly
  half a bar each), and whether Storm Volley's 1.5x into Linnet plus Deadeye's 0.25 crit
  rate is too much burst on the healer.
- **M16's two live-found bugs (the FMV-clip hang, the IMGUI click-eating button) were
  fixed by defensive/structural changes, not a fully confirmed root cause for the
  first one.** The clip-player timeout (5 real seconds) guarantees the turn always
  proceeds either way, but *why* `VideoPlayer.Prepare()`/playback would ever actually
  hang was never conclusively identified -- worth watching for recurrence, and if it
  never recurs, treat the timeout as the permanent fix rather than a stopgap.
- **Resolved this session, confirmed by the project owner's own interactive Editor
  use: manual mode's click-to-target and the action-menu buttons work fine with a real
  mouse.** The long-standing "never interactively click-tested" gap below was always
  specifically about *this project's automation tooling* not being able to synthesize
  clicks against the standalone exe -- it was never evidence that clicking wouldn't work
  for an actual person at the keyboard. The project owner played manual mode directly
  (screenshots this session show `Kestrel's turn -- choose an action` / `Sable's turn --
  choose an action` mid-interaction, catching and reporting three real bugs along the
  way -- the camera crash, the action-menu position, and the MP-bar rendering bug, all
  itemized above). **Net: prefer testing via the project owner's own interactive Editor
  session over automation for anything UI-shaped going forward** -- it's strictly more
  capable than anything this environment's automation can reach, and already caught
  bugs automation never would have (the camera bug especially -- batchmode never got far
  enough to hit it, since it fails earlier on asset loading; see the item below).
- **Headless `-batchmode -executeMethod` calls that touch `AssetDatabase
  .SaveAssets()`/`CreateAsset` (i.e. `BattleAssetBuilder.Build()`, and therefore
  `BattleSceneBuilder.CreateBattleScene()` and `BuildBattleStandalone.Build()` which call
  it) can corrupt every touched ScriptableObject's `m_Script` reference into
  `{fileID: 0}` — found and fought at length verifying M9.** Confirmed via a controlled
  test: the pristine pre-M9 code+assets built 0-warning clean through this exact
  toolchain; only after adding M9's new C# (new `Formation.cs` class, a new field on
  `CharacterDefinition`) and rebuilding ScriptableObject assets in the same headless
  session did `m_Script` corruption start, and it was **not** limited to newly-created
  assets — a second pass corrupted long-untouched files like `Char_player_melee.asset`
  and `Tier_Standard.asset` too, and giving brand-new assets fresh GUIDs didn't reliably
  fix them either (a later pass still flagged "Script attached to ... is missing" for
  literally every Resources/Battle asset, all 21 of them, in one run). Retrying more
  batchmode passes did not self-heal it. **Workaround used this session:** temporarily
  comment out the `BattleAssetBuilder.Build();` line inside `CreateBattleScene()` before
  running `BuildBattleStandalone.Build()` for verification (skips re-touching the
  ScriptableObjects), and hand-patch any asset that already got corrupted by restoring
  `m_Script: {fileID: <11500000 or 21300000>, guid: <the .cs file's own .meta guid>,
  type: 3}` (compare against a known-good sibling asset of the same C# type for the
  exact `m_EditorClassIdentifier` format too — `git diff` on a corrupted file shows
  exactly what broke). **This is very likely specific to a from-scratch headless
  compile-then-save in the same invocation** — every M2-M8 asset that predates this
  session was built via normal interactive Editor use and was never seen corrupting
  itself in a batchmode run that *didn't* also just recompile changed scripts. **Next
  session picking this up: open the project in the real Unity Editor GUI once (double-
  click into it, let it finish importing, maybe just press Play once) before trusting
  any further headless `-executeMethod` asset-builder runs** — this should "bless" the
  new scripts' MonoScript GUID registration the way batchmode apparently can't, after
  which headless verification should go back to being reliable like it was for M3-M8.
  **Update: the project owner did exactly that later this session** -- opened the
  Editor interactively, and M9+M10 both ran and were played there without ever hitting
  this corruption (only the pre-existing camera bug above, unrelated). That's real
  evidence for the "interactive-first-use blesses it" hypothesis, though still not
  fully proven (no controlled A/B was re-run afterward). M9's C# is also unit-tested
  (22/22 EditMode tests, including `FormationTests.cs`) and compiles clean end-to-end.
  **Bottom line: don't use headless `-executeMethod` asset-builder runs after a script
  change; open the Editor normally instead** -- confirmed to work, and it's what the
  project owner will be doing anyway to playtest.
- **This repo moved from `S:\AI\Game\AI.Game` to `S:\AI\Game\test\AI.Game`** during a
  folder cleanup (same session as M8). `S:\AI\Main Game\AI.Game` — a *different*
  project, Asset Forge's own Unity import/validation sandbox, confusingly also named
  `AI.Game` — moved to `S:\AI\Game\test\AssetForge-Sandbox` at the same time and
  Asset Forge's `config.toml`/`config.py` were updated to match. `BuildBattleStandalone.cs`
  and `BuildAndroid.cs` now output straight to `S:\AI\Game\play\windows\` /
  `S:\AI\Game\play\android\` instead of into this repo (see "How to run it"). If any
  tooling/scripts/notes still reference the old `S:\AI\Game\AI.Game` path, they're
  stale — this file and the wiki are current as of the move.
- **No `adb` on this machine — now the single biggest blocker on the project's stated
  priority order.** M13 established explicitly: the game must be fully playable
  start-to-finish on Android first, Windows second, iPhone third (not started at all).
  Everything built so far (IMGUI HUD, tap-only gestures since M11's SM/M13's Item
  popups, no hold/right-click/hover-dependent interaction anywhere) should translate to
  touch reasonably well by design, but "should translate" is exactly the kind of claim
  this project has learned not to trust without someone actually touching a real
  device — M10's camera bug and the M10/M11 Skill Move gap both hid behind confident-
  looking code that had never actually been run the way a player would run it. Needs a
  different machine/session with Android platform tools, or the project owner
  installing them here.
- **GitHub wiki is unblocked** (was blocked through M9 — GitHub only provisions a
  repo's wiki git backend after a page is saved once through the web UI, with no
  API/git-push way around it; the project owner has since created that first page). The
  4 pages (Home, Battle-System, Art-Pipeline, Roadmap) live at
  `S:\AI\Game\AI.Game.wiki\` — a **separate git clone**, not part of the main repo, so
  it needs its own commit + push alongside the main one:
  `cd "S:\AI\Game\AI.Game.wiki" && git add -A && git commit && git push`.
- **Automation still can't drive the standalone exe's window directly** (see the two
  items above for why, and why it no longer matters much: the project owner's own
  interactive Editor session covers this better than automation ever could). Synthetic
  input (`SendKeys`, hardware-level `SendInput` with an `AttachThreadInput` focus-steal)
  doesn't reliably reach the game window -- `GetForegroundWindow` confirmed the click's
  target window never actually changed, so clicks landed on the desktop, not the game.
  Auto mode was thoroughly verified this way in earlier sessions regardless (screenshots
  across many real turns), and the M7 dock-overlap bug was *found* this way.
- **M7's first-pass dock spacing overlapped units** (`DockColumnSpacing` cut to 1.9 to
  make room for the new centre stage, well under the 3.6 the original single-line
  layout's own comment says is required) — Husk and Stinger visibly collided on the
  enemy dock. Fixed in M8 by restoring 3.6 and widening the camera instead of
  shrinking spacing; worth remembering if the layout constants get touched again.
- **FMV clip playback is not wired in.** `Tools/ComfyUI/` generated 3 real h264 clips
  in M1/M2 and `ClipEntry`/`ClipSet` exist on `CharacterDefinition`, but
  `BattleVisuals` only ever rendered static sprites — no `VideoPlayer`, no chroma-key
  shader. This is real, uncompleted scope from the original brief's three-layer
  renderer design, not something that was decided against.
- **No fixed master palette** for the (now largely superseded) `Tools/ComfyUI/`
  pixel-sprite pipeline — `postprocess.quantize_palette()` uses adaptive median-cut as
  a placeholder. Low priority now that the roster uses curated HD art instead.
- **M4 has no snapshot/zip** under `AI.Game Commits/battle-slice/` (see Milestones
  table) — the commit/tag/push happened, just not the extra per-section copy step.
- **Resolved in M11: the 9-skill Skill Move content is now actually built into the
  assets** (verified on disk -- all 12 skill assets and 7 patterns present, all 9
  characters carrying `maxMp: 100` and 3 `skillMoves` references, **zero
  `m_Script: {fileID: 0}` corruption**), and `BattleContentGuard` re-runs the builder
  automatically whenever it goes stale, so "authored in C#, never written to disk" can't
  silently recur. That rebuild also ran through an interactive Editor session and came
  out clean, which is further evidence for the "interactive is safe, headless
  `-executeMethod` isn't" hypothesis below.
- **Resolved in M12: a first-pass MP economy exists** (BA trickle, between-map partial
  recovery, Mana Spring) -- see item 9 above. Still open: no *per-turn* passive regen
  (a unit that dumps its whole pool mid-battle is genuinely tapped out until the next
  map), and no potion/item system -- the project owner named "recover through ... a
  potion-like item" as a goal, but there's no inventory system in this slice at all to
  hang it on. `BattleUnit.RestoreMp(int)` is the primitive a future item system should
  call; nothing else about it exists yet. `RestoreMpFull()` is similarly just a hook --
  no farm/town "sleep" system calls it, since no persistence layer connects battle party
  state to the farm scene.
- **Resolved (for real this time): FMV clip `impactFrames`.** First hand-fixed during
  M12 (see CHANGELOG for that writeup) -- then **regressed back to the exact same
  corrupted values, byte-for-byte identical, the very next time the project owner
  rebuilt in the interactive Editor for M13.** That's the key finding: a hand-edit of
  the built asset alone never sticks, because `BattleAssetBuilder.BuildClipSet`
  unconditionally overwrites `impactFrames` from `clipAsset.impact_frames` on every
  `Build()` run, and that field deserializes to nonsense *deterministically* -- the
  same wrong numbers every time, not random garbage, which rules out disk/session
  corruption and points at something in the manifest-JSON parsing path itself
  (`manifest.export.json`'s own data is clean and verified by hand: `[18]`, `[22]`,
  `[16,24]`). Root cause still not confirmed (a `JsonUtility` array-parsing edge case
  is suspected, never proven). **Real fix this time**: stopped trusting that field
  entirely. `BattleAssetBuilder.KnownGoodImpactFrames` (new) authors the 3 known-correct
  values directly in C#, the same way every other hand-tuned number in this builder
  already is, bypassing the unreliable manifest field rather than trying to fix its
  parsing. `ContentVersion` bumped to 6. **Not yet verified headlessly** -- the
  project owner's Editor was open when this landed; `ClipMetadataTests.cs` needs one
  more `-runTests` pass once it's closed to confirm the code fix actually produces
  correct output on a fresh `Build()`, not just that the hand-patched files
  (already reapplied) look right by inspection.
- **M14's map-2 roster (Rotfang/Deadeye/Hexweaver) and the new `OffensiveSkillMoveChance`
  AI branch are code-complete but not yet confirmed in a live battle.** Explicitly
  deferred by the project owner: "we can't really check the status effect step but we
  can check that at another time after we get more enemy types" -- these enemies are
  exactly that "more enemy types." Next interactive session: fight map 2, confirm
  Rotfang actually Poisons someone, Deadeye actually Attack-Downs someone, Hexweaver
  actually Weakens someone and heals its own side, and that Kestrel/Sable/etc.
  occasionally reach for their offensive Skill Moves in auto mode too.

## Natural next steps, roughly in priority order

1. **On-device Android verification** — see "Known gaps." Top of the list, not a
   someday item: the project owner's explicit priority is Android first, Windows
   second, iPhone third. Blocked on `adb` access.
2. **Play map 2.** The blocker on this is gone -- M17's content is built (v9) and
   committed, so Rotfang/Deadeye/Hexweaver now have their elements and ultimates live.
   **M18's `N` skip exists precisely to make this fast**: start a battle, press `N`, and
   you're on map 2 with a full-HP party without fighting map 1 first. Then confirm the
   whole stack against a second roster at once -- Poison/Attack Down/Defense Down/Stun
   visibly doing something, auto mode reaching for offensive Skill Moves, Break/crit
   landing, and M17's own additions: Sable hitting Rotfang for 1.5x, Deadeye's Storm
   Volley hitting all three party members at three different multipliers, and Hexweaver's
   Blood Chorus healing its own side. Auto mode alone shows all of this --
   `ChooseAutoSkill` fires an ultimate the instant a gauge fills, on either faction -- so
   this needs a play-through, not manual input.
3. **Retune M17's first-pass numbers against that fight.** See "Known gaps" for the two
   specific things to watch (Blood Chorus sustain, Storm Volley burst on the healer).
   The profiles are all in one table (`CombatStats` in `Data/Core/StatBlock.cs`) and the
   ultimates are 3 adjacent lines in `BuildMap2EnemySkills`, so a retune is a one-file
   edit plus a rebuild.
4. **Screenshot the manual-mode-only HUD for the wiki.** `Combat-Systems.md` has real
   in-game screenshots for everything auto mode surfaces (roster bars, turn-order strip)
   but nothing for the manual-only pieces -- the colour-coded BA/SM/U/R/S/I row and the
   Unit Stats card -- because synthetic clicks can't reach the standalone build from this
   environment. Two screenshots from a real play session would fill the last gap in that
   page.
5. **Author real `SkillEffect` assets (M15).** Every Skill Move currently falls back to
   the map's generic impact FX, since no skill-specific effect exists yet -- the hook
   (`SkillDefinition.effect`) is ready the moment art/effect sheets are available, no
   further code changes needed to attach one.
6. **A real potion/item economy** (drop rates, a shop, farm integration) -- M13 shipped
   the mechanic with a hardcoded placeholder stock (5 of each C-rank potion every fresh
   battle) because no economy system exists yet to source real starting inventory from.
7. Choose/build final FMV clip assets (Unity Asset Store base or new ComfyUI
   generations) -- explicitly deferred by the project owner until the foundation above
   is laid out further. The components are ready (M12) whenever this comes back up.
8. Frame-accurate impact-FX sync using the now-correct `impactFrames` data (M12/M14's
   fix) -- currently `PlayImpactBeat` just uses the clip's own runtime as a flat hold,
   not synced to the clip's actual hit frame.
9. Consider extending the status-effect system if content wants to go beyond the 7
   types already built (e.g. a Taunt/aggro mechanic, shields, cleanse effects) -- the
   core tick/apply/multiplier plumbing (M13) is general enough to add types to without
   restructuring it. `Poison` itself is still unused outside of Rotfang.
10. More enemy variety beyond map 2's 3, if the project owner wants it -- the
    `BuildCustomEnemy` pattern (M14) makes a new enemy cheap to add as long as it can
    borrow art from an existing `CharacterDefinition` (no new art generation needed).
    Since M17 that path also takes an element and an ultimate as required parameters, so
    a new enemy can't silently ship outside the combat systems the way these three
    originally did.
11. **Bigger meta-systems from the project owner's own "modern AAA" list, still
    unstarted**: boss phase/pattern triggers, an equipment/loadout system (a stats
    screen exists now via M16's Unit Stats panel, but nothing equips gear yet), and
    save/persistence. Escape/flee came off this list in M19. Boss phases are the natural
    next one -- `MapDefinition.forbidEscape` is already the first hook it needs, and it's
    the only remaining item that lives entirely inside the battle scene; equipment and
    persistence both want the farm/battle boundary settled first.
12. Beyond the vertical slice: the roster is currently 6 fixed archetypes plus 3 bench
    reserves. FOUNDATION.md's broader systems (tier/fusion, gacha, farm/town economy)
    are designed but not connected to this battle system yet — that's the actual "rest
    of the game," this slice only proves the battle screen works.
