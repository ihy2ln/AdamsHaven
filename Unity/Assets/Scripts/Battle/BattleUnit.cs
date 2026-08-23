using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Game.Data;

namespace Game.Battle
{
    /// <summary>
    /// A unit's runtime battle state: rolled definition + instance, current HP/MP,
    /// and its column in the side-view formation (lane is always 0 for this slice --
    /// see BattleAssetBuilder's "side-view formation" comment for why column alone
    /// is enough to express a Darkest Dungeon-style rank).
    /// </summary>
    public class BattleUnit
    {
        public readonly CharacterDefinition Definition;
        public readonly CharacterInstance Instance;
        public readonly Faction Faction;
        public readonly bool FacingRight;

        public int Column;
        public int CurrentHp;
        public int CurrentMp;

        /// <summary>Damage taken since this unit's own turn last resolved -- checked
        /// against a threshold right before their next turn (BattleController.RunBattle)
        /// to decide whether they enter Break, then reset regardless of the outcome so
        /// each cycle starts fresh. See StatusEffectType.Break's doc.</summary>
        public int DamageTakenSinceLastTurn;

        public const int MaxUltimateCharge = 100;
        public int CurrentUltimateCharge;
        public bool IsUltimateReady => CurrentUltimateCharge >= MaxUltimateCharge;

        public readonly List<StatusEffectInstance> StatusEffects = new();

        public bool IsAlive => CurrentHp > 0;
        public StatBlock Stats => Instance.EffectiveStats(Definition);
        public int MaxMp => Definition.maxMp;

        public BattleUnit(CharacterDefinition def, CharacterInstance instance, Faction faction, int column, bool facingRight)
        {
            Definition = def;
            Instance = instance;
            Faction = faction;
            Column = column;
            FacingRight = facingRight;
            CurrentHp = Stats.hp;
            CurrentMp = Definition.maxMp;
        }

        public void ApplyDamage(int amount) => CurrentHp = Mathf.Max(0, CurrentHp - amount);
        public void ApplyHeal(int amount) => CurrentHp = Mathf.Min(Stats.hp, CurrentHp + amount);

        public void SpendMp(int amount) => CurrentMp = Mathf.Max(0, CurrentMp - amount);
        public void RestoreMp(int amount) => CurrentMp = Mathf.Min(MaxMp, CurrentMp + amount);

        public void GainUltimateCharge(int amount) =>
            CurrentUltimateCharge = Mathf.Clamp(CurrentUltimateCharge + amount, 0, MaxUltimateCharge);

        /// <summary>Called once the ultimate actually fires (BattleController) -- always
        /// drains the gauge to empty; there's no partial-cost ultimate in this design,
        /// unlike MP-cost Skill Moves.</summary>
        public void SpendUltimateCharge() => CurrentUltimateCharge = 0;

        /// <summary>Full restore -- not called by anything in the battle scene today.
        /// Provided as the hook a future farm/town "sleep to recover" system should call;
        /// there's no persistence layer connecting battle party state to the farm scene
        /// yet, so wiring an actual sleep mechanic is out of scope until that exists.</summary>
        public void RestoreMpFull() => CurrentMp = MaxMp;

        /// <summary>Between-battle partial MP recovery: 25%-50% of the *missing* amount,
        /// rolled per unit. Deliberately distinct from HP, which does not recover between
        /// this slice's two maps (the carried wound is the point, per BattleWorld's
        /// carry-over doc) -- an arbitrary number by design (project owner direction), not
        /// a tuned value. Called from BattleWorld when a carried-over roster loads map 2.</summary>
        public void RecoverMpAfterBattle()
        {
            int missing = MaxMp - CurrentMp;
            if (missing <= 0) return;
            float fraction = UnityEngine.Random.Range(0.25f, 0.5f);
            RestoreMp(Mathf.RoundToInt(missing * fraction));
        }

        // -- status effects (M13) --------------------------------------------------

        public bool IsStunned => StatusEffects.Any(s => s.Type == StatusEffectType.Stun);

        /// <summary>True if this unit's turn should be skipped entirely -- Stun (skill-
        /// inflicted) or Break (M16, damage-threshold-triggered) both incapacitate the
        /// same way. Use this instead of IsStunned for the actual turn-skip check
        /// (BattleController.RunBattle); IsStunned stays specifically about Stun for
        /// anything that cares which of the two actually applied.</summary>
        public bool IsIncapacitated => StatusEffects.Any(s => s.Type == StatusEffectType.Stun || s.Type == StatusEffectType.Break);

        /// <summary>Sum of AttackUp minus sum of AttackDown, as a multiplier against
        /// DamageCalculator's offense stat (1.0 = no effect). Read fresh every time
        /// rather than cached -- effects change turn to turn and there's no dirty-flag
        /// plumbing to invalidate a cache correctly.</summary>
        public float AttackMultiplier => 1f + NetMagnitude(StatusEffectType.AttackUp) - NetMagnitude(StatusEffectType.AttackDown);

        /// <summary>Same shape as AttackMultiplier, against DamageCalculator's defense
        /// stat -- also folds in Break's Magnitude (M16) the same way DefenseDown works,
        /// since "broken" is specifically the state of taking bonus damage while down.</summary>
        public float DefenseMultiplier => 1f + NetMagnitude(StatusEffectType.DefenseUp)
            - NetMagnitude(StatusEffectType.DefenseDown) - NetMagnitude(StatusEffectType.Break);

        float NetMagnitude(StatusEffectType type) => StatusEffects.Where(s => s.Type == type).Sum(s => s.Magnitude);

        /// <summary>Applies (or refreshes) a status effect. Refreshing in place rather
        /// than stacking a second instance is the standard JRPG convention -- reapplying
        /// Poison resets its clock and magnitude instead of double-ticking -- and avoids
        /// unbounded stacking from repeated casts of the same skill.</summary>
        public void ApplyStatus(StatusEffectType type, float magnitude, int turns)
        {
            if (type == StatusEffectType.None) return;
            var existing = StatusEffects.Find(s => s.Type == type);
            if (existing != null) { existing.Magnitude = magnitude; existing.RemainingTurns = turns; return; }
            StatusEffects.Add(new StatusEffectInstance { Type = type, Magnitude = magnitude, RemainingTurns = turns });
        }

        /// <summary>Called once at the start of this unit's own turn (BattleController
        /// .RunBattle) -- applies Poison/Regen's flat HP tick, then counts every active
        /// effect down by one turn and drops any that just expired. Whether the unit was
        /// stunned is a separate question the caller must check *before* calling this
        /// (IsStunned, evaluated against the pre-tick state) -- see RunBattle for why a
        /// 1-turn Stun needs to skip exactly one turn, not zero or two.</summary>
        public void TickStatusEffects()
        {
            foreach (var effect in StatusEffects)
            {
                if (effect.Type == StatusEffectType.Poison) ApplyDamage(Mathf.RoundToInt(effect.Magnitude));
                else if (effect.Type == StatusEffectType.Regen) ApplyHeal(Mathf.RoundToInt(effect.Magnitude));
                effect.RemainingTurns--;
            }
            StatusEffects.RemoveAll(s => s.RemainingTurns <= 0);
        }
    }
}
