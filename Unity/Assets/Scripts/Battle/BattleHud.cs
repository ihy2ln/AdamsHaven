using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>IMGUI HUD (no Canvas wiring), mirrors FarmHud's approach: HP/MP bars,
    /// battle log line/review panel, damage numbers, win/lose banner, and the modern-UX
    /// layer -- pause menu, settings, undo/redo, keybind legend.</summary>
    public class BattleHud : MonoBehaviour
    {
        BattleController _ctrl;
        Camera _cam;
        BattleVisuals _visuals;
        GUIStyle _title, _body, _name, _big, _sub, _dmg, _btn, _smallBtn, _iconBtn, _prompt, _actorName, _logEntry, _logRound, _toggle, _barLabel;
        Texture2D _ultGradientTex;

        bool _showLog;
        Vector2 _logScroll;
        bool _showSettings;
        bool _showKeybinds;
        bool _confirmRestart;

        // M16: tap a unit's name in the roster to inspect its full stats (single-unit
        // popup); the pause menu's "Unit Stats" instead lists everyone at once.
        BattleUnit _inspectedUnit;
        bool _showStats;
        Vector2 _statsScroll;

        // Tap the "SM" icon to toggle the Skill Move list -- see DrawActionMenu. This was
        // originally press-and-hold (0.35s), which read as an unresponsive button: a tap
        // did nothing and gave no hint that holding was the gesture. BA/R/S are all taps,
        // so SM being the one exception was the problem, not the timing.
        bool _showSkillList;

        // Same tap-to-toggle pattern as _showSkillList, for the "I" (Item) icon (M13).
        bool _showItemList;

        public void Init(BattleController ctrl, Camera cam, BattleVisuals visuals, bool logOpenByDefault)
        {
            _ctrl = ctrl;
            _cam = cam;
            _visuals = visuals;
            _showLog = logOpenByDefault;
            if (_showLog) _logScroll = new Vector2(0, float.MaxValue);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.L))
            {
                _showLog = !_showLog;
                if (_showLog) _logScroll = new Vector2(0, float.MaxValue);
            }

            // The skill/item lists only belong to a live action choice -- close them as
            // soon as the turn moves on to target selection or to the next unit.
            if (_ctrl == null || _ctrl.Phase != ActionPhase.ChooseAction)
            {
                _showSkillList = false;
                _showItemList = false;
            }
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20, fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.91f, 0.69f, 0.35f) },
            };
            _body = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14, normal = { textColor = new Color(0.94f, 0.90f, 0.83f) }, wordWrap = true,
            };
            _name = new GUIStyle(GUI.skin.label) { fontSize = 11, normal = { textColor = Color.white } };
            _big = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow },
            };
            _sub = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
            _dmg = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 18, fontStyle = FontStyle.Bold };
            _smallBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            _iconBtn = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _prompt = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) },
            };
            _actorName = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.3f) },
            };
            _logEntry = new GUIStyle(GUI.skin.label) { fontSize = 13, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }, wordWrap = true };
            _logRound = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.91f, 0.69f, 0.35f) } };
            _toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 14, normal = { textColor = new Color(0.94f, 0.90f, 0.83f) } };
            _barLabel = new GUIStyle(GUI.skin.label) { fontSize = 9, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            // 64x1 full-hue-cycle strip, sampled 0..pct across its U axis by
            // DrawUltimateBarFill -- as the gauge fills, progressively more of the
            // rainbow reveals rather than the whole strip just fading in, so it visibly
            // reads as "gradient rainbow hex themed" per the project owner's spec.
            const int GradientWidth = 64;
            _ultGradientTex = new Texture2D(GradientWidth, 1, TextureFormat.RGB24, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < GradientWidth; i++)
                _ultGradientTex.SetPixel(i, 0, Color.HSVToRGB((float)i / GradientWidth, 0.85f, 1f));
            _ultGradientTex.Apply();
        }

        void OnGUI()
        {
            if (_ctrl == null || _ctrl.World == null) return;
            EnsureStyles();
            int w = Screen.width, h = Screen.height;

            GUI.Label(new Rect(0, 12, w, 30), $"AI.Game -- Battle ({(_ctrl.ManualMode ? "manual" : "auto")})", _title);
            DrawTurnOrderStrip(w);
            DrawModeToggle(w);
            DrawUndoRedoButtons(w);
            DrawPauseButton(w);
            DrawLogToggle(w);
            DrawKeybindToggle(w);

            DrawRoster(_ctrl.World.PlayerUnits, 16, 90, false);
            DrawRoster(_ctrl.World.EnemyUnits, w - 176, 90, true);

            GUI.Box(new Rect(12, h - 56, Mathf.Min(700, w - 24), 40), GUIContent.none);
            GUI.Label(new Rect(24, h - 48, Mathf.Min(680, w - 48), 30), _ctrl.LastAction, _body);

            DrawDamageNumbers();
            if (_ctrl.Phase == ActionPhase.ChooseAction) DrawActionMenu();
            if (_ctrl.Phase == ActionPhase.ChooseBench) DrawBenchMenu(w);
            if (_ctrl.Phase == ActionPhase.ChooseTarget) DrawTargetPrompt(w);
            if (_showLog) DrawLogPanel(w, h);
            if (_showKeybinds) DrawKeybindPanel(w, h);
            if (_inspectedUnit != null) DrawUnitStatsPanel(_inspectedUnit, w, h);

            if (_ctrl.Outcome != BattleOutcome.InProgress && !_ctrl.Paused) DrawOutcomeBanner(w, h);
            if (_ctrl.Paused) DrawPauseOverlay(w, h);
        }

        void DrawUndoRedoButtons(int w)
        {
            GUI.enabled = _ctrl.CanUndo;
            if (GUI.Button(new Rect(w / 2f - 150, 14, 56, 30), "Undo", _smallBtn)) _ctrl.Undo();
            GUI.enabled = _ctrl.CanRedo;
            if (GUI.Button(new Rect(w / 2f + 94, 14, 56, 30), "Redo", _smallBtn)) _ctrl.Redo();
            GUI.enabled = true;
        }

        void DrawPauseButton(int w)
        {
            if (GUI.Button(new Rect(w - 304, 14, 140, 26), "Pause (Esc)", _btn)) _ctrl.SetPaused(true);
        }

        void DrawLogToggle(int w)
        {
            string label = _showLog ? "Hide Log (L)" : "Turn Log (L)";
            if (GUI.Button(new Rect(w - 150, 14, 134, 26), label, _btn)) _showLog = !_showLog;
        }

        void DrawKeybindToggle(int w)
        {
            if (GUI.Button(new Rect(w - 150, 44, 134, 24), "Keybinds (?)", _smallBtn)) _showKeybinds = !_showKeybinds;
        }

        void DrawKeybindPanel(int w, int h)
        {
            var panel = new Rect(w / 2f - 180, 130, 360, 262);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 10, panel.y + 6, panel.width - 20, 22), "Keybinds", _title);
            string[] lines =
            {
                "T -- toggle auto / manual mode",
                "L -- open / close turn log",
                "Esc -- pause",
                "Ctrl+Z -- undo last turn",
                "Ctrl+Y -- redo turn",
                "R -- restart (after battle ends)",
                "N -- skip this battle, go to the next stage",
                "Click -- choose a highlighted target",
                "BA/SM/U/R/S/I/F -- tap. SM/I open a list; tap again to close",
                "U needs a full ultimate gauge; F flees (costs the turn either way)",
            };
            float y = panel.y + 32;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(panel.x + 12, y, panel.width - 24, 20), line, _logEntry);
                y += 20;
            }
        }

        void DrawLogPanel(int w, int h)
        {
            var panel = new Rect(w / 2f - 220, 130, 440, Mathf.Min(h - 220, 420));
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 10, panel.y + 6, panel.width - 20, 22), "Turn Log", _title);

            var viewRect = new Rect(panel.x + 8, panel.y + 32, panel.width - 16, panel.height - 40);
            float lineHeight = 20f;
            var entries = _ctrl.Log.Entries;
            float contentHeight = entries.Count * lineHeight + CountRoundHeaders(entries) * 18f + 8f;
            var contentRect = new Rect(0, 0, viewRect.width - 20, contentHeight);

            _logScroll = GUI.BeginScrollView(viewRect, _logScroll, contentRect);
            float y = 0f;
            int lastRound = -1;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Round != lastRound)
                {
                    GUI.Label(new Rect(4, y, contentRect.width - 8, 18), $"-- Round {entry.Round} --", _logRound);
                    y += 18f;
                    lastRound = entry.Round;
                }
                GUI.Label(new Rect(10, y, contentRect.width - 14, lineHeight), entry.Text, _logEntry);
                y += lineHeight;
            }
            GUI.EndScrollView();
        }

        static int CountRoundHeaders(IReadOnlyList<BattleLogEntry> entries)
        {
            int count = 0, lastRound = -1;
            foreach (var entry in entries)
            {
                if (entry.Round == lastRound) continue;
                lastRound = entry.Round;
                count++;
            }
            return count;
        }

        // Compact per-unit roster readout: name, a thin HP bar, a thinner MP bar below it,
        // an ultimate-gauge bar (M16) below that, a break-progress bar (M16) below that,
        // and (M13) a status-tag line squeezed into the gap before the next unit.
        const float BarWidth = 160f, HpHeight = 11f, MpHeight = 6f, UltHeight = 6f, BreakHeight = 4f, NameHeight = 13f, BarGap = 2f, UnitGap = 20f;

        void DrawRoster(IEnumerable<BattleUnit> units, float x, float y, bool rightAligned)
        {
            foreach (var unit in units)
            {
                DrawUnitBars(x, y, unit, rightAligned);
                y += NameHeight + HpHeight + BarGap + MpHeight + BarGap + UltHeight + BarGap + BreakHeight + UnitGap;
            }
        }

        void DrawUnitBars(float x, float y, BattleUnit unit, bool rightAligned)
        {
            // A label styled as a Button (GUI.skin.label has no button background
            // texture, so this looks identical to a plain name label) -- tap a unit's
            // name to inspect its full stats (M16). Toggle, matching every other
            // popup-open button in this HUD.
            var nameStyle = new GUIStyle(_name) { alignment = rightAligned ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            if (GUI.Button(new Rect(x, y, BarWidth, NameHeight), unit.Definition.displayName, nameStyle))
                _inspectedUnit = _inspectedUnit == unit ? null : unit;
            y += NameHeight;

            var hpRect = new Rect(x, y, BarWidth, HpHeight);
            GUI.Box(hpRect, GUIContent.none);
            float hpPct = unit.Stats.hp > 0 ? (float)unit.CurrentHp / unit.Stats.hp : 0f;
            DrawBarFill(hpRect, hpPct, rightAligned,
                !unit.IsAlive ? Color.gray : (hpPct > 0.5f ? Color.green : (hpPct > 0.2f ? Color.yellow : Color.red)));
            GUI.Label(hpRect, $"{unit.CurrentHp}/{unit.Stats.hp}", _barLabel);
            y += HpHeight + BarGap;

            var mpRect = new Rect(x, y, BarWidth, MpHeight);
            GUI.Box(mpRect, GUIContent.none);
            float mpPct = unit.MaxMp > 0 ? (float)unit.CurrentMp / unit.MaxMp : 0f;
            DrawBarFill(mpRect, mpPct, rightAligned, new Color(0.2f, 0.45f, 1f));
            y += MpHeight + BarGap;

            var ultRect = new Rect(x, y, BarWidth, UltHeight);
            GUI.Box(ultRect, GUIContent.none);
            float ultPct = (float)unit.CurrentUltimateCharge / BattleUnit.MaxUltimateCharge;
            DrawUltimateBarFill(ultRect, ultPct, rightAligned);
            y += UltHeight + BarGap;

            // Break progress (M16) -- how close `unit` is to the damage-since-last-turn
            // threshold that triggers Break (BattleController.BreakDamageThresholdFraction),
            // not how close they are to recovering from an already-active one. Lets a
            // player see danger building before the status tag/sprite-darken actually fire.
            var breakRect = new Rect(x, y, BarWidth, BreakHeight);
            GUI.Box(breakRect, GUIContent.none);
            float breakThreshold = BattleController.BreakDamageThresholdFraction * unit.Stats.hp;
            float breakPct = breakThreshold > 0f ? unit.DamageTakenSinceLastTurn / breakThreshold : 0f;
            DrawBarFill(breakRect, breakPct, rightAligned, new Color(0.85f, 0.25f, 0.15f));
            y += BreakHeight + 1f;

            if (unit.StatusEffects.Count > 0)
            {
                string tags = string.Join(" ", unit.StatusEffects.Select(StatusTag));
                var tagStyle = new GUIStyle(_barLabel) { alignment = rightAligned ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
                GUI.Label(new Rect(x, y, BarWidth, UnitGap - 2f), tags, tagStyle);
            }
        }

        /// <summary>Short readout for the roster status-tag line -- type abbreviation
        /// plus turns remaining, e.g. "PSN(2)", "ATK-(1)". Not the popup's job to explain
        /// the full effect, just to show at a glance that something is active and when it
        /// runs out.</summary>
        static string StatusTag(StatusEffectInstance effect)
        {
            string abbrev = effect.Type switch
            {
                StatusEffectType.AttackUp => "ATK+",
                StatusEffectType.AttackDown => "ATK-",
                StatusEffectType.DefenseUp => "DEF+",
                StatusEffectType.DefenseDown => "DEF-",
                StatusEffectType.Poison => "PSN",
                StatusEffectType.Regen => "REGEN",
                StatusEffectType.Stun => "STUN",
                StatusEffectType.Break => "BRK",
                _ => effect.Type.ToString(),
            };
            return $"{abbrev}({effect.RemainingTurns})";
        }

        const float UltRotationHz = 0.6f;

        /// <summary>Ultimate gauge fill (M16): while charging, a rainbow gradient reveals
        /// progressively (0..pct of the strip's hue range, not the whole strip fading in)
        /// via _ultGradientTex; once full, switches to a single hue that continuously
        /// rotates through the wheel (Time.unscaledTime -- same "ignore pause/speed"
        /// convention SM's old hold-timer used) so a ready ultimate visibly announces
        /// itself even on a screen full of other bars.</summary>
        void DrawUltimateBarFill(Rect rect, float pct, bool rightAligned)
        {
            pct = Mathf.Clamp01(pct);
            float fillWidth = (rect.width - 2) * pct;
            float fillX = rightAligned ? rect.x + 1 + (rect.width - 2 - fillWidth) : rect.x + 1;
            var fill = new Rect(fillX, rect.y + 1, fillWidth, Mathf.Max(1f, rect.height - 2));
            if (fillWidth <= 0f) return;

            if (pct >= 1f)
            {
                float hue = (Time.unscaledTime * UltRotationHz) % 1f;
                var old = GUI.color;
                GUI.color = Color.HSVToRGB(hue, 0.85f, 1f);
                GUI.DrawTexture(fill, Texture2D.whiteTexture);
                GUI.color = old;
                return;
            }

            var texCoords = rightAligned ? new Rect(1f - pct, 0f, pct, 1f) : new Rect(0f, 0f, pct, 1f);
            GUI.DrawTextureWithTexCoords(fill, _ultGradientTex, texCoords);
        }

        /// <summary>Draws a Sprite's actual sub-rect (not its whole backing texture --
        /// battleSprite may be packed in an atlas) into `rect`, with a solid-color border
        /// so the turn-order strip (M16) reads faction at a glance without needing to
        /// recognize the art itself. No-ops (draws just the border) if `sprite` is null --
        /// same "missing art isn't an error" convention the rest of this project follows.</summary>
        static void DrawSpriteIcon(Rect rect, Sprite sprite, Color borderColor)
        {
            var old = GUI.color;
            GUI.color = borderColor;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;

            if (sprite == null || sprite.texture == null) return;
            var inner = new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4);
            var texRect = sprite.rect;
            var tex = sprite.texture;
            var uv = new Rect(texRect.x / tex.width, texRect.y / tex.height, texRect.width / tex.width, texRect.height / tex.height);
            GUI.DrawTextureWithTexCoords(inner, tex, uv);
        }

        static void DrawBarFill(Rect rect, float pct, bool rightAligned, Color color)
        {
            // 1px padding per side, not 2 -- 2px-per-side left zero height for the thin
            // MP bar (height 4-6) once the border was subtracted, which is why it always
            // rendered empty regardless of the underlying value.
            float fillWidth = (rect.width - 2) * Mathf.Clamp01(pct);
            float fillX = rightAligned ? rect.x + 1 + (rect.width - 2 - fillWidth) : rect.x + 1;
            var fill = new Rect(fillX, rect.y + 1, fillWidth, Mathf.Max(1f, rect.height - 2));
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(fill, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawModeToggle(int w)
        {
            string label = _ctrl.ManualMode ? "Mode: Manual (T)" : "Mode: Auto (T)";
            if (GUI.Button(new Rect(w / 2f - 90, 14, 180, 30), label, _btn)) _ctrl.ToggleMode();
        }

        const int TurnOrderSlots = 7;
        const float TurnOrderIconSize = 32f, TurnOrderIconGap = 6f;

        /// <summary>Small-portrait turn-order preview (M16), centred under the title/mode
        /// toggle -- left to right, with the RIGHT edge being soonest to act (index 0 of
        /// UpcomingTurnOrder sits in the rightmost slot) and each unit further out in the
        /// order placed one slot further left. The number under each icon (1 = acts next)
        /// makes the direction unambiguous even without comparing positions by eye.</summary>
        void DrawTurnOrderStrip(int w)
        {
            var upcoming = _ctrl.UpcomingTurnOrder(TurnOrderSlots);
            if (upcoming.Count == 0) return;

            float step = TurnOrderIconSize + TurnOrderIconGap;
            float totalWidth = upcoming.Count * TurnOrderIconSize + (upcoming.Count - 1) * TurnOrderIconGap;
            float leftX = w / 2f - totalWidth / 2f;
            float y = 50f;

            for (int i = 0; i < upcoming.Count; i++)
            {
                // i=0 (soonest) goes in the rightmost slot; furthest-out goes leftmost.
                float x = leftX + (upcoming.Count - 1 - i) * step;
                var rect = new Rect(x, y, TurnOrderIconSize, TurnOrderIconSize);
                var unit = upcoming[i];
                var border = unit.Faction == Faction.Player ? new Color(0.3f, 0.55f, 1f) : new Color(0.9f, 0.3f, 0.3f);
                DrawSpriteIcon(rect, unit.Definition.battleSprite, border);
                GUI.Label(new Rect(x, y + TurnOrderIconSize, TurnOrderIconSize, 16f), (i + 1).ToString(), _barLabel);
            }
        }

        void DrawTargetPrompt(int w)
        {
            if (_ctrl.PendingActor == null || _cam == null || _visuals == null) return;

            GUI.Label(new Rect(0, 126, w, 26), $"{_ctrl.PendingActor.Definition.displayName} -- tap a highlighted target",
                _prompt);

            foreach (var target in _ctrl.PendingTargets)
            {
                var pos = _visuals.GetUnitWorldPosition(target);
                var screen = _cam.WorldToScreenPoint(pos);
                if (screen.z < 0) continue;
                var guiPos = new Vector2(screen.x, Screen.height - screen.y);

                var old = GUI.color;
                GUI.color = new Color(1f, 0.9f, 0.2f, 0.9f);
                var ring = new Rect(guiPos.x - 55, guiPos.y - 70, 110, 110);
                GUI.Box(ring, GUIContent.none);
                GUI.color = old;
            }
        }

        // Small icon bar (BA/SM/U/R/S/I) anchored at the acting unit's feet, plus its
        // name label just above the icons -- replaces the old full-width centred panel.
        const float IconSize = 32f, IconGap = 4f;

        /// <summary>Keeps a centred UI block fully on screen -- returns the left edge X
        /// for the given desired centre X and total width, clamped so neither edge goes
        /// past the screen margin. Units near the left/right edge of the battlefield
        /// (e.g. Sable in the back column) would otherwise push the action menu or skill
        /// popup partly off-screen.</summary>
        static float ClampedLeftX(float desiredCenterX, float totalWidth, float margin = 10f)
        {
            float half = totalWidth / 2f;
            float min = margin + half;
            float max = Screen.width - margin - half;
            float center = max < min ? Screen.width / 2f : Mathf.Clamp(desiredCenterX, min, max);
            return center - half;
        }

        void DrawActionMenu()
        {
            var actor = _ctrl.PendingActor;
            if (actor == null || _cam == null || _visuals == null) { _showSkillList = false; return; }

            // Anchored below the acting unit's actual feet -- DockPosition is the
            // sprite's pivot, which is Center (Unity's default sprite import pivot), not
            // Bottom, so it sits at the character's torso, not their feet. Stepping down
            // half the sprite's world-space height (BattleLayout.TargetUnitHeight, what
            // every sprite is normalized to -- see BattleVisuals.BuildUnitView) reaches
            // the visual bottom edge instead. Clamped horizontally so it can't run off
            // the edge of the screen for a back-column/edge unit.
            var feetWorld = _visuals.DockPosition(actor) + Vector3.down * (BattleLayout.TargetUnitHeight / 2f);
            var screen = _cam.WorldToScreenPoint(feetWorld);
            if (screen.z < 0) { _showSkillList = false; return; }
            float anchorX = screen.x;
            float anchorY = Screen.height - screen.y;

            float nameLeft = ClampedLeftX(anchorX, 140f);
            GUI.Label(new Rect(nameLeft, anchorY + 2, 140, 18), actor.Definition.displayName, _actorName);

            var basicAttack = _ctrl.BasicAttackSkill(actor);
            var skillMoveOptions = _ctrl.SkillMoveOptions(actor);
            var ultimateSkill = actor.Definition.ultimateSkill;

            var labels = new[] { "BA", "SM", "U", "R", "S", "I", "F" };
            var enabled = new[]
            {
                basicAttack != null, skillMoveOptions.Count > 0, actor.IsUltimateReady && ultimateSkill != null,
                _ctrl.CanReposition, _ctrl.CanSub, _ctrl.CanUseItem, _ctrl.CanEscape,
            };

            float totalW = labels.Length * IconSize + (labels.Length - 1) * IconGap;
            float startX = ClampedLeftX(anchorX, totalW);
            float y = anchorY + 22f;

            for (int i = 0; i < labels.Length; i++)
            {
                var rect = new Rect(startX + i * (IconSize + IconGap), y, IconSize, IconSize);

                // The ready-ultimate glow is drawn as a separate, non-interactive border
                // BEHIND the button rather than animating the button's own backgroundColor
                // (the original approach) -- a GUI.Button whose paint state changes every
                // single repaint is a known IMGUI foot-gun for eaten/missed clicks, and
                // that's exactly what the project owner reported ("have to press multiple
                // times ... for each unit", i.e. every unit's U button, the one button
                // whose colour was animating). The button itself now always paints with a
                // completely stable colour, identical in kind to BA/SM/R/S/I.
                if (labels[i] == "U" && enabled[i]) DrawUltimateGlow(rect);

                GUI.enabled = enabled[i];
                GUI.backgroundColor = ActionButtonColor(labels[i]);
                bool clicked = GUI.Button(rect, labels[i], _iconBtn);
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                if (!clicked) continue;

                switch (labels[i])
                {
                    case "BA": _ctrl.ChooseSkill(basicAttack); break;
                    // Toggle, so a second tap backs out of the list without committing to
                    // a skill/item -- there's no other way to dismiss either popup.
                    case "SM": _showSkillList = !_showSkillList; break;
                    case "U": _ctrl.ChooseSkill(ultimateSkill); break;
                    case "R": _ctrl.ChooseReposition(); break;
                    case "S": _ctrl.OpenBenchMenu(); break;
                    case "I": _showItemList = !_showItemList; break;
                    case "F": _ctrl.ChooseEscape(); break;
                }
            }

            // The one action whose outcome is a coin flip, so the odds go on screen
            // rather than only in the log after the fact -- and they climb visibly with
            // each failure, which is the whole reason the escalation exists.
            if (_ctrl.CanEscape)
            {
                string odds = $"Flee {_ctrl.EscapeChanceNow:P0}";
                if (_ctrl.FailedEscapeAttempts > 0) odds += $" (+{_ctrl.FailedEscapeAttempts})";
                GUI.Label(new Rect(startX, y + IconSize + 2f, totalW, 16f), odds, _barLabel);
            }

            if (_showSkillList) DrawSkillListPopup(actor, skillMoveOptions, startX + totalW / 2f, y);
            if (_showItemList) DrawItemListPopup(startX + totalW / 2f, y);
        }

        /// <summary>Per-action tint for the BA/SM/U/R/S/I buttons (M16) -- a colour-coded
        /// row reads faster than six identical grey buttons distinguished only by their
        /// 2-letter label. Deliberately time-independent for every label including U --
        /// see DrawUltimateGlow for why U's own animated highlight lives on a separate,
        /// non-interactive layer instead of here.</summary>
        static Color ActionButtonColor(string label) => label switch
        {
            "BA" => new Color(0.85f, 0.4f, 0.3f),
            "SM" => new Color(0.35f, 0.55f, 0.9f),
            "U" => new Color(0.75f, 0.6f, 0.2f),
            "R" => new Color(0.4f, 0.75f, 0.45f),
            "S" => new Color(0.6f, 0.45f, 0.8f),
            "I" => new Color(0.35f, 0.8f, 0.55f),
            "F" => new Color(0.7f, 0.7f, 0.75f),
            _ => Color.white,
        };

        const float UltGlowThickness = 4f;

        /// <summary>Animated rainbow border drawn BEHIND the U button's rect, purely
        /// decorative and non-interactive -- a plain GUI.DrawTexture per side, not a
        /// control, so it can never intercept or interfere with the click landing on the
        /// actual Button. See the button-loop's own comment for why this was split out
        /// from ActionButtonColor in the first place.</summary>
        static void DrawUltimateGlow(Rect buttonRect)
        {
            var glow = Color.HSVToRGB((Time.unscaledTime * UltRotationHz) % 1f, 0.85f, 1f);
            var r = new Rect(buttonRect.x - UltGlowThickness, buttonRect.y - UltGlowThickness,
                buttonRect.width + UltGlowThickness * 2f, buttonRect.height + UltGlowThickness * 2f);
            var old = GUI.color;
            GUI.color = glow;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void DrawSkillListPopup(BattleUnit actor, IReadOnlyList<SkillDefinition> options, float anchorX, float iconsY)
        {
            const float panelW = 150f, btnH = 26f;
            float panelH = options.Count * (btnH + 4f) + 8f;
            float panelLeft = ClampedLeftX(anchorX, panelW);
            float panelTop = Mathf.Max(4f, iconsY - panelH - 8f);
            var panel = new Rect(panelLeft, panelTop, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            float y = panel.y + 4f;
            foreach (var skill in options)
            {
                // Always the skill's own name. This used to force "Heal" for anything with
                // targetsAllies, which collapsed every support skill into the same label --
                // Kestrel's Second Wind and Rally both read "Heal", as did all three of
                // Linnet's. targetsAllies means "aims at my side", not "is the heal".
                string name = !string.IsNullOrEmpty(skill.displayName) ? skill.displayName : "Skill";
                int mpCost = _ctrl.EffectiveMpCost(skill);
                string label = mpCost > 0 ? $"{name} ({mpCost} MP)" : name;

                GUI.enabled = actor.CurrentMp >= mpCost;
                if (GUI.Button(new Rect(panel.x + 4, y, panelW - 8, btnH), label, _smallBtn))
                {
                    _ctrl.ChooseSkill(skill);
                    _showSkillList = false;
                }
                GUI.enabled = true;
                y += btnH + 4f;
            }
        }

        static readonly PotionKind[] AllPotionKinds = { PotionKind.Hp, PotionKind.Mp, PotionKind.Multi };

        /// <summary>Mirrors DrawSkillListPopup exactly -- 3 fixed rows (Hp/Mp/Multi,
        /// always all 3, unlike the skill list's variable count) showing the slot's
        /// potion name, rank, and remaining count; greyed out at 0.</summary>
        void DrawItemListPopup(float anchorX, float iconsY)
        {
            const float panelW = 170f, btnH = 26f;
            float panelH = AllPotionKinds.Length * (btnH + 4f) + 8f;
            float panelLeft = ClampedLeftX(anchorX, panelW);
            float panelTop = Mathf.Max(4f, iconsY - panelH - 8f);
            var panel = new Rect(panelLeft, panelTop, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            float y = panel.y + 4f;
            foreach (var kind in AllPotionKinds)
            {
                var slot = _ctrl.Inventory.Slot(kind);
                string name = slot.Potion != null ? slot.Potion.displayName : $"{kind} Potion";
                string label = $"{name} [{(slot.Potion != null ? slot.Potion.rank.ToString() : "?")}] x{slot.Count}";

                GUI.enabled = slot.IsUsable;
                if (GUI.Button(new Rect(panel.x + 4, y, panelW - 8, btnH), label, _smallBtn))
                {
                    _ctrl.ChooseItem(kind);
                    _showItemList = false;
                }
                GUI.enabled = true;
                y += btnH + 4f;
            }
        }

        void DrawBenchMenu(int w)
        {
            var actor = _ctrl.PendingActor;
            if (actor == null) return;

            var bench = _ctrl.BenchOptions;
            const float panelW = 360f, btnH = 36f;
            float panelH = 60f + bench.Count * (btnH + 8f) + 44f;
            var panel = new Rect(w / 2f - panelW / 2f, 150, panelW, panelH);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 10, panel.y + 6, panel.width - 20, 24),
                $"Sub in for {actor.Definition.displayName}", _prompt);

            float y = panel.y + 40;
            foreach (var benched in bench)
            {
                string label = $"{benched.Definition.displayName}  ({benched.CurrentHp}/{benched.Stats.hp} HP)";
                if (GUI.Button(new Rect(panel.x + 20, y, panel.width - 40, btnH), label, _btn))
                    _ctrl.ChooseSub(benched);
                y += btnH + 8f;
            }
            if (GUI.Button(new Rect(panel.x + 20, y + 8f, panel.width - 40, btnH), "Back", _smallBtn))
                _ctrl.CancelBenchMenu();
        }

        void DrawDamageNumbers()
        {
            if (_cam == null) return;
            foreach (var dmg in _ctrl.DamageNumbers)
            {
                var screen = _cam.WorldToScreenPoint(dmg.WorldPos + Vector3.up * (dmg.Age * 1.2f));
                if (screen.z < 0) continue;
                var guiPos = new Vector2(screen.x, Screen.height - screen.y);
                var style = new GUIStyle(_dmg);
                var c = dmg.Color; c.a = Mathf.Clamp01(1.4f - dmg.Age);
                style.normal.textColor = c;
                GUI.Label(new Rect(guiPos.x - 40, guiPos.y - 20, 80, 30), dmg.Text, style);
            }
        }

        void DrawOutcomeBanner(int w, int h)
        {
            // Escaped (M19) deliberately gets no "Next Battle" button even though the
            // party is alive and a next map exists: fleeing is not progress. That's the
            // whole cost of the action, and the only thing separating it from M18's
            // skip, which is a dev convenience rather than a move in the game.
            bool advancing = _ctrl.Outcome == BattleOutcome.PlayerVictory && _ctrl.World.HasNextMap;
            string label = _ctrl.Outcome switch
            {
                BattleOutcome.PlayerVictory => "VICTORY",
                BattleOutcome.Escaped => "ESCAPED",
                _ => "DEFEAT",
            };
            GUI.Label(new Rect(0, h / 2f - 60, w, 60), label, _big);

            if (advancing)
            {
                GUI.Label(new Rect(0, h / 2f, w, 30), "The party presses onward, wounds and all", _sub);
                if (GUI.Button(new Rect(w / 2f - 100, h / 2f + 36, 200, 44), "Next Battle", _btn)) _ctrl.AdvanceToNextMap();
            }
            else
            {
                string sub = _ctrl.Outcome == BattleOutcome.Escaped
                    ? "The party withdrew -- no ground gained. Press R or tap below to try again"
                    : "Press R or tap below to fight again";
                GUI.Label(new Rect(0, h / 2f, w, 30), sub, _sub);
                if (GUI.Button(new Rect(w / 2f - 80, h / 2f + 36, 160, 44), "Restart", _btn)) _ctrl.Restart();
            }
        }

        void DrawPauseOverlay(int w, int h)
        {
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = old;

            if (_confirmRestart) { DrawConfirmRestartPanel(w, h); return; }
            if (_showSettings) { DrawSettingsPanel(w, h); return; }
            if (_showStats) { DrawAllUnitsStatsPanel(w, h); return; }

            const float panelW = 320f, panelH = 400f;
            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x, panel.y + 10, panel.width, 30), "Paused", new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });

            float bx = panel.x + 20, bw = panel.width - 40, by = panel.y + 50;
            if (GUI.Button(new Rect(bx, by, bw, 34), "Resume (Esc)", _btn)) _ctrl.SetPaused(false);
            by += 40;

            GUI.enabled = _ctrl.CanUndo;
            if (GUI.Button(new Rect(bx, by, bw, 34), "Undo Turn (Ctrl+Z)", _btn)) _ctrl.Undo();
            GUI.enabled = _ctrl.CanRedo;
            by += 40;
            if (GUI.Button(new Rect(bx, by, bw, 34), "Redo Turn (Ctrl+Y)", _btn)) _ctrl.Redo();
            GUI.enabled = true;
            by += 40;

            if (GUI.Button(new Rect(bx, by, bw, 34), "Unit Stats", _btn)) _showStats = true;
            by += 40;

            if (GUI.Button(new Rect(bx, by, bw, 34), "Settings", _btn)) _showSettings = true;
            by += 40;

            // Greyed out on the last map, and after a wipe -- see
            // BattleController.CanSkipToNextMap for why the second case matters.
            GUI.enabled = _ctrl.CanSkipToNextMap;
            if (GUI.Button(new Rect(bx, by, bw, 34), "Skip to Next Battle (N)", _btn))
            {
                GUI.enabled = true;
                _ctrl.SkipToNextMap();
                return; // the scene is being rebuilt under us -- stop drawing this frame
            }
            GUI.enabled = true;
            by += 40;

            if (GUI.Button(new Rect(bx, by, bw, 34), "Restart Whole Battle", _btn))
            {
                if (_ctrl.Outcome == BattleOutcome.InProgress) _confirmRestart = true;
                else _ctrl.Restart();
            }
            by += 40;

            if (GUI.Button(new Rect(bx, by, bw, 34), "Quit", _btn)) Application.Quit();
        }

        /// <summary>Compact multi-line stat readout shared by DrawUnitStatsPanel (one
        /// unit, opened by tapping its name in the roster) and DrawAllUnitsStatsPanel (M16
        /// -- every unit at once, from the pause menu). Advances `y` past what it drew.</summary>
        void DrawStatLines(float x, ref float y, float width, BattleUnit unit)
        {
            var s = unit.Stats;
            GUI.Label(new Rect(x, y, width, 20),
                $"{unit.Definition.classType}  --  {unit.Definition.element}", _logEntry);
            y += 20;
            GUI.Label(new Rect(x, y, width, 20),
                $"HP {unit.CurrentHp}/{s.hp}   MP {unit.CurrentMp}/{unit.MaxMp}   ULT {unit.CurrentUltimateCharge}/{BattleUnit.MaxUltimateCharge}",
                _logEntry);
            y += 20;
            GUI.Label(new Rect(x, y, width, 20),
                $"ATK {s.attack}   DEF {s.defense}   MAG {s.magic}   RES {s.resistance}   SPD {s.speed}", _logEntry);
            y += 20;
            GUI.Label(new Rect(x, y, width, 20),
                $"Crit {s.critRate * 100f:0}%   CritDmg {(s.critDamage > 0f ? s.critDamage : DamageCalculator.DefaultCritDamage):0.0}x"
                + $"   Acc {(s.accuracy > 0f ? s.accuracy : 1f) * 100f:0}%   Eva {s.evasion * 100f:0}%", _logEntry);
            y += 20;
            if (unit.StatusEffects.Count > 0)
            {
                GUI.Label(new Rect(x, y, width, 20), string.Join(" ", unit.StatusEffects.Select(StatusTag)), _logEntry);
                y += 20;
            }
        }

        /// <summary>Single-unit stats popup (M16) -- opened by tapping a unit's name in
        /// the roster (BattleHud.DrawUnitBars), toggled shut the same way.</summary>
        void DrawUnitStatsPanel(BattleUnit unit, int w, int h)
        {
            const float panelW = 320f;
            // DrawStatLines always draws 4 fixed lines plus 1 more only when there are
            // active status effects -- computed here, not measured by actually calling
            // it, since IMGUI draws immediately and a real "dry run" would double-render.
            float panelH = 50f + 4 * 20f + (unit.StatusEffects.Count > 0 ? 20f : 0f) + 34f;

            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 8, panel.y + 6, panel.width - 60, 26), unit.Definition.displayName, _title);
            if (GUI.Button(new Rect(panel.x + panel.width - 44, panel.y + 6, 34, 26), "X", _smallBtn)) _inspectedUnit = null;

            float y = panel.y + 38;
            DrawStatLines(panel.x + 8, ref y, panel.width - 16, unit);

            if (GUI.Button(new Rect(panel.x + 8, y + 4, panel.width - 16, 26), "Max Ultimate (Test)", _smallBtn))
                _ctrl.DebugMaxUltimateCharge(unit);
        }

        /// <summary>Every unit at once (M16), from the pause menu's "Unit Stats" button --
        /// per the project owner's spec, an alternative to inspecting one unit at a time.</summary>
        void DrawAllUnitsStatsPanel(int w, int h)
        {
            const float panelW = 420f;
            float panelH = Mathf.Min(h - 80, 480f);
            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 8, panel.y + 6, panel.width - 100, 26), "Unit Stats", _title);
            if (GUI.Button(new Rect(panel.x + panel.width - 90, panel.y + 8, 80, 26), "Back", _smallBtn)) _showStats = false;

            var viewRect = new Rect(panel.x + 8, panel.y + 40, panel.width - 16, panel.height - 48);
            var allUnits = _ctrl.World.PlayerUnits.Concat(_ctrl.World.EnemyUnits).ToList();
            // 24 (name) + up to 5*20 (DrawStatLines' 4 fixed lines + 1 conditional status
            // line) + a "Max Ultimate" test button + padding -- generous rather than
            // exact, a little dead space per row beats clipping the status line.
            const float rowH = 168f;
            var contentRect = new Rect(0, 0, viewRect.width - 20, allUnits.Count * rowH);

            _statsScroll = GUI.BeginScrollView(viewRect, _statsScroll, contentRect);
            float rowY = 0f;
            foreach (var unit in allUnits)
            {
                GUI.Box(new Rect(0, rowY, contentRect.width, rowH - 8), GUIContent.none);
                GUI.Label(new Rect(6, rowY + 2, contentRect.width - 12, 20),
                    $"{unit.Definition.displayName} ({unit.Faction})", _actorName);
                float statsY = rowY + 24;
                DrawStatLines(6, ref statsY, contentRect.width - 12, unit);
                if (GUI.Button(new Rect(6, statsY + 4, contentRect.width - 12, 24), "Max Ultimate (Test)", _smallBtn))
                    _ctrl.DebugMaxUltimateCharge(unit);
                rowY += rowH;
            }
            GUI.EndScrollView();
        }

        void DrawConfirmRestartPanel(int w, int h)
        {
            var panel = new Rect(w / 2f - 190, h / 2f - 75, 380, 150);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 16, panel.y + 14, panel.width - 32, 50),
                "Restart the whole battle? This battle's progress can't be recovered afterward (undo history resets too).",
                _body);
            if (GUI.Button(new Rect(panel.x + 20, panel.y + 96, 160, 36), "Cancel", _btn)) _confirmRestart = false;
            if (GUI.Button(new Rect(panel.x + 200, panel.y + 96, 160, 36), "Restart", _btn))
            {
                _confirmRestart = false;
                _ctrl.Restart();
            }
        }

        void DrawSettingsPanel(int w, int h)
        {
            var settings = _ctrl.Settings;
            const float panelW = 400f, panelH = 520f;
            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 16, panel.y + 10, panel.width - 32, 26), "Settings", _title);

            float x = panel.x + 16, cw = panel.width - 32, y = panel.y + 48;
            GUI.Label(new Rect(x, y, cw, 20), "Battle speed", _body);
            y += 24;
            var speeds = BattleSettings.SpeedOptions;
            float speedBtnW = (cw - (speeds.Length - 1) * 8) / speeds.Length;
            for (int i = 0; i < speeds.Length; i++)
            {
                var r = new Rect(x + i * (speedBtnW + 8), y, speedBtnW, 32);
                bool active = Mathf.Approximately(settings.SpeedMultiplier, speeds[i]);
                var prevBg = GUI.backgroundColor;
                if (active) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);
                if (GUI.Button(r, $"{speeds[i]:0.#}x", _btn)) _ctrl.SetSpeedMultiplier(speeds[i]);
                GUI.backgroundColor = prevBg;
            }
            y += 44;

            settings.ShowDamageNumbers = GUI.Toggle(new Rect(x, y, cw, 24), settings.ShowDamageNumbers, " Show damage numbers", _toggle);
            y += 30;
            settings.LogOpenByDefault = GUI.Toggle(new Rect(x, y, cw, 24), settings.LogOpenByDefault, " Open turn log by default", _toggle);
            y += 30;
            settings.AutoModeDefault = GUI.Toggle(new Rect(x, y, cw, 24), settings.AutoModeDefault, " Start battles in auto mode", _toggle);
            y += 38;

            GUI.Label(new Rect(x, y, cw, 20), $"Master volume -- {Mathf.RoundToInt(settings.MasterVolume * 100)}%", _body);
            y += 24;
            float newVolume = GUI.HorizontalSlider(new Rect(x, y, cw, 20), settings.MasterVolume, 0f, 1f);
            if (!Mathf.Approximately(newVolume, settings.MasterVolume))
            {
                settings.MasterVolume = newVolume;
                AudioListener.volume = newVolume;
            }
            y += 34;

            GUI.Label(new Rect(x, y, cw, 20), "Dev tuning -- speeds through battles, not real balance", _body);
            y += 22;
            GUI.Label(new Rect(x, y, cw, 20), $"Damage dealt -- {settings.DamageDealtMultiplier:0.0}x", _body);
            y += 22;
            settings.DamageDealtMultiplier = GUI.HorizontalSlider(new Rect(x, y, cw, 20), settings.DamageDealtMultiplier, 0.25f, 5f);
            y += 30;
            GUI.Label(new Rect(x, y, cw, 20), $"Damage received -- {settings.DamageReceivedMultiplier:0.0}x", _body);
            y += 22;
            settings.DamageReceivedMultiplier = GUI.HorizontalSlider(new Rect(x, y, cw, 20), settings.DamageReceivedMultiplier, 0f, 2f);
            y += 30;
            GUI.Label(new Rect(x, y, cw, 20), $"MP usage -- {settings.MpCostMultiplier:0.0}x (0x = free Skill Moves)", _body);
            y += 22;
            settings.MpCostMultiplier = GUI.HorizontalSlider(new Rect(x, y, cw, 20), settings.MpCostMultiplier, 0f, 2f);
            y += 34;

            if (GUI.Button(new Rect(x, y, cw, 36), "Save & Back", _btn))
            {
                settings.Save();
                _showSettings = false;
            }
        }
    }
}
