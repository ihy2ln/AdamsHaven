using UnityEngine;

namespace Game.Town
{
    /// <summary>Keeps the pitched ortho camera centred on the player as they walk the
    /// hub -- Town is far larger than Farm's fixed-frame plot, so a static camera
    /// (Farm/Battle's own convention) would leave most of it offscreen.</summary>
    public class TownCameraFollow : MonoBehaviour
    {
        public Transform Target;
        Vector3 _offset;
        bool _offsetSet;

        void LateUpdate()
        {
            if (Target == null) return;
            if (!_offsetSet)
            {
                _offset = transform.position - Target.position;
                _offsetSet = true;
            }
            transform.position = Target.position + _offset;
        }
    }
}
