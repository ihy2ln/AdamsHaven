using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// Loads the battle-slice ScriptableObjects (built by BattleAssetBuilder into
    /// Resources/Battle/) and rolls the 6 units for one battle. Plain C# like
    /// FarmWorld -- no scene dependency.
    /// </summary>
    public class BattleWorld
    {
        public const int MapCount = 2;

        public int MapIndex { get; }
        public MapDefinition Map { get; }
        public List<BattleUnit> AllUnits { get; } = new();

        /// <summary>Player reserves not currently in the fight -- sub-in/sub-out swaps
        /// a unit here with one in AllUnits. Column is the -1 off-field sentinel until
        /// subbed in. Enemy side has no bench in this slice.</summary>
        public List<BattleUnit> Bench { get; } = new();

        /// <summary>The player's 3 battle-carried potion slots (M13). Owned here rather
        /// than on BattleController so it carries over between maps 1 and 2 the same way
        /// AllUnits/Bench do -- see the constructor's carryOverInventory parameter.</summary>
        public BattleInventory Inventory { get; }

        public IEnumerable<BattleUnit> PlayerUnits => AllUnits.Where(u => u.Faction == Faction.Player);
        public IEnumerable<BattleUnit> EnemyUnits => AllUnits.Where(u => u.Faction == Faction.Enemy);

        public bool PlayerDefeated => PlayerUnits.Any() && PlayerUnits.All(u => !u.IsAlive);
        public bool EnemyDefeated => EnemyUnits.Any() && EnemyUnits.All(u => !u.IsAlive);
        public bool IsOver => PlayerDefeated || EnemyDefeated;
        public bool HasNextMap => MapIndex < MapCount - 1;

        public bool LoadedOk { get; }

        public const int BenchColumn = -1;

        // Mirrors the enemy formation built by BattleAssetBuilder: front-line melee
        // adjacent to the enemy's front line (column 2 vs 3), support/ranged behind.
        static readonly (string unitId, int column)[] PlayerFormation =
        {
            ("player_ranged", 0),
            ("player_support", 1),
            ("player_melee", 2),
        };

        static readonly string[] BenchRoster = { "player_bench_melee", "player_bench_ranged", "player_bench_support" };

        /// <param name="mapIndex">0-based; picks Map_BattleSlice{mapIndex+1}.</param>
        /// <param name="carryOverPlayer">If supplied, these exact BattleUnit instances (with
        /// their current HP/MP) are reused for the player side instead of rolling fresh ones
        /// -- lets a party carry wounds from a previous map into the next.</param>
        /// <param name="carryOverBench">Same, for the bench.</param>
        /// <param name="carryOverInventory">Same, for the potion slots -- if supplied,
        /// reused as-is (remaining counts and all); if null, freshly seeded with
        /// placeholder starting stock (see SeedPlaceholderInventory).</param>
        public BattleWorld(int mapIndex = 0, IReadOnlyList<BattleUnit> carryOverPlayer = null,
            IReadOnlyList<BattleUnit> carryOverBench = null, BattleInventory carryOverInventory = null)
        {
            MapIndex = Mathf.Clamp(mapIndex, 0, MapCount - 1);
            Map = Resources.Load<MapDefinition>($"Battle/Maps/Map_BattleSlice{MapIndex + 1}");
            var tier = Resources.Load<TierDefinition>("Battle/Tiers/Tier_Standard");

            if (Map == null || tier == null)
            {
                Debug.LogError("[AI.Game] Battle data assets missing -- run "
                    + "AI.Game > Battle > Build Assets From Manifest first.");
                LoadedOk = false;
                return;
            }

            int seed = 0;
            var noRollPool = new List<SkillDefinition>(); // only standardSkill/skillMoves are used in this slice

            if (carryOverPlayer != null)
            {
                // Reuse the same BattleUnit objects (preserves CurrentHp/Mp) -- drop the
                // dead, reassign columns 0..2 in prior front-to-back order. HP does NOT
                // recover here (the carried wound is the point of the 2-map sequence) but
                // MP gets a partial "caught our breath" top-up -- see BattleUnit
                // .RecoverMpAfterBattle.
                var survivors = carryOverPlayer.Where(u => u.IsAlive).OrderBy(u => u.Column).ToList();
                foreach (var unit in survivors) unit.RecoverMpAfterBattle();
                for (int i = 0; i < survivors.Count; i++) survivors[i].Column = i;
                AllUnits.AddRange(survivors);
            }
            else
            {
                foreach (var (unitId, column) in PlayerFormation)
                {
                    var def = Resources.Load<CharacterDefinition>($"Battle/Characters/Char_{unitId}");
                    if (def == null)
                    {
                        Debug.LogWarning($"[AI.Game] Missing CharacterDefinition for {unitId}");
                        continue;
                    }
                    var instance = CharacterFactory.Create(def, tier, noRollPool, seed++);
                    AllUnits.Add(new BattleUnit(def, instance, Faction.Player, column, facingRight: true));
                }
            }

            if (carryOverBench != null)
            {
                foreach (var unit in carryOverBench) unit.RecoverMpAfterBattle();
                Bench.AddRange(carryOverBench);
            }
            else
            {
                foreach (var unitId in BenchRoster)
                {
                    var def = Resources.Load<CharacterDefinition>($"Battle/Characters/Char_{unitId}");
                    if (def == null)
                    {
                        Debug.LogWarning($"[AI.Game] Missing CharacterDefinition for {unitId}");
                        continue;
                    }
                    var instance = CharacterFactory.Create(def, tier, noRollPool, seed++);
                    Bench.Add(new BattleUnit(def, instance, Faction.Player, BenchColumn, facingRight: true));
                }
            }

            foreach (var placement in Map.enemies)
            {
                if (placement.character == null) continue;
                var placementTier = placement.tier != null ? placement.tier : tier;
                var instance = CharacterFactory.Create(placement.character, placementTier, noRollPool, seed++);
                AllUnits.Add(new BattleUnit(placement.character, instance, Faction.Enemy, placement.position.y, facingRight: false));
            }

            LoadedOk = AllUnits.Count > 0;
            WarnOnStaleContent();

            Inventory = carryOverInventory ?? SeedPlaceholderInventory();
        }

        /// <summary>No economy/shop/farm system exists yet to source real starting stock
        /// from (see PROJECT-README's Known gaps), so a fresh (non-carried-over) battle
        /// seeds a placeholder 5 of each C-rank potion -- enough to actually exercise the
        /// Item action without asset-authoring friction, not a real drop-rate/economy
        /// decision. Loads the same 3 potion assets BattleAssetBuilder builds
        /// (Battle/Potions/Potion_Hp|Mp|Multi); if they're missing (stale content),
        /// leaves that slot empty rather than crashing -- same "missing art is not an
        /// error" convention BattleAssetBuilder itself follows.</summary>
        static BattleInventory SeedPlaceholderInventory()
        {
            const int StartingCount = 5;
            var inventory = new BattleInventory();
            Seed(inventory.Hp, "Battle/Potions/Potion_Hp", StartingCount);
            Seed(inventory.Mp, "Battle/Potions/Potion_Mp", StartingCount);
            Seed(inventory.Multi, "Battle/Potions/Potion_Multi", StartingCount);
            return inventory;

            static void Seed(BattleInventorySlot slot, string path, int count)
            {
                var potion = Resources.Load<PotionDefinition>(path);
                if (potion == null)
                {
                    Debug.LogWarning($"[AI.Game] Missing PotionDefinition at Resources/{path} -- "
                        + "run AI.Game > Battle > Build Assets From Manifest.");
                    return;
                }
                slot.Potion = potion;
                slot.Count = Mathf.Min(count, potion.maxStack);
            }
        }

        /// <summary>Surfaces the one failure mode that otherwise looks like a UI bug: a
        /// CharacterDefinition built before the Skill Move system existed has an empty
        /// skillMoves list, so manual mode's "SM" button greys out and auto mode silently
        /// falls back to the free 0-cost BA forever (MP never drains). In the Editor
        /// BattleContentGuard rebuilds these automatically; in a standalone build the
        /// assets are already baked, so a loud log line is all we can do.</summary>
        void WarnOnStaleContent()
        {
            var stale = AllUnits.Concat(Bench)
                .Select(u => u.Definition)
                .Distinct()
                .Where(d => d.skillMoves == null || !d.skillMoves.Any(s => s != null))
                .Select(d => d.displayName)
                .ToList();

            if (stale.Count == 0) return;
            Debug.LogError($"[AI.Game] {stale.Count} character(s) have no Skill Moves: {string.Join(", ", stale)}. "
                + "Their SM button will be greyed out and they'll never spend MP. Run "
                + "AI.Game > Battle > Build Assets From Manifest from an interactive Editor.");
        }
    }
}
