using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    public class DamageNumber
    {
        public Vector3 WorldPos;
        public string Text;
        public Color Color;
        public float Age;
    }

    /// <summary>Escaped and Quit (M19/M20) are terminal states that aren't defeats: the
    /// party is intact, they just left. Both return to camp with entry HP/MP restored;
    /// they differ only in what happens to the battle's earnings -- escaping banks them,
    /// quitting throws them away. See BattleController.LeaveBattle.</summary>
    public enum BattleOutcome { InProgress, PlayerVictory, EnemyVictory, Escaped, Quit }

    /// <summary>Which top-level action a manual-mode player turn resolved to.</summary>
    public enum ChosenAction { None, Skill, Reposition, Sub, Item, Escape, Quit }

    /// <summary>Drives what BattleHud shows during a manual-mode player turn.</summary>
    public enum ActionPhase { Idle, ChooseAction, ChooseBench, ChooseTarget }

    /// <summary>
    /// Turn state machine with two modes:
    ///  - Auto: both sides act automatically each turn (BD2/gacha-style "auto battle").
    ///  - Manual: enemy turns still resolve automatically, but a player-faction turn
    ///    pauses and waits for the player to choose an action (Attack/Heal, Reposition,
    ///    or Sub), then a target/bench pick if that action needs one.
    ///
    /// Also owns pause (Time.timeScale-driven -- every wait in this class and in
    /// BattleVisuals' stage tweens is a WaitForSeconds/Time.deltaTime, so scaling or
    /// zeroing Time.timeScale pauses and speed-controls the whole battle for free) and
    /// multi-step undo/redo via BattleHistory.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        public BattleWorld World { get; private set; }
        public BattleSettings Settings { get; private set; }
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.InProgress;
        public readonly BattleLog Log = new();
        public string LastAction => Log.Entries.Count > 0 ? Log.Entries[^1].Text : "";
        public readonly List<DamageNumber> DamageNumbers = new();

        public bool ManualMode { get; private set; }
        public bool Paused { get; private set; }
        public BattleUnit PendingActor { get; private set; }
        public ActionPhase Phase { get; private set; } = ActionPhase.Idle;
        public IReadOnlyList<BattleUnit> PendingTargets => _pendingTargets;
        public IReadOnlyList<BattleUnit> BenchOptions => World.Bench;
        public bool CanReposition => _repositionOptions.Count > 0;
        public bool CanSub => World.Bench.Count > 0;
        public bool CanUseItem => World.Inventory.HasAnyUsable;
        public BattleInventory Inventory => World.Inventory;

        // Undo/redo are closed off once the party has left the battle (M20). Escaping
        // and quitting both mutate state BattleHistory doesn't model -- entry HP/MP
        // restore, cleared status effects, a banked or binned reward ledger -- so
        // rewinding into the middle of a battle you've already walked out of would
        // reconstruct a half-correct world. Victory and defeat are unaffected.
        public bool CanUndo => _history.CanUndo && !HasLeftBattle;
        public bool CanRedo => _history.CanRedo && !HasLeftBattle;

        public bool HasLeftBattle => Outcome == BattleOutcome.Escaped || Outcome == BattleOutcome.Quit;

        /// <summary>What this battle has earned so far (M20) -- what Flee protects and
        /// Quit throws away. Surfaced for the action row, which shows the stake next to
        /// the flee odds so the player can weigh the two exits against each other.</summary>
        public BattleRewards PendingRewards => World.Pending;

        /// <summary>Raised when the party leaves a battle under its own power (M20) --
        /// escaped or quit. BattleBootstrap listens and boots the camp screen.</summary>
        public event Action OnLeaveRequested;

        /// <summary>Failed escape attempts this battle (M19). Feeds EscapeCalculator's
        /// escalating bonus, and reset by Init -- deliberately *not* rolled back by
        /// Undo. BattleHistory snapshots unit HP/MP/column and the log; threading a
        /// scalar through it for this is a wider change than the feature justifies, and
        /// the failure mode is benign in the only direction that matters: undoing a
        /// failed escape and retrying keeps the accumulated bonus, so the player is
        /// never trapped, only occasionally let off easy.</summary>
        public int FailedEscapeAttempts { get; private set; }

        /// <summary>Whether the party may attempt to flee this battle at all (M19).
        /// Maps can forbid it (MapDefinition.forbidEscape) -- the standard "you can't
        /// run from a boss" rule, and the hook a future boss-phase system wants. No
        /// shipped map sets it yet, so this is true everywhere today.</summary>
        public bool CanEscape => World != null && World.Map != null && !World.Map.forbidEscape;

        /// <summary>Live escape odds, for the HUD to show before the player commits a
        /// turn to it. Same call ResolveEscape rolls against, so the number on screen is
        /// the number that gets used -- not an approximation of it.</summary>
        public float EscapeChanceNow => World == null ? 0f : EscapeCalculator.EscapeChance(
            World.PlayerUnits, World.EnemyUnits, FailedEscapeAttempts);

        /// <summary>Whether "skip this battle, go to the next stage" (M18) is available
        /// right now. Two conditions, and the second one is not cosmetic: BattleWorld's
        /// carry-over path drops the dead, so advancing with a wiped party boots the next
        /// map with *zero* player units -- and PlayerDefeated is `PlayerUnits.Any() &&
        /// PlayerUnits.All(dead)`, which is false when there are none at all. The battle
        /// would then never end, with every enemy turn logging "has no usable skill"
        /// forever. See BattleWorldTests.CarryingOverAWipedParty_WouldLeaveABattleThatCanNeverEnd,
        /// which pins that hazard so this guard can't be dropped as redundant-looking.
        /// A lost battle fails this check on its own (no survivors), which is the right
        /// answer -- Restart is the way out of a defeat, not Skip.</summary>
        public bool CanSkipToNextMap =>
            World != null && World.LoadedOk && World.HasNextMap && World.PlayerUnits.Any(u => u.IsAlive);

        public event Action OnRestartRequested;
        public event Action OnAdvanceRequested;

        /// <summary>Non-destructive preview for BattleHud's turn-order strip (M16) --
        /// see TurnOrder.PeekUpcoming. Empty before Init runs.</summary>
        public IReadOnlyList<BattleUnit> UpcomingTurnOrder(int count) =>
            _turnOrder?.PeekUpcoming(count) ?? new List<BattleUnit>();

        /// <summary>Dev-testing shortcut (M16, from the Unit Stats panel) -- instantly
        /// fills one unit's ultimate gauge so its ultimate can be tested on that unit's
        /// very next turn instead of grinding a whole battle to charge it normally.</summary>
        public void DebugMaxUltimateCharge(BattleUnit unit) => unit.GainUltimateCharge(BattleUnit.MaxUltimateCharge);

        BattleVisuals _visuals;
        Camera _cam;
        TurnOrder _turnOrder;
        readonly BattleHistory _history = new();
        Coroutine _runCoroutine;
        List<BattleUnit> _pendingTargets = new();
        List<BattleUnit> _repositionOptions = new();
        BattleUnit _submittedTarget;
        ChosenAction _chosenAction;
        SkillDefinition _chosenSkill;
        BattleUnit _chosenSubIncoming;
        PotionKind? _chosenItemKind;
        const float PreActionDelaySeconds = 0.35f;
        const float ImpactHoldSeconds = 0.5f;

        /// <summary>"very small amount back" per project owner direction on the MP-economy
        /// design -- an arbitrary number, not a tuned one. Only the true BA (skill ==
        /// unit.Definition.standardSkill) grants this, so it can't be farmed by a Skill
        /// Move that happens to cost 0 MP (e.g. the dev-tuning MpCostMultiplier slider at 0x).</summary>
        const int BasicAttackMpRegen = 4;

        /// <summary>Passive per-turn trickle (M13), on top of the BA-specific bonus above
        /// -- deliberately smaller than BasicAttackMpRegen so it doesn't trivialize that
        /// bonus, but the project owner's own framing is the point: small per turn adds
        /// up over a long battle. Applies to every unit's own turn regardless of what
        /// action they take (even a skipped/stunned one) or which faction they're on.</summary>
        const int PassiveMpRegenPerTurn = 3;

        /// <summary>Ultimate gauge (M16), same two-source shape as the MP economy above:
        /// a bigger chunk for actually acting (granted in ResolveAction, any skill --
        /// "every action", not just BA like MP's equivalent bonus) plus a smaller passive
        /// trickle every turn regardless of action. Arbitrary numbers, not tuned.</summary>
        const int UltimateChargePerAction = 15;
        const int PassiveUltimateChargePerTurn = 5;

        /// <summary>Break (M16): a unit that takes this fraction of its own max HP in
        /// damage since its last turn enters Break right as its next turn comes up --
        /// skips that turn (BattleUnit.IsIncapacitated) and takes bonus damage while down
        /// (folded into DefenseMultiplier). Arbitrary numbers, not a tuned balance pass.
        /// Threshold is public so BattleHud can render a break-progress bar against the
        /// same number this class actually checks against.</summary>
        public const float BreakDamageThresholdFraction = 0.3f;
        const float BreakVulnerabilityBonus = 0.3f;
        const int BreakDurationTurns = 1;

        public void Init(BattleWorld world, BattleVisuals visuals, Camera cam, BattleSettings settings)
        {
            World = world;
            _visuals = visuals;
            _cam = cam;
            Settings = settings;
            Outcome = BattleOutcome.InProgress;
            Paused = false;
            FailedEscapeAttempts = 0;
            ManualMode = !settings.AutoModeDefault;
            Time.timeScale = settings.SpeedMultiplier;

            _turnOrder = new TurnOrder(world.AllUnits);
            if (!world.LoadedOk) return;

            _history.Capture(World.AllUnits, World.Bench, Log, World.Inventory);
            _runCoroutine = StartCoroutine(RunBattle());
        }

        void Update()
        {
            for (int i = DamageNumbers.Count - 1; i >= 0; i--)
            {
                DamageNumbers[i].Age += Time.deltaTime;
                if (DamageNumbers[i].Age > 1.2f) DamageNumbers.RemoveAt(i);
            }

            if (Outcome != BattleOutcome.InProgress && Input.GetKeyDown(KeyCode.R)) Restart();
            // Works while paused too -- Update isn't gated by Time.timeScale, and the
            // next map's Init clears Paused anyway.
            if (Input.GetKeyDown(KeyCode.N)) SkipToNextMap();
            if (Input.GetKeyDown(KeyCode.T)) ToggleMode();
            if (Input.GetKeyDown(KeyCode.Escape)) SetPaused(!Paused);

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            if (ctrl && Input.GetKeyDown(KeyCode.Z)) Undo();
            if (ctrl && Input.GetKeyDown(KeyCode.Y)) Redo();

            if (Phase == ActionPhase.ChooseTarget && !Paused && Input.GetMouseButtonDown(0)) HandleClick(Input.mousePosition);
        }

        void OnDestroy()
        {
            // Battle scene owns global Time.timeScale while it's active -- don't leak a
            // paused/slowed state into whatever loads next.
            Time.timeScale = 1f;
        }

        public void ToggleMode() => ManualMode = !ManualMode;

        public void SetPaused(bool paused)
        {
            Paused = paused;
            Time.timeScale = Paused ? 0f : Settings.SpeedMultiplier;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            Settings.SpeedMultiplier = multiplier;
            Settings.Save();
            if (!Paused) Time.timeScale = multiplier;
        }

        public void Undo()
        {
            if (!_history.CanUndo) return;
            _history.Undo(World.AllUnits, World.Bench, Log, World.Inventory);
            ResumeFromHistory();
        }

        public void Redo()
        {
            if (!_history.CanRedo) return;
            _history.Redo(World.AllUnits, World.Bench, Log, World.Inventory);
            ResumeFromHistory();
        }

        void ResumeFromHistory()
        {
            if (_runCoroutine != null) StopCoroutine(_runCoroutine);
            PendingActor = null;
            Phase = ActionPhase.Idle;
            _pendingTargets = new List<BattleUnit>();
            _submittedTarget = null;
            Outcome = BattleOutcome.InProgress;

            _turnOrder = new TurnOrder(World.AllUnits);
            for (int i = 0; i < _history.Cursor; i++) _turnOrder.Next();

            _visuals.SyncAll(World);
            _visuals.SnapAllToDock(World);

            _runCoroutine = StartCoroutine(RunBattle());
        }

        void LogLine(string text) => Log.Add(_turnOrder.RoundNumber, text);

        void HandleClick(Vector3 screenPos)
        {
            if (_cam == null || !_visuals.TryGetUnitAtScreenPoint(screenPos, _cam, out var clicked)) return;
            if (!_pendingTargets.Contains(clicked)) return;
            _submittedTarget = clicked;
        }

        // -- manual-mode action menu (called by BattleHud) --------------------------

        /// <summary>Player picked "Attack"/"Heal" (standardSkill) or the secondary
        /// attack, if the acting unit has one. Wakes RunManualPlayerTurn's action-choice
        /// wait; target selection happens next via HandleClick.</summary>
        public void ChooseSkill(SkillDefinition skill)
        {
            if (skill == null) return;
            _chosenSkill = skill;
            _chosenAction = ChosenAction.Skill;
        }

        /// <summary>Player picked Flee (M19). Costs the turn whether or not it works --
        /// resolution happens in RunManualPlayerTurn, not here, so the roll stays on the
        /// coroutine's timeline like every other action.</summary>
        public void ChooseEscape()
        {
            if (!CanEscape) return;
            _chosenAction = ChosenAction.Escape;
        }

        /// <summary>Player picked Quit (M20) -- abandon the fight outright. No roll and
        /// no speed check, unlike Flee: quitting always works. What it costs is the
        /// battle's entire haul, which is exactly the trade -- quitting is free when
        /// you've earned nothing and ruinous once you have. Still routed through the
        /// action queue rather than resolved here, so it spends the turn's slot like
        /// every other action and can't fire mid-animation.</summary>
        public void ChooseQuit() => _chosenAction = ChosenAction.Quit;

        /// <summary>Player picked Reposition -- swap column with an adjacent ally.</summary>
        public void ChooseReposition()
        {
            if (!CanReposition) return;
            _chosenAction = ChosenAction.Reposition;
        }

        /// <summary>HUD-only navigation into the bench picker -- doesn't wake the
        /// coroutine yet (that happens once a bench unit is actually chosen).</summary>
        public void OpenBenchMenu()
        {
            if (!CanSub) return;
            Phase = ActionPhase.ChooseBench;
        }

        public void CancelBenchMenu() => Phase = ActionPhase.ChooseAction;

        /// <summary>Player picked which bench unit subs in for the acting unit.</summary>
        public void ChooseSub(BattleUnit benchUnit)
        {
            if (benchUnit == null || !World.Bench.Contains(benchUnit)) return;
            _chosenSubIncoming = benchUnit;
            _chosenAction = ChosenAction.Sub;
        }

        /// <summary>Player picked which potion slot to use (M13) -- wakes RunManualPlayerTurn's
        /// action-choice wait the same way ChooseSkill does; target selection happens next
        /// via HandleClick, offered to the whole living ally faction (any ally, not just
        /// ones missing HP/MP -- matches how a real player can waste a potion on purpose).</summary>
        public void ChooseItem(PotionKind kind)
        {
            if (!World.Inventory.Slot(kind).IsUsable) return;
            _chosenItemKind = kind;
            _chosenAction = ChosenAction.Item;
        }

        /// <summary>The "BA" quick-attack slot -- always free, always standardSkill.</summary>
        public SkillDefinition BasicAttackSkill(BattleUnit unit) => unit.Definition.standardSkill;

        /// <summary>The "SM" (Skill Move) list -- mana-cost skills beyond BA.</summary>
        public IReadOnlyList<SkillDefinition> SkillMoveOptions(BattleUnit unit) =>
            unit.Definition.skillMoves.Where(s => s != null).ToList();

        /// <summary>skill.mpCost scaled by the dev-tuning Settings.MpCostMultiplier --
        /// what actually gets spent/checked against, not the raw authored cost.</summary>
        public int EffectiveMpCost(SkillDefinition skill) => Mathf.RoundToInt(skill.mpCost * Settings.MpCostMultiplier);

        IEnumerator RunBattle()
        {
            while (!World.IsOver && !HasLeftBattle)
            {
                var unit = _turnOrder.Next();
                if (unit == null) break;

                yield return new WaitForSeconds(PreActionDelaySeconds);

                unit.RestoreMp(PassiveMpRegenPerTurn);
                unit.GainUltimateCharge(PassiveUltimateChargePerTurn);

                // Break (M16): evaluated right as this unit's turn comes up, against
                // damage taken since its last one. Applied BEFORE the wasStunned read
                // below so a freshly-triggered Break (duration 1) skips this same turn --
                // identical ordering trick to the existing 1-turn-Stun case just below.
                if (!unit.IsIncapacitated && unit.DamageTakenSinceLastTurn >= BreakDamageThresholdFraction * unit.Stats.hp)
                {
                    unit.ApplyStatus(StatusEffectType.Break, BreakVulnerabilityBonus, BreakDurationTurns);
                    LogLine($"{unit.Definition.displayName} is broken!");
                }
                unit.DamageTakenSinceLastTurn = 0;

                // Checked BEFORE TickStatusEffects (which decrements/removes expired
                // effects) so a 1-turn Stun/Break skips exactly one turn: turn 1 sees
                // wasStunned=true and skips while the tick counts 1->0 and removes it,
                // turn 2 sees no Stun/Break left and acts normally.
                bool wasStunned = unit.IsIncapacitated;
                unit.TickStatusEffects();
                _visuals.SyncStatusTint(unit);

                if (!unit.IsAlive)
                {
                    // Poison finished them off on their own turn tick -- nothing in the
                    // normal hit-resolution path runs for this, so replicate the same
                    // death bookkeeping ResolveAction does on a killing blow.
                    LogLine($"{unit.Definition.displayName} succumbs to poison.");
                    _visuals.SyncDefeated(unit);
                    Formation.Compact(World.AllUnits, unit.Faction);
                    AwardKill(unit);
                }
                else if (wasStunned)
                {
                    LogLine($"{unit.Definition.displayName} can't act.");
                }
                else if (ManualMode && unit.Faction == Faction.Player)
                    yield return RunManualPlayerTurn(unit);
                else
                    yield return RunAutoTurn(unit);

                // One capture per consumed TurnOrder.Next() -- see BattleHistory's
                // class doc for why this 1:1 correspondence matters for Undo/Redo.
                _history.Capture(World.AllUnits, World.Bench, Log, World.Inventory);
            }

            // Escaping or quitting (M19/M20) already set Outcome and logged its own
            // line -- don't relabel it as a victory just because the party is standing.
            if (HasLeftBattle) yield break;

            Outcome = World.PlayerDefeated ? BattleOutcome.EnemyVictory : BattleOutcome.PlayerVictory;
            if (Outcome == BattleOutcome.PlayerVictory)
            {
                // Winning banks the haul, same as escaping -- the difference is that a
                // win also lets you move on. A defeat keeps Pending unbanked, so a
                // wipe loses the fight's earnings exactly like quitting does.
                World.Banked.Absorb(World.Pending);
                LogLine($"Victory! Spoils: {World.Pending.Describe()}.");
                LogLine(World.HasNextMap ? "Proceed to the next battle." : "The dungeon is clear!");
            }
            else
                LogLine($"Defeat... {World.Pending.Describe()} lost with the party.");
        }

        IEnumerator RunAutoTurn(BattleUnit unit)
        {
            var skill = ChooseAutoSkill(unit, out var targets);
            if (skill == null)
            {
                LogLine($"{unit.Definition.displayName} has no usable skill.");
                yield break;
            }
            if (targets.Count == 0)
            {
                LogLine($"{unit.Definition.displayName} has no valid target.");
                yield break;
            }

            var target = targets[UnityEngine.Random.Range(0, targets.Count)];
            yield return _visuals.MoveToStage(unit, target);
            var hitTargets = ResolveAction(unit, skill, target);
            yield return PlayImpactBeat(unit, skill);
            yield return _visuals.ReturnToDock(unit, target);
            foreach (var faction in DeadFactionsAmong(hitTargets)) yield return _visuals.ReflowFormation(World, faction);
        }

        /// <summary>"Sometimes reach for an offensive Skill Move instead of BA" chance
        /// (M14) -- without this, a Skill Move that inflicts a status effect was dead
        /// content for any unit that never gets a manual-mode turn (every enemy, and
        /// every player unit while in auto mode): ChooseAutoSkill only ever picked BA or
        /// the one heal carve-out below. Added specifically so map 2's Rotfang/Deadeye/
        /// Hexweaver (M14) actually use their Poison/Attack-Down/Defense-Down kits in a
        /// real battle -- but it applies to every unit in auto mode, not just enemies,
        /// so player units auto-battling now also occasionally use Power Strike/Snipe/
        /// Barrage instead of only ever basic-attacking. Arbitrary, not tuned.</summary>
        const float OffensiveSkillMoveChance = 0.35f;

        /// <summary>Auto-mode/enemy skill choice. Healer-archetype units heal (their
        /// mana-cost skillMoves entry with targetsAllies) when an ally is missing HP and
        /// they can afford it, otherwise fall back to BA -- keeps a healer from wasting
        /// turns topping off a full-HP ally once nobody nearby needs it. Beyond that,
        /// OffensiveSkillMoveChance (M14) gives a chance per turn to reach for a
        /// non-heal Skill Move instead of BA; Reposition/Sub remain manual-only.</summary>
        SkillDefinition ChooseAutoSkill(BattleUnit unit, out List<BattleUnit> targets)
        {
            var basic = unit.Definition.standardSkill;
            if (basic == null || basic.pattern == null)
            {
                targets = new List<BattleUnit>();
                return null;
            }

            // Ultimate (M16) takes priority over everything else once ready -- it's a
            // rare, earned resource, not a per-turn tactical option like the offensive
            // Skill Move roll below, so auto mode always spends it rather than rolling a
            // chance. Falls through to normal choice if there's no ultimateSkill authored
            // yet (every character today) or it has no valid target this turn.
            if (unit.IsUltimateReady && unit.Definition.ultimateSkill != null)
            {
                var ultTargets = TargetResolver.GetValidTargets(unit, unit.Definition.ultimateSkill, World.AllUnits);
                if (ultTargets.Count > 0)
                {
                    targets = ultTargets;
                    return unit.Definition.ultimateSkill;
                }
            }

            // !restoresMana excludes Mana Spring (M12) -- without it, FirstOrDefault could
            // just as easily hand auto mode the mana-restore skill instead of the actual
            // heal whenever list order put it first, and a "healer" that tops up MP while
            // an ally bleeds out reads as broken, not clever. Mana Spring stays manual-only,
            // like the rest of skillMoves beyond this one auto-heal carve-out.
            var healMove = unit.Definition.skillMoves.FirstOrDefault(s => s != null && s.targetsAllies && !s.restoresMana);
            if (healMove != null && unit.CurrentMp >= healMove.mpCost)
            {
                bool allyNeedsHeal = World.AllUnits.Any(u =>
                    u.Faction == unit.Faction && u.IsAlive && u.CurrentHp < u.Stats.hp);
                if (allyNeedsHeal)
                {
                    var healTargets = TargetResolver.GetValidTargets(unit, healMove, World.AllUnits);
                    if (healTargets.Count > 0)
                    {
                        targets = healTargets;
                        return healMove;
                    }
                }
            }

            var offensiveMoves = unit.Definition.skillMoves
                .Where(s => s != null && !s.targetsAllies && unit.CurrentMp >= EffectiveMpCost(s))
                .ToList();
            if (offensiveMoves.Count > 0 && UnityEngine.Random.value < OffensiveSkillMoveChance)
            {
                var chosen = offensiveMoves[UnityEngine.Random.Range(0, offensiveMoves.Count)];
                var moveTargets = TargetResolver.GetValidTargets(unit, chosen, World.AllUnits);
                if (moveTargets.Count > 0)
                {
                    targets = moveTargets;
                    return chosen;
                }
            }

            targets = TargetResolver.GetValidTargets(unit, basic, World.AllUnits);
            return basic;
        }

        IEnumerator RunManualPlayerTurn(BattleUnit unit)
        {
            PendingActor = unit;
            _repositionOptions = World.AllUnits
                .Where(u => u.Faction == unit.Faction && u.IsAlive && Mathf.Abs(u.Column - unit.Column) == 1)
                .ToList();
            _chosenAction = ChosenAction.None;
            _chosenSkill = null;
            _chosenSubIncoming = null;
            _chosenItemKind = null;
            _submittedTarget = null;
            Phase = ActionPhase.ChooseAction;
            LogLine($"{unit.Definition.displayName}'s turn -- choose an action.");

            yield return new WaitUntil(() => _chosenAction != ChosenAction.None);

            switch (_chosenAction)
            {
                case ChosenAction.Skill:
                {
                    Phase = ActionPhase.ChooseTarget;
                    _pendingTargets = TargetResolver.GetValidTargets(unit, _chosenSkill, World.AllUnits);
                    if (_pendingTargets.Count == 0)
                    {
                        LogLine($"{unit.Definition.displayName} has no valid target.");
                        break;
                    }
                    yield return new WaitUntil(() => _submittedTarget != null);
                    var target = _submittedTarget;
                    yield return _visuals.MoveToStage(unit, target);
                    var hitTargets = ResolveAction(unit, _chosenSkill, target);
                    yield return PlayImpactBeat(unit, _chosenSkill);
                    yield return _visuals.ReturnToDock(unit, target);
                    foreach (var faction in DeadFactionsAmong(hitTargets)) yield return _visuals.ReflowFormation(World, faction);
                    break;
                }
                case ChosenAction.Reposition:
                {
                    Phase = ActionPhase.ChooseTarget;
                    _pendingTargets = _repositionOptions;
                    yield return new WaitUntil(() => _submittedTarget != null);
                    var neighbor = _submittedTarget;
                    LogLine($"{unit.Definition.displayName} repositions with {neighbor.Definition.displayName}.");
                    (unit.Column, neighbor.Column) = (neighbor.Column, unit.Column);
                    yield return _visuals.SwapPositions(unit, neighbor);
                    break;
                }
                case ChosenAction.Sub:
                {
                    var incoming = _chosenSubIncoming;
                    SubUnit(unit, incoming);
                    yield return _visuals.SwapUnitView(unit, incoming);
                    break;
                }
                case ChosenAction.Escape:
                {
                    ResolveEscape(unit);
                    yield return new WaitForSeconds(ImpactHoldSeconds);
                    break;
                }
                case ChosenAction.Quit:
                {
                    LeaveBattle(BattleOutcome.Quit, keepRewards: false);
                    yield return new WaitForSeconds(ImpactHoldSeconds);
                    break;
                }
                case ChosenAction.Item:
                {
                    Phase = ActionPhase.ChooseTarget;
                    // Any living ally, not just ones missing HP/MP -- a real player can
                    // choose to "waste" a potion on a full-HP unit if they want to, same
                    // as Heal already allows.
                    _pendingTargets = World.AllUnits.Where(u => u.Faction == unit.Faction && u.IsAlive).ToList();
                    yield return new WaitUntil(() => _submittedTarget != null);
                    var target = _submittedTarget;
                    yield return _visuals.MoveToStage(unit, target);
                    UseItem(unit, _chosenItemKind.Value, target);
                    yield return new WaitForSeconds(ImpactHoldSeconds);
                    yield return _visuals.ReturnToDock(unit, target);
                    break;
                }
            }

            PendingActor = null;
            Phase = ActionPhase.Idle;
            _pendingTargets = new List<BattleUnit>();
        }

        void SubUnit(BattleUnit outgoing, BattleUnit incoming)
        {
            incoming.Column = outgoing.Column;
            outgoing.Column = BattleWorld.BenchColumn;
            World.AllUnits.Remove(outgoing);
            World.AllUnits.Add(incoming);
            World.Bench.Remove(incoming);
            World.Bench.Add(outgoing);
            LogLine($"{outgoing.Definition.displayName} subs out for {incoming.Definition.displayName}.");
        }

        /// <summary>Consumes one potion from the chosen slot and applies its effect to
        /// target (M13). Free -- no MP cost, this is a physical item, not magic -- but,
        /// like every other manual-mode action, costs the acting unit's turn. Silently
        /// no-ops if the slot ran out between ChooseItem and now (shouldn't happen in
        /// practice -- Item's icon greys out via CanUseItem the instant a slot hits 0 --
        /// but the slot could only ever be read as usable at click time, not resolve
        /// time, without this guard).</summary>
        void UseItem(BattleUnit user, PotionKind kind, BattleUnit target)
        {
            var slot = World.Inventory.Slot(kind);
            if (!slot.IsUsable) return;
            slot.Count--;

            int potency = PotionCalculator.Potency(slot.Potion.rank);
            if (kind == PotionKind.Hp || kind == PotionKind.Multi)
            {
                target.ApplyHeal(potency);
                LogLine($"{user.Definition.displayName} uses {slot.Potion.displayName} on {target.Definition.displayName} (+{potency} HP).");
                if (Settings.ShowDamageNumbers) SpawnDamageNumber(target, $"+{potency}", new Color(0.55f, 0.9f, 0.55f));
            }
            if (kind == PotionKind.Mp || kind == PotionKind.Multi)
            {
                target.RestoreMp(potency);
                LogLine($"{user.Definition.displayName} uses {slot.Potion.displayName} on {target.Definition.displayName} (+{potency} MP).");
                if (Settings.ShowDamageNumbers) SpawnDamageNumber(target, $"+{potency} MP", new Color(0.45f, 0.65f, 0.95f));
            }
        }

        /// <summary>Resolves a skill against a chosen target tile and returns every unit
        /// actually hit -- one for single-target skills, several for an AoE skill (a
        /// pattern with more than one areaOffset, e.g. Volley). Deducts mpCost up front
        /// regardless of outcome. Callers use the returned list to know which factions
        /// might need BattleVisuals.ReflowFormation afterward.</summary>
        List<BattleUnit> ResolveAction(BattleUnit unit, SkillDefinition skill, BattleUnit target)
        {
            int mpSpent = EffectiveMpCost(skill);
            if (mpSpent > 0)
                unit.SpendMp(mpSpent);
            else if (skill == unit.Definition.standardSkill)
                unit.RestoreMp(BasicAttackMpRegen);

            // "Every action" (M16) -- any skill use grants ultimate charge, not just BA
            // like the MP bonus above. The ultimate itself drains the gauge instead of
            // adding to it -- it just consumed the whole bar, granting more the same turn
            // would be a (harmless but confusing) residual charge.
            if (skill == unit.Definition.ultimateSkill)
                unit.SpendUltimateCharge();
            else
                unit.GainUltimateCharge(UltimateChargePerAction);

            bool isAoe = skill.pattern != null && skill.pattern.areaOffsets.Count > 1;
            var hitTargets = isAoe
                ? TargetResolver.GetAreaTargets(unit, skill, target.Column, World.AllUnits)
                : new List<BattleUnit> { target };

            // Accuracy (M16) only applies to offensive skills -- heals/buffs never miss,
            // matching genre convention, so ally-targeting skills skip the roll entirely
            // and always "hit" every target in hitTargets. Rolled once per target up
            // front (not per branch) since both the status-effect application below and
            // the damage loop further down need to agree on who actually got hit.
            bool isOffensive = !skill.targetsAllies;
            var wasHit = hitTargets.ToDictionary(h => h,
                h => !isOffensive || UnityEngine.Random.value < DamageCalculator.HitChance(unit, h));

            // Applied up front, before the heal/mana/damage branches below (each of
            // which returns hitTargets immediately once done) -- a status effect isn't
            // tied to which of those branches fires, so it can't live inside any one of
            // them without duplicating this across all three. Gated on wasHit so a missed
            // offensive attack doesn't still land its status effect.
            if (skill.inflictsStatus != StatusEffectType.None)
            {
                foreach (var hit in hitTargets)
                {
                    if (!wasHit[hit]) continue;
                    hit.ApplyStatus(skill.inflictsStatus, skill.statusMagnitude, skill.statusDuration);
                    LogLine($"{hit.Definition.displayName} is affected by {skill.inflictsStatus}.");
                }
            }

            if (skill.targetsAllies && skill.restoresMana)
            {
                foreach (var ally in hitTargets)
                {
                    int restored = DamageCalculator.ComputeManaRestore(unit, skill);
                    ally.RestoreMp(restored);
                    LogLine($"{unit.Definition.displayName} restores {restored} MP to {ally.Definition.displayName}.");
                    if (Settings.ShowDamageNumbers) SpawnDamageNumber(ally, $"+{restored} MP", new Color(0.45f, 0.65f, 0.95f));
                }
                return hitTargets;
            }

            if (skill.targetsAllies)
            {
                foreach (var ally in hitTargets)
                {
                    int heal = DamageCalculator.ComputeHeal(unit, skill);
                    ally.ApplyHeal(heal);
                    LogLine($"{unit.Definition.displayName} heals {ally.Definition.displayName} for {heal}.");
                    if (Settings.ShowDamageNumbers) SpawnDamageNumber(ally, $"+{heal}", new Color(0.55f, 0.9f, 0.55f));
                }
                return hitTargets;
            }

            foreach (var hit in hitTargets)
            {
                if (!wasHit[hit])
                {
                    LogLine($"{unit.Definition.displayName} misses {hit.Definition.displayName}.");
                    continue;
                }

                int distance = TargetResolver.ColumnDistance(unit, hit);
                bool isCrit = UnityEngine.Random.value < unit.Stats.critRate;
                int damage = DamageCalculator.ComputeDamage(unit, hit, skill, distance, isCrit);
                // Dev-convenience multipliers for speeding through battles while the game
                // is being built -- boosts damage the player deals, softens damage the
                // player takes. 1x on both is the real, untuned rate.
                float mult = unit.Faction == Faction.Player
                    ? Settings.DamageDealtMultiplier
                    : Settings.DamageReceivedMultiplier;
                damage = Mathf.Max(0, Mathf.RoundToInt(damage * mult));
                hit.ApplyDamage(damage);
                hit.DamageTakenSinceLastTurn += damage;
                LogLine(isCrit
                    ? $"{unit.Definition.displayName} CRITS {hit.Definition.displayName} for {damage}!"
                    : $"{unit.Definition.displayName} hits {hit.Definition.displayName} for {damage}.");
                if (Settings.ShowDamageNumbers) SpawnDamageNumber(hit, damage.ToString(), isCrit ? new Color(1f, 0.75f, 0.2f) : Color.white);
                _visuals.FlashHit(hit);
                _visuals.PlayImpactFx(hit, skill);
                if (_visuals.HasReactionClip(hit)) _visuals.PlayReactionClip(hit);
                if (!hit.IsAlive)
                {
                    _visuals.SyncDefeated(hit);
                    Formation.Compact(World.AllUnits, hit.Faction);
                    AwardKill(hit);
                }
            }
            return hitTargets;
        }

        /// <summary>The post-hit beat between ResolveAction and ReturnToDock: plays the
        /// action's FMV clip when one exists (M12), otherwise the original flat pause.
        /// Shared by both RunAutoTurn and RunManualPlayerTurn -- was duplicated verbatim
        /// as `yield return new WaitForSeconds(ImpactHoldSeconds);` in both before this.</summary>
        IEnumerator PlayImpactBeat(BattleUnit unit, SkillDefinition skill)
        {
            if (_visuals.HasActionClip(unit, skill))
                yield return _visuals.PlayActionClip(unit, skill, onImpact: null);
            else
                yield return new WaitForSeconds(ImpactHoldSeconds);
        }

        static IEnumerable<Faction> DeadFactionsAmong(IEnumerable<BattleUnit> hitTargets) =>
            hitTargets.Where(t => !t.IsAlive).Select(t => t.Faction).Distinct();

        void SpawnDamageNumber(BattleUnit target, string text, Color color)
        {
            DamageNumbers.Add(new DamageNumber
            {
                WorldPos = _visuals.GetUnitWorldPosition(target),
                Text = text,
                Color = color,
                Age = 0f,
            });
        }

        /// <summary>Rolls one escape attempt for `unit`'s side (M19). The turn is spent
        /// either way -- that's the cost of trying, and the reason a failed attempt isn't
        /// simply free retries until it lands.
        ///
        /// The roll lives here rather than in EscapeCalculator for the same reason crit
        /// and the offensive-Skill-Move AI chance do: UnityEngine.Random can't be tested
        /// headlessly, so the pure math stays in a class that can be, and only the die
        /// itself lives on the MonoBehaviour.</summary>
        void ResolveEscape(BattleUnit unit)
        {
            // Player-faction only, and EscapeChanceNow's player-vs-enemy framing assumes
            // it: ChooseEscape is reachable only from the manual-mode action row, and
            // ChooseAutoSkill has no escape branch, so no enemy can get here. Enemies
            // fleeing would need the chance computed from the acting unit's own side --
            // deliberately not built, since nothing wants it yet.
            float chance = EscapeChanceNow;

            if (UnityEngine.Random.value <= chance)
            {
                LogLine($"{unit.Definition.displayName} calls the retreat. ({chance:P0} chance)");
                LeaveBattle(BattleOutcome.Escaped, keepRewards: true);
                return;
            }

            FailedEscapeAttempts++;
            LogLine($"The party couldn't get away. ({chance:P0} chance -- the next attempt is easier)");
        }

        /// <summary>The shared exit for both ways of walking out of a fight (M20).
        ///
        /// The whole design lives in the one `keepRewards` flag. Escaping banks what the
        /// battle earned; quitting bins it. Everything else the two share: the party goes
        /// back to the HP/MP it walked in with (RestoreEntryState), and the camp screen
        /// picks up from there. That's what makes them a real choice rather than two
        /// words for the same button -- escaping is slow and can fail but protects a haul
        /// you've built up, quitting is instant and certain but only sane while you have
        /// nothing to lose.
        ///
        /// Restoring entry HP deliberately un-kills anyone who died this battle: a
        /// withdrawal undoes the fight, and a fight you undid didn't kill anyone. See
        /// BattleWorld.RestoreEntryState.</summary>
        void LeaveBattle(BattleOutcome outcome, bool keepRewards)
        {
            string haul = World.Pending.Describe();

            if (keepRewards)
            {
                World.Banked.Absorb(World.Pending);
                LogLine($"The party escaped with {haul}.");
            }
            else
            {
                LogLine(World.Pending.IsEmpty
                    ? "The party withdrew. Nothing had been earned yet -- nothing lost."
                    : $"The party withdrew, abandoning {haul}.");
            }

            World.Pending.Clear();
            World.RestoreEntryState();
            _visuals.SyncAll(World);
            Outcome = outcome;
        }

        /// <summary>Credits a defeated enemy's EXP/materials to this battle's pending
        /// haul (M20). Called from both death paths -- the killing blow in ResolveAction
        /// and the poison tick in RunBattle -- so how an enemy died never changes what it
        /// pays out. Player deaths award nothing, obviously.</summary>
        void AwardKill(BattleUnit dead)
        {
            if (dead.Faction != Faction.Enemy) return;
            World.Pending.Award(dead);
        }

        /// <summary>Hand off to the camp screen after escaping or quitting (M20).
        /// Driven by the outcome banner's button rather than fired automatically from
        /// LeaveBattle, so the player gets a beat to read what they kept or lost before
        /// the scene changes -- the same shape victory's "Next Battle" button already
        /// had. Stops the turn coroutine for the same reason AdvanceToNextMap does.</summary>
        public void GoToCamp()
        {
            if (!HasLeftBattle) return;
            if (_runCoroutine != null) { StopCoroutine(_runCoroutine); _runCoroutine = null; }
            OnLeaveRequested?.Invoke();
        }

        public void Restart() => OnRestartRequested?.Invoke();

        /// <summary>Move on to the next map, carrying the party as it stands. Used both
        /// by the victory banner's "Next Battle" button and by SkipToNextMap below.
        ///
        /// Stops the turn coroutine first. On the victory path that's a no-op (RunBattle
        /// has already returned by then), but a mid-battle skip can land in the middle of
        /// a turn, and the listener rebuilds the whole scene under us -- Destroy is
        /// deferred to end of frame, so without this the rest of the in-flight turn would
        /// still resolve against a world that's being replaced.</summary>
        public void AdvanceToNextMap()
        {
            if (_runCoroutine != null) { StopCoroutine(_runCoroutine); _runCoroutine = null; }
            OnAdvanceRequested?.Invoke();
        }

        /// <summary>Forfeit the current battle and jump straight to the next stage (M18),
        /// party/bench/inventory carried over exactly as they stand -- same path a
        /// victory takes, minus the winning. Deliberately has no confirmation prompt,
        /// unlike Restart: skipping *advances* and keeps everything you're carrying, so
        /// the only thing lost is this battle's undo history. Silently does nothing when
        /// CanSkipToNextMap is false (no next map, or a wiped party) -- BattleHud greys
        /// the button out in that state, and the `N` key has no other meaning, so there's
        /// nothing to explain to the player.</summary>
        public void SkipToNextMap()
        {
            if (!CanSkipToNextMap) return;
            LogLine("Skipped the rest of this battle -- moving on to the next stage.");
            AdvanceToNextMap();
        }
    }
}
