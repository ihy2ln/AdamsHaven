using UnityEngine;

namespace Game.Town
{
    /// <summary>
    /// Boots the Town hub at runtime -- no Inspector wiring required, mirrors
    /// FarmBootstrap/BattleBootstrap's pattern exactly. Attach to an empty GameObject in
    /// Town.unity (or let AI.Game > Town > Create Starter Scene create it).
    ///
    /// M30/M31: a walkable blockout of the project owner's own reference image (a
    /// forest-clearing crossroads town -- Market Row, Residential, Utility/Storage, Town
    /// Hall + plaza) with two gates out to Farm and Battle. No real building function
    /// yet -- see PROJECT-README's Town milestone list for what's next.
    /// </summary>
    public class TownBootstrap : MonoBehaviour
    {
        void Start() => Boot();

        [ContextMenu("Boot Town")]
        public void Boot()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            var sceneGo = new GameObject("TownScene");
            sceneGo.transform.SetParent(transform, false);
            var built = TownVisuals.Build(sceneGo.transform);

            var camGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            // Same Unity-6 fake-null trap as Battle/FarmBootstrap -- `??` on a
            // UnityEngine.Object can skip AddComponent, so this checks `== null` instead.
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            cam.tag = "MainCamera";
            ApplyCamera(cam, built.PlayerSpawn);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = built.PlayerSpawn;
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            var bodyGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            bodyGo.name = "PlayerBody";
            bodyGo.transform.SetParent(playerGo.transform, false);
            bodyGo.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            Object.DestroyImmediate(bodyGo.GetComponent<Collider>());
            var bodyRenderer = bodyGo.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                var shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
                bodyRenderer.material = new Material(shader) { color = new Color(0.36f, 0.55f, 0.85f) };
            }

            var ctrl = playerGo.AddComponent<TownController>();
            ctrl.Init(built.Buildings, built.Gates);

            var camFollow = camGo.AddComponent<TownCameraFollow>();
            camFollow.Target = playerGo.transform;

            var hudGo = new GameObject("TownHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<TownHud>().Init(ctrl);

            Debug.Log("[Adams Haven] Town hub booted -- blockout only, see PROJECT-README's Town milestones.");
        }

        static void ApplyCamera(Camera cam, Vector3 lookAt)
        {
            cam.orthographic = true;
            cam.orthographicSize = 16f;
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            cam.transform.position = lookAt - cam.transform.forward * 24f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.55f, 0.6f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 140f;
            cam.allowMSAA = false;
        }
    }
}
