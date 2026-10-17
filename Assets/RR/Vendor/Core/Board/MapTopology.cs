using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Commandfall.Core
{
    /// <summary>
    /// How a class of unit crosses static terrain, for topology analysis (temporary occupancy is never part of it).
    /// Ground units climb high ground at a cost; hover units pay 1 everywhere; hazard-averse planning treats unstable
    /// conduits as walls (units that must not end a move on one still need a way around it). Unit-specific movement
    /// abilities (Overclock dashes, Asset Relocation) only add options, so they never make a valid layout invalid.
    /// </summary>
    public sealed class MovementProfile
    {
        public readonly string Name;
        readonly bool _avoidsHazards, _hover;
        readonly Func<TerrainType, bool> _alsoBlocks;

        MovementProfile(string name, bool avoidsHazards, bool hover, Func<TerrainType, bool> alsoBlocks = null)
        { Name = name; _avoidsHazards = avoidsHazards; _hover = hover; _alsoBlocks = alsoBlocks; }

        public bool Blocks(TerrainType t)
        {
            var p = TerrainRules.Of(t);
            return p.BlocksMovement || (_avoidsHazards && p.Damaging) || (_alsoBlocks != null && _alsoBlocks(t));
        }

        /// <summary>Diagnostics: a ground profile that also treats <paramref name="walls"/> as impassable — e.g. how a
        /// layout reads to a player when its high-ground landforms look like boulders.</summary>
        public static MovementProfile Perceived(string name, params TerrainType[] walls) =>
            new MovementProfile(name, false, false, t => Array.IndexOf(walls, t) >= 0);

        public int StepCost(TerrainType from, TerrainType to)
        {
            if (_hover) return 1;
            var p = TerrainRules.Of(to);
            return p.ClimbCost > 1 && from != to ? p.ClimbCost : 1;
        }

        public static readonly MovementProfile Ground = new MovementProfile("Ground", false, false);
        public static readonly MovementProfile Hover = new MovementProfile("Hover", false, true);
        public static readonly MovementProfile HazardAverse = new MovementProfile("Hazard-averse", true, false);
        public static readonly MovementProfile[] All = { Ground, Hover, HazardAverse };

        public override string ToString() => Name;
    }

    /// <summary>Acceptance thresholds for a battlefield layout.</summary>
    public sealed class TopologyRules
    {
        /// <summary>Most dead-end regions a layout may have (each must be declared intentional).</summary>
        public int MaxDeadEnds = 2;
        /// <summary>A region whose only way in is a contiguous run of at most this many hexes is single-entry.</summary>
        public int MaxEntranceWidth = 2;
        /// <summary>Share of hexes ordinary ground units can stand on.</summary>
        public double MinTraversableFraction = 0.85;
        /// <summary>Strategic approaches (West, Centre, East lanes) that must each link the two deployment territories.</summary>
        public int MinApproaches = 3;
        /// <summary>Vertex-disjoint routes each approach must offer on its own (no single-file crawls).</summary>
        public int MinApproachWidth = 2;
        /// <summary>Rows of no-man's land on which each pair of neighbouring approaches must connect.</summary>
        public int MinCrossLinks = 2;
        /// <summary>A single-entry region larger than this share of the board is a severed section, not a pocket.</summary>
        public double PocketFraction = 0.15;
        /// <summary>Deployable hexes each territory needs (the army size cap).</summary>
        public int MinDeployable = 7;
        /// <summary>Check the approach and cross-link requirements (off for the small legacy board).</summary>
        public bool RequireApproaches = true;

        public static readonly TopologyRules Default = new TopologyRules();
    }

    public enum DeadEndKind : byte
    {
        /// <summary>One hex with a single way in.</summary>
        TerminalCell = 0,
        /// <summary>A single-file chain of hexes leading nowhere.</summary>
        Corridor = 1,
        /// <summary>A wider cul-de-sac behind a narrow mouth.</summary>
        Pocket = 2,
    }

    public sealed class DeadEndRegion
    {
        public DeadEndKind Kind;
        public List<Hex> Hexes = new List<Hex>();
        /// <summary>The hexes a unit must pass to get in or out (1 or 2 adjacent hexes).</summary>
        public List<Hex> Entrance = new List<Hex>();
        public bool Intentional;
        public override string ToString() =>
            $"{Kind} of {Hexes.Count} at {string.Join(" ", Hexes.Take(6))}{(Hexes.Count > 6 ? " …" : "")} (entrance {string.Join(" ", Entrance)}){(Intentional ? " [intentional]" : "")}";
    }

    public enum ChokeKind : byte
    {
        /// <summary>A pinch between blockers that has alternatives around it: a legitimate contested passage.</summary>
        Narrows = 0,
        /// <summary>A narrow cut that alone separates two substantial parts of the board.</summary>
        Mandatory = 1,
    }

    public sealed class Chokepoint
    {
        public ChokeKind Kind;
        public List<Hex> Hexes = new List<Hex>();
        /// <summary>Hexes on the smaller side of a mandatory cut (0 for narrows).</summary>
        public int Severed;
        public override string ToString() => Kind == ChokeKind.Mandatory
            ? $"mandatory passage {string.Join(" ", Hexes)} cuts off {Severed} hexes"
            : $"narrows at {string.Join(" ", Hexes)}";
    }

    public sealed class ApproachInfo
    {
        public string Name;
        public int Hexes;
        /// <summary>The lane alone links the two deployment territories.</summary>
        public bool Connects;
        /// <summary>Vertex-disjoint routes through the lane alone (its narrowest width).</summary>
        public int Width;
        public override string ToString() => $"{Name}: {Hexes} hexes, {(Connects ? $"connects, width {Width}" : "does NOT connect")}";
    }

    /// <summary>Everything the validator learned about one layout under one movement profile.</summary>
    public sealed class TopologyReport
    {
        public string MapId;
        public string Profile;
        public int TileCount, TraversableCount, BlockedCount, SafeCount;
        public double TraversableFraction;
        public List<List<Hex>> Components = new List<List<Hex>>();
        public List<Hex> Unreachable = new List<Hex>();
        public List<Hex> ArticulationPoints = new List<Hex>();
        /// <summary>Hexes with exactly one traversable neighbour.</summary>
        public List<Hex> TerminalHexes = new List<Hex>();
        public List<DeadEndRegion> DeadEnds = new List<DeadEndRegion>();
        public List<Chokepoint> Chokepoints = new List<Chokepoint>();
        public List<ApproachInfo> Approaches = new List<ApproachInfo>();
        /// <summary>Rows of no-man's land on which approach i connects to approach i + 1.</summary>
        public int[] CrossLinks = new int[0];
        /// <summary>Vertex-disjoint routes between the two deployment territories over the whole board.</summary>
        public int ZoneRoutes;
        public int[] DeployableHexes = new int[2];
        /// <summary>Reports for the other movement profiles (connectivity only).</summary>
        public List<TopologyReport> Profiles = new List<TopologyReport>();
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public bool Passed => Errors.Count == 0 && Profiles.All(p => p.Errors.Count == 0);

        public int ViableApproaches(TopologyRules rules) => Approaches.Count(a => a.Connects && a.Width >= rules.MinApproachWidth);

        public IEnumerable<string> AllErrors() => Errors.Concat(Profiles.SelectMany(p => p.Errors.Select(e => $"[{p.Profile}] {e}")));

        public string Summary() =>
            $"{MapId}: {TraversableCount}/{TileCount} traversable ({TraversableFraction:P0}), {Components.Count} region(s), " +
            $"{DeadEnds.Count} dead end(s) ({DeadEnds.Count(d => d.Intentional)} intentional), {ArticulationPoints.Count} articulation point(s), " +
            $"{Chokepoints.Count(c => c.Kind == ChokeKind.Mandatory)} mandatory / {Chokepoints.Count(c => c.Kind == ChokeKind.Narrows)} narrows, " +
            $"approaches {string.Join(", ", Approaches.Select(a => a.Connects ? $"{a.Name} w{a.Width}" : $"{a.Name} ✗"))}, " +
            $"cross-links [{string.Join(", ", CrossLinks)}], zone routes {ZoneRoutes} — {(Passed ? "PASS" : "FAIL")}";

        /// <summary>Multi-line developer diagnostic with an annotated map: '!' unreachable, 'D' dead end, 'e' its entrance,
        /// 'A' articulation point, 'n' narrows.</summary>
        public string Describe(MapDefinition map)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Summary());
            var dead = new HashSet<Hex>(DeadEnds.SelectMany(d => d.Hexes));
            var ent = new HashSet<Hex>(DeadEnds.SelectMany(d => d.Entrance));
            var art = new HashSet<Hex>(ArticulationPoints);
            var narrows = new HashSet<Hex>(Chokepoints.Where(c => c.Kind == ChokeKind.Narrows).SelectMany(c => c.Hexes));
            var cut = new HashSet<Hex>(Unreachable);
            sb.Append(map.Ascii(h => cut.Contains(h) ? '!' : dead.Contains(h) ? 'D' : ent.Contains(h) ? 'e' : art.Contains(h) ? 'A' : narrows.Contains(h) ? 'n' : (char?)null));
            foreach (var d in DeadEnds) sb.AppendLine("  dead end: " + d);
            foreach (var c in Chokepoints) sb.AppendLine("  " + c);
            foreach (var a in Approaches) sb.AppendLine("  approach " + a);
            foreach (var e in AllErrors()) sb.AppendLine("  ERROR " + e);
            foreach (var w in Warnings) sb.AppendLine("  warning " + w);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Graph analysis of a battlefield's walkable hexes (axial adjacency, the same six neighbours the rules use).
    /// Finds connected regions, articulation points, terminal hexes, dead-end regions (any region reachable only through
    /// a contiguous mouth of at most <see cref="TopologyRules.MaxEntranceWidth"/> hexes, however many hexes it holds and
    /// however they connect inside), mandatory chokepoints (narrow cuts that sever large regions), narrows, the three
    /// strategic approaches and their cross-connections. Deterministic: every collection is ordered by hex.
    /// </summary>
    public static class MapTopology
    {
        sealed class Graph
        {
            public readonly List<Hex> Nodes;
            public readonly Dictionary<Hex, int> Index;
            public readonly List<int>[] Adj;

            public Graph(IEnumerable<Hex> nodes)
            {
                Nodes = nodes.OrderBy(h => h).ToList();
                Index = new Dictionary<Hex, int>(Nodes.Count);
                for (int i = 0; i < Nodes.Count; i++) Index[Nodes[i]] = i;
                Adj = new List<int>[Nodes.Count];
                for (int i = 0; i < Nodes.Count; i++)
                {
                    Adj[i] = new List<int>(6);
                    for (int d = 0; d < 6; d++)
                        if (Index.TryGetValue(Nodes[i].Neighbor(d), out int j)) Adj[i].Add(j);
                }
            }

            /// <summary>Connected components of the nodes allowed by <paramref name="keep"/>, largest first.</summary>
            public List<List<int>> Components(Func<int, bool> keep)
            {
                var seen = new bool[Nodes.Count];
                var result = new List<List<int>>();
                var queue = new Queue<int>();
                for (int s = 0; s < Nodes.Count; s++)
                {
                    if (seen[s] || !keep(s)) continue;
                    var comp = new List<int>();
                    seen[s] = true; queue.Enqueue(s);
                    while (queue.Count > 0)
                    {
                        int v = queue.Dequeue(); comp.Add(v);
                        foreach (int w in Adj[v]) if (!seen[w] && keep(w)) { seen[w] = true; queue.Enqueue(w); }
                    }
                    comp.Sort();
                    result.Add(comp);
                }
                // Largest first; ties by the smallest hex (components are sorted, and node order is hex order).
                result.Sort((a, b) => a.Count != b.Count ? b.Count.CompareTo(a.Count) : a[0].CompareTo(b[0]));
                return result;
            }
        }

        public static TopologyReport Analyze(MapDefinition map, MovementProfile profile = null, TopologyRules rules = null, bool full = true)
        {
            profile ??= MovementProfile.Ground;
            rules ??= TopologyRules.Default;
            var rep = new TopologyReport { MapId = map.Id, Profile = profile.Name, TileCount = map.TileCount };
            var g = new Graph(map.AllHexes.Where(h => !profile.Blocks(map.Terrain(h))));
            int n = g.Nodes.Count;
            rep.TraversableCount = map.AllHexes.Count(h => !TerrainRules.BlocksMovement(map.Terrain(h)));
            rep.BlockedCount = map.TileCount - rep.TraversableCount;
            rep.SafeCount = map.AllHexes.Count(h => { var p = TerrainRules.Of(map.Terrain(h)); return !p.BlocksMovement && !p.Damaging; });
            rep.TraversableFraction = map.TileCount > 0 ? rep.TraversableCount / (double)map.TileCount : 0;
            if (n == 0) { rep.Errors.Add("No traversable hexes."); return rep; }

            var comps = g.Components(_ => true);
            foreach (var c in comps) rep.Components.Add(c.Select(i => g.Nodes[i]).ToList());
            var main = new HashSet<int>(comps[0]);
            for (int k = 1; k < comps.Count; k++) rep.Unreachable.AddRange(comps[k].Select(i => g.Nodes[i]));
            rep.Unreachable.Sort();
            if (rep.Unreachable.Count > 0)
                rep.Errors.Add($"{rep.Unreachable.Count} hex(es) cannot be reached from the main battlefield: {string.Join(" ", rep.Unreachable.Take(12))}");

            for (int seat = 0; seat < 2; seat++)
            {
                var zone = map.DeploymentZone(seat).Where(h => g.Index.TryGetValue(h, out int i) && main.Contains(i)).ToList();
                rep.DeployableHexes[seat] = zone.Count;
                if (zone.Count < rules.MinDeployable)
                    rep.Errors.Add($"Seat {seat} territory has only {zone.Count} usable deployment hexes (needs {rules.MinDeployable}).");
            }
            if (!full) return rep;

            // ---------------------------------------------------------------- local structure
            foreach (int v in main)
                if (g.Adj[v].Count(main.Contains) == 1) rep.TerminalHexes.Add(g.Nodes[v]);
            rep.TerminalHexes.Sort();
            rep.ArticulationPoints = ArticulationPoints(g, main).Select(i => g.Nodes[i]).OrderBy(h => h).ToList();

            // ---------------------------------------------------------------- single-entry regions
            int pocketLimit = Math.Max(3, (int)Math.Floor(rules.PocketFraction * main.Count));
            var pockets = new List<(HashSet<int> region, List<int> entrance)>();
            var mandatory = new List<Chokepoint>();
            foreach (var cut in Cuts(g, main, rules.MaxEntranceWidth))
            {
                var cutSet = new HashSet<int>(cut);
                var parts = g.Components(i => main.Contains(i) && !cutSet.Contains(i));
                if (parts.Count < 2) continue;
                for (int p = 1; p < parts.Count; p++)
                {
                    var part = parts[p];
                    // The mouth must be the whole cut: a pair whose second hex doesn't touch the part is a single cut.
                    var touching = cut.Where(c => g.Adj[c].Any(w => part.BinarySearch(w) >= 0)).ToList();
                    if (touching.Count != cut.Count) continue;
                    if (part.Count <= pocketLimit) pockets.Add((new HashSet<int>(part), cut.ToList()));
                    else mandatory.Add(new Chokepoint { Kind = ChokeKind.Mandatory, Hexes = cut.Select(i => g.Nodes[i]).OrderBy(h => h).ToList(), Severed = part.Count });
                }
            }
            // Keep maximal regions: nested pockets (a deeper cut inside a pocket) and overlapping ones merge into one.
            pockets.Sort((a, b) => b.region.Count != a.region.Count ? b.region.Count.CompareTo(a.region.Count) : a.region.Min().CompareTo(b.region.Min()));
            var regions = new List<(HashSet<int> region, List<int> entrance)>();
            foreach (var p in pockets)
            {
                int hit = regions.FindIndex(r => r.region.Overlaps(p.region));
                if (hit < 0) { regions.Add(p); continue; }
                regions[hit].region.UnionWith(p.region);
            }
            foreach (var (region, entrance) in regions)
            {
                var hexes = region.Select(i => g.Nodes[i]).OrderBy(h => h).ToList();
                var d = new DeadEndRegion
                {
                    Hexes = hexes,
                    Entrance = entrance.Select(i => g.Nodes[i]).OrderBy(h => h).ToList(),
                    Intentional = map.IntentionalDeadEnds.Any(a => region.Contains(g.Index.TryGetValue(a, out int ai) ? ai : -1)),
                };
                // A corridor is a simple path: nobody inside has more than two neighbours inside, and there are no loops.
                int edges = region.Sum(v => g.Adj[v].Count(region.Contains)) / 2;
                bool chain = edges == region.Count - 1 && region.All(v => g.Adj[v].Count(region.Contains) <= 2);
                d.Kind = hexes.Count == 1 ? DeadEndKind.TerminalCell : chain ? DeadEndKind.Corridor : DeadEndKind.Pocket;
                rep.DeadEnds.Add(d);
            }
            rep.DeadEnds.Sort((a, b) => a.Hexes[0].CompareTo(b.Hexes[0]));
            foreach (var d in rep.DeadEnds.Where(x => !x.Intentional))
                rep.Errors.Add($"Accidental dead end: {d}");
            if (rep.DeadEnds.Count > rules.MaxDeadEnds)
                rep.Errors.Add($"{rep.DeadEnds.Count} dead-end regions (at most {rules.MaxDeadEnds} allowed).");
            foreach (var hex in map.IntentionalDeadEnds)
                if (!rep.DeadEnds.Any(d => d.Hexes.Contains(hex)))
                    rep.Warnings.Add($"Declared dead end at {hex} is not enclosed (it is open ground).");

            // Mandatory chokepoints, deduplicated by cut.
            foreach (var m in mandatory.GroupBy(c => string.Join(";", c.Hexes)).Select(gr => gr.OrderBy(c => c.Severed).First()))
            {
                rep.Chokepoints.Add(m);
                rep.Errors.Add($"Single narrow passage: {m}");
            }
            // Narrows: a hex wedged between blockers (its walkable neighbours fall into two or more separate arcs) that is
            // not a cut — a contested gap with a way around it.
            var cutHexes = new HashSet<Hex>(rep.Chokepoints.SelectMany(c => c.Hexes).Concat(rep.ArticulationPoints));
            foreach (int v in main.OrderBy(i => i))
            {
                var h = g.Nodes[v];
                if (cutHexes.Contains(h)) continue;
                if (OpenArcs(h, x => g.Index.TryGetValue(x, out int xi) && main.Contains(xi)) >= 2)
                    rep.Chokepoints.Add(new Chokepoint { Kind = ChokeKind.Narrows, Hexes = new List<Hex> { h } });
            }

            // ---------------------------------------------------------------- strategic approaches
            int maxLat = map.AllHexes.Max(h => Math.Abs(h.Lateral));
            int third = Math.Max(1, maxLat / 3);
            int Lane(Hex h) => h.Lateral < -third ? 0 : h.Lateral > third ? 2 : 1;
            string[] names = { "West flank", "Centre", "East flank" };
            var zone0 = new HashSet<Hex>(map.AllHexes.Where(h => map.ZoneSeat(h) == 0));
            var zone1 = new HashSet<Hex>(map.AllHexes.Where(h => map.ZoneSeat(h) == 1));
            for (int lane = 0; lane < 3; lane++)
            {
                int l = lane;
                Func<int, bool> inLane = i => main.Contains(i) && Lane(g.Nodes[i]) == l;
                var info = new ApproachInfo { Name = names[lane], Hexes = main.Count(i => Lane(g.Nodes[i]) == l) };
                info.Width = DisjointRoutes(g, inLane, i => zone0.Contains(g.Nodes[i]), i => zone1.Contains(g.Nodes[i]));
                info.Connects = info.Width > 0;
                rep.Approaches.Add(info);
            }
            rep.ZoneRoutes = DisjointRoutes(g, main.Contains, i => zone0.Contains(g.Nodes[i]), i => zone1.Contains(g.Nodes[i]));
            rep.CrossLinks = new int[2];
            for (int pair = 0; pair < 2; pair++)
            {
                var rows = new HashSet<int>();
                foreach (int v in main)
                {
                    var h = g.Nodes[v];
                    if (Lane(h) != pair || map.ZoneSeat(h) >= 0) continue;
                    foreach (int w in g.Adj[v])
                        if (main.Contains(w) && Lane(g.Nodes[w]) == pair + 1 && map.ZoneSeat(g.Nodes[w]) < 0) { rows.Add(h.R); break; }
                }
                rep.CrossLinks[pair] = rows.Count;
            }
            if (rules.RequireApproaches)
            {
                int viable = rep.ViableApproaches(rules);
                if (viable < rules.MinApproaches)
                    rep.Errors.Add($"Only {viable} strategic approach(es) link the territories at width ≥ {rules.MinApproachWidth} (needs {rules.MinApproaches}): {string.Join("; ", rep.Approaches)}");
                for (int pair = 0; pair < 2; pair++)
                    if (rep.CrossLinks[pair] < rules.MinCrossLinks)
                        rep.Errors.Add($"{names[pair]} and {names[pair + 1]} connect on only {rep.CrossLinks[pair]} row(s) of no-man's land (needs {rules.MinCrossLinks}).");
            }

            if (rep.TraversableFraction < rules.MinTraversableFraction)
                rep.Errors.Add($"Only {rep.TraversableFraction:P0} of the battlefield is traversable (needs {rules.MinTraversableFraction:P0}).");
            var accidental = new HashSet<Hex>(rep.DeadEnds.Where(d => !d.Intentional).SelectMany(d => d.Hexes));
            foreach (var h in map.AllHexes.Where(h => map.ZoneSeat(h) >= 0 && accidental.Contains(h)))
                rep.Warnings.Add($"Deployment hex {h} lies inside an accidental dead end.");
            return rep;
        }

        // ------------------------------------------------------------------ graph helpers

        /// <summary>Candidate entrances: every single hex, and every pair of adjacent hexes when width ≥ 2.</summary>
        static IEnumerable<List<int>> Cuts(Graph g, HashSet<int> main, int maxWidth)
        {
            var nodes = main.OrderBy(i => i).ToList();
            foreach (int v in nodes) yield return new List<int> { v };
            if (maxWidth < 2) yield break;
            foreach (int v in nodes)
                foreach (int w in g.Adj[v])
                    if (w > v && main.Contains(w)) yield return new List<int> { v, w };
        }

        /// <summary>Articulation points of the subgraph (iterative Tarjan low-link).</summary>
        static List<int> ArticulationPoints(Graph g, HashSet<int> main)
        {
            int n = g.Nodes.Count, timer = 0;
            var disc = new int[n]; var low = new int[n]; var parent = new int[n];
            var isAp = new bool[n];
            for (int i = 0; i < n; i++) { disc[i] = -1; parent[i] = -1; }
            var iter = new int[n];
            foreach (int root in main.OrderBy(i => i))
            {
                if (disc[root] >= 0) continue;
                int rootChildren = 0;
                var stack = new Stack<int>();
                stack.Push(root); disc[root] = low[root] = timer++;
                while (stack.Count > 0)
                {
                    int v = stack.Peek();
                    if (iter[v] < g.Adj[v].Count)
                    {
                        int w = g.Adj[v][iter[v]++];
                        if (!main.Contains(w)) continue;
                        if (disc[w] < 0)
                        {
                            parent[w] = v; disc[w] = low[w] = timer++;
                            if (v == root) rootChildren++;
                            stack.Push(w);
                        }
                        else if (w != parent[v]) low[v] = Math.Min(low[v], disc[w]);
                    }
                    else
                    {
                        stack.Pop();
                        int p = parent[v];
                        if (p >= 0)
                        {
                            low[p] = Math.Min(low[p], low[v]);
                            if (p != root && low[v] >= disc[p]) isAp[p] = true;
                        }
                    }
                }
                if (rootChildren > 1) isAp[root] = true;
            }
            var result = new List<int>();
            for (int i = 0; i < n; i++) if (isAp[i]) result.Add(i);
            return result;
        }

        /// <summary>Number of separate arcs of walkable neighbours around a hex (2+ means it sits in a gap between blockers).</summary>
        static int OpenArcs(Hex h, Func<Hex, bool> walkable)
        {
            int arcs = 0, open = 0;
            for (int d = 0; d < 6; d++)
            {
                bool cur = walkable(h.Neighbor(d)), prev = walkable(h.Neighbor(d + 5));
                if (cur) open++;
                if (cur && !prev) arcs++;
            }
            return open == 6 ? 1 : arcs;
        }

        /// <summary>Maximum number of vertex-disjoint routes from any source hex to any sink hex inside the allowed
        /// subgraph (Edmonds–Karp on the node-split graph).</summary>
        static int DisjointRoutes(Graph g, Func<int, bool> allowed, Func<int, bool> isSource, Func<int, bool> isSink)
        {
            int n = g.Nodes.Count;
            int S = 2 * n, T = 2 * n + 1, size = 2 * n + 2;
            var cap = new Dictionary<long, int>();
            var adj = new List<int>[size];
            for (int i = 0; i < size; i++) adj[i] = new List<int>();
            void Edge(int a, int b, int c)
            {
                long k = (long)a * size + b, kr = (long)b * size + a;
                if (!cap.ContainsKey(k)) { cap[k] = 0; adj[a].Add(b); }
                if (!cap.ContainsKey(kr)) { cap[kr] = 0; adj[b].Add(a); }
                cap[k] += c;
            }
            for (int v = 0; v < n; v++)
            {
                if (!allowed(v)) continue;
                Edge(2 * v, 2 * v + 1, 1);                 // in → out: each hex carries one route
                foreach (int w in g.Adj[v]) if (allowed(w)) Edge(2 * v + 1, 2 * w, 1);
                if (isSource(v)) Edge(S, 2 * v, 1);
                if (isSink(v)) Edge(2 * v + 1, T, 1);
            }
            int flow = 0;
            var prev = new int[size];
            while (true)
            {
                for (int i = 0; i < size; i++) prev[i] = -1;
                prev[S] = S;
                var q = new Queue<int>(); q.Enqueue(S);
                while (q.Count > 0 && prev[T] < 0)
                {
                    int a = q.Dequeue();
                    foreach (int b in adj[a])
                        if (prev[b] < 0 && cap[(long)a * size + b] > 0) { prev[b] = a; q.Enqueue(b); }
                }
                if (prev[T] < 0) break;
                for (int b = T; b != S; b = prev[b])
                {
                    int a = prev[b];
                    cap[(long)a * size + b] -= 1;
                    cap[(long)b * size + a] += 1;
                }
                flow++;
            }
            return flow;
        }
    }

    /// <summary>
    /// Validates battlefield layouts before a battle can begin, and repairs failures deterministically: the obstacle most
    /// responsible for the first failure is moved to a nearby hex where it does no harm, or removed, mirrored so the
    /// layout stays fair; then everything is checked again. A layout that still fails is rejected.
    /// </summary>
    public static class TopologyValidator
    {
        public static TopologyReport Validate(MapDefinition map, TopologyRules rules = null)
        {
            rules ??= TopologyRules.Default;
            var rep = MapTopology.Analyze(map, MovementProfile.Ground, rules);
            // Hover units share the ground units' blocking terrain; hazard-averse planning must still reach everything safe.
            foreach (var p in new[] { MovementProfile.Hover, MovementProfile.HazardAverse })
            {
                var pr = MapTopology.Analyze(map, p, rules, full: false);
                if (p == MovementProfile.HazardAverse && pr.Unreachable.Count > 0)
                    pr.Errors[0] = $"Unstable conduits seal off {pr.Unreachable.Count} safe hex(es): {string.Join(" ", pr.Unreachable.Take(12))}";
                rep.Profiles.Add(pr);
            }
            return rep;
        }

        public static MapDefinition Repair(MapDefinition source, out TopologyReport report, TopologyRules rules = null)
        {
            rules ??= TopologyRules.Default;
            var map = source.Clone();
            report = Validate(map, rules);
            bool symmetric = source.IsPointSymmetric();
            for (int guard = 0; guard < 64 && !report.Passed; guard++)
            {
                var culprit = Culprit(map, report);
                if (culprit == null) break;
                var c = culprit.Value;
                var original = map.Terrain(c);
                int errors = report.AllErrors().Count();
                // First try to move the blocker a short way (keeps the designer's intent), then remove it.
                bool fixedByMove = false;
                foreach (var spot in map.AllHexes.Where(h => Hex.Distance(h, c) is >= 1 and <= 2 && map.Terrain(h) == TerrainType.Open
                                                           && map.ZoneSeat(h) < 0 && (!symmetric || h != c.Rotate180()))
                                                 .OrderBy(h => Hex.Distance(h, c)).ThenBy(h => h))
                {
                    var trial = map.Clone();
                    Put(trial, c, TerrainType.Open, symmetric);
                    Put(trial, spot, original, symmetric);
                    var tr = Validate(trial, rules);
                    if (tr.AllErrors().Count() < errors && !NewProblems(report, tr))
                    {
                        map = trial; report = tr; fixedByMove = true;
                        map.RepairLog.Add($"Moved {original} from {c} to {spot}{(symmetric ? " (and the mirrored hexes)" : "")}.");
                        break;
                    }
                }
                if (fixedByMove) continue;
                Put(map, c, TerrainType.Open, symmetric);
                map.RepairLog.Add($"Removed {original} at {c}{(symmetric ? $" and {c.Rotate180()}" : "")}.");
                report = Validate(map, rules);
            }
            if (!report.Passed) map.RepairLog.Add("Rejected: the layout still fails validation after repair.");
            return map;
        }

        static void Put(MapDefinition m, Hex h, TerrainType t, bool symmetric)
        {
            m.Set(t, h);
            if (symmetric && m.Contains(h.Rotate180())) m.Set(t, h.Rotate180());
        }

        static bool NewProblems(TopologyReport before, TopologyReport after) =>
            after.DeadEnds.Count > before.DeadEnds.Count || after.Unreachable.Count > before.Unreachable.Count
            || after.Chokepoints.Count(c => c.Kind == ChokeKind.Mandatory) > before.Chokepoints.Count(c => c.Kind == ChokeKind.Mandatory);

        /// <summary>The blocking hex most responsible for the first failure (null when no blocker is involved).</summary>
        public static Hex? Culprit(MapDefinition map, TopologyReport rep)
        {
            var problem = new HashSet<Hex>();
            problem.UnionWith(rep.Unreachable);
            foreach (var d in rep.DeadEnds.Where(d => !d.Intentional || rep.DeadEnds.Count > TopologyRules.Default.MaxDeadEnds))
            { problem.UnionWith(d.Hexes); problem.UnionWith(d.Entrance); }
            foreach (var c in rep.Chokepoints.Where(c => c.Kind == ChokeKind.Mandatory)) problem.UnionWith(c.Hexes);
            foreach (var p in rep.Profiles) problem.UnionWith(p.Unreachable);
            bool hazardIssue = rep.Profiles.Any(p => p.Profile == MovementProfile.HazardAverse.Name && p.Errors.Count > 0);
            Func<Hex, bool> isBlocker = h => TerrainRules.BlocksMovement(map.Terrain(h)) || (hazardIssue && TerrainRules.Of(map.Terrain(h)).Damaging);
            var blockers = map.AllHexes.Where(isBlocker).ToList();
            if (blockers.Count == 0) return null;
            if (problem.Count == 0)
            {
                // Global failures (density, approaches, cross-links): the blocker closest to the centre line goes first.
                return blockers.OrderBy(h => Math.Abs(h.Lateral) + Math.Abs(h.R) * 2).ThenBy(h => h).First();
            }
            return blockers.OrderByDescending(h => h.Neighbors().Count(problem.Contains))
                           .ThenBy(h => problem.Min(p => Hex.Distance(p, h)))
                           .ThenBy(h => h).First();
        }
    }

    /// <summary>
    /// Static all-pairs path lengths (in steps) over walkable terrain, computed once per map. Lets the AI measure "how far
    /// is it really" around obstacles in O(1); temporary blockers (units, bulwarks) are layered on top per decision.
    /// </summary>
    public sealed class MapPaths
    {
        readonly Dictionary<Hex, int> _index = new Dictionary<Hex, int>();
        readonly short[] _dist;
        readonly int _n;
        public const int Unreachable = short.MaxValue;

        /// <summary>Hexes in index order (the map's hex order).</summary>
        public readonly Hex[] Nodes;
        /// <summary>Neighbour indices, 6 per hex (−1 off the board).</summary>
        public readonly int[] Adjacent;
        /// <summary>True where static terrain lets units through.</summary>
        public readonly bool[] Walkable;
        public int Count => _n;
        public int IndexOf(Hex h) => _index.TryGetValue(h, out int i) ? i : -1;

        public MapPaths(MapDefinition map)
        {
            var nodes = map.AllHexes.ToList();
            _n = nodes.Count;
            Nodes = nodes.ToArray();
            for (int i = 0; i < _n; i++) _index[nodes[i]] = i;
            Adjacent = new int[_n * 6];
            Walkable = new bool[_n];
            for (int i = 0; i < _n; i++)
            {
                Walkable[i] = !TerrainRules.BlocksMovement(map.Terrain(nodes[i]));
                for (int d = 0; d < 6; d++) Adjacent[i * 6 + d] = _index.TryGetValue(nodes[i].Neighbor(d), out int w) ? w : -1;
            }
            _dist = new short[_n * _n];
            var queue = new Queue<int>();
            for (int s = 0; s < _n; s++)
            {
                int row = s * _n;
                for (int i = 0; i < _n; i++) _dist[row + i] = Unreachable;
                // Distances are measured from any hex (even a blocked one, e.g. a unit's target on an obstacle edge) but
                // only walkable hexes are passed through.
                _dist[row + s] = 0; queue.Enqueue(s);
                while (queue.Count > 0)
                {
                    int v = queue.Dequeue();
                    if (v != s && TerrainRules.BlocksMovement(map.Terrain(nodes[v]))) continue;
                    var hv = nodes[v];
                    for (int d = 0; d < 6; d++)
                    {
                        if (!_index.TryGetValue(hv.Neighbor(d), out int w) || _dist[row + w] != Unreachable) continue;
                        _dist[row + w] = (short)(_dist[row + v] + 1);
                        queue.Enqueue(w);
                    }
                }
            }
        }

        /// <summary>Steps from <paramref name="a"/> to <paramref name="b"/> around blocking terrain; hex distance plus a
        /// large penalty when no walkable route exists (or a hex is off the board).</summary>
        public int Distance(Hex a, Hex b)
        {
            if (!_index.TryGetValue(a, out int i) || !_index.TryGetValue(b, out int j)) return Hex.Distance(a, b) + 40;
            int d = _dist[i * _n + j];
            return d == Unreachable ? Hex.Distance(a, b) + 40 : d;
        }
    }
}
