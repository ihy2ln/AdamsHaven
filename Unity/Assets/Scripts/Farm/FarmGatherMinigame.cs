using System;
using UnityEngine;

namespace Game.Farm
{
    public enum FarmGatherMode { PrecisionTap, Swipe }

    public readonly struct FarmGatherOutcome
    {
        public bool Succeeded { get; }
        public FarmGatherMode Mode { get; }
        public float Score01 { get; }
        public int BonusMaterials { get; }

        public FarmGatherOutcome(bool succeeded, FarmGatherMode mode, float score01)
        {
            Succeeded = succeeded;
            Mode = mode;
            Score01 = Mathf.Clamp01(score01);
            BonusMaterials = succeeded ? FarmGatherRules.BonusForScore(Score01) : 0;
        }
    }

    /// <summary>Pure progression and reward rules shared by UI and tests.</summary>
    public static class FarmGatherRules
    {
        public const int SwipeUnlockLevel = 3;

        public static FarmGatherMode ModeForLevel(int farmLevel)
            => farmLevel >= SwipeUnlockLevel ? FarmGatherMode.Swipe : FarmGatherMode.PrecisionTap;

        public static int BonusForScore(float score01)
        {
            if (score01 >= 0.90f) return 3;
            if (score01 >= 0.65f) return 2;
            if (score01 >= 0.40f) return 1;
            return 0;
        }
    }

    /// <summary>
    /// Short, Farm-only gathering challenge. Levels 1-2 use three precision
    /// taps; level 3 unlocks pointer/touch swipes across targets. Completing the
    /// challenge authorizes the pending harvest or obstacle action and awards
    /// extra materials according to performance.
    /// </summary>
    public sealed class FarmGatherMinigame : MonoBehaviour
    {
        const int TapRounds = 3;
        const float TapDuration = 7f;
        const float SwipeDuration = 6f;
        const float SwipeRadius = 34f;

        static readonly float[] TapTargets = { 0.30f, 0.72f, 0.50f };
        static readonly Vector2[] SwipeTargets =
        {
            new Vector2(0.22f, 0.30f),
            new Vector2(0.50f, 0.22f),
            new Vector2(0.77f, 0.36f),
            new Vector2(0.34f, 0.70f),
            new Vector2(0.68f, 0.72f)
        };

        readonly bool[] _sliced = new bool[SwipeTargets.Length];
        Action<FarmGatherOutcome> _onComplete;
        FarmGatherMode _mode;
        string _title = "Gather";
        float _clock;
        float _timeRemaining;
        float _score;
        int _round;
        int _successes;
        int _slicedCount;
        int _beginFrame;
        bool _trackingSwipe;
        Vector2 _lastPointer;
        GUIStyle _titleStyle;
        GUIStyle _bodyStyle;
        GUIStyle _centerStyle;
        GUIStyle _targetStyle;

        public bool IsActive { get; private set; }
        public FarmGatherMode Mode => _mode;
        public string ModeLabel => _mode == FarmGatherMode.Swipe ? "Swipe gathering" : "Precision gathering";

        public void Begin(string title, int farmLevel, Action<FarmGatherOutcome> onComplete)
        {
            if (IsActive || onComplete == null) return;
            _title = string.IsNullOrEmpty(title) ? "Gather" : title;
            _mode = FarmGatherRules.ModeForLevel(farmLevel);
            _onComplete = onComplete;
            _clock = 0f;
            _score = 0f;
            _round = 0;
            _successes = 0;
            _slicedCount = 0;
            _trackingSwipe = false;
            _beginFrame = Time.frameCount;
            Array.Clear(_sliced, 0, _sliced.Length);
            _timeRemaining = _mode == FarmGatherMode.Swipe ? SwipeDuration : TapDuration;
            IsActive = true;
        }

        void Update()
        {
            if (!IsActive || Time.timeScale <= 0f) return;
            // Do not count the click/key that opened the challenge as its first
            // action; controller/component Update order is not guaranteed.
            if (Time.frameCount == _beginFrame) return;
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Finish(false, 0f);
                return;
            }

            _timeRemaining -= Time.unscaledDeltaTime;
            if (_timeRemaining <= 0f)
            {
                if (_mode == FarmGatherMode.Swipe)
                    Finish(_slicedCount >= 3, (float)_slicedCount / SwipeTargets.Length);
                else
                    Finish(false, _score / TapRounds);
                return;
            }

            if (_mode == FarmGatherMode.PrecisionTap) UpdateTap();
            else UpdateSwipe();
        }

        void UpdateTap()
        {
            _clock += Time.unscaledDeltaTime * 1.35f;
            if (!Input.GetMouseButtonDown(0) &&
                !Input.GetKeyDown(KeyCode.Space) &&
                !Input.GetKeyDown(KeyCode.Return) &&
                !Input.GetKeyDown(KeyCode.E)) return;

            var marker = Mathf.PingPong(_clock, 1f);
            var accuracy = Mathf.Clamp01(1f - Mathf.Abs(marker - TapTargets[_round]) / 0.32f);
            _score += accuracy;
            if (accuracy >= 0.42f) _successes++;
            _round++;
            _clock = 0f;
            if (_round >= TapRounds) Finish(_successes >= 2, _score / TapRounds);
        }

        void UpdateSwipe()
        {
            Vector2 pointer;
            var pressed = TryGetPointerDown(out pointer);
            if (pressed)
            {
                _trackingSwipe = true;
                _lastPointer = pointer;
                CheckSwipeSegment(pointer, pointer);
            }

            if (_trackingSwipe && TryGetPointerHeld(out pointer))
            {
                CheckSwipeSegment(_lastPointer, pointer);
                _lastPointer = pointer;
            }

            if (PointerReleased()) _trackingSwipe = false;
            if (_slicedCount == SwipeTargets.Length) Finish(true, 1f);
        }

        void CheckSwipeSegment(Vector2 from, Vector2 to)
        {
            var area = PlayArea();
            for (var i = 0; i < SwipeTargets.Length; i++)
            {
                if (_sliced[i]) continue;
                var center = new Vector2(
                    area.x + area.width * SwipeTargets[i].x,
                    area.y + area.height * SwipeTargets[i].y);
                if (DistanceToSegment(center, from, to) > SwipeRadius) continue;
                _sliced[i] = true;
                _slicedCount++;
            }
        }

        void Finish(bool succeeded, float score01)
        {
            if (!IsActive) return;
            IsActive = false;
            _trackingSwipe = false;
            var callback = _onComplete;
            _onComplete = null;
            callback?.Invoke(new FarmGatherOutcome(succeeded, _mode, score01));
        }

        void OnGUI()
        {
            if (!IsActive) return;
            EnsureStyles();
            GUI.depth = -100;
            var panel = PanelRect();
            GUI.Box(panel, GUIContent.none);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, 34f), _title, _titleStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 50f, panel.width - 40f, 24f),
                _mode == FarmGatherMode.Swipe
                    ? "Hold and swipe through at least 3 targets. More cuts earn more materials."
                    : "Press E, Space, Enter, or tap when the marker crosses the gold zone.", _bodyStyle);
            GUI.Label(new Rect(panel.x + panel.width - 120f, panel.y + 16f, 96f, 24f),
                Mathf.CeilToInt(_timeRemaining) + "s", _bodyStyle);

            if (_mode == FarmGatherMode.Swipe) DrawSwipeGame();
            else DrawTapGame();

            if (GUI.Button(new Rect(panel.x + 20f, panel.yMax - 42f, 92f, 26f), "CANCEL"))
                Finish(false, 0f);
        }

        void DrawTapGame()
        {
            var area = PlayArea();
            var bar = new Rect(area.x + 20f, area.center.y - 22f, area.width - 40f, 44f);
            GUI.Box(bar, GUIContent.none);
            var targetX = bar.x + TapTargets[Mathf.Min(_round, TapRounds - 1)] * bar.width;
            GUI.Box(new Rect(targetX - 24f, bar.y + 3f, 48f, bar.height - 6f), "GOLD");
            var markerX = bar.x + Mathf.PingPong(_clock, 1f) * bar.width;
            GUI.Box(new Rect(markerX - 5f, bar.y - 8f, 10f, bar.height + 16f), GUIContent.none);
            GUI.Label(new Rect(area.x, bar.yMax + 18f, area.width, 28f),
                "Hit " + (_round + 1) + "/" + TapRounds + "  ·  Good hits " + _successes, _centerStyle);
        }

        void DrawSwipeGame()
        {
            var area = PlayArea();
            GUI.Box(area, GUIContent.none);
            for (var i = 0; i < SwipeTargets.Length; i++)
            {
                var center = new Vector2(
                    area.x + area.width * SwipeTargets[i].x,
                    area.y + area.height * SwipeTargets[i].y);
                var size = SwipeRadius * 2f;
                var rect = new Rect(center.x - SwipeRadius, center.y - SwipeRadius, size, size);
                GUI.Label(rect, _sliced[i] ? "CUT" : "SWIPE", _targetStyle);
            }
            GUI.Label(new Rect(area.x, area.yMax + 8f, area.width, 26f),
                "Targets " + _slicedCount + "/" + SwipeTargets.Length, _centerStyle);
        }

        void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.76f, 0.30f) }
            };
            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                wordWrap = true,
                normal = { textColor = Color.white }
            };
            _centerStyle = new GUIStyle(_bodyStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            _targetStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.96f, 0.76f, 0.30f) }
            };
        }

        static Rect PanelRect()
        {
            var width = Mathf.Min(720f, Screen.width - 32f);
            var height = Mathf.Min(440f, Screen.height - 32f);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        static Rect PlayArea()
        {
            var panel = PanelRect();
            return new Rect(panel.x + 32f, panel.y + 88f, panel.width - 64f, panel.height - 156f);
        }

        static bool TryGetPointerDown(out Vector2 position)
        {
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
            {
                position = ToGui(Input.GetTouch(0).position);
                return true;
            }
            position = ToGui(Input.mousePosition);
            return Input.GetMouseButtonDown(0);
        }

        static bool TryGetPointerHeld(out Vector2 position)
        {
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                position = ToGui(touch.position);
                return touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary;
            }
            position = ToGui(Input.mousePosition);
            return Input.GetMouseButton(0);
        }

        static bool PointerReleased()
        {
            if (Input.touchCount > 0)
            {
                var phase = Input.GetTouch(0).phase;
                return phase == TouchPhase.Ended || phase == TouchPhase.Canceled;
            }
            return Input.GetMouseButtonUp(0);
        }

        static Vector2 ToGui(Vector2 screenPosition)
            => new Vector2(screenPosition.x, Screen.height - screenPosition.y);

        static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            var segment = to - from;
            var lengthSqr = segment.sqrMagnitude;
            if (lengthSqr <= 0.0001f) return Vector2.Distance(point, from);
            var t = Mathf.Clamp01(Vector2.Dot(point - from, segment) / lengthSqr);
            return Vector2.Distance(point, from + segment * t);
        }
    }
}
