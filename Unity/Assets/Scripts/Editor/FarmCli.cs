#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Explicit farm-only entry points for the external CLI/MCP bridge.
    /// No battle scene or battle asset is touched by these methods.
    /// </summary>
    public static class FarmCli
    {
        const string FarmScene = "Assets/Scenes/Farm.unity";

        public static void EnsureScene() => FarmBatchSetup.EnsureFarmScene();

        public static void RefreshAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            Debug.Log("[AI.Game] Farm assets refreshed.");
            EditorApplication.Exit(0);
        }

        public static void Validate()
        {
            var sceneExists = File.Exists(FarmScene);
            var map = Resources.Load<TextAsset>("Farm/farm-map-5x5");
            var sprites = Directory.Exists("Assets/Resources/Farm/Art/Plants")
                ? Directory.GetFiles("Assets/Resources/Farm/Art/Plants", "*.png").Length
                : 0;
            Debug.Log($"[AI.Game] Farm validation: scene={sceneExists}, map={map != null}, plantSprites={sprites}");
            EditorApplication.Exit(sceneExists && map != null ? 0 : 1);
        }
    }
}
#endif
