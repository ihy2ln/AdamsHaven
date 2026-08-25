using System.Collections.Generic;
using UnityEngine;

namespace Game.Farm
{
    /// <summary>
    /// Top-down anime × HD pixel visuals for the authored 16×16 clearing farm.
    /// Point-filtered textures, crop stages, watered soil, and a chibi farmer.
    /// </summary>
    public class FarmVisuals : MonoBehaviour
    {
        FarmWorld _world;
        readonly Dictionary<Vector2Int, GameObject> _tileViews = new();
        readonly Dictionary<Vector2Int, GameObject> _soilViews = new();
        readonly Dictionary<Vector2Int, GameObject> _obstacleViews = new();
        readonly Dictionary<Vector2Int, GameObject> _decorativeFloraViews = new();
        readonly Dictionary<Vector2Int, GameObject> _cropViews = new();
        readonly Dictionary<Vector2Int, string> _cropSignatures = new();
        Material _matGrassA, _matGrassB, _matTilled, _matWatered, _matWood, _matLeaf, _matLeafHi, _matRock, _matCloth, _matBadge, _matGrid;
        Material _matCropLeaf, _matCropReady, _matGlowroot;
        Material _matGrassTexture, _matTilledTexture, _matWateredTexture, _matPlotBackground;
        Material _matCabbageSprite, _matRadishSprite, _matRockSprite, _matTreeSprite, _matWeedSprite;
        Material _matBushSprite, _matCloverSprite, _matFlowerSprite, _matFernSprite;
        GameObject _playerView;
        GameObject _hover;
        Transform _obstacleRoot;
        Transform _cropRoot;

        public void Build(FarmWorld world)
        {
            _world = world;
            BuildMaterials();
            BuildAtmosphere();
            BuildGroundPlate();
            BuildFringe();
            BuildTiles();
            BuildObstacles();
            BuildCrops();
            BuildDecorativeFlora();
            BuildPlayer();
            BuildHover();
        }

        void BuildMaterials()
        {
            _matGrassA = FarmPixelArt.MakePixelMat(
                new Color(0.22f, 0.48f, 0.30f),
                new Color(0.34f, 0.62f, 0.36f), 16, 0.4f);
            _matGrassB = FarmPixelArt.MakePixelMat(
                new Color(0.18f, 0.40f, 0.26f),
                new Color(0.28f, 0.52f, 0.30f), 16, 0.35f);
            _matTilled = FarmPixelArt.MakePixelMat(
                new Color(0.48f, 0.30f, 0.18f),
                new Color(0.62f, 0.40f, 0.24f), 12, 0.45f);
            _matWatered = FarmPixelArt.MakePixelMat(
                new Color(0.28f, 0.20f, 0.18f),
                new Color(0.38f, 0.28f, 0.22f), 12, 0.45f);
            _matWood = FarmPixelArt.MakeFlatPixel(new Color(0.42f, 0.26f, 0.14f));
            _matLeaf = FarmPixelArt.MakeFlatPixel(new Color(0.18f, 0.46f, 0.26f));
            _matLeafHi = FarmPixelArt.MakeFlatPixel(new Color(0.82f, 0.52f, 0.18f));
            _matRock = FarmPixelArt.MakePixelMat(
                new Color(0.40f, 0.38f, 0.42f),
                new Color(0.62f, 0.58f, 0.56f), 10, 0.4f);
            _matCloth = FarmPixelArt.MakeFlatPixel(new Color(0.28f, 0.42f, 0.72f));
            _matBadge = FarmPixelArt.MakeFlatPixel(new Color(0.08f, 0.08f, 0.12f));
            _matGrid = FarmPixelArt.MakeFlatPixel(new Color(1f, 1f, 1f));
            _matCropLeaf = FarmPixelArt.MakeFlatPixel(new Color(0.30f, 0.68f, 0.28f));
            _matCropReady = FarmPixelArt.MakeFlatPixel(new Color(0.92f, 0.84f, 0.52f));
            _matGlowroot = FarmPixelArt.MakeFlatPixel(new Color(0.48f, 0.78f, 0.92f));

            _matGrassTexture = FarmPixelArt.MakeTextureMat(
                FarmPixelArt.LoadTexture("Farm/Art/background_ground_grass_00001_"), new Color(0.28f, 0.56f, 0.30f));
            _matTilledTexture = FarmPixelArt.MakeTextureMat(
                FarmPixelArt.LoadTexture("Farm/Art/background_ground_tilled_00001_"), new Color(0.48f, 0.30f, 0.18f));
            _matWateredTexture = FarmPixelArt.MakeTextureMat(
                FarmPixelArt.LoadTexture("Farm/Art/background_ground_tilled_00002_"), new Color(0.28f, 0.20f, 0.18f));
            _matPlotBackground = FarmPixelArt.MakeTextureMat(
                FarmPixelArt.LoadTexture("Farm/Art/farm-layout-clearing-03"), new Color(0.22f, 0.32f, 0.22f));
            _matCabbageSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/sprite_cabbage_00001_"), new Color(0.30f, 0.68f, 0.28f));
            _matRadishSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/sprite_radishrow_00001_"), new Color(0.80f, 0.25f, 0.42f));
            _matRockSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/sprite_rock_00001_"), new Color(0.40f, 0.38f, 0.42f));
            _matTreeSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/Plants/flora_sheet01_017"), new Color(0.18f, 0.46f, 0.26f));
            _matWeedSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/Plants/flora_sheet04_005"), new Color(0.18f, 0.46f, 0.26f));
            _matBushSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/Plants/flora_sheet01_010"), new Color(0.18f, 0.46f, 0.26f));
            _matCloverSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/Plants/flora_sheet01_002"), new Color(0.24f, 0.56f, 0.28f));
            _matFlowerSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/Plants/flora_sheet01_007"), new Color(0.70f, 0.40f, 0.52f));
            _matFernSprite = FarmPixelArt.MakeKeyedSpriteMat(
                FarmPixelArt.LoadTexture("Farm/Art/Plants/flora_sheet01_005"), new Color(0.20f, 0.52f, 0.30f));
        }

        void BuildAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.42f, 0.28f, 0.38f);
            RenderSettings.fogDensity = 0.045f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.48f, 0.62f);

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.78f, 0.52f);
            sun.intensity = 0.85f;
            sun.shadows = LightShadows.None; // flatter, more pixel/anime
            sun.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
            sun.transform.SetParent(transform, false);

            // Soft key spotlight over the farm diorama.
            var spot = new GameObject("StageSpot").AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.88f, 0.65f);
            spot.intensity = 1.8f;
            spot.range = 16f;
            spot.spotAngle = 55f;
            spot.transform.position = MapCenter + new Vector3(0.3f, 7f, -0.2f);
            spot.transform.LookAt(MapCenter);
            spot.transform.SetParent(transform, false);
        }

        void BuildGroundPlate()
        {
            // Illustrated farm environment under the interactive 16×16 grid.
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "StagePlate";
            plate.transform.SetParent(transform, false);
            plate.transform.position = MapCenter + new Vector3(0f, -0.32f, 0f);
            var plateSize = Mathf.Max(_world.Width, _world.Height) * FarmIso.TileSize * 1.65f;
            var layoutTexture = _matPlotBackground.mainTexture as Texture2D;
            var layoutAspect = layoutTexture == null ? 1f : (float)layoutTexture.width / layoutTexture.height;
            plate.transform.localScale = new Vector3(plateSize * layoutAspect, 0.08f, plateSize);
            plate.GetComponent<Renderer>().sharedMaterial = _matPlotBackground;
            Object.Destroy(plate.GetComponent<Collider>());
        }

        void BuildFringe()
        {
            // The authored clearing layout already supplies the forest edge.
        }

        void BuildTiles()
        {
            var root = new GameObject("Tiles").transform;
            root.SetParent(transform, false);
            for (var y = 0; y < _world.Height; y++)
            for (var x = 0; x < _world.Width; x++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"Tile_{x}_{y}";
                go.transform.SetParent(root, false);
                go.transform.position = FarmIso.GridToWorld(x, y, FarmIso.TileHeight * 0.5f);
                // The collider is the interaction cell. Keep it the same size as
                // the visual soil overlay so pointer targeting never lands in a
                // gap between farm squares.
                go.transform.localScale = new Vector3(FarmIso.TileSize, FarmIso.TileHeight, FarmIso.TileSize);
                _tileViews[new Vector2Int(x, y)] = go;
                // Keep the collider for pointer targeting, but let the authored
                // dirt layout remain the visible farmland surface.
                go.GetComponent<Renderer>().enabled = false;
                var overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
                overlay.name = $"SoilOverlay_{x}_{y}";
                overlay.transform.SetParent(root, false);
                overlay.transform.position = FarmIso.GridToWorld(x, y, FarmIso.TileHeight + 0.025f);
                overlay.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                // One overlay is one logical farm cell. The authored clearing is
                // the visible untilled ground, so this only appears after hoeing
                // and must line up edge-to-edge with the interaction grid.
                overlay.transform.localScale = new Vector3(FarmIso.TileSize, FarmIso.TileSize, 1f);
                Object.Destroy(overlay.GetComponent<Collider>());
                _soilViews[new Vector2Int(x, y)] = overlay;
                ApplyTileMaterial(go, x, y);
            }
        }

        void BuildObstacles()
        {
            _obstacleRoot = new GameObject("Obstacles").transform;
            _obstacleRoot.SetParent(transform, false);
            for (var y = 0; y < _world.Height; y++)
            for (var x = 0; x < _world.Width; x++)
            {
                var id = _world.GetObstacleId(x, y);
                if (string.IsNullOrEmpty(id)) continue;
                var view = CreateObstacleView(id, x, y, _world.GetObstacle(x, y));
                view.transform.SetParent(_obstacleRoot, false);
                _obstacleViews[new Vector2Int(x, y)] = view;
            }
        }

        void BuildCrops()
        {
            _cropRoot = new GameObject("Crops").transform;
            _cropRoot.SetParent(transform, false);
            for (var y = 0; y < _world.Height; y++)
            for (var x = 0; x < _world.Width; x++)
                RebuildCrop(x, y);
        }

        void BuildDecorativeFlora()
        {
            var root = new GameObject("DecorativeFlora").transform;
            root.SetParent(transform, false);
            // Deterministic accents keep the clearing readable while making the
            // separated plant assets feel planted in the authored forest edge.
            // They are presentation-only: no collider, crop state, or save data.
            // Offsets sit inside the cell so the art does not form a rigid row.
            AddDecorativeFlora(root, 2, 0, _matFernSprite, 0.52f, new Vector3(-0.08f, 0f, -0.12f));
            AddDecorativeFlora(root, 5, 0, _matFlowerSprite, 0.46f, new Vector3(0.08f, 0f, -0.10f));
            AddDecorativeFlora(root, 8, 0, _matCloverSprite, 0.44f, new Vector3(-0.03f, 0f, -0.08f));
            AddDecorativeFlora(root, 11, 0, _matBushSprite, 0.54f, new Vector3(0.10f, 0f, -0.10f));
            AddDecorativeFlora(root, 13, 1, _matFlowerSprite, 0.42f, new Vector3(0.10f, 0f, -0.03f));
            AddDecorativeFlora(root, 15, 2, _matFernSprite, 0.50f, new Vector3(0.10f, 0f, -0.05f));
            AddDecorativeFlora(root, 15, 5, _matCloverSprite, 0.46f, new Vector3(0.12f, 0f, 0.04f));
            AddDecorativeFlora(root, 15, 8, _matWeedSprite, 0.42f, new Vector3(0.10f, 0f, 0.08f));
            AddDecorativeFlora(root, 15, 11, _matBushSprite, 0.50f, new Vector3(0.10f, 0f, 0.03f));
            AddDecorativeFlora(root, 14, 13, _matFlowerSprite, 0.44f, new Vector3(0.08f, 0f, 0.08f));
            AddDecorativeFlora(root, 13, 15, _matFernSprite, 0.52f, new Vector3(0.06f, 0f, 0.10f));
            AddDecorativeFlora(root, 10, 15, _matCloverSprite, 0.44f, new Vector3(-0.05f, 0f, 0.10f));
            AddDecorativeFlora(root, 7, 15, _matFlowerSprite, 0.42f, new Vector3(0.04f, 0f, 0.11f));
            AddDecorativeFlora(root, 2, 15, _matBushSprite, 0.52f, new Vector3(-0.08f, 0f, 0.10f));
            AddDecorativeFlora(root, 0, 13, _matFernSprite, 0.50f, new Vector3(-0.10f, 0f, 0.06f));
            AddDecorativeFlora(root, 0, 10, _matFlowerSprite, 0.44f, new Vector3(-0.10f, 0f, -0.04f));
            AddDecorativeFlora(root, 0, 7, _matCloverSprite, 0.46f, new Vector3(-0.10f, 0f, 0.04f));
            AddDecorativeFlora(root, 0, 3, _matWeedSprite, 0.42f, new Vector3(-0.10f, 0f, -0.04f));
            AddDecorativeFlora(root, 2, 2, _matFlowerSprite, 0.40f, new Vector3(-0.08f, 0f, -0.06f));
            AddDecorativeFlora(root, 13, 4, _matCloverSprite, 0.42f, new Vector3(0.08f, 0f, 0.03f));
        }

        void AddDecorativeFlora(Transform parent, int x, int y, Material material, float scale, Vector3 cellOffset)
        {
            if (!_world.InBounds(x, y) || !string.IsNullOrEmpty(_world.GetObstacleId(x, y)) || _world.GetCrop(x, y) != null)
                return;
            var root = new GameObject($"Flora_{x}_{y}");
            root.transform.SetParent(parent, false);
            root.transform.position = FarmIso.GridToWorld(x, y, FarmIso.TileHeight)
                + new Vector3(cellOffset.x * FarmIso.TileSize, 0f, cellOffset.z * FarmIso.TileSize);
            AddSprite(root.transform, material, new Vector3(0f, 0.28f, 0f),
                new Vector3(scale, scale, scale));
            _decorativeFloraViews[new Vector2Int(x, y)] = root.gameObject;
        }

        void RebuildCrop(int x, int y)
        {
            var key = new Vector2Int(x, y);
            var crop = _world.GetCrop(x, y);
            var stage = crop == null ? 0 : _world.GetCropVisualStage(x, y);
            var ready = crop != null && _world.IsCropReady(x, y);
            var signature = crop == null ? "" : crop.CropId + ":" + stage + ":" + ready;
            if (_cropSignatures.TryGetValue(key, out var previous) && previous == signature) return;
            _cropSignatures[key] = signature;

            if (_cropViews.TryGetValue(key, out var oldView))
            {
                Destroy(oldView);
                _cropViews.Remove(key);
            }

            if (crop == null) return;
            var root = new GameObject($"Crop_{crop.CropId}_{x}_{y}");
            root.transform.SetParent(_cropRoot, false);
            root.transform.position = FarmIso.GridToWorld(x, y, FarmIso.TileHeight);

            var height = 0.18f + stage * 0.16f;
            var cropSprite = crop.CropId == "haven_turnip" ? _matRadishSprite : _matCabbageSprite;
            AddSprite(root.transform, cropSprite, new Vector3(0f, 0.34f + stage * 0.08f, 0f),
                new Vector3(0.82f + stage * 0.06f, 0.82f + stage * 0.06f, 0.82f + stage * 0.06f));
            if (ready)
            {
                var produceMaterial = crop.CropId == "dungeon_glowroot" ? _matGlowroot : _matCropReady;
                AddCube(root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.28f, 0.22f, 0.28f), produceMaterial);
            }
            _cropViews[key] = root;
        }

        void ApplyTileMaterial(GameObject tileView, int x, int y)
        {
            if (tileView != null) tileView.GetComponent<Renderer>().enabled = false;
            if (!_soilViews.TryGetValue(new Vector2Int(x, y), out var overlay)) return;
            var renderer = overlay.GetComponent<Renderer>();
            if (_world.IsWatered(x, y))
            {
                renderer.sharedMaterial = _matWateredTexture;
                renderer.enabled = true;
            }
            else if (_world.GetSoil(x, y) == FarmSoilKind.Tilled)
            {
                renderer.sharedMaterial = _matTilledTexture;
                renderer.enabled = true;
            }
            else
            {
                renderer.enabled = false;
            }
        }

        GameObject CreateObstacleView(string id, int x, int y, FarmObstacleType type)
        {
            if (string.IsNullOrEmpty(id) || type == null) return new GameObject($"Obs_Empty_{x}_{y}");
            var root = new GameObject($"Obs_{id}_{x}_{y}");
            root.transform.position = FarmIso.GridToWorld(x, y, FarmIso.TileHeight);

            switch (id)
            {
                case "weed":
                    AddSprite(root.transform, _matWeedSprite, new Vector3(0f, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f));
                    break;
                case "tree":
                    AddSprite(root.transform, _matTreeSprite, new Vector3(0f, 0.7f, 0f), new Vector3(1.35f, 1.35f, 1.35f));
                    break;
                case "rock":
                case "boulder":
                    AddSprite(root.transform, _matRockSprite, new Vector3(0f, 0.35f, 0f), new Vector3(0.95f, 0.95f, 0.95f));
                    break;
                /* Procedural fallback remains for the smaller farm-specific shapes. */
                case "old_weed":
                    for (var i = 0; i < 4; i++)
                    {
                        var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        blade.transform.SetParent(root.transform, false);
                        blade.transform.localPosition = new Vector3((i - 1.5f) * 0.12f, 0.28f, (i % 2) * 0.05f);
                        blade.transform.localScale = new Vector3(0.07f, 0.5f + i * 0.04f, 0.07f);
                        blade.transform.localRotation = Quaternion.Euler(0f, 0f, (i - 1.5f) * 10f);
                        blade.GetComponent<Renderer>().sharedMaterial = i % 2 == 0 ? _matLeaf : _matLeafHi;
                        Object.Destroy(blade.GetComponent<Collider>());
                    }
                    break;
                case "old_rock":
                    // Faceted pixel rock — stacked cubes
                    AddCube(root.transform, new Vector3(0f, 0.28f, 0f), new Vector3(0.55f, 0.4f, 0.5f), _matRock);
                    AddCube(root.transform, new Vector3(0.18f, 0.38f, 0.05f), new Vector3(0.32f, 0.28f, 0.3f), _matRock);
                    break;
                case "old_boulder":
                    AddCube(root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.9f, 0.72f, 0.82f), _matRock);
                    AddCube(root.transform, new Vector3(0.22f, 0.72f, 0.06f), new Vector3(0.48f, 0.38f, 0.42f), _matRock);
                    break;
                case "stump":
                    AddCube(root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.62f, 0.42f, 0.62f), _matWood);
                    AddCube(root.transform, new Vector3(0f, 0.48f, 0f), new Vector3(0.44f, 0.08f, 0.44f), _matCropReady);
                    break;
                case "bush":
                    AddSprite(root.transform, _matBushSprite, new Vector3(0f, 0.48f, 0f), new Vector3(1.05f, 1.05f, 1.05f));
                    break;
                case "old_tree":
                    AddCube(root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.9f, 0.2f), _matWood);
                    AddCube(root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(0.85f, 0.55f, 0.85f), _matLeaf);
                    AddCube(root.transform, new Vector3(0.2f, 1.35f, 0.1f), new Vector3(0.4f, 0.35f, 0.4f), _matLeafHi);
                    AddCube(root.transform, new Vector3(-0.15f, 1.25f, -0.1f), new Vector3(0.35f, 0.3f, 0.35f), _matLeaf);
                    break;
            }

            var hit = root.AddComponent<BoxCollider>();
            hit.center = new Vector3(0f, 0.55f, 0f);
            hit.size = new Vector3(0.95f, 1.3f, 0.95f);
            return root;
        }

        static void AddSprite(Transform parent, Material material, Vector3 localPosition, Vector3 localScale)
        {
            var sprite = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sprite.name = "GeneratedSprite";
            sprite.transform.SetParent(parent, false);
            sprite.transform.localPosition = localPosition;
            sprite.transform.localScale = localScale;
            sprite.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(sprite.GetComponent<Collider>());
            sprite.AddComponent<FarmBillboard>();
        }

        static void AddCube(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Object.Destroy(go.GetComponent<Collider>());
        }

        void BuildPlayer()
        {
            _playerView = new GameObject("Player");
            _playerView.transform.SetParent(transform, false);

            // Body — chibi proportions
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(_playerView.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            body.transform.localScale = new Vector3(0.38f, 0.45f, 0.28f);
            body.GetComponent<Renderer>().sharedMaterial = _matCloth;
            Object.Destroy(body.GetComponent<Collider>());

            // Cape accent
            var cape = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cape.transform.SetParent(_playerView.transform, false);
            cape.transform.localPosition = new Vector3(0.05f, 0.45f, 0.16f);
            cape.transform.localScale = new Vector3(0.3f, 0.4f, 0.06f);
            cape.GetComponent<Renderer>().sharedMaterial =
                FarmPixelArt.MakeFlatPixel(new Color(0.78f, 0.28f, 0.32f));
            Object.Destroy(cape.GetComponent<Collider>());

            // Anime face billboard
            var face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            face.transform.SetParent(_playerView.transform, false);
            face.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            face.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            var faceMat = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent"));
            faceMat.mainTexture = FarmPixelArt.MakeChibiFace(32);
            face.GetComponent<Renderer>().sharedMaterial = faceMat;
            Object.Destroy(face.GetComponent<Collider>());
            face.AddComponent<FarmBillboard>();

            SyncPlayer();
        }

        void BuildHover()
        {
            _hover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _hover.name = "Hover";
            _hover.transform.SetParent(transform, false);
            _hover.transform.localScale = new Vector3(FarmIso.TileSize * 0.96f, 0.06f, FarmIso.TileSize * 0.96f);
            _hover.GetComponent<Renderer>().sharedMaterial =
                FarmPixelArt.MakeFlatPixel(new Color(0.95f, 0.75f, 0.35f));
            Object.Destroy(_hover.GetComponent<Collider>());
            _hover.SetActive(false);
        }

        public void SyncPlayer()
        {
            if (_playerView == null || _world == null) return;
            _playerView.transform.position = FarmIso.GridToWorld(_world.Player.X, _world.Player.Y, FarmIso.TileHeight);
        }

        public void SetHover(Vector2Int? cell)
        {
            if (_hover == null) return;
            if (cell == null || !_world.InBounds(cell.Value.x, cell.Value.y))
            {
                _hover.SetActive(false);
                return;
            }
            _hover.SetActive(true);
            _hover.transform.position = FarmIso.GridToWorld(cell.Value.x, cell.Value.y, FarmIso.TileHeight + 0.06f);
        }

        public void RemoveObstacle(int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (_obstacleViews.TryGetValue(key, out var go))
            {
                Destroy(go);
                _obstacleViews.Remove(key);
            }
        }

        public void RefreshTile(int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (_tileViews.TryGetValue(key, out var tileView)) ApplyTileMaterial(tileView, x, y);
            if (string.IsNullOrEmpty(_world.GetObstacleId(x, y))) RemoveObstacle(x, y);
            if (_world.GetCrop(x, y) != null || _world.GetSoil(x, y) == FarmSoilKind.Tilled)
                RemoveDecorativeFlora(x, y);
            RebuildCrop(x, y);
        }

        void RemoveDecorativeFlora(int x, int y)
        {
            var key = new Vector2Int(x, y);
            if (!_decorativeFloraViews.TryGetValue(key, out var view)) return;
            Destroy(view);
            _decorativeFloraViews.Remove(key);
        }

        public void RefreshAllTiles()
        {
            for (var y = 0; y < _world.Height; y++)
            for (var x = 0; x < _world.Width; x++)
                RefreshTile(x, y);
        }

        public Vector3 MapCenter =>
            FarmIso.GridToWorld((_world.Width - 1) * 0.5f, (_world.Height - 1) * 0.5f, 0f)
            + new Vector3(0f, 0.2f, 0f);
    }

    public class FarmBillboard : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - Camera.main.transform.position);
        }
    }
}
