using UnityEngine;

namespace Game.Town
{
    /// <summary>The five building verbs from FOUNDATION.md's SimCity-style Town design
    /// (5.5) -- none functional yet (M30/M31), just labels on placeholder lots so the
    /// eventual real building slots into a spot that already reads correctly on the
    /// map.</summary>
    public enum TownBuildingType
    {
        MarketStall,
        House,
        UtilityShed,
        TownHall,
    }

    /// <summary>Tags a placeholder lot in the Town scene (M31) -- a real, walkable,
    /// collide-able footprint with a name and a district, but no function behind it
    /// yet. `TownController` proximity-checks these to show an interaction prompt;
    /// there's nothing to interact with until a future milestone gives each type real
    /// behaviour (see PROJECT-README's Town milestones).</summary>
    public class TownBuilding : MonoBehaviour
    {
        public TownBuildingType Type;
        public string DisplayName;

        public string PromptLine => $"{DisplayName} -- not built yet.";
    }
}
