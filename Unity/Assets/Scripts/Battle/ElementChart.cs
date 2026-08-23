using System.Collections.Generic;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// Pure C#, no MonoBehaviour/scene dependency -- testable headlessly. Standard gacha
    /// 5-element cycle (Fire > Wind > Earth > Lightning > Water > Fire) plus a mutual
    /// Light/Dark rivalry, matching the roster of ElementType (M16). Neutral -- either
    /// side -- always returns 1x: it's the "no element" default every pre-M16 skill and
    /// character still carries, so nothing gets an accidental bonus/penalty until a
    /// Skill Move actually authors a real element (see SkillDefinition.element).
    /// Numbers are arbitrary (project convention: "arbitrary, not a tuned balance
    /// pass"), not derived from anywhere.
    /// </summary>
    public static class ElementChart
    {
        public const float WeaknessMultiplier = 1.5f;
        public const float ResistMultiplier = 1f / WeaknessMultiplier;

        static readonly HashSet<(ElementType attacker, ElementType target)> Beats = new()
        {
            (ElementType.Fire, ElementType.Wind),
            (ElementType.Wind, ElementType.Earth),
            (ElementType.Earth, ElementType.Lightning),
            (ElementType.Lightning, ElementType.Water),
            (ElementType.Water, ElementType.Fire),
            (ElementType.Light, ElementType.Dark),
            (ElementType.Dark, ElementType.Light),
        };

        /// <summary>WeaknessMultiplier if `attackerElement` beats `targetElement`,
        /// ResistMultiplier if the reverse is true, else 1x (includes either side being
        /// Neutral, or two of the same element).</summary>
        public static float GetMultiplier(ElementType attackerElement, ElementType targetElement)
        {
            if (attackerElement == ElementType.Neutral || targetElement == ElementType.Neutral) return 1f;
            if (Beats.Contains((attackerElement, targetElement))) return WeaknessMultiplier;
            if (Beats.Contains((targetElement, attackerElement))) return ResistMultiplier;
            return 1f;
        }
    }
}
