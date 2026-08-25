using UnityEngine;

namespace Game.Town
{
    /// <summary>Keeps the pitched ortho camera centred on the player as they walk the
    /// hub -- Town is far larger than Farm's fixed-frame plot, so a static camera
    /// (Farm/Battle's own convention) would leave most of it offscreen. `Yaw` is
    /// player-adjustable (M34, `TownController`'s Q/R) rather than fixed -- the ground
    /// reference image's own orientation doesn't necessarily match the camera's
    /// original fixed facing, and letting the player rotate the view around sidesteps
    /// having to guess the one "correct" fixed angle from outside the Editor.</summary>
    public class TownCameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Pitch = 55f;
        public float Distance = 24f;
        public float Yaw;

        void LateUpdate()
        {
            if (Target == null) return;
            var rot = Quaternion.Euler(Pitch, Yaw, 0f);
            transform.rotation = rot;
            transform.position = Target.position - rot * Vector3.forward * Distance;
        }
    }
}
