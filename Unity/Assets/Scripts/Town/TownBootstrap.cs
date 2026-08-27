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
            ApplyCamera(cam);

            var playerGo = new GameObject("Player");
            playerGo.transform.SetParent(transform, false);
            playerGo.transform.position = built.PlayerSpawn;
            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.4f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            // M39: the flat-color capsule is gone -- TownPlayerWalkVisual is a
            // billboard quad cycling through AssetForge-generated walk frames while
            // the CharacterController is actually moving. cc itself still owns
            // collision; this is purely visual, same as the capsule it replaces.
            TownPlayerWalkVisual.Create(playerGo.transform, cc);

            // M40: fixed side-on rig. M35's default yaw is gone along with the orbit
            // controls it seeded -- a flat side view has exactly one camera angle.
            var camFollow = camGo.AddComponent<TownCameraFollow>();
            camFollow.Target = playerGo.transform;

            // M38: session-only economy + the build-choice/construction panel. Economy
            // is a plain class (no save yet -- see TownEconomy's own doc comment),
            // reachable only through TownBuildMenu's own reference to it -- fine as
            // long as nothing re-runs Boot() mid-session, which nothing does yet (unlike
            // BattleBootstrap, Town has no rebuild trigger of its own today).
            var economy = new TownEconomy();
            var buildMenuGo = new GameObject("TownBuildMenu");
            buildMenuGo.transform.SetParent(transform, false);
            var buildMenu = buildMenuGo.AddComponent<TownBuildMenu>();
            buildMenu.Init(economy);

            var ctrl = playerGo.AddComponent<TownController>();
            ctrl.Init(built.Buildings, built.Gates, buildMenu);

            var hudGo = new GameObject("TownHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<TownHud>().Init(ctrl);

            Debug.Log("[Adams Haven] Town hub booted -- see PROJECT-README's Town milestones.");
        }

        // Position/rotation are no longer set here -- TownCameraFollow (added after the
        // player exists, see Boot() above) owns those every frame. This only sets the
        // properties that don't change at runtime.
        static void ApplyCamera(Camera cam)
        {
            cam.orthographic = true;
            // M40: 8 (a 16-unit tall view) frames a street of <=7-unit buildings with
            // sky above. M30's 16 was sized for looking down at a whole map at once.
            cam.orthographicSize = 8f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.55f, 0.6f);
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 140f;
            cam.allowMSAA = false;
        }
    }
}
