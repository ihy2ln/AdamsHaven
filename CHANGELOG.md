# Changelog — Battle Vertical Slice

Reverse-chronological history of the `feature/battle-slice` branch. For *current*
state (what's done, what's known-broken, what's next) see `PROJECT-README.md` instead
— this file is a record of what shipped when, not a living status doc.

## Unreleased — M9-M17 (2026-08-23)

Not yet tagged or cut as a release. Depth on the battle slice: a real roster bench,
a per-unit skill system, the tooling to stop authored content from silently failing
to ship, a first-pass MP economy plus FMV playback components, battle potions,
standard JRPG status effects, an explicit Android-first platform priority, a distinct
map-2 enemy roster with the AI to actually use its kit, and a full "modern AAA
turn-based" combat layer on top.

*(M15 and M16 landed without a CHANGELOG entry at the time — added below alongside
M17 so this file stops at the same milestone `PROJECT-README.md` does. Their full
writeups live in that file's "What changed from the original design", items 12-14.)*

- **M17 — Map-2 elements/ultimates, the first authored stat pass.** Closed two gaps
  M16 named itself. Map 2's Rotfang/Deadeye/Hexweaver are built by `BuildCustomEnemy`,
  a code path that hardcoded `ElementType.Neutral` and never set `ultimateSkill`, so
  M16's whole element/ultimate layer silently skipped the only fight with a distinct
  roster. They now carry Earth/Lightning/Fire -- picked as a set against the party's
  Fire/Wind/Water rather than for flavour -- plus one ultimate each (Plague Maw, Storm
  Volley, Blood Chorus), built to the same rules as the archetype ultimates. Side
  effect: all 5 elements of the weakness cycle are now on real built content, where
  through M16 only Fire/Wind/Water were. Separately, `BattleWorld
  .RandomizeTestCombatStats` -- M16's explicitly temporary "spray a random roll on
  every unit at battle boot so crits and misses are visible at all" hack -- is deleted,
  replaced by five authored `CombatStats` profiles (Bruiser/Skirmisher/Caster/Brute/
  Sniper) applied to the assets via `StatBlock.WithCombatStats`. Accuracy stays high
  and evasion low by design, so the worst matchup in the game still lands ~82% of the
  time; a new test walks all 132 attacker/target pairs to hold that floor. 6 new
  EditMode tests (89 total). `ContentVersion` bumped to 9 -- the assets rebuild
  automatically on the next interactive Editor open, and until then 5 asset-content
  tests fail by design, naming exactly what's missing.

- **M16 — Elements, crit/accuracy, Break, ultimate gauge, combat HUD.** A full
  "modern AAA turn-based" mechanics pass, built as a testable math/data layer first
  (`ElementChart.cs`, `DamageCalculator` crit/hit-chance) then the HUD to see it.
  7 elements plus Neutral on a 5-way weakness cycle with a Light/Dark rivalry; crit
  rate/damage and accuracy/evasion as new `StatBlock` fields, each with a fallback
  chosen so no pre-M16 content broke; a Break stagger status (take 30% of max HP
  since your last turn and lose your next one, taking bonus damage while down); and
  an ultimate gauge with one authored ultimate per archetype (Inferno Blade/Gale
  Storm/Tidal Renewal), gauge-gated rather than MP-gated. HUD: a turn-order strip, a
  break-progress bar, sprite darkening while broken, a colour-coded "U" action
  button, and a Unit Stats inspector. Two real bugs found in live play and fixed: an
  unbounded FMV-clip wait that could soft-lock the entire battle (now capped at 5
  real seconds), and a Unity IMGUI bug where the ultimate button's own animated
  `GUI.backgroundColor` was eating its clicks (fixed by moving the animation to a
  non-interactive layer behind it).

- **M15 — Centre-stage-only movement, body/skill-effect decoupling.** Reworked how a
  turn's action reads after two rounds of the project owner watching it play and
  redirecting: every action now moves only the acting unit to a shared centre-stage
  mark and the target never moves, replacing both M10's melee walk-up-to-target and an
  intermediate "attacker crosses onto the target's side" attempt that didn't survive
  first contact with real play. Separately, a character's body animation (its FMV clip)
  and a skill's visual identity are now fully decoupled -- every Skill Move plays the
  caster's own swing/cast clip (previously basic-attack-only), while a new optional
  `SkillEffect` asset carries the skill-specific impact visual, ahead of the project
  owner's planned "skill orb" system. No `ContentVersion` bump: `SkillDefinition
  .effect` is a new optional field defaulting to null on every existing asset.

- **M14 — `impactFrames` re-fix, map-2 enemy roster, offensive-Skill-Move AI.** The
  M12 `impactFrames` hand-fix regressed to the exact same corrupted values on the very
  next interactive rebuild (M13) -- confirming `BuildClipSet` overwrites it from the
  manifest's `impact_frames` field on every `Build()` run, and that field
  deserializes to the same wrong numbers deterministically (root cause still
  unconfirmed). Fixed for real by no longer trusting that field: the 3 known-correct
  values are now authored directly in C# (`BattleAssetBuilder.KnownGoodImpactFrames`).
  Separately, per the project owner's request for "more enemy types" (needed to give
  M13's status-effect system something to prove itself against): map 2 now fields its
  own roster -- Rotfang (Poison), Deadeye (Attack Down), Hexweaver (Defense Down, plus
  Heal for its own side) -- reusing Thorne/Reed/Vesper's already-imported art+clips
  rather than any new generated assets. `BattleAssetBuilder.BuildMap` no longer
  hardcodes which characters go in a map; it takes an explicit enemy-placement list
  now. Found and fixed a real gap while wiring this: `BattleController.ChooseAutoSkill`
  never reached for anything but BA or the one heal carve-out, so *any* offensive
  Skill Move -- including M13's status-inflicting retrofits -- was unreachable by any
  AI-driven turn (every enemy, and every auto-mode player unit). Added a 35% per-turn
  chance to reach for an affordable, valid offensive Skill Move instead, symmetric
  across both factions. 3 new EditMode tests. Status-effect verification explicitly
  deferred by the project owner until these new enemies are actually played against.

- **M13 — Battle potions, status effects, per-turn MP regen, platform priority.**
  Direction from the project owner across five areas. **Potions:** a new
  `BattleInventory` with exactly 3 fixed slots (Hp/Mp/Multi), each holding an F-SSS
  ranked `PotionDefinition` (reuses the existing character `Tier` enum for rank)
  stacked up to 99; `PotionCalculator.Potency(Tier)` is a flat restore-amount table,
  arbitrary numbers per the project owner's direction; a new manual-mode "I" icon
  opens a popup to pick a slot, then a target, same flow as every other targeted
  action; `BattleWorld` seeds a placeholder 5-of-each-C-rank stock on a fresh battle
  (no shop/economy exists yet to source real stock from) and carries it across the
  2-map sequence; `BattleHistory` gained an optional inventory parameter so
  Undo/Redo can't be exploited into a free potion duplicate. **Status effects:** a
  real, standard-JRPG-shaped system -- `StatusEffectType` (AttackUp/Down,
  DefenseUp/Down, Poison, Regen, Stun), ticking once per the affected unit's own turn,
  buffs/debuffs folded into `DamageCalculator` via new `BattleUnit.AttackMultiplier`/
  `DefenseMultiplier`, Stun skipping that unit's action entirely. Retrofit onto 5
  existing Skill Moves additively (their original heal/damage numbers untouched):
  Second Wind and Focus Heal also grant Regen, Power Strike also applies Defense Down,
  Snipe also applies Attack Down, Barrage (a full-team AoE) also applies Stun to
  everyone it hits. **MP:** a new passive per-turn trickle (+3, on top of M12's
  BA-specific +4) -- small per tick, meaningful over a long battle, per the project
  owner's framing. **FMV:** explicitly deferred until more of the foundation is laid
  out. **Platform priority:** stated explicitly for the first time -- Android fully
  playable first, Windows second, iPhone third (not started) -- which reprioritizes
  on-device Android verification (still blocked on `adb` access) to the top of the
  project's open-items list. 15 new EditMode tests (`StatusEffectTests.cs`,
  `PotionTests.cs`, plus additions to `BattleAssetContentTests.cs` and
  `BattleHistoryTests.cs`), all pure C#, all safe headless.
- **M12 — MP economy, FMV chroma-key components, code-based Skill Move tests.** Per
  the project owner's direction: verify Skill Moves with tests rather than interactive
  play, then build a real (if arbitrary-numbered) MP economy and the FMV plumbing
  clips need, even though final clip assets aren't chosen yet. `BattleUnit` gained
  `SpendMp`/`RestoreMp`/`RestoreMpFull`/`RecoverMpAfterBattle`; MP now has four
  sources -- a small trickle from a unit's own BA, 25-50% of missing MP restored
  between maps 1 and 2 (HP still doesn't recover there, on purpose), a new Support
  Skill Move **Mana Spring** (restores an ally's MP via a new `SkillDefinition
  .restoresMana` flag), and a `RestoreMpFull()` hook reserved for a future farm/town
  rest system. Found and fixed a real bug while adding Mana Spring: the auto-heal
  skill lookup could have handed a healer the mana-restore skill instead of the actual
  heal once a unit had two ally-targeting Skill Moves. Also shipped
  `Assets/Shaders/ChromaKeyVideo.shader` and `BattleClipPlayer.cs` -- a reusable,
  chroma-keyed `VideoPlayer` component per unit, wired into `BattleVisuals`/
  `BattleController` for the true basic attack only (Skill Moves share one placeholder
  clip key today and would show the wrong clip). This is live today against the 3 real
  clips M1/M2 already generated, not just scaffolding. Found a separate real bug along
  the way: the `impactFrames` metadata on the existing Clip assets is corrupted
  (`12000000` where `12` was surely meant) -- doesn't block anything since playback
  doesn't depend on it, but needs fixing (or the clips regenerating) before
  frame-accurate impact sync is worth building. 9 new EditMode tests (`MpRegenTests.cs`
  plus `BattleAssetContentTests` additions), all pure C#, all safe headless.
- **M11 — Skill Moves reach the game; content guard; SM tap.** M10's 9-skill system
  existed only as C# inside `BattleAssetBuilder` — the menu command that writes it to
  the ScriptableObjects was never run, so every character shipped with an empty
  `skillMoves` list. In-game that read as three separate bugs (SM permanently greyed
  out, no skills 1-3, mana bar never moving); all three were the same cause. Fixed by
  building the assets, then by three layers so it can't recur: `BattleContentGuard`
  (an `[InitializeOnLoad]` hook that re-runs the builder whenever `Resources/Battle`
  goes stale, and hard-bails in batchmode so it can't trip the asset-corruption bug),
  `BattleAssetContentTests` (7 tests reading the *built* assets — every other test in
  the suite builds objects in memory and so passed happily while the shipped content
  was empty), and a boot-time error naming any character with no Skill Moves. Also
  fixed: every ally-targeting skill rendered as "Heal" in the SM popup (Kestrel's list
  read *Heal / Heal / Power Strike*), a latent `??`-on-a-`UnityEngine.Object` in the
  map-background fallback, and SM's press-and-hold gesture — now a plain tap, since
  holding read as an unresponsive button next to BA/R/S.
- **M10 — Camera bugfix, Skill Move system, compact action UI, melee movement.** Found
  and fixed a real bug that predated M9: `BattleBootstrap`/`FarmBootstrap` never
  attached a `Camera` at all, because `??` doesn't detect Unity's "fake null" the way
  `== null` does. Replaced the old two-skill model with a free "BA" plus 3 mana-cost
  Skill Moves per archetype (9 new skills, heal/damage primitives only). Manual mode's
  action menu became 4 small icons (BA/SM/R/S) anchored under the acting character,
  melee attackers now walk up to their target, and the Settings panel gained
  damage/MP-cost dev-tuning sliders.
- **M9 — Frontline succession, bench, reposition, 2-map sequence.** A faction's
  formation auto-compacts when its frontmost unit dies. Three bench reserves
  (Thorne/Reed/Vesper) can be subbed in for an active unit, and allies can swap
  columns — both cost the turn. Healers gained an attack alongside their heal, and a
  map-1 victory carries the wounded party's HP into map 2.

## v0.4.0-battle-slice — M0-M8 (2026-08-16)

The full vertical slice: a playable, real-art, side-view party battle with auto and
manual/turn-based modes, a three-panel presentation, a reviewable turn log, and
modern-RPG UX (pause, settings, multi-step undo/redo).

- **M8 — Pause, settings, undo/redo, dock-spacing bugfix.** `BattleHistory` is a
  multi-step undo/redo stack (`Ctrl+Z`/`Ctrl+Y`) snapshotting unit HP/MP + the full
  turn log once per turn. Pause (`Esc`) and battle speed (0.5-4x) both ride on
  `Time.timeScale`. `BattleSettings` persists via `PlayerPrefs`. Also fixes a real
  dock-overlap bug found during visual verification (see M7).
- **M7 — Three-panel cinematic layout.** Allies dock left, enemies dock right, and
  the acting unit + its target tween into an empty centre "stage" for each turn's
  action before returning to their dock. Combat logic still only reasons about
  `Column` — the staging is presentation-only.
- **M6 — Turn log.** `BattleLog` records every turn as a round-tagged entry; a
  scrollable review panel (`L`) shows the full history.
- **M5 — Android build.** APK builds clean (IL2CPP, ARM64, min API 26); on-device
  install/verification blocked (no `adb` on the build machine at the time).
- **M4 — Curated HD roster art + manual mode.** Six named characters (Kestrel,
  Sable, Linnet vs. Husk, Warden, Stinger) replace generic placeholders, sourced from
  a pre-existing curated art library. Manual/turn-based mode (`T` to toggle) lets the
  player choose targets by clicking a highlighted unit.
- **M3 — Side-view battle logic pivot.** Pivoted from `FOUNDATION.md`'s original
  isometric lane/column tactics grid to a Darkest Dungeon / Slay the Spire-style
  static side view, after seeing the isometric version running. Turn queue, targeting,
  and damage resolution are pure C#, covered by EditMode tests.
- **M2 — Generated assets + import automation.** ComfyUI-generated sprites/portraits/
  clips imported and wired into `ScriptableObject` data via `BattleAssetBuilder`.
- **M1 — ComfyUI asset generation pipeline.** Workflows, manifest, generate/postprocess
  scripts for the (now largely superseded by curated art) pixel-art pipeline.
- **M0 — Scaffold.** Branch, `.gitignore`, folder structure, docs stub.

### Known gaps as of this release

- Manual mode's click-to-target and the M8 UI (pause/settings/undo/redo/log buttons)
  are code-complete and unit-tested, but haven't been interactively click-tested —
  see `PROJECT-README.md`'s "Known gaps" for why.
- Android APK builds clean but is unverified on a physical device (no `adb` here).
- FMV clip playback (chroma-keyed video, generated in M1/M2) isn't wired into
  `BattleVisuals` yet — static sprites only.
- Not merged to `main` — this is still a feature-branch vertical slice, not the
  shipped game.

## v0.3.x — Farm

Pre-battle-slice Unity farm scene (Stardew × Rune Factory 4×4 clearing,
Brown Dust 2-inspired isometric lighting). See tags `v0.1.0-farm-map` through
`v0.3.1-farm-aesthetics-2x2`.
