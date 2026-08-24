using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Battle;

namespace Game.Tests
{
    /// <summary>
    /// BattleInventory.Grant (M25) -- the pickup path a Treasure-node reward calls.
    /// Pure C#, no Resources dependency, so it's testable without a scene the same way
    /// BattleRewards is.
    /// </summary>
    public class BattleInventoryTests
    {
        static PotionDefinition MakePotion(int maxStack)
        {
            var potion = ScriptableObject.CreateInstance<PotionDefinition>();
            potion.maxStack = maxStack;
            return potion;
        }

        [Test]
        public void Grant_AddsToTheNamedSlotAndReturnsHowManyWereActuallyAdded()
        {
            var inventory = new BattleInventory();
            inventory.Hp.Potion = MakePotion(maxStack: 99);
            inventory.Hp.Count = 3;

            int gained = inventory.Grant(PotionKind.Hp, 2);

            Assert.AreEqual(2, gained);
            Assert.AreEqual(5, inventory.Hp.Count);
        }

        [Test]
        public void Grant_ClampsToThePotionsOwnMaxStack()
        {
            var inventory = new BattleInventory();
            inventory.Multi.Potion = MakePotion(maxStack: 10);
            inventory.Multi.Count = 9;

            int gained = inventory.Grant(PotionKind.Multi, 5);

            Assert.AreEqual(1, gained, "only 1 more should fit before hitting maxStack.");
            Assert.AreEqual(10, inventory.Multi.Count);
        }

        [Test]
        public void Grant_AtAnAlreadyFullStack_AddsNothing()
        {
            var inventory = new BattleInventory();
            inventory.Mp.Potion = MakePotion(maxStack: 5);
            inventory.Mp.Count = 5;

            int gained = inventory.Grant(PotionKind.Mp, 3);

            Assert.AreEqual(0, gained, "a full stack should report an honest zero, not overflow.");
            Assert.AreEqual(5, inventory.Mp.Count);
        }

        /// <summary>The failure mode this exists to avoid: a slot whose Potion asset
        /// never got built (missing content, same class of gap BattleContentGuard
        /// checks for elsewhere) should never silently claim a pickup happened.</summary>
        [Test]
        public void Grant_WithNoBuiltPotionForThatSlot_AddsNothingRatherThanFakingIt()
        {
            var inventory = new BattleInventory(); // Hp/Mp/Multi.Potion all start null

            int gained = inventory.Grant(PotionKind.Hp, 1);

            Assert.AreEqual(0, gained);
            Assert.AreEqual(0, inventory.Hp.Count);
        }

        [Test]
        public void Grant_WithZeroOrNegativeAmount_IsANoOp()
        {
            var inventory = new BattleInventory();
            inventory.Hp.Potion = MakePotion(maxStack: 99);
            inventory.Hp.Count = 4;

            Assert.AreEqual(0, inventory.Grant(PotionKind.Hp, 0));
            Assert.AreEqual(0, inventory.Grant(PotionKind.Hp, -3));
            Assert.AreEqual(4, inventory.Hp.Count, "neither call should have changed the count.");
        }
    }
}
