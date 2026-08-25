using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Navigation
{
    /// <summary>
    /// Project-wide travel overlay. It is intentionally independent from Farm, Town,
    /// and Battle so each scene can keep its own gameplay code and still expose the
    /// same Camp/Pause travel loop.
    /// </summary>
    public sealed class HavenNavigation : MonoBehaviour
    {
        static HavenNavigation _instance;
        bool _open;
        GUIStyle _title, _body, _button, _hint;

        static readonly string[] Destinations = { "Camp", "Home", "Farm", "Town", "Battle" };

        public static bool IsOpen => _instance != null && _instance._open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("HavenNavigation");
            _instance = go.AddComponent<HavenNavigation>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
            Time.timeScale = 1f;
        }

        void Update()
        {
            // Tab is used instead of Escape so Battle's existing pause/settings
            // screen remains unchanged. The travel overlay is still a true pause.
            var battleScene = SceneManager.GetActiveScene().name == "Battle";
            if (Input.GetKeyDown(KeyCode.Tab)
                || (!battleScene && Input.GetKeyDown(KeyCode.Escape)))
                SetOpen(!_open);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _open = false;
            Time.timeScale = 1f;
        }

        public static void Open()
        {
            if (_instance != null) _instance.SetOpen(true);
        }

        void SetOpen(bool open)
        {
            _open = open;
            Time.timeScale = _open ? 0f : 1f;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.88f, 0.88f, 0.92f) }
            };
            _button = new GUIStyle(GUI.skin.button)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            _hint = new GUIStyle(_body) { fontSize = 11 };
        }

        void OnGUI()
        {
            EnsureStyles();
            if (!_open)
            {
                // Battle's existing HUD owns its pause panel; this small hint keeps
                // the shared travel screen discoverable there without changing that UI.
                if (SceneManager.GetActiveScene().name == "Battle")
                    GUI.Label(new Rect(Screen.width - 150f, Screen.height - 30f, 138f, 20f),
                        "TAB  Travel", _hint);
                return;
            }

            GUI.color = new Color(0.02f, 0.03f, 0.06f, 0.78f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            const float panelW = 430f;
            const float panelH = 390f;
            var panel = new Rect(Screen.width / 2f - panelW / 2f,
                Screen.height / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, 34f),
                "ADAMS HAVEN", _title);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 52f, panel.width - 40f, 24f),
                "Camp / Pause Travel", _body);

            float x = panel.x + 34f;
            float y = panel.y + 94f;
            const float gap = 42f;
            for (var i = 0; i < Destinations.Length; i++)
            {
                var destination = Destinations[i];
                if (GUI.Button(new Rect(x, y + i * gap, panel.width - 68f, 34f),
                    destination, _button)) Navigate(destination);
            }

            GUI.Label(new Rect(panel.x + 20f, panel.y + panel.height - 32f,
                panel.width - 40f, 18f), "Tab: close travel  ·  destination state is saved by its scene", _hint);
        }

        void Navigate(string destination)
        {
            SetOpen(false);
            SceneManager.LoadScene(destination);
        }
    }
}
