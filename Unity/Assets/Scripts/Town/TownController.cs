using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Navigation;

namespace Game.Town
{
    /// <summary>Free-walk movement for the Town hub (M30) -- deliberately not Farm's
    /// grid/tile system (FarmWorld/FarmIso). Town has no per-tile simulation state, just
    /// a walkable space with building lots in it, so plain continuous movement plus a
    /// CharacterController for collision is the whole input model. Also owns the
    /// proximity checks that drive TownHud's interaction prompt and Gates' scene
    /// transitions.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class TownController : MonoBehaviour
    {
        const float MoveSpeed = 6f;
        const float InteractRange = 3.5f;
        const float CameraRotateSpeed = 90f; // degrees/sec while Q or R is held

        CharacterController _cc;
        List<TownBuilding> _buildings = new();
        List<TownGate> _gates = new();
        TownCameraFollow _cameraFollow;

        public TownBuilding NearestBuilding { get; private set; }
        public TownGate NearestGate { get; private set; }

        public void Init(List<TownBuilding> buildings, List<TownGate> gates, TownCameraFollow cameraFollow)
        {
            _buildings = buildings;
            _gates = gates;
            _cameraFollow = cameraFollow;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            if (HavenNavigation.IsOpen) return;

            if (_cameraFollow != null)
            {
                if (Input.GetKey(KeyCode.Q)) _cameraFollow.Yaw -= CameraRotateSpeed * Time.deltaTime;
                if (Input.GetKey(KeyCode.R)) _cameraFollow.Yaw += CameraRotateSpeed * Time.deltaTime;
            }

            var h = Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;
            var v = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v = -1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v = 1f;

            var input = new Vector3(h, 0f, v);
            if (input.sqrMagnitude > 1f) input.Normalize();

            // Camera-relative (M34): W always means "away from camera" on screen, not a
            // fixed world axis -- otherwise rotating the view with Q/R would leave
            // movement pointing the wrong way on screen after the first turn.
            var yaw = _cameraFollow != null ? _cameraFollow.Yaw : 0f;
            var move = Quaternion.Euler(0f, yaw, 0f) * input;
            _cc.SimpleMove(move * MoveSpeed);

            UpdateNearest();

            if (Input.GetKeyDown(KeyCode.E) && NearestGate != null)
                SceneManager.LoadScene(NearestGate.TargetSceneName);
        }

        void UpdateNearest()
        {
            NearestBuilding = Nearest(_buildings, b => b.transform.position);
            NearestGate = Nearest(_gates, g => g.transform.position);
        }

        T Nearest<T>(List<T> items, System.Func<T, Vector3> pos) where T : Component
        {
            T best = null;
            var bestDist = InteractRange;
            foreach (var item in items)
            {
                var d = Vector3.Distance(transform.position, pos(item));
                if (d > bestDist) continue;
                bestDist = d;
                best = item;
            }
            return best;
        }
    }
}
