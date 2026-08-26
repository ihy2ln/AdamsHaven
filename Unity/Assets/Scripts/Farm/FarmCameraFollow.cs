using UnityEngine;

namespace Game.Farm
{
    /// <summary>
    /// Player-following camera for the flat 2.5D Farm view. It scrolls across
    /// the authored plot and clamps its focus so the camera does not drift far
    /// beyond the playable field.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FarmCameraFollow : MonoBehaviour
    {
        const float FollowSharpness = 12f;

        Transform _target;
        Camera _camera;
        int _width;
        int _height;

        public void Init(Transform target, int width, int height)
        {
            _target = target;
            _width = width;
            _height = height;
            _camera = GetComponent<Camera>();
            SnapToTarget();
        }

        void LateUpdate()
        {
            if (_target == null || _camera == null) return;
            var desired = DesiredPosition();
            var blend = 1f - Mathf.Exp(-FollowSharpness * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);
        }

        void SnapToTarget()
        {
            if (_target == null) return;
            if (_camera == null) _camera = GetComponent<Camera>();
            transform.position = DesiredPosition();
        }

        Vector3 DesiredPosition()
        {
            var focus = _target.position;
            var fieldMaxX = Mathf.Max(0f, (_width - 1) * FarmIso.TileSize);
            var fieldMaxZ = Mathf.Max(0f, (_height - 1) * FarmIso.TileSize);
            var halfVisibleX = FarmIso.ScrollingOrthoSize * Mathf.Max(1f, _camera.aspect) * 0.62f;
            var halfVisibleZ = FarmIso.ScrollingOrthoSize * 0.62f;

            focus.x = ClampFocus(focus.x, 0f, fieldMaxX, halfVisibleX);
            focus.z = ClampFocus(focus.z, 0f, fieldMaxZ, halfVisibleZ);
            focus.y = 0.2f;
            return focus - transform.forward * 18f;
        }

        static float ClampFocus(float value, float min, float max, float margin)
        {
            if (max - min <= margin * 2f) return (min + max) * 0.5f;
            return Mathf.Clamp(value, min + margin, max - margin);
        }
    }
}
