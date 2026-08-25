using UnityEngine;

namespace Game.Town
{
    /// <summary>Minimal IMGUI HUD for the Town hub (M30) -- a title card and a one-line
    /// interaction prompt, matching Farm/Battle's own code-first IMGUI convention rather
    /// than a Canvas.</summary>
    public class TownHud : MonoBehaviour
    {
        TownController _controller;
        GUIStyle _title, _body, _prompt;

        public void Init(TownController controller) => _controller = controller;

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
                fontSize = 13,
                normal = { textColor = new Color(0.94f, 0.90f, 0.83f) },
                wordWrap = true
            };
            _prompt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        void OnGUI()
        {
            if (_controller == null) return;
            EnsureStyles();

            GUI.Box(new Rect(12, 12, 320, 76), GUIContent.none);
            GUI.Label(new Rect(24, 18, 300, 24), "ADAMS HAVEN  ·  TOWN", _title);
            GUI.Label(new Rect(24, 44, 296, 20), "WASD/arrows to walk  ·  E to use a gate", _body);
            GUI.Label(new Rect(24, 62, 296, 20), "Placeholder blockout -- see PROJECT-README's Town milestones", _body);

            var line = _controller.NearestGate != null ? _controller.NearestGate.PromptLine
                : _controller.NearestBuilding != null ? _controller.NearestBuilding.PromptLine
                : null;

            if (!string.IsNullOrEmpty(line))
            {
                var w = Mathf.Min(420, Screen.width - 40);
                var rect = new Rect(Screen.width / 2f - w / 2f, Screen.height - 70, w, 34);
                GUI.Box(rect, GUIContent.none);
                GUI.Label(rect, line, _prompt);
            }
        }
    }
}
