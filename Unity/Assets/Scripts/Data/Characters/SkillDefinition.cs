using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// A skill. Units receive three at recruitment:
    ///   1 standard - fixed, defined on the character
    ///   1 class    - rolled from the class pool
    ///   1 element  - rolled from the element pool
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Character/Skill", fileName = "Skill_")]
    public class SkillDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string skillId;
        public string displayName;
        [TextArea] public string description;

        [Tooltip("Which roll pool this belongs to. Standard skills are assigned directly, not rolled.")]
        public SkillPool pool = SkillPool.Class;

        [Tooltip("Used when pool == Class.")]
        public ClassType classType;

        [Tooltip("Used when pool == Element.")]
        public ElementType element;

        [Header("Targeting")]
        public SkillPattern pattern;

        [Header("Cost and effect")]
        [Min(0)] public int mpCost;
        [Min(0)] public int cooldown;
        [Tooltip("Multiplier against the relevant offensive stat.")]
        public float power = 1f;
        public bool usesMagic;

        [Header("Elevation interaction")]
        [Tooltip("Ranged skills gain the downward bonus. Melee skills take the upward penalty.")]
        public bool isRanged;

        [Tooltip("True for heals/buffs: TargetResolver offers the caster's own faction instead of the opposing one.")]
        public bool targetsAllies;

        [Tooltip("Only meaningful when targetsAllies is true: restores the target's MP "
            + "(via power * caster magic, same formula as a heal) instead of HP. "
            + "BattleController.ResolveAction branches on this within the targetsAllies path.")]
        public bool restoresMana;

        [Header("Status effect (optional, M13)")]
        [Tooltip("None = doesn't inflict/grant anything. Applied to every unit in "
            + "hitTargets alongside whatever this skill's main effect is (damage/heal/"
            + "mana-restore) -- see BattleController.ResolveAction.")]
        public StatusEffectType inflictsStatus = StatusEffectType.None;

        [Tooltip("Flat HP/turn for Poison/Regen; fractional multiplier (0.2=20%) for "
            + "Attack/DefenseUp/Down; unused for Stun.")]
        public float statusMagnitude;

        [Min(0)] public int statusDuration;

        [Header("Presentation")]
        [Tooltip("Key into the caster's ClipSet -- the character's own body animation "
            + "(swing/cast/shoot), reused by every skill that shares this key rather than "
            + "needing a unique clip per skill. Skill identity belongs on `effect` below, "
            + "not here.")]
        public string clipKey;

        [Tooltip("Optional skill-specific impact flipbook, independent of the caster's "
            + "body clip above -- see SkillEffect's class doc. Null falls back to the "
            + "map's generic impact FX (BattleVisuals.PlayImpactFx).")]
        public SkillEffect effect;

        [Min(0f)] public float rollWeight = 1f;
    }
}
