using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Farm
{
    /// <summary>Input and presentation orchestration for the farm simulation.</summary>
    public sealed class FarmController : MonoBehaviour
    {
        static readonly FarmTool[] ToolOrder =
        {
            FarmTool.Hoe,
            FarmTool.WateringCan,
            FarmTool.Scythe,
            FarmTool.Axe,
            FarmTool.Pickaxe
        };

        static readonly string[] CropOrder = { "haven_turnip", "dungeon_glowroot" };

        public FarmWorld World { get; private set; }
        public FarmTool SelectedTool { get; private set; } = FarmTool.Hoe;
        public string SelectedCropId { get; private set; } = CropOrder[0];
        public string LastMessage { get; private set; } = "Till, fertilize, plant, and water. Crops grow with time or battles.";
        public string StatusKind { get; private set; } = "info";

        FarmVisuals _visuals;
        Camera _camera;
        FarmSaveRepository _saves;
        float _nextGrowthRefresh;

        public void Init(FarmWorld world, FarmVisuals visuals, Camera camera, FarmSaveRepository saves)
        {
            World = world;
            _visuals = visuals;
            _camera = camera;
            _saves = saves;
        }

        void Update()
        {
            if (World == null) return;
            HandleKeyboard();
            HandlePointer();
            if (Time.unscaledTime >= _nextGrowthRefresh)
            {
                _nextGrowthRefresh = Time.unscaledTime + 1f;
                _visuals.RefreshAllTiles();
            }
        }

        void HandleKeyboard()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) Move(0, -1);
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) Move(0, 1);
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) Move(-1, 0);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) Move(1, 0);

            if (Input.GetKeyDown(KeyCode.Alpha1)) SelectTool(FarmTool.Hoe);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SelectTool(FarmTool.WateringCan);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SelectTool(FarmTool.Scythe);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SelectTool(FarmTool.Axe);
            if (Input.GetKeyDown(KeyCode.Alpha5)) SelectTool(FarmTool.Pickaxe);
            if (Input.GetKeyDown(KeyCode.Q)) CycleSeed();

            if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)) UseSelectedTool();
            if (Input.GetKeyDown(KeyCode.P)) PlantSelectedSeed();
            if (Input.GetKeyDown(KeyCode.F)) ApplyFertilizer();
            if (Input.GetKeyDown(KeyCode.R)) Harvest();
            if (Input.GetKeyDown(KeyCode.B)) SimulateBattleCompletion();
        }

        void HandlePointer()
        {
            if (_camera == null) return;
            var ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 100f))
            {
                _visuals.SetHover(null);
                return;
            }

            var cell = FarmIso.WorldToGrid(hit.point);
            _visuals.SetHover(cell);
            if (!Input.GetMouseButtonDown(0) || !World.InBounds(cell.x, cell.y)) return;

            var distance = Mathf.Abs(cell.x - World.Player.X) + Mathf.Abs(cell.y - World.Player.Y);
            if (distance == 0)
            {
                if (World.IsCropReady(cell.x, cell.y)) Apply(World.Harvest(cell.x, cell.y));
                else Apply(World.UseTool(cell.x, cell.y, SelectedTool));
            }
            else if (distance == 1 && World.GetObstacle(cell.x, cell.y) != null)
                Apply(World.UseTool(cell.x, cell.y, SelectedTool));
            else if (distance == 1)
                Move(cell.x - World.Player.X, cell.y - World.Player.Y);
            else Push("Move closer to interact with that tile.", "warn");
        }

        void Move(int dx, int dy)
        {
            var result = World.Move(dx, dy);
            Apply(result);
            if (result.Succeeded) RefreshTargetMessage();
        }

        void Apply(FarmActionResult result)
        {
            if (result == null) return;
            Push(result.Message, result.Succeeded ? SuccessKind(result.Code) : "warn");
            if (!result.Succeeded) return;

            if (result.Code == FarmActionCode.Moved) _visuals.SyncPlayer();
            else if (result.Code == FarmActionCode.BattleApplied) _visuals.RefreshAllTiles();
            else _visuals.RefreshTile(result.Position.x, result.Position.y);

            if (result.LevelsGained > 0) Push("Farm level up! Now Lv." + World.Player.Level + ".", "level");
            Save();
        }

        void RefreshTargetMessage()
        {
            var target = FacingTile();
            var tile = World.GetTile(target.x, target.y);
            if (tile == null) return;
            var crop = World.GetCrop(target.x, target.y);
            var obstacle = World.GetObstacle(target.x, target.y);
            if (crop != null)
                Push(World.GetGrowthMessage(target.x, target.y), World.IsCropReady(target.x, target.y) ? "ok" : "info");
            else if (obstacle != null)
                Push(obstacle.label + " — use " + obstacle.tool + ".", World.Player.Level >= obstacle.requiredLevel ? "info" : "warn");
            else Push(World.GetSoil(target.x, target.y) == FarmSoilKind.Tilled ? "Prepared soil." : "Wild grass.", "info");
        }

        void Push(string message, string kind)
        {
            LastMessage = message;
            StatusKind = kind;
        }

        void Save()
        {
            try { _saves?.Save(World.SaveData); }
            catch (System.Exception exception) { Debug.LogWarning("[Adams Haven] Farm save failed: " + exception.Message); }
        }

        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        Vector2Int FacingTile()
        {
            var target = new Vector2Int(World.Player.X + World.Player.FacingX, World.Player.Y + World.Player.FacingY);
            return World.InBounds(target.x, target.y) ? target : new Vector2Int(World.Player.X, World.Player.Y);
        }

        public string SelectedCropName
        {
            get
            {
                FarmCropRule crop;
                return World != null && World.Simulation.Content.TryGetCrop(SelectedCropId, out crop) ? crop.DisplayName : SelectedCropId;
            }
        }

        public int SelectedSeedCount
        {
            get
            {
                FarmCropRule crop;
                return World != null && World.Simulation.Content.TryGetCrop(SelectedCropId, out crop)
                    ? World.GetItemCount(crop.SeedItemId)
                    : 0;
            }
        }

        public int FertilizerCount => World == null ? 0 : World.GetItemCount("fertilizer_basic");

        public void SelectTool(FarmTool tool)
        {
            SelectedTool = tool;
            Push("Selected " + FormatTool(tool) + ".", "info");
        }

        public void CycleTool()
        {
            var index = System.Array.IndexOf(ToolOrder, SelectedTool);
            SelectTool(ToolOrder[(index + 1) % ToolOrder.Length]);
        }

        public void CycleSeed()
        {
            var index = System.Array.IndexOf(CropOrder, SelectedCropId);
            SelectedCropId = CropOrder[(index + 1) % CropOrder.Length];
            Push("Selected " + SelectedCropName + " seeds.", "info");
        }

        public void UseSelectedTool()
        {
            var target = FacingTile();
            Apply(World.UseTool(target.x, target.y, SelectedTool));
        }

        public void PlantSelectedSeed()
        {
            var target = FacingTile();
            Apply(World.Plant(target.x, target.y, SelectedCropId));
        }

        public void ApplyFertilizer()
        {
            var target = FacingTile();
            Apply(World.Fertilize(target.x, target.y, "fertilizer_basic"));
        }

        public void Harvest()
        {
            var target = FacingTile();
            Apply(World.Harvest(target.x, target.y));
        }

        public void SimulateBattleCompletion()
        {
            Apply(World.ApplyBattle(new FarmBattleReport
            {
                stageId = "farm_foundation_test",
                victory = true,
                battlesCompleted = 1
            }));
        }

        /// <summary>Placeholder return leg for the battle&lt;-&gt;farm boundary (M29 added
        /// the one-directional camp-to-farm trip; this is the way back). "Camp",
        /// "home", and "town" are all the same undifferentiated destination right now --
        /// no dedicated town/home scene exists yet, so this just re-enters Battle.unity,
        /// which boots a fresh dungeon run the same way pressing Play on that scene
        /// always has. Revisit once there's an actual town/home scene and a reason to
        /// hand off farm state on the way back.</summary>
        public void ReturnToDungeon() => SceneManager.LoadScene("Battle");

        public void UiMove(int dx, int dy) => Move(dx, dy);

        public static string FormatTool(FarmTool tool) => tool == FarmTool.WateringCan ? "Watering Can" : tool.ToString();

        static string SuccessKind(FarmActionCode code)
        {
            return code == FarmActionCode.Harvested || code == FarmActionCode.ObstacleCleared ? "ok" : "info";
        }
    }
}
