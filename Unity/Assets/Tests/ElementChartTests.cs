using NUnit.Framework;
using Game.Data;
using Game.Battle;

namespace Game.Tests
{
    /// <summary>The M16 elemental weakness chart -- pure C#, no scene dependency.</summary>
    public class ElementChartTests
    {
        [Test]
        public void Neutral_NeverGetsOrTakesABonus()
        {
            Assert.AreEqual(1f, ElementChart.GetMultiplier(ElementType.Neutral, ElementType.Fire));
            Assert.AreEqual(1f, ElementChart.GetMultiplier(ElementType.Fire, ElementType.Neutral));
            Assert.AreEqual(1f, ElementChart.GetMultiplier(ElementType.Neutral, ElementType.Neutral));
        }

        [Test]
        public void SameElement_IsNeitherWeaknessNorResist()
        {
            Assert.AreEqual(1f, ElementChart.GetMultiplier(ElementType.Fire, ElementType.Fire));
        }

        [TestCase(ElementType.Fire, ElementType.Wind)]
        [TestCase(ElementType.Wind, ElementType.Earth)]
        [TestCase(ElementType.Earth, ElementType.Lightning)]
        [TestCase(ElementType.Lightning, ElementType.Water)]
        [TestCase(ElementType.Water, ElementType.Fire)]
        [TestCase(ElementType.Light, ElementType.Dark)]
        [TestCase(ElementType.Dark, ElementType.Light)]
        public void KnownWeakness_ReturnsWeaknessMultiplier(ElementType attacker, ElementType target)
        {
            Assert.AreEqual(ElementChart.WeaknessMultiplier, ElementChart.GetMultiplier(attacker, target));
        }

        [Test]
        public void ReverseOfAKnownWeakness_ReturnsResistMultiplier()
        {
            // Fire beats Wind, so Wind attacking Fire should be resisted, not neutral.
            Assert.AreEqual(ElementChart.ResistMultiplier, ElementChart.GetMultiplier(ElementType.Wind, ElementType.Fire));
        }

        [Test]
        public void UnrelatedElements_AreNeutral()
        {
            // Fire and Lightning aren't adjacent in the 5-cycle either direction.
            Assert.AreEqual(1f, ElementChart.GetMultiplier(ElementType.Fire, ElementType.Lightning));
        }
    }
}
