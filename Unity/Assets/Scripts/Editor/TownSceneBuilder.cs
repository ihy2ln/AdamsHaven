#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Town;

namespace Game.EditorTools
{
    /// <summary>Mirrors FarmSceneBuilder/BattleSceneBuilder's pattern -- an Editor-only
    /// menu item so scene creation (AssetDatabase.SaveAssets/CreateAsset) only ever runs
    /// from an interactive session, never headless batchmode (see PROJECT-README's
    /// "Known gaps" on why). Appends to whatever's already in EditorBuildSettings rather
    /// than replacing it, unlike FarmSceneBuilder's own version of this -- by now there
    /// are three scenes to keep, not two.</summary>
    public static class TownSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/Town.unity";

        [MenuItem("AI.Game/Town/Create Starter Scene")]
        public static void CreateStarterScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var bootstrap = new GameObject("TownBootstrap");
            bootstrap.AddComponent<TownBootstrap>();

            var light = new GameObject("PlaceholderLight");
            var l = light.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = 1f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Adams Haven] Created {ScenePath} and added it to Build Settings.");
        }

        [MenuItem("AI.Game/Town/Open Starter Scene")]
        public static void OpenStarterScene()
        {
            if (!File.Exists(ScenePath)) CreateStarterScene();
            EditorSceneManager.OpenScene(ScenePath);
        }

        static void AddToBuildSettings(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Any(s => s.path == scenePath)) return;
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
