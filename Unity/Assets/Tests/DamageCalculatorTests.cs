using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Battle;
using static Game.Tests.BattleTestHelpers;

namespace Game.Tests
{
    public class DamageCalculatorTests
    {
        [Test]
        public void Damage_NeverGoesNegative()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 1, defense = 1, magic = 1, resistance = 1, speed = 1 }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 1, defense = 999, magic = 1, resistance = 999, speed = 1 }, Faction.Enemy, 3, false);
            var skill = MakeSkill(pattern: null, isRanged: false, usesMagic: false, power: 1f);

            int damage = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);

            Assert.GreaterOrEqual(damage, 0);
        }

        [Test]
        public void RangedDamage_IncreasesWithDistance()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 20, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 5, false);
            var skill = MakeSkill(pattern: null, isRanged: true, usesMagic: false, power: 1f);

            int near = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);
            int far = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 5);

            Assert.Greater(far, near);
        }

        [Test]
        public void MeleeDamage_DoesNotScaleWithDistance()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 20, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 5, false);
            var skill = MakeSkill(pattern: null, isRanged: false, usesMagic: false, power: 1f);

            int atDistance1 = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1);
            int atDistance5 = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 5);

            Assert.AreEqual(atDistance1, atDistance5);
        }

        [Test]
        public void Heal_ScalesWithMagic()
        {
            var weakHealer = MakeUnit(new StatBlock { hp = 100, attack = 1, defense = 1, magic = 5, resistance = 1, speed = 1 }, Faction.Player, 1, true);
            var strongHealer = MakeUnit(new StatBlock { hp = 100, attack = 1, defense = 1, magic = 30, resistance = 1, speed = 1 }, Faction.Player, 1, true);
            var skill = MakeSkill(pattern: null, usesMagic: true, power: 1f, targetsAllies: true);

            Assert.Greater(DamageCalculator.ComputeHeal(strongHealer, skill), DamageCalculator.ComputeHeal(weakHealer, skill));
        }

        // -- M16: crit, elemental weakness, hit chance ------------------------------

        [Test]
        public void Crit_MultipliesDamageByCritDamage()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 20, defense = 5, magic = 10, resistance = 5, speed = 10, critDamage = 2f }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 3, false);
            var skill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int normal = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1, isCrit: false);
            int crit = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1, isCrit: true);

            Assert.AreEqual(normal * 2, crit);
        }

        [Test]
        public void Crit_WithNoCritDamageAuthored_FallsBackToDefaultMultiplier()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 20, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 3, false);
            var skill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int normal = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1, isCrit: false);
            int crit = DamageCalculator.ComputeDamage(attacker, target, skill, columnDistance: 1, isCrit: true);

            Assert.AreEqual(Mathf.RoundToInt(normal * DamageCalculator.DefaultCritDamage), crit);
        }

        [Test]
        public void ElementalWeakness_IncreasesDamage()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 20, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Player, 0, true);
            var fireWeakTarget = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 3, false);
            fireWeakTarget.Definition.element = ElementType.Wind; // Fire beats Wind
            var fireSkill = MakeSkill(pattern: null, usesMagic: false, power: 1f);
            fireSkill.element = ElementType.Fire;
            var neutralSkill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int neutral = DamageCalculator.ComputeDamage(attacker, fireWeakTarget, neutralSkill, columnDistance: 1);
            int exploited = DamageCalculator.ComputeDamage(attacker, fireWeakTarget, fireSkill, columnDistance: 1);

            Assert.Greater(exploited, neutral);
        }

        [Test]
        public void ElementalResist_DecreasesDamage()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 20, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Player, 0, true);
            // Water beats Fire, so a Fire attacker against a Water target is resisted.
            var waterTarget = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 3, false);
            waterTarget.Definition.element = ElementType.Water;
            var fireSkill = MakeSkill(pattern: null, usesMagic: false, power: 1f);
            fireSkill.element = ElementType.Fire;
            var neutralSkill = MakeSkill(pattern: null, usesMagic: false, power: 1f);

            int neutral = DamageCalculator.ComputeDamage(attacker, waterTarget, neutralSkill, columnDistance: 1);
            int resisted = DamageCalculator.ComputeDamage(attacker, waterTarget, fireSkill, columnDistance: 1);

            Assert.Less(resisted, neutral);
        }

        [Test]
        public void HitChance_DefaultsToAlwaysHitWhenAccuracyUnauthored()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10 }, Faction.Enemy, 3, false);

            Assert.AreEqual(1f, DamageCalculator.HitChance(attacker, target));
        }

        [Test]
        public void HitChance_SubtractsEvasionFromAccuracy()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10, accuracy = 0.9f }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10, evasion = 0.2f }, Faction.Enemy, 3, false);

            Assert.AreEqual(0.7f, DamageCalculator.HitChance(attacker, target), 0.0001f);
        }

        [Test]
        public void HitChance_NeverGoesNegative()
        {
            var attacker = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10, accuracy = 0.5f }, Faction.Player, 0, true);
            var target = MakeUnit(new StatBlock { hp = 100, attack = 10, defense = 5, magic = 10, resistance = 5, speed = 10, evasion = 0.9f }, Faction.Enemy, 3, false);

            Assert.AreEqual(0f, DamageCalculator.HitChance(attacker, target));
        }
    }
}
