using System.Collections.Generic;
using UnityEngine;

namespace Game.Town
{
    /// <summary>Money + per-kind materials, session-only for now (resets every time
    /// Town loads -- Farm has its own real save file via FarmSaveRepository, Town
    /// doesn't yet; a natural next step, not built here). Seeded with a placeholder
    /// starting stock rather than wired to Battle's real BattleRewards, since that
    /// needs a cross-scene save/session layer this project doesn't have yet (see
    /// PROJECT-README's Known Gaps) -- FOUNDATION.md's own resource table says Battle
    /// stages fund Town building materials, so this is standing in for that until the
    /// real pipe exists, not a competing design.</summary>
    public class TownEconomy
    {
        public const int StartingMoney = 300;
        static readonly Dictionary<TownMaterialKind, int> StartingMaterials = new()
        {
            { TownMaterialKind.Hide, 10 },
            { TownMaterialKind.Ore, 10 },
            { TownMaterialKind.Essence, 5 },
        };

        public int Money { get; private set; } = StartingMoney;
        readonly Dictionary<TownMaterialKind, int> _materials = new(StartingMaterials);

        public int Material(TownMaterialKind kind) => _materials.TryGetValue(kind, out var n) ? n : 0;

        public bool CanAfford(int money, IEnumerable<(TownMaterialKind kind, int amount)> materials)
        {
            if (Money < money) return false;
            foreach (var (kind, amount) in materials)
                if (Material(kind) < amount) return false;
            return true;
        }

        public bool TrySpend(int money, IEnumerable<(TownMaterialKind kind, int amount)> materials)
        {
            var list = new List<(TownMaterialKind, int)>(materials);
            if (!CanAfford(money, list)) return false;
            Money -= money;
            foreach (var (kind, amount) in list)
                _materials[kind] = Material(kind) - amount;
            return true;
        }

        public void Grant(int money, TownMaterialKind kind, int amount)
        {
            Money += money;
            _materials[kind] = Material(kind) + amount;
        }

        /// <summary>Test-only top-up so the loop is playable without a real
        /// Battle-&gt;Town material pipe yet -- same spirit as Farm's own "T test
        /// battle" key.</summary>
        public void GrantTestStock()
        {
            Money += 200;
            foreach (var kind in (TownMaterialKind[])System.Enum.GetValues(typeof(TownMaterialKind)))
                _materials[kind] = Material(kind) + 10;
        }

        public string Describe() =>
            $"${Money}  ·  Hide {Material(TownMaterialKind.Hide)}  ·  Ore {Material(TownMaterialKind.Ore)}  ·  Essence {Material(TownMaterialKind.Essence)}";
    }
}
