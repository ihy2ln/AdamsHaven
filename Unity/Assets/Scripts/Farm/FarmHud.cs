using UnityEngine;

namespace Game.Farm
{
    /// <summary>Code-first HUD for the farm foundation; no scene wiring required.</summary>
    public sealed class FarmHud : MonoBehaviour
    {
        FarmController _controller;
        GUIStyle _title, _body, _small, _warn, _ok, _level, _button;

        public void Init(FarmController controller) => _controller = controller;

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.91f, 0.69f, 0.35f) }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = new Color(0.94f, 0.90f, 0.83f) },
                wordWrap = true
            };
            _small = new GUIStyle(_body) { fontSize = 12 };
            _warn = new GUIStyle(_body) { normal = { textColor = new Color(1f, 0.55f, 0.5f) } };
            _ok = new GUIStyle(_body) { normal = { textColor = new Color(0.56f, 0.82f, 0.63f) } };
            _level = new GUIStyle(_body) { normal = { textColor = new Color(0.91f, 0.69f, 0.35f) }, fontStyle = FontStyle.Bold };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
        }

        void OnGUI()
        {
            if (_controller == null || _controller.World == null) return;
            EnsureStyles();
            var world = _controller.World;
            var save = world.SaveData;
            var player = world.Player;

            GUI.Box(new Rect(12, 12, 350, 168), GUIContent.none);
            GUI.Label(new Rect(24, 18, 330, 24), "ADAMS HAVEN  ·  FARM", _title);
            GUI.Label(new Rect(24, 44, 320, 20), $"{world.DisplayName}  ·  16×16  ·  Battles {save.totalBattles}", _body);
            GUI.Label(new Rect(24, 66, 320, 20), $"Farm Lv {player.Level}  ·  XP {FormatXp(player)}  ·  Harvests {save.totalHarvests}", _body);
            GUI.Label(new Rect(24, 88, 320, 20), $"Tool: {FarmController.FormatTool(_controller.SelectedTool)}  [1–5]", _body);
            GUI.Label(new Rect(24, 110, 320, 20), $"Seed: {_controller.SelectedCropName} ×{_controller.SelectedSeedCount}  [Q]", _body);
            GUI.Label(new Rect(24, 132, 320, 20), $"Fertilizer ×{_controller.FertilizerCount}  ·  Obstacles {world.RemainingObstacles()}", _small);
            GUI.Label(new Rect(24, 150, 330, 20), "E tool  ·  P plant  ·  F fertilize  ·  R harvest  ·  B battle  ·  T test", _small);

            if (GUI.Button(new Rect(12, 188, 170, 28), "ENTER BATTLE", _button)) _controller.EnterBattle();

            var messageStyle = _controller.StatusKind switch
            {
                "warn" => _warn,
                "ok" => _ok,
                "level" => _level,
                _ => _body
            };
            GUI.Box(new Rect(12, Screen.height - 72, Mathf.Min(580, Screen.width - 24), 48), GUIContent.none);
            GUI.Label(new Rect(24, Screen.height - 62, Mathf.Min(556, Screen.width - 48), 34), _controller.LastMessage, messageStyle);

            DrawTouchControls();
        }

        void DrawTouchControls()
        {
            const float size = 52f;
            var padX = Screen.width - size * 3 - 20;
            var padY = Screen.height - size * 3 - 20;
            if (GUI.Button(new Rect(padX + size, padY, size, size), "▲", _button)) _controller.UiMove(0, -1);
            if (GUI.Button(new Rect(padX, padY + size, size, size), "◀", _button)) _controller.UiMove(-1, 0);
            if (GUI.Button(new Rect(padX + size, padY + size, size, size), "TOOL", _button)) _controller.UseSelectedTool();
            if (GUI.Button(new Rect(padX + size * 2, padY + size, size, size), "▶", _button)) _controller.UiMove(1, 0);
            if (GUI.Button(new Rect(padX + size, padY + size * 2, size, size), "▼", _button)) _controller.UiMove(0, 1);

            var actionX = Mathf.Max(12f, padX - 118f);
            if (GUI.Button(new Rect(actionX, padY, 108, 30), "CHANGE TOOL", _button)) _controller.CycleTool();
            if (GUI.Button(new Rect(actionX, padY + 34, 108, 30), "CHANGE SEED", _button)) _controller.CycleSeed();
            if (GUI.Button(new Rect(actionX, padY + 68, 108, 30), "FERTILIZE", _button)) _controller.ApplyFertilizer();
            if (GUI.Button(new Rect(actionX, padY + 102, 108, 30), "PLANT", _button)) _controller.PlantSelectedSeed();
            if (GUI.Button(new Rect(actionX, padY + 136, 108, 30), "HARVEST", _button)) _controller.Harvest();
        }

        static string FormatXp(FarmWorld.PlayerView player)
        {
            var needed = player.XpToNext();
            return needed == null ? "MAX" : player.Xp + "/" + needed;
        }
    }
}
