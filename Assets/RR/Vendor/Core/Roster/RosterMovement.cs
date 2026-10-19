using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    /// <summary>
    /// Codex movement geometry: STEP, RAY, ZIGZAG, DIAG, KNIGHT, LEAP and PHASE (Docs/UnitCodex "Movement patterns").
    /// Pure queries; <see cref="RosterRules"/> applies the result. Path-based patterns pay +1 for each rough hex; jumps
    /// (LEAP, KNIGHT, DIAG hops) ignore terrain cost but never land on walls, units or blocking barriers.
    /// </summary>
    public static class RosterMovement
    {
        /// <summary>The twelve KNIGHT offsets 2·a + b (a a primary direction, b one of its two neighbours).</summary>
        public static readonly Hex[] KnightOffsets = BuildKnight();
        /// <summary>The six corner-to-corner DIAG hops a + b (adjacent directions a, b).</summary>
        public static readonly Hex[] DiagOffsets = { new Hex(1, 1), new Hex(2, -1), new Hex(1, -2), new Hex(-1, -1), new Hex(-2, 1), new Hex(-1, 2) };

        static Hex[] BuildKnight()
        {
            var list = new List<Hex>();
            for (int a = 0; a < 6; a++)
            {
                var da = Hex.Directions[a] * 2;
                list.Add(da + Hex.Directions[(a + 5) % 6]);
                list.Add(da + Hex.Directions[(a + 1) % 6]);
            }
            return list.ToArray();
        }

        /// <summary>The two hexes a DIAG hop squeezes between (both walls = blocked).</summary>
        static (Hex, Hex) DiagFlanks(Hex from, Hex hop)
        {
            for (int a = 0; a < 6; a++)
            {
                var da = Hex.Directions[a]; var db = Hex.Directions[(a + 1) % 6];
                if (da + db == hop) return (from + da, from + db);
            }
            return (from, from);
        }

        /// <summary>Free landing/standing hex: on the board, not a wall/wreck/blocking barrier, and empty.</summary>
        public static bool Open(MatchState s, Hex h) => !RosterTerrain.Blocks(s, h) && s.UnitAt(h) == null;

        /// <summary>Movement allowance this activation, in the pattern's own units (hexes, or hops for DIAG). 0 = cannot move.</summary>
        public static int Budget(MatchState s, UnitState u)
        {
            if (!u.OnBoard) return 0;
            if (u.HasR(RStatusType.Rooted) || u.HasR(RStatusType.Captured) || u.HasR(RStatusType.Boarded)) return 0;
            int move = u.R.MoveHexes + u.RAmount(RStatusType.MoveBuff) + RosterPassives.MoveBonus(s, u);
            int minus = 0;
            var sup = u.GetR(RStatusType.Suppressed);
            if (sup.HasValue) minus += sup.Value.Amount;
            minus += RosterPassives.MovePenalty(s, u);
            if (minus > 0) move = Math.Max(1, move - minus);
            return Math.Max(0, move);
        }

        /// <summary>
        /// Legal destinations for the unit's normal move (or its free 1-hex step after acting). Planning view: hidden enemies
        /// and hidden enemy mines are not considered (the caller passes the fogged view).
        /// </summary>
        public static Dictionary<Hex, MoveOption> Reachable(MatchState s, UnitState u, int extra = 0)
        {
            var result = new Dictionary<Hex, MoveOption>();
            if (!u.OnBoard) return result;
            if (u.ActedThisActivation || u.MovedThisActivation)
            {
                // War Momentum / Breakthrough Pace: one free step after the kill, ignoring terrain cost.
                if (!u.HasR(RStatusType.FreeStep) || u.HasR(RStatusType.Rooted)) return result;
                foreach (var n in u.Position.Neighbors())
                    if (Open(s, n)) result[n] = new MoveOption { Destination = n, Cost = 1, Path = new List<Hex> { u.Position, n } };
                return result;
            }
            int budget = Budget(s, u);
            if (budget <= 0) return result;
            budget += extra;   // slice: overclocked move (SliceRules)
            var pattern = u.R.Pattern;
            switch (pattern)
            {
                case MovePattern.Knight:
                    if (budget >= 3) { Knight(s, u, result); return result; }
                    return Paths(s, u, budget, MovePattern.Step);
                case MovePattern.Diag:
                    Diag(s, u, budget, result);
                    return result;
                case MovePattern.Leap:
                    return Leap(s, u.Position, budget, ignoreUnits: true);
                default:
                    return Paths(s, u, budget, pattern);
            }
        }

        static void Knight(MatchState s, UnitState u, Dictionary<Hex, MoveOption> result)
        {
            foreach (var off in KnightOffsets)
            {
                var h = u.Position + off;
                if (Open(s, h)) result[h] = new MoveOption { Destination = h, Cost = 3, Path = new List<Hex> { u.Position, h } };
            }
        }

        static void Diag(MatchState s, UnitState u, int hops, Dictionary<Hex, MoveOption> result)
        {
            foreach (var off in DiagOffsets)
            {
                var cur = u.Position;
                var path = new List<Hex> { cur };
                for (int k = 1; k <= hops; k++)
                {
                    var (f1, f2) = DiagFlanks(cur, off);
                    if (RosterTerrain.Blocks(s, f1) && RosterTerrain.Blocks(s, f2)) break;
                    var next = cur + off;
                    if (!Open(s, next)) break;
                    path.Add(next);
                    result[next] = new MoveOption { Destination = next, Cost = k, Path = new List<Hex>(path) };
                    cur = next;
                }
            }
        }

        /// <summary>Hexes within <paramref name="range"/> steps over non-wall hexes (units skipped when ignoreUnits), ending empty.</summary>
        public static Dictionary<Hex, MoveOption> Leap(MatchState s, Hex from, int range, bool ignoreUnits)
        {
            var result = new Dictionary<Hex, MoveOption>();
            var dist = new Dictionary<Hex, int> { [from] = 0 };
            var queue = new Queue<Hex>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var h = queue.Dequeue();
                int d = dist[h];
                if (d >= range) continue;
                for (int i = 0; i < 6; i++)
                {
                    var n = h.Neighbor(i);
                    if (dist.ContainsKey(n) || RosterTerrain.Blocks(s, n)) continue;
                    if (!ignoreUnits && s.UnitAt(n) != null) continue;
                    dist[n] = d + 1;
                    queue.Enqueue(n);
                }
            }
            foreach (var kv in dist.OrderBy(k => k.Key))
                if (kv.Key != from && s.UnitAt(kv.Key) == null)
                    result[kv.Key] = new MoveOption { Destination = kv.Key, Cost = kv.Value, Path = new List<Hex> { from, kv.Key } };
            return result;
        }

        /// <summary>Straight-line destinations (RAY-style) from a hex, through empty open hexes.</summary>
        public static Dictionary<Hex, MoveOption> Straight(MatchState s, Hex from, int range, int ignoreUnitId = 0)
        {
            var result = new Dictionary<Hex, MoveOption>();
            for (int d = 0; d < 6; d++)
            {
                var path = new List<Hex> { from };
                var cur = from;
                for (int k = 1; k <= range; k++)
                {
                    cur = cur.Neighbor(d);
                    var occ = s.UnitAt(cur);
                    if (RosterTerrain.Blocks(s, cur) || (occ != null && occ.Id != ignoreUnitId)) break;
                    path.Add(cur);
                    result[cur] = new MoveOption { Destination = cur, Cost = k, Path = new List<Hex>(path) };
                }
            }
            return result;
        }

        struct Node : IEquatable<Node>
        {
            public Hex Hex; public sbyte Dir; public byte Turns; public byte FreeRough; public byte AllyPass;
            public bool Equals(Node o) => Hex == o.Hex && Dir == o.Dir && Turns == o.Turns && FreeRough == o.FreeRough && AllyPass == o.AllyPass;
            public override bool Equals(object obj) => obj is Node n && Equals(n);
            public override int GetHashCode() => Hex.GetHashCode() * 31 + Dir * 7 + Turns * 3 + FreeRough * 5 + AllyPass * 11;
        }

        /// <summary>STEP, RAY, ZIGZAG and PHASE as a search over (hex, heading, turns used) with rough costs.</summary>
        public static Dictionary<Hex, MoveOption> Paths(MatchState s, UnitState u, int budget, MovePattern pattern)
        {
            var result = new Dictionary<Hex, MoveOption>();
            var start = new Node { Hex = u.Position, Dir = -1 };
            var best = new Dictionary<Node, int> { [start] = 0 };
            var prev = new Dictionary<Node, Node>();
            var open = new List<Node> { start };
            bool ignoreRough = RosterPassives.IgnoresRough(s, u);
            bool roughOnce = RosterPassives.IgnoresFirstRough(s, u);
            bool allyPass = RosterPassives.MayPassAlly(s, u);
            while (open.Count > 0)
            {
                // Small graphs (budget ≤ 6): a linear pick of the cheapest node keeps this deterministic and simple.
                int bi = 0;
                for (int i = 1; i < open.Count; i++)
                {
                    int ci = best[open[i]], cb = best[open[bi]];
                    if (ci < cb || (ci == cb && open[i].Hex.CompareTo(open[bi].Hex) < 0)) bi = i;
                }
                var cur = open[bi]; open.RemoveAt(bi);
                int cost = best[cur];
                for (int d = 0; d < 6; d++)
                {
                    if (pattern == MovePattern.Ray && cur.Dir >= 0 && d != cur.Dir) continue;
                    int turns = cur.Turns;
                    if (cur.Dir >= 0 && d != cur.Dir)
                    {
                        if (pattern == MovePattern.Zigzag)
                        {
                            int diff = Math.Abs(d - cur.Dir); diff = Math.Min(diff, 6 - diff);
                            if (diff != 1 || turns >= 1) continue;
                            turns++;
                        }
                    }
                    var n = cur.Hex.Neighbor(d);
                    if (RosterTerrain.Blocks(s, n)) continue;
                    var occ = s.UnitAt(n);
                    byte pass = cur.AllyPass;
                    if (occ != null)
                    {
                        if (pattern == MovePattern.Phase) { }
                        else if (allyPass && occ.Owner == u.Owner && pass == 0) pass = 1;
                        else continue;
                    }
                    int step = 1;
                    byte freeRough = cur.FreeRough;
                    if (RosterTerrain.IsRough(s, n) && !ignoreRough)
                    {
                        if (roughOnce && freeRough == 0) freeRough = 1;
                        else step = 2;
                    }
                    int nc = cost + step;
                    if (nc > budget) continue;
                    var next = new Node { Hex = n, Dir = (sbyte)d, Turns = (byte)turns, FreeRough = freeRough, AllyPass = pass };
                    if (best.TryGetValue(next, out var old) && old <= nc) continue;
                    best[next] = nc; prev[next] = cur;
                    open.Add(next);
                }
            }
            foreach (var kv in best.OrderBy(k => k.Value).ThenBy(k => k.Key.Hex))
            {
                var h = kv.Key.Hex;
                if (h == u.Position || s.UnitAt(h) != null || result.ContainsKey(h)) continue;
                var path = new List<Hex>();
                var node = kv.Key;
                path.Add(node.Hex);
                while (prev.TryGetValue(node, out var p)) { path.Add(p.Hex); node = p; }
                path.Reverse();
                result[h] = new MoveOption { Destination = h, Cost = kv.Value, Path = path };
            }
            return result;
        }

        /// <summary>The hex one step from <paramref name="from"/> toward <paramref name="toward"/> (deterministic).</summary>
        public static Hex StepToward(Hex from, Hex toward)
        {
            if (from == toward) return from;
            var line = Hex.Line(from, toward, 1);
            return line.Count > 1 ? line[1] : from;
        }

        /// <summary>The open neighbour of <paramref name="from"/> farthest from <paramref name="away"/> (ties: lowest hex).</summary>
        public static Hex? StepAway(MatchState s, Hex from, Hex away)
        {
            Hex? best = null; int bestD = Hex.Distance(from, away);
            foreach (var n in from.Neighbors().OrderBy(h => h))
            {
                if (!Open(s, n)) continue;
                int d = Hex.Distance(n, away);
                if (d > bestD) { bestD = d; best = n; }
            }
            return best;
        }

        /// <summary>Planned description of why a destination is not reachable.</summary>
        public static string Explain(MatchState s, UnitState u, Hex target)
        {
            if (!s.Map.Contains(target)) return "That hex is outside the battlefield.";
            if (RosterTerrain.Blocks(s, target)) return s.BarrierAt(target) != null ? "A barrier blocks that hex." : $"{RosterTerrain.Name(s.Config.Domain, TerrainType.Obstacle)}: impassable.";
            var occ = s.UnitAt(target);
            if (occ != null) return occ.Owner == u.Owner ? "Another of your units is standing there." : "An enemy unit occupies that hex.";
            if (u.HasR(RStatusType.Rooted)) return $"{u.Def.Name} is ROOTED and cannot move this activation.";
            if (u.ActedThisActivation) return "This unit has already acted; it can only end its activation.";
            if (u.MovedThisActivation) return "This unit has already moved this activation.";
            return $"{u.Def.Name} moves by {u.R.Pattern.ToString().ToUpperInvariant()} {Budget(s, u)}: that hex is not one of its legal destinations.";
        }
    }
}
