using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>Sprites for the side-view battle: background + one SpriteRenderer per
    /// unit. Mirrors FarmVisuals' role (build once from a world, then sync).</summary>
    public class BattleVisuals : MonoBehaviour
    {
        readonly Dictionary<BattleUnit, GameObject> _unitViews = new();
        readonly Dictionary<BattleUnit, SpriteRenderer> _unitRenderers = new();
        readonly Dictionary<BattleUnit, BattleClipPlayer> _clipPlayers = new();
        Game.Data.MapDefinition _map;

        const string ChromaKeyShaderName = "Game/ChromaKeyVideo";

        public void Build(BattleWorld world)
        {
            _map = world.Map;
            BuildBackground(world);
            foreach (var unit in world.AllUnits) BuildUnitView(unit);
            // Bench units get a view too, parked inactive off-dock, so SubUnit only ever
            // has to toggle active state + reposition rather than instantiate mid-battle.
            foreach (var unit in world.Bench) BuildUnitView(unit, startActive: false);
        }

        void BuildBackground(BattleWorld world)
        {
            var bgSprite = world.Map != null ? world.Map.backgroundSprite : null;
            var go = new GameObject("Background");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0f, 0f, 5f); // behind units
            var sr = go.AddComponent<SpriteRenderer>();
            if (bgSprite != null)
            {
                sr.sprite = bgSprite;
                // Scale to fill the camera's view. Uniform "cover" scale (not independent
                // x/y stretch) -- source photos vary between landscape and portrait, and a
                // non-uniform stretch visibly squashed a portrait source when one was swapped
                // in for the second battle map.
                var cam = Camera.main;
                if (cam != null && cam.orthographic)
                {
                    float worldHeight = cam.orthographicSize * 2f;
                    float worldWidth = worldHeight * cam.aspect;
                    var bounds = sr.sprite.bounds.size;
                    float scale = Mathf.Max(worldWidth / bounds.x, worldHeight / bounds.y);
                    go.transform.localScale = new Vector3(scale, scale, 1f);
                }
            }
            else
            {
                sr.sprite = PlaceholderArt.FlatSprite(new Color(0.12f, 0.12f, 0.16f));
                go.transform.localScale = new Vector3(20f, 12f, 1f);
            }
            sr.sortingOrder = -10;
        }

        void BuildUnitView(BattleUnit unit, bool startActive = true)
        {
            var go = new GameObject($"Unit_{unit.Definition.characterId}");
            go.transform.SetParent(transform, false);
            go.transform.position = startActive ? BattleLayout.UnitPosition(unit.Column) : Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            var def = unit.Definition;
            sr.sprite = def.battleSprite != null ? def.battleSprite
                : def.pixelSprite32 != null ? def.pixelSprite32
                : PlaceholderArt.UnitFallback();
            sr.flipX = !unit.FacingRight;
            sr.sortingOrder = 10;

            // Normalize by world-space height regardless of source resolution/aspect --
            // a fixed scale multiplier overlapped neighbouring columns the moment art
            // with a different native size was swapped in (confirmed via a real build).
            float height = Mathf.Max(sr.sprite.bounds.size.y, 0.01f);
            float scale = BattleLayout.TargetUnitHeight / height;
            go.transform.localScale = Vector3.one * scale;

            go.SetActive(startActive);
            _unitViews[unit] = go;
            _unitRenderers[unit] = sr;

            // Parented under `go` so it tracks every existing tween for free -- see
            // BattleClipPlayer.Init's doc for why it needs `scale` to undo `go`'s own
            // normalization. Left inactive; PlayActionClip (attacker) and
            // PlayReactionClip (target, see HasReactionClip) are the only callers.
            // Nudged slightly toward the camera (-z) so a reaction clip composites over
            // this unit's own still-visible sprite instead of z-fighting it -- harmless
            // for the attacker-clip case too, since that path already hides the sprite.
            var clipGo = new GameObject("Clip");
            clipGo.transform.SetParent(go.transform, false);
            clipGo.transform.localPosition = new Vector3(0f, 0f, -0.05f);
            var clipPlayer = clipGo.AddComponent<BattleClipPlayer>();
            clipPlayer.Init(Shader.Find(ChromaKeyShaderName), scale);
            _clipPlayers[unit] = clipPlayer;
        }

        /// <summary>Screen-space hit test for manual targeting -- no colliders needed,
        /// just checks each unit's SpriteRenderer world bounds against the click point
        /// projected onto the units' z-plane.</summary>
        public bool TryGetUnitAtScreenPoint(Vector3 screenPos, Camera cam, out BattleUnit unit)
        {
            float distanceToUnitPlane = -cam.transform.position.z; // units sit at z=0
            var world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, distanceToUnitPlane));
            foreach (var kv in _unitRenderers)
            {
                if (kv.Value.bounds.Contains(new Vector3(world.x, world.y, 0f)))
                {
                    unit = kv.Key;
                    return true;
                }
            }
            unit = null;
            return false;
        }

        public Vector3 GetUnitWorldPosition(BattleUnit unit) =>
            _unitViews.TryGetValue(unit, out var go) ? go.transform.position + Vector3.up * 1.6f : Vector3.zero;

        /// <summary>A unit's resting position in its side dock -- the "return to" point
        /// after a centre-stage cinematic beat, and the source of truth BattleLayout
        /// itself uses when a unit's view is first built.</summary>
        public Vector3 DockPosition(BattleUnit unit) => BattleLayout.UnitPosition(unit.Column);

        const float StageTweenSeconds = 0.25f;

        /// <summary>Tweens only the acting unit from its dock to true screen centre for a
        /// turn's cinematic beat -- the target stays put on its dock throughout. No-op
        /// for a unit with no view (e.g. missing/never-built).</summary>
        public IEnumerator MoveToStage(BattleUnit actor, BattleUnit target)
        {
            yield return TweenPair(actor, BattleLayout.StagePosition(),
                target, DockPosition(target));
        }

        /// <summary>Reverse of MoveToStage -- tweens both back to their dock
        /// positions.</summary>
        public IEnumerator ReturnToDock(BattleUnit actor, BattleUnit target)
        {
            yield return TweenPair(actor, DockPosition(actor), target, DockPosition(target));
        }

        /// <summary>True if `skill` has a real FMV clip to play for `unit` right now --
        /// callers (BattleController) branch on this to choose PlayActionClip over the
        /// flat ImpactHoldSeconds wait. Not restricted to the basic attack: this is the
        /// caster's own body animation (swing/cast/shoot), reused by every skill sharing
        /// that clipKey -- skill-specific identity belongs on SkillDefinition.effect
        /// instead (see BattleController.ResolveAction/PlayImpactFx), so it's correct for
        /// e.g. Power Strike to play the same swing clip as the plain basic attack.</summary>
        public bool HasActionClip(BattleUnit unit, SkillDefinition skill)
        {
            var entry = unit.Definition.clips != null ? unit.Definition.clips.Get(skill.clipKey) : null;
            return entry != null && entry.clip != null
                && _clipPlayers.TryGetValue(unit, out var player) && player.IsReady;
        }

        /// <summary>Plays the clip HasActionClip already confirmed exists, hiding the
        /// unit's sprite for the duration and restoring it after. Simplification worth
        /// noting: this replaces the post-ResolveAction wait, so the clip starts playing
        /// only after damage/heal numbers and the flash/impact-FX have already fired --
        /// it is not frame-synced to the clip's own impactFrames yet (see
        /// BattleClipPlayer's class doc for why that metadata can't be trusted today).</summary>
        public IEnumerator PlayActionClip(BattleUnit unit, SkillDefinition skill, Action onImpact)
        {
            var entry = unit.Definition.clips.Get(skill.clipKey);
            var player = _clipPlayers[unit];
            if (_unitRenderers.TryGetValue(unit, out var sr)) sr.enabled = false;
            yield return player.Play(entry, unit.FacingRight, onImpact);
            if (_unitRenderers.TryGetValue(unit, out var sr2)) sr2.enabled = true;
        }

        /// <summary>True if `unit` has a real "hit" reaction clip (ClipSet.KeyHit) to
        /// overlay when it gets hit -- callers (BattleController.ResolveAction) check
        /// this alongside FlashHit/PlayImpactFx. No character authors this clip yet (the
        /// key was reserved from the start, see ClipSet's class doc, but only the 3
        /// basic-attack clips exist today) -- this fails safe to the existing flash/FX
        /// presentation until reaction clips are actually provided, same pattern as
        /// HasActionClip.</summary>
        public bool HasReactionClip(BattleUnit unit)
        {
            var entry = unit.Definition.clips != null ? unit.Definition.clips.Get(Game.Data.ClipSet.KeyHit) : null;
            return entry != null && entry.clip != null
                && _clipPlayers.TryGetValue(unit, out var player) && player.IsReady;
        }

        /// <summary>Plays the clip HasReactionClip already confirmed exists, layered over
        /// `unit`'s sprite (not hiding it, unlike PlayActionClip -- this is an overlay
        /// reaction, not a stand-in for the whole unit) via the -0.05z nudge BuildUnitView
        /// gives every clip quad. Fire-and-forget like FlashHit/PlayImpactFx -- doesn't
        /// block the turn's PlayImpactBeat, since a hit reaction should play alongside the
        /// attacker's own action clip, not gate it.</summary>
        public void PlayReactionClip(BattleUnit unit)
        {
            var entry = unit.Definition.clips.Get(Game.Data.ClipSet.KeyHit);
            StartCoroutine(_clipPlayers[unit].Play(entry, unit.FacingRight, onImpact: null));
        }

        /// <summary>Reposition action: two same-faction units have already swapped Column
        /// values (BattleController.Reposition) -- tween both to their new dock positions.</summary>
        public IEnumerator SwapPositions(BattleUnit a, BattleUnit b)
        {
            yield return TweenPair(a, DockPosition(a), b, DockPosition(b));
        }

        /// <summary>After Formation.Compact reassigns columns on a death, tween every
        /// surviving unit of that faction to its new dock position so the frontline
        /// shift reads clearly instead of popping.</summary>
        public IEnumerator ReflowFormation(BattleWorld world, Game.Data.Faction faction)
        {
            var movers = world.AllUnits.Where(u => u.Faction == faction && u.IsAlive).ToList();
            if (movers.Count == 0) yield break;

            float t = 0f;
            var froms = movers.Select(u => _unitViews.TryGetValue(u, out var go) ? go.transform.position : DockPosition(u)).ToList();
            while (t < StageTweenSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / StageTweenSeconds));
                for (int i = 0; i < movers.Count; i++)
                    if (_unitViews.TryGetValue(movers[i], out var go))
                        go.transform.position = Vector3.Lerp(froms[i], DockPosition(movers[i]), k);
                yield return null;
            }
            foreach (var u in movers)
                if (_unitViews.TryGetValue(u, out var go)) go.transform.position = DockPosition(u);
        }

        /// <summary>Sub-in/sub-out: outgoing steps off-field, incoming appears in the
        /// exact column it vacated. BattleController has already swapped their Column
        /// values and the World.AllUnits/Bench lists before calling this.</summary>
        public IEnumerator SwapUnitView(BattleUnit outgoing, BattleUnit incoming)
        {
            if (_unitViews.TryGetValue(outgoing, out var outGo)) outGo.SetActive(false);
            if (_unitViews.TryGetValue(incoming, out var inGo))
            {
                inGo.transform.position = DockPosition(incoming);
                inGo.SetActive(true);
                if (_unitRenderers.TryGetValue(incoming, out var inSr)) inSr.color = Color.white;
            }
            yield break;
        }

        IEnumerator TweenPair(BattleUnit unitA, Vector3 toA, BattleUnit unitB, Vector3 toB)
        {
            _unitViews.TryGetValue(unitA, out var goA);
            _unitViews.TryGetValue(unitB, out var goB);
            if (goA == null && goB == null) yield break;

            var fromA = goA != null ? goA.transform.position : toA;
            var fromB = goB != null ? goB.transform.position : toB;

            float t = 0f;
            while (t < StageTweenSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / StageTweenSeconds));
                if (goA != null) goA.transform.position = Vector3.Lerp(fromA, toA, k);
                if (goB != null) goB.transform.position = Vector3.Lerp(fromB, toB, k);
                yield return null;
            }
            if (goA != null) goA.transform.position = toA;
            if (goB != null) goB.transform.position = toB;
        }

        static readonly Color DeadTint = new(0.35f, 0.35f, 0.35f, 0.6f);
        static readonly Color BrokenTint = new(0.55f, 0.55f, 0.55f, 1f);

        /// <summary>Dead overrides broken (a corpse doesn't need two tints), otherwise
        /// broken (M16, "for now make them slightly darker" per the project owner) is the
        /// only other tint state today.</summary>
        static Color UnitTint(BattleUnit unit)
        {
            if (!unit.IsAlive) return DeadTint;
            if (unit.StatusEffects.Any(s => s.Type == StatusEffectType.Break)) return BrokenTint;
            return Color.white;
        }

        /// <summary>Re-applies every unit's dead/alive/broken tint -- needed after
        /// BattleHistory.Restore snapshots HP back onto units outside the normal
        /// ApplyDamage path (undo can revive a unit SyncDefeated already greyed out).</summary>
        public void SyncAll(BattleWorld world)
        {
            foreach (var unit in world.AllUnits)
                if (_unitRenderers.TryGetValue(unit, out var sr)) sr.color = UnitTint(unit);
        }

        /// <summary>Re-applies just `unit`'s dead/alive/broken tint -- called right after
        /// BattleController.RunBattle ticks status effects, the one point in the turn loop
        /// where Break can either newly apply or just have expired.</summary>
        public void SyncStatusTint(BattleUnit unit)
        {
            if (_unitRenderers.TryGetValue(unit, out var sr)) sr.color = UnitTint(unit);
        }

        /// <summary>Immediately (no tween) snaps every unit back to its dock position and
        /// re-applies active/bench visibility -- used after Undo/Redo, which can interrupt
        /// a MoveToStage/ReturnToDock tween mid-flight when it stops the turn coroutine,
        /// and can also move units between World.AllUnits and World.Bench (undoing past a
        /// sub-in/sub-out).</summary>
        public void SnapAllToDock(BattleWorld world)
        {
            foreach (var unit in world.AllUnits)
            {
                if (!_unitViews.TryGetValue(unit, out var go)) continue;
                go.SetActive(true);
                go.transform.position = DockPosition(unit);
            }
            foreach (var unit in world.Bench)
                if (_unitViews.TryGetValue(unit, out var go)) go.SetActive(false);
        }

        public void FlashHit(BattleUnit unit)
        {
            if (_unitRenderers.TryGetValue(unit, out var sr)) StartCoroutine(FlashRoutine(sr));
        }

        System.Collections.IEnumerator FlashRoutine(SpriteRenderer sr)
        {
            var original = sr.color;
            sr.color = Color.red;
            yield return new WaitForSeconds(0.12f);
            if (sr != null) sr.color = original;
        }

        public void SyncDefeated(BattleUnit unit)
        {
            if (!unit.IsAlive && _unitRenderers.TryGetValue(unit, out var sr))
                sr.color = DeadTint;
        }

        const float FxWorldHeight = 1.8f;
        const float FxFrameSeconds = 0.045f;

        /// <summary>Overlays an impact flipbook on `target`. Uses `skill.effect` (see
        /// SkillEffect's class doc) when the skill authors one, otherwise falls back to
        /// the map's generic fxImpactSheet -- every skill today falls back, since no
        /// SkillEffect assets exist yet, but this is the hook a future skill/orb-specific
        /// effect plugs into without touching this method again.</summary>
        public void PlayImpactFx(BattleUnit target, SkillDefinition skill = null)
        {
            var effect = skill != null ? skill.effect : null;
            Sprite sheet = effect != null ? effect.sheet : _map?.fxImpactSheet;
            List<Vector4> rects = effect != null ? effect.frameRects : _map?.fxImpactFrameRects;
            float worldHeight = effect != null ? effect.worldHeight : FxWorldHeight;
            float frameSeconds = effect != null ? effect.frameSeconds : FxFrameSeconds;

            if (sheet == null || rects == null || rects.Count == 0) return;
            if (!_unitViews.TryGetValue(target, out var targetGo)) return;
            StartCoroutine(ImpactFxRoutine(targetGo.transform.position + Vector3.up * 0.6f, sheet, rects, worldHeight, frameSeconds));
        }

        System.Collections.IEnumerator ImpactFxRoutine(Vector3 worldPos, Sprite sheet, List<Vector4> frameRects, float worldHeight, float frameSeconds)
        {
            var tex = sheet.texture;
            var go = new GameObject("ImpactFx");
            go.transform.SetParent(transform, false);
            go.transform.position = worldPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 20;

            foreach (var rect in frameRects)
            {
                var pixelRect = new Rect(rect.x, tex.height - rect.y - rect.w, rect.z, rect.w);
                var frameSprite = Sprite.Create(tex, pixelRect, new Vector2(0.5f, 0.5f), 100f);
                sr.sprite = frameSprite;
                float s = worldHeight / Mathf.Max(frameSprite.bounds.size.y, 0.01f);
                go.transform.localScale = Vector3.one * s;
                yield return new WaitForSeconds(frameSeconds);
            }
            Destroy(go);
        }
    }
}
