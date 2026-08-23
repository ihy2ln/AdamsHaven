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

        /// <summary>Copy of this StatBlock with the 4 combat-roll fields replaced from a
        /// named CombatStats profile (M17). Exists so the authored numbers live in one
        /// readable table (CombatStats' profiles) rather than being repeated inline on
        /// every StatBlock literal in BattleAssetBuilder.</summary>
        public StatBlock WithCombatStats(CombatStats profile)
        {
            var copy = this;
            copy.critRate = profile.CritRate;
            copy.critDamage = profile.CritDamage;
            copy.accuracy = profile.Accuracy;
            copy.evasion = profile.Evasion;
            return copy;
        }

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

    /// <summary>
    /// M17: the authored crit/accuracy/evasion numbers, as five named profiles rather
    /// than per-character literals. Before this, every StatBlock in the game shipped with
    /// all four fields at 0 and BattleWorld.RandomizeTestCombatStats sprayed a random
    /// roll over them at battle start purely so M16's crit/miss systems would be visible
    /// at all -- that testing aid is gone as of M17 and these are the real values.
    ///
    /// The shape of the pass: accuracy sits high (0.92-0.98) and evasion low (0.02-0.10)
    /// so the worst matchup in the game -- Rotfang's 0.92 into Deadeye's 0.10 -- still
    /// lands ~82% of the time. A turn-based game where a fifth of your turns evaporate
    /// reads as broken, not tactical, so misses are a rare punctuation rather than a
    /// real resource cost. Crit rate is where the archetypes actually differ (0.08 on a
    /// caster up to 0.25 on the sniper), with crit damage moving inversely -- the brute
    /// crits least often and hardest.
    ///
    /// Arbitrary in the sense this project's convention means: coherent and playtested-
    /// by-eye, not derived from a damage-per-turn model. A real tuning pass would want
    /// to see these against actual battle-length data.
    /// </summary>
    [Serializable]
    public struct CombatStats
    {
        public readonly float CritRate;
        public readonly float CritDamage;
        public readonly float Accuracy;
        public readonly float Evasion;

        public CombatStats(float critRate, float critDamage, float accuracy, float evasion)
        {
            CritRate = critRate;
            CritDamage = critDamage;
            Accuracy = accuracy;
            Evasion = evasion;
        }

        /// <summary>Melee archetype (Kestrel/Thorne, and map 1's Husk reskin). Durable
        /// front-liner: rarely crits, hits hard when it does, doesn't dodge.</summary>
        public static readonly CombatStats Bruiser = new CombatStats(0.10f, 1.75f, 0.94f, 0.03f);

        /// <summary>Ranged archetype (Sable/Reed, map 1's Stinger). The party's crit
        /// carry, and the only player unit with meaningful evasion.</summary>
        public static readonly CombatStats Skirmisher = new CombatStats(0.22f, 1.60f, 0.97f, 0.09f);

        /// <summary>Support archetype (Linnet/Vesper, map 1's Warden) and map 2's
        /// Hexweaver. Accurate but low-crit -- most of its output is heals, which
        /// DamageCalculator.ComputeHeal doesn't crit on anyway.</summary>
        public static readonly CombatStats Caster = new CombatStats(0.08f, 1.50f, 0.95f, 0.05f);

        /// <summary>Map 2's Rotfang. The Bruiser profile pushed further in both
        /// directions -- the biggest crit multiplier in the game and the worst
        /// accuracy, so its damage spikes are genuinely swingy.</summary>
        public static readonly CombatStats Brute = new CombatStats(0.12f, 1.80f, 0.92f, 0.02f);

        /// <summary>Map 2's Deadeye. Best crit rate, best accuracy and best evasion in
        /// the game, paid for with the roster's lowest HP (100) and defense (8) -- the
        /// unit the party is meant to kill first.</summary>
        public static readonly CombatStats Sniper = new CombatStats(0.25f, 1.70f, 0.98f, 0.10f);
    }
}
