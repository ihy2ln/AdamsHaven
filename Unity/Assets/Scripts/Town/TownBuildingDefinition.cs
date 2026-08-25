using System.Collections.Generic;

namespace Game.Town
{
    /// <summary>The four sectors from the project owner's own building-rules spec --
    /// Commercial (sell items/mats/gear/characters), Housing (resident happiness ->
    /// buffs and work rate), Industrial (crafting/enhancement/training), Government
    /// (passive town-wide boosts). Each of Town's 15 plots belongs to one sector and can
    /// become any `TownBuildingDefinition` catalogued under it -- the sector is fixed at
    /// plot placement, the specific building is a player choice at build time.</summary>
    public enum TownDistrict
    {
        Commercial,
        Housing,
        Industrial,
        Government,
    }

    /// <summary>One buildable option -- cost (money + per-material amounts) and a
    /// real-time build duration, per the project owner's spec: "cost of mats earned
    /// from battle/dungeon and from farm, with the addition of time and money." Every
    /// number here is a first-pass placeholder, same convention as every other system
    /// in this project (Farm's potion potency, Battle's combat stats) -- not tuned,
    /// just real enough to prove the loop. `RoofImageKey` optionally reuses one of
    /// M34's cropped-from-the-reference-image roof textures (built, then left unused
    /// when M36 reworked Town to start empty) so a completed building actually looks
    /// like something instead of just changing a label.</summary>
    public class TownBuildingDefinition
    {
        public string Id;
        public string DisplayName;
        public TownDistrict District;
        public int MoneyCost;
        public IReadOnlyList<(TownMaterialKind kind, int amount)> MaterialCost;
        public float BuildSeconds;
        public string VerbDescription;
        public string RoofImageKey;

        /// <summary>The material this building needs most of -- what a Rush spends on
        /// top of money, per the project owner's "more money and mats to hasten the
        /// time" spec. Falls back to Hide if a definition somehow lists none.</summary>
        public TownMaterialKind PrimaryMaterial
        {
            get
            {
                var best = TownMaterialKind.Hide;
                var bestAmount = -1;
                foreach (var (kind, amount) in MaterialCost)
                    if (amount > bestAmount) { best = kind; bestAmount = amount; }
                return best;
            }
        }
    }

    /// <summary>The starting catalog -- a handful of real options per sector, not an
    /// exhaustive list of every profession the project owner named (blacksmith/
    /// jeweler/armorer/schooling, etc. all included; more can be added to any sector's
    /// list cheaply since the data model doesn't care how many entries a district
    /// has).</summary>
    public static class TownBuildingCatalog
    {
        public static readonly IReadOnlyList<TownBuildingDefinition> All = new[]
        {
            new TownBuildingDefinition
            {
                Id = "general_store", DisplayName = "General Store", District = TownDistrict.Commercial,
                MoneyCost = 50, MaterialCost = new[] { (TownMaterialKind.Hide, 3) },
                BuildSeconds = 30f, VerbDescription = "Sell farm goods (not wired yet).",
                RoofImageKey = "market_general_store",
            },
            new TownBuildingDefinition
            {
                Id = "gear_trader", DisplayName = "Gear Trader", District = TownDistrict.Commercial,
                MoneyCost = 120, MaterialCost = new[] { (TownMaterialKind.Ore, 5) },
                BuildSeconds = 60f, VerbDescription = "Buy/sell gear (not wired yet).",
                RoofImageKey = "market_apothecary",
            },
            new TownBuildingDefinition
            {
                Id = "character_broker", DisplayName = "Character Broker", District = TownDistrict.Commercial,
                MoneyCost = 300, MaterialCost = new[] { (TownMaterialKind.Essence, 8) },
                BuildSeconds = 120f, VerbDescription = "Recruit characters (not wired yet).",
                RoofImageKey = "market_trading_post",
            },

            new TownBuildingDefinition
            {
                Id = "cottage", DisplayName = "Cottage", District = TownDistrict.Housing,
                MoneyCost = 40, MaterialCost = new[] { (TownMaterialKind.Hide, 4) },
                BuildSeconds = 30f, VerbDescription = "Houses residents; +5% happiness (stub).",
                RoofImageKey = "house_cottage_1",
            },
            new TownBuildingDefinition
            {
                Id = "manor", DisplayName = "Manor", District = TownDistrict.Housing,
                MoneyCost = 150, MaterialCost = new[] { (TownMaterialKind.Hide, 6), (TownMaterialKind.Ore, 3) },
                BuildSeconds = 90f, VerbDescription = "Nicer housing; +15% happiness (stub).",
                RoofImageKey = "house_cottage_2",
            },

            new TownBuildingDefinition
            {
                Id = "blacksmith", DisplayName = "Blacksmith", District = TownDistrict.Industrial,
                MoneyCost = 100, MaterialCost = new[] { (TownMaterialKind.Ore, 8) },
                BuildSeconds = 60f, VerbDescription = "Craft/enhance weapons (not wired yet).",
                RoofImageKey = "utility_grain_barn",
            },
            new TownBuildingDefinition
            {
                Id = "jeweler", DisplayName = "Jeweler", District = TownDistrict.Industrial,
                MoneyCost = 130, MaterialCost = new[] { (TownMaterialKind.Ore, 4), (TownMaterialKind.Essence, 4) },
                BuildSeconds = 75f, VerbDescription = "Craft accessories (not wired yet).",
                RoofImageKey = "utility_storehouse",
            },
            new TownBuildingDefinition
            {
                Id = "armorer", DisplayName = "Armorer", District = TownDistrict.Industrial,
                MoneyCost = 110, MaterialCost = new[] { (TownMaterialKind.Hide, 5), (TownMaterialKind.Ore, 5) },
                BuildSeconds = 60f, VerbDescription = "Craft armor (not wired yet).",
                RoofImageKey = "utility_tool_shed",
            },
            new TownBuildingDefinition
            {
                Id = "school", DisplayName = "School", District = TownDistrict.Industrial,
                MoneyCost = 90, MaterialCost = new[] { (TownMaterialKind.Essence, 5) },
                BuildSeconds = 45f, VerbDescription = "Train units (not wired yet).",
                RoofImageKey = "utility_water_tower",
            },

            new TownBuildingDefinition
            {
                Id = "town_hall", DisplayName = "Town Hall", District = TownDistrict.Government,
                MoneyCost = 200,
                MaterialCost = new[] { (TownMaterialKind.Hide, 5), (TownMaterialKind.Ore, 5), (TownMaterialKind.Essence, 5) },
                BuildSeconds = 90f, VerbDescription = "Passive town-wide boost (stub).",
                RoofImageKey = "town_hall",
            },
        };

        public static IEnumerable<TownBuildingDefinition> ForDistrict(TownDistrict district)
        {
            foreach (var def in All)
                if (def.District == district) yield return def;
        }
    }
}
