using System;
using Game.Data;

namespace Game.Battle
{
    /// <summary>One of BattleInventory's 3 fixed slots -- Potion is which item occupies
    /// it (or null if the slot has never been stocked), Count is how many, clamped to
    /// Potion.maxStack (99 by default) by whoever adds to it.</summary>
    public class BattleInventorySlot
    {
        public PotionDefinition Potion;
        public int Count;

        public bool IsUsable => Potion != null && Count > 0;
    }

    /// <summary>
    /// Exactly 3 battle-carried potion slots -- one HP, one MP, one Multi (restores
    /// both) -- per the project owner's design (M13). Not a general inventory system:
    /// there's no economy/shop/farm integration yet to source these from, so BattleWorld
    /// seeds a placeholder stock on a fresh (non-carried-over) roll. Carries over between
    /// this slice's 2 maps the same way the roster's HP/bench does -- see BattleWorld's
    /// constructor.
    /// </summary>
    public class BattleInventory
    {
        public readonly BattleInventorySlot Hp = new();
        public readonly BattleInventorySlot Mp = new();
        public readonly BattleInventorySlot Multi = new();

        public BattleInventorySlot Slot(PotionKind kind) => kind switch
        {
            PotionKind.Hp => Hp,
            PotionKind.Mp => Mp,
            PotionKind.Multi => Multi,
            _ => null,
        };

        public bool HasAnyUsable => Hp.IsUsable || Mp.IsUsable || Multi.IsUsable;

        /// <summary>Adds `amount` of `kind` to its slot, clamped to that potion's own
        /// `maxStack` (M25 -- what a Treasure-node pickup calls). Deliberately pure and
        /// Unity-independent (System.Math, not Mathf) so it's testable without a scene.
        /// Returns how many were actually added, which can be less than `amount` (or 0)
        /// if the stack was already near/at cap, or 0 outright if the slot's own
        /// `Potion` reference was never set (missing built content) -- either way the
        /// caller can report an honest "found nothing new" instead of claiming a pickup
        /// that didn't happen.</summary>
        public int Grant(PotionKind kind, int amount)
        {
            var slot = Slot(kind);
            if (slot?.Potion == null || amount <= 0) return 0;
            int before = slot.Count;
            slot.Count = Math.Min(slot.Count + amount, slot.Potion.maxStack);
            return slot.Count - before;
        }
    }
}
