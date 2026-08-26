using System;
using System.Collections.Generic;

namespace Game.Farm
{
    public interface IFarmClock
    {
        long NowUnixSeconds { get; }
    }

    public sealed class SystemFarmClock : IFarmClock
    {
        public long NowUnixSeconds => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>
    /// Deterministic, presentation-free farm rules. Real time is supplied through a clock
    /// so save/offline behavior and tests use the same code path.
    /// </summary>
    public sealed class FarmSimulation
    {
        static readonly int[] LevelCurve = { 0, 15, 40, 80, 140, 220, 320, 460 };

        readonly FarmContentCatalog _content;
        readonly IFarmClock _clock;
        readonly Dictionary<int, FarmTileState> _tiles = new Dictionary<int, FarmTileState>();

        public FarmSaveData State { get; }
        public FarmContentCatalog Content => _content;
        public event Action<FarmEvent> EventRaised;

        public FarmSimulation(FarmContentCatalog content, FarmSaveData state, IFarmClock clock)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            State = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            RepairAndIndexState();
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < State.width && y < State.height;

        public FarmTileState GetTile(int x, int y)
        {
            FarmTileState tile;
            return InBounds(x, y) && _tiles.TryGetValue(Key(x, y), out tile) ? tile : null;
        }

        public FarmCropRule GetCropRule(FarmTileState tile)
        {
            FarmCropRule crop;
            return tile != null && tile.crop != null && _content.TryGetCrop(tile.crop.cropId, out crop) ? crop : null;
        }

        public FarmObstacleRule GetObstacleRule(FarmTileState tile)
        {
            FarmObstacleRule obstacle;
            return tile != null && !string.IsNullOrEmpty(tile.obstacleId) && _content.TryGetObstacle(tile.obstacleId, out obstacle)
                ? obstacle
                : null;
        }

        public int GetItemCount(string itemId)
        {
            var stack = FindStack(itemId);
            return stack == null ? 0 : stack.quantity;
        }

        public int? XpToNextLevel()
        {
            return State.player.farmLevel >= LevelCurve.Length ? (int?)null : LevelCurve[State.player.farmLevel];
        }

        public int EffectiveGrowthSeconds(FarmTileState tile)
        {
            var crop = GetCropRule(tile);
            if (crop == null) return 0;
            var speedBonus = tile.watered ? crop.WaterSpeedBonus : 0f;
            FarmFertilizerRule fertilizer;
            if (_content.TryGetFertilizer(tile.fertilizerId, out fertilizer)) speedBonus += fertilizer.SpeedBonus;
            speedBonus = Math.Max(0f, Math.Min(0.9f, speedBonus));
            return Math.Max(1, (int)Math.Round(crop.GrowthSeconds * (1f - speedBonus), MidpointRounding.AwayFromZero));
        }

        public float GetGrowthProgress(FarmTileState tile)
        {
            var crop = GetCropRule(tile);
            if (crop == null || (crop.RequiresWater && !tile.watered)) return 0f;
            var elapsed = Math.Max(0L, _clock.NowUnixSeconds - tile.crop.plantedAtUnix);
            var timeProgress = crop.GrowthSeconds <= 0 ? 1f : (float)elapsed / EffectiveGrowthSeconds(tile);
            var battlesElapsed = Math.Max(0, State.totalBattles - tile.crop.plantedAtBattleCount);
            var battleProgress = crop.GrowthBattles <= 0 ? 0f : (float)battlesElapsed / crop.GrowthBattles;
            return Math.Min(1f, Math.Max(timeProgress, battleProgress));
        }

        public bool IsCropReady(FarmTileState tile) => GetGrowthProgress(tile) >= 1f;

        public int GetCropVisualStage(FarmTileState tile, int visualStages = 4)
        {
            if (GetCropRule(tile) == null || visualStages <= 1) return 0;
            return Math.Min(visualStages - 1, (int)Math.Floor(GetGrowthProgress(tile) * visualStages));
        }

        public int SecondsUntilReady(FarmTileState tile)
        {
            var crop = GetCropRule(tile);
            if (crop == null || IsCropReady(tile)) return 0;
            if (crop.RequiresWater && !tile.watered) return -1;
            var elapsed = Math.Max(0L, _clock.NowUnixSeconds - tile.crop.plantedAtUnix);
            return Math.Max(0, EffectiveGrowthSeconds(tile) - (int)Math.Min(int.MaxValue, elapsed));
        }

        public int BattlesUntilReady(FarmTileState tile)
        {
            var crop = GetCropRule(tile);
            if (crop == null || crop.GrowthBattles <= 0 || IsCropReady(tile)) return 0;
            if (crop.RequiresWater && !tile.watered) return crop.GrowthBattles;
            return Math.Max(0, crop.GrowthBattles - (State.totalBattles - tile.crop.plantedAtBattleCount));
        }

        public FarmActionResult TryMove(int dx, int dy)
        {
            if (Math.Abs(dx) + Math.Abs(dy) == 1)
            {
                State.player.facingX = dx;
                State.player.facingY = dy;
            }
            var target = new FarmPosition(State.player.x + dx, State.player.y + dy);
            if (!InBounds(target.x, target.y)) return FarmActionResult.Fail("The farm boundary is in the way.", target);
            var obstacle = GetObstacleRule(GetTile(target.x, target.y));
            if (obstacle != null && obstacle.BlocksMovement)
                return FarmActionResult.Fail(obstacle.DisplayName + " blocks the path.", target);
            State.player.x = target.x;
            State.player.y = target.y;
            return FarmActionResult.Success(FarmActionCode.Moved, "Moved.", target);
        }

        public FarmActionResult UseTool(int x, int y, FarmTool tool)
        {
            var position = new FarmPosition(x, y);
            var tile = GetTile(x, y);
            if (tile == null) return FarmActionResult.Fail("That tile is outside this plot.", position);
            if (!IsReachable(position)) return FarmActionResult.Fail("Move next to that tile first.", position);

            var obstacle = GetObstacleRule(tile);
            if (obstacle != null)
            {
                if (obstacle.RequiredTool != tool)
                    return FarmActionResult.Fail("Use the " + ToolName(obstacle.RequiredTool) + " on " + obstacle.DisplayName + ".", position);
                if (State.player.farmLevel < obstacle.RequiredFarmLevel)
                    return FarmActionResult.Fail("Farm level " + obstacle.RequiredFarmLevel + " is required.", position);

                tile.obstacleId = "";
                State.clearedObstacles++;
                var levels = ApplyXp(obstacle.XpReward);
                AddItem(obstacle.DropItemId, obstacle.DropQuantity);
                Raise(FarmEventType.ObstacleCleared, obstacle.ObstacleId, 1, position);
                return FarmActionResult.Success(
                    FarmActionCode.ObstacleCleared,
                    "Cleared " + obstacle.DisplayName + ".",
                    position,
                    obstacle.XpReward,
                    levels,
                    obstacle.DropItemId,
                    obstacle.DropQuantity);
            }

            if (tool == FarmTool.Hoe)
            {
                if (tile.crop != null) return FarmActionResult.Fail("A crop is already growing here.", position);
                if (tile.soil == FarmSoilState.Tilled) return FarmActionResult.Fail("This soil is already tilled.", position);
                tile.soil = FarmSoilState.Tilled;
                Raise(FarmEventType.SoilChanged, "tilled", 1, position);
                return FarmActionResult.Success(FarmActionCode.Tilled, "Tilled the soil.", position);
            }

            if (tool == FarmTool.WateringCan)
            {
                if (tile.soil != FarmSoilState.Tilled) return FarmActionResult.Fail("Till the soil before watering it.", position);
                if (tile.watered) return FarmActionResult.Fail("This plot is already watered.", position);
                tile.watered = true;
                var plantedCrop = GetCropRule(tile);
                if (tile.crop != null && plantedCrop != null && plantedCrop.RequiresWater)
                {
                    // Dry-required crops genuinely stall; time/battles before watering do not bank.
                    tile.crop.plantedAtUnix = _clock.NowUnixSeconds;
                    tile.crop.plantedAtBattleCount = State.totalBattles;
                }
                Raise(FarmEventType.SoilChanged, "watered", 1, position);
                return FarmActionResult.Success(FarmActionCode.Watered, "Watered the plot. Growth is now active.", position);
            }

            return FarmActionResult.Fail("There is nothing here for the " + ToolName(tool) + ".", position);
        }

        public FarmActionResult ApplyFertilizer(int x, int y, string fertilizerItemId)
        {
            var position = new FarmPosition(x, y);
            var tile = GetTile(x, y);
            FarmFertilizerRule fertilizer;
            if (tile == null) return FarmActionResult.Fail("That tile is outside this plot.", position);
            if (!IsReachable(position)) return FarmActionResult.Fail("Move next to that tile first.", position);
            if (!_content.TryGetFertilizer(fertilizerItemId, out fertilizer)) return FarmActionResult.Fail("That fertilizer is unknown.", position);
            if (tile.soil != FarmSoilState.Tilled) return FarmActionResult.Fail("Till the soil before fertilizing it.", position);
            if (!string.IsNullOrEmpty(tile.fertilizerId)) return FarmActionResult.Fail("This plot is already fertilized.", position);
            if (!RemoveItem(fertilizer.ItemId, 1)) return FarmActionResult.Fail("You are out of " + fertilizer.DisplayName + ".", position);
            tile.fertilizerId = fertilizer.ItemId;
            Raise(FarmEventType.SoilChanged, fertilizer.ItemId, 1, position);
            return FarmActionResult.Success(FarmActionCode.Fertilized, "Applied " + fertilizer.DisplayName + ".", position, itemId: fertilizer.ItemId, quantity: -1);
        }

        public FarmActionResult Plant(int x, int y, string cropId)
        {
            var position = new FarmPosition(x, y);
            var tile = GetTile(x, y);
            FarmCropRule crop;
            if (tile == null) return FarmActionResult.Fail("That tile is outside this plot.", position);
            if (!IsReachable(position)) return FarmActionResult.Fail("Move next to that tile first.", position);
            if (!_content.TryGetCrop(cropId, out crop)) return FarmActionResult.Fail("That seed is unknown.", position);
            if (!crop.CanPlantAt(tile.location)) return FarmActionResult.Fail(crop.DisplayName + " cannot grow on a " + tile.location.ToString().ToLowerInvariant() + " plot.", position);
            if (!string.IsNullOrEmpty(tile.obstacleId)) return FarmActionResult.Fail("Clear this tile before planting.", position);
            if (tile.soil != FarmSoilState.Tilled) return FarmActionResult.Fail("Till this tile before planting.", position);
            if (tile.crop != null) return FarmActionResult.Fail("A crop is already planted here.", position);
            if (!RemoveItem(crop.SeedItemId, 1)) return FarmActionResult.Fail("You are out of " + crop.DisplayName + " seeds.", position);

            tile.crop = new FarmCropState
            {
                cropId = crop.CropId,
                plantedAtUnix = _clock.NowUnixSeconds,
                plantedAtBattleCount = State.totalBattles
            };
            Raise(FarmEventType.CropPlanted, crop.CropId, 1, position);
            return FarmActionResult.Success(FarmActionCode.Planted, "Planted " + crop.DisplayName + ".", position, itemId: crop.SeedItemId, quantity: -1);
        }

        public FarmActionResult Harvest(int x, int y)
        {
            var position = new FarmPosition(x, y);
            var tile = GetTile(x, y);
            if (tile == null || tile.crop == null) return FarmActionResult.Fail("There is no crop to harvest.", position);
            if (!IsReachable(position)) return FarmActionResult.Fail("Move next to that tile first.", position);
            var crop = GetCropRule(tile);
            if (crop == null) return FarmActionResult.Fail("This crop's definition is missing.", position);
            if (!IsCropReady(tile)) return FarmActionResult.Fail(GrowthMessage(tile), position);

            var span = Math.Max(0, crop.MaxYield - crop.MinYield);
            var roll = Math.Abs((State.totalBattles * 397) ^ (State.totalHarvests * 31) ^ (x * 17) ^ y);
            var quantity = crop.MinYield + (span == 0 ? 0 : roll % (span + 1));
            FarmFertilizerRule fertilizer;
            if (_content.TryGetFertilizer(tile.fertilizerId, out fertilizer)) quantity += Math.Max(0, fertilizer.BonusYield);
            AddItem(crop.ProduceItemId, quantity);
            State.totalHarvests++;
            tile.crop.harvestsTaken++;

            if (crop.RegrowSeconds > 0)
            {
                tile.crop.plantedAtUnix = _clock.NowUnixSeconds;
                tile.crop.plantedAtBattleCount = State.totalBattles;
                tile.watered = false;
            }
            else
            {
                tile.crop = null;
                tile.watered = false;
                tile.fertilizerId = "";
            }

            Raise(FarmEventType.CropHarvested, crop.ProduceItemId, quantity, position);
            return FarmActionResult.Success(
                FarmActionCode.Harvested,
                "Harvested " + quantity + " " + crop.DisplayName + ".",
                position,
                itemId: crop.ProduceItemId,
                quantity: quantity);
        }

        /// <summary>
        /// Applies a presentation-earned gathering bonus after a successful
        /// harvest or obstacle clear. The minigame decides performance; the
        /// simulation remains the only owner of inventory mutation.
        /// </summary>
        public FarmActionResult ApplyGatherBonus(string itemId, int quantity, FarmPosition position)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0)
                return FarmActionResult.Fail("No gathering bonus was earned.", position);
            AddItem(itemId, quantity);
            return FarmActionResult.Success(
                FarmActionCode.GatherBonus,
                "Gathering skill added " + quantity + " " + GetItemDisplayName(itemId) + ".",
                position,
                itemId: itemId,
                quantity: quantity);
        }

        public FarmActionResult ApplyBattleReport(FarmBattleReport report)
        {
            if (report == null) return FarmActionResult.Fail("Battle report was missing.", PlayerPosition());
            var battles = Math.Max(0, report.battlesCompleted);
            State.totalBattles += battles;
            if (report.rewards != null)
                foreach (var reward in report.rewards)
                    if (reward != null && reward.quantity > 0) AddItem(reward.itemId, reward.quantity);
            Raise(FarmEventType.BattleCompleted, report.stageId, battles, PlayerPosition());
            return FarmActionResult.Success(
                FarmActionCode.BattleApplied,
                battles + " battle(s) recorded for crop growth.",
                PlayerPosition());
        }

        public FarmActionResult TransferToTown(FarmTownRequest request)
        {
            var position = PlayerPosition();
            if (request == null || request.quantity <= 0) return FarmActionResult.Fail("Town transfer was invalid.", position);
            if (!_content.IsProduce(request.itemId)) return FarmActionResult.Fail("Only farm produce can be sent to the Market or Kitchen.", position);
            if (!RemoveItem(request.itemId, request.quantity)) return FarmActionResult.Fail("There is not enough produce for that town action.", position);
            Raise(FarmEventType.ExportedToTown, request.verb + ":" + request.itemId, request.quantity, position);
            return FarmActionResult.Success(
                FarmActionCode.ExportedToTown,
                "Sent " + request.quantity + " produce to town for " + request.verb + ".",
                position,
                itemId: request.itemId,
                quantity: -request.quantity);
        }

        public string GrowthMessage(FarmTileState tile)
        {
            var crop = GetCropRule(tile);
            if (crop == null) return "No crop is growing here.";
            if (crop.RequiresWater && !tile.watered) return crop.DisplayName + " needs water before it can grow.";
            if (IsCropReady(tile)) return crop.DisplayName + " is ready to harvest.";
            var seconds = SecondsUntilReady(tile);
            var battles = BattlesUntilReady(tile);
            if (crop.GrowthBattles > 0)
                return crop.DisplayName + " — " + FormatTime(seconds) + " or " + battles + " battle(s) remaining.";
            return crop.DisplayName + " — " + FormatTime(seconds) + " remaining.";
        }

        public int RemainingObstacles()
        {
            var count = 0;
            foreach (var tile in State.tiles)
                if (!string.IsNullOrEmpty(tile.obstacleId)) count++;
            return count;
        }

        public string GetItemDisplayName(string itemId)
        {
            switch (itemId)
            {
                case "seed_haven_turnip": return "Haven Turnip Seeds";
                case "seed_dungeon_glowroot": return "Dungeon Glowroot Seeds";
                case "produce_haven_turnip": return "Haven Turnips";
                case "produce_dungeon_glowroot": return "Dungeon Glowroot";
                case "fertilizer_basic": return "Basic Fertilizer";
                case "fiber": return "Fiber";
                case "stone": return "Stone";
                case "wood": return "Wood";
                default: return itemId ?? "";
            }
        }

        void RepairAndIndexState()
        {
            State.schemaVersion = FarmSaveData.CurrentSchemaVersion;
            if (State.player == null) State.player = new FarmPlayerProgress();
            if (State.inventory == null) State.inventory = new List<FarmItemStack>();
            if (State.tiles == null) State.tiles = new List<FarmTileState>();
            _tiles.Clear();
            foreach (var tile in State.tiles)
                if (tile != null && InBounds(tile.x, tile.y)) _tiles[Key(tile.x, tile.y)] = tile;
            for (var y = 0; y < State.height; y++)
            for (var x = 0; x < State.width; x++)
                if (!_tiles.ContainsKey(Key(x, y)))
                {
                    var tile = new FarmTileState { x = x, y = y, location = FarmLocation.Town };
                    State.tiles.Add(tile);
                    _tiles[Key(x, y)] = tile;
                }
        }

        bool IsReachable(FarmPosition target)
        {
            return Math.Abs(target.x - State.player.x) + Math.Abs(target.y - State.player.y) <= 1;
        }

        int ApplyXp(int amount)
        {
            State.player.farmXp += Math.Max(0, amount);
            var levels = 0;
            while (State.player.farmLevel < LevelCurve.Length)
            {
                var needed = LevelCurve[State.player.farmLevel];
                if (State.player.farmXp < needed) break;
                State.player.farmXp -= needed;
                State.player.farmLevel++;
                levels++;
                Raise(FarmEventType.FarmLevelGained, "farm", State.player.farmLevel, PlayerPosition());
            }
            return levels;
        }

        FarmItemStack FindStack(string itemId)
        {
            for (var i = 0; i < State.inventory.Count; i++)
                if (State.inventory[i] != null && State.inventory[i].itemId == itemId) return State.inventory[i];
            return null;
        }

        void AddItem(string itemId, int quantity)
        {
            if (string.IsNullOrEmpty(itemId) || quantity <= 0) return;
            var stack = FindStack(itemId);
            if (stack == null)
            {
                stack = new FarmItemStack(itemId, 0);
                State.inventory.Add(stack);
            }
            stack.quantity += quantity;
            Raise(FarmEventType.ItemChanged, itemId, quantity, PlayerPosition());
        }

        bool RemoveItem(string itemId, int quantity)
        {
            var stack = FindStack(itemId);
            if (stack == null || quantity <= 0 || stack.quantity < quantity) return false;
            stack.quantity -= quantity;
            Raise(FarmEventType.ItemChanged, itemId, -quantity, PlayerPosition());
            return true;
        }

        void Raise(FarmEventType type, string subjectId, int amount, FarmPosition position)
        {
            EventRaised?.Invoke(new FarmEvent(type, subjectId, amount, position));
        }

        int Key(int x, int y) => y * State.width + x;
        FarmPosition PlayerPosition() => new FarmPosition(State.player.x, State.player.y);

        static string ToolName(FarmTool tool)
        {
            switch (tool)
            {
                case FarmTool.WateringCan: return "watering can";
                case FarmTool.Pickaxe: return "pickaxe";
                default: return tool.ToString().ToLowerInvariant();
            }
        }

        static string FormatTime(int seconds)
        {
            if (seconds < 0) return "water required";
            var minutes = seconds / 60;
            var remainder = seconds % 60;
            return minutes > 0 ? minutes + "m " + remainder + "s" : remainder + "s";
        }
    }
}
