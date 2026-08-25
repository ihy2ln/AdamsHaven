using System;
using System.Collections.Generic;

namespace Game.Farm
{
    public sealed class FarmCropRule
    {
        public string CropId = "";
        public string DisplayName = "";
        public string SeedItemId = "";
        public string ProduceItemId = "";
        public FarmPlotRule PlotRule = FarmPlotRule.Town;
        public int GrowthSeconds = 600;
        public int GrowthBattles;
        public int RegrowSeconds;
        public int MinYield = 1;
        public int MaxYield = 1;
        public bool RequiresWater = true;
        public float WaterSpeedBonus = 0.25f;

        public bool CanPlantAt(FarmLocation location)
        {
            return PlotRule == FarmPlotRule.Both ||
                   (PlotRule == FarmPlotRule.Town && location == FarmLocation.Town) ||
                   (PlotRule == FarmPlotRule.Dungeon && location == FarmLocation.Dungeon);
        }
    }

    public sealed class FarmFertilizerRule
    {
        public string ItemId = "";
        public string DisplayName = "";
        public float SpeedBonus;
        public int BonusYield;
        public float QualityUpChance;
    }

    public sealed class FarmObstacleRule
    {
        public string ObstacleId = "";
        public string DisplayName = "";
        public FarmTool RequiredTool;
        public int RequiredFarmLevel = 1;
        public int XpReward = 1;
        public bool BlocksMovement = true;
        public string DropItemId = "";
        public int DropQuantity = 1;
    }

    public sealed class FarmContentCatalog
    {
        readonly Dictionary<string, FarmCropRule> _crops = new Dictionary<string, FarmCropRule>();
        readonly Dictionary<string, FarmFertilizerRule> _fertilizers = new Dictionary<string, FarmFertilizerRule>();
        readonly Dictionary<string, FarmObstacleRule> _obstacles = new Dictionary<string, FarmObstacleRule>();

        public IEnumerable<FarmCropRule> Crops => _crops.Values;
        public void Add(FarmCropRule crop) => _crops[crop.CropId] = crop;
        public void Add(FarmFertilizerRule fertilizer) => _fertilizers[fertilizer.ItemId] = fertilizer;
        public void Add(FarmObstacleRule obstacle) => _obstacles[obstacle.ObstacleId] = obstacle;
        public bool TryGetCrop(string id, out FarmCropRule crop) => _crops.TryGetValue(id ?? "", out crop);
        public bool TryGetFertilizer(string id, out FarmFertilizerRule fertilizer) => _fertilizers.TryGetValue(id ?? "", out fertilizer);
        public bool TryGetObstacle(string id, out FarmObstacleRule obstacle) => _obstacles.TryGetValue(id ?? "", out obstacle);

        public bool IsProduce(string itemId)
        {
            foreach (var crop in _crops.Values)
                if (crop.ProduceItemId == itemId) return true;
            return false;
        }
    }

    public static class FarmStarterContent
    {
        // The authored clearing plate is a 10×10 field. These are real gameplay
        // cells, not just a decorative overlay.
        public const int Width = 10;
        public const int Height = 10;

        public static FarmContentCatalog CreateCatalog()
        {
            var catalog = new FarmContentCatalog();
            catalog.Add(new FarmCropRule
            {
                CropId = "haven_turnip",
                DisplayName = "Haven Turnip",
                SeedItemId = "seed_haven_turnip",
                ProduceItemId = "produce_haven_turnip",
                PlotRule = FarmPlotRule.Both,
                GrowthSeconds = 600,
                GrowthBattles = 3,
                MinYield = 1,
                MaxYield = 3,
                RequiresWater = true,
                WaterSpeedBonus = 0.25f
            });
            catalog.Add(new FarmCropRule
            {
                CropId = "dungeon_glowroot",
                DisplayName = "Dungeon Glowroot",
                SeedItemId = "seed_dungeon_glowroot",
                ProduceItemId = "produce_dungeon_glowroot",
                PlotRule = FarmPlotRule.Dungeon,
                GrowthSeconds = 900,
                GrowthBattles = 2,
                MinYield = 1,
                MaxYield = 2,
                RequiresWater = true,
                WaterSpeedBonus = 0.15f
            });
            catalog.Add(new FarmFertilizerRule
            {
                ItemId = "fertilizer_basic",
                DisplayName = "Basic Fertilizer",
                SpeedBonus = 0.20f,
                BonusYield = 1,
                QualityUpChance = 0.10f
            });

            AddObstacle(catalog, "weed", "Weed", FarmTool.Scythe, 1, 4, false, "fiber", 1);
            AddObstacle(catalog, "bush", "Thorn Bush", FarmTool.Scythe, 2, 8, true, "fiber", 2);
            AddObstacle(catalog, "stump", "Tree Stump", FarmTool.Axe, 2, 12, true, "wood", 2);
            AddObstacle(catalog, "tree", "Oak Tree", FarmTool.Axe, 3, 20, true, "wood", 4);
            AddObstacle(catalog, "rock", "Rock", FarmTool.Pickaxe, 2, 10, true, "stone", 2);
            AddObstacle(catalog, "boulder", "Large Boulder", FarmTool.Pickaxe, 4, 35, true, "stone", 5);
            return catalog;
        }

        public static FarmSaveData CreateNewGame()
        {
            var obstacles = new string[Height, Width];
            obstacles[0, 0] = "tree";
            obstacles[0, 1] = "weed";
            obstacles[0, 8] = "weed";
            obstacles[0, 9] = "tree";
            obstacles[1, 1] = "boulder";
            obstacles[1, 4] = "weed";
            obstacles[1, 8] = "rock";
            obstacles[2, 2] = "stump";
            obstacles[2, 7] = "bush";
            obstacles[3, 1] = "bush";
            obstacles[3, 8] = "weed";
            obstacles[4, 0] = "rock";
            obstacles[4, 9] = "rock";
            obstacles[5, 2] = "weed";
            obstacles[5, 7] = "weed";
            obstacles[6, 1] = "bush";
            obstacles[6, 8] = "stump";
            obstacles[8, 1] = "weed";
            obstacles[8, 3] = "rock";
            obstacles[8, 7] = "rock";
            obstacles[8, 8] = "weed";
            obstacles[9, 0] = "tree";
            obstacles[9, 4] = "rock";
            obstacles[9, 9] = "tree";

            var state = new FarmSaveData
            {
                width = Width,
                height = Height,
                player = new FarmPlayerProgress { x = 5, y = 9, facingX = 0, facingY = -1 }
            };

            for (var y = 0; y < Height; y++)
            for (var x = 0; x < Width; x++)
            {
                state.tiles.Add(new FarmTileState
                {
                    x = x,
                    y = y,
                    location = FarmLocation.Town,
                    obstacleId = obstacles[y, x] ?? "",
                    soil = FarmSoilState.Untilled
                });
            }

            state.inventory.Add(new FarmItemStack("seed_haven_turnip", 6));
            state.inventory.Add(new FarmItemStack("fertilizer_basic", 2));
            return state;
        }

        static void AddObstacle(
            FarmContentCatalog catalog,
            string id,
            string displayName,
            FarmTool tool,
            int level,
            int xp,
            bool blocksMovement,
            string dropItemId,
            int dropQuantity)
        {
            catalog.Add(new FarmObstacleRule
            {
                ObstacleId = id,
                DisplayName = displayName,
                RequiredTool = tool,
                RequiredFarmLevel = level,
                XpReward = xp,
                BlocksMovement = blocksMovement,
                DropItemId = dropItemId,
                DropQuantity = dropQuantity
            });
        }
    }
}
