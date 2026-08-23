using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Data;
using Game.Battle;

namespace Game.Tests
{
    /// <summary>
    /// Asserts that the ScriptableObjects actually on disk in Resources/Battle carry the
    /// content BattleAssetBuilder authors -- as opposed to the rest of the suite, which
    /// tests logic against hand-built in-memory objects and therefore passes just fine
    /// while the real assets are stale.
    ///
    /// That gap is exactly how M10 shipped broken: the 9 Skill Moves existed only in the
    /// builder's C#, `Build Assets From Manifest` was never run, so every character asset
    /// still had an empty skillMoves list. Manual mode's "SM" button greyed out and MP
    /// never drained, with nothing failing anywhere to say why. These tests fail loudly in
    /// that state, and are safe to run headlessly (-runTests never touches AssetDatabase,
    /// so the batchmode asset-corruption gotcha doesn't apply).
    /// </summary>
    public class BattleAssetContentTests
    {
        static readonly string[] AllUnitIds =
        {
            "player_melee", "player_ranged", "player_support",
            "enemy_melee", "enemy_ranged", "enemy_support",
            "player_bench_melee", "player_bench_ranged", "player_bench_support",
        };

        /// <summary>Support archetypes carry a 4th Skill Move (Mana Spring, M12) -- every
        /// other archetype still has exactly 3.</summary>
        static int ExpectedSkillMoveCount(string unitId) => unitId.EndsWith("support") ? 4 : 3;

        static CharacterDefinition Load(string unitId)
        {
            var def = Resources.Load<CharacterDefinition>($"Battle/Characters/Char_{unitId}");
            Assert.IsNotNull(def, $"Char_{unitId} missing from Resources/Battle/Characters -- "
                + "run AI.Game > Battle > Build Assets From Manifest.");
            return def;
        }

        [Test]
        public void EveryCharacter_HasAFreeBasicAttack()
        {
            foreach (var unitId in AllUnitIds)
            {
                var def = Load(unitId);
                Assert.IsNotNull(def.standardSkill, $"{unitId} has no standardSkill (the free BA).");
                Assert.IsNotNull(def.standardSkill.pattern, $"{unitId}'s BA has no targeting pattern.");
                Assert.AreEqual(0, def.standardSkill.mpCost, $"{unitId}'s BA must be free.");
            }
        }

        /// <summary>The regression that started all this: empty skillMoves == permanently
        /// greyed-out SM button, since BattleHud enables it on SkillMoveOptions.Count > 0.</summary>
        [Test]
        public void EveryCharacter_HasThreeUsableSkillMoves()
        {
            foreach (var unitId in AllUnitIds)
            {
                var def = Load(unitId);
                var moves = def.skillMoves?.Where(s => s != null).ToList();
                int expected = ExpectedSkillMoveCount(unitId);

                Assert.IsNotNull(moves, $"{unitId} has a null skillMoves list.");
                Assert.AreEqual(expected, moves.Count,
                    $"{unitId} has {moves.Count} Skill Moves, expected {expected}.");
                Assert.AreEqual(expected, moves.Select(s => s.skillId).Distinct().Count(),
                    $"{unitId}'s Skill Moves are not distinct: {string.Join(", ", moves.Select(s => s.skillId))}");

                // The SM popup lists these by displayName alone, so duplicate names are
                // indistinguishable in-game even when the underlying assets differ -- which
                // is exactly what the old "label anything targetsAllies as Heal" HUD rule
                // produced (Kestrel showed Heal/Heal/Power Strike).
                Assert.AreEqual(expected, moves.Select(s => s.displayName).Distinct().Count(),
                    $"{unitId}'s Skill Moves share a display name: {string.Join(", ", moves.Select(s => s.displayName))}");

                foreach (var move in moves)
                {
                    Assert.IsNotNull(move.pattern, $"{unitId}'s '{move.displayName}' has no targeting pattern.");
                    Assert.IsFalse(string.IsNullOrEmpty(move.displayName),
                        $"{unitId} has a Skill Move with no displayName -- the SM popup would show a blank row.");
                }
            }
        }

        /// <summary>MP only ever decreases (there's no regen yet), so a unit starting at
        /// full MP must be able to afford at least one Skill Move on turn one, and no move
        /// may cost more than the pool can ever hold.</summary>
        [Test]
        public void EveryCharacter_CanAffordSkillMovesFromAFullPool()
        {
            foreach (var unitId in AllUnitIds)
            {
                var def = Load(unitId);
                Assert.Greater(def.maxMp, 0, $"{unitId} has maxMp {def.maxMp} -- no Skill Move would ever be affordable.");

                var moves = def.skillMoves.Where(s => s != null).ToList();
                foreach (var move in moves)
                    Assert.LessOrEqual(move.mpCost, def.maxMp,
                        $"{unitId}'s '{move.displayName}' costs {move.mpCost} MP but maxMp is {def.maxMp}.");

                Assert.IsTrue(moves.Any(m => m.mpCost > 0),
                    $"{unitId} has no Skill Move that costs MP -- its mana bar would never move.");
            }
        }

        /// <summary>BattleController.ChooseAutoSkill looks for a targetsAllies-and-not-
        /// restoresMana entry in skillMoves to decide whether a healer heals this turn. Two
        /// ways this can silently break: Heal missing entirely (stale assets, M10's
        /// original bug), or a plain "any targetsAllies" filter grabbing Mana Spring (M12)
        /// instead of Heal -- a "healer" that tops up MP while an ally bleeds out.</summary>
        [Test]
        public void SupportUnits_AttackWithBaAndHealFromSkillMoves()
        {
            foreach (var unitId in AllUnitIds.Where(id => id.EndsWith("support")))
            {
                var def = Load(unitId);
                Assert.IsFalse(def.standardSkill.targetsAllies,
                    $"{unitId}'s BA should be its attack -- healers can attack too (M10).");
                Assert.IsTrue(def.skillMoves.Any(s => s != null && s.targetsAllies && !s.restoresMana),
                    $"{unitId} has no HP-healing Skill Move (targetsAllies && !restoresMana), so auto mode will never heal.");
            }
        }

        /// <summary>Mana Spring (M12): restoresMana routes through a different branch of
        /// BattleController.ResolveAction than a heal, and DamageCalculator.ComputeManaRestore
        /// must actually produce a positive number against the real character's magic stat.</summary>
        [Test]
        public void SupportUnits_HaveAnAffordableManaRestoreSkill()
        {
            foreach (var unitId in AllUnitIds.Where(id => id.EndsWith("support")))
            {
                var def = Load(unitId);
                var manaSkill = def.skillMoves.FirstOrDefault(s => s != null && s.restoresMana);
                Assert.IsNotNull(manaSkill, $"{unitId} has no restoresMana Skill Move.");
                Assert.IsTrue(manaSkill.targetsAllies, $"{unitId}'s '{manaSkill.displayName}' restores mana but doesn't targetsAllies.");
                Assert.LessOrEqual(manaSkill.mpCost, def.maxMp,
                    $"{unitId}'s '{manaSkill.displayName}' costs more MP than the pool can ever hold.");

                var dummyCaster = BattleTestHelpers.MakeUnit(def.baseStats, Faction.Player, column: 1, facingRight: true);
                int restored = DamageCalculator.ComputeManaRestore(dummyCaster, manaSkill);
                Assert.Greater(restored, 0, $"{unitId}'s '{manaSkill.displayName}' would restore 0 MP against its own base stats.");
            }
        }

        [Test]
        public void MeleeUnits_HaveAMeleeFlavouredAttack()
        {
            // DamageCalculator.ComputeDamage grants a ranged-only damage bonus that scales
            // with column distance -- a melee BA flagged isRanged would get that bonus,
            // on top of misrepresenting the archetype in any future ranged-only logic.
            foreach (var unitId in AllUnitIds.Where(id => id.EndsWith("melee")))
            {
                var def = Load(unitId);
                Assert.IsFalse(def.standardSkill.isRanged, $"{unitId}'s BA is flagged ranged -- it would get the ranged distance damage bonus.");
                Assert.IsFalse(def.standardSkill.targetsAllies, $"{unitId}'s BA should target enemies.");
            }
        }

        /// <summary>BattleController.ResolveAction treats a pattern with more than one
        /// areaOffset as AoE (TargetResolver.GetAreaTargets). If these collapse to a single
        /// offset, Mass Heal/Volley/Barrage quietly become single-target skills.</summary>
        [Test]
        public void AoeSkillMoves_CoverMoreThanOneColumn()
        {
            var aoeSkillIds = new[]
            {
                "skill_supportmassheal",
                "skill_rangedvolley",
                "skill_rangedbarrage",
            };

            var allMoves = AllUnitIds
                .Select(Load)
                .SelectMany(d => d.skillMoves.Where(s => s != null))
                .GroupBy(s => s.skillId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var skillId in aoeSkillIds)
            {
                Assert.IsTrue(allMoves.ContainsKey(skillId),
                    $"AoE skill '{skillId}' is not on any character. Present: {string.Join(", ", allMoves.Keys)}");
                var skill = allMoves[skillId];
                Assert.Greater(skill.pattern.areaOffsets.Count, 1,
                    $"'{skill.displayName}' has {skill.pattern.areaOffsets.Count} area offset(s) -- it would only hit one target.");
            }
        }

        /// <summary>BattleWorld.SeedPlaceholderInventory loads these 3 assets by exact
        /// path -- missing one leaves that Item slot permanently empty (see
        /// BattleContentGuard's parallel check, which is what actually catches this in
        /// practice; this test is the "code-based stats" verification of the same thing).</summary>
        [Test]
        public void AllThreePotions_ExistWithCorrectKindAndUsablePotency()
        {
            var expected = new[]
            {
                ("Potion_Hp", PotionKind.Hp),
                ("Potion_Mp", PotionKind.Mp),
                ("Potion_Multi", PotionKind.Multi),
            };

            foreach (var (assetName, kind) in expected)
            {
                var potion = Resources.Load<PotionDefinition>($"Battle/Potions/{assetName}");
                Assert.IsNotNull(potion, $"{assetName} missing from Resources/Battle/Potions -- "
                    + "run AI.Game > Battle > Build Assets From Manifest.");
                Assert.AreEqual(kind, potion.kind, $"{assetName} has kind {potion.kind}, expected {kind}.");
                Assert.Greater(potion.maxStack, 0, $"{assetName} has maxStack {potion.maxStack}.");
                Assert.Greater(PotionCalculator.Potency(potion.rank), 0, $"{assetName}'s rank ({potion.rank}) produces 0 potency.");
            }
        }

        /// <summary>The 5 Skill Moves that carry a status effect on top of their existing
        /// heal/damage (M13) -- verifies the retrofit actually landed on the right skill
        /// with the right type, not just that *some* skill somewhere has one.</summary>
        [Test]
        public void RetrofittedSkillMoves_CarryTheirIntendedStatusEffect()
        {
            var expected = new (string skillId, StatusEffectType type)[]
            {
                ("skill_meleeguard", StatusEffectType.Regen),        // Second Wind
                ("skill_meleepowerstrike", StatusEffectType.DefenseDown), // Power Strike
                ("skill_supportfocusheal", StatusEffectType.Regen),  // Focus Heal
                ("skill_rangedsnipe", StatusEffectType.AttackDown),  // Snipe
                ("skill_rangedbarrage", StatusEffectType.Stun),      // Barrage
            };

            var allMoves = AllUnitIds
                .Select(Load)
                .SelectMany(d => d.skillMoves.Where(s => s != null))
                .GroupBy(s => s.skillId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var (skillId, type) in expected)
            {
                Assert.IsTrue(allMoves.ContainsKey(skillId), $"'{skillId}' is not on any character.");
                var skill = allMoves[skillId];
                Assert.AreEqual(type, skill.inflictsStatus,
                    $"'{skill.displayName}' inflicts {skill.inflictsStatus}, expected {type}.");
                Assert.Greater(skill.statusDuration, 0, $"'{skill.displayName}' has a 0-turn status duration.");
            }
        }

        [Test]
        public void BothMaps_ExistWithThreeEnemies()
        {
            for (int i = 1; i <= 2; i++)
            {
                var map = Resources.Load<MapDefinition>($"Battle/Maps/Map_BattleSlice{i}");
                Assert.IsNotNull(map, $"Map_BattleSlice{i} missing.");
                Assert.AreEqual(3, map.enemies.Count(e => e.character != null), $"Map {i} should field 3 enemies.");
                Assert.IsNotNull(map.backgroundSprite, $"Map {i} has no background sprite.");
            }
        }

        /// <summary>M14: map 2 gets its own distinct enemy roster (Rotfang/Deadeye/
        /// Hexweaver) instead of reusing map 1's Husk/Warden/Stinger -- this is the test
        /// that would catch someone accidentally wiring the same characters into both
        /// maps again.</summary>
        [Test]
        public void Map2_FieldsADifferentRosterThanMap1()
        {
            var map1 = Resources.Load<MapDefinition>("Battle/Maps/Map_BattleSlice1");
            var map2 = Resources.Load<MapDefinition>("Battle/Maps/Map_BattleSlice2");

            var map1Ids = map1.enemies.Select(e => e.character.characterId).OrderBy(id => id).ToList();
            var map2Ids = map2.enemies.Select(e => e.character.characterId).OrderBy(id => id).ToList();

            CollectionAssert.AreNotEqual(map1Ids, map2Ids, "map 2 should not field the exact same enemies as map 1.");
            CollectionAssert.AreEquivalent(
                new[] { "enemy_rotfang", "enemy_deadeye", "enemy_hexweaver" }, map2Ids,
                $"map 2's roster is {string.Join(", ", map2Ids)}, expected Rotfang/Deadeye/Hexweaver.");
        }

        static readonly (string unitId, string displayName, StatusEffectType status)[] Map2Enemies =
        {
            ("enemy_rotfang", "Rotfang", StatusEffectType.Poison),
            ("enemy_deadeye", "Deadeye", StatusEffectType.AttackDown),
            ("enemy_hexweaver", "Hexweaver", StatusEffectType.DefenseDown),
        };

        /// <summary>Each map-2 enemy needs a free BA and at least one offensive Skill
        /// Move carrying the status effect that's its whole reason for existing -- this
        /// system was added (M13) specifically because map 1's Husk/Warden/Stinger are
        /// flat player reskins with no offensive Skill Move an AI would ever reach for
        /// (see BattleController.ChooseAutoSkill), so status effects had nothing to
        /// exercise them in a real battle.</summary>
        [Test]
        public void Map2Enemies_HaveAFreeBaAndAnOffensiveStatusSkill()
        {
            foreach (var (unitId, displayName, status) in Map2Enemies)
            {
                var def = Load(unitId);
                Assert.AreEqual(displayName, def.displayName);
                Assert.IsNotNull(def.standardSkill, $"{unitId} has no BA.");
                Assert.AreEqual(0, def.standardSkill.mpCost, $"{unitId}'s BA must be free.");
                Assert.IsNotNull(def.battleSprite, $"{unitId} has no battle sprite -- art borrowing from the bench roster failed.");

                var offensiveMove = def.skillMoves.FirstOrDefault(s => s != null && !s.targetsAllies && s.inflictsStatus == status);
                Assert.IsNotNull(offensiveMove,
                    $"{unitId} has no offensive Skill Move inflicting {status}. Has: "
                    + string.Join(", ", def.skillMoves.Where(s => s != null).Select(s => $"{s.displayName}->{s.inflictsStatus}")));
                Assert.Greater(offensiveMove.mpCost, 0, $"{unitId}'s '{offensiveMove.displayName}' should cost MP like every other Skill Move.");
            }
        }

        /// <summary>Hexweaver is the one map-2 enemy with a heal alongside its debuff --
        /// same "auto mode heals its side before attacking" carve-out the player's
        /// support archetype gets (ChooseAutoSkill's healMove filter), so it needs a
        /// real HP-healing, non-restoresMana ally-targeting move to trigger it.</summary>
        [Test]
        public void Hexweaver_CanHealItsOwnSide()
        {
            var def = Load("enemy_hexweaver");
            Assert.IsTrue(def.skillMoves.Any(s => s != null && s.targetsAllies && !s.restoresMana),
                "Hexweaver has no HP-healing Skill Move, so auto mode will never heal its allies.");
        }
    }
}
