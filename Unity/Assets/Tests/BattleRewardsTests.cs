using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Battle;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    /// <summary>
    /// The EXP/material ledger (M20) that gives escaping and quitting something to
    /// actually be about. Pure C# -- BattleRewards has no Resources or MonoBehaviour
    /// dependency, which is the whole reason the drop values are derived from stats
    /// rather than authored on the assets.
    /// </summary>
    public class BattleRewardsTests
    {
        static BattleUnit Enemy(int hp, int attack, int magic, int speed, ElementType element)
        {
            var unit = MakeUnit(
                new StatBlock { hp = hp, attack = attack, defense = 5, magic = magic, resistance = 5, speed = speed },
                Faction.Enemy, column: 3, facingRight: false);
            unit.Definition.element = element;
            return unit;
        }

        [Test]
        public void AFreshLedger_IsEmpty()
        {
            var rewards = new BattleRewards();

            Assert.IsTrue(rewards.IsEmpty);
            Assert.AreEqual(0, rewards.Exp);
            Assert.AreEqual(0, rewards.TotalMaterials);
            Assert.AreEqual("nothing", rewards.Describe(),
                "an empty haul should describe itself, so callers never special-case it.");
        }

        [Test]
        public void DefeatingAnEnemy_AwardsExpAndExactlyOneMaterial()
        {
            var rewards = new BattleRewards();
            var enemy = Enemy(hp: 100, attack: 20, magic: 10, speed: 10, ElementType.Fire);

            rewards.Award(enemy);

            Assert.AreEqual(BattleRewards.ExpFor(enemy), rewards.Exp);
            Assert.AreEqual(1, rewards.TotalMaterials, "one kill should drop exactly one material.");
            Assert.IsFalse(rewards.IsEmpty);
        }

        /// <summary>Attack counts double, so a glass cannon pays out more than its HP
        /// alone would suggest -- otherwise Deadeye (the roster's lowest HP, highest
        /// threat) would be worth less than a passive wall.</summary>
        [Test]
        public void ExpWeightsAttack_SoAGlassCannonIsWorthMoreThanAWall()
        {
            var cannon = Enemy(hp: 100, attack: 30, magic: 5, speed: 15, ElementType.Wind);
            var wall = Enemy(hp: 130, attack: 5, magic: 5, speed: 5, ElementType.Wind);

            Assert.Greater(BattleRewards.ExpFor(cannon), BattleRewards.ExpFor(wall));
        }

        [Test]
        public void NothingIsEverWorthZeroExp()
        {
            var trivial = Enemy(hp: 1, attack: 0, magic: 0, speed: 0, ElementType.Neutral);

            Assert.GreaterOrEqual(BattleRewards.ExpFor(trivial), 1);
        }

        /// <summary>Material kind follows the dropper's element, which is what makes map
        /// 1 (Fire/Water/Wind) and map 2 (Earth/Lightning/Fire) drop visibly different
        /// things. Neutral falls back to Hide rather than dropping nothing -- a unit
        /// built before elements existed should still be worth killing.</summary>
        [Test]
        public void MaterialKindFollowsTheDroppersElement()
        {
            Assert.AreEqual(MaterialKind.Ore, BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, ElementType.Fire)));
            Assert.AreEqual(MaterialKind.Ore, BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, ElementType.Earth)));
            Assert.AreEqual(MaterialKind.Essence, BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, ElementType.Lightning)));
            Assert.AreEqual(MaterialKind.Hide, BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, ElementType.Water)));
            Assert.AreEqual(MaterialKind.Hide, BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, ElementType.Neutral)));
        }

        /// <summary>Banking on a victory or a successful escape. The pending ledger is
        /// cleared separately by the caller, so Absorb must not mutate its argument.</summary>
        [Test]
        public void Absorb_AddsOneLedgerIntoAnotherWithoutMutatingTheSource()
        {
            var run = new BattleRewards();
            var battle = new BattleRewards();
            battle.Award(Enemy(100, 20, 10, 10, ElementType.Fire));
            battle.Award(Enemy(100, 20, 10, 10, ElementType.Lightning));

            int battleExp = battle.Exp;
            run.Absorb(battle);

            Assert.AreEqual(battleExp, run.Exp);
            Assert.AreEqual(2, run.TotalMaterials);
            Assert.AreEqual(battleExp, battle.Exp, "Absorb should read the source, not drain it.");
        }

        /// <summary>The quit path. Everything earned this battle goes away, and the
        /// ledger is reusable afterwards rather than left in a half-state.</summary>
        [Test]
        public void Clear_EmptiesEverything()
        {
            var rewards = new BattleRewards();
            rewards.Award(Enemy(100, 20, 10, 10, ElementType.Earth));

            rewards.Clear();

            Assert.IsTrue(rewards.IsEmpty);
            Assert.AreEqual(0, rewards.Exp);
            Assert.AreEqual(0, rewards.Count(MaterialKind.Ore));
        }

        /// <summary>Camp's Rest spends materials, and it's driven by a UI button -- a
        /// ledger that can be pushed below zero by a double-click is a real bug, so
        /// Spend clamps and reports what it actually took.</summary>
        [Test]
        public void Spend_ClampsToWhatIsActuallyThereAndReportsIt()
        {
            var rewards = new BattleRewards();
            rewards.Award(Enemy(100, 20, 10, 10, ElementType.Fire)); // 1 Ore

            Assert.AreEqual(1, rewards.Spend(MaterialKind.Ore, 5), "should only take what exists.");
            Assert.AreEqual(0, rewards.Count(MaterialKind.Ore));
            Assert.AreEqual(0, rewards.Spend(MaterialKind.Ore, 5), "spending from empty takes nothing.");
            Assert.GreaterOrEqual(rewards.Count(MaterialKind.Ore), 0, "a ledger must never go negative.");
        }

        [Test]
        public void Describe_ListsExpAndEachMaterialKindItActuallyHas()
        {
            var rewards = new BattleRewards();
            rewards.Award(Enemy(100, 20, 10, 10, ElementType.Fire));      // Ore
            rewards.Award(Enemy(100, 20, 10, 10, ElementType.Water));     // Hide

            string described = rewards.Describe();

            StringAssert.Contains("EXP", described);
            StringAssert.Contains("Hide", described);
            StringAssert.Contains("Ore", described);
            StringAssert.DoesNotContain("Essence", described, "kinds with a zero count shouldn't be listed.");
        }

        /// <summary>Map 2's roster drops something map 1's doesn't. Not a formula check
        /// -- a content check, using the elements M17 actually authored, so it fails if
        /// someone retunes the map-2 elements into the same bucket as map 1's.</summary>
        [Test]
        public void Map2Roster_DropsMaterialsMap1DoesNot()
        {
            var map1 = new[] { ElementType.Fire, ElementType.Water, ElementType.Wind }
                .Select(e => BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, e))).Distinct().ToList();
            var map2 = new[] { ElementType.Earth, ElementType.Lightning, ElementType.Fire }
                .Select(e => BattleRewards.MaterialFor(Enemy(1, 1, 1, 1, e))).Distinct().ToList();

            CollectionAssert.Contains(map2, MaterialKind.Essence);
            CollectionAssert.DoesNotContain(map1, MaterialKind.Essence,
                "Essence should be the thing map 2 has that map 1 doesn't -- that's what makes going deeper worth it.");
        }
    }
}
