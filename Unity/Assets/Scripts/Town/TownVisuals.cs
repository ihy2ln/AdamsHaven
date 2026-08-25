using System.Collections.Generic;
using UnityEngine;

namespace Game.Town
{
    /// <summary>Builds the Town blockout at runtime. M33: the project owner's own
    /// reference image now paints the ground directly (`Resources/Town/Art/
    /// town_reference_01.png`, loaded at runtime) -- every primitive from M30/M31
    /// (buildings, trees, roads, plaza) still exists in the exact same layout, but with
    /// its renderer switched off, so it's collision-only underneath the picture. Hand-
    /// placed coordinates approximate the image's four districts (Market Row NW,
    /// Residential NE, Utility/Storage SW, Town Hall + plaza SE) rather than matching
    /// its pixels exactly. M34 adds a bas-relief layer on top: each building also gets a
    /// `RoofCap`, a small textured plane cropped from that same reference image
    /// (`Resources/Town/Art/Roofs/`) at roughly this building's own spot, raised just
    /// above its (still invisible) roof box -- a real per-building art pass with proper
    /// walls is still a later milestone; this is a fast stand-in built entirely from the
    /// one reference image, not final art.</summary>
    public static class TownVisuals
    {
        const string ReferenceImageResourcePath = "Town/Art/town_reference_01";
        const string RoofImageResourceDir = "Town/Art/Roofs/";
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

            BuildGroundImage(parent);

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

        static void BuildGroundImage(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "Ground_ReferenceImage";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            // Default Plane mesh is 10x10 units; stretched to a square covering the
            // same footprint the building layout and forest ring already use. Slight
            // distortion from the source image's own portrait aspect is an accepted
            // tradeoff -- see the class doc comment.
            go.transform.localScale = new Vector3(ForestRadius / 5f, 1f, ForestRadius / 5f);

            var texture = Resources.Load<Texture2D>(ReferenceImageResourcePath);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
                var mat = new Material(shader);
                if (texture != null) mat.mainTexture = texture;
                else mat.color = GroundColor; // Resources.Load miss -- fall back rather than render blank/pink.
                renderer.material = mat;
            }
        }

        static void BuildRoads(Transform parent)
        {
            Primitive(PrimitiveType.Cube, parent, "Road_NS",
                Vector3.zero, new Vector3(RoadHalfWidth * 2f, 0.05f, RoadReach * 2f), RoadColor, visible: false);
            Primitive(PrimitiveType.Cube, parent, "Road_EW",
                Vector3.zero, new Vector3(RoadReach * 2f, 0.05f, RoadHalfWidth * 2f), RoadColor, visible: false);
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
                pos + Vector3.up * 0.75f, new Vector3(0.5f, 0.75f, 0.5f), TrunkColor, visible: false);
            var leaves = Primitive(PrimitiveType.Sphere, parent, "Tree_Leaves",
                pos + Vector3.up * 2.2f, new Vector3(2.2f, 2.2f, 2.2f), LeafColor, visible: false);
            leaves.transform.SetParent(trunk.transform, true);
        }

        static void BuildMarketRow(Transform parent, List<TownBuilding> buildings)
        {
            var names = new[] { "General Store", "Apothecary", "Weaver's Stall", "Fishmonger", "Trading Post" };
            var roofKeys = new[] { "market_general_store", "market_apothecary", "market_weavers_stall", "market_fishmonger", "market_trading_post" };
            var start = new Vector3(-14f, 0f, 14f);
            for (var i = 0; i < names.Length; i++)
            {
                var pos = start + new Vector3(-i * 6.5f, 0f, 0f);
                buildings.Add(Building(parent, pos, new Vector3(5f, 3f, 5f),
                    MarketWall, MarketRoof, TownBuildingType.MarketStall, names[i], roofKeys[i]));
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
                    HouseWall, HouseRoof, TownBuildingType.House, names[i] + " " + (i + 1), $"house_cottage_{i + 1}"));
            }
        }

        static void BuildUtility(Transform parent, List<TownBuilding> buildings)
        {
            var names = new[] { "Storehouse", "Tool Shed", "Grain Barn" };
            var roofKeys = new[] { "utility_storehouse", "utility_tool_shed", "utility_grain_barn" };
            var origin = new Vector3(-14f, 0f, -14f);
            for (var i = 0; i < names.Length; i++)
            {
                var pos = origin + new Vector3(-i * 8f, 0f, -(i % 2) * 6f);
                buildings.Add(Building(parent, pos, new Vector3(6f, 3.4f, 6f),
                    UtilityWall, UtilityRoof, TownBuildingType.UtilityShed, names[i], roofKeys[i]));
            }

            var towerPos = origin + new Vector3(6f, 0f, -10f);
            var tower = Primitive(PrimitiveType.Cylinder, parent, "WaterTower",
                towerPos + Vector3.up * 3f, new Vector3(2f, 3f, 2f), UtilityRoof, visible: false);
            var tb = tower.AddComponent<TownBuilding>();
            tb.Type = TownBuildingType.UtilityShed;
            tb.DisplayName = "Water Tower";
            buildings.Add(tb);
            RoofCap(parent, tower.transform, towerPos, new Vector3(3f, 6f, 3f), "utility_water_tower");
        }

        static void BuildTownHall(Transform parent, List<TownBuilding> buildings)
        {
            var pos = new Vector3(16f, 0f, -14f);
            buildings.Add(Building(parent, pos, new Vector3(9f, 5f, 7f),
                HallWall, HallRoof, TownBuildingType.TownHall, "Town Hall", "town_hall"));

            var plaza = Primitive(PrimitiveType.Cube, parent, "Plaza",
                pos + new Vector3(0f, -0.02f, 9f), new Vector3(12f, 0.03f, 10f), RoadColor, visible: false);
            plaza.transform.localScale = new Vector3(12f, 0.03f, 10f);

            var poleBase = pos + new Vector3(0f, 0f, 9f);
            Primitive(PrimitiveType.Cylinder, parent, "Flagpole",
                poleBase + Vector3.up * 2.5f, new Vector3(0.15f, 2.5f, 0.15f), new Color(0.6f, 0.6f, 0.6f), visible: false);
            var flag = Primitive(PrimitiveType.Cube, parent, "Flag",
                poleBase + new Vector3(0.6f, 4.6f, 0f), new Vector3(1.2f, 0.7f, 0.05f), new Color(0.75f, 0.15f, 0.15f), visible: false);
        }

        static TownBuilding Building(Transform parent, Vector3 basePos, Vector3 size,
            Color wall, Color roof, TownBuildingType type, string displayName, string roofImageKey = null)
        {
            var body = Primitive(PrimitiveType.Cube, parent, displayName,
                basePos + Vector3.up * (size.y / 2f), size, wall, visible: false);
            var roofGo = Primitive(PrimitiveType.Cube, parent, displayName + "_Roof",
                basePos + Vector3.up * (size.y + 0.3f), new Vector3(size.x * 1.1f, 0.6f, size.z * 1.1f), roof, visible: false);
            roofGo.transform.SetParent(body.transform, true);

            if (roofImageKey != null)
                RoofCap(parent, body.transform, basePos, size, roofImageKey);

            var tb = body.AddComponent<TownBuilding>();
            tb.Type = type;
            tb.DisplayName = displayName;
            return tb;
        }

        /// <summary>M34: a textured plane sitting just above the (invisible) roof box,
        /// cropped from the reference image at roughly this building's own spot --
        /// gives a bas-relief "the painted roof pokes up out of the flat ground" look
        /// without needing real 3D wall geometry, which a single top-down painting has
        /// no data for anyway (see PROJECT-README's M34 entry). Crop boxes are hand-
        /// eyeballed against the source image, not pixel-measured -- expect drift.</summary>
        static void RoofCap(Transform parent, Transform body, Vector3 basePos, Vector3 size, string roofImageKey)
        {
            var texture = Resources.Load<Texture2D>(RoofImageResourceDir + roofImageKey);
            if (texture == null) return; // missing crop -- fail quiet, keep the flat-colour roof box underneath.

            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = "RoofCap";
            Object.DestroyImmediate(go.GetComponent<Collider>()); // the roof box already provides collision.
            // Built against `parent` (the un-scaled scene root), same as every other
            // Primitive() call -- NOT against `body`, whose own non-uniform localScale
            // (== the building's size) would otherwise get multiplied into this plane's
            // scale too if parented to it directly. Reparented to `body` afterward, with
            // worldPositionStays so Unity compensates the local values for us, purely to
            // keep the hierarchy tidy under the building it belongs to.
            go.transform.SetParent(parent, false);
            go.transform.position = basePos + Vector3.up * (size.y + 0.65f);
            go.transform.localScale = new Vector3(size.x * 1.1f / 10f, 1f, size.z * 1.1f / 10f);
            go.transform.SetParent(body, true);

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(shader) { mainTexture = texture };
            renderer.material = mat;
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

        /// <summary>`visible: false` (M33) keeps the collider -- walking still blocks on
        /// this shape -- but switches the renderer off, since the reference-image ground
        /// (`BuildGroundImage`) now shows this building/tree/road/plaza already painted
        /// in. Gates stay visible (default true): they're a Town-only affordance, not
        /// part of the original artwork, so they need to read as real objects.</summary>
        static GameObject Primitive(PrimitiveType type, Transform parent, string name,
            Vector3 localPos, Vector3 scale, Color color, bool visible = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                if (!visible)
                {
                    renderer.enabled = false;
                }
                else
                {
                    var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                    var mat = new Material(shader) { color = color };
                    if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.05f);
                    renderer.material = mat;
                }
            }

            return go;
        }
    }
}
