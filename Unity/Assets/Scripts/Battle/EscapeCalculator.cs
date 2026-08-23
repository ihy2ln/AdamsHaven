using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Battle
{
    /// <summary>
    /// Pure C#, no MonoBehaviour/scene dependency -- testable headlessly, same split
    /// DamageCalculator established: the math lives here, the actual UnityEngine.Random
    /// roll it feeds lives on BattleController.
    ///
    /// Standard JRPG escape: a flat base chance shifted by how much faster your side is
    /// than theirs, plus an escalating bonus per failed attempt. The escalation is the
    /// part that matters most -- without it a slow party facing a fast enemy can be
    /// locked into a fight it can't win and can't leave, burning turns on a roll that
    /// never improves. With it, escape is guaranteed-ish within a few tries, so fleeing
    /// costs turns rather than being a coin flip you can lose forever.
    ///
    /// Numbers are arbitrary in this project's usual sense (coherent, not derived from a
    /// model) -- see PROJECT-README's balance notes.
    /// </summary>
    public static class EscapeCalculator
    {
        /// <summary>Chance with evenly-matched speed and no prior failures.</summary>
        public const float BaseChance = 0.5f;

        /// <summary>How far a speed advantage can shift the base, at most. A side twice
        /// as fast as its opponent gets the full swing; the ratio is clamped so a wildly
        /// lopsided matchup can't blow past it.</summary>
        public const float SpeedSwing = 0.25f;

        /// <summary>Added per previous failed attempt this battle. Four failures alone
        /// clear MaxChance from the base, which is the intended feel: repeated attempts
        /// always work eventually.</summary>
        public const float PerFailedAttemptBonus = 0.15f;

        /// <summary>A guard, not a working floor. With the constants as they stand the
        /// speed term can only ever subtract SpeedSwing/2, so the true worst case is
        /// BaseChance - 0.125 = 37.5% and this never binds -- it exists so that widening
        /// SpeedSwing later can't accidentally produce a 0% chance, which would make the
        /// Flee button a lie. EscapeTests pins the real worst case separately.</summary>
        public const float MinChance = 0.1f;

        /// <summary>Never a certainty on the first try, however fast you are. Escaping
        /// is meant to cost something.</summary>
        public const float MaxChance = 0.95f;

        /// <summary>Chance (0-1) that `fleeing` gets away from `opposing` right now.
        /// Only living units count on both sides -- a party fleeing three corpses should
        /// read as fast, and a lone survivor shouldn't be dragged down by the average of
        /// allies who are already dead.
        ///
        /// Either side being empty falls back to the base chance rather than dividing by
        /// zero. That state shouldn't be reachable in a real battle (the battle would be
        /// over), but this is a pure function and callers shouldn't have to know that.</summary>
        public static float EscapeChance(
            IEnumerable<BattleUnit> fleeing, IEnumerable<BattleUnit> opposing, int failedAttempts)
        {
            float fleeSpeed = AverageSpeed(fleeing);
            float enemySpeed = AverageSpeed(opposing);

            float chance = BaseChance + failedAttempts * PerFailedAttemptBonus;

            if (fleeSpeed > 0f && enemySpeed > 0f)
            {
                // Ratio rather than difference: "twice as fast" should mean the same
                // thing at speed 5 vs 10 as at 50 vs 100, and this slice's speeds are
                // small enough (8-13) that a raw difference would barely register.
                float ratio = Mathf.Clamp(fleeSpeed / enemySpeed, 0.5f, 2f);
                chance += (ratio - 1f) * SpeedSwing;
            }

            return Mathf.Clamp(chance, MinChance, MaxChance);
        }

        static float AverageSpeed(IEnumerable<BattleUnit> units)
        {
            var alive = units?.Where(u => u != null && u.IsAlive).ToList();
            if (alive == null || alive.Count == 0) return 0f;
            return (float)alive.Average(u => u.Stats.speed);
        }
    }
}
