using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// Pure C#, no MonoBehaviour/scene dependency -- testable headlessly.
    /// power * attack (or magic if usesMagic) vs defense/resistance, per the original
    /// vertical-slice brief. Ranged distance scaling and exact numbers are an open
    /// balance-pass item (FOUNDATION.md 1.4/11) -- RangedDistanceBonusPerColumn is a
    /// placeholder constant, not a tuned value.
    ///
    /// Crit/hit rolls (M16) deliberately don't happen in here -- ComputeDamage takes
    /// `isCrit` as a pre-rolled bool so this stays 100% deterministic and testable, the
    /// same reasoning M14's OffensiveSkillMoveChance already established: the actual
    /// UnityEngine.Random roll lives on BattleController (untested, verified by
    /// interactive play), while the pure math it feeds into stays here.
    /// </summary>
    public static class DamageCalculator
    {
        public const float RangedDistanceBonusPerColumn = 0.15f;

        /// <summary>Fallback crit multiplier for a unit whose StatBlock.critDamage was
        /// never authored (0, the type default) but somehow still rolled a crit --
        /// shouldn't happen in practice (critRate is also 0 by default, see StatBlock's
        /// doc), but a crit that deals 0 bonus damage would look like a silent bug rather
        /// than "this unit just doesn't have a tuned crit multiplier yet."</summary>
        public const float DefaultCritDamage = 1.5f;

        public static int ComputeDamage(BattleUnit attacker, BattleUnit target, SkillDefinition skill, int columnDistance, bool isCrit = false)
        {
            var atkStats = attacker.Stats;
            var defStats = target.Stats;

            // AttackMultiplier/DefenseMultiplier fold in any active AttackUp/Down,
            // DefenseUp/Down, or Break (M13/M16) status effects -- all default to 1.0
            // (no effect) on a unit with nothing active, so this is a no-op change for
            // anyone without status effects.
            float offense = (skill.usesMagic ? atkStats.magic : atkStats.attack) * attacker.AttackMultiplier;
            float defense = (skill.usesMagic ? defStats.resistance : defStats.defense) * target.DefenseMultiplier;

            float raw = skill.power * offense - defense;

            if (skill.isRanged && columnDistance > 1)
                raw *= 1f + RangedDistanceBonusPerColumn * (columnDistance - 1);

            raw *= ElementChart.GetMultiplier(skill.element, target.Definition.element);

            if (isCrit)
                raw *= atkStats.critDamage > 0f ? atkStats.critDamage : DefaultCritDamage;

            return Mathf.Max(0, Mathf.RoundToInt(raw));
        }

        /// <summary>Chance (0-1) `attacker`'s next hit against `target` actually lands.
        /// A unit with no authored accuracy (0, the type default) is treated as always
        /// hitting -- see StatBlock's doc for why 0 needs a different fallback here than
        /// it does for critRate/critDamage. Clamped so an evasion stat that outweighs
        /// accuracy can't roll a negative chance.</summary>
        public static float HitChance(BattleUnit attacker, BattleUnit target)
        {
            float accuracy = attacker.Stats.accuracy > 0f ? attacker.Stats.accuracy : 1f;
            return Mathf.Clamp01(accuracy - target.Stats.evasion);
        }

        public static int ComputeHeal(BattleUnit caster, SkillDefinition skill)
        {
            float magic = caster.Stats.magic;
            return Mathf.Max(0, Mathf.RoundToInt(skill.power * magic));
        }

        /// <summary>Same formula as ComputeHeal -- kept as a separate method (not an
        /// alias) so a future balance pass can diverge MP-restore scaling from HP-heal
        /// scaling without an implicit coupling.</summary>
        public static int ComputeManaRestore(BattleUnit caster, SkillDefinition skill)
        {
            float magic = caster.Stats.magic;
            return Mathf.Max(0, Mathf.RoundToInt(skill.power * magic));
        }
    }
}
