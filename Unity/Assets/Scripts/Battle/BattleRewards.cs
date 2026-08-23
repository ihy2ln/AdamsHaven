using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>Crafting materials dropped by defeated enemies (M20). Three kinds, split
    /// by the dropper's element rather than its class -- elements only became real
    /// content in M17, and splitting on them means map 1 and map 2 drop visibly different
    /// things. Placeholder in the same sense M13's potion stock is: there's no economy,
    /// shop or crafting system for these to feed yet, and no authored drop tables.</summary>
    public enum MaterialKind { Hide, Ore, Essence }

    /// <summary>
    /// What a battle has earned so far -- EXP and materials (M20). Pure C#, no
    /// MonoBehaviour or Resources dependency, so it's testable headlessly.
    ///
    /// The reason this exists is the escape/quit split: leaving a fight has to cost
    /// something you can actually see, or "escape" and "quit" are the same button with
    /// different words on it. Escaping banks what's in here; quitting throws it away.
    /// Before M20 there was nothing in the game to bank or throw away.
    ///
    /// **This is knowingly a parallel, throwaway material model.** `Game.Data` already
    /// has the real one -- `MaterialDefinition` (tier/age/island/rarity/category, sell
    /// value, stack size) and `DropTable` (guaranteed + chance entries, unlock-gated
    /// entries, dungeon level) -- designed from FOUNDATION.md and never connected to the
    /// battle system. Using it properly means authoring `Mat_*` and `Drops_*`
    /// ScriptableObjects plus a reference from each enemy to a table, i.e. an asset build,
    /// which this environment can only do through an interactive Editor session (see
    /// PROJECT-README's "Known gaps") and which would have blocked M20 entirely.
    ///
    /// So drop values are *derived from the dropper's own stats* instead, and the three
    /// MaterialKind values are stand-ins, not a design decision competing with
    /// MaterialCategory. The point of M20 is the escape-vs-quit *choice*; it needs a
    /// stake, not an economy. When the battle/economy boundary is built, this whole class
    /// should be replaced by DropTable lookups rather than extended -- and the seam is
    /// narrow on purpose: Award() is the only thing that decides what a kill is worth.
    /// </summary>
    public class BattleRewards
    {
        /// <summary>Divisor on the derived stat total. Purely a scale knob -- it decides
        /// whether a battle is worth tens or hundreds of EXP, nothing else.</summary>
        public const int ExpDivisor = 10;

        readonly int[] _materials = new int[Enum.GetValues(typeof(MaterialKind)).Length];

        public int Exp { get; private set; }

        public int Count(MaterialKind kind) => _materials[(int)kind];

        public int TotalMaterials => _materials.Sum();

        /// <summary>Nothing banked and nothing earned -- the state a fresh battle starts
        /// in, and the thing "quitting is fine if you just started" actually means.</summary>
        public bool IsEmpty => Exp == 0 && TotalMaterials == 0;

        /// <summary>Non-zero entries only, for display. Ordered by kind so the camp
        /// screen's list doesn't reshuffle between frames.</summary>
        public IEnumerable<(MaterialKind kind, int count)> Materials =>
            Enumerable.Range(0, _materials.Length)
                .Where(i => _materials[i] > 0)
                .Select(i => ((MaterialKind)i, _materials[i]));

        /// <summary>Credits one defeated enemy's drop. Called on the killing blow, so an
        /// enemy that dies to poison on its own turn counts exactly the same as one that
        /// dies to a hit -- both paths run through BattleController's death bookkeeping.</summary>
        public void Award(BattleUnit defeated)
        {
            if (defeated == null) return;
            Exp += ExpFor(defeated);
            _materials[(int)MaterialFor(defeated)] += 1;
        }

        /// <summary>Folds `other` into this one. Used to bank a battle's pending earnings
        /// into the run total on a victory or a successful escape.</summary>
        public void Absorb(BattleRewards other)
        {
            if (other == null) return;
            Exp += other.Exp;
            for (int i = 0; i < _materials.Length; i++) _materials[i] += other._materials[i];
        }

        /// <summary>Removes up to `amount` of one material kind, returning how many were
        /// actually taken. Never goes negative -- the caller is a UI button, and a UI
        /// button that can push a ledger below zero is a bug waiting for a bad frame.</summary>
        public int Spend(MaterialKind kind, int amount)
        {
            int taken = Mathf.Clamp(amount, 0, _materials[(int)kind]);
            _materials[(int)kind] -= taken;
            return taken;
        }

        public void Clear()
        {
            Exp = 0;
            Array.Clear(_materials, 0, _materials.Length);
        }

        /// <summary>EXP for defeating `unit`, derived from how much unit it is. Attack is
        /// weighted double so a glass cannon is worth more than its HP alone suggests --
        /// otherwise Deadeye (100 HP) would pay out less than a passive wall. Floored at
        /// 1 so nothing is ever worth literally nothing to kill.</summary>
        public static int ExpFor(BattleUnit unit)
        {
            var s = unit.Stats;
            return Mathf.Max(1, (s.hp + s.attack * 2 + s.magic + s.speed) / ExpDivisor);
        }

        /// <summary>Which material `unit` drops, by its element. Neutral falls to Hide
        /// rather than dropping nothing -- a unit built before elements existed should
        /// still be worth killing.</summary>
        public static MaterialKind MaterialFor(BattleUnit unit) => unit.Definition.element switch
        {
            ElementType.Fire or ElementType.Earth => MaterialKind.Ore,
            ElementType.Lightning or ElementType.Light or ElementType.Dark => MaterialKind.Essence,
            _ => MaterialKind.Hide,
        };

        /// <summary>One-line summary for the log and the camp screen, e.g.
        /// "34 EXP, 2 Hide, 1 Ore". "nothing" when empty, so the caller never has to
        /// special-case an empty haul in its own string building.</summary>
        public string Describe()
        {
            if (IsEmpty) return "nothing";
            var parts = new List<string>();
            if (Exp > 0) parts.Add($"{Exp} EXP");
            parts.AddRange(Materials.Select(m => $"{m.count} {m.kind}"));
            return string.Join(", ", parts);
        }
    }
}
