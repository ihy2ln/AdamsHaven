using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Town
{
    /// <summary>Side-scrolling movement for the Town hub (M40) -- deliberately not
    /// Farm's grid/tile system (FarmWorld/FarmIso). Town has no per-tile simulation
    /// state, just a street with building lots along it, so plain left/right movement
    /// plus a CharacterController for gravity/collision is the whole input model. Also
    /// owns the proximity checks that drive TownHud's interaction prompt, the build
    /// menu, and Gates' scene transitions.
    ///
    /// M30-M39's free XZ walking and Q/R camera rotation are both gone: a flat side
    /// view has one movement axis and one fixed camera angle, so there's nothing left
    /// for the second axis or the orbit controls to do.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class TownController : MonoBehaviour
    {
        const float MoveSpeed = 6f;
        const float InteractRange = 3.5f;

        CharacterController _cc;
        List<TownBuilding> _buildings = new();
        List<TownGate> _gates = new();
        TownBuildMenu _buildMenu;

        public TownBuilding NearestBuilding { get; private set; }
        public TownGate NearestGate { get; private set; }

        public void Init(List<TownBuilding> buildings, List<TownGate> gates, TownBuildMenu buildMenu)
        {
            _buildings = buildings;
            _gates = gates;
            _buildMenu = buildMenu;
        }

        void Awake() => _cc = GetComponent<CharacterController>();

        void Update()
        {
            if (Time.timeScale <= 0f) return;
            if (_buildMenu != null && _buildMenu.IsOpen) return;

            // Side-scroller (M40): one axis. Vertical input is ignored -- there's no
            // depth to walk into anymore, and no jump yet. SimpleMove still applies
            // gravity, which is what keeps the player on the street's ground collider.
            var h = Input.GetAxisRaw("Horizontal");
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h = -1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h = 1f;

            _cc.SimpleMove(new Vector3(Mathf.Clamp(h, -1f, 1f), 0f, 0f) * MoveSpeed);

            UpdateNearest();
            // TownBuildMenu only ticks CompleteIfDue on the one plot its own panel is
            // open for -- without this, a plot the player walked away from mid-build
            // would sit at "0s left" forever instead of flipping to Built, since nothing
            // else re-checks its timer once the menu that started it is closed.
            foreach (var b in _buildings) b.CompleteIfDue();

            if (Input.GetKeyDown(KeyCode.E))
            {
                // Gate wins over a plot when both are in range -- gates sit at the tree
                // line, away from any plot, so this only matters at the edge of range.
                if (NearestGate != null) SceneManager.LoadScene(NearestGate.TargetSceneName);
                else if (NearestBuilding != null && _buildMenu != null) _buildMenu.OpenFor(NearestBuilding);
            }
        }

        void UpdateNearest()
        {
            NearestBuilding = Nearest(_buildings, b => b.transform.position);
            NearestGate = Nearest(_gates, g => g.transform.position);
        }

        /// <summary>Distance along the street only (M40). A plot's transform sits at
        /// the centre of a building that stands several units tall and a couple of
        /// units back in Z, so a full 3D distance would push tall buildings out of
        /// interact range while the player is standing right at their base.</summary>
        T Nearest<T>(List<T> items, System.Func<T, Vector3> pos) where T : Component
        {
            T best = null;
            var bestDist = InteractRange;
            foreach (var item in items)
            {
                var d = Mathf.Abs(pos(item).x - transform.position.x);
                if (d > bestDist) continue;
                bestDist = d;
                best = item;
            }
            return best;
        }
    }
}
