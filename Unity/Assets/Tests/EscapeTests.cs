using System.Collections.Generic;
using NUnit.Framework;
using Game.Data;
using Game.Battle;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    /// <summary>
    /// EscapeCalculator (M19) -- the escape-chance math, pure C# and independent of the
    /// UnityEngine.Random roll that consumes it (which lives on BattleController and
    /// isn't headlessly testable, same split crit and the offensive-Skill-Move AI chance
    /// already use).
    /// </summary>
    public class EscapeTests
    {
        const float Delta = 0.0001f;

        static StatBlock WithSpeed(int speed) =>
            new() { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = speed };

        static List<BattleUnit> Side(Faction faction, params int[] speeds)
        {
            var units = new List<BattleUnit>();
            for (int i = 0; i < speeds.Length; i++)
                units.Add(MakeUnit(WithSpeed(speeds[i]), faction, column: i, facingRight: true));
            return units;
        }

        [Test]
        public void EvenlyMatchedSpeed_WithNoFailures_IsTheBaseChance()
        {
            float chance = EscapeCalculator.EscapeChance(
                Side(Faction.Player, 10, 10, 10), Side(Faction.Enemy, 10, 10, 10), failedAttempts: 0);

            Assert.AreEqual(EscapeCalculator.BaseChance, chance, Delta);
        }

        [Test]
        public void AFasterParty_EscapesMoreOftenThanASlowerOne()
        {
            var enemies = Side(Faction.Enemy, 10, 10, 10);
            float fast = EscapeCalculator.EscapeChance(Side(Faction.Player, 15, 15, 15), enemies, 0);
            float slow = EscapeCalculator.EscapeChance(Side(Faction.Player, 5, 5, 5), enemies, 0);

            Assert.Greater(fast, EscapeCalculator.BaseChance, "a faster party should beat the base chance.");
            Assert.Less(slow, EscapeCalculator.BaseChance, "a slower party should fall below it.");
        }

        /// <summary>Speed is compared as a ratio, not a difference -- "twice as fast"
        /// should mean the same thing at 5-vs-10 as at 50-vs-100. A difference-based
        /// formula would make this slice's single-digit speeds barely register at all.</summary>
        [Test]
        public void SpeedIsComparedAsARatio_NotADifference()
        {
            float small = EscapeCalculator.EscapeChance(Side(Faction.Player, 10), Side(Faction.Enemy, 5), 0);
            float large = EscapeCalculator.EscapeChance(Side(Faction.Player, 100), Side(Faction.Enemy, 50), 0);

            Assert.AreEqual(small, large, Delta, "the same speed ratio should give the same chance at any scale.");
        }

        [Test]
        public void EachFailedAttempt_MakesTheNextOneEasier()
        {
            var us = Side(Faction.Player, 10);
            var them = Side(Faction.Enemy, 10);

            float first = EscapeCalculator.EscapeChance(us, them, 0);
            float second = EscapeCalculator.EscapeChance(us, them, 1);
            float third = EscapeCalculator.EscapeChance(us, them, 2);

            Assert.AreEqual(EscapeCalculator.PerFailedAttemptBonus, second - first, Delta);
            Assert.AreEqual(EscapeCalculator.PerFailedAttemptBonus, third - second, Delta);
        }

        /// <summary>The property the escalation exists for: a party can never be locked
        /// into a fight it can't leave. Even at the worst possible speed disadvantage,
        /// enough attempts reach the cap -- so fleeing costs turns, it isn't a coin flip
        /// you can lose indefinitely.</summary>
        [Test]
        public void EnoughFailedAttempts_ReachTheCapEvenAtTheWorstSpeedDisadvantage()
        {
            float chance = EscapeCalculator.EscapeChance(
                Side(Faction.Player, 1), Side(Faction.Enemy, 100), failedAttempts: 10);

            Assert.AreEqual(EscapeCalculator.MaxChance, chance, Delta);
        }

        /// <summary>Never a certainty on a first try, however fast the party is -- the
        /// ratio clamp plus MaxChance both hold here.</summary>
        [Test]
        public void EvenAnAbsurdSpeedAdvantage_IsNotACertainty()
        {
            float chance = EscapeCalculator.EscapeChance(
                Side(Faction.Player, 999), Side(Faction.Enemy, 1), failedAttempts: 0);

            Assert.LessOrEqual(chance, EscapeCalculator.MaxChance);
            Assert.AreEqual(EscapeCalculator.BaseChance + EscapeCalculator.SpeedSwing, chance, Delta,
                "the speed ratio is clamped at 2x, so this should be exactly one full SpeedSwing above base.");
        }

        /// <summary>Pins the real worst case, which is *not* MinChance -- with the
        /// current constants the speed term can only ever subtract SpeedSwing/2, so a
        /// hopeless first attempt still lands 37.5% of the time and MinChance never
        /// binds. See MinChance's own doc: it's a guard against a future retune, not a
        /// working floor, and this test is what says so out loud.</summary>
        [Test]
        public void TheWorstFirstAttemptInTheGame_IsStillBetterThanEvens()
        {
            float chance = EscapeCalculator.EscapeChance(
                Side(Faction.Player, 1), Side(Faction.Enemy, 100), failedAttempts: 0);

            Assert.AreEqual(EscapeCalculator.BaseChance - EscapeCalculator.SpeedSwing / 2f, chance, Delta);
            Assert.Greater(chance, EscapeCalculator.MinChance,
                "MinChance shouldn't be binding today -- if it is, the constants moved and its doc is now wrong.");
        }

        /// <summary>Dead units don't slow the party down. A lone fast survivor should
        /// read as fast, not be averaged against allies who are already corpses -- and
        /// the same on the other side, where fleeing three dead enemies should be easy.</summary>
        [Test]
        public void DeadUnitsAreExcludedFromBothAverages()
        {
            var party = Side(Faction.Player, 20, 2, 2);
            party[1].ApplyDamage(party[1].Stats.hp);
            party[2].ApplyDamage(party[2].Stats.hp);

            float chance = EscapeCalculator.EscapeChance(party, Side(Faction.Enemy, 10), 0);
            float loneSurvivorOnly = EscapeCalculator.EscapeChance(Side(Faction.Player, 20), Side(Faction.Enemy, 10), 0);

            Assert.AreEqual(loneSurvivorOnly, chance, Delta,
                "the two dead allies should not have dragged the party's average speed down.");
        }

        /// <summary>A pure function shouldn't require its callers to know that an empty
        /// side is unreachable in a real battle. No divide-by-zero, no NaN.</summary>
        [Test]
        public void AnEmptySide_FallsBackToTheBaseChanceRatherThanDividingByZero()
        {
            var empty = new List<BattleUnit>();

            Assert.AreEqual(EscapeCalculator.BaseChance,
                EscapeCalculator.EscapeChance(empty, Side(Faction.Enemy, 10), 0), Delta);
            Assert.AreEqual(EscapeCalculator.BaseChance,
                EscapeCalculator.EscapeChance(Side(Faction.Player, 10), empty, 0), Delta);
            Assert.AreEqual(EscapeCalculator.BaseChance,
                EscapeCalculator.EscapeChance(null, null, 0), Delta);
        }
    }
}
