using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// Plays one ClipEntry's chroma-keyed FMV on a runtime quad positioned over a unit's
    /// sprite, via ChromaKeyVideo.shader. Built once per unit view
    /// (BattleVisuals.BuildUnitView) and left inactive until Play() is called --
    /// VideoPlayer + RenderTexture setup is comparatively expensive, so this reuses one
    /// instance across a unit's whole battle rather than creating one per action.
    ///
    /// FOUNDATION.md's three-layer renderer always intended chroma-keyed FMV over the
    /// static side-view background; BattleVisuals only ever rendered sprites until M12
    /// wired this plumbing through. Real clips exist for the 3 basic-attack archetypes
    /// (Tools/ComfyUI, M1/M2) and double as every character's generic body animation --
    /// every skill sharing that archetype's clipKey reuses the same clip (see
    /// BattleVisuals.HasActionClip), with skill-specific identity carried separately by
    /// SkillDefinition.effect instead of a unique clip per skill.
    ///
    /// Known gap: the impactFrames metadata baked into today's Clip_*.asset files reads as
    /// corrupted (values in the millions on a few-hundred-frame clip -- e.g. 12000000
    /// instead of 12), almost certainly a manifest/generate.py authoring bug from M1/M2.
    /// onImpact is wired to fire only when a frame threshold is actually reached, so this
    /// fails safe against that (the callback just never fires) rather than crashing or
    /// firing at the wrong time -- but don't rely on impact-frame sync being meaningful
    /// until that metadata is fixed or regenerated.
    /// </summary>
    public class BattleClipPlayer : MonoBehaviour
    {
        VideoPlayer _player;
        RenderTexture _rt;
        Transform _quad;
        MeshRenderer _renderer;
        Material _material;
        float _parentScale;

        static readonly int KeyColorId = Shader.PropertyToID("_KeyColor");
        static readonly int ToleranceId = Shader.PropertyToID("_Tolerance");

        public bool IsReady => _player != null;

        /// <param name="parentScale">The uniform scale BattleVisuals already applied to
        /// this unit's root GameObject to normalize its sprite to
        /// BattleLayout.TargetUnitHeight world units tall. This component is parented
        /// under that same root (so it tracks the unit's position through every existing
        /// tween for free), which means its own quad's *local* scale must divide that
        /// factor back out, or the clip would render at TargetUnitHeight^2 relative to
        /// everything else on screen.</param>
        public void Init(Shader chromaShader, float parentScale)
        {
            if (chromaShader == null)
            {
                Debug.LogWarning("[AI.Game] ChromaKeyVideo shader not found -- FMV playback "
                    + "disabled for this unit, falling back to sprites. Check Project Settings "
                    + "> Graphics > Always Included Shaders if this happens in a standalone build.");
                return;
            }

            _parentScale = Mathf.Max(parentScale, 0.0001f);

            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.None;
            _player.isLooping = false;

            var quadGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quadGo.name = "ClipQuad";
            Destroy(quadGo.GetComponent<Collider>());
            quadGo.transform.SetParent(transform, false);
            _quad = quadGo.transform;
            _renderer = quadGo.GetComponent<MeshRenderer>();
            _material = new Material(chromaShader);
            _renderer.sharedMaterial = _material;

            gameObject.SetActive(false);
        }

        /// <summary>Plays entry.clip chroma-keyed at BattleLayout.TargetUnitHeight world
        /// units tall (matching the sprite it's standing in for), invoking onImpact once
        /// per frame threshold in entry.impactFrames as playback crosses it. No-ops
        /// (yields nothing) if entry/entry.clip is null or Init was never given a valid
        /// shader -- callers must check IsReady and the entry beforehand (see
        /// BattleVisuals.HasActionClip) and keep their own sprite-only fallback for that
        /// case; this method doesn't signal failure any other way.</summary>
        public IEnumerator Play(ClipEntry entry, bool facingRight, Action onImpact)
        {
            if (!IsReady || entry == null || entry.clip == null) yield break;

            _rt = new RenderTexture((int)entry.clip.width, (int)entry.clip.height, 0);
            _player.clip = entry.clip;
            _player.targetTexture = _rt;
            _material.mainTexture = _rt;
            _material.SetColor(KeyColorId, entry.chromaKey);
            _material.SetFloat(ToleranceId, entry.chromaTolerance);

            float aspect = entry.clip.height > 0 ? (float)entry.clip.width / entry.clip.height : 1f;
            float h = BattleLayout.TargetUnitHeight / _parentScale;
            float w = h * aspect * (facingRight ? 1f : -1f);
            _quad.localScale = new Vector3(w, h, 1f);

            gameObject.SetActive(true);
            _player.Prepare();
            while (!_player.isPrepared) yield return null;
            _player.Play();

            var fired = entry.impactFrames.Count > 0 ? new bool[entry.impactFrames.Count] : Array.Empty<bool>();
            while (_player.isPlaying)
            {
                for (int i = 0; i < fired.Length; i++)
                {
                    if (fired[i] || _player.frame < entry.impactFrames[i]) continue;
                    fired[i] = true;
                    onImpact?.Invoke();
                }
                yield return null;
            }

            gameObject.SetActive(false);
            Destroy(_rt);
            _rt = null;
        }
    }
}
