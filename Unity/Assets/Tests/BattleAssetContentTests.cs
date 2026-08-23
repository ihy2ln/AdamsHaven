using System.Collections.Generic;
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

        // -- M16: per-archetype elements and ultimates -------------------------------

        static readonly (string unitId, ElementType element)[] ArchetypeElements =
        {
            ("player_melee", ElementType.Fire), ("player_ranged", ElementType.Wind), ("player_support", ElementType.Water),
            ("enemy_melee", ElementType.Fire), ("enemy_ranged", ElementType.Wind), ("enemy_support", ElementType.Water),
            ("player_bench_melee", ElementType.Fire), ("player_bench_ranged", ElementType.Wind), ("player_bench_support", ElementType.Water),
        };

        /// <summary>Every non-map-2 unit's element (M16) should match its archetype, not
        /// sit at the Neutral default that made DamageCalculator.ElementMultiplier a
        /// no-op for every character before this milestone.</summary>
        [Test]
        public void EveryArchetype_HasItsOwnNonNeutralElement()
        {
            foreach (var (unitId, element) in ArchetypeElements)
                Assert.AreEqual(element, Load(unitId).element, $"{unitId} should be {element}-elemental.");
        }

        /// <summary>Each archetype's ultimateSkill (M16) exists and carries the same
        /// element as the character -- "should match the element and class type" per the
        /// project owner's spec -- and is gauge-gated (0 mpCost) rather than MP-gated,
        /// since BattleController.ResolveAction drains BattleUnit.CurrentUltimateCharge
        /// for whichever skill == Definition.ultimateSkill regardless of mpCost.</summary>
        [Test]
        public void EveryArchetype_HasAnUltimateMatchingItsOwnElement()
        {
            foreach (var (unitId, element) in ArchetypeElements)
            {
                var def = Load(unitId);
                Assert.IsNotNull(def.ultimateSkill, $"{unitId} has no ultimateSkill -- the U button can never enable.");
                Assert.AreEqual(element, def.ultimateSkill.element, $"{unitId}'s ultimate should be {element}-elemental like the character.");
                Assert.AreEqual(0, def.ultimateSkill.mpCost, $"{unitId}'s ultimate should be free (gauge-gated, not MP-gated).");
            }
        }

        // -- M17: map-2 elements/ultimates and the authored combat-stat pass ----------

        /// <summary>M16 authored elements and ultimates through the shared-archetype loop
        /// only, so map 2's BuildCustomEnemy roster stayed Neutral with no ultimate --
        /// this is the test that would catch a future enemy added down that same path and
        /// silently left out of the combat systems again. The exact elements are
        /// BattleAssetBuilder.Map2EnemyElement's call (see its doc for why these three).</summary>
        static readonly (string unitId, ElementType element)[] Map2Elements =
        {
            ("enemy_rotfang", ElementType.Earth),
            ("enemy_deadeye", ElementType.Lightning),
            ("enemy_hexweaver", ElementType.Fire),
        };

        [Test]
        public void Map2Enemies_HaveTheirOwnNonNeutralElement()
        {
            foreach (var (unitId, element) in Map2Elements)
            {
                var def = Load(unitId);
                Assert.AreEqual(element, def.element, $"{unitId} should be {element}-elemental, not the Neutral default.");
                Assert.AreEqual(element, def.standardSkill.element,
                    $"{unitId}'s BA should carry the caster's own element, same rule the archetypes follow.");
            }
        }

        /// <summary>Same three assertions the archetype ultimates get -- exists, matches
        /// the caster's element, gauge-gated rather than MP-gated. These matter more in
        /// practice than the player-side ones: BattleController.ChooseAutoSkill fires an
        /// ultimate for any unit that has one, so these are what make a plain auto-battle
        /// of map 2 show off the system with no manual input.</summary>
        [Test]
        public void Map2Enemies_HaveAnUltimateMatchingTheirOwnElement()
        {
            foreach (var (unitId, element) in Map2Elements)
            {
                var def = Load(unitId);
                Assert.IsNotNull(def.ultimateSkill, $"{unitId} has no ultimateSkill -- it can never fire one in auto mode.");
                Assert.AreEqual(element, def.ultimateSkill.element, $"{unitId}'s ultimate should be {element}-elemental like the character.");
                Assert.AreEqual(0, def.ultimateSkill.mpCost, $"{unitId}'s ultimate should be free (gauge-gated, not MP-gated).");
                Assert.AreNotEqual(def.standardSkill, def.ultimateSkill, $"{unitId}'s ultimate must not just be its BA.");
            }
        }

        /// <summary>The point of map 2's element picks: a party of Fire/Wind/Water meets
        /// three enemies at three genuinely different multipliers, rather than everyone
        /// trading 1x. Guards against a future retune quietly flattening the encounter --
        /// e.g. giving every map-2 enemy the same element.</summary>
        [Test]
        public void Map2Roster_IsElementallyDistinct()
        {
            var elements = Map2Elements.Select(e => Load(e.unitId).element).ToList();
            CollectionAssert.AllItemsAreUnique(elements,
                "map 2's three enemies should not share an element -- that's what makes the encounter read.");
            CollectionAssert.DoesNotContain(elements, ElementType.Neutral);
        }

        /// <summary>All 5 elements of ElementChart's cycle should be live on real built
        /// content, not just the Fire/Wind/Water M16 gave the archetypes -- otherwise
        /// Earth and Lightning are chart entries no battle can ever exercise. Light/Dark
        /// are deliberately excluded: nothing carries them yet, so they'd be a no-op 1x
        /// either way (see ElementChart's own doc).</summary>
        [Test]
        public void EveryCycleElement_IsCarriedBySomeBuiltCharacter()
        {
            // new HashSet<>(...) rather than ToHashSet(): the latter is .NET Standard
            // 2.1, and this project is pinned to 2.0 (same trap as Dictionary
            // .GetValueOrDefault, see BattleAssetBuilder.GetOrEmpty).
            var live = new HashSet<ElementType>(
                AllUnitIds.Concat(Map2Elements.Select(e => e.unitId)).Select(id => Load(id).element));

            foreach (var element in new[] { ElementType.Fire, ElementType.Wind, ElementType.Earth,
                                            ElementType.Lightning, ElementType.Water })
                CollectionAssert.Contains(live, element,
                    $"no built character is {element}-elemental, so ElementChart's {element} row is unreachable in play.");
        }

        static readonly string[] EveryCombatantId =
        {
            "player_melee", "player_ranged", "player_support",
            "enemy_melee", "enemy_ranged", "enemy_support",
            "player_bench_melee", "player_bench_ranged", "player_bench_support",
            "enemy_rotfang", "enemy_deadeye", "enemy_hexweaver",
        };

        /// <summary>M17's balance pass: every unit's crit/accuracy/evasion are now
        /// authored on the asset (CombatStats' profiles), where through M16 they were all
        /// 0 on disk and BattleWorld.RandomizeTestCombatStats sprayed a random roll over
        /// them at battle start. That helper is deleted, so a unit that comes back from
        /// this test with 0s is a unit whose crit and miss systems are silently inert --
        /// exactly the failure mode the randomizer was papering over.</summary>
        [Test]
        public void EveryCombatant_HasAuthoredCritAndAccuracyStats()
        {
            foreach (var unitId in EveryCombatantId)
            {
                var s = Load(unitId).baseStats;
                Assert.Greater(s.critRate, 0f, $"{unitId} can never crit -- critRate is still the 0 default.");
                Assert.Less(s.critRate, 1f, $"{unitId} crits on every hit.");
                Assert.GreaterOrEqual(s.critDamage, 1f, $"{unitId}'s crit would deal *less* than a normal hit.");
                Assert.Greater(s.accuracy, 0f,
                    $"{unitId}'s accuracy is still the 0 default -- DamageCalculator.HitChance silently treats that as 'always hits'.");
                Assert.LessOrEqual(s.accuracy, 1f, $"{unitId}'s accuracy is above 100%.");
                Assert.GreaterOrEqual(s.evasion, 0f, $"{unitId} has negative evasion.");
            }
        }

        /// <summary>M19: neither shipped map forbids escape. `forbidEscape` defaults to
        /// false so no rebuild was needed to add it, and no content sets it yet -- it's
        /// the hook a future boss-phase system wants. This pins the current answer, so
        /// flipping one becomes a deliberate, visible change rather than a surprise the
        /// first time someone can't flee.</summary>
        [Test]
        public void NeitherShippedMap_ForbidsEscape()
        {
            for (int i = 1; i <= 2; i++)
            {
                var map = Resources.Load<MapDefinition>($"Battle/Maps/Map_BattleSlice{i}");
                Assert.IsNotNull(map, $"Map_BattleSlice{i} missing.");
                Assert.IsFalse(map.forbidEscape, $"map {i} blocks the Flee action -- no content is meant to yet.");
            }
        }

        /// <summary>The one number the whole accuracy pass hangs on: no matchup anywhere
        /// in the game may miss more than a fifth of the time. A turn-based battle where
        /// turns regularly evaporate reads as broken rather than tactical, so accuracy
        /// stays high and evasion low (see CombatStats' doc). This is a design invariant,
        /// not a formula check -- it fails the moment someone retunes one unit's evasion
        /// up without looking at what it does to the least accurate attacker.</summary>
        [Test]
        public void NoMatchupInTheGame_MissesMoreThanAFifthOfTheTime()
        {
            const float floor = 0.8f;
            var units = EveryCombatantId
                .Select(id => (id, unit: BattleTestHelpers.MakeUnit(
                    Load(id).baseStats, Faction.Player, column: 1, facingRight: true)))
                .ToList();

            foreach (var (attackerId, attacker) in units)
            foreach (var (targetId, target) in units)
            {
                if (attackerId == targetId) continue;
                float chance = DamageCalculator.HitChance(attacker, target);
                Assert.GreaterOrEqual(chance, floor,
                    $"{attackerId} (accuracy {attacker.Stats.accuracy:0.00}) only lands {chance:P0} of its hits on "
                    + $"{targetId} (evasion {target.Stats.evasion:0.00}) -- floor is {floor:P0}.");
            }
        }
    }
}
