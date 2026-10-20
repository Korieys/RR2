using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    /// <summary>One system's supply state for one polity, with the reason a player can read.</summary>
    public sealed class SupplyNode
    {
        public string SystemId;
        public bool Supplied;
        /// <summary>The previous system on the supply path (null at a source).</summary>
        public string Via;
        public int Days;                 // lane days from the nearest source
        public string Reason;            // why it is cut, or why it is supplied
        public string Threat;            // a known threat to it, or null
    }

    /// <summary>A polity's supply network, derived from routes, political access and blockades (campaign alpha Prompt 15).</summary>
    public sealed class SupplyNetwork
    {
        public string Polity;
        public Dictionary<string, SupplyNode> Nodes = new Dictionary<string, SupplyNode>(StringComparer.Ordinal);
        public List<string> Sources = new List<string>();
        public bool Has(string systemId) => Nodes.TryGetValue(systemId, out var n) && n.Supplied;
        public SupplyNode Node(string systemId) => Nodes.TryGetValue(systemId, out var n) ? n : null;
        /// <summary>The supply path from a source to <paramref name="systemId"/>, source first, or empty when it is cut.</summary>
        public List<string> Path(string systemId)
        {
            var path = new List<string>();
            for (var n = Node(systemId); n != null && n.Supplied; n = n.Via != null ? Node(n.Via) : null) { path.Add(n.SystemId); if (n.Via == null) break; }
            path.Reverse();
            return path;
        }
    }

    /// <summary>
    /// Supply is derived, never stored: every call recomputes it from the same public map facts (system ownership, orbital
    /// control, passage access, truces and fleets holding position). The strategic map has no fog of war in this slice; if a
    /// later prompt adds one, <see cref="Passable"/> must read only known fleet positions. Transport capacity is not modelled.
    /// </summary>
    public static class Supply
    {
        /// <summary>Systems that originate supply: the polity's own, unblockaded systems with a shipyard or a completed forward dock.</summary>
        public static List<string> SourcesOf(GalaxyState g, string polity)
        {
            var list = new List<string>();
            foreach (var s in g.Systems.Where(s => PassageWar.InScope(g, s.Id)).OrderBy(s => s.Id, StringComparer.Ordinal))
            {
                if (s.Blockaded || !s.Planets.Any(p => p.OwnerId == polity)) continue;
                if (s.Planets.Any(p => p.OwnerId == polity && p.HasShipyard) || Economy.HasDock(g, polity, s.Id)) list.Add(s.Id);
            }
            return list;
        }

        /// <summary>Whether supply can pass through (or reach) a system, and why not.</summary>
        public static bool Passable(GalaxyState g, string polity, StarSystem s, out string reason)
        {
            reason = null;
            var blocker = g.Fleets.Where(f => f.SystemId == s.Id && !f.Moving && f.Ships.Count > 0 && StrategyRules.Hostile(f.OwnerId, polity) && !PassageWar.AtPeace(g, f.OwnerId, polity))
                                  .OrderBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
            if (blocker != null) { reason = $"{blocker.Name} ({g.GetPolity(blocker.OwnerId).Name}) holds position at {s.Name}"; return false; }
            if (s.OrbitalControlId != null && StrategyRules.Hostile(s.OrbitalControlId, polity) && s.OwnerId != null && !PassageWar.AtPeace(g, s.OrbitalControlId, polity))
            {
                if (s.OwnerId == polity || s.OrbitalControlId != s.OwnerId) { reason = $"{g.GetPolity(s.OrbitalControlId).Name} holds the orbit of {s.Name}"; return false; }
            }
            var passage = g.Slice?.Passage(s.Id);
            if (passage != null && passage.AccessHolderId != polity)
            {
                reason = passage.AccessHolderId == null ? $"{passage.Name} is contested; supply needs access" : $"access through {passage.Name} belongs to {g.GetPolity(passage.AccessHolderId).Name}";
                return false;
            }
            if (s.OwnerId != null && s.OwnerId != polity && g.GetPolity(s.OwnerId)?.Playable == true && !PassageWar.AtPeace(g, s.OwnerId, polity))
            { reason = $"{s.Name} is {g.GetPolity(s.OwnerId).Name} territory"; return false; }
            return true;
        }

        public static SupplyNetwork Of(GalaxyState g, string polity)
        {
            var net = new SupplyNetwork { Polity = polity };
            net.Sources.AddRange(SourcesOf(g, polity));
            var dist = new Dictionary<string, int>(StringComparer.Ordinal);
            var open = new SortedSet<(int d, string id)>();
            foreach (var src in net.Sources)
            {
                dist[src] = 0; open.Add((0, src));
                net.Nodes[src] = new SupplyNode { SystemId = src, Supplied = true, Days = 0, Reason = $"a supply source ({g.GetSystem(src).Name})" };
            }
            while (open.Count > 0)
            {
                var (d, id) = open.Min; open.Remove(open.Min);
                if (d > dist[id]) continue;
                foreach (var lane in g.LanesOf(id).OrderBy(l => l.Other(id), StringComparer.Ordinal))
                {
                    var nid = lane.Other(id);
                    if (!PassageWar.InScope(g, nid)) continue;
                    var ns = g.GetSystem(nid);
                    if (!Passable(g, polity, ns, out var why))
                    {
                        if (!net.Nodes.ContainsKey(nid)) net.Nodes[nid] = new SupplyNode { SystemId = nid, Supplied = false, Reason = why };
                        continue;
                    }
                    int nd = d + lane.Days;
                    if (dist.TryGetValue(nid, out var cur) && nd >= cur) continue;
                    if (dist.ContainsKey(nid)) open.Remove((cur, nid));
                    dist[nid] = nd; open.Add((nd, nid));
                    net.Nodes[nid] = new SupplyNode { SystemId = nid, Supplied = true, Via = id, Days = nd, Reason = Root(net, id) == id ? $"supplied from {g.GetSystem(id).Name}" : $"supplied from {g.GetSystem(Root(net, id)).Name} via {g.GetSystem(id).Name}" };
                }
            }
            foreach (var s in g.Systems.Where(s => PassageWar.InScope(g, s.Id) && !net.Nodes.ContainsKey(s.Id)))
            {
                Passable(g, polity, s, out var why);
                net.Nodes[s.Id] = new SupplyNode { SystemId = s.Id, Supplied = false, Reason = why ?? "no supplied route reaches it" };
            }
            // Known threats: hostile fleets on the map whose orders lead through a supplied system.
            foreach (var f in g.Fleets.Where(f => f.Moving && f.Ships.Count > 0 && StrategyRules.Hostile(f.OwnerId, polity) && !PassageWar.AtPeace(g, f.OwnerId, polity)).OrderBy(f => f.Id, StringComparer.Ordinal))
            {
                int eta = 0; string at = f.SystemId; bool first = true;
                foreach (var next in f.Order.Path)
                {
                    var lane = g.LaneBetween(at, next);
                    eta += (lane?.Days ?? 0) - (first ? f.Order.DaysIntoLane : 0);
                    first = false;
                    if (net.Nodes.TryGetValue(next, out var n) && n.Supplied && n.Threat == null)
                        n.Threat = $"{f.Name} ({g.GetPolity(f.OwnerId).Name}) is expected at {g.GetSystem(next).Name} on day {g.Day + Math.Max(1, eta)}";
                    at = next;
                }
            }
            return net;
        }

        static string Root(SupplyNetwork net, string id)
        {
            var n = net.Node(id);
            while (n != null && n.Via != null) n = net.Node(n.Via);
            return n?.SystemId ?? id;
        }

        /// <summary>One line per relevant system for the overlay legend and the supply window.</summary>
        public static List<string> Describe(GalaxyState g, string polity)
        {
            var net = Of(g, polity);
            var lines = new List<string>();
            foreach (var id in PassageWar.Of(g)?.ActiveSystems ?? new List<string>())
            {
                var n = net.Node(id);
                if (n == null) continue;
                var name = g.GetSystem(id).Name;
                lines.Add(n.Supplied ? $"{name}: supplied ({n.Reason}, {n.Days} days)" + (n.Threat != null ? $". Threatened: {n.Threat}." : ".")
                                     : $"{name}: cut ({n.Reason}).");
            }
            return lines;
        }
    }
}
