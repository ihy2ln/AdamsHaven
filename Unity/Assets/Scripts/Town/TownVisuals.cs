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
        const float PlotSpacing = 8f;      // > widest plot half-width pair, so nothing overlaps.

        // Depth layers, near camera (-Z) to far (+Z): foreground dressing, the player's
        // walking line at 0, ground dressing, plots, mid trees, far trees, backdrop.
        const float ForegroundZ = -2.4f;
        const float MidTreeZ = 7f;
        const float FarTreeZ = 12f;
        const float BackdropZ = 18f;

        const string PropsResourceDir = "Town/Art/Props/";
        /// <summary>Optional. Absent today -- `BuildBackdrop` falls back to the camera's
        /// flat clear colour rather than failing, so dropping a side-view backdrop PNG
        /// in at this path lights it up with no code change.</summary>
        const string BackdropResourcePath = "Town/Art/town_backdrop_hills";

        static readonly Color GroundColor = new(0.55f, 0.45f, 0.32f);
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

            BuildBackdrop(parent);
            BuildGround(parent);
            BuildTreeline(parent);
            BuildGroundDressing(parent);
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

        /// <summary>Distant scenery quad, tiled horizontally so the source art keeps
        /// its own aspect instead of being stretched across the whole street. Silently
        /// does nothing when the texture is absent -- see `BackdropResourcePath`.</summary>
        static void BuildBackdrop(Transform parent)
        {
            var tex = Resources.Load<Texture2D>(BackdropResourcePath);
            if (tex == null) return;
            tex.wrapMode = TextureWrapMode.Repeat; // tiling needs it regardless of import settings.

            const float height = 34f;
            var totalWidth = StreetHalfLength * 2f + 20f;
            var tileWidth = height * tex.width / (float)tex.height;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Backdrop";
            StripCollider(go);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height / 2f - 6f, BackdropZ);
            go.transform.localScale = new Vector3(totalWidth, height, 1f);

            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { mainTexture = tex };
            mat.mainTextureScale = new Vector2(Mathf.Ceil(totalWidth / tileWidth), 1f);
            go.GetComponent<Renderer>().material = mat;
        }

        /// <summary>Two depth layers of real tree art (M41). The far layer is large,
        /// tinted down and closely spaced so it reads as a solid treeline rather than
        /// individual trees; the mid layer is smaller, brighter and sparser. Placement
        /// is seeded, not `UnityEngine.Random` -- the street should look identical
        /// every boot, and this also avoids disturbing any other system's random
        /// state.</summary>
        static void BuildTreeline(Transform parent)
        {
            var tree = Prop("tree");
            if (tree == null) return;
            var rng = new System.Random(20260825);

            for (var x = -StreetHalfLength; x <= StreetHalfLength; x += 9f)
                Sprite(parent, "Tree_Far", tree,
                    x + Jitter(rng, 2f), FarTreeZ, 14f + (float)rng.NextDouble() * 5f,
                    new Color(0.42f, 0.5f, 0.44f), rng.Next(2) == 0);

            for (var x = -StreetHalfLength + 5f; x <= StreetHalfLength; x += 17f)
                Sprite(parent, "Tree_Mid", tree,
                    x + Jitter(rng, 2.5f), MidTreeZ, 9f + (float)rng.NextDouble() * 3f,
                    new Color(0.72f, 0.78f, 0.72f), rng.Next(2) == 0);
        }

        /// <summary>Weeds, rocks and stray crops along the street, plus a sparse
        /// foreground layer nearer the camera than the player for a bit of depth.
        /// Foreground pieces are kept short on purpose -- tall ones would swallow the
        /// player as they walked behind them.</summary>
        static void BuildGroundDressing(Transform parent)
        {
            var weed = Prop("weed");
            var rock = Prop("rock");
            var cabbage = Prop("cabbage");
            var radish = Prop("radish");
            var rng = new System.Random(70011);

            for (var x = -StreetHalfLength + 3f; x <= StreetHalfLength - 3f; x += 4.5f)
            {
                // Leave the gate pads clear so nothing hides an exit.
                if (Mathf.Abs(Mathf.Abs(x) - (StreetHalfLength - 4f)) < 3.5f) continue;

                var roll = rng.Next(100);
                var (tex, baseHeight, label) =
                    roll < 45 ? (weed, 1.5f, "Weed")
                    : roll < 70 ? (rock, 1.1f, "Rock")
                    : roll < 85 ? (cabbage, 1.2f, "Cabbage")
                    : (radish, 1.3f, "Radish");

                Sprite(parent, label, tex,
                    x + Jitter(rng, 1.2f),
                    0.7f + (float)rng.NextDouble() * 1.1f,
                    baseHeight * (0.8f + (float)rng.NextDouble() * 0.5f),
                    Color.white, rng.Next(2) == 0);
            }

            for (var x = -StreetHalfLength + 8f; x <= StreetHalfLength - 8f; x += 21f)
                Sprite(parent, "Foreground", rng.Next(2) == 0 ? weed : rock,
                    x + Jitter(rng, 2f), ForegroundZ, 1.6f,
                    new Color(0.78f, 0.8f, 0.78f), rng.Next(2) == 0);
        }

        static float Jitter(System.Random rng, float amount) =>
            (float)(rng.NextDouble() * 2.0 - 1.0) * amount;

        static Texture2D Prop(string name) => Resources.Load<Texture2D>(PropsResourceDir + name);

        /// <summary>A textured quad standing on the ground, sized from the texture's own
        /// aspect so nothing is squashed. `Sprites/Default` is deliberate on two counts:
        /// it honours the PNG's alpha, and it has `Cull Off` -- so the quad renders no
        /// matter which way Unity's built-in Quad mesh happens to face, with no
        /// 180-degree guesswork. Negative X scale mirrors a prop so repeats of the same
        /// texture don't read as obvious copies.</summary>
        static GameObject Sprite(Transform parent, string name, Texture2D tex,
            float x, float z, float height, Color tint, bool flip = false)
        {
            if (tex == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            StripCollider(go); // scenery, never an obstacle.
            go.transform.SetParent(parent, false);

            var width = height * tex.width / (float)tex.height;
            go.transform.localPosition = new Vector3(x, height / 2f, z);
            go.transform.localScale = new Vector3(flip ? -width : width, height, 1f);

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
            go.GetComponent<Renderer>().material =
                new Material(shader) { mainTexture = tex, color = tint };
            return go;
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
