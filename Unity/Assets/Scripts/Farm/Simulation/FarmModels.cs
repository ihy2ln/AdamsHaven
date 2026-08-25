using System;
using System.Collections.Generic;

namespace Game.Farm
{
    public enum FarmLocation { Town, Dungeon }
    public enum FarmPlotRule { Town, Dungeon, Both }
    public enum FarmTool { Hoe, WateringCan, Scythe, Axe, Pickaxe }
    public enum FarmSoilState { Untilled, Tilled }

    public enum FarmActionCode
    {
        None,
        Moved,
        Tilled,
        Watered,
        Fertilized,
        ObstacleCleared,
        Planted,
        Harvested,
        BattleApplied,
        ExportedToTown,
        Failed
    }

    public enum FarmEventType
    {
        ItemChanged,
        SoilChanged,
        CropPlanted,
        CropHarvested,
        ObstacleCleared,
        FarmLevelGained,
        BattleCompleted,
        ExportedToTown
    }

    [Serializable]
    public struct FarmPosition : IEquatable<FarmPosition>
    {
        public int x;
        public int y;

        public FarmPosition(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public bool Equals(FarmPosition other) => x == other.x && y == other.y;
        public override bool Equals(object obj) => obj is FarmPosition other && Equals(other);
        public override int GetHashCode() => (x * 397) ^ y;
    }

    [Serializable]
    public sealed class FarmCropState
    {
        public string cropId = "";
        public long plantedAtUnix;
        public int plantedAtBattleCount;
        public int harvestsTaken;
    }

    [Serializable]
    public sealed class FarmTileState
    {
        public int x;
        public int y;
        public FarmLocation location;
        public FarmSoilState soil;
        public string obstacleId = "";
        public bool watered;
        public string fertilizerId = "";
        public FarmCropState crop;

        public FarmPosition Position => new FarmPosition(x, y);
    }

    [Serializable]
    public sealed class FarmItemStack
    {
        public string itemId = "";
        public int quantity;

        public FarmItemStack() { }

        public FarmItemStack(string itemId, int quantity)
        {
            this.itemId = itemId;
            this.quantity = quantity;
        }
    }

    [Serializable]
    public sealed class FarmPlayerProgress
    {
        public int x;
        public int y;
        public int facingX = 1;
        public int facingY;
        public int farmLevel = 1;
        public int farmXp;
    }

    [Serializable]
    public sealed class FarmSaveData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public string farmId = "farm_clearing_16x16";
        public string displayName = "Starter Plot";
        public int width;
        public int height;
        public int totalBattles;
        public int totalHarvests;
        public int clearedObstacles;
        public FarmPlayerProgress player = new FarmPlayerProgress();
        public List<FarmTileState> tiles = new List<FarmTileState>();
        public List<FarmItemStack> inventory = new List<FarmItemStack>();
    }

    public sealed class FarmActionResult
    {
        public bool Succeeded { get; private set; }
        public FarmActionCode Code { get; private set; }
        public string Message { get; private set; }
        public FarmPosition Position { get; private set; }
        public int XpGained { get; private set; }
        public int LevelsGained { get; private set; }
        public string ItemId { get; private set; }
        public int Quantity { get; private set; }

        public static FarmActionResult Success(
            FarmActionCode code,
            string message,
            FarmPosition position,
            int xpGained = 0,
            int levelsGained = 0,
            string itemId = "",
            int quantity = 0)
        {
            return new FarmActionResult
            {
                Succeeded = true,
                Code = code,
                Message = message,
                Position = position,
                XpGained = xpGained,
                LevelsGained = levelsGained,
                ItemId = itemId,
                Quantity = quantity
            };
        }

        public static FarmActionResult Fail(string message, FarmPosition position)
        {
            return new FarmActionResult
            {
                Code = FarmActionCode.Failed,
                Message = message,
                Position = position,
                ItemId = ""
            };
        }
    }

    public sealed class FarmEvent
    {
        public FarmEventType Type { get; }
        public string SubjectId { get; }
        public int Amount { get; }
        public FarmPosition Position { get; }

        public FarmEvent(FarmEventType type, string subjectId, int amount, FarmPosition position)
        {
            Type = type;
            SubjectId = subjectId ?? "";
            Amount = amount;
            Position = position;
        }
    }
}
