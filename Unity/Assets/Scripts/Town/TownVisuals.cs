using System.Collections.Generic;
using UnityEngine;

namespace Game.Town
{
    /// <summary>Builds the Town plot at runtime. The project owner's reference image
    /// paints the ground directly (`Resources/Town/Art/town_ground_empty_01.png`,
    /// loaded at runtime) -- dirt roads in a cross pattern through forest, genuinely
    /// empty this time (M37; the original `town_reference_01.png` still had M30/M31's
    /// buildings painted in, which never matched the "starts empty" direction -- kept
    /// on disk, unused, in case a with-buildings reference is wanted again later).
    /// Matches §5.5: buildings
    /// cost materials and a real-time build countdown, so the town legitimately starts
    /// with nothing actually built, rather than a hardcoded blockout standing in for
    /// real construction. M36 adds back what M30/M31's removed building blockout was
    /// really marking: not buildings, but the *plots of land* they'll eventually occupy
    /// -- flat, walkable, collider-free markers (`TownBuilding`, reused rather than
    /// renamed -- see its own doc comment) at the same four-district layout as before,
    /// each reading "not built yet" until a real building-placement system exists.</summary>
    public static class TownVisuals
    {
        const string ReferenceImageResourcePath = "Town/Art/town_ground_empty_01";
        const float RoadHalfWidth = 2.5f;
        const float RoadReach = 42f;
        const float ForestRadius = 46f;

        static readonly Color RoadColor = new(0.42f, 0.38f, 0.32f);
        static readonly Color GroundColor = new(0.29f, 0.42f, 0.24f);
        static readonly Color TrunkColor = new(0.3f, 0.22f, 0.14f);
        static readonly Color LeafColor = new(0.16f, 0.32f, 0.16f);
        static readonly Color GateColor = new(0.85f, 0.75f, 0.25f);
        static readonly Color MarketPlotColor = new(0.68f, 0.42f, 0.22f, 0.35f);
        static readonly Color ResidentialPlotColor = new(0.4f, 0.55f, 0.35f, 0.35f);
        static readonly Color UtilityPlotColor = new(0.5f, 0.45f, 0.4f, 0.35f);
        static readonly Color CivicPlotColor = new(0.35f, 0.45f, 0.62f, 0.35f);

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

            BuildPlots(parent, result.Buildings);

            BuildGate(parent, result.Gates, "Path to the Farm", "Farm", new Vector3(-ForestRadius + 4f, 0f, -ForestRadius + 4f));
            BuildGate(parent, result.Gates, "Path to the Dungeon", "Battle", new Vector3(ForestRadius - 4f, 0f, -ForestRadius + 4f));

            return result;
        }

        /// <summary>M36: the four districts from the reference image, as empty,
        /// walkable plots rather than solid buildings -- same positions/footprints
        /// M30/M31 used for actual building blockouts, reused here since they already
        /// approximate the picture's layout reasonably well.</summary>
        static void BuildPlots(Transform parent, List<TownBuilding> buildings)
        {
            var marketNames = new[] { "General Store Plot", "Apothecary Plot", "Weaver's Stall Plot", "Fishmonger Plot", "Trading Post Plot" };
            var marketStart = new Vector3(-14f, 0f, 14f);
            for (var i = 0; i < marketNames.Length; i++)
            {
                var pos = marketStart + new Vector3(-i * 6.5f, 0f, 0f);
                buildings.Add(Plot(parent, pos, new Vector2(5f, 5f), MarketPlotColor, TownBuildingType.MarketStall, marketNames[i]));
            }

            var residentialOrigin = new Vector3(14f, 0f, 30f);
            for (var i = 0; i < 6; i++)
            {
                var col = i % 2;
                var row = i / 2;
                var pos = residentialOrigin + new Vector3(col * 11f, 0f, -row * 11f);
                buildings.Add(Plot(parent, pos, new Vector2(4.5f, 4.5f), ResidentialPlotColor, TownBuildingType.House, $"Cottage Plot {i + 1}"));
            }

            var utilityNames = new[] { "Storehouse Plot", "Tool Shed Plot", "Grain Barn Plot" };
            var utilityOrigin = new Vector3(-14f, 0f, -14f);
            for (var i = 0; i < utilityNames.Length; i++)
            {
                var pos = utilityOrigin + new Vector3(-i * 8f, 0f, -(i % 2) * 6f);
                buildings.Add(Plot(parent, pos, new Vector2(6f, 6f), UtilityPlotColor, TownBuildingType.UtilityShed, utilityNames[i]));
            }
            var towerPos = utilityOrigin + new Vector3(6f, 0f, -10f);
            buildings.Add(Plot(parent, towerPos, new Vector2(3f, 3f), UtilityPlotColor, TownBuildingType.UtilityShed, "Water Tower Plot"));

            var hallPos = new Vector3(16f, 0f, -14f);
            buildings.Add(Plot(parent, hallPos, new Vector2(9f, 7f), CivicPlotColor, TownBuildingType.TownHall, "Town Hall Plot"));
        }

        /// <summary>A flat, walkable, translucent rectangle marking an empty buildable
        /// lot -- collider stripped on purpose (the point of a plot is you can stand on
        /// it while nothing's built there), positioned just above the ground image so it
        /// doesn't z-fight with it.</summary>
        static TownBuilding Plot(Transform parent, Vector3 pos, Vector2 footprint, Color color, TownBuildingType type, string displayName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = displayName;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // plots are walkable, not obstacles.
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos + Vector3.up * 0.03f;
            go.transform.localScale = new Vector3(footprint.x / 10f, 1f, footprint.y / 10f);

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
                renderer.material = new Material(shader) { color = color };
            }

            var tb = go.AddComponent<TownBuilding>();
            tb.Type = type;
            tb.DisplayName = displayName;
            return tb;
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
