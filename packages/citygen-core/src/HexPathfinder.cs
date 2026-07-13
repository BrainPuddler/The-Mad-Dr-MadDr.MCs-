using System;
using System.Collections.Generic;

namespace MadDr.CityGen
{
    /// <summary>
    /// A* pathfinding over the hex grid -- the navigation layer the
    /// waypoint system drives. Deterministic: ties in the open set break
    /// by (R, Q, insertion order), never hash order, per this package's
    /// standing determinism discipline.
    ///
    /// Movement rules come in as a blocked set (from
    /// <see cref="BattlefieldState.BlockedToGround"/> /
    /// <see cref="BattlefieldState.BlockedToAmphibious"/>), so the same
    /// pathfinder serves ground and amphibious plans and automatically
    /// respects live destruction: a Destroyed building's hexes leave the
    /// ground-blocked set (open flank routes, docs/18 SS3) and a
    /// Destroyed bridge's hexes join it (reverts to water).
    /// </summary>
    public static class HexPathfinder
    {
        /// <summary>Shortest path from start to goal inclusive, or null if
        /// unreachable (or the goal itself is blocked/off-map). Steps only
        /// through in-bounds, unblocked hexes.</summary>
        public static List<HexCoord> FindPath(
            HexCoord start, HexCoord goal, CityModel city, HashSet<HexCoord> blocked,
            int maxExpansions = 40000)
        {
            if (!city.Contains(start) || !city.Contains(goal)) return null;
            if (blocked.Contains(goal)) return null;
            if (start.Equals(goal)) return new List<HexCoord> { start };

            var open = new SortedSet<Node>(NodeComparer.Instance);
            var gScore = new Dictionary<HexCoord, int>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();
            var closed = new HashSet<HexCoord>();
            var seq = 0;

            gScore[start] = 0;
            open.Add(new Node(start, start.DistanceTo(goal), seq++));

            var expansions = 0;
            while (open.Count > 0 && expansions++ < maxExpansions)
            {
                var current = open.Min;
                open.Remove(current);
                if (closed.Contains(current.Hex)) continue;
                closed.Add(current.Hex);

                if (current.Hex.Equals(goal)) return Reconstruct(cameFrom, goal);

                var g = gScore[current.Hex];
                foreach (var n in current.Hex.Neighbors())
                {
                    if (closed.Contains(n) || !city.Contains(n) || blocked.Contains(n)) continue;
                    var tentative = g + 1;
                    int known;
                    if (gScore.TryGetValue(n, out known) && known <= tentative) continue;
                    gScore[n] = tentative;
                    cameFrom[n] = current.Hex;
                    open.Add(new Node(n, tentative + n.DistanceTo(goal), seq++));
                }
            }
            return null;
        }

        /// <summary>Path to any unblocked hex ADJACENT to the target --
        /// the approach for attacking something that occupies blocked
        /// hexes: you can't path INTO it, you path next to it and swing.
        /// Returns null if no adjacent hex is reachable. If the start is
        /// already adjacent, returns the single-hex path [start].</summary>
        public static List<HexCoord> FindPathToAdjacent(
            HexCoord start, HexCoord target, CityModel city, HashSet<HexCoord> blocked,
            int maxExpansions = 40000)
        {
            return FindPathToBuilding(start, new[] { target }, city, blocked, maxExpansions);
        }

        /// <summary>Path to any unblocked hex adjacent to ANY hex of a
        /// multi-hex footprint -- attacking a building means reaching its
        /// rim, wherever that's closest. A single-hex "adjacent to the
        /// target" query fails outright on a landmark whose center hex is
        /// ringed entirely by its own footprint; this is the query
        /// gameplay actually needs. Returns [start] if already adjacent.</summary>
        public static List<HexCoord> FindPathToBuilding(
            HexCoord start, IReadOnlyList<HexCoord> footprint, CityModel city, HashSet<HexCoord> blocked,
            int maxExpansions = 40000)
        {
            var goals = new HashSet<HexCoord>();
            foreach (var hex in footprint)
                foreach (var n in hex.Neighbors())
                    if (city.Contains(n) && !blocked.Contains(n)) goals.Add(n);
            if (goals.Count == 0) return null;
            if (goals.Contains(start)) return new List<HexCoord> { start };
            return FindPathToAnyOf(start, goals, city, blocked, maxExpansions);
        }

        /// <summary>Multi-goal A*: shortest path from start to the nearest
        /// member of a goal set. Heuristic is the min hex distance over
        /// all goals -- admissible, so paths stay optimal.</summary>
        public static List<HexCoord> FindPathToAnyOf(
            HexCoord start, HashSet<HexCoord> goals, CityModel city, HashSet<HexCoord> blocked,
            int maxExpansions = 40000)
        {
            if (!city.Contains(start) || goals.Count == 0) return null;
            if (goals.Contains(start)) return new List<HexCoord> { start };

            var open = new SortedSet<Node>(NodeComparer.Instance);
            var gScore = new Dictionary<HexCoord, int>();
            var cameFrom = new Dictionary<HexCoord, HexCoord>();
            var closed = new HashSet<HexCoord>();
            var seq = 0;

            gScore[start] = 0;
            open.Add(new Node(start, HeuristicToSet(start, goals), seq++));

            var expansions = 0;
            while (open.Count > 0 && expansions++ < maxExpansions)
            {
                var current = open.Min;
                open.Remove(current);
                if (closed.Contains(current.Hex)) continue;
                closed.Add(current.Hex);

                if (goals.Contains(current.Hex)) return Reconstruct(cameFrom, current.Hex);

                var g = gScore[current.Hex];
                foreach (var n in current.Hex.Neighbors())
                {
                    if (closed.Contains(n) || !city.Contains(n) || blocked.Contains(n)) continue;
                    var tentative = g + 1;
                    int known;
                    if (gScore.TryGetValue(n, out known) && known <= tentative) continue;
                    gScore[n] = tentative;
                    cameFrom[n] = current.Hex;
                    open.Add(new Node(n, tentative + HeuristicToSet(n, goals), seq++));
                }
            }
            return null;
        }

        private static int HeuristicToSet(HexCoord from, HashSet<HexCoord> goals)
        {
            var best = int.MaxValue;
            foreach (var g in goals)
            {
                var d = from.DistanceTo(g);
                if (d < best) best = d;
            }
            return best;
        }

        private static List<HexCoord> Reconstruct(Dictionary<HexCoord, HexCoord> cameFrom, HexCoord goal)
        {
            var path = new List<HexCoord> { goal };
            var cur = goal;
            HexCoord prev;
            while (cameFrom.TryGetValue(cur, out prev))
            {
                path.Add(prev);
                cur = prev;
            }
            path.Reverse();
            return path;
        }

        private readonly struct Node
        {
            public readonly HexCoord Hex;
            public readonly int F;
            public readonly int Seq;

            public Node(HexCoord hex, int f, int seq)
            {
                Hex = hex;
                F = f;
                Seq = seq;
            }
        }

        private sealed class NodeComparer : IComparer<Node>
        {
            public static readonly NodeComparer Instance = new NodeComparer();

            public int Compare(Node a, Node b)
            {
                if (a.F != b.F) return a.F.CompareTo(b.F);
                if (a.Hex.R != b.Hex.R) return a.Hex.R.CompareTo(b.Hex.R);
                if (a.Hex.Q != b.Hex.Q) return a.Hex.Q.CompareTo(b.Hex.Q);
                return a.Seq.CompareTo(b.Seq);
            }
        }
    }
}
