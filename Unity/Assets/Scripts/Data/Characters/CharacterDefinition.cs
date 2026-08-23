using System.Collections.Generic;
using UnityEngine;

namespace Game.Data
{
    /// <summary>The authored template for a unit. Rolled values live on CharacterInstance.</summary>
    [CreateAssetMenu(menuName = "Game/Character/Character", fileName = "Char_")]
    public class CharacterDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string characterId;
        public string displayName;
        public ClassType classType;
        public ElementType element;
        public Age age;
        [TextArea] public string bio;

        [Header("Base stats (before tier multiplier and variance)")]
        public StatBlock baseStats;

        [Header("Movement - fixed, never rolled")]
        [Min(1)] public int movePoints = 4;

        [Tooltip("Max climbable height difference per step. Exceeding it makes the edge ILLEGAL, not expensive.")]
        [Min(0)] public int jump = 1;

        [Tooltip("MP cost to change lane. Default 2. Skirmisher archetypes lower this.")]
        [Min(1)] public int costLateral = 2;

        [Tooltip("MP cost to move along a lane.")]
        [Min(1)] public int costForward = 1;

        [Tooltip("Extra MP per level of elevation change.")]
        [Min(0)] public int costPerHeightLevel = 1;

        [Header("Resources")]
        [Tooltip("Mana/skill-charge pool for future special skills 1-3. Not yet spent by "
            + "anything -- the battle HUD renders it so the resource is visible while that "
            + "system is built out.")]
        [Min(0)] public int maxMp = 100;

        [Header("Skills - 1 fixed, 2 rolled")]
        [Tooltip("The free 'BA' (Basic Attack) action -- always available, no mana cost.")]
        public SkillDefinition standardSkill;

        [Tooltip("The 'SM' (Skill Move) list -- mana-cost actions beyond the basic attack, "
            + "shown by tapping SM in manual mode. Up to 3 slots for special skills 1-3; "
            + "archetype design intent: frontline gets defensive skills, healers get "
            + "support skills (this is where Heal lives), ranged gets sniping/AoE skills.")]
        public List<SkillDefinition> skillMoves = new();

        [Tooltip("Fires once BattleUnit.IsUltimateReady (M16) -- the gauge charges from "
            + "acting and a small per-turn trickle (BattleController), fully draining on "
            + "use. Design intent: match this unit's own element and classType (a Fire "
            + "Warrior's ultimate is a Fire-elemental Warrior-flavoured move), unlike "
            + "skillMoves which don't need to match either. Null until content authors it.")]
        public SkillDefinition ultimateSkill;

        [Tooltip("Leave empty to roll from the global pool matching classType.")]
        public List<SkillDefinition> classSkillPool = new();

        [Tooltip("Leave empty to roll from the global pool matching element.")]
        public List<SkillDefinition> elementSkillPool = new();

        [Header("Growth")]
        [Tooltip("Stat gain per level as a fraction of base.")]
        public float growthPerLevel = 0.06f;

        [Header("Presentation")]
        public ClipSet clips;
        public Sprite portrait;
        public Sprite pixelSprite32;

        [Tooltip("HD side-view battle art (arbitrary resolution/aspect, Bilinear-filtered) -- "
            + "distinct from pixelSprite32, which stays reserved for a future pixel/strategic "
            + "view per FOUNDATION.md's multi-view design. Falls back to pixelSprite32 if null.")]
        public Sprite battleSprite;

        [Tooltip("Leave empty to use a 3x nearest-neighbour upscale of pixelSprite32.")]
        public Sprite pixelSprite96;

        [Header("Dialogue")]
        [TextArea(4, 12)]
        [Tooltip("Static personality block injected into the LLM system prompt.")]
        public string personaPrompt;
    }
}
