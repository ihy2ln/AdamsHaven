using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// Boots the battle scene at runtime -- no Inspector wiring required. Mirrors
    /// FarmBootstrap's pattern exactly. Attach to an empty GameObject in Battle.unity
    /// (or let AI.Game > Battle > Create Battle Scene create it).
    ///
    /// Owns the run's DungeonRun (M24) -- the branching-path structure the project
    /// owner asked for, "just the structure" for now (placeholder visuals, placeholder
    /// content behind everything but Enemy/Elite nodes). It lives here rather than on
    /// BattleWorld because it has to survive across battles *and* camp visits, neither
    /// of which owns the other -- BattleBootstrap's own GameObject is the one thing that
    /// persists across every BootMap/BootCamp call (only its children get torn down),
    /// so an instance field here is the natural home. See RunMap.cs for the graph
    /// itself, DungeonRun.cs for position-tracking, and EnterNode below for how a
    /// chosen node turns into an actual boot call.
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
        DungeonRun _run;

        void Start() => Boot();

        /// <summary>Parameterless entry point for Start()/the context menu/"Leave
        /// dungeon" -- generates a fresh DungeonRun and auto-enters its first floor-0
        /// node. Auto-picking rather than offering a choice here is a deliberate
        /// simplification (M24): a brand-new run has no BattleWorld yet to source a
        /// camp screen's party/bench/inventory from, and building one just to show a
        /// single-battle choice isn't worth it yet -- the real branching choices start
        /// showing at camp after this first fight ends. Revisit if "choose your very
        /// first node too" turns out to matter.</summary>
        [ContextMenu("Boot Battle")]
        public void Boot()
        {
            _run = new DungeonRun(RunMapGenerator.Generate(Random.Range(int.MinValue, int.MaxValue)));
            var start = _run.AvailableNextNodes();
            // Prefer an Enemy node among floor 0's options over an inert placeholder
            // one (floor 0 can roll Unknown too -- see RunMapGenerator.FirstFloorWeights)
            // -- a first launch should always drop the player into an actual fight, the
            // way every version of this project has before M24, not sometimes land them
            // on a "nothing here yet" camp screen with nothing to compare it against.
            var first = start.FirstOrDefault(n => n.Type == RunNodeType.Enemy) ?? start[0];
            EnterNode(first, null, null, null, null);
        }

        public void BootMap(int mapIndex, IReadOnlyList<BattleUnit> carryOverPlayer, IReadOnlyList<BattleUnit> carryOverBench,
            BattleInventory carryOverInventory, BattleRewards carryOverRewards = null)
        {
            ClearChildren();

            var camGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            // Deliberately not `?? camGo.AddComponent<Camera>()` -- Unity 6's component
            // binding can return a non-CLR-null wrapper for "no such component", which
            // makes `??` skip AddComponent entirely and leaves `cam` pointing at nothing
            // (confirmed via a real Editor Play-mode crash: MissingComponentException in
            // BattleLayout.ApplyBattleCamera's `cam.orthographic = true`, with the
            // Inspector showing Main Camera had no Camera component at all). Explicit
            // `== null` uses Unity's overloaded equality and behaves correctly.
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";
            BattleLayout.ApplyBattleCamera(cam);

            var settings = BattleSettings.Load();
            AudioListener.volume = settings.MasterVolume;

            var world = new BattleWorld(mapIndex, carryOverPlayer, carryOverBench, carryOverInventory, carryOverRewards);

            var visualsGo = new GameObject("BattleVisuals");
            visualsGo.transform.SetParent(transform, false);
            var visuals = visualsGo.AddComponent<BattleVisuals>();
            visuals.Build(world);

            var ctrlGo = new GameObject("BattleController");
            ctrlGo.transform.SetParent(transform, false);
            var ctrl = ctrlGo.AddComponent<BattleController>();
            // M18's dev skip (`N` / pause menu "Skip to Next Battle") stays wired to the
            // flat mapIndex+1 sequence rather than the dungeon run -- it's an explicit
            // testing shortcut for jumping between the two built MapDefinitions, not a
            // move in the game, and was never meant to understand branching. Using it
            // does leave `_run`'s position stale relative to whatever content actually
            // loaded; that's an accepted, documented gap in a dev-only tool, not a bug.
            ctrl.OnRestartRequested += () => BootMap(0, null, null, null);
            ctrl.OnAdvanceRequested += () => BootMap(mapIndex + 1, world.PlayerUnits.ToList(), world.Bench.ToList(),
                world.Inventory, world.Banked);
            // Escaping, quitting, or (M23) choosing Camp after a win all land here.
            // BootCamp shows whatever DungeonRun.AvailableNextNodes() offers right now --
            // it doesn't need to know why the player left, only what they're carrying.
            ctrl.OnLeaveRequested += () => BootCamp(world, ctrl.Outcome);
            // Retry Battle (M23) re-boots this same mapIndex/content -- not a node move,
            // just a fresh attempt at what's already loaded, so DungeonRun's position is
            // untouched. The party was already reset to entry state by
            // BattleController.RetryBattle() before this fires.
            ctrl.OnRetryRequested += () => BootMap(mapIndex, world.PlayerUnits.ToList(), world.Bench.ToList(),
                world.Inventory, world.Banked);
            ctrl.Init(world, visuals, cam, settings);

            var hudGo = new GameObject("BattleHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<BattleHud>().Init(ctrl, cam, visuals, settings.LogOpenByDefault);

            Debug.Log(world.LoadedOk
                ? $"[AI.Game] Battle booted (map {mapIndex + 1}/{BattleWorld.MapCount})."
                : "[AI.Game] Battle boot failed to load data -- run AI.Game > Battle > Build Assets From Manifest.");
        }

        /// <summary>Which built MapDefinition a node's content maps to (M24). The
        /// single biggest placeholder in this whole feature: every Enemy node in the
        /// entire run reuses map 1's roster (Husk/Warden/Stinger) and every Elite node
        /// reuses map 2's (Rotfang/Deadeye/Hexweaver, the tougher one) -- there's no
        /// per-node content yet, so the same two fights repeat as the party climbs. That
        /// was the explicit trade the project owner asked for this session ("just work
        /// on the structure, we will be replacing the map look and icons") -- swap this
        /// for real per-node content whenever it exists; every other piece of this
        /// system (the graph, the traversal, the camp picker) is already built to not
        /// care how many distinct battles there actually are behind it.</summary>
        static int MapIndexForNode(RunMapNode node) => node.Type == RunNodeType.Elite ? 1 : 0;

        /// <summary>Commits to `node`: advances DungeonRun's position, then boots
        /// whatever that node type implies. Enemy/Elite boot a real battle. Rest and
        /// Treasure (M25) have real function too -- Rest opens camp's own per-unit Rest
        /// checklist directly, Treasure grants a potion. Unknown and Merchant still have
        /// nothing behind them (no event table, no shop) and just return to camp having
        /// moved there, so the graph traversal is fully exercised even for the two node
        /// types that don't do anything yet.
        ///
        /// `node` must be one of `_run.AvailableNextNodes()` -- both the initial
        /// Boot() call and CampScreen's picker only ever offer nodes from that list, so
        /// DungeonRun.MoveTo failing here would mean a caller passed something it
        /// shouldn't have; logged rather than silently ignored so that bug wouldn't go
        /// unnoticed.</summary>
        void EnterNode(RunMapNode node, IReadOnlyList<BattleUnit> party, IReadOnlyList<BattleUnit> bench,
            BattleInventory inventory, BattleRewards rewards)
        {
            if (!_run.MoveTo(node.Id))
            {
                Debug.LogError($"[AI.Game] Tried to enter node {node.Id} ({node.Type}) but DungeonRun says it "
                    + "isn't reachable from the current position -- this should never happen from the UI.");
                return;
            }

            var partyList = party?.ToList() ?? new List<BattleUnit>();
            var benchList = bench?.ToList() ?? new List<BattleUnit>();

            switch (node.Type)
            {
                case RunNodeType.Enemy:
                case RunNodeType.Elite:
                    BootMap(MapIndexForNode(node), party, bench, inventory, rewards);
                    break;
                case RunNodeType.Rest:
                    ShowCamp(partyList, benchList, inventory, rewards,
                        "A quiet spot to recover. Rest is open below.", openRestPanel: true);
                    break;
                case RunNodeType.Treasure:
                    ShowCamp(partyList, benchList, inventory, rewards, GrantTreasure(inventory));
                    break;
                default: // Unknown, Merchant -- still no content behind them.
                    ShowCamp(partyList, benchList, inventory, rewards,
                        $"There's nothing here yet -- {node.Type} nodes aren't implemented. Made camp instead.");
                    break;
            }
        }

        /// <summary>A Treasure node's payoff (M25): one potion, kind picked at random
        /// among the three that exist, added via BattleInventory.Grant. The random pick
        /// itself is the one piece of this that's deliberately NOT pure/testable --
        /// UnityEngine.Random can't run headlessly, so it stays here on the
        /// MonoBehaviour side, same split DamageCalculator's crit roll and
        /// EscapeCalculator's flee roll already use; Grant's own clamping/bookkeeping is
        /// the pure part, and that's what BattleInventoryTests actually covers.</summary>
        static string GrantTreasure(BattleInventory inventory)
        {
            if (inventory == null) return "Found a chest, but had nowhere to put what was inside.";

            var kinds = new[] { PotionKind.Hp, PotionKind.Mp, PotionKind.Multi };
            var kind = kinds[Random.Range(0, kinds.Length)];
            var slot = inventory.Slot(kind);
            int gained = inventory.Grant(kind, 1);

            if (gained > 0) return $"Found a chest: +{gained} {slot.Potion.displayName}. ({slot.Count} carried now.)";
            return slot?.Potion == null
                ? "Found a chest, but couldn't tell what was inside -- run Build Assets From Manifest."
                : $"Found a chest, but you're already carrying the max {slot.Potion.displayName} ({slot.Count}).";
        }

        /// <summary>Tears the battle down and stands up the camp screen in its place
        /// (M20; shows the dungeon map since M24). Same scene, different contents --
        /// BootMap already worked this way, so camp doesn't need a scene of its own (and
        /// Battle.unity isn't in the build settings, so a second scene would need
        /// wiring that doesn't exist yet).</summary>
        public void BootCamp(BattleWorld world, BattleOutcome outcome)
        {
            // Three arrival lines, one per way of reaching camp (M20 escape/quit, M23
            // victory) -- each reads honestly about what actually happened, since this is
            // the player's first look at the outcome after the banner.
            string arrival = outcome switch
            {
                BattleOutcome.Escaped => "You broke off the fight and made it back to camp, carrying what you'd taken.",
                BattleOutcome.PlayerVictory => "Battle won. You made camp before pressing on.",
                _ => "You walked away before it was worth anything. Nothing gained, nothing spent.",
            };
            ShowCamp(world.PlayerUnits.ToList(), world.Bench.ToList(), world.Inventory, world.Banked, arrival);
        }

        /// <summary>Shared by BootCamp and every EnterNode branch that doesn't boot a
        /// battle -- they all end up at the same screen with the same wiring, just with
        /// different arrival text, party/bench lists that were already extracted rather
        /// than pulled fresh from a BattleWorld, and (M25) whether a Rest node wants the
        /// Rest checklist opened immediately.</summary>
        void ShowCamp(List<BattleUnit> party, List<BattleUnit> bench, BattleInventory inventory,
            BattleRewards rewards, string arrival, bool openRestPanel = false)
        {
            ClearChildren();

            var campGo = new GameObject("CampScreen");
            campGo.transform.SetParent(transform, false);
            var camp = campGo.AddComponent<CampScreen>();
            camp.Init(_run, party, bench, inventory, rewards, arrival, openRestPanel);
            camp.OnNodeChosen += node => EnterNode(node, party, bench, inventory, rewards);
            camp.OnLeaveDungeonRequested += () =>
            {
                // No home scene is wired to the battle slice yet -- Farm.unity exists and
                // is the only scene in the build settings, but nothing connects the two
                // (see PROJECT-README's "Known gaps"). Until that boundary is built,
                // going home means a fresh dungeon run, via Boot() -- same placeholder
                // this button had before M24, just now also regenerating the map.
                Debug.Log("[AI.Game] Left the dungeon -- no home scene wired yet, starting a fresh run.");
                Boot();
            };

            Debug.Log($"[AI.Game] Camp booted -- {_run.AvailableNextNodes().Count} node(s) available next. \"{arrival}\"");
        }

        void ClearChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }
    }
}
