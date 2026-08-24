using System.Collections.Generic;
using System.Linq;

namespace Game.Battle
{
    /// <summary>
    /// Tracks where the party stands on a RunMap (M24) -- pure C#, no Resources/scene
    /// dependency, so it's testable headlessly like RunMap itself. BattleBootstrap owns
    /// one of these per run; CampScreen reads it to draw the map and offer choices.
    ///
    /// Deliberately dumb: it only knows "where am I" and "where can I go next" against
    /// the graph. It has no opinion on what a node's type means (that's
    /// BattleBootstrap.EnterNode) and no opinion on rewards/party state (that's
    /// BattleWorld/BattleRewards, carried alongside this by whoever calls MoveTo).
    /// </summary>
    public class DungeonRun
    {
        public RunMap Map { get; }

        /// <summary>Null means "hasn't entered floor 0 yet" -- a state this project
        /// currently never leaves visible to the player (BattleBootstrap.Boot()
        /// auto-enters the first floor-0 node immediately), but AvailableNextNodes and
        /// MoveTo both handle it correctly so the class doesn't assume that choice.</summary>
        public int? CurrentNodeId { get; private set; }

        public HashSet<int> VisitedNodeIds { get; } = new();

        /// <summary>True once the party has entered the map's single final-floor node
        /// (see RunMap's convergence invariant) -- the run is cleared.</summary>
        public bool IsComplete => CurrentNodeId.HasValue && Map.Get(CurrentNodeId.Value).Floor == Map.FloorCount - 1;

        public DungeonRun(RunMap map) => Map = map;

        /// <summary>Nodes the party may move to right now: floor 0 if nothing's been
        /// entered yet, otherwise whatever the current node's own NextNodeIds resolve
        /// to. Empty once IsComplete -- the final node has no outgoing edges by
        /// construction (see RunMap's connectivity invariants).</summary>
        public IReadOnlyList<RunMapNode> AvailableNextNodes()
        {
            if (CurrentNodeId == null) return Map.StartNodes;
            return Map.Get(CurrentNodeId.Value).NextNodeIds.Select(Map.Get).ToList();
        }

        public bool CanMoveTo(int nodeId) => AvailableNextNodes().Any(n => n.Id == nodeId);

        /// <summary>Advances to `nodeId` if it's actually reachable from here; a no-op
        /// (returns false) otherwise -- callers are expected to only ever offer reachable
        /// nodes as choices, so this is a safety net against a stale UI reference, not a
        /// path a normal play session should hit.</summary>
        public bool MoveTo(int nodeId)
        {
            if (!CanMoveTo(nodeId)) return false;
            CurrentNodeId = nodeId;
            VisitedNodeIds.Add(nodeId);
            return true;
        }
    }
}
