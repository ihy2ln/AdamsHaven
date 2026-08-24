using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Battle
{
    /// <summary>Node type on a dungeon run's branching path (M24). Matches the project
    /// owner's reference image 1:1 -- Unknown/Merchant/Treasure/Rest/Enemy/Elite -- so
    /// the icon set can be swapped in later without touching this enum. Four of six have
    /// real content behind them (see BattleBootstrap.EnterNode): Enemy/Elite boot a real
    /// battle, Rest (M25) opens camp's existing per-unit Rest checklist directly,
    /// Treasure (M25) grants a potion via BattleInventory.Grant. Unknown and Merchant are
    /// still structurally real but functionally inert until their own systems exist (an
    /// event table, a shop) -- see RunMap's own doc for why that's an intentional split,
    /// not an oversight.</summary>
    public enum RunNodeType { Unknown, Merchant, Treasure, Rest, Enemy, Elite }

    /// <summary>One node in a RunMap. `Column` is layout-only (which slot in its floor
    /// it sits in) -- traversal only ever cares about `NextNodeIds`, never about
    /// column adjacency, so a future prettier layout pass can move nodes around without
    /// touching what's actually connected to what.</summary>
    public class RunMapNode
    {
        public int Id;
        public int Floor;
        public int Column;
        public RunNodeType Type;

        /// <summary>Ids of nodes in Floor+1 this node connects forward to. Empty only
        /// for nodes on the last floor (RunMap.Generate guarantees every other node has
        /// at least one outgoing edge -- see its own doc for the connectivity
        /// invariants a generated map is required to satisfy).</summary>
        public List<int> NextNodeIds = new();
    }

    /// <summary>
    /// A branching path of battles between camp visits (M24) -- the data half of the
    /// Slay-the-Spire-style map the project owner asked for. Deliberately just the
    /// graph: no rendering, no node-content logic, no persistence beyond staying alive
    /// in memory for one run (see DungeonRun for position-tracking, BattleBootstrap for
    /// wiring, CampScreen for the placeholder visual). "For now just work on the
    /// structure, we'll replace the map look and icons" -- this class and
    /// RunMapGenerator are that structure.
    ///
    /// Connectivity invariants a generated RunMap always satisfies (enforced by
    /// `Generate`, pinned by RunMapTests):
    ///   - Exactly one node on the final floor (a natural convergence point -- every
    ///     path funnels into a single "boss-like" encounter, the one structural trait
    ///     borrowed directly from Slay the Spire's own map rather than invented here).
    ///   - Every node on floor > 0 has at least one incoming edge (no orphans).
    ///   - Every node on floor < FloorCount-1 has at least one outgoing edge (no dead
    ///     ends short of the final floor).
    ///   - Every edge connects a floor-F node to a floor-(F+1) node -- never skips or
    ///     doubles back.
    /// Together these guarantee every node is reachable from some floor-0 node, and
    /// every floor-0 node can reach the final node -- the property that makes "pick a
    /// path" a real choice rather than a maze with dead ends.
    /// </summary>
    public class RunMap
    {
        public int FloorCount { get; }
        public IReadOnlyList<RunMapNode> Nodes { get; }

        readonly Dictionary<int, RunMapNode> _byId;
        readonly List<List<RunMapNode>> _byFloor;

        public RunMap(int floorCount, List<RunMapNode> nodes)
        {
            FloorCount = floorCount;
            Nodes = nodes;
            _byId = nodes.ToDictionary(n => n.Id);
            _byFloor = new List<List<RunMapNode>>(floorCount);
            for (int f = 0; f < floorCount; f++) _byFloor.Add(new List<RunMapNode>());
            foreach (var n in nodes) _byFloor[n.Floor].Add(n);
            foreach (var floor in _byFloor) floor.Sort((a, b) => a.Column.CompareTo(b.Column));
        }

        public RunMapNode Get(int id) => _byId.TryGetValue(id, out var n) ? n : null;

        public IReadOnlyList<RunMapNode> NodesInFloor(int floor) => _byFloor[floor];

        public IReadOnlyList<RunMapNode> StartNodes => NodesInFloor(0);

        /// <summary>The single convergence node on the last floor -- see the class doc's
        /// connectivity invariants for why there's always exactly one.</summary>
        public RunMapNode FinalNode => NodesInFloor(FloorCount - 1)[0];
    }

    /// <summary>
    /// Builds a RunMap. Pure C#, seeded via System.Random exactly like
    /// CharacterFactory.Create -- same seed always produces the same map, which matters
    /// for reproducing a specific run to debug it.
    ///
    /// Algorithm (loosely Slay the Spire's, not a faithful port -- "just the structure"):
    /// walk `PathCount` independent paths from a random floor-0 node to the top, each
    /// step nudging its column by -1/0/+1 (clamped to the next floor's width) and
    /// recording the edge it crossed. That alone can leave gaps -- a node nobody's path
    /// happened to pass through, or a random walk that turns out to be a dead end -- so
    /// a repair pass afterward guarantees the invariants documented on RunMap: every
    /// node above floor 0 gets an incoming edge if it has none, and every node before
    /// the final floor gets an outgoing edge if it has none, each wired to its nearest
    /// neighbour by column so the result still looks like a coherent path network
    /// rather than random noise bolted onto a random walk.
    /// </summary>
    public static class RunMapGenerator
    {
        public const int DefaultFloorCount = 6;
        public const int MinFloorWidth = 3;
        public const int MaxFloorWidth = 5;
        public const int PathCount = 6;

        /// <summary>Rough content weights per floor (Enemy most common, Elite rare,
        /// Rest/Merchant/Treasure/Unknown filling the rest) -- arbitrary in this
        /// project's usual sense: coherent, not tuned. Elite is deliberately absent from
        /// floor 0 (nothing should ambush the party before their first real choice) and
        /// weighted heavily on the final floor instead, for a "boss-like" climax without
        /// inventing a node type the reference image doesn't have.</summary>
        static readonly (RunNodeType type, int weight)[] MidFloorWeights =
        {
            (RunNodeType.Enemy, 45), (RunNodeType.Unknown, 15), (RunNodeType.Merchant, 12),
            (RunNodeType.Treasure, 12), (RunNodeType.Rest, 14), (RunNodeType.Elite, 2),
        };

        static readonly (RunNodeType type, int weight)[] FirstFloorWeights =
        {
            (RunNodeType.Enemy, 70), (RunNodeType.Unknown, 30),
        };

        static readonly (RunNodeType type, int weight)[] FinalFloorWeights =
        {
            (RunNodeType.Elite, 80), (RunNodeType.Enemy, 20),
        };

        public static RunMap Generate(int seed, int floorCount = DefaultFloorCount,
            int minWidth = MinFloorWidth, int maxWidth = MaxFloorWidth, int pathCount = PathCount)
        {
            if (floorCount < 2) throw new ArgumentException("a run needs at least a start floor and a final floor.");
            var rng = new Random(seed);

            var widths = new int[floorCount];
            for (int f = 0; f < floorCount; f++)
                widths[f] = (f == floorCount - 1) ? 1 : rng.Next(minWidth, maxWidth + 1);

            var nodes = new List<RunMapNode>();
            var byFloorColumn = new Dictionary<(int floor, int col), RunMapNode>();
            int nextId = 0;
            for (int f = 0; f < floorCount; f++)
            {
                for (int c = 0; c < widths[f]; c++)
                {
                    var node = new RunMapNode { Id = nextId++, Floor = f, Column = c };
                    nodes.Add(node);
                    byFloorColumn[(f, c)] = node;
                }
            }

            var edges = new HashSet<(int from, int to)>();
            void Connect(RunMapNode from, RunMapNode to)
            {
                if (edges.Add((from.Id, to.Id))) from.NextNodeIds.Add(to.Id);
            }

            // Random walks -- the main source of branching/merging paths.
            for (int p = 0; p < pathCount; p++)
            {
                int col = rng.Next(widths[0]);
                for (int f = 0; f < floorCount - 1; f++)
                {
                    var from = byFloorColumn[(f, col)];
                    int nextCol = Math.Clamp(col + rng.Next(-1, 2), 0, widths[f + 1] - 1);
                    var to = byFloorColumn[(f + 1, nextCol)];
                    Connect(from, to);
                    col = nextCol;
                }
            }

            // Repair pass -- guarantees RunMap's documented connectivity invariants
            // regardless of how the random walks above happened to land. `hasIncoming`
            // is a single O(edges) scan up front rather than re-scanning `edges` per
            // node; outgoing just reads NextNodeIds.Count directly.
            var hasIncoming = new HashSet<int>();
            foreach (var n in nodes) foreach (var toId in n.NextNodeIds) hasIncoming.Add(toId);

            for (int f = 1; f < floorCount; f++)
            {
                foreach (var node in byFloorColumn.Values.Where(n => n.Floor == f))
                {
                    if (hasIncoming.Contains(node.Id)) continue;
                    var nearest = NearestByColumn(widths[f - 1], node.Column, c => byFloorColumn[(f - 1, c)]);
                    Connect(nearest, node);
                    hasIncoming.Add(node.Id);
                }
            }
            for (int f = 0; f < floorCount - 1; f++)
            {
                foreach (var node in byFloorColumn.Values.Where(n => n.Floor == f && n.NextNodeIds.Count == 0))
                {
                    var nearest = NearestByColumn(widths[f + 1], node.Column, c => byFloorColumn[(f + 1, c)]);
                    Connect(node, nearest);
                }
            }

            // Content. Floor 0 and the final floor get their own weight tables; every
            // floor in between shares one.
            for (int f = 0; f < floorCount; f++)
            {
                var weights = f == 0 ? FirstFloorWeights : f == floorCount - 1 ? FinalFloorWeights : MidFloorWeights;
                foreach (var node in byFloorColumn.Values.Where(n => n.Floor == f))
                    node.Type = PickWeighted(weights, rng);
            }

            return new RunMap(floorCount, nodes);
        }

        static RunMapNode NearestByColumn(int width, int targetColumn, Func<int, RunMapNode> lookup)
        {
            int best = 0, bestDist = int.MaxValue;
            for (int c = 0; c < width; c++)
            {
                int dist = Math.Abs(c - targetColumn);
                if (dist < bestDist) { bestDist = dist; best = c; }
            }
            return lookup(best);
        }

        static RunNodeType PickWeighted((RunNodeType type, int weight)[] table, Random rng)
        {
            int total = table.Sum(t => t.weight);
            int roll = rng.Next(total);
            foreach (var (type, weight) in table)
            {
                if (roll < weight) return type;
                roll -= weight;
            }
            return table[^1].type;
        }
    }
}
