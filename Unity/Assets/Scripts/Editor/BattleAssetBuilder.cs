#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds ClipSet/SkillPattern/SkillDefinition/CharacterDefinition/MapDefinition
    /// ScriptableObjects for the battle vertical slice from
    /// Tools/ComfyUI/manifest.export.json (a JSON mirror of manifest.yaml -- this
    /// project has no YAML parser or Newtonsoft.Json package, so generate.py writes
    /// the JSON copy on every run; see Tools/ComfyUI/generate.py export_manifest_json).
    ///
    /// Idempotent: re-running loads and updates existing assets in place rather than
    /// duplicating them, per the M2 brief. Missing generated art is not an error --
    /// fields are just left null and a warning is logged, so this can run before every
    /// asset exists and fill in the rest on a later run (never let missing art block
    /// the pipeline).
    /// </summary>
    public static class BattleAssetBuilder
    {
        const string OutDir = "Assets/Resources/Battle";

        /// <summary>Bump whenever the content this builder authors changes shape (new
        /// skills, new stats, retuned patterns). BattleContentGuard compares this against
        /// the last-built stamp and re-runs Build() automatically inside an interactive
        /// Editor session, so freshly-written content can never sit un-built again --
        /// which is exactly what happened to M10's 9 Skill Moves (built in code, never
        /// written to the ScriptableObjects, so every SM button stayed greyed out).
        /// 1 = pre-M10 (standardSkill only). 2 = M10 (maxMp + 3 skillMoves per archetype).
        /// 3 = the support heal renamed off "Support Basic Attack" to "Heal". 4 = M12's
        /// Mana Spring (Support's 4th skillMove, restoresMana). 5 = M13's 3 potion
        /// assets (Battle/Potions/) and status effects on 5 existing Skill Moves. 6 =
        /// impactFrames now authored in KnownGoodImpactFrames instead of read from the
        /// manifest's unreliable impact_frames field. 7 = M14's map-2-only enemy roster
        /// (Rotfang/Deadeye/Hexweaver).
        /// </summary>
        public const int ContentVersion = 7;

        /// <summary>EditorPrefs key holding the ContentVersion last written to disk.
        /// Deliberately EditorPrefs rather than an asset in the repo: a fresh clone (or a
        /// git revert of Resources/Battle) then rebuilds once on first open, which is the
        /// behaviour we want.</summary>
        public const string ContentVersionPrefKey = "AI.Game.Battle.ContentVersion";

        [MenuItem("AI.Game/Battle/Build Assets From Manifest")]
        public static void Build()
        {
            var export = LoadManifestExport();
            if (export == null) return;

            string unityRelRoot = export.outputRoot.StartsWith("Unity/")
                ? export.outputRoot.Substring("Unity/".Length)
                : export.outputRoot;

            var byType = export.assets.GroupBy(a => a.type).ToDictionary(g => g.Key, g => g.ToList());
            var spritesByUnit = GetOrEmpty(byType, "sprite").ToDictionary(a => a.unit_id);
            var portraitsByUnit = GetOrEmpty(byType, "portrait").ToDictionary(a => a.unit_id);
            var clipsById = GetOrEmpty(byType, "clip").ToDictionary(a => a.id);
            var backgroundAsset = GetOrEmpty(byType, "background").FirstOrDefault();

            Directory.CreateDirectory(OutDir + "/Tiers");
            Directory.CreateDirectory(OutDir + "/Patterns");
            Directory.CreateDirectory(OutDir + "/Skills");
            Directory.CreateDirectory(OutDir + "/Clips");
            Directory.CreateDirectory(OutDir + "/Characters");
            Directory.CreateDirectory(OutDir + "/Maps");
            Directory.CreateDirectory(OutDir + "/Potions");

            var tier = BuildTier();
            BuildPotions();

            // Side-view (Darkest Dungeon / Slay the Spire style) formation, not the isometric
            // lane grid FOUNDATION.md originally specified -- see PROJECT-README.md pivot note.
            // Single lane; column IS the horizontal rank. Player ranks 0(back)-2(front),
            // enemy ranks 3(front)-5(back), so the two melee units land adjacent (2 vs 3).
            var archetypes = new[]
            {
                new ArchetypeSpec("Melee", "clip_melee_basic", ClassType.Warrior, isRanged: false, usesMagic: false,
                    power: 1.2f, rangeOffsets: new() { new Vector2Int(0, 1) }),
                new ArchetypeSpec("Ranged", "clip_ranged_basic", ClassType.Ranger, isRanged: true, usesMagic: false,
                    power: 1.0f, rangeOffsets: Enumerable.Range(1, 5).Select(c => new Vector2Int(0, c)).ToList()),
                new ArchetypeSpec("Support", "clip_heal_basic", ClassType.Healer, isRanged: false, usesMagic: true,
                    power: 1.0f, targetsAllies: true,
                    rangeOffsets: new() { new Vector2Int(0, -1), new Vector2Int(0, 0), new Vector2Int(0, 1) }),
            };

            var skillByArchetype = new Dictionary<string, SkillDefinition>();
            var clipSetByArchetype = new Dictionary<string, ClipSet>();

            foreach (var arch in archetypes)
            {
                var pattern = BuildPattern(arch);
                var clipAsset = TryGet(clipsById, arch.ClipAssetId);
                var clipSet = BuildClipSet(arch, clipAsset, unityRelRoot);
                var skill = BuildSkill(arch, pattern);

                skillByArchetype[arch.Name] = skill;
                clipSetByArchetype[arch.Name] = clipSet;
            }

            var unitIds = new[]
            {
                "player_melee", "player_ranged", "player_support",
                "enemy_melee", "enemy_ranged", "enemy_support",
                // Bench reserves (sub-in/sub-out) -- reskins of the matching active
                // archetype's stats/skill/pattern, distinct art + name only. Substring
                // matching against archetype names (below) already resolves these
                // correctly since e.g. "player_bench_melee".Contains("melee").
                "player_bench_melee", "player_bench_ranged", "player_bench_support",
            };

            // "SM" (Skill Move) content: mana-cost actions beyond the free "BA" attack,
            // 3 per archetype (the full 1-3 set). Design intent per the project owner:
            // frontline gets defensive skills, healers get support skills (this is where
            // Heal moves to), ranged gets sniping/AoE skills -- kept to heal/damage
            // primitives only, no status-effect system yet (explicit scope call).
            var meleePattern = skillByArchetype["Melee"].pattern; // Pattern_MeleeBasic, ±1 column
            var supportPattern = skillByArchetype["Support"].pattern; // Pattern_SupportBasic, ±1 column incl. self
            var rangedPattern = skillByArchetype["Ranged"].pattern; // Pattern_RangedBasic, any column

            // Melee -- defensive: patch self up, cover an adjacent ally, or finish a target harder.
            // Second Wind and Power Strike also carry a status effect (M13) on top of
            // their existing heal/damage -- additive, doesn't touch the tuned power/
            // mpCost numbers above. Magnitudes/durations are arbitrary (project owner
            // direction), not a tuned balance pass.
            var meleeGuard = BuildSkillMove("Skill_MeleeGuard", "Second Wind",
                BuildSelfOnlyPattern(), power: 0.8f, usesMagic: false, targetsAllies: true, mpCost: 30,
                inflictsStatus: StatusEffectType.Regen, statusMagnitude: 10f, statusDuration: 2);
            var meleeRally = BuildSkillMove("Skill_MeleeRally", "Rally",
                supportPattern, power: 0.7f, usesMagic: false, targetsAllies: true, mpCost: 25);
            var meleePowerStrike = BuildSkillMove("Skill_MeleePowerStrike", "Power Strike",
                meleePattern, power: 2.0f, usesMagic: false, targetsAllies: false, mpCost: 35,
                inflictsStatus: StatusEffectType.DefenseDown, statusMagnitude: 0.2f, statusDuration: 2);

            // Ranged -- sniping and AoE: a heavy single shot, a 3-wide cluster hit, and a
            // guaranteed full-team volley for the "AoE" end of that design intent. Snipe
            // and Barrage also carry a status effect (M13), same additive rule as above.
            var rangedVolley = BuildSkillMove("Skill_RangedVolley", "Volley",
                BuildVolleyPattern(rangedPattern.rangeOffsets), power: 0.6f, usesMagic: false,
                targetsAllies: false, mpCost: 25, isRanged: true);
            var rangedSnipe = BuildSkillMove("Skill_RangedSnipe", "Snipe",
                rangedPattern, power: 1.8f, usesMagic: false, targetsAllies: false, mpCost: 30, isRanged: true,
                inflictsStatus: StatusEffectType.AttackDown, statusMagnitude: 0.2f, statusDuration: 2);
            var rangedBarrage = BuildSkillMove("Skill_RangedBarrage", "Barrage",
                BuildBarragePattern(rangedPattern.rangeOffsets), power: 0.5f, usesMagic: false,
                targetsAllies: false, mpCost: 45, isRanged: true,
                inflictsStatus: StatusEffectType.Stun, statusMagnitude: 0f, statusDuration: 1);

            // Healer -- support: the relocated single-target Heal, a wider group heal, and
            // a bigger single-target emergency heal.
            // BuildSkill named this "Support Basic Attack" back when it was the Support
            // archetype's standardSkill. It's the single-target Heal now (M10 moved it into
            // skillMoves and gave healers a real attack for their BA), so it needs its own
            // identity -- the SM popup shows displayName verbatim.
            var healSkill = skillByArchetype["Support"];
            healSkill.skillId = "skill_support_heal";
            healSkill.displayName = "Heal";
            healSkill.mpCost = 20;
            EditorUtility.SetDirty(healSkill);
            var massHeal = BuildSkillMove("Skill_SupportMassHeal", "Mass Heal",
                BuildWideHealPattern(), power: 0.6f, usesMagic: true, targetsAllies: true, mpCost: 35);
            var focusHeal = BuildSkillMove("Skill_SupportFocusHeal", "Focus Heal",
                supportPattern, power: 1.6f, usesMagic: true, targetsAllies: true, mpCost: 30,
                inflictsStatus: StatusEffectType.Regen, statusMagnitude: 10f, statusDuration: 2);
            // Healer's standardSkill (BA) becomes the low-power attack instead of the heal
            // -- "healers can attack too" -- now that Heal itself lives in skillMoves.
            var healerBasicAttack = BuildSkillMove("Skill_SupportAttackBasic", "Support Strike",
                supportPattern, power: 0.5f, usesMagic: false, targetsAllies: false, mpCost: 0);
            // Mana Spring (M12) -- the healer spends their own MP to hand a slice of it to
            // an ally, per the project owner's MP-economy design: a small passive trickle
            // from basic attacks, a bigger chunk between battles, full restore reserved for
            // a future farm/town "sleep" hook, and this -- an active, targeted top-up a
            // player can reach for mid-battle. Same range as Heal (self or an adjacent
            // ally); restoresMana routes it to BattleController.ResolveAction's MP branch
            // instead of the HP-heal branch.
            var manaSpring = BuildSkillMove("Skill_SupportManaSpring", "Mana Spring",
                supportPattern, power: 1.0f, usesMagic: true, targetsAllies: true, mpCost: 15, restoresMana: true);

            var skillMovesByArchetype = new Dictionary<string, List<SkillDefinition>>
            {
                ["Melee"] = new() { meleeGuard, meleeRally, meleePowerStrike },
                ["Ranged"] = new() { rangedVolley, rangedSnipe, rangedBarrage },
                ["Support"] = new() { healSkill, massHeal, focusHeal, manaSpring },
            };
            var standardSkillOverride = new Dictionary<string, SkillDefinition> { ["Support"] = healerBasicAttack };

            // Map 2's distinct enemy roster (M14) -- Rotfang/Deadeye/Hexweaver, reusing
            // Thorne/Reed/Vesper's already-imported art+clips (no new art generated, per
            // the project owner's "worry about assets later" direction) but hand-authored
            // stats and a genuinely different kit each, built below in
            // BuildMap2EnemySkills/BuildMap2Enemies once characterDefs exists to borrow
            // art from. Existed to give the M13 status-effect system enemies that
            // actually use it -- Map 1's Husk/Warden/Stinger are flat player reskins with
            // no offensive Skill Moves an AI would ever ordinarily reach for (see
            // BattleController.ChooseAutoSkill's new offensive-move branch).
            var map2Skills = BuildMap2EnemySkills(meleePattern, rangedPattern, supportPattern);

            var characterDefs = new Dictionary<string, CharacterDefinition>();
            foreach (var unitId in unitIds)
            {
                string archName = archetypes.First(a => unitId.Contains(a.Name.ToLowerInvariant())).Name;
                var arch = archetypes.First(a => a.Name == archName);
                var standardSkill = standardSkillOverride.TryGetValue(archName, out var ov) ? ov : skillByArchetype[archName];
                var charDef = BuildCharacter(
                    unitId, arch, tier,
                    standardSkill, clipSetByArchetype[archName],
                    TryGet(spritesByUnit, unitId), TryGet(portraitsByUnit, unitId),
                    unityRelRoot, skillMoves: skillMovesByArchetype[archName]);
                characterDefs[unitId] = charDef;
            }

            var map2Enemies = BuildMap2Enemies(characterDefs, map2Skills);

            var (fxSheet, fxRects) = LoadFxSheet();
            var manifestBackground = LoadSprite(backgroundAsset, unityRelRoot);
            // Explicit == null, not ??: Unity's overloaded equality is what detects a
            // destroyed/unassigned Object, and ?? bypasses it (the same trap that left
            // BattleBootstrap silently without a Camera -- see PROJECT-README.md M10).
            var map1Placements = new List<EnemyPlacement>
            {
                new() { character = characterDefs["enemy_melee"], tier = tier, position = new Vector2Int(0, 3), level = 1 },
                new() { character = characterDefs["enemy_support"], tier = tier, position = new Vector2Int(0, 4), level = 1 },
                new() { character = characterDefs["enemy_ranged"], tier = tier, position = new Vector2Int(0, 5), level = 1 },
            };
            var map2Placements = new List<EnemyPlacement>
            {
                new() { character = map2Enemies["enemy_rotfang"], tier = tier, position = new Vector2Int(0, 3), level = 1 },
                new() { character = map2Enemies["enemy_hexweaver"], tier = tier, position = new Vector2Int(0, 4), level = 1 },
                new() { character = map2Enemies["enemy_deadeye"], tier = tier, position = new Vector2Int(0, 5), level = 1 },
            };
            BuildMap(1, map1Placements, OrFallback(LoadBackgroundSprite("bg_battle1"), manifestBackground), fxSheet, fxRects);
            BuildMap(2, map2Placements, OrFallback(LoadBackgroundSprite("bg_battle2"), manifestBackground), fxSheet, fxRects);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorPrefs.SetInt(ContentVersionPrefKey, ContentVersion);
            Debug.Log($"[AI.Game] Battle assets built from manifest (content v{ContentVersion}).");
        }

        // -- manifest loading -----------------------------------------------------

        static ManifestExport LoadManifestExport()
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string manifestPath = Path.Combine(repoRoot, "Tools", "ComfyUI", "manifest.export.json");
            if (!File.Exists(manifestPath))
            {
                Debug.LogError($"[AI.Game] manifest.export.json not found at {manifestPath}. "
                    + "Run `python Tools/ComfyUI/generate.py --dry-run` at least once to generate it.");
                return null;
            }
            return JsonUtility.FromJson<ManifestExport>(File.ReadAllText(manifestPath));
        }

        static Sprite LoadSprite(ManifestAsset asset, string unityRelRoot)
        {
            if (asset == null) return null;
            string path = $"{unityRelRoot}/{asset.output}";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[AI.Game] Expected sprite not found (not generated yet?): {path}");
            return sprite;
        }

        // Curated HD roster art (Tools/AssetImport/import_roster.py), separate from the
        // ComfyUI manifest pipeline -- see that script's docstring for provenance.
        static readonly Dictionary<string, string> RosterNames = new()
        {
            ["player_melee"] = "Kestrel",
            ["player_ranged"] = "Sable",
            ["player_support"] = "Linnet",
            ["enemy_melee"] = "Husk",
            ["enemy_ranged"] = "Warden",
            ["enemy_support"] = "Stinger",
            // Bench reserves -- see Tools/AssetImport/import_bench_and_maps.py.
            ["player_bench_melee"] = "Thorne",
            ["player_bench_ranged"] = "Reed",
            ["player_bench_support"] = "Vesper",
        };

        static Sprite LoadBattleSprite(string unitId)
        {
            string path = $"Assets/Art/Generated/battle_sprites/char_{unitId}_battle.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[AI.Game] Expected battle sprite not found (run "
                + $"Tools/AssetImport/import_roster.py?): {path}");
            return sprite;
        }

        // Battle-map backgrounds curated by Tools/AssetImport/import_bench_and_maps.py,
        // same "not the ComfyUI manifest pipeline" provenance as LoadBattleSprite.
        static Sprite LoadBackgroundSprite(string key)
        {
            string path = $"Assets/Art/Generated/backgrounds/{key}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) Debug.LogWarning($"[AI.Game] Expected background not found (run "
                + $"Tools/AssetImport/import_bench_and_maps.py?): {path}");
            return sprite;
        }

        static VideoClip LoadClip(ManifestAsset asset, string unityRelRoot)
        {
            if (asset == null) return null;
            string path = $"{unityRelRoot}/{asset.output}";
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
            if (clip == null) Debug.LogWarning($"[AI.Game] Expected clip not found (not generated yet?): {path}");
            return clip;
        }

        // -- asset builders --------------------------------------------------------

        static TierDefinition BuildTier()
        {
            var tier = LoadOrCreate<TierDefinition>($"{OutDir}/Tiers/Tier_Standard.asset");
            tier.tier = Tier.C;
            tier.statMultiplier = 1f;
            tier.variance = 0.15f;
            tier.summonWeight = 1f;
            EditorUtility.SetDirty(tier);
            return tier;
        }

        /// <summary>The 3 potion assets BattleWorld.SeedPlaceholderInventory loads by
        /// path (M13). C-rank starting point -- there's no economy/shop system yet to
        /// roll/sell different ranks, so "the middle of F..SSS" is a placeholder, same
        /// spirit as TierDefinition defaulting to Tier.C for "Standard".</summary>
        static void BuildPotions()
        {
            BuildPotion("Potion_Hp", "HP Potion", PotionKind.Hp);
            BuildPotion("Potion_Mp", "MP Potion", PotionKind.Mp);
            BuildPotion("Potion_Multi", "Multi Potion", PotionKind.Multi);
        }

        static void BuildPotion(string assetName, string displayName, PotionKind kind)
        {
            var potion = LoadOrCreate<PotionDefinition>($"{OutDir}/Potions/{assetName}.asset");
            potion.potionId = assetName.ToLowerInvariant();
            potion.displayName = displayName;
            potion.kind = kind;
            potion.rank = Tier.C;
            potion.maxStack = 99;
            EditorUtility.SetDirty(potion);
        }

        static SkillPattern BuildPattern(ArchetypeSpec arch)
        {
            var pattern = LoadOrCreate<SkillPattern>($"{OutDir}/Patterns/Pattern_{arch.Name}Basic.asset");
            pattern.rangeOffsets = new List<Vector2Int>(arch.RangeOffsets);
            pattern.areaOffsets = new List<Vector2Int> { Vector2Int.zero };
            pattern.mirrorOnFacing = true;
            pattern.requiresLineOfSight = false;
            EditorUtility.SetDirty(pattern);
            return pattern;
        }

        static SkillDefinition BuildSkill(ArchetypeSpec arch, SkillPattern pattern)
        {
            var skill = LoadOrCreate<SkillDefinition>($"{OutDir}/Skills/Skill_{arch.Name}Basic.asset");
            skill.skillId = $"skill_{arch.Name.ToLowerInvariant()}_basic";
            skill.displayName = $"{arch.Name} Basic Attack";
            skill.pool = SkillPool.Standard;
            skill.pattern = pattern;
            skill.mpCost = 0;
            skill.cooldown = 0;
            skill.power = arch.Power;
            skill.usesMagic = arch.UsesMagic;
            skill.isRanged = arch.IsRanged;
            skill.targetsAllies = arch.TargetsAllies;
            skill.clipKey = "basicAttack"; // matches the ClipEntry.key built in BuildClipSet
            EditorUtility.SetDirty(skill);
            return skill;
        }

        /// <summary>Known-good impact-frame markers, authored directly here instead of
        /// read from manifest.export.json's own impact_frames field (M13 finding). That
        /// field's JSON is clean (verified by hand: [18], [22], [16,24]) but
        /// clipAsset.impact_frames deserializes to nonsense -- millions, on clips a few
        /// hundred frames long at most -- deterministically and reproducibly across
        /// independent Build() runs months apart, so this isn't disk corruption and a
        /// hand-edit of the built asset alone doesn't stick (the next Build() just
        /// overwrites it again from the same bad source). Root cause unconfirmed --
        /// suspected JsonUtility array-parsing edge case, never proven -- but bypassing
        /// the unreliable field for these 3 known clips sidesteps it entirely. Extend
        /// this table when new clips are added instead of trusting impact_frames again.</summary>
        static readonly Dictionary<string, int[]> KnownGoodImpactFrames = new()
        {
            ["clip_melee_basic"] = new[] { 18 },
            ["clip_ranged_basic"] = new[] { 22 },
            ["clip_heal_basic"] = new[] { 16, 24 },
        };

        static ClipSet BuildClipSet(ArchetypeSpec arch, ManifestAsset clipAsset, string unityRelRoot)
        {
            var clipSet = LoadOrCreate<ClipSet>($"{OutDir}/Clips/Clips_{arch.Name}Basic.asset");
            var entry = clipSet.clips.Find(c => c.key == "basicAttack") ?? new ClipEntry { key = "basicAttack" };
            entry.clip = LoadClip(clipAsset, unityRelRoot);
            entry.impactFrames = clipAsset != null && KnownGoodImpactFrames.TryGetValue(clipAsset.id, out var frames)
                ? new List<int>(frames)
                : new List<int>();
            entry.frameRate = clipAsset != null && clipAsset.fps > 0 ? clipAsset.fps : 24;
            entry.chromaKey = Color.green; // FOUNDATION.md: FMV clips are always keyed on solid #00FF00
            entry.chromaTolerance = 0.25f;
            entry.loop = false;

            if (!clipSet.clips.Contains(entry)) clipSet.clips.Add(entry);
            EditorUtility.SetDirty(clipSet);
            return clipSet;
        }

        static CharacterDefinition BuildCharacter(
            string unitId, ArchetypeSpec arch, TierDefinition tier,
            SkillDefinition standardSkill, ClipSet clipSet,
            ManifestAsset spriteAsset, ManifestAsset portraitAsset, string unityRelRoot,
            List<SkillDefinition> skillMoves = null)
        {
            var def = LoadOrCreate<CharacterDefinition>($"{OutDir}/Characters/Char_{unitId}.asset");
            def.characterId = unitId;
            def.displayName = RosterNames.TryGetValue(unitId, out var name) ? name : Titleize(unitId);
            def.classType = arch.ClassType;
            def.element = ElementType.Neutral;
            def.age = Age.Modern;
            def.baseStats = arch.BaseStats;
            def.maxMp = 100;
            def.movePoints = 4;
            def.jump = 1;
            def.costLateral = 2;
            def.costForward = 1;
            def.costPerHeightLevel = 1;
            def.standardSkill = standardSkill;
            def.skillMoves = skillMoves != null ? new List<SkillDefinition>(skillMoves) : new List<SkillDefinition>();
            def.growthPerLevel = 0.06f;
            def.clips = clipSet;
            def.portrait = LoadSprite(portraitAsset, unityRelRoot);
            def.pixelSprite32 = LoadSprite(spriteAsset, unityRelRoot);
            def.battleSprite = LoadBattleSprite(unitId);
            EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>Range/area = self-tile only (offset 0,0) -- since exactly one unit
        /// ever occupies a given column in this single-lane slice, a targetsAllies skill
        /// on this pattern always resolves to "self", no special-casing needed. Used by
        /// the frontline's defensive skill move.</summary>
        static SkillPattern BuildSelfOnlyPattern()
        {
            var pattern = LoadOrCreate<SkillPattern>($"{OutDir}/Patterns/Pattern_SelfOnly.asset");
            pattern.rangeOffsets = new List<Vector2Int> { Vector2Int.zero };
            pattern.areaOffsets = new List<Vector2Int> { Vector2Int.zero };
            pattern.mirrorOnFacing = true;
            pattern.requiresLineOfSight = false;
            EditorUtility.SetDirty(pattern);
            return pattern;
        }

        /// <summary>Same range as the Ranged archetype's BA (any column), but a 3-wide
        /// area centred on the chosen column -- BattleController.ResolveAction treats any
        /// pattern with more than one areaOffset as AoE and hits every unit the area
        /// covers via TargetResolver.GetAreaTargets. Used by ranged's AoE skill move.</summary>
        static SkillPattern BuildVolleyPattern(List<Vector2Int> rangeOffsets)
        {
            var pattern = LoadOrCreate<SkillPattern>($"{OutDir}/Patterns/Pattern_RangedVolley.asset");
            pattern.rangeOffsets = new List<Vector2Int>(rangeOffsets);
            pattern.areaOffsets = new List<Vector2Int> { new(0, -1), new(0, 0), new(0, 1) };
            pattern.mirrorOnFacing = true;
            pattern.requiresLineOfSight = false;
            EditorUtility.SetDirty(pattern);
            return pattern;
        }

        /// <summary>Range = ±1 column incl. self (same shape as the Support archetype's
        /// Heal), area = ±1 column around the chosen ally -- an AoE heal instead of Heal's
        /// single target. Used by the healer's Mass Heal skill move.</summary>
        static SkillPattern BuildWideHealPattern()
        {
            var pattern = LoadOrCreate<SkillPattern>($"{OutDir}/Patterns/Pattern_SupportWide.asset");
            pattern.rangeOffsets = new List<Vector2Int> { new(0, -1), new(0, 0), new(0, 1) };
            pattern.areaOffsets = new List<Vector2Int> { new(0, -1), new(0, 0), new(0, 1) };
            pattern.mirrorOnFacing = true;
            pattern.requiresLineOfSight = false;
            EditorUtility.SetDirty(pattern);
            return pattern;
        }

        /// <summary>Same range as the Ranged archetype's BA, but an area wide enough
        /// (±5 columns) to guarantee covering the whole opposing formation regardless of
        /// which column is aimed at -- this map only ever has 3 columns per side, so ±5
        /// is "always full width" with margin to spare. Used by ranged's Barrage skill
        /// move, the "guaranteed AoE" tier above Volley's 3-wide cluster.</summary>
        static SkillPattern BuildBarragePattern(List<Vector2Int> rangeOffsets)
        {
            var pattern = LoadOrCreate<SkillPattern>($"{OutDir}/Patterns/Pattern_RangedBarrage.asset");
            pattern.rangeOffsets = new List<Vector2Int>(rangeOffsets);
            pattern.areaOffsets = Enumerable.Range(-5, 11).Select(c => new Vector2Int(0, c)).ToList();
            pattern.mirrorOnFacing = true;
            pattern.requiresLineOfSight = false;
            EditorUtility.SetDirty(pattern);
            return pattern;
        }

        static SkillDefinition BuildSkillMove(string assetName, string displayName, SkillPattern pattern,
            float power, bool usesMagic, bool targetsAllies, int mpCost, bool isRanged = false, bool restoresMana = false,
            StatusEffectType inflictsStatus = StatusEffectType.None, float statusMagnitude = 0f, int statusDuration = 0)
        {
            var skill = LoadOrCreate<SkillDefinition>($"{OutDir}/Skills/{assetName}.asset");
            skill.skillId = assetName.ToLowerInvariant();
            skill.displayName = displayName;
            skill.pool = SkillPool.Standard;
            skill.pattern = pattern;
            skill.mpCost = mpCost;
            skill.cooldown = 0;
            skill.power = power;
            skill.usesMagic = usesMagic;
            skill.isRanged = isRanged;
            skill.targetsAllies = targetsAllies;
            skill.restoresMana = restoresMana;
            skill.inflictsStatus = inflictsStatus;
            skill.statusMagnitude = statusMagnitude;
            skill.statusDuration = statusDuration;
            skill.clipKey = "basicAttack";
            EditorUtility.SetDirty(skill);
            return skill;
        }

        static (Sprite, List<Vector4>) LoadFxSheet()
        {
            const string pngPath = "Assets/Art/Generated/fx/fx_hit_impact_sheet.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pngPath);
            var rects = new List<Vector4>();
            string jsonFullPath = Path.Combine(Application.dataPath, "Art/Generated/fx/fx_hit_impact_sheet.json");
            if (sprite == null || !File.Exists(jsonFullPath))
            {
                Debug.LogWarning("[AI.Game] fx_hit_impact_sheet not found -- hit VFX will be skipped.");
                return (sprite, rects);
            }
            var meta = JsonUtility.FromJson<FxSheetMeta>(File.ReadAllText(jsonFullPath));
            foreach (var f in meta.frames) rects.Add(new Vector4(f.x, f.y, f.w, f.h));
            return (sprite, rects);
        }

        /// <summary>Map 2's 3 enemy Skill Moves (M14) -- each pairs damage with the
        /// status effect that gives that enemy its identity: Rotfang applies Poison,
        /// Deadeye applies Attack Down, Hexweaver applies Defense Down (plus a plain Heal
        /// so it can sustain its own side, same "healers can attack/support" pattern as
        /// the player roster). Reuses the same 3 base patterns every archetype already
        /// shares -- these enemies don't need new targeting shapes, just a new kit.
        /// Numbers are arbitrary (project owner direction), not a tuned balance pass.</summary>
        static Dictionary<string, SkillDefinition> BuildMap2EnemySkills(
            SkillPattern meleePattern, SkillPattern rangedPattern, SkillPattern supportPattern)
        {
            var venomStrike = BuildSkillMove("Skill_EnemyVenomStrike", "Venom Strike",
                meleePattern, power: 1.5f, usesMagic: false, targetsAllies: false, mpCost: 25,
                inflictsStatus: StatusEffectType.Poison, statusMagnitude: 12f, statusDuration: 3);
            var cripplingShot = BuildSkillMove("Skill_EnemyCripplingShot", "Crippling Shot",
                rangedPattern, power: 1.4f, usesMagic: false, targetsAllies: false, mpCost: 25, isRanged: true,
                inflictsStatus: StatusEffectType.AttackDown, statusMagnitude: 0.25f, statusDuration: 2);
            var hexweaverHeal = BuildSkillMove("Skill_EnemyHexweaverHeal", "Heal",
                supportPattern, power: 1.0f, usesMagic: true, targetsAllies: true, mpCost: 20);
            var weaken = BuildSkillMove("Skill_EnemyWeaken", "Weaken",
                supportPattern, power: 0.8f, usesMagic: true, targetsAllies: false, mpCost: 25,
                inflictsStatus: StatusEffectType.DefenseDown, statusMagnitude: 0.25f, statusDuration: 2);

            return new Dictionary<string, SkillDefinition>
            {
                ["venomStrike"] = venomStrike,
                ["cripplingShot"] = cripplingShot,
                ["hexweaverHeal"] = hexweaverHeal,
                ["weaken"] = weaken,
            };
        }

        /// <summary>Map 2's 3 enemies (M14), reusing Thorne/Reed/Vesper's already-built
        /// art+clips (BuildCharacter already loaded those into characterDefs for the
        /// bench) rather than any new generated assets -- explicitly deferred by the
        /// project owner. Hand-authored stats (a step up from map 1's Husk/Warden/
        /// Stinger, which reuse the player archetypes' own numbers verbatim) instead of
        /// going through ArchetypeSpec, since these aren't archetype reskins.</summary>
        static Dictionary<string, CharacterDefinition> BuildMap2Enemies(
            Dictionary<string, CharacterDefinition> characterDefs, Dictionary<string, SkillDefinition> skills)
        {
            var rotfangBa = BuildSkillMove("Skill_EnemyRotfangBasic", "Bite",
                characterDefs["enemy_melee"].standardSkill.pattern, power: 1.2f, usesMagic: false, targetsAllies: false, mpCost: 0);
            var deadeyeBa = BuildSkillMove("Skill_EnemyDeadeyeBasic", "Snipe Shot",
                characterDefs["enemy_ranged"].standardSkill.pattern, power: 1.0f, usesMagic: false, targetsAllies: false, mpCost: 0, isRanged: true);
            var hexweaverBa = BuildSkillMove("Skill_EnemyHexweaverBasic", "Hex Bolt",
                characterDefs["enemy_support"].standardSkill.pattern, power: 0.5f, usesMagic: false, targetsAllies: false, mpCost: 0);

            var rotfang = BuildCustomEnemy("enemy_rotfang", "Rotfang", ClassType.Warrior,
                new StatBlock { hp = 150, attack = 26, defense = 16, magic = 4, resistance = 8, speed = 8 },
                rotfangBa, new List<SkillDefinition> { skills["venomStrike"] },
                characterDefs["player_bench_melee"]);

            var deadeye = BuildCustomEnemy("enemy_deadeye", "Deadeye", ClassType.Ranger,
                new StatBlock { hp = 100, attack = 24, defense = 8, magic = 8, resistance = 8, speed = 13 },
                deadeyeBa, new List<SkillDefinition> { skills["cripplingShot"] },
                characterDefs["player_bench_ranged"]);

            var hexweaver = BuildCustomEnemy("enemy_hexweaver", "Hexweaver", ClassType.Healer,
                new StatBlock { hp = 105, attack = 10, defense = 10, magic = 22, resistance = 14, speed = 10 },
                hexweaverBa, new List<SkillDefinition> { skills["hexweaverHeal"], skills["weaken"] },
                characterDefs["player_bench_support"]);

            return new Dictionary<string, CharacterDefinition>
            {
                ["enemy_rotfang"] = rotfang,
                ["enemy_deadeye"] = deadeye,
                ["enemy_hexweaver"] = hexweaver,
            };
        }

        /// <summary>Like BuildCharacter, but for a unit that isn't one of the 3 shared
        /// archetypes and has no manifest sprite/portrait/clip entries of its own --
        /// borrows every art reference (battleSprite/portrait/pixelSprite32/clips)
        /// directly from an already-built CharacterDefinition instead. No tier parameter
        /// -- tier isn't stored on CharacterDefinition, it's applied per-EnemyPlacement
        /// (see BuildMap2Enemies' callers).</summary>
        static CharacterDefinition BuildCustomEnemy(string unitId, string displayName, ClassType classType,
            StatBlock baseStats, SkillDefinition standardSkill, List<SkillDefinition> skillMoves,
            CharacterDefinition artSource)
        {
            var def = LoadOrCreate<CharacterDefinition>($"{OutDir}/Characters/Char_{unitId}.asset");
            def.characterId = unitId;
            def.displayName = displayName;
            def.classType = classType;
            def.element = ElementType.Neutral;
            def.age = Age.Modern;
            def.baseStats = baseStats;
            def.maxMp = 100;
            def.movePoints = 4;
            def.jump = 1;
            def.costLateral = 2;
            def.costForward = 1;
            def.costPerHeightLevel = 1;
            def.standardSkill = standardSkill;
            def.skillMoves = new List<SkillDefinition>(skillMoves);
            def.growthPerLevel = 0.06f;
            def.clips = artSource.clips;
            def.portrait = artSource.portrait;
            def.pixelSprite32 = artSource.pixelSprite32;
            def.battleSprite = artSource.battleSprite;
            EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>Builds one battle map from an explicit enemy placement list --
        /// caller's choice, not derived here. Through M13 both maps reused the same 3
        /// enemy archetypes; M14 gave map 2 its own distinct roster (Rotfang/Deadeye/
        /// Hexweaver, see BuildMap2Enemies) while map 1 keeps Husk/Warden/Stinger, so
        /// this method itself stopped caring which map it's building for.</summary>
        static void BuildMap(int mapNumber, List<EnemyPlacement> enemies,
            Sprite backgroundSprite, Sprite fxSheet, List<Vector4> fxRects)
        {
            // Side-view formation: a single lane, column = horizontal rank (see archetypes
            // comment above). Player ranks 0(back)-2(front); enemy ranks 3(front)-5(back) --
            // the two front-liners land adjacent (2 vs 3) so melee's 1-column range meets.
            const int lanes = 1, cols = 6;
            var map = LoadOrCreate<MapDefinition>($"{OutDir}/Maps/Map_BattleSlice{mapNumber}.asset");
            map.laneCount = lanes;
            map.columnCount = cols;
            map.backgroundSprite = backgroundSprite;
            map.fxImpactSheet = fxSheet;
            map.fxImpactFrameRects = fxRects;

            map.tiles = new List<TileData>(lanes * cols);
            for (int i = 0; i < lanes * cols; i++)
                map.tiles.Add(new TileData { height = 0, terrain = TerrainType.Plain, blocked = false });

            map.playerDeployTiles = new List<Vector2Int>
            {
                new(0, 0), new(0, 1), new(0, 2),
            };

            map.enemies = enemies;

            EditorUtility.SetDirty(map);
        }

        // Dictionary.GetValueOrDefault isn't available under this project's .NET Standard
        // 2.0 API compatibility level (ProjectSettings apiCompatibilityLevel: 6) -- roll our own.
        static List<ManifestAsset> GetOrEmpty(Dictionary<string, List<ManifestAsset>> dict, string key) =>
            dict.TryGetValue(key, out var list) ? list : new List<ManifestAsset>();

        /// <summary>`preferred` unless it's null-or-destroyed by Unity's own equality
        /// rules. A plain `a ?? b` compares CLR references and so treats Unity's
        /// "fake null" wrapper as a live object.</summary>
        static T OrFallback<T>(T preferred, T fallback) where T : UnityEngine.Object =>
            preferred == null ? fallback : preferred;

        static TValue TryGet<TValue>(Dictionary<string, TValue> dict, string key) where TValue : class =>
            dict.TryGetValue(key, out var value) ? value : null;

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static string Titleize(string unitId) =>
            string.Join(" ", unitId.Split('_').Select(w => char.ToUpperInvariant(w[0]) + w.Substring(1)));

        // -- archetype placeholder tuning -------------------------------------------

        class ArchetypeSpec
        {
            public readonly string Name;
            public readonly string ClipAssetId;
            public readonly ClassType ClassType;
            public readonly bool IsRanged;
            public readonly bool UsesMagic;
            public readonly bool TargetsAllies;
            public readonly float Power;
            public readonly List<Vector2Int> RangeOffsets;
            public readonly StatBlock BaseStats;

            public ArchetypeSpec(string name, string clipAssetId, ClassType classType, bool isRanged, bool usesMagic,
                float power, List<Vector2Int> rangeOffsets, bool targetsAllies = false)
            {
                Name = name;
                ClipAssetId = clipAssetId;
                ClassType = classType;
                IsRanged = isRanged;
                UsesMagic = usesMagic;
                TargetsAllies = targetsAllies;
                Power = power;
                RangeOffsets = rangeOffsets;
                BaseStats = name switch
                {
                    "Melee" => new StatBlock { hp = 120, attack = 22, defense = 14, magic = 6, resistance = 8, speed = 9 },
                    "Ranged" => new StatBlock { hp = 90, attack = 18, defense = 8, magic = 8, resistance = 8, speed = 11 },
                    "Support" => new StatBlock { hp = 95, attack = 8, defense = 9, magic = 20, resistance = 12, speed = 10 },
                    _ => new StatBlock { hp = 100, attack = 15, defense = 10, magic = 10, resistance = 10, speed = 10 },
                };
            }
        }

        // -- manifest.export.json mirror (field names match the JSON keys exactly for JsonUtility) --

        [Serializable]
        class ManifestAsset
        {
            public string id;
            public string type;
            public string unit_id;
            public int[] impact_frames;
            public int fps;
            public string output;
        }

        [Serializable]
        class ManifestExport
        {
            public List<ManifestAsset> assets;
            public string outputRoot;
        }

        // fx_hit_impact_sheet.json mirror (written by both generate.py's postprocess.py
        // and Tools/AssetImport/import_roster.py -- same shape either way)
        [Serializable]
        class FxFrameRect
        {
            public int index;
            public int x;
            public int y;
            public int w;
            public int h;
        }

        [Serializable]
        class FxSheetMeta
        {
            public int frameWidth;
            public int frameHeight;
            public List<FxFrameRect> frames;
        }
    }
}
#endif
