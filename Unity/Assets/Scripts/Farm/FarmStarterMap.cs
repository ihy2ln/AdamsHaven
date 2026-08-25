using System;
using UnityEngine;

namespace Game.Farm
{
    [Serializable]
    public sealed class FarmObstacleType
    {
        public string id;
        public string label;
        public int requiredLevel = 1;
        public int xp = 1;
        public string tool = "hand";
        public bool blocksMovement = true;
        public bool blocksPlanting = true;

        public static FarmObstacleType FromRule(FarmObstacleRule rule)
        {
            return rule == null ? null : new FarmObstacleType
            {
                id = rule.ObstacleId,
                label = rule.DisplayName,
                requiredLevel = rule.RequiredFarmLevel,
                xp = rule.XpReward,
                tool = rule.RequiredTool.ToString().ToLowerInvariant(),
                blocksMovement = rule.BlocksMovement,
                blocksPlanting = true
            };
        }
    }

    public enum FarmSoilKind { Untilled, Tilled }

    /// <summary>Scene-facing metadata for the authored 10×10 clearing farm.</summary>
    public static class FarmStarterMap
    {
        public const string Id = "farm_clearing_10x10";
        public const string DisplayName = "Starter Plot";
        public const int Width = FarmStarterContent.Width;
        public const int Height = FarmStarterContent.Height;
        public static readonly Vector2Int PlayerStart = new Vector2Int(5, 9);
    }
}
