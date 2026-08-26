using UnityEngine;

namespace Game.Farm
{
    /// <summary>
    /// Boots the starter farm at runtime — no Inspector wiring required.
    /// Attach to an empty GameObject in Farm.unity (or let the editor builder create it).
    /// </summary>
    public class FarmBootstrap : MonoBehaviour
    {
        void Start() => Boot();

        [ContextMenu("Boot Farm")]
        public void Boot()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            var clock = new SystemFarmClock();
            var saves = new FarmSaveRepository();
            var world = new FarmWorld(saves.LoadOrCreate(), clock);

            var camGo = GameObject.Find("Main Camera") ?? new GameObject("Main Camera");
            // Not `?? camGo.AddComponent<Camera>()` -- see BattleBootstrap.cs's comment
            // on the identical line; `??` can skip AddComponent entirely here in Unity 6.
            var cam = camGo.GetComponent<Camera>();
            if (cam == null) cam = camGo.AddComponent<Camera>();
            if (camGo.GetComponent<AudioListener>() == null) camGo.AddComponent<AudioListener>();
            cam.tag = "MainCamera";

            var visualsGo = new GameObject("FarmVisuals");
            visualsGo.transform.SetParent(transform, false);
            var visuals = visualsGo.AddComponent<FarmVisuals>();
            visuals.Build(world);
            FarmIso.ApplyFlatScrollingCamera(cam, visuals.MapCenter);
            var cameraFollow = camGo.GetComponent<FarmCameraFollow>();
            if (cameraFollow == null) cameraFollow = camGo.AddComponent<FarmCameraFollow>();
            cameraFollow.Init(visuals.PlayerTransform, world.Width, world.Height);

            var minigameGo = new GameObject("FarmGatherMinigame");
            minigameGo.transform.SetParent(transform, false);
            var gatherMinigame = minigameGo.AddComponent<FarmGatherMinigame>();

            var ctrlGo = new GameObject("FarmController");
            ctrlGo.transform.SetParent(transform, false);
            var ctrl = ctrlGo.AddComponent<FarmController>();
            ctrl.Init(world, visuals, cam, saves, gatherMinigame);

            var hudGo = new GameObject("FarmHud");
            hudGo.transform.SetParent(transform, false);
            hudGo.AddComponent<FarmHud>().Init(ctrl);

            Debug.Log("[Adams Haven] 10×10 flat 2.5D farm booted: scrolling camera, skill gathering, crops, saves, and Battle travel.");
        }
    }
}
