using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Data;
using Game.Battle;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    /// <summary>
    /// BattleWorld's carry-over path -- the thing that makes the 2-map sequence a
    /// sequence rather than two unrelated fights, and (since M18's battle skip) the
    /// thing a mid-battle skip goes through too.
    ///
    /// Unlike most of this suite these tests do touch Resources, because BattleWorld's
    /// constructor loads the map and tier assets and there's no seam to fake them. They
    /// still never touch AssetDatabase, so the batchmode-corruption gotcha doesn't apply
    /// (same reasoning as BattleAssetContentTests).
    /// </summary>
    public class BattleWorldTests
    {
        static readonly StatBlock DummyStats =
            new() { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 };

        static List<BattleUnit> MakeParty()
        {
            var party = new List<BattleUnit>();
            for (int column = 0; column < 3; column++)
                party.Add(MakeUnit(DummyStats, Faction.Player, column, facingRight: true));
            return party;
        }

        /// <summary>The hazard M18's `CanSkipToNextMap` guard exists to prevent, pinned
        /// so nobody deletes that guard as a redundant-looking null check.
        ///
        /// Carry-over drops the dead, so advancing with a wiped party produces a world
        /// with zero player units. That is *not* the same as a lost battle: PlayerDefeated
        /// is `PlayerUnits.Any() && PlayerUnits.All(dead)`, which is false when there are
        /// none at all, so IsOver never becomes true and BattleController.RunBattle loops
        /// enemy turns forever with nothing to target. A soft-lock, not a defeat screen.
        /// The victory path can't reach this state (you can't win by dying), which is
        /// exactly why it only became reachable once skipping mid-battle existed.</summary>
        [Test]
        public void CarryingOverAWipedParty_WouldLeaveABattleThatCanNeverEnd()
        {
            var party = MakeParty();
            foreach (var unit in party) unit.ApplyDamage(unit.Stats.hp);
            Assert.IsTrue(party.All(u => !u.IsAlive), "test setup: the party should be wiped.");

            var world = new BattleWorld(mapIndex: 1, carryOverPlayer: party,
                carryOverBench: new List<BattleUnit>(), carryOverInventory: null);

            Assert.IsEmpty(world.PlayerUnits, "carry-over should drop the dead -- that's the premise of this test.");
            Assert.IsFalse(world.PlayerDefeated,
                "with no player units at all, PlayerDefeated reads false -- this is the trap.");
            Assert.IsFalse(world.IsOver,
                "a battle with no player units and living enemies can never end. "
                + "BattleController.CanSkipToNextMap must keep the game out of this state.");
        }

        /// <summary>The case a skip is actually for: wounded survivors carry their exact
        /// HP forward and compact into columns 0..n in their existing front-to-back
        /// order, with the dead left behind. Same behaviour a victory already had -- this
        /// pins that skipping reuses it rather than quietly getting its own path.</summary>
        [Test]
        public void CarryingOverSurvivors_KeepsTheirWoundsAndCompactsColumns()
        {
            var party = MakeParty();
            party[1].ApplyDamage(DummyStats.hp);      // the middle unit dies
            party[2].ApplyDamage(40);                 // the front unit is wounded

            var world = new BattleWorld(mapIndex: 1, carryOverPlayer: party,
                carryOverBench: new List<BattleUnit>(), carryOverInventory: null);

            var survivors = world.PlayerUnits.OrderBy(u => u.Column).ToList();
            Assert.AreEqual(2, survivors.Count, "the dead unit should have been dropped, the other two kept.");
            CollectionAssert.AreEqual(new[] { 0, 1 }, survivors.Select(u => u.Column).ToList(),
                "survivors should compact into columns 0..n with no gap where the dead unit was.");

            Assert.AreEqual(DummyStats.hp, survivors[0].CurrentHp, "the untouched unit should carry over at full HP.");
            Assert.AreEqual(DummyStats.hp - 40, survivors[1].CurrentHp,
                "the wounded unit should carry its wound forward -- HP deliberately does not recover between maps.");
        }

        /// <summary>Map 2 is the last map, so there is nothing to skip *to* from there --
        /// the other half of CanSkipToNextMap's condition. A test rather than a comment
        /// because MapCount is a constant someone will eventually raise, and this is the
        /// line that should start failing when they do.</summary>
        [Test]
        public void TheLastMap_HasNoNextMap()
        {
            Assert.IsTrue(new BattleWorld(mapIndex: 0).HasNextMap, "map 1 should have a next map to skip to.");
            Assert.IsFalse(new BattleWorld(mapIndex: BattleWorld.MapCount - 1).HasNextMap,
                "the last map has nothing to advance to.");
        }
    }
}
