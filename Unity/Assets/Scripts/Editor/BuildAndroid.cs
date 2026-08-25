#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Android release build of the battle + farm scene flow. Player settings are set here in
    /// script, not by hand in the Inspector -- matches the project's code-first
    /// convention. No adb on this dev machine, so this produces the APK but cannot
    /// install/verify on a physical device; that step needs to happen on a machine
    /// (or session) that has Android platform tools.
    ///
    /// Outputs to S:\AI\Game\play\android\ -- see BuildBattleStandalone's doc comment
    /// for why "play" (ready-to-launch builds) is a sibling of "test" (this repo's new
    /// home under S:\AI\Game\test\AI.Game), not a subfolder of it.
    /// </summary>
    public static class BuildAndroid
    {
        [MenuItem("AI.Game/Battle/Build Android APK")]
        public static void Build()
        {
            BattleSceneBuilder.CreateBattleScene();

            PlayerSettings.productName = "Adams Haven";
            PlayerSettings.companyName = "AI.Game";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.adamshaven.game");
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            EditorUserBuildSettings.buildAppBundle = false;
            // Debug keystore is fine -- this is sideloaded, never shipped to a store.
            PlayerSettings.Android.useCustomKeystore = false;

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Battle.unity", "Assets/Scenes/Farm.unity" },
                locationPathName = "S:/AI/Game/play/android/AI.Game-Battle-v0.4.0-debug.apk",
                target = BuildTarget.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;
            Debug.Log($"[AI.Game] Android build result: {summary.result}, "
                + $"{summary.totalErrors} errors, {summary.totalWarnings} warnings, "
                + $"output: {summary.outputPath}, size: {summary.totalSize / 1024 / 1024}MB");

            if (summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                EditorApplication.Exit(1);
        }
    }
}
#endif
