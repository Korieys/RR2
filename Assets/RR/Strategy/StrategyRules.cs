using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>Read-only view of the Commandfall Unit Codex roster for strategic code (names, HP, cost, role).</summary>
    public static class ShipCatalog
    {
        public static UnitDef Def(string id) => RosterContent.Unit(id);
        public static int MaxHp(string id) => Def(id)?.MaxHp ?? 1;
        public static string Name(string id) => Def(id)?.Name ?? id;
        public static int Cost(string id) { var d = Def(id); return d == null ? 0 : RosterContent.Cost(d); }
        public static int Tier(string id) => Def(id)?.Roster?.Tier ?? 0;
        public static bool IsLeader(string id) => Def(id)?.Roster?.IsLeaderRole ?? false;
        public static string Role(string id) => Def(id)?.Roster?.Role.ToString() ?? "";

        /// <summary>Ships the player can commission at a shipyard in this slice (tier I-III, so they are legal
        /// under the Balanced unlock tier), with provisional strategic costs and build times.</summary>
        public static readonly (string defId, Resources cost, int days)[] Commissionable =
        {
            ("silex_prism_escort",     new Resources(40, 90, 30),  8),
            ("silex_lattice_corvette", new Resources(60, 140, 50), 14),
            ("silex_tessera_frigate",  new Resources(70, 170, 60), 18),
            ("silex_pillar_destroyer", new Resources(110, 230, 90), 24),
        };

        public static readonly Resources FoundryCost = new Resources(0, 40, 120);
        public const int FoundryDays = 20;
        public static readonly Resources FoundryYield = new Resources(0, 4, 0);
        public const int MaxFoundries = 2;
    }

    public sealed class StratResult
    {
        public bool Ok;
        public string Reason;
        public static StratResult Accept(string note = null) => new StratResult { Ok = true, Reason = note };
        public static StratResult Reject(string why) => new StratResult { Ok = false, Reason = why };
        public override string ToString() => Ok ? "ok" : "rejected: " + Reason;
    }

    public sealed class ProjectOption
    {
        public ProjectKind Kind;
        public string ShipDefId;
        public string Label;
        public Resources Cost;
        public int Days;
        public bool Available;
        public string UnavailableReason;
    }

    public sealed class RoutePlan
    {
        public bool Legal;
        public string Reason;
        public List<string> Path = new List<string>();  // excludes the start system
        public int Days;
    }

    public sealed class DayReport
    {
        public bool Advanced;
        public string BlockedReason;
        public List<string> Arrivals = new List<string>();     // fleet ids that arrived somewhere
        public List<string> Completed = new List<string>();    // planet ids whose project completed
        public Engagement NewEngagement;
    }

    /// <summary>
    /// Authoritative strategic rules for the slice. Every mutation goes through here; each command validates first and
    /// returns a reason when it refuses. The day loop has a fixed order: economy, projects, AI intent, movement, contact.
    /// Strategic time model (provisional, documented): a pausable real-time clock that advances whole days.
    /// </summary>
    public static class StrategyRules
    {
        // ================================================================== management

        public static List<ProjectOption> ProjectOptions(GalaxyState g, string planetId)
        {
            var list = new List<ProjectOption>();
            var p = g.GetPlanet(planetId);
            if (p == null) return list;
            var sys = g.SystemOf(p);
            var player = g.Player;
            string common = p.OwnerId != player.Id ? "This world is not under your rule."
                          : p.Project != null ? "This world is already working on a project."
                          : sys.Blockaded ? "Enemy ships hold the orbit; construction is halted."
                          : g.PendingEngagement != null ? "Resolve the engagement before issuing new projects." : null;
            if (p.HasShipyard)
            {
                foreach (var (defId, cost, days) in ShipCatalog.Commissionable)
                {
                    string why = common ?? (player.Stock.Covers(cost) ? null : "Not enough resources.");
                    list.Add(new ProjectOption { Kind = ProjectKind.CommissionShip, ShipDefId = defId, Label = "Commission " + ShipCatalog.Name(defId),
                        Cost = cost, Days = days, Available = why == null, UnavailableReason = why });
                }
            }
            if (p.Kind == PlanetKind.Crystal)
            {
                string why = common ?? (p.Foundries >= ShipCatalog.MaxFoundries ? "This world has no room for another foundry."
                           : player.Stock.Covers(ShipCatalog.FoundryCost) ? null : "Not enough resources.");
                list.Add(new ProjectOption { Kind = ProjectKind.CrystalFoundry, Label = "Raise a Crystal Foundry", Cost = ShipCatalog.FoundryCost,
                    Days = ShipCatalog.FoundryDays, Available = why == null, UnavailableReason = why });
            }
            return list;
        }

        public static StratResult StartProject(GalaxyState g, string planetId, ProjectKind kind, string shipDefId = null)
        {
            var opt = ProjectOptions(g, planetId).FirstOrDefault(o => o.Kind == kind && (kind != ProjectKind.CommissionShip || o.ShipDefId == shipDefId));
            if (opt == null) return StratResult.Reject("That project is not offered on this world.");
            if (!opt.Available) return StratResult.Reject(opt.UnavailableReason);
            var p = g.GetPlanet(planetId);
            var player = g.Player;
            player.Stock = player.Stock - opt.Cost;
            p.Project = new ProjectState { Kind = kind, ShipDefId = shipDefId, Cost = opt.Cost, DaysTotal = opt.Days };
            g.Note("build", $"{p.Name} began work: {opt.Label} ({opt.Days} days).");
            return StratResult.Accept();
        }

        public static StratResult CancelProject(GalaxyState g, string planetId)
        {
            var p = g.GetPlanet(planetId);
            if (p == null || p.Project == null) return StratResult.Reject("There is no project to cancel.");
            if (p.OwnerId != g.Player.Id) return StratResult.Reject("This world is not under your rule.");
            g.Player.Stock = g.Player.Stock + p.Project.Cost.Half();
            g.Note("build", $"{p.Name} cancelled its project; half of the resources were recovered.");
            p.Project = null;
            return StratResult.Accept();
        }

        public static Resources DailyIncome(GalaxyState g, string polityId)
        {
            var total = default(Resources);
            foreach (var s in g.Systems)
                foreach (var p in s.Planets)
                    if (p.OwnerId == polityId && !s.Blockaded)
                        total = total + p.DailyYield + new Resources(0, ShipCatalog.FoundryYield.Materials * p.Foundries, 0);
            return total;
        }

        // ================================================================== fleets

        public static bool Hostile(string a, string b) => a != null && b != null && a != b;

        public static bool CanFight(Fleet f) => f.Ships.Any(s => ShipCatalog.IsLeader(s.DefId));

        public static RoutePlan PlanRoute(GalaxyState g, Fleet f, string targetId)
        {
            var plan = new RoutePlan();
            if (f == null) { plan.Reason = "No fleet selected."; return plan; }
            if (g.GetSystem(targetId) == null) { plan.Reason = "Unknown destination."; return plan; }
            if (f.EngagementId != null) { plan.Reason = "This fleet is committed to an engagement."; return plan; }
            string start = f.SystemId;
            if (f.Moving && f.Order.DaysIntoLane > 0) { plan.Reason = "This fleet is between systems; it can be redirected when it arrives."; return plan; }
            if (start == targetId) { plan.Reason = "The fleet is already there."; return plan; }
            // Dijkstra over lanes by travel days (ties broken by id for determinism).
            var dist = new Dictionary<string, int> { [start] = 0 };
            var prev = new Dictionary<string, string>();
            var open = new SortedSet<(int d, string id)> { (0, start) };
            while (open.Count > 0)
            {
                var (d, id) = open.Min; open.Remove(open.Min);
                if (id == targetId) break;
                if (d > dist[id]) continue;
                foreach (var lane in g.LanesOf(id))
                {
                    var n = lane.Other(id);
                    int nd = d + lane.Days;
                    if (!dist.TryGetValue(n, out var cur) || nd < cur)
                    {
                        if (dist.ContainsKey(n)) open.Remove((cur, n));
                        dist[n] = nd; prev[n] = id; open.Add((nd, n));
                    }
                }
            }
            if (!dist.ContainsKey(targetId)) { plan.Reason = "No jump route connects these systems."; return plan; }
            var path = new List<string>();
            for (var at = targetId; at != start; at = prev[at]) path.Add(at);
            path.Reverse();
            plan.Path = path; plan.Days = dist[targetId]; plan.Legal = true;
            return plan;
        }

        public static StratResult OrderMove(GalaxyState g, string fleetId, string targetId, bool asAI = false)
        {
            var f = g.GetFleet(fleetId);
            if (f == null) return StratResult.Reject("No fleet selected.");
            if (!asAI && f.OwnerId != g.Player.Id) return StratResult.Reject("That fleet does not answer to you.");
            if (!asAI && g.PendingEngagement != null) return StratResult.Reject("Resolve the engagement before issuing new orders.");
            var plan = PlanRoute(g, f, targetId);
            if (!plan.Legal) return StratResult.Reject(plan.Reason);
            f.Order = new MoveOrder { Path = plan.Path, DaysIntoLane = 0, FromSystemId = f.SystemId };
            g.Note("order", $"{f.Name} ordered to {g.GetSystem(targetId).Name} ({plan.Days} days).");
            return StratResult.Accept();
        }

        public static StratResult CancelMove(GalaxyState g, string fleetId)
        {
            var f = g.GetFleet(fleetId);
            if (f == null || !f.Moving) return StratResult.Reject("The fleet has no movement order.");
            if (f.OwnerId != g.Player.Id) return StratResult.Reject("That fleet does not answer to you.");
            if (f.Order.DaysIntoLane > 0) return StratResult.Reject("The fleet is already in transit; it will stop at the next system.");
            f.Order = null;
            g.Note("order", $"{f.Name} holds at {g.GetSystem(f.SystemId).Name}.");
            return StratResult.Accept();
        }

        // ================================================================== the day loop

        public static DayReport AdvanceDay(GalaxyState g)
        {
            var rep = new DayReport();
            if (g.PendingEngagement != null) { rep.BlockedReason = "An engagement is waiting for a decision."; return rep; }
            g.Day++;
            rep.Advanced = true;

            // 1. Economy
            foreach (var pol in g.Polities.Where(p => p.Playable)) pol.Stock = pol.Stock + DailyIncome(g, pol.Id);

            // 2. Projects
            foreach (var s in g.Systems)
                foreach (var p in s.Planets)
                {
                    if (p.Project == null) continue;
                    if (s.Blockaded) continue;
                    p.Project.DaysDone++;
                    if (p.Project.DaysDone >= p.Project.DaysTotal) { CompleteProject(g, s, p); rep.Completed.Add(p.Id); }
                }

            // 3. AI intent
            foreach (var f in g.Fleets.Where(f => f.AIControlled && f.AITarget != null && !f.Moving && f.EngagementId == null).OrderBy(f => f.Id).ToList())
            {
                if (g.Day < f.AIDepartDay || f.SystemId == f.AITarget) continue;
                if (!CanFight(f)) continue;
                var r = OrderMove(g, f.Id, f.AITarget, asAI: true);
                if (r.Ok) g.Note("danger", $"Scouts report {f.Name} leaving {g.GetSystem(f.SystemId).Name}, bound for {g.GetSystem(f.AITarget).Name}.");
            }

            // 4. Movement (fixed order by id)
            foreach (var f in g.Fleets.Where(f => f.Moving && f.EngagementId == null).OrderBy(f => f.Id, StringComparer.Ordinal).ToList())
            {
                var lane = g.LaneBetween(f.SystemId, f.Order.Path[0]);
                f.Order.DaysIntoLane++;
                if (f.Order.DaysIntoLane < lane.Days) continue;
                f.LastSystemId = f.SystemId;
                f.SystemId = f.Order.Path[0];
                f.Order.Path.RemoveAt(0);
                f.Order.DaysIntoLane = 0;
                if (f.Order.Path.Count == 0) f.Order = null;
                rep.Arrivals.Add(f.Id);
                OnArrival(g, f, rep);
                if (rep.NewEngagement != null) break;
            }

            // 5. Lane interception: hostile fleets crossing the same lane in opposite directions meet.
            if (rep.NewEngagement == null) DetectInterception(g, rep);

            // 6. Friendly fleets holding together merge (player only).
            MergeFleets(g);
            return rep;
        }

        static void CompleteProject(GalaxyState g, StarSystem s, Planet p)
        {
            var proj = p.Project;
            p.Project = null;
            if (proj.Kind == ProjectKind.CrystalFoundry)
            {
                p.Foundries++;
                g.Note("build", $"{p.Name} raised a Crystal Foundry (+{ShipCatalog.FoundryYield.Materials} materials per day).");
                return;
            }
            var fleet = g.FleetsAt(s.Id).Where(f => f.OwnerId == p.OwnerId && f.EngagementId == null).OrderByDescending(f => f.Ships.Count).FirstOrDefault();
            if (fleet == null)
            {
                fleet = new Fleet { Id = "fleet_" + g.NextId++, Name = s.Name + " Guard", OwnerId = p.OwnerId, SystemId = s.Id, HomeSystemId = s.Id, LastSystemId = s.Id };
                g.Fleets.Add(fleet);
            }
            fleet.Ships.Add(new ShipRecord { Uid = g.NextUid++, DefId = proj.ShipDefId, Hp = ShipCatalog.MaxHp(proj.ShipDefId) });
            g.Note("build", $"{ShipCatalog.Name(proj.ShipDefId)} commissioned at {p.Name} and joined {fleet.Name}.");
        }

        static void OnArrival(GalaxyState g, Fleet f, DayReport rep)
        {
            var sys = g.GetSystem(f.SystemId);
            var hostile = g.FleetsAt(sys.Id).Where(o => o != f && Hostile(o.OwnerId, f.OwnerId) && o.EngagementId == null).OrderBy(o => o.Id).FirstOrDefault();
            if (hostile != null)
            {
                f.Order = null;
                rep.NewEngagement = StartEngagement(g, sys, f, hostile);
                return;
            }
            if (Hostile(sys.OwnerId, f.OwnerId) && CanFight(f) && f.Order == null)
            {
                if (sys.OrbitalControlId != f.OwnerId)
                {
                    sys.OrbitalControlId = f.OwnerId;
                    sys.ConquestBy = f.OwnerId;
                    if (sys.Conquest < ConquestStage.OrbitSecured) sys.Conquest = ConquestStage.OrbitSecured;
                    g.Note(f.OwnerId == g.Player.Id ? "result" : "danger",
                        $"{f.Name} took the orbit of {sys.Name} unopposed. Its worlds are blockaded; the planets remain {g.GetPolity(sys.OwnerId).Name}.");
                }
            }
            else if (sys.OwnerId == f.OwnerId && sys.Blockaded && CanFight(f))
            {
                sys.OrbitalControlId = f.OwnerId; sys.ConquestBy = null; sys.Conquest = ConquestStage.None;
                g.Note("result", $"{f.Name} restored control of the orbit above {sys.Name}.");
            }
        }

        static void DetectInterception(GalaxyState g, DayReport rep)
        {
            var movers = g.Fleets.Where(f => f.Moving && f.EngagementId == null && f.Order.DaysIntoLane > 0).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < movers.Count; i++)
                for (int j = i + 1; j < movers.Count; j++)
                {
                    var a = movers[i]; var b = movers[j];
                    if (!Hostile(a.OwnerId, b.OwnerId)) continue;
                    if (a.SystemId != b.Order.Path[0] || b.SystemId != a.Order.Path[0]) continue;
                    var lane = g.LaneBetween(a.SystemId, b.SystemId);
                    if (a.Order.DaysIntoLane + b.Order.DaysIntoLane < lane.Days) continue;
                    // They meet; both drop back to the system nearer the meeting point (ties: a's origin).
                    string at = a.Order.DaysIntoLane <= b.Order.DaysIntoLane ? a.SystemId : b.SystemId;
                    foreach (var f in new[] { a, b })
                    {
                        if (f.SystemId != at) { f.LastSystemId = f.SystemId; f.SystemId = at; }
                        f.Order = null;
                    }
                    var defender = g.GetSystem(at).OwnerId == b.OwnerId ? b : a;
                    var attacker = defender == a ? b : a;
                    g.Note("danger", $"{a.Name} and {b.Name} met in transit and fell back to {g.GetSystem(at).Name}.");
                    rep.NewEngagement = StartEngagement(g, g.GetSystem(at), attacker, defender);
                    return;
                }
        }

        static Engagement StartEngagement(GalaxyState g, StarSystem sys, Fleet attacker, Fleet defender)
        {
            // A side without a flagship cannot form a battle line (Commandfall needs one leader per army).
            if (!CanFight(defender) || !CanFight(attacker))
            {
                var weak = !CanFight(defender) ? defender : attacker;
                var strong = weak == defender ? attacker : defender;
                Withdraw(g, weak, sys);
                g.Note(weak.OwnerId == g.Player.Id ? "danger" : "result", $"{weak.Name} has no flagship to lead a battle line and withdrew from {sys.Name}.");
                if (strong == attacker && Hostile(sys.OwnerId, attacker.OwnerId))
                {
                    sys.OrbitalControlId = attacker.OwnerId; sys.ConquestBy = attacker.OwnerId;
                    if (sys.Conquest < ConquestStage.OrbitSecured) sys.Conquest = ConquestStage.OrbitSecured;
                }
                return null;
            }
            var e = new Engagement { Id = "eng_" + g.NextId++, SystemId = sys.Id, AttackerFleetId = attacker.Id, DefenderFleetId = defender.Id, Day = g.Day,
                State = EngagementState.Pending };
            attacker.EngagementId = e.Id; defender.EngagementId = e.Id;
            attacker.Order = null; defender.Order = null;
            g.Engagements.Add(e);
            g.Note("danger", $"Contact at {sys.Name}: {attacker.Name} engages {defender.Name}.");
            return e;
        }

        /// <summary>Sends a fleet back toward its previous system (or home) and releases it from engagements.</summary>
        public static void Withdraw(GalaxyState g, Fleet f, StarSystem from)
        {
            f.EngagementId = null;
            string back = f.LastSystemId != null && f.LastSystemId != from.Id ? f.LastSystemId : f.HomeSystemId;
            if (back == null || back == from.Id) { f.Order = null; return; }
            var plan = PlanRoute(g, f, back);
            f.Order = plan.Legal ? new MoveOrder { Path = plan.Path, FromSystemId = from.Id } : null;
            if (f.AIControlled) f.AITarget = null;   // a beaten raid goes home and stays there
        }

        /// <summary>The player declines battle: their fleet withdraws and the enemy holds the system's orbit.</summary>
        public static StratResult Retreat(GalaxyState g, string engagementId)
        {
            var e = g.GetEngagement(engagementId);
            if (e == null || e.State != EngagementState.Pending) return StratResult.Reject("There is no pending engagement.");
            var mine = new[] { g.GetFleet(e.AttackerFleetId), g.GetFleet(e.DefenderFleetId) }.FirstOrDefault(f => f.OwnerId == g.Player.Id);
            if (mine == null) return StratResult.Reject("None of your fleets is in this engagement.");
            var other = mine.Id == e.AttackerFleetId ? g.GetFleet(e.DefenderFleetId) : g.GetFleet(e.AttackerFleetId);
            var sys = g.GetSystem(e.SystemId);
            Withdraw(g, mine, sys);
            other.EngagementId = null;
            e.State = EngagementState.Retreated;
            e.ResultSummary = $"{mine.Name} withdrew from {sys.Name} without a battle.";
            if (Hostile(sys.OwnerId, other.OwnerId))
            {
                sys.OrbitalControlId = other.OwnerId; sys.ConquestBy = other.OwnerId;
                if (sys.Conquest < ConquestStage.OrbitSecured) sys.Conquest = ConquestStage.OrbitSecured;
            }
            g.Note("danger", e.ResultSummary + (sys.OwnerId == mine.OwnerId ? $" {other.Name} now holds the orbit." : ""));
            return StratResult.Accept();
        }

        static void MergeFleets(GalaxyState g)
        {
            foreach (var group in g.Fleets.Where(f => !f.Moving && f.EngagementId == null && !f.AIControlled && f.Ships.Count > 0)
                         .GroupBy(f => (f.OwnerId, f.SystemId)).Where(gr => gr.Count() > 1).ToList())
            {
                var ordered = group.OrderByDescending(f => f.Ships.Any(s => ShipCatalog.IsLeader(s.DefId))).ThenByDescending(f => f.Ships.Count).ThenBy(f => f.Id).ToList();
                var main = ordered[0];
                foreach (var other in ordered.Skip(1))
                {
                    main.Ships.AddRange(other.Ships);
                    other.Ships.Clear();
                    g.Note("order", $"{other.Name} joined {main.Name} at {g.GetSystem(main.SystemId).Name}.");
                }
            }
            g.Fleets.RemoveAll(f => f.Ships.Count == 0 && f.EngagementId == null);
        }
    }
}