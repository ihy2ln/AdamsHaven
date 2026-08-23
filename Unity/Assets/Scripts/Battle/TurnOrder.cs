using System.Collections.Generic;
using System.Linq;

namespace Game.Battle
{
    /// <summary>Speed-sorted turn queue. Re-sorts alive units at the start of every
    /// round (classic JRPG "highest speed acts first each round", not a continuous
    /// ATB rail -- simplest thing that gives a readable, deterministic order).</summary>
    public class TurnOrder
    {
        readonly List<BattleUnit> _allUnits;
        Queue<BattleUnit> _round = new();

        public int RoundNumber { get; private set; }

        /// <summary>Stores the caller's list by reference, not a copy -- BattleWorld.AllUnits
        /// is a live list that sub-in/sub-out mutates (Add/Remove) mid-battle, and those
        /// changes need to be visible the next time a round is refilled. A mid-round sub
        /// doesn't retroactively touch the already-built _round queue, which is intentional:
        /// the incoming unit joins starting next round, matching "sub costs the turn."</summary>
        public TurnOrder(List<BattleUnit> allUnits)
        {
            _allUnits = allUnits;
        }

        /// <summary>Next acting unit, starting a new round (re-sorted by current speed)
        /// whenever the previous round runs out. Returns null if no unit is alive.</summary>
        public BattleUnit Next()
        {
            while (true)
            {
                if (_round.Count == 0)
                {
                    var alive = _allUnits.Where(u => u.IsAlive)
                        .OrderByDescending(u => u.Stats.speed)
                        .ToList();
                    if (alive.Count == 0) return null;
                    RoundNumber++;
                    _round = new Queue<BattleUnit>(alive);
                }

                var unit = _round.Dequeue();
                if (unit.IsAlive) return unit;
                // Dequeued a unit that died mid-round -- try the next one instead of
                // handing back a corpse.
            }
        }

        /// <summary>Read-only preview of the next `count` units to act, for BattleHud's
        /// turn-order strip (M16) -- never dequeues anything. The current round's
        /// remaining queue comes first (already in the exact order Next() will hand them
        /// out); if that's not enough to fill `count`, pads with a fresh speed-sort of
        /// next round, same formula Next() itself uses once the current queue empties.
        /// That tail is a preview, not a promise -- speed-affecting effects between now
        /// and then can still reorder it for real.</summary>
        public List<BattleUnit> PeekUpcoming(int count)
        {
            var result = _round.Where(u => u.IsAlive).ToList();
            if (result.Count < count)
            {
                var nextRound = _allUnits.Where(u => u.IsAlive).OrderByDescending(u => u.Stats.speed);
                result.AddRange(nextRound);
            }
            return result.Take(count).ToList();
        }
    }
}
