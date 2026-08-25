using System;
using UnityEngine;

namespace Game.Farm
{
    /// <summary>
    /// Farm-owned presentation wrapper for the Town walking character. It loads
    /// the same authored frame set but does not reference Game.Town, keeping the
    /// Farm assembly independent while both scenes share one character look.
    /// </summary>
    public sealed class FarmPlayerWalkVisual : MonoBehaviour
    {
        const string FramesResourceDir = "Town/Art/PlayerWalk";
        const float FramesPerSecond = 8f;
        const float StepDuration = 0.72f;

        Texture2D[] _frames;
        Material _material;
        float _clock;
        float _movingFor;
        int _frame = -1;

        public static FarmPlayerWalkVisual Create(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "PlayerWalkVisual";
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            go.transform.localScale = new Vector3(1.44f, 1.8f, 1f);

            var visual = go.AddComponent<FarmPlayerWalkVisual>();
            visual.Initialize();
            go.AddComponent<FarmBillboard>();
            return visual;
        }

        void Initialize()
        {
            _frames = Resources.LoadAll<Texture2D>(FramesResourceDir);
            Array.Sort(_frames, (a, b) => string.CompareOrdinal(a.name, b.name));

            var shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Unlit/Texture");
            _material = new Material(shader);
            GetComponent<MeshRenderer>().sharedMaterial = _material;
            SetFrame(0);
        }

        public void PlayStep()
        {
            if (_frames == null || _frames.Length == 0) return;
            _clock = 0f;
            _movingFor = StepDuration;
            SetFrame(0);
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0 || _material == null) return;
            if (_movingFor <= 0f)
            {
                SetFrame(0);
                return;
            }

            _movingFor -= Time.deltaTime;
            _clock += Time.deltaTime * FramesPerSecond;
            SetFrame(Mathf.FloorToInt(_clock) % _frames.Length);
        }

        void SetFrame(int index)
        {
            if (index == _frame || _frames == null || _frames.Length == 0) return;
            _frame = index;
            _material.mainTexture = _frames[index];
        }
    }
}
