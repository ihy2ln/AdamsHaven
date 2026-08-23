using NUnit.Framework;
using Game.Data;
using Game.Battle;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    /// <summary>The M16 ultimate gauge primitives on BattleUnit -- pure C#, independent of
    /// BattleController's turn loop (which needs a live scene to test at all -- see
    /// PROJECT-README's testing philosophy). Charging/draining is a MonoBehaviour-level
    /// concern (BattleController.ResolveAction/RunBattle), not covered here, matching the
    /// same split MpRegenTests.cs already established for the MP economy.</summary>
    public class UltimateGaugeTests
    {
        static readonly StatBlock DummyStats = new() { hp = 100, attack = 20, defense = 10, magic = 10, resistance = 10, speed = 10 };

        [Test]
        public void GainUltimateCharge_ClampsAtMax()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.GainUltimateCharge(BattleUnit.MaxUltimateCharge + 50);
            Assert.AreEqual(BattleUnit.MaxUltimateCharge, unit.CurrentUltimateCharge);
        }

        [Test]
        public void GainUltimateCharge_NeverGoesNegative()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.GainUltimateCharge(-9999);
            Assert.AreEqual(0, unit.CurrentUltimateCharge);
        }

        [Test]
        public void IsUltimateReady_FalseUntilMaxCharge()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            Assert.IsFalse(unit.IsUltimateReady);

            unit.GainUltimateCharge(BattleUnit.MaxUltimateCharge - 1);
            Assert.IsFalse(unit.IsUltimateReady);

            unit.GainUltimateCharge(1);
            Assert.IsTrue(unit.IsUltimateReady);
        }

        [Test]
        public void SpendUltimateCharge_DrainsToZeroAndClearsReady()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.GainUltimateCharge(BattleUnit.MaxUltimateCharge);
            Assert.IsTrue(unit.IsUltimateReady);

            unit.SpendUltimateCharge();

            Assert.AreEqual(0, unit.CurrentUltimateCharge);
            Assert.IsFalse(unit.IsUltimateReady);
        }
    }
}
