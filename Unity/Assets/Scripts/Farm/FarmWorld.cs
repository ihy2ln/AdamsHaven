namespace Game.Farm
{
    /// <summary>Unity-facing facade over the presentation-free farm simulation.</summary>
    public sealed class FarmWorld
    {
        public sealed class PlayerView
        {
            readonly FarmSimulation _simulation;

            public PlayerView(FarmSimulation simulation) => _simulation = simulation;

            public int X => _simulation.State.player.x;
            public int Y => _simulation.State.player.y;
            public int FacingX => _simulation.State.player.facingX;
            public int FacingY => _simulation.State.player.facingY;
            public int Level => _simulation.State.player.farmLevel;
            public int Xp => _simulation.State.player.farmXp;
            public int? XpToNext() => _simulation.XpToNextLevel();
        }

        public FarmSimulation Simulation { get; }
        public FarmSaveData SaveData => Simulation.State;
        public PlayerView Player { get; }
        public int Width => SaveData.width;
        public int Height => SaveData.height;
        public string DisplayName => SaveData.displayName;
        public int ClearedCount => SaveData.clearedObstacles;

        public FarmWorld(FarmSaveData saveData, IFarmClock clock)
        {
            Simulation = new FarmSimulation(FarmStarterContent.CreateCatalog(), saveData, clock);
            Player = new PlayerView(Simulation);
        }

        public bool InBounds(int x, int y) => Simulation.InBounds(x, y);
        public FarmTileState GetTile(int x, int y) => Simulation.GetTile(x, y);
        public string GetObstacleId(int x, int y) => GetTile(x, y)?.obstacleId;
        public FarmObstacleType GetObstacle(int x, int y) => FarmObstacleType.FromRule(Simulation.GetObstacleRule(GetTile(x, y)));
        public FarmSoilKind GetSoil(int x, int y) => GetTile(x, y)?.soil == FarmSoilState.Tilled ? FarmSoilKind.Tilled : FarmSoilKind.Untilled;
        public bool IsWatered(int x, int y) => GetTile(x, y)?.watered == true;
        public bool IsFertilized(int x, int y) => !string.IsNullOrEmpty(GetTile(x, y)?.fertilizerId);
        public bool IsCropReady(int x, int y) => Simulation.IsCropReady(GetTile(x, y));
        public FarmCropRule GetCrop(int x, int y) => Simulation.GetCropRule(GetTile(x, y));
        public int GetCropVisualStage(int x, int y) => Simulation.GetCropVisualStage(GetTile(x, y));
        public string GetGrowthMessage(int x, int y) => Simulation.GrowthMessage(GetTile(x, y));

        public bool BlocksMovement(int x, int y)
        {
            var rule = Simulation.GetObstacleRule(GetTile(x, y));
            return rule != null && rule.BlocksMovement;
        }

        public FarmActionResult Move(int dx, int dy) => Simulation.TryMove(dx, dy);
        public FarmActionResult UseTool(int x, int y, FarmTool tool) => Simulation.UseTool(x, y, tool);
        public FarmActionResult Plant(int x, int y, string cropId) => Simulation.Plant(x, y, cropId);
        public FarmActionResult Fertilize(int x, int y, string fertilizerItemId) => Simulation.ApplyFertilizer(x, y, fertilizerItemId);
        public FarmActionResult Harvest(int x, int y) => Simulation.Harvest(x, y);
        public FarmActionResult ApplyBattle(FarmBattleReport report) => Simulation.ApplyBattleReport(report);
        public int RemainingObstacles() => Simulation.RemainingObstacles();
        public int GetItemCount(string itemId) => Simulation.GetItemCount(itemId);
    }
}
