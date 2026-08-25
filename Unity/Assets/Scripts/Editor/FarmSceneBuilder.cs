#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Farm;

namespace Game.EditorTools
{
    public static class FarmSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Farm.unity";

        [MenuItem("AI.Game/Farm/Create Starter Scene")]
        public static void CreateStarterScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("FarmBootstrap");
            bootstrap.AddComponent<FarmBootstrap>();

            var light = new GameObject("PlaceholderLight");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            const string battlePath = "Assets/Scenes/Battle.unity";
            if (File.Exists(battlePath)) scenes.Add(new EditorBuildSettingsScene(battlePath, true));
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[AI.Game] Created {ScenePath} and set as build scene 0.");
        }

        [MenuItem("AI.Game/Farm/Open Starter Scene")]
        public static void OpenStarterScene()
        {
            if (!File.Exists(ScenePath)) CreateStarterScene();
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
#endif
