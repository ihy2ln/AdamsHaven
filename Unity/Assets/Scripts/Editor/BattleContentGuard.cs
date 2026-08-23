#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// Keeps Resources/Battle in sync with whatever BattleAssetBuilder currently authors,
    /// without anyone having to remember the menu item.
    ///
    /// Why this exists: M10 added a 9-skill Skill Move system entirely in
    /// BattleAssetBuilder's C#, but nothing ever wrote it to the ScriptableObjects --
    /// `AI.Game > Battle > Build Assets From Manifest` was never actually clicked. The
    /// game ran, compiled clean, and passed its tests, but every CharacterDefinition on
    /// disk still had an empty `skillMoves` list, so BattleHud's "SM" button was
    /// permanently greyed out and MP never drained (auto mode fell through to the free
    /// 0-cost BA every turn). Nothing surfaced that -- it looked like a UI bug, and cost
    /// two sessions. A stale-content check on load removes the whole class of problem.
    ///
    /// Runs only in an interactive Editor session. `Application.isBatchMode` bails out
    /// deliberately: headless -batchmode runs that call AssetDatabase.SaveAssets() after a
    /// script change are the documented cause of `m_Script: {fileID: 0}` corruption across
    /// every asset in this folder (see PROJECT-README.md "Known gaps"), so this must never
    /// fire there. EditMode tests are unaffected -- BattleAssetContentTests asserts the
    /// same invariants read-only, and is the thing that fails loudly if content is stale
    /// on a machine where this guard hasn't run yet.
    /// </summary>
    [InitializeOnLoad]
    public static class BattleContentGuard
    {
        const string CharacterDir = "Battle/Characters";
        const string PotionDir = "Battle/Potions";

        static BattleContentGuard()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += RunWhenIdle;
        }

        /// <summary>Building assets mid-compile, mid-import or in play mode either throws
        /// or gets thrown away by the following domain reload -- defer until the Editor is
        /// actually settled. delayCall re-arms once per editor tick, so this costs nothing
        /// while waiting and stops as soon as it gets a clean window.</summary>
        static void RunWhenIdle()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating
                || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RunWhenIdle;
                return;
            }

            if (!IsStale(out string reason)) return;

            Debug.Log($"[AI.Game] Battle content is stale ({reason}) -- rebuilding "
                + "Resources/Battle automatically. Run AI.Game > Battle > Build Assets "
                + "From Manifest by hand if you ever need to force this.");
            BattleAssetBuilder.Build();

            if (IsStale(out string stillBroken))
                Debug.LogError($"[AI.Game] Battle content is STILL stale after a rebuild: {stillBroken}. "
                    + "The build likely failed earlier in the log (missing manifest.export.json?).");
        }

        /// <summary>Two independent checks, either of which forces a rebuild. The version
        /// stamp catches "the builder's C# authors something newer than what's on disk".
        /// The content probe catches "the assets on disk are wrong right now" regardless of
        /// what the stamp claims -- e.g. after a git revert of Resources/Battle, or if a
        /// previous build half-failed.</summary>
        static bool IsStale(out string reason)
        {
            int built = EditorPrefs.GetInt(BattleAssetBuilder.ContentVersionPrefKey, 0);
            if (built != BattleAssetBuilder.ContentVersion)
            {
                reason = $"last built as content v{built}, builder now authors v{BattleAssetBuilder.ContentVersion}";
                return true;
            }

            var problems = FindContentProblems();
            reason = problems.Count > 0 ? string.Join("; ", problems) : null;
            return problems.Count > 0;
        }

        /// <summary>Read-only probe of the built CharacterDefinitions. Mirrors what
        /// BattleAssetContentTests asserts -- kept in the Editor assembly rather than
        /// shared, since Game.Tests (an asmdef) can't reference Assembly-CSharp-Editor.</summary>
        static List<string> FindContentProblems()
        {
            var defs = Resources.LoadAll<CharacterDefinition>(CharacterDir);
            var problems = new List<string>();

            if (defs.Length == 0) return new List<string> { "no CharacterDefinitions in Resources/" + CharacterDir };

            foreach (var def in defs.OrderBy(d => d.name))
            {
                if (def.standardSkill == null) problems.Add($"{def.name} has no standardSkill (BA)");
                int moves = def.skillMoves?.Count(s => s != null) ?? 0;
                if (moves == 0) problems.Add($"{def.name} has no skillMoves (SM would be greyed out)");
                if (def.maxMp <= 0) problems.Add($"{def.name} has maxMp {def.maxMp}");

                // M16/M17: the same three silent-inertness failures BattleAssetContentTests
                // asserts. Each one looks like working content -- the battle runs fine -- it
                // just quietly never triggers the system it belongs to: Neutral is a 1x
                // no-op through ElementChart, a null ultimate leaves the U button unable to
                // ever enable, and accuracy 0 is read as "always hits" by
                // DamageCalculator.HitChance while critRate 0 simply never crits. Exactly
                // the shape of the M10 Skill Move bug this guard exists to prevent.
                if (def.element == ElementType.Neutral)
                    problems.Add($"{def.name} is Neutral (elements would be a no-op for it)");
                if (def.ultimateSkill == null)
                    problems.Add($"{def.name} has no ultimateSkill (the U button could never enable)");
                if (def.baseStats.critRate <= 0f || def.baseStats.accuracy <= 0f)
                    problems.Add($"{def.name} has unauthored combat stats "
                        + $"(critRate {def.baseStats.critRate}, accuracy {def.baseStats.accuracy})");
            }

            // M13: the 3 battle-inventory potions BattleWorld.SeedPlaceholderInventory
            // loads by exact path -- missing any one leaves that Item slot permanently
            // empty with only a Console warning to explain why.
            foreach (var potionPath in new[] { "Potion_Hp", "Potion_Mp", "Potion_Multi" })
            {
                if (Resources.Load<PotionDefinition>($"{PotionDir}/{potionPath}") == null)
                    problems.Add($"missing PotionDefinition at Resources/{PotionDir}/{potionPath}");
            }

            return problems;
        }
    }
}
#endif
