using UnityEngine;

namespace Game.Town
{
    /// <summary>Side-scroller camera (M40): fixed straight-on orientation looking down
    /// +Z at the XY plane, tracking the player left/right along X only. Replaces
    /// M34/M35's pitched, player-rotatable orbit rig -- pitch and yaw are both
    /// meaningless in a flat side view, so `Q`/`R` rotation went away with them
    /// (see TownController).
    ///
    /// Y is deliberately NOT followed: with no jumping, a camera that tracked vertical
    /// movement would only ever jitter against the fixed ground line. `Height` frames
    /// the street instead, so the horizon stays put while the player walks.</summary>
    public class TownCameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 20f;
        public float Height = 3.5f;

        void LateUpdate()
        {
            if (Target == null) return;
            // Identity rotation looks straight down +Z; sitting back on -Z puts the
            // whole street in front of the camera.
            transform.rotation = Quaternion.identity;
            transform.position = new Vector3(Target.position.x, Height, -Distance);
        }
    }
}
