using System;
using System.IO;
using UnityEngine;

namespace Game.Farm
{
    /// <summary>Client-authoritative local save boundary; cloud transport can wrap this state later.</summary>
    public sealed class FarmSaveRepository
    {
        const string FileName = "adams-haven-farm.json";

        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        public FarmSaveData LoadOrCreate()
        {
            try
            {
                if (!File.Exists(SavePath)) return FarmStarterContent.CreateNewGame();
                var state = JsonUtility.FromJson<FarmSaveData>(File.ReadAllText(SavePath));
                if (state == null || state.schemaVersion != FarmSaveData.CurrentSchemaVersion)
                    return FarmStarterContent.CreateNewGame();
                return UpgradeToAuthoredClearing(state);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[Adams Haven] Farm save could not be loaded: " + exception.Message);
                return FarmStarterContent.CreateNewGame();
            }
        }

        public void Save(FarmSaveData state)
        {
            if (state == null) return;
            var directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temporaryPath = SavePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(state, true));
            if (File.Exists(SavePath)) File.Delete(SavePath);
            File.Move(temporaryPath, SavePath);
        }

        static FarmSaveData UpgradeToAuthoredClearing(FarmSaveData state)
        {
            if (state.width == FarmStarterContent.Width && state.height == FarmStarterContent.Height)
                return state;

            var upgraded = FarmStarterContent.CreateNewGame();
            upgraded.schemaVersion = state.schemaVersion;
            upgraded.farmId = FarmStarterMap.Id;
            upgraded.displayName = state.displayName;
            upgraded.totalBattles = state.totalBattles;
            upgraded.totalHarvests = state.totalHarvests;
            upgraded.clearedObstacles = state.clearedObstacles;
            upgraded.player = state.player ?? new FarmPlayerProgress();
            upgraded.player.x = Mathf.Clamp(upgraded.player.x, 0, upgraded.width - 1);
            upgraded.player.y = Mathf.Clamp(upgraded.player.y, 0, upgraded.height - 1);
            upgraded.inventory = state.inventory ?? new System.Collections.Generic.List<FarmItemStack>();

            if (state.tiles != null)
                foreach (var oldTile in state.tiles)
                {
                    if (oldTile == null || oldTile.x < 0 || oldTile.y < 0 ||
                        oldTile.x >= upgraded.width || oldTile.y >= upgraded.height) continue;
                    var newTile = upgraded.tiles[oldTile.y * upgraded.width + oldTile.x];
                    newTile.soil = oldTile.soil;
                    newTile.obstacleId = oldTile.obstacleId;
                    newTile.watered = oldTile.watered;
                    newTile.fertilizerId = oldTile.fertilizerId;
                    newTile.crop = oldTile.crop;
                }
            return upgraded;
        }
    }
}
