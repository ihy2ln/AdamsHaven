using System.Linq;
using UnityEngine;

namespace Game.Town
{
    /// <summary>The build-choice/construction-progress panel for a single plot --
    /// IMGUI modal, matching Battle's `CampScreen` convention rather than a Canvas.
    /// `TownController` owns opening/closing it (E near a plot); movement pauses while
    /// it's open the same way it already pauses for `HavenNavigation`.</summary>
    public class TownBuildMenu : MonoBehaviour
    {
        TownEconomy _economy;
        TownBuilding _plot;
        string _notice;

        GUIStyle _title, _body, _btn, _cost;

        public bool IsOpen => _plot != null;

        public void Init(TownEconomy economy) => _economy = economy;

        public void OpenFor(TownBuilding plot)
        {
            _plot = plot;
            _notice = null;
        }

        public void Close()
        {
            _plot = null;
            _notice = null;
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true, normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6) };
            _cost = new GUIStyle(_body) { normal = { textColor = new Color(0.85f, 0.8f, 0.5f) } };
        }

        void Update()
        {
            if (_plot == null) return;
            _plot.CompleteIfDue();
        }

        void OnGUI()
        {
            if (_plot == null) return;
            EnsureStyles();

            var w = Mathf.Min(520, Screen.width - 40);
            var h = Mathf.Min(520, Screen.height - 40);
            var rect = new Rect(Screen.width / 2f - w / 2f, Screen.height / 2f - h / 2f, w, h);
            GUI.Box(rect, GUIContent.none);

            float x = rect.x + 20f, y = rect.y + 16f, cw = rect.width - 40f;

            GUI.Label(new Rect(x, y, cw, 28), _plot.DisplayName, _title);
            y += 30f;
            GUI.Label(new Rect(x, y, cw, 20), $"Sector: {_plot.District}", _body);
            y += 22f;
            GUI.Label(new Rect(x, y, cw, 20), "Wallet: " + _economy.Describe(), _cost);
            y += 26f;

            switch (_plot.State)
            {
                case TownPlotState.Empty:
                    y = DrawChoices(x, y, cw);
                    break;
                case TownPlotState.UnderConstruction:
                    y = DrawConstruction(x, y, cw);
                    break;
                case TownPlotState.Built:
                    GUI.Label(new Rect(x, y, cw, 40), _plot.Definition.VerbDescription, _body);
                    y += 44f;
                    break;
            }

            if (!string.IsNullOrEmpty(_notice))
            {
                GUI.Label(new Rect(x, y, cw, 36), _notice, _cost);
                y += 40f;
            }

            if (GUI.Button(new Rect(x, rect.y + rect.height - 40f, 100f, 28f), "Close", _btn))
                Close();

            // Test-only top-up -- there's no real Battle -> Town material pipe yet
            // (see TownEconomy's own doc comment), so this keeps the loop testable.
            if (GUI.Button(new Rect(rect.x + rect.width - 160f, rect.y + rect.height - 40f, 140f, 28f), "Test: +stock", _btn))
                _economy.GrantTestStock();
        }

        float DrawChoices(float x, float y, float cw)
        {
            var options = TownBuildingCatalog.ForDistrict(_plot.District).ToList();
            foreach (var def in options)
            {
                var afford = _economy.CanAfford(def.MoneyCost, def.MaterialCost);
                var label = $"{def.DisplayName}  --  ${def.MoneyCost}, " +
                    string.Join(", ", def.MaterialCost.Select(m => $"{m.amount} {m.kind}")) +
                    $"  ({def.BuildSeconds:0}s)";

                GUI.enabled = afford;
                if (GUI.Button(new Rect(x, y, cw, 34), label, _btn))
                {
                    if (_economy.TrySpend(def.MoneyCost, def.MaterialCost))
                    {
                        _plot.StartConstruction(def);
                        _notice = $"Started building {def.DisplayName}.";
                    }
                }
                GUI.enabled = true;
                y += 38f;
            }
            return y;
        }

        float DrawConstruction(float x, float y, float cw)
        {
            var def = _plot.Definition;
            GUI.Label(new Rect(x, y, cw, 24), $"Building {def.DisplayName} -- {_plot.RemainingSeconds:0}s remaining.", _body);
            y += 30f;

            var (money, kind, amount) = _plot.RushCost();
            var canRush = _economy.CanAfford(money, new[] { (kind, amount) });
            GUI.enabled = canRush;
            if (GUI.Button(new Rect(x, y, cw, 34), $"Rush -- ${money} + {amount} {kind}", _btn))
            {
                if (_economy.TrySpend(money, new[] { (kind, amount) }))
                {
                    _plot.ConstructionEndTime = Time.time;
                    _plot.CompleteIfDue();
                    _notice = $"Rushed {def.DisplayName} to completion.";
                }
            }
            GUI.enabled = true;
            y += 38f;
            return y;
        }
    }
}
