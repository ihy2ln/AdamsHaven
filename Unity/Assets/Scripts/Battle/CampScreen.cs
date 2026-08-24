using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// The between-battles camp (M20) -- where the party lands after escaping or quitting
    /// a fight, and the first thing in this project that exists outside a battle.
    ///
    /// Four things, per the project owner's spec: what the next battle is, resting, a
    /// stat overview, and leaving the dungeon for home. IMGUI like the rest of the
    /// slice's UI, and booted by BattleBootstrap into the same scene rather than being a
    /// scene of its own -- BootMap already tears the scene down and rebuilds it, so
    /// "camp" is just a different thing to build.
    ///
    /// This is deliberately a screen, not a system. There's no camp economy, no time
    /// passing, no encounter table -- Rest is the only thing that spends anything, and it
    /// spends the materials M20 just started dropping. Everything here is the shape a
    /// real camp would have, sized to what the slice can actually support today.
    /// </summary>
    public class CampScreen : MonoBehaviour
    {
        /// <summary>Materials one unit's rest costs (M22 -- per-unit, was a flat
        /// whole-party cost in M20). Deliberately payable from a single successful
        /// escape's haul -- resting has to be reachable, or the escape-with-your-mats
        /// decision has nothing to buy. Not a tuned number; the project owner's own
        /// direction is that none of M20's numbers are final until the farm/town economy
        /// exists to weigh them against.</summary>
        public const int RestMaterialCostPerUnit = 1;

        BattleRewards _banked;
        List<BattleUnit> _party;
        List<BattleUnit> _bench;
        BattleInventory _inventory;
        int _mapIndex;
        string _arrivalLine;

        bool _showStats;
        bool _showRest;
        readonly HashSet<BattleUnit> _restSelection = new();
        string _notice;

        /// <summary>Raised when the player chooses to take on the next battle. Carries
        /// nothing -- BattleBootstrap already holds everything the next map needs.</summary>
        public event Action OnContinueRequested;

        /// <summary>Raised by "Leave dungeon". No home scene is wired to the battle slice
        /// yet (Farm.unity exists but nothing connects the two -- see PROJECT-README's
        /// gaps), so BattleBootstrap currently treats this as ending the run.</summary>
        public event Action OnLeaveDungeonRequested;

        GUIStyle _title, _body, _btn, _heading;

        public void Init(int mapIndex, List<BattleUnit> party, List<BattleUnit> bench,
            BattleInventory inventory, BattleRewards banked, string arrivalLine)
        {
            _mapIndex = mapIndex;
            _party = party ?? new List<BattleUnit>();
            _bench = bench ?? new List<BattleUnit>();
            _inventory = inventory;
            _banked = banked ?? new BattleRewards();
            _arrivalLine = arrivalLine;
        }

        void BuildStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
            _heading = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.85f, 0.8f, 0.6f) } };
            _body = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, normal = { textColor = new Color(0.88f, 0.88f, 0.9f) } };
            _btn = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        }

        void OnGUI()
        {
            BuildStyles();
            int w = Screen.width, h = Screen.height;

            GUI.color = new Color(0.05f, 0.06f, 0.09f, 1f);
            GUI.DrawTexture(new Rect(0, 0, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            if (_showStats) { DrawStatsPanel(w, h); return; }
            if (_showRest) { DrawRestPanel(w, h); return; }

            const float panelW = 460f, panelH = 470f;
            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            float x = panel.x + 20f, cw = panel.width - 40f, y = panel.y + 14f;

            GUI.Label(new Rect(x, y, cw, 30), "Camp", _title);
            y += 34f;

            if (!string.IsNullOrEmpty(_arrivalLine))
            {
                GUI.Label(new Rect(x, y, cw, 34), _arrivalLine, _body);
                y += 36f;
            }

            GUI.Label(new Rect(x, y, cw, 20), "Carrying", _heading);
            y += 20f;
            GUI.Label(new Rect(x, y, cw, 20), _banked.Describe(), _body);
            y += 16f;
            GUI.Label(new Rect(x, y, cw, 20), PotionSummary(), _body);
            y += 26f;

            GUI.Label(new Rect(x, y, cw, 20), "Party", _heading);
            y += 20f;
            GUI.Label(new Rect(x, y, cw, 20), PartySummary(), _body);
            y += 26f;

            GUI.Label(new Rect(x, y, cw, 20), "Next", _heading);
            y += 20f;
            GUI.Label(new Rect(x, y, cw, 34), NextBattleLine(), _body);
            y += 38f;

            if (!string.IsNullOrEmpty(_notice))
            {
                GUI.Label(new Rect(x, y, cw, 20), _notice, _body);
            }
            y += 24f;

            bool canRest = CanOpenRest(out string restLabel);
            GUI.enabled = canRest;
            if (GUI.Button(new Rect(x, y, cw, 34), restLabel, _btn)) OpenRest();
            GUI.enabled = true;
            y += 40f;

            if (GUI.Button(new Rect(x, y, cw, 34), "Stat Overview", _btn)) _showStats = true;
            y += 40f;

            bool hasNext = _mapIndex < BattleWorld.MapCount;
            GUI.enabled = hasNext;
            if (GUI.Button(new Rect(x, y, cw, 34), hasNext ? NextBattleButtonLabel() : "No battles left", _btn))
                OnContinueRequested?.Invoke();
            GUI.enabled = true;
            y += 40f;

            if (GUI.Button(new Rect(x, y, cw, 34), "Leave dungeon (go home)", _btn))
                OnLeaveDungeonRequested?.Invoke();
        }

        /// <summary>The potion slots carry between battles the same way the reward
        /// ledger does, so camp is the natural place to see what's left of them.</summary>
        string PotionSummary()
        {
            if (_inventory == null) return "no potions";
            var slots = new[] { PotionKind.Hp, PotionKind.Mp, PotionKind.Multi };
            return "Potions: " + string.Join("  ", slots.Select(k => $"{k} x{_inventory.Slot(k).Count}"));
        }

        string PartySummary()
        {
            if (_party.Count == 0) return "nobody standing";
            return string.Join("   ", _party.OrderBy(u => u.Column)
                .Select(u => $"{u.Definition.displayName} {u.CurrentHp}/{u.Stats.hp}"));
        }

        /// <summary>Which fight is next and who's in it. Reads the map's own enemy
        /// placements rather than a hardcoded list, so this stays correct if the roster
        /// or the map count changes.</summary>
        string NextBattleLine()
        {
            if (_mapIndex >= BattleWorld.MapCount) return "The dungeon is behind you. Nothing ahead but the road home.";

            var map = Resources.Load<MapDefinition>($"Battle/Maps/Map_BattleSlice{_mapIndex + 1}");
            if (map == null) return $"Battle {_mapIndex + 1} of {BattleWorld.MapCount}.";

            var names = map.enemies
                .Where(e => e.character != null)
                .Select(e => e.character.displayName)
                .ToList();

            string who = names.Count > 0 ? string.Join(", ", names) : "unknown";
            return $"Battle {_mapIndex + 1} of {BattleWorld.MapCount} -- {who}.";
        }

        string NextBattleButtonLabel() => $"Take on battle {_mapIndex + 1}";

        /// <summary>Whether the Rest panel is even worth opening -- someone has to be
        /// hurt, and there has to be at least one material banked to spend on them.
        /// Doesn't check that the player can afford *everyone* -- resting is per-unit
        /// now (M22), so 1 material is enough to open it and rest just one.</summary>
        bool CanOpenRest(out string label)
        {
            bool anyHurt = _party.Concat(_bench).Any(NeedsRest);
            int mats = _banked.TotalMaterials;

            if (!anyHurt)
            {
                label = "Rest -- everyone is already fit";
                return false;
            }
            if (mats <= 0)
            {
                label = "Rest -- you have no materials to spend";
                return false;
            }
            label = $"Rest... ({mats} material{(mats == 1 ? "" : "s")} banked)";
            return true;
        }

        static bool NeedsRest(BattleUnit u) => u.CurrentHp < u.Stats.hp || u.CurrentMp < u.MaxMp
            || u.StatusEffects.Count > 0;

        void OpenRest()
        {
            _restSelection.Clear();
            _showRest = true;
        }

        /// <summary>Per-unit rest (M22): 1 material heals one unit to full HP/MP and
        /// clears its status effects. Replaces M20's flat whole-party cost -- the project
        /// owner's spec is that resting with fewer materials than the party needs should
        /// still be possible, just on whoever you pick, rather than an all-or-nothing
        /// purchase. The camp's own materials list is the wallet; there's no crafting
        /// system yet to make one kind meaningfully different from another, so spending
        /// draws from whichever kinds are available rather than requiring a specific one.</summary>
        void DrawRestPanel(int w, int h)
        {
            var candidates = _party.Concat(_bench).ToList();
            const float panelW = 480f;
            float panelH = Mathf.Min(h - 60f, 140f + candidates.Count * 30f);
            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            float x = panel.x + 16f, cw = panel.width - 32f, y = panel.y + 12f;
            GUI.Label(new Rect(x, y, cw, 28), "Rest", _title);
            y += 30f;
            GUI.Label(new Rect(x, y, cw, 18),
                $"{RestMaterialCostPerUnit} material per member -- choose who to spend it on.", _body);
            y += 24f;

            foreach (var unit in candidates)
            {
                bool needs = NeedsRest(unit);
                bool selected = _restSelection.Contains(unit);
                string status = needs
                    ? $"HP {unit.CurrentHp}/{unit.Stats.hp}  MP {unit.CurrentMp}/{unit.MaxMp}"
                        + (unit.StatusEffects.Count > 0 ? $"  ({unit.StatusEffects.Count} status)" : "")
                    : "already fit";

                GUI.enabled = needs;
                bool now = GUI.Toggle(new Rect(x, y, cw, 24), selected,
                    $"  {unit.Definition.displayName} -- {status}");
                GUI.enabled = true;

                if (needs && now != selected)
                {
                    if (now) _restSelection.Add(unit); else _restSelection.Remove(unit);
                }
                y += 26f;
            }

            y += 6f;
            int cost = _restSelection.Count;
            int available = _banked.TotalMaterials;
            GUI.Label(new Rect(x, y, cw, 18),
                cost == 0 ? "Select who to rest." : $"Cost: {cost} material{(cost == 1 ? "" : "s")} (you have {available}).",
                _body);
            y += 24f;

            bool canAfford = cost > 0 && cost <= available;
            GUI.enabled = canAfford;
            if (GUI.Button(new Rect(x, y, cw, 32), canAfford ? $"Rest {cost} member{(cost == 1 ? "" : "s")}" : "Rest", _btn))
                ConfirmRest();
            GUI.enabled = true;
            y += 38f;

            if (GUI.Button(new Rect(x, y, cw, 30), "Back", _btn))
            {
                _restSelection.Clear();
                _showRest = false;
            }
        }

        /// <summary>Spends 1 material per selected unit and restores exactly those
        /// units. Draws materials across kinds in order rather than requiring a specific
        /// one -- see the class doc on why BattleRewards treats its three kinds as
        /// interchangeable for now.</summary>
        void ConfirmRest()
        {
            int owed = _restSelection.Count;
            if (owed <= 0 || owed > _banked.TotalMaterials) return;

            foreach (var (kind, count) in _banked.Materials.ToList())
            {
                if (owed <= 0) break;
                int take = Mathf.Min(owed, count);
                _banked.Spend(kind, take);
                owed -= take;
            }

            var rested = _restSelection.ToList();
            foreach (var unit in rested)
            {
                unit.CurrentHp = unit.Stats.hp;
                unit.CurrentMp = unit.MaxMp;
                unit.StatusEffects.Clear();
            }

            _notice = $"Rested {rested.Count} member{(rested.Count == 1 ? "" : "s")} "
                + $"({rested.Count} material{(rested.Count == 1 ? "" : "s")} spent).";
            _restSelection.Clear();
            _showRest = false;
        }

        void DrawStatsPanel(int w, int h)
        {
            const float panelW = 520f;
            var all = _party.Concat(_bench).ToList();
            float panelH = Mathf.Min(h - 60f, 90f + all.Count * 46f);
            var panel = new Rect(w / 2f - panelW / 2f, h / 2f - panelH / 2f, panelW, panelH);
            GUI.Box(panel, GUIContent.none);

            float x = panel.x + 16f, cw = panel.width - 32f, y = panel.y + 12f;
            GUI.Label(new Rect(x, y, cw, 28), "Stat Overview", _title);
            y += 34f;

            foreach (var unit in all)
            {
                var s = unit.Stats;
                bool benched = _bench.Contains(unit);
                GUI.Label(new Rect(x, y, cw, 18),
                    $"{unit.Definition.displayName}{(benched ? "  (bench)" : "")}  --  "
                    + $"{unit.Definition.classType}, {unit.Definition.element}", _heading);
                y += 18f;
                GUI.Label(new Rect(x, y, cw, 18),
                    $"HP {unit.CurrentHp}/{s.hp}   MP {unit.CurrentMp}/{unit.MaxMp}   "
                    + $"ATK {s.attack}  DEF {s.defense}  MAG {s.magic}  RES {s.resistance}  SPD {s.speed}", _body);
                y += 16f;
                GUI.Label(new Rect(x, y, cw, 18),
                    $"Crit {s.critRate * 100f:0}% x{(s.critDamage > 0f ? s.critDamage : DamageCalculator.DefaultCritDamage):0.0}   "
                    + $"Acc {(s.accuracy > 0f ? s.accuracy : 1f) * 100f:0}%   Eva {s.evasion * 100f:0}%", _body);
                y += 22f;
            }

            if (GUI.Button(new Rect(panel.x + panel.width - 110f, panel.y + panel.height - 40f, 94f, 28f), "Back", _btn))
                _showStats = false;
        }
    }
}
