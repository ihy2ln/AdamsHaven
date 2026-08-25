using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        CharacterController _cc;
        List<TownBuilding> _buildings = new();
        List<TownGate> _gates = new();

        public TownBuilding NearestBuilding { get; private set; }
        public TownGate NearestGate { get; private set; }

        public void Init(List<TownBuilding> buildings, List<TownGate> gates)
        {
            _buildings = buildings;
            _gates = gates;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            var h = Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;
            var v = Input.GetAxisRaw("Vertical");
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v = -1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v = 1f;

            var move = new Vector3(h, 0f, v);
            if (move.sqrMagnitude > 1f) move.Normalize();
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
