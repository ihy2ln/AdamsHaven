using UnityEngine;

namespace Game.Town
{
    public enum TownPlotState
    {
        Empty,
        UnderConstruction,
        Built,
    }

    /// <summary>A plot of land in Town -- M31 tagged it with a fixed intended building;
    /// M38 turns it into the actual construction state machine from the project owner's
    /// building-rules spec: Empty (choose what to build) -&gt; UnderConstruction (paid
    /// for, real-time countdown, rushable with more money+materials) -&gt; Built (verb
    /// unlocked, still a stub -- see `TownBuildingDefinition.VerbDescription`).
    /// `TownController` proximity-checks these to drive `TownHud`/`TownBuildMenu`.</summary>
    public class TownBuilding : MonoBehaviour
    {
        public TownDistrict District;
        public string DisplayName;
        public TownPlotState State = TownPlotState.Empty;
        public TownBuildingDefinition Definition;
        public float ConstructionEndTime;
        public Color EmptyColor;
        public Color ConstructionColor = new(0.8f, 0.7f, 0.2f, 0.55f);
        public Color BuiltColor = new(0.6f, 0.6f, 0.6f, 0.9f);

        Renderer _renderer;
        void Awake() => _renderer = GetComponent<Renderer>();

        public float RemainingSeconds => Mathf.Max(0f, ConstructionEndTime - Time.time);

        public string PromptLine => State switch
        {
            TownPlotState.Empty => $"{DisplayName} ({District}) -- press E to build.",
            TownPlotState.UnderConstruction => $"{Definition.DisplayName} -- building, {RemainingSeconds:0}s left (E to check/rush).",
            TownPlotState.Built => $"{Definition.DisplayName} -- {Definition.VerbDescription}",
            _ => DisplayName,
        };

        public void StartConstruction(TownBuildingDefinition definition)
        {
            Definition = definition;
            State = TownPlotState.UnderConstruction;
            ConstructionEndTime = Time.time + definition.BuildSeconds;
            ApplyStateColor();
        }

        public void CompleteIfDue()
        {
            if (State != TownPlotState.UnderConstruction || Time.time < ConstructionEndTime) return;
            State = TownPlotState.Built;
            ApplyStateColor();
        }

        /// <summary>Only a colour swap for now, not the real solid-building-plus-roof
        /// visual M34 built and M36 later removed -- reusing that fully (positioning a
        /// box + the matching roof crop where this plot sits) is a real follow-up, not
        /// done here so this milestone stays about the rules/loop, not visuals.</summary>
        void ApplyStateColor()
        {
            if (_renderer == null) _renderer = GetComponent<Renderer>();
            if (_renderer == null) return;
            var color = State switch
            {
                TownPlotState.UnderConstruction => ConstructionColor,
                TownPlotState.Built => BuiltColor,
                _ => EmptyColor,
            };
            _renderer.material.color = color;
        }

        /// <summary>Rush cost, per the project owner's spec ("use more money and mats
        /// to hasten the time") -- arbitrary formula, not tuned: money scales with
        /// seconds remaining, plus a small amount of whichever material this building
        /// needed most. Paying it completes construction immediately rather than
        /// partially shortening it -- simpler to reason about and to test.</summary>
        public (int money, TownMaterialKind materialKind, int materialAmount) RushCost()
        {
            var remaining = RemainingSeconds;
            var money = Mathf.CeilToInt(remaining * 3f);
            var material = Mathf.CeilToInt(remaining / 15f);
            return (money, Definition.PrimaryMaterial, material);
        }
    }
}
