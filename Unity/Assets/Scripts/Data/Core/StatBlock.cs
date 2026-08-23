using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// Combat stats that roll with variance at acquisition.
    /// Movement stats (movePoints, jump) deliberately live on CharacterDefinition
    /// and do NOT roll — random movement range would make tactical planning unreadable.
    /// </summary>
    [Serializable]
    public struct StatBlock
    {
        public int hp;
        public int attack;
        public int defense;
        public int magic;
        public int resistance;
        public int speed;

        [Tooltip("Chance (0-1) to land a critical hit. 0 (the type default) means this "
            + "unit never crits -- safe for any content authored before M16.")]
        public float critRate;

        [Tooltip("Total damage multiplier on a crit (e.g. 1.5 = +50%). Only read when "
            + "critRate actually triggers a crit, so a 0 default is harmless.")]
        public float critDamage;

        [Tooltip("Chance (0-1) an attack lands, before the target's evasion is "
            + "subtracted. 0 (the type default) is treated as \"always hits\" by "
            + "DamageCalculator.HitChance -- content authored before M16 never misses.")]
        public float accuracy;

        [Tooltip("Chance (0-1) subtracted from the attacker's accuracy. 0 default is "
            + "harmless (no dodge bonus).")]
        public float evasion;

        public static StatBlock operator *(StatBlock s, float m) => new StatBlock
        {
            hp         = Mathf.RoundToInt(s.hp * m),
            attack     = Mathf.RoundToInt(s.attack * m),
            defense    = Mathf.RoundToInt(s.defense * m),
            magic      = Mathf.RoundToInt(s.magic * m),
            resistance = Mathf.RoundToInt(s.resistance * m),
            speed      = Mathf.RoundToInt(s.speed * m),
            critRate   = s.critRate,
            critDamage = s.critDamage,
            accuracy   = s.accuracy,
            evasion    = s.evasion
        };

        public static StatBlock operator +(StatBlock a, StatBlock b) => new StatBlock
        {
            hp         = a.hp + b.hp,
            attack     = a.attack + b.attack,
            defense    = a.defense + b.defense,
            magic      = a.magic + b.magic,
            resistance = a.resistance + b.resistance,
            speed      = a.speed + b.speed,
            critRate   = a.critRate + b.critRate,
            critDamage = a.critDamage > 0f ? a.critDamage : b.critDamage,
            accuracy   = a.accuracy + b.accuracy,
            evasion    = a.evasion + b.evasion
        };

        /// <summary>Independent random roll per stat. Caller supplies a seeded RNG.
        /// Crit/accuracy fields are deliberately left untouched by variance -- they're
        /// percentages, not scaling quantities like HP/attack, and a tier/level roll
        /// pushing crit rate above 100% or below 0% would need its own clamping logic
        /// this method doesn't have.</summary>
        public StatBlock RollVariance(System.Random rng, float variancePct)
        {
            if (variancePct <= 0f) return this;
            float Roll() => 1f + ((float)rng.NextDouble() * 2f - 1f) * variancePct;
            return new StatBlock
            {
                hp         = Mathf.RoundToInt(hp * Roll()),
                attack     = Mathf.RoundToInt(attack * Roll()),
                defense    = Mathf.RoundToInt(defense * Roll()),
                magic      = Mathf.RoundToInt(magic * Roll()),
                resistance = Mathf.RoundToInt(resistance * Roll()),
                speed      = Mathf.RoundToInt(speed * Roll()),
                critRate   = critRate,
                critDamage = critDamage,
                accuracy   = accuracy,
                evasion    = evasion
            };
        }
    }
}
