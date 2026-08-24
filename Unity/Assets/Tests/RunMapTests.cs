using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Battle;

namespace Game.Tests
{
    /// <summary>
    /// RunMap/RunMapGenerator (M24) -- the branching dungeon-path structure. Pure C#,
    /// no Resources dependency, so every seed used here is reproducible and these run
    /// headlessly like the rest of the math-layer suite (ElementChart, DamageCalculator,
    /// EscapeCalculator).
    ///
    /// The point of this file is the connectivity invariants RunMap's own doc comment
    /// promises -- a generated map that's missing one of these isn't a cosmetic bug,
    /// it's a broken run (a node nobody can ever reach, or a path that dead-ends before
    /// the final floor).
    /// </summary>
    public class RunMapTests
    {
        static readonly int[] Seeds = { 0, 1, 7, 42, 12345, 999999 };

        [Test]
        public void Generate_ProducesExactlyTheRequestedFloorCount()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed, floorCount: 6);
                Assert.AreEqual(6, map.FloorCount);
                for (int f = 0; f < 6; f++)
                    Assert.IsNotEmpty(map.NodesInFloor(f), $"seed {seed}: floor {f} has no nodes.");
            }
        }

        [Test]
        public void TheFinalFloor_AlwaysHasExactlyOneNode()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed);
                Assert.AreEqual(1, map.NodesInFloor(map.FloorCount - 1).Count,
                    $"seed {seed}: the final floor should converge to a single node.");
                Assert.AreSame(map.NodesInFloor(map.FloorCount - 1)[0], map.FinalNode);
            }
        }

        /// <summary>No orphans: every node above floor 0 must be reachable from
        /// somewhere in the floor below it. This is the invariant the repair pass exists
        /// for -- without it, a random walk can easily leave a node with zero incoming
        /// edges, which would show up in play as a node the player can never actually
        /// pick.</summary>
        [Test]
        public void EveryNodeAboveFloorZero_HasAtLeastOneIncomingEdge()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed);
                var hasIncoming = new HashSet<int>();
                foreach (var n in map.Nodes) foreach (var toId in n.NextNodeIds) hasIncoming.Add(toId);

                for (int f = 1; f < map.FloorCount; f++)
                    foreach (var node in map.NodesInFloor(f))
                        Assert.IsTrue(hasIncoming.Contains(node.Id),
                            $"seed {seed}: node {node.Id} on floor {f} has no incoming edge -- it can never be reached.");
            }
        }

        /// <summary>No dead ends: every node before the final floor must have somewhere
        /// to go. Without the repair pass, a random walk can pass a node by entirely,
        /// leaving it with zero outgoing edges -- a choice that would strand the party.</summary>
        [Test]
        public void EveryNodeBeforeTheFinalFloor_HasAtLeastOneOutgoingEdge()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed);
                for (int f = 0; f < map.FloorCount - 1; f++)
                    foreach (var node in map.NodesInFloor(f))
                        Assert.IsNotEmpty(node.NextNodeIds,
                            $"seed {seed}: node {node.Id} on floor {f} has no outgoing edge -- it's a dead end.");
            }
        }

        [Test]
        public void EveryEdge_ConnectsAFloorToTheNextFloorOnly()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed);
                foreach (var node in map.Nodes)
                    foreach (var nextId in node.NextNodeIds)
                        Assert.AreEqual(node.Floor + 1, map.Get(nextId).Floor,
                            $"seed {seed}: node {node.Id} (floor {node.Floor}) connects to floor "
                            + $"{map.Get(nextId).Floor} -- edges must never skip or double back a floor.");
            }
        }

        /// <summary>Every floor-0 node can reach the final node, and the final node is
        /// reachable from every floor-0 node -- the property that makes "pick a starting
        /// path" a meaningful choice rather than a trap. Proven by breadth-first walk
        /// from each start node.</summary>
        [Test]
        public void EveryStartNode_CanReachTheFinalNode()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed);
                foreach (var start in map.StartNodes)
                {
                    var reached = new HashSet<int> { start.Id };
                    var frontier = new Queue<int>();
                    frontier.Enqueue(start.Id);
                    while (frontier.Count > 0)
                    {
                        var current = map.Get(frontier.Dequeue());
                        foreach (var nextId in current.NextNodeIds)
                            if (reached.Add(nextId)) frontier.Enqueue(nextId);
                    }
                    Assert.IsTrue(reached.Contains(map.FinalNode.Id),
                        $"seed {seed}: start node {start.Id} can never reach the final node {map.FinalNode.Id}.");
                }
            }
        }

        [Test]
        public void SameSeed_AlwaysProducesTheSameMap()
        {
            var a = RunMapGenerator.Generate(42);
            var b = RunMapGenerator.Generate(42);

            Assert.AreEqual(a.Nodes.Count, b.Nodes.Count);
            for (int i = 0; i < a.Nodes.Count; i++)
            {
                Assert.AreEqual(a.Nodes[i].Floor, b.Nodes[i].Floor);
                Assert.AreEqual(a.Nodes[i].Type, b.Nodes[i].Type);
                CollectionAssert.AreEqual(a.Nodes[i].NextNodeIds, b.Nodes[i].NextNodeIds);
            }
        }

        [Test]
        public void FloorZero_NeverContainsAnElite()
        {
            foreach (var seed in Seeds)
            {
                var map = RunMapGenerator.Generate(seed);
                Assert.IsFalse(map.NodesInFloor(0).Any(n => n.Type == RunNodeType.Elite),
                    $"seed {seed}: floor 0 should never ambush the party with an Elite before their first real choice.");
            }
        }

        // -- DungeonRun -----------------------------------------------------------

        [Test]
        public void DungeonRun_StartsWithFloorZeroAsTheOnlyAvailableMoves()
        {
            var run = new DungeonRun(RunMapGenerator.Generate(1));

            Assert.IsNull(run.CurrentNodeId);
            CollectionAssert.AreEquivalent(
                run.Map.StartNodes.Select(n => n.Id).ToList(),
                run.AvailableNextNodes().Select(n => n.Id).ToList());
        }

        [Test]
        public void MoveTo_OnlySucceedsForAReachableNode()
        {
            var run = new DungeonRun(RunMapGenerator.Generate(2));
            var validFirst = run.AvailableNextNodes()[0];
            var unreachable = run.Map.NodesInFloor(2)[0]; // two floors ahead -- never directly reachable from the start

            Assert.IsFalse(run.CanMoveTo(unreachable.Id));
            Assert.IsFalse(run.MoveTo(unreachable.Id), "moving to an unreachable node should fail, not silently teleport.");
            Assert.IsNull(run.CurrentNodeId, "a failed MoveTo shouldn't change position.");

            Assert.IsTrue(run.MoveTo(validFirst.Id));
            Assert.AreEqual(validFirst.Id, run.CurrentNodeId);
            Assert.IsTrue(run.VisitedNodeIds.Contains(validFirst.Id));
        }

        [Test]
        public void AvailableNextNodes_UpdatesToTheCurrentNodesOwnEdgesAfterMoving()
        {
            var run = new DungeonRun(RunMapGenerator.Generate(3));
            var first = run.AvailableNextNodes()[0];
            run.MoveTo(first.Id);

            CollectionAssert.AreEquivalent(
                first.NextNodeIds,
                run.AvailableNextNodes().Select(n => n.Id).ToList());
        }

        [Test]
        public void IsComplete_OnlyTrueOnceTheFinalNodeIsEntered()
        {
            var map = RunMapGenerator.Generate(4, floorCount: 3);
            var run = new DungeonRun(map);

            Assert.IsFalse(run.IsComplete);
            run.MoveTo(run.AvailableNextNodes()[0].Id);
            Assert.IsFalse(run.IsComplete, "reaching floor 1 of 3 shouldn't count as complete.");

            // Walk to the final node however many hops it takes.
            while (!run.IsComplete)
            {
                var next = run.AvailableNextNodes();
                Assert.IsNotEmpty(next, "should always have somewhere to go until the run is complete.");
                run.MoveTo(next[0].Id);
            }

            Assert.AreEqual(map.FinalNode.Id, run.CurrentNodeId);
            Assert.IsEmpty(run.AvailableNextNodes(), "the final node has no outgoing edges -- nowhere left to go.");
        }

        // -- GenerateCuratedTestRun (M27) ----------------------------------------------

        /// <summary>Pins the exact sequence from the project owner's own words: "a 2
        /// normal fights, rest area, item chest area ... then a boss fight" -- with the
        /// two normal fights kept distinct (Enemy then Elite, not the same type twice),
        /// which is what makes them map 1's roster and map 2's roster separately rather
        /// than the same fight repeated.</summary>
        [Test]
        public void CuratedTestRun_IsExactlyTheProjectOwnersFiveNodeSequence()
        {
            var map = RunMapGenerator.GenerateCuratedTestRun();

            Assert.AreEqual(5, map.FloorCount);
            var types = Enumerable.Range(0, 5).Select(f => map.NodesInFloor(f).Single().Type).ToList();
            CollectionAssert.AreEqual(
                new[] { RunNodeType.Enemy, RunNodeType.Elite, RunNodeType.Rest, RunNodeType.Treasure, RunNodeType.Elite },
                types);
        }

        [Test]
        public void CuratedTestRun_IsFullyLinearWithNoBranching()
        {
            var map = RunMapGenerator.GenerateCuratedTestRun();

            for (int f = 0; f < map.FloorCount - 1; f++)
                Assert.AreEqual(1, map.NodesInFloor(f).Single().NextNodeIds.Count,
                    $"floor {f} should connect to exactly one node -- no branching in the curated sequence.");

            Assert.IsEmpty(map.FinalNode.NextNodeIds, "the boss node should have nowhere further to go.");
        }

        /// <summary>The same connectivity invariants Generate()'s own random maps are
        /// held to -- a hand-authored map should satisfy them just as strictly as a
        /// generated one.</summary>
        [Test]
        public void CuratedTestRun_SatisfiesTheSameConnectivityInvariantsAsAGeneratedMap()
        {
            var map = RunMapGenerator.GenerateCuratedTestRun();

            Assert.AreEqual(1, map.NodesInFloor(map.FloorCount - 1).Count, "exactly one node on the final floor.");
            for (int f = 0; f < map.FloorCount; f++)
                foreach (var node in map.NodesInFloor(f))
                    foreach (var nextId in node.NextNodeIds)
                        Assert.AreEqual(f + 1, map.Get(nextId).Floor, "every edge should connect to the very next floor.");
        }

        /// <summary>End to end: a DungeonRun walking this map hits exactly one choice
        /// per floor (never a branch), and IsComplete only becomes true on the boss
        /// node -- the same "final node is the boss" contract BattleBootstrap.EnterNode
        /// leans on for BossStatMultiplier (M26).</summary>
        [Test]
        public void DungeonRun_WalkingTheCuratedSequence_HitsExactlyOneChoicePerFloorAndCompletesOnTheBoss()
        {
            var run = new DungeonRun(RunMapGenerator.GenerateCuratedTestRun());

            for (int f = 0; f < run.Map.FloorCount; f++)
            {
                var available = run.AvailableNextNodes();
                Assert.AreEqual(1, available.Count, $"floor {f} should offer exactly one next node.");
                Assert.IsFalse(run.IsComplete, $"shouldn't be complete before entering floor {run.Map.FloorCount - 1}.");
                run.MoveTo(available[0].Id);
            }

            Assert.IsTrue(run.IsComplete, "entering the final node should complete the run.");
            Assert.AreEqual(RunNodeType.Elite, run.Map.Get(run.CurrentNodeId.Value).Type, "the boss node should be Elite.");
        }
    }
}
