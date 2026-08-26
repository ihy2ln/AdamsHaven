using Game.Farm;
using NUnit.Framework;

namespace Game.Tests
{
    public sealed class FarmSimulationTests
    {
        sealed class FakeClock : IFarmClock
        {
            public long NowUnixSeconds { get; set; }
            public void Advance(int seconds) => NowUnixSeconds += seconds;
        }

        [Test]
        public void WaterAndFertilizerReduceRealTimeAndIncreaseYield()
        {
            var clock = new FakeClock { NowUnixSeconds = 1_000 };
            var simulation = CreateSimulation(clock, 3, 2);

            Assert.That(simulation.ApplyFertilizer(3, 3, "fertilizer_basic").Succeeded, Is.True);
            Assert.That(simulation.Plant(3, 3, "haven_turnip").Succeeded, Is.True);
            Assert.That(simulation.UseTool(3, 3, FarmTool.WateringCan).Succeeded, Is.True);
            Assert.That(simulation.EffectiveGrowthSeconds(simulation.GetTile(3, 3)), Is.EqualTo(330));

            clock.Advance(329);
            Assert.That(simulation.IsCropReady(simulation.GetTile(3, 3)), Is.False);
            clock.Advance(1);
            Assert.That(simulation.IsCropReady(simulation.GetTile(3, 3)), Is.True);

            var harvest = simulation.Harvest(3, 3);
            Assert.That(harvest.Succeeded, Is.True);
            Assert.That(harvest.Quantity, Is.GreaterThanOrEqualTo(2));
            Assert.That(simulation.GetItemCount("produce_haven_turnip"), Is.EqualTo(harvest.Quantity));
        }

        [Test]
        public void DryCropStallsAndDoesNotBankTimeBeforeWatering()
        {
            var clock = new FakeClock { NowUnixSeconds = 2_000 };
            var simulation = CreateSimulation(clock, 3, 2);
            Assert.That(simulation.Plant(3, 3, "haven_turnip").Succeeded, Is.True);

            clock.Advance(2_000);
            Assert.That(simulation.IsCropReady(simulation.GetTile(3, 3)), Is.False);
            Assert.That(simulation.UseTool(3, 3, FarmTool.WateringCan).Succeeded, Is.True);
            Assert.That(simulation.IsCropReady(simulation.GetTile(3, 3)), Is.False);
        }

        [Test]
        public void BattleCountCanFinishWateredCropBeforeTimer()
        {
            var clock = new FakeClock { NowUnixSeconds = 3_000 };
            var simulation = CreateSimulation(clock, 3, 2);
            simulation.Plant(3, 3, "haven_turnip");
            simulation.UseTool(3, 3, FarmTool.WateringCan);

            simulation.ApplyBattleReport(new FarmBattleReport { stageId = "endless", victory = true, battlesCompleted = 2 });
            Assert.That(simulation.IsCropReady(simulation.GetTile(3, 3)), Is.False);
            simulation.ApplyBattleReport(new FarmBattleReport { stageId = "endless", victory = true, battlesCompleted = 1 });
            Assert.That(simulation.IsCropReady(simulation.GetTile(3, 3)), Is.True);
        }

        [Test]
        public void DungeonOnlyCropRejectsTownPlotAndAcceptsDungeonPlot()
        {
            var clock = new FakeClock { NowUnixSeconds = 4_000 };
            var state = FarmStarterContent.CreateNewGame();
            state.player.x = 3;
            state.player.y = 2;
            state.inventory.Add(new FarmItemStack("seed_dungeon_glowroot", 2));
            var simulation = new FarmSimulation(FarmStarterContent.CreateCatalog(), state, clock);

            Assert.That(simulation.Plant(3, 3, "dungeon_glowroot").Succeeded, Is.False);
            simulation.GetTile(3, 3).location = FarmLocation.Dungeon;
            Assert.That(simulation.Plant(3, 3, "dungeon_glowroot").Succeeded, Is.True);
        }

        [Test]
        public void ProduceTransfersOnlyThroughMarketOrKitchenBoundary()
        {
            var clock = new FakeClock { NowUnixSeconds = 5_000 };
            var simulation = CreateSimulation(clock, 3, 2);
            simulation.Plant(3, 3, "haven_turnip");
            simulation.UseTool(3, 3, FarmTool.WateringCan);
            simulation.ApplyBattleReport(new FarmBattleReport { victory = true, battlesCompleted = 3 });
            var harvest = simulation.Harvest(3, 3);

            var transfer = simulation.TransferToTown(new FarmTownRequest
            {
                verb = FarmTownVerb.Cook,
                itemId = "produce_haven_turnip",
                quantity = harvest.Quantity
            });
            Assert.That(transfer.Succeeded, Is.True);
            Assert.That(simulation.GetItemCount("produce_haven_turnip"), Is.Zero);
            Assert.That(simulation.TransferToTown(new FarmTownRequest
            {
                verb = FarmTownVerb.SellProduce,
                itemId = "wood",
                quantity = 1
            }).Succeeded, Is.False);
        }

        [Test]
        public void TownGatewayExposesNamedMarketAndKitchenHandoffs()
        {
            var clock = new FakeClock { NowUnixSeconds = 5_500 };
            var simulation = CreateSimulation(clock, 3, 2);
            simulation.Plant(3, 3, "haven_turnip");
            simulation.UseTool(3, 3, FarmTool.WateringCan);
            simulation.ApplyBattleReport(new FarmBattleReport { victory = true, battlesCompleted = 3 });
            var harvest = simulation.Harvest(3, 3);
            var gateway = new Game.Farm.Integration.FarmTownGateway(simulation);

            Assert.That(gateway.SellProduce("produce_haven_turnip", harvest.Quantity).Succeeded, Is.True);
            Assert.That(gateway.CookProduce("produce_haven_turnip", 1).Succeeded, Is.False);
        }

        [Test]
        public void ObstaclesRequireTheirConfiguredTool()
        {
            var clock = new FakeClock { NowUnixSeconds = 6_000 };
            var state = FarmStarterContent.CreateNewGame();
            state.player.x = 0;
            state.player.y = 3;
            var simulation = new FarmSimulation(FarmStarterContent.CreateCatalog(), state, clock);

            Assert.That(simulation.UseTool(1, 3, FarmTool.Axe).Succeeded, Is.False);
            Assert.That(simulation.UseTool(1, 3, FarmTool.Scythe).Succeeded, Is.False, "The bush is correctly level-gated at farm level 2.");
            state.player.farmLevel = 2;
            Assert.That(simulation.UseTool(1, 3, FarmTool.Scythe).Succeeded, Is.True);
            Assert.That(simulation.GetItemCount("fiber"), Is.EqualTo(2));
        }

        [Test]
        public void GatheringPerformanceAddsBonusMaterialsThroughSimulationInventory()
        {
            var clock = new FakeClock { NowUnixSeconds = 7_000 };
            var simulation = CreateSimulation(clock, 0, 3);
            simulation.State.player.farmLevel = 2;
            var cleared = simulation.UseTool(1, 3, FarmTool.Scythe);

            Assert.That(cleared.Succeeded, Is.True);
            var bonus = simulation.ApplyGatherBonus(cleared.ItemId, 3, cleared.Position);

            Assert.That(bonus.Succeeded, Is.True);
            Assert.That(bonus.Code, Is.EqualTo(FarmActionCode.GatherBonus));
            Assert.That(simulation.GetItemCount("fiber"), Is.EqualTo(5));
        }

        [Test]
        public void SwipeGatheringUnlocksAtFarmLevelThreeAndRewardsPerformance()
        {
            Assert.That(FarmGatherRules.ModeForLevel(1), Is.EqualTo(FarmGatherMode.PrecisionTap));
            Assert.That(FarmGatherRules.ModeForLevel(2), Is.EqualTo(FarmGatherMode.PrecisionTap));
            Assert.That(FarmGatherRules.ModeForLevel(3), Is.EqualTo(FarmGatherMode.Swipe));
            Assert.That(FarmGatherRules.BonusForScore(0.39f), Is.Zero);
            Assert.That(FarmGatherRules.BonusForScore(0.65f), Is.EqualTo(2));
            Assert.That(FarmGatherRules.BonusForScore(0.95f), Is.EqualTo(3));
        }

        static FarmSimulation CreateSimulation(FakeClock clock, int playerX, int playerY)
        {
            var state = FarmStarterContent.CreateNewGame();
            state.player.x = playerX;
            state.player.y = playerY;
            return new FarmSimulation(FarmStarterContent.CreateCatalog(), state, clock);
        }
    }
}
