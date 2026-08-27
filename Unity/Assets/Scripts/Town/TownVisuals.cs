using System.Collections.Generic;
using UnityEngine;

namespace Game.Town
{
    /// <summary>Builds the Town street at runtime (M40) -- a flat side-scroller,
    /// replacing M30-M39's top-down 2.5D crossroads.
    ///
    /// The old layout put four districts in four quadrants around a crossroads on the
    /// XZ plane, viewed through a pitched camera. A flat side view has one walkable
    /// axis, so the districts became four consecutive stretches of a single street
    /// running along X: Commercial, then Housing, then Industrial, then Government,
    /// with a gate at each end (Farm on the left, the dungeon on the right).
    ///
    /// **The reference-image ground is gone, and can't come back as-is.**
    /// `town_ground_empty_01.png` (M37) is a top-down painting of a clearing -- there
    /// is no way to read it as ground from a side-on camera, so the street is flat
    /// colour for now with a simple treeline behind it. The asset stays on disk,
    /// untouched, for whenever a side-view backdrop exists to replace it (or if a
    /// top-down map screen ever wants it). See PROJECT-README's M40 entry.
    ///
    /// Plots render as translucent standing "ghosts" of the building that will occupy
    /// them, going solid once built (`TownBuilding.ApplyStateColor`) -- the same
    /// city-builder convention where an unbuilt lot shows a preview outline.</summary>
    public static class TownVisuals
    {
        // Street runs from -StreetHalfLength to +StreetHalfLength along X.
        const float StreetHalfLength = 70f;
        const float GroundDepth = 6f;      // Z extent -- enough for the player capsule to stand on.
        const float PlotZ = 2f;            // Plots sit behind the player's walking line (Z = 0).
        const float PlotDepth = 1.2f;
        const float TreelineZ = 7f;
        const float PlotSpacing = 8f;      // > widest plot half-width pair, so nothing overlaps.

        static readonly Color GroundColor = new(0.55f, 0.45f, 0.32f);
        static readonly Color TreeColor = new(0.16f, 0.30f, 0.18f);
        static readonly Color GateColor = new(0.85f, 0.75f, 0.25f);
        static readonly Color CommercialPlotColor = new(0.68f, 0.42f, 0.22f, 0.35f);
        static readonly Color HousingPlotColor = new(0.4f, 0.55f, 0.35f, 0.35f);
        static readonly Color IndustrialPlotColor = new(0.5f, 0.45f, 0.4f, 0.35f);
        static readonly Color GovernmentPlotColor = new(0.35f, 0.45f, 0.62f, 0.35f);

        public class BuildResult
        {
            public Vector3 PlayerSpawn;
            public readonly List<TownBuilding> Buildings = new();
            public readonly List<TownGate> Gates = new();
        }

        public static BuildResult Build(Transform parent)
        {
            // Slightly above the ground surface so gravity settles the player onto it
            // rather than starting them intersecting it.
            var result = new BuildResult { PlayerSpawn = new Vector3(0f, 1.2f, 0f) };

            BuildGround(parent);
            BuildTreeline(parent);
            BuildBounds(parent);
            BuildPlots(parent, result.Buildings);

            BuildGate(parent, result.Gates, "Path to the Farm", "Farm", -StreetHalfLength + 4f);
            BuildGate(parent, result.Gates, "Path to the Dungeon", "Battle", StreetHalfLength - 4f);

            return result;
        }

        /// <summary>The street itself -- the one collider the player actually stands
        /// on, since SimpleMove applies gravity every frame.</summary>
        static void BuildGround(Transform parent)
        {
            Primitive(PrimitiveType.Cube, parent, "Street",
                new Vector3(0f, -0.5f, 0f),
                new Vector3(StreetHalfLength * 2f + 6f, 1f, GroundDepth),
                GroundColor);
        }

        /// <summary>Flat backdrop so the camera isn't staring into empty clear-colour
        /// behind the buildings. Deliberately crude -- placeholder set dressing until
        /// real side-view art exists, same status as the plot ghosts.</summary>
        static void BuildTreeline(Transform parent)
        {
            for (var x = -StreetHalfLength; x <= StreetHalfLength; x += 6f)
            {
                var height = 5f + 2f * Mathf.Abs(Mathf.Sin(x * 0.7f));
                var tree = Primitive(PrimitiveType.Cube, parent, "BackdropTree",
                    new Vector3(x, height / 2f, TreelineZ),
                    new Vector3(4.5f, height, 1f), TreeColor);
                StripCollider(tree);
            }
        }

        /// <summary>Invisible walls capping each end of the street, so the player can't
        /// walk off into nothing. Keeps its collider -- unlike everything else here,
        /// blocking is the entire point.</summary>
        static void BuildBounds(Transform parent)
        {
            foreach (var side in new[] { -1f, 1f })
            {
                Primitive(PrimitiveType.Cube, parent, "StreetBound",
                    new Vector3(side * (StreetHalfLength + 1f), 3f, 0f),
                    new Vector3(1f, 8f, GroundDepth), Color.white, visible: false);
            }
        }

        /// <summary>The four districts, laid out left-to-right as consecutive stretches
        /// of the street. Same counts and same `TownDistrict` tagging M36-M38 used --
        /// only the arrangement changed, so the build rules, catalog, and economy carry
        /// over untouched.</summary>
        static void BuildPlots(Transform parent, List<TownBuilding> buildings)
        {
            var slot = 0;
            const int total = 16; // 5 commercial + 6 housing + 4 industrial + 1 government
            float NextX() => -((total - 1) * PlotSpacing / 2f) + (slot++ * PlotSpacing);

            for (var i = 0; i < 5; i++)
                buildings.Add(Plot(parent, NextX(), new Vector2(5f, 5f),
                    CommercialPlotColor, TownDistrict.Commercial, $"Commercial Plot {i + 1}"));

            for (var i = 0; i < 6; i++)
                buildings.Add(Plot(parent, NextX(), new Vector2(4.5f, 4f),
                    HousingPlotColor, TownDistrict.Housing, $"Housing Plot {i + 1}"));

            for (var i = 0; i < 4; i++)
                buildings.Add(Plot(parent, NextX(), new Vector2(6f, 5.5f),
                    IndustrialPlotColor, TownDistrict.Industrial, $"Industrial Plot {i + 1}"));

            buildings.Add(Plot(parent, NextX(), new Vector2(9f, 7f),
                GovernmentPlotColor, TownDistrict.Government, "Government Plot 1"));
        }

        /// <summary>A standing, translucent box marking a buildable lot -- collider
        /// stripped on purpose (plots are things you walk past and interact with, not
        /// obstacles), and set back at `PlotZ` so the player always draws in front of
        /// them.</summary>
        static TownBuilding Plot(Transform parent, float x, Vector2 size, Color color,
            TownDistrict district, string displayName)
        {
            var go = Primitive(PrimitiveType.Cube, parent, displayName,
                new Vector3(x, size.y / 2f, PlotZ),
                new Vector3(size.x, size.y, PlotDepth), color, transparent: true);
            StripCollider(go);

            var tb = go.AddComponent<TownBuilding>();
            tb.District = district;
            tb.DisplayName = displayName;
            tb.EmptyColor = color;
            return tb;
        }

        static void BuildGate(Transform parent, List<TownGate> gates, string displayName,
            string targetScene, float x)
        {
            var post = Primitive(PrimitiveType.Cube, parent, "Gate_" + targetScene,
                new Vector3(x, 2f, 0.8f), new Vector3(1.5f, 4f, 0.4f), GateColor);
            StripCollider(post); // proximity-triggered, not a physical barrier.

            var gate = post.AddComponent<TownGate>();
            gate.DisplayName = displayName;
            gate.TargetSceneName = targetScene;
            gates.Add(gate);
        }

        static void StripCollider(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
        }

        /// <summary>`visible: false` keeps the collider but switches the renderer off
        /// (the street bounds). `transparent: true` picks a shader that actually
        /// honours the colour's alpha, which the plot ghosts need and Standard
        /// wouldn't give them without extra render-mode setup.</summary>
        static GameObject Primitive(PrimitiveType type, Transform parent, string name,
            Vector3 localPos, Vector3 scale, Color color, bool visible = true, bool transparent = false)
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
                    var shader = transparent
                        ? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color")
                        : Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
                    var mat = new Material(shader) { color = color };
                    renderer.material = mat;
                }
            }

            return go;
        }
    }
}
