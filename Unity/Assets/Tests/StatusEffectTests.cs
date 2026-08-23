using NUnit.Framework;
using Game.Data;
using Game.Battle;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    /// <summary>
    /// The standard-JRPG status-effect system added in M13: buffs/debuffs (Attack/Defense
    /// Up/Down), DoT/HoT (Poison/Regen), and Stun. Pure C# on BattleUnit/DamageCalculator,
    /// independent of BattleController's turn loop (which needs a live scene to test at
    /// all -- see PROJECT-README's testing philosophy).
    /// </summary>
    public class StatusEffectTests
    {
        static readonly StatBlock DummyStats = new() { hp = 100, attack = 20, defense = 10, magic = 10, resistance = 10, speed = 10 };

        [Test]
        public void ApplyStatus_Poison_TicksFlatDamagePerTurn()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.ApplyStatus(StatusEffectType.Poison, magnitude: 15f, turns: 2);

            unit.TickStatusEffects();
            Assert.AreEqual(85, unit.CurrentHp);

            unit.TickStatusEffects();
            Assert.AreEqual(70, unit.CurrentHp);
        }

        [Test]
        public void ApplyStatus_Regen_TicksFlatHealPerTurn()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.ApplyDamage(50); // 50/100
            unit.ApplyStatus(StatusEffectType.Regen, magnitude: 10f, turns: 2);

            unit.TickStatusEffects();
            Assert.AreEqual(60, unit.CurrentHp);
        }

        [Test]
        public void TickStatusEffects_RemovesEffectAfterItsLastTurn()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.ApplyStatus(StatusEffectType.Poison, magnitude: 5f, turns: 1);

            Assert.AreEqual(1, unit.StatusEffects.Count);
            unit.TickStatusEffects();
            Assert.AreEqual(0, unit.StatusEffects.Count, "a 1-turn effect should be gone after its single tick.");
        }

        [Test]
        public void ApplyStatus_ReappliedType_RefreshesInPlaceRatherThanStacking()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.ApplyStatus(StatusEffectType.Poison, magnitude: 5f, turns: 1);
            unit.ApplyStatus(StatusEffectType.Poison, magnitude: 20f, turns: 3);

            Assert.AreEqual(1, unit.StatusEffects.Count, "reapplying the same type should refresh, not add a second instance.");
            unit.TickStatusEffects();
            Assert.AreEqual(80, unit.CurrentHp, "the refreshed magnitude (20) should apply, not the original (5).");
        }

        [Test]
        public void IsStunned_TrueOnlyWhileStunActive()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            Assert.IsFalse(unit.IsStunned);

            unit.ApplyStatus(StatusEffectType.Stun, magnitude: 0f, turns: 1);
            Assert.IsTrue(unit.IsStunned);

            unit.TickStatusEffects();
            Assert.IsFalse(unit.IsStunned, "a 1-turn Stun should be gone after one tick.");
        }

        [Test]
        public void AttackMultiplier_CombinesUpAndDown()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            Assert.AreEqual(1f, unit.AttackMultiplier);

            unit.ApplyStatus(StatusEffectType.AttackUp, magnitude: 0.3f, turns: 2);
            Assert.AreEqual(1.3f, unit.AttackMultiplier, 0.0001f);

            unit.ApplyStatus(StatusEffectType.AttackDown, magnitude: 0.1f, turns: 2);
            Assert.AreEqual(1.2f, unit.AttackMultiplier, 0.0001f, "Up and Down should net together, not override.");
        }

        [Test]
        public void DefenseMultiplier_AffectsDamageCalculator()
        {
            var attacker = MakeUnit(DummyStats, Faction.Player, 0, true);
            var target = MakeUnit(DummyStats, Faction.Enemy, 3, false);
            var skill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int baseline = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            target.ApplyStatus(StatusEffectType.DefenseDown, magnitude: 0.5f, turns: 2);
            int againstWeakenedDefense = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            Assert.Greater(againstWeakenedDefense, baseline, "DefenseDown should let more damage through.");
        }

        [Test]
        public void AttackMultiplier_AffectsDamageCalculator()
        {
            var attacker = MakeUnit(DummyStats, Faction.Player, 0, true);
            var target = MakeUnit(DummyStats, Faction.Enemy, 3, false);
            var skill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int baseline = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            attacker.ApplyStatus(StatusEffectType.AttackDown, magnitude: 0.5f, turns: 2);
            int weakened = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            Assert.Less(weakened, baseline, "AttackDown on the attacker should deal less damage.");
        }

        // -- M16: Break -------------------------------------------------------------

        [Test]
        public void IsIncapacitated_TrueForEitherStunOrBreak()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            Assert.IsFalse(unit.IsIncapacitated);

            unit.ApplyStatus(StatusEffectType.Break, magnitude: 0.3f, turns: 1);
            Assert.IsTrue(unit.IsIncapacitated, "Break should incapacitate the same way Stun does.");

            unit.TickStatusEffects();
            Assert.IsFalse(unit.IsIncapacitated, "a 1-turn Break should be gone after one tick, same as Stun.");
        }

        [Test]
        public void Break_DoesNotSetIsStunned()
        {
            var unit = MakeUnit(DummyStats, Faction.Player, 0, true);
            unit.ApplyStatus(StatusEffectType.Break, magnitude: 0.3f, turns: 1);

            Assert.IsFalse(unit.IsStunned, "IsStunned should stay specific to the Stun type; IsIncapacitated is the combined check.");
            Assert.IsTrue(unit.IsIncapacitated);
        }

        [Test]
        public void Break_IncreasesDamageTakenLikeDefenseDown()
        {
            var attacker = MakeUnit(DummyStats, Faction.Player, 0, true);
            var target = MakeUnit(DummyStats, Faction.Enemy, 3, false);
            var skill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int baseline = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            target.ApplyStatus(StatusEffectType.Break, magnitude: 0.3f, turns: 1);
            int againstBroken = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            Assert.Greater(againstBroken, baseline, "a broken target should take bonus damage.");
        }
    }
}
