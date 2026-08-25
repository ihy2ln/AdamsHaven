using System.Collections.Generic;
using UnityEngine;

namespace Game.Town
{
    /// <summary>Builds the Town blockout at runtime -- plain primitives, no imported art
    /// (M30/M31; a real art pass is its own later milestone, through the same curated
    /// library/ComfyUI pipeline the Farm roster used, not generated ad hoc). Laid out to
    /// match the project owner's own reference image: a forest clearing split by a
    /// crossroads into four districts -- Market Row (NW), Residential (NE), Utility/
    /// Storage (SW), and a Town Hall + plaza (SE) -- with two gates at the tree line
    /// leading to Farm and to the dungeon (Battle).</summary>
    public static class TownVisuals
    {
        const float RoadHalfWidth = 2.5f;
        const float RoadReach = 42f;
        const float ForestRadius = 46f;

        static readonly Color RoadColor = new(0.42f, 0.38f, 0.32f);
        static readonly Color GroundColor = new(0.29f, 0.42f, 0.24f);
        static readonly Color MarketWall = new(0.55f, 0.4f, 0.24f);
        static readonly Color MarketRoof = new(0.68f, 0.3f, 0.22f);
        static readonly Color HouseWall = new(0.62f, 0.58f, 0.5f);
        static readonly Color HouseRoof = new(0.32f, 0.38f, 0.5f);
        static readonly Color UtilityWall = new(0.4f, 0.38f, 0.36f);
        static readonly Color UtilityRoof = new(0.35f, 0.28f, 0.2f);
        static readonly Color HallWall = new(0.5f, 0.52f, 0.58f);
        static readonly Color HallRoof = new(0.22f, 0.26f, 0.36f);
        static readonly Color TrunkColor = new(0.3f, 0.22f, 0.14f);
        static readonly Color LeafColor = new(0.16f, 0.32f, 0.16f);
        static readonly Color GateColor = new(0.85f, 0.75f, 0.25f);

        public class BuildResult
        {
            public Vector3 PlayerSpawn;
            public readonly List<TownBuilding> Buildings = new();
            public readonly List<TownGate> Gates = new();
        }

        public static BuildResult Build(Transform parent)
        {
            var result = new BuildResult { PlayerSpawn = new Vector3(0f, 0.5f, -5f) };

            var groundGo = Primitive(PrimitiveType.Plane, parent, "Ground",
                Vector3.zero, new Vector3(12f, 1f, 12f), GroundColor);
            groundGo.transform.localScale = new Vector3(ForestRadius / 5f, 1f, ForestRadius / 5f);

            BuildRoads(parent);
            BuildForestRing(parent);

            BuildMarketRow(parent, result.Buildings);
            BuildResidential(parent, result.Buildings);
            BuildUtility(parent, result.Buildings);
            BuildTownHall(parent, result.Buildings);

            BuildGate(parent, result.Gates, "Path to the Farm", "Farm", new Vector3(-ForestRadius + 4f, 0f, -ForestRadius + 4f));
            BuildGate(parent, result.Gates, "Path to the Dungeon", "Battle", new Vector3(ForestRadius - 4f, 0f, -ForestRadius + 4f));

            return result;
        }

        static void BuildRoads(Transform parent)
        {
            Primitive(PrimitiveType.Cube, parent, "Road_NS",
                Vector3.zero, new Vector3(RoadHalfWidth * 2f, 0.05f, RoadReach * 2f), RoadColor);
            Primitive(PrimitiveType.Cube, parent, "Road_EW",
                Vector3.zero, new Vector3(RoadReach * 2f, 0.05f, RoadHalfWidth * 2f), RoadColor);
        }

        static void BuildForestRing(Transform parent)
        {
            const int count = 40;
            for (var i = 0; i < count; i++)
            {
                var angle = i * (360f / count) * Mathf.Deg2Rad;
                var jitter = 4f * Mathf.Sin(i * 12.9f);
                var radius = ForestRadius + jitter;
                var pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Tree(parent, pos);
            }
        }

        static void Tree(Transform parent, Vector3 pos)
        {
            var trunk = Primitive(PrimitiveType.Cylinder, parent, "Tree_Trunk",
                pos + Vector3.up * 0.75f, new Vector3(0.5f, 0.75f, 0.5f), TrunkColor);
            var leaves = Primitive(PrimitiveType.Sphere, parent, "Tree_Leaves",
                pos + Vector3.up * 2.2f, new Vector3(2.2f, 2.2f, 2.2f), LeafColor);
            leaves.transform.SetParent(trunk.transform, true);
        }

        static void BuildMarketRow(Transform parent, List<TownBuilding> buildings)
        {
            var names = new[] { "General Store", "Apothecary", "Weaver's Stall", "Fishmonger", "Trading Post" };
            var start = new Vector3(-14f, 0f, 14f);
            for (var i = 0; i < names.Length; i++)
            {
                var pos = start + new Vector3(-i * 6.5f, 0f, 0f);
                buildings.Add(Building(parent, pos, new Vector3(5f, 3f, 5f),
                    MarketWall, MarketRoof, TownBuildingType.MarketStall, names[i]));
            }
        }

        static void BuildResidential(Transform parent, List<TownBuilding> buildings)
        {
            var names = new[] { "Cottage", "Cottage", "Cottage", "Cottage", "Cottage", "Cottage" };
            var origin = new Vector3(14f, 0f, 30f);
            for (var i = 0; i < names.Length; i++)
            {
                var col = i % 2;
                var row = i / 2;
                var pos = origin + new Vector3(col * 11f, 0f, -row * 11f);
                buildings.Add(Building(parent, pos, new Vector3(4.5f, 2.6f, 4.5f),
                    HouseWall, HouseRoof, TownBuildingType.House, names[i] + " " + (i + 1)));
            }
        }

        static void BuildUtility(Transform parent, List<TownBuilding> buildings)
        {
            var names = new[] { "Storehouse", "Tool Shed", "Grain Barn" };
            var origin = new Vector3(-14f, 0f, -14f);
            for (var i = 0; i < names.Length; i++)
            {
                var pos = origin + new Vector3(-i * 8f, 0f, -(i % 2) * 6f);
                buildings.Add(Building(parent, pos, new Vector3(6f, 3.4f, 6f),
                    UtilityWall, UtilityRoof, TownBuildingType.UtilityShed, names[i]));
            }

            var towerPos = origin + new Vector3(6f, 0f, -10f);
            var tower = Primitive(PrimitiveType.Cylinder, parent, "WaterTower",
                towerPos + Vector3.up * 3f, new Vector3(2f, 3f, 2f), UtilityRoof);
            var tb = tower.AddComponent<TownBuilding>();
            tb.Type = TownBuildingType.UtilityShed;
            tb.DisplayName = "Water Tower";
            buildings.Add(tb);
        }

        static void BuildTownHall(Transform parent, List<TownBuilding> buildings)
        {
            var pos = new Vector3(16f, 0f, -14f);
            buildings.Add(Building(parent, pos, new Vector3(9f, 5f, 7f),
                HallWall, HallRoof, TownBuildingType.TownHall, "Town Hall"));

            var plaza = Primitive(PrimitiveType.Cube, parent, "Plaza",
                pos + new Vector3(0f, -0.02f, 9f), new Vector3(12f, 0.03f, 10f), RoadColor);
            plaza.transform.localScale = new Vector3(12f, 0.03f, 10f);

            var poleBase = pos + new Vector3(0f, 0f, 9f);
            Primitive(PrimitiveType.Cylinder, parent, "Flagpole",
                poleBase + Vector3.up * 2.5f, new Vector3(0.15f, 2.5f, 0.15f), new Color(0.6f, 0.6f, 0.6f));
            var flag = Primitive(PrimitiveType.Cube, parent, "Flag",
                poleBase + new Vector3(0.6f, 4.6f, 0f), new Vector3(1.2f, 0.7f, 0.05f), new Color(0.75f, 0.15f, 0.15f));
        }

        static TownBuilding Building(Transform parent, Vector3 basePos, Vector3 size,
            Color wall, Color roof, TownBuildingType type, string displayName)
        {
            var body = Primitive(PrimitiveType.Cube, parent, displayName,
                basePos + Vector3.up * (size.y / 2f), size, wall);
            var roofGo = Primitive(PrimitiveType.Cube, parent, displayName + "_Roof",
                basePos + Vector3.up * (size.y + 0.3f), new Vector3(size.x * 1.1f, 0.6f, size.z * 1.1f), roof);
            roofGo.transform.SetParent(body.transform, true);

            var tb = body.AddComponent<TownBuilding>();
            tb.Type = type;
            tb.DisplayName = displayName;
            return tb;
        }

        static void BuildGate(Transform parent, List<TownGate> gates, string displayName, string targetScene, Vector3 pos)
        {
            var pad = Primitive(PrimitiveType.Cylinder, parent, "Gate_" + targetScene,
                pos + Vector3.up * 0.05f, new Vector3(2.4f, 0.05f, 2.4f), GateColor);
            var gate = pad.AddComponent<TownGate>();
            gate.DisplayName = displayName;
            gate.TargetSceneName = targetScene;
            gates.Add(gate);
        }

        static GameObject Primitive(PrimitiveType type, Transform parent, string name,
            Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                var mat = new Material(shader) { color = color };
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
                renderer.material = mat;
            }

            return go;
        }
    }
}
