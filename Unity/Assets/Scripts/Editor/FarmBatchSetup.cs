#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Farm;

namespace Game.EditorTools
{
    public static class FarmBatchSetup
    {
        public static void EnsureFarmScene()
        {
            const string scenePath = "Assets/Scenes/Farm.unity";
            Directory.CreateDirectory("Assets/Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrap = new GameObject("FarmBootstrap");
            bootstrap.AddComponent<FarmBootstrap>();
            EditorSceneManager.SaveScene(scene, scenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };

            PlayerSettings.productName = "Adams Haven";
            PlayerSettings.companyName = "AI.Game";
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.adamshaven.game");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            AssetDatabase.SaveAssets();
            Debug.Log($"[AI.Game] Ensured {scenePath}");
            EditorApplication.Exit(0);
        }
    }
}
#endif
