using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Farm;

/// <summary>
/// Runs once on batchmode import to generate Farm.unity without menu clicks.
/// </summary>
[InitializeOnLoad]
public static class FarmAutoSetup
{
    const string FlagKey = "AIGame.FarmSceneCreated";

    static FarmAutoSetup()
    {
        if (SessionState.GetBool(FlagKey, false)) return;
        EditorApplication.delayCall += Run;
    }

    static void Run()
    {
        if (SessionState.GetBool(FlagKey, false)) return;
        SessionState.SetBool(FlagKey, true);

        const string scenePath = "Assets/Scenes/Farm.unity";
        if (!System.IO.File.Exists(scenePath))
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("FarmBootstrap");
            bootstrap.AddComponent<FarmBootstrap>();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[AI.Game] Auto-created Farm.unity");
        }

        // Ensure product name
        PlayerSettings.productName = "Adams Haven";
        PlayerSettings.companyName = "AI.Game";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.adamshaven.game");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
    }
}
