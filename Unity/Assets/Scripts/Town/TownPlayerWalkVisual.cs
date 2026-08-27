using System;
using UnityEngine;

namespace Game.Town
{
    /// <summary>M39 pilot: a billboard walk-cycle for the Town player, replacing the
    /// flat capsule color. Frames came from AssetForge's own pipeline -- ComfyUI
    /// MiniMax H3 image-to-video from a reference photo, extracted and chroma-keyed
    /// via AssetForge's editor (POST /api/editor/{sid}/export) -- landing as
    /// Resources/Town/Art/PlayerWalk/walk_00..31.png (32-frame cycle). Loaded as plain
    /// Texture2D via Resources.LoadAll, the same runtime-texture approach
    /// TownVisuals.RoofCap already uses, not Unity's Sprite importer (no .meta
    /// authoring needed for these to just work). Frame count is read from the loaded
    /// array, not hardcoded, so this scales to any frame count dropped in the folder --
    /// FramesPerSecond is the thing that needs to move with it, so total cycle time
    /// (frames / fps) stays roughly constant instead of the walk slowing down every
    /// time the frame count goes up.</summary>
    public class TownPlayerWalkVisual : MonoBehaviour
    {
        const string FramesResourceDir = "Town/Art/PlayerWalk";
        const float FramesPerSecond = 16f;
        const float MovingSpeedThreshold = 0.1f; // units/sec

        Texture2D[] _frames;
        Material _mat;
        CharacterController _cc;
        float _clock;
        int _frame = -1;
        Vector3 _lastPos;
        bool _hasLastPos;

        /// <summary>Builds the quad, attaches this component + TownBillboard, and
        /// parents it under the player. Mirrors FarmVisuals.BuildPlayer's use of
        /// Unity's own PrimitiveType.Quad (not a hand-built mesh) so the billboard
        /// technique is proven, not guessed at.</summary>
        public static GameObject Create(Transform parent, CharacterController cc)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "PlayerWalkVisual";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            // 1.8 tall (matches CharacterController.height), 0.8 aspect (matches the
            // 160x200 exported frames) so the character doesn't look stretched.
            go.transform.localScale = new Vector3(1.44f, 1.8f, 1f);

            var visual = go.AddComponent<TownPlayerWalkVisual>();
            visual.Init(cc);
            go.AddComponent<TownBillboard>();
            return go;
        }

        void Init(CharacterController cc)
        {
            _cc = cc;
            _frames = Resources.LoadAll<Texture2D>(FramesResourceDir);
            Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var renderer = GetComponent<MeshRenderer>();
            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
            _mat = new Material(shader);
            renderer.sharedMaterial = _mat;
            SetFrame(0);
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _cc == null) return;

            // CharacterController.velocity doesn't reflect SimpleMove's actual
            // horizontal displacement in this setup -- confirmed by direct testing
            // (walked the player across the crossroads, camera followed, sprite never
            // left frame 0). Tracking the player's own position delta instead is
            // immune to whichever Move/SimpleMove quirk caused that.
            var pos = _cc.transform.position;
            var delta = pos - _lastPos;
            var moving = _hasLastPos && Time.deltaTime > 0f
                && delta.magnitude / Time.deltaTime > MovingSpeedThreshold;
            _lastPos = pos;
            _hasLastPos = true;

            // Facing (M41). The walk frames are side-on art drawn facing LEFT, so
            // left-ward movement uses them as-authored and right-ward mirrors them.
            // Verified by inspecting the frames directly, not assumed -- mirroring
            // front-facing art would have been a regression, which is why M40 left
            // this out until the art could actually be checked.
            if (moving && Mathf.Abs(delta.x) > 0.0001f)
            {
                var faceLeft = delta.x < 0f;
                var s = transform.localScale;
                var width = Mathf.Abs(s.x);
                transform.localScale = new Vector3(faceLeft ? width : -width, s.y, s.z);
            }

            if (!moving)
            {
                _clock = 0f;
                SetFrame(0);
                return;
            }
            _clock += Time.deltaTime * FramesPerSecond;
            SetFrame(Mathf.FloorToInt(_clock) % _frames.Length);
        }

        void SetFrame(int index)
        {
            if (index == _frame || _frames == null || _frames.Length == 0) return;
            _frame = index;
            _mat.mainTexture = _frames[index];
        }
    }

    /// <summary>Always faces the camera. Same technique as Farm's FarmBillboard
    /// (FarmVisuals.cs), duplicated rather than cross-referenced -- Town and Farm are
    /// deliberately separate assemblies (see Game.Farm.asmdef).</summary>
    public class TownBillboard : MonoBehaviour
    {
        void LateUpdate()
        {
            if (Camera.main == null) return;
            var dir = transform.position - Camera.main.transform.position;
            // Yaw only (M40). The side-scroller camera sits above the player, so a full
            // LookRotation would pitch the quad to face it and the character would
            // visibly lean back. Flattening Y keeps the sprite upright.
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(dir);
        }
    }
}
