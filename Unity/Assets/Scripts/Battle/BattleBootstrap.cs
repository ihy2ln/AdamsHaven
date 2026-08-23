using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Battle
{
    /// <summary>
    /// Boots the battle scene at runtime -- no Inspector wiring required. Mirrors
    /// FarmBootstrap's pattern exactly. Attach to an empty GameObject in Battle.unity
    /// (or let AI.Game > Battle > Create Battle Scene create it).
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
        void Start() => Boot();

        /// <summary>Parameterless entry point for Start()/the context menu -- always
        /// map 0, fresh roster. The two-map sequence and full-battle restart both funnel
        /// through BootMap below.</summary>
        [ContextMenu("Boot Battle")]
        public void Boot() => BootMap(0, null, null, null);

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
            ctrl.OnRestartRequested += () => BootMap(0, null, null, null);
            ctrl.OnAdvanceRequested += () => BootMap(mapIndex + 1, world.PlayerUnits.ToList(), world.Bench.ToList(),
                world.Inventory, world.Banked);
            // Escaping or quitting (M20) goes to camp rather than to another battle. The
            // map index is deliberately NOT advanced -- withdrawing from a fight doesn't
            // clear it, so camp offers the same battle again.
            ctrl.OnLeaveRequested += () => BootCamp(mapIndex, world, ctrl.Outcome);
            ctrl.Init(world, visuals, cam, settings);

            var hudGo = new GameObject("BattleHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<BattleHud>().Init(ctrl, cam, visuals, settings.LogOpenByDefault);

            Debug.Log(world.LoadedOk
                ? $"[AI.Game] Battle booted (map {mapIndex + 1}/{BattleWorld.MapCount})."
                : "[AI.Game] Battle boot failed to load data -- run AI.Game > Battle > Build Assets From Manifest.");
        }

        /// <summary>Tears the battle down and stands up the camp screen in its place
        /// (M20). Same scene, different contents -- BootMap already worked this way, so
        /// camp doesn't need a scene of its own (and Battle.unity isn't in the build
        /// settings, so a second scene would need wiring that doesn't exist yet).
        ///
        /// `mapIndex` is the battle the party just walked out of, not the next one:
        /// leaving a fight doesn't clear it, so camp's "take on battle N" re-enters the
        /// same map with the party restored to its entry state.</summary>
        public void BootCamp(int mapIndex, BattleWorld world, BattleOutcome outcome)
        {
            ClearChildren();

            var party = world.PlayerUnits.ToList();
            var bench = world.Bench.ToList();

            string arrival = outcome == BattleOutcome.Escaped
                ? $"You broke off the fight and made it back to camp, carrying what you'd taken."
                : "You walked away before it was worth anything. Nothing gained, nothing spent.";

            var campGo = new GameObject("CampScreen");
            campGo.transform.SetParent(transform, false);
            var camp = campGo.AddComponent<CampScreen>();
            camp.Init(mapIndex, party, bench, world.Inventory, world.Banked, arrival);
            camp.OnContinueRequested += () => BootMap(mapIndex, party, bench, world.Inventory, world.Banked);
            camp.OnLeaveDungeonRequested += () =>
            {
                // No home scene is wired to the battle slice yet -- Farm.unity exists and
                // is the only scene in the build settings, but nothing connects the two
                // (see PROJECT-README's "Known gaps"). Until that boundary is built,
                // going home means starting over.
                Debug.Log("[AI.Game] Left the dungeon -- no home scene wired yet, restarting the run.");
                BootMap(0, null, null, null);
            };

            Debug.Log($"[AI.Game] Camp booted after {outcome} (next battle {mapIndex + 1}/{BattleWorld.MapCount}).");
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
