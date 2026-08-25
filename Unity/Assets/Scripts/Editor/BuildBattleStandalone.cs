#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Windows standalone build of just the battle scene -- for verifying the auto-battle
    /// slice actually plays (there's no Android adb on this dev machine, and headless
    /// batchmode can compile/test logic but can't show what it looks like on screen).
    /// Not a shipping build config, just a fast local verification build.
    ///
    /// Outputs to S:\AI\Game\play\windows\, NOT into this repo -- "play" is a sibling of
    /// "test" (this repo's new home under S:\AI\Game\test\AI.Game) so a from-source
    /// build and a ready-to-launch build never live in the same tree. Absolute path is
    /// deliberate; this project already pins several paths to this machine's layout
    /// (the Unity Editor install, the curated art library) rather than pretending to be
    /// portable across machines.
    /// </summary>
    public static class BuildBattleStandalone
    {
        [MenuItem("AI.Game/Battle/Build Windows Standalone (dev)")]
        public static void Build()
        {
            BattleSceneBuilder.CreateBattleScene();

            // All location scenes ride along so Camp / Pause travel works in a real
            // standalone build, not just inside the Editor's Build Settings list.
            var scenes = new List<string> { "Assets/Scenes/Battle.unity" };
            foreach (var optional in new[]
            {
                "Assets/Scenes/Farm.unity", "Assets/Scenes/Town.unity",
                "Assets/Scenes/Camp.unity", "Assets/Scenes/Home.unity"
            })
                if (File.Exists(optional)) scenes.Add(optional);

            var options = new BuildPlayerOptions
            {
                scenes = scenes.ToArray(),
                locationPathName = "S:/AI/Game/play/windows/AI.Game-Battle.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[AI.Game] Build result: {summary.result}, "
                + $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, "
                + $"output: {summary.outputPath}");

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
#endif
