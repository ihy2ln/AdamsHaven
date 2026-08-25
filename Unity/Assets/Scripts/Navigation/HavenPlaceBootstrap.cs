using UnityEngine;

namespace Game.Navigation
{
    /// <summary>Small, playable placeholder for the Home and Camp destinations.</summary>
    public sealed class HavenPlaceBootstrap : MonoBehaviour
    {
        public string PlaceTitle = "HOME";
        public string PlaceSubtitle = "A quiet place to plan the next day.";
        public bool IsCamp;

        void Start() => Build();

        void Build()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "PlaceGround";
            ground.transform.SetParent(transform, false);
            ground.transform.localScale = new Vector3(3.5f, 1f, 3.5f);
            ground.GetComponent<Renderer>().material = Material(new Color(0.25f, 0.37f, 0.23f));

            if (IsCamp) BuildCampfire(); else BuildHome();
            BuildPlayer();
            BuildCamera();

            var hud = new GameObject("PlaceHud");
            hud.transform.SetParent(transform, false);
            hud.AddComponent<HavenPlaceHud>().Init(this);
        }

        void BuildCampfire()
        {
            var fire = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fire.name = "Campfire";
            fire.transform.SetParent(transform, false);
            fire.transform.localPosition = new Vector3(0f, 0.45f, 1.5f);
            fire.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
            fire.GetComponent<Renderer>().material = Material(new Color(0.95f, 0.45f, 0.12f));
            for (var i = 0; i < 4; i++)
            {
                var log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = "CampLog";
                log.transform.SetParent(transform, false);
                log.transform.localPosition = new Vector3(Mathf.Cos(i * 1.57f) * 0.55f, 0.16f,
                    1.5f + Mathf.Sin(i * 1.57f) * 0.55f);
                log.transform.localRotation = Quaternion.Euler(0f, i * 45f, 90f);
                log.transform.localScale = new Vector3(0.16f, 0.65f, 0.16f);
                log.GetComponent<Renderer>().material = Material(new Color(0.32f, 0.18f, 0.10f));
            }
        }

        void BuildHome()
        {
            var house = GameObject.CreatePrimitive(PrimitiveType.Cube);
            house.name = "HomeHouse";
            house.transform.SetParent(transform, false);
            house.transform.localPosition = new Vector3(0f, 1.2f, 1.5f);
            house.transform.localScale = new Vector3(4.5f, 2.4f, 3.4f);
            house.GetComponent<Renderer>().material = Material(new Color(0.68f, 0.47f, 0.30f));

            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "HomeRoof";
            roof.transform.SetParent(transform, false);
            roof.transform.localPosition = new Vector3(0f, 2.75f, 1.5f);
            roof.transform.localScale = new Vector3(5f, 0.45f, 3.9f);
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            roof.GetComponent<Renderer>().material = Material(new Color(0.45f, 0.17f, 0.14f));
        }

        void BuildPlayer()
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.SetParent(transform, false);
            player.transform.localPosition = new Vector3(0f, 1f, -2.5f);
            player.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            player.GetComponent<Renderer>().material = Material(new Color(0.28f, 0.45f, 0.76f));
        }

        void BuildCamera()
        {
            var camGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            var cam = camGo.GetComponent<Camera>() ?? camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = 7f;
            cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
            cam.transform.position = new Vector3(0f, 12f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = IsCamp ? new Color(0.10f, 0.08f, 0.14f) : new Color(0.28f, 0.37f, 0.48f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 80f;
        }

        static Material Material(Color color)
        {
            var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            return new Material(shader) { color = color };
        }
    }

    public sealed class HavenPlaceHud : MonoBehaviour
    {
        HavenPlaceBootstrap _place;
        GUIStyle _title, _body, _button;

        public void Init(HavenPlaceBootstrap place) => _place = place;

        void OnGUI()
        {
            if (_place == null) return;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.92f, 0.72f, 0.36f) } };
                _body = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true,
                    normal = { textColor = new Color(0.94f, 0.90f, 0.83f) } };
                _button = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold };
            }
            GUI.Box(new Rect(12f, 12f, 370f, 112f), GUIContent.none);
            GUI.Label(new Rect(24f, 18f, 340f, 26f), "ADAMS HAVEN  ·  " + _place.PlaceTitle, _title);
            GUI.Label(new Rect(24f, 48f, 340f, 42f), _place.PlaceSubtitle, _body);
            GUI.Label(new Rect(24f, 92f, 340f, 22f), "TAB  Camp / Pause Travel", _body);
        }
    }
}
