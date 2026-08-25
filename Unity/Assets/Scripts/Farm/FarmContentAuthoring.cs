using System.Collections.Generic;
using Game.Data;

namespace Game.Farm
{
    /// <summary>Converts authored ScriptableObjects into the presentation-free runtime catalogue.</summary>
    public static class FarmContentAuthoring
    {
        public static FarmContentCatalog Build(IEnumerable<CropDefinition> definitions)
        {
            var catalog = new FarmContentCatalog();
            if (definitions == null) return catalog;
            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrEmpty(definition.cropId)) continue;
                catalog.Add(new FarmCropRule
                {
                    CropId = definition.cropId,
                    DisplayName = definition.displayName,
                    SeedItemId = definition.seed == null ? "" : definition.seed.materialId,
                    ProduceItemId = definition.produce == null ? "" : definition.produce.materialId,
                    PlotRule = ConvertPlotRule(definition.plotType),
                    GrowthSeconds = definition.growthSeconds,
                    GrowthBattles = definition.growthBattles,
                    RegrowSeconds = definition.regrowSeconds,
                    MinYield = definition.minYield,
                    MaxYield = definition.maxYield,
                    RequiresWater = definition.requiresWater,
                    WaterSpeedBonus = definition.waterSpeedBonus
                });

                if (definition.fertilizers == null) continue;
                foreach (var effect in definition.fertilizers)
                {
                    if (effect.fertilizer == null || string.IsNullOrEmpty(effect.fertilizer.materialId)) continue;
                    catalog.Add(new FarmFertilizerRule
                    {
                        ItemId = effect.fertilizer.materialId,
                        DisplayName = effect.fertilizer.displayName,
                        SpeedBonus = effect.speedBonus,
                        BonusYield = effect.bonusYield,
                        QualityUpChance = effect.qualityUpChance
                    });
                }
            }
            return catalog;
        }

        static FarmPlotRule ConvertPlotRule(FarmPlotType plotType)
        {
            switch (plotType)
            {
                case FarmPlotType.Dungeon: return FarmPlotRule.Dungeon;
                case FarmPlotType.Both: return FarmPlotRule.Both;
                default: return FarmPlotRule.Town;
            }
        }
    }
}
