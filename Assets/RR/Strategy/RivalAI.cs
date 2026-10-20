using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    // ======================================================================================================
    // Campaign alpha Prompt 17: a bounded strategic rival. It decides from a belief state built only from what its side can
    // know (the map's public facts and its own books) and acts only through the commands the player uses: OrderMove,
    // DeclareObjective, StartProject, Repair, ProposeTreaty, the AI's answer to treaties. No hidden income, no spawning,
    // no random adjustment, no rubber band. Deliberation is a fixed list of candidate plans scored by integer formulas with
    // a stable tie-break, and a commitment rule against oscillation. Reasons go to a developer log, never to the player UI.
    // ======================================================================================================

    /// <summary>What one side may know about another: positions, classes and hulls of fleets on the map, orbits, access,
    /// relations, history. Not: the other side's orders beyond the lane a fleet is visibly on, its treasury, its projects,
    /// its fitted modules, its dock stores, or any future outcome.</summary>
    public sealed class Belief
    {
        public sealed class SeenFleet
        {
            public string Id, OwnerId, SystemId, NextSystemId;   // NextSystemId: only the lane it is visibly on
            public bool Moving, CanFight;
            public int Ships, Hull;
        }

        public string Polity;
        public int Day;
        public List<SeenFleet> Fleets = new List<SeenFleet>();

        public static Belief Of(GalaxyState g, string polity)
        {
            var b = new Belief { Polity = polity, Day = g.Day };
            foreach (var f in g.Fleets.Where(f => f.Ships.Count > 0).OrderBy(f => f.Id, StringComparer.Ordinal))
                b.Fleets.Add(new SeenFleet
                {
                    Id = f.Id, OwnerId = f.OwnerId, SystemId = f.SystemId, Moving = f.Moving,
                    NextSystemId = f.Moving ? f.Order.Path[0] : null,          // the lane it is on, never its destination
                    CanFight = StrategyRules.CanFight(f), Ships = f.Ships.Count, Hull = f.Ships.Sum(s => s.Hp),
                });
            return b;
        }

        public SeenFleet MainOf(string polity) => Fleets.Where(f => f.OwnerId == polity && f.CanFight).OrderByDescending(f => f.Hull).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();
    }

    /// <summary>Faction priorities grounded in the implemented doctrines (provisional values). They change thresholds and
    /// weights, so they change choices, not only words.</summary>
    public sealed class RivalDoctrine
    {
        public string Polity;
        /// <summary>Own strength as a percentage of the enemy's needed before attacking.</summary>
        public int AttackRatio;
        public int Aggression, Industry, Commerce, TreatyWill;
        public string[] PreferredPriorities;

        public static RivalDoctrine For(string polity) => polity == "pygmaei"
            ? new RivalDoctrine { Polity = polity, AttackRatio = 85, Aggression = 20, Industry = 15, Commerce = 0, TreatyWill = 0, PreferredPriorities = new[] { "ore_drive", "forward_dock" } }
            : new RivalDoctrine { Polity = polity, AttackRatio = 120, Aggression = 0, Industry = 0, Commerce = 20, TreatyWill = 25, PreferredPriorities = new[] { "convoy_office", "underwriting", "ore_contract" } };
    }

    public sealed class RivalPlan
    {
        public string Id, Kind, Target, Reason;
        public int Score, Payment;
    }

    public static class RivalAI
    {
        public const int Hysteresis = 30, ReserveSettlements = 2;

        static ScenarioState Sc(GalaxyState g) => PassageWar.Of(g);
        static string Other(string p) => PassageWar.Other(p);

        public static void Log(GalaxyState g, string polity, string text)
        {
            var sc = Sc(g);
            if (sc == null) return;
            sc.AiLog.Add($"day {g.Day} {polity}: {text}");
            if (sc.AiLog.Count > 400) sc.AiLog.RemoveRange(0, sc.AiLog.Count - 400);
        }

        /// <summary>One strategic day for every AI-controlled power in the scenario (called inside AdvanceDay, before movement).</summary>
        public static void Daily(GalaxyState g)
        {
            if (!PassageWar.Running(g) || Operations.Active(g) != null || g.PendingEngagement != null) return;
            foreach (var pol in new[] { "inhumanus", "pygmaei" })
                if (pol != g.Player.Id || Sc(g).AutoPlayer) Act(g, pol);
        }

        /// <summary>Plans and acts for one power: at most one economic action and one movement plan a day.</summary>
        public static void Act(GalaxyState g, string polity)
        {
            var belief = Belief.Of(g, polity);
            var doctrine = RivalDoctrine.For(polity);
            var econ = EconomicPlans(g, belief, doctrine).Concat(SettlementPlans(g, belief, doctrine)).OrderByDescending(p => p.Score).ThenBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
            if (econ != null && econ.Score > 0) Execute(g, polity, econ);
            var moves = MovementPlans(g, belief, doctrine).OrderByDescending(p => p.Score).ThenBy(p => p.Id, StringComparer.Ordinal).ToList();
            var best = moves.FirstOrDefault();
            if (best == null) return;
            var sc = Sc(g);
            sc.AiPlan.TryGetValue(polity, out var current);
            var currentPlan = current != null ? moves.FirstOrDefault(m => m.Id == current) : null;
            // Commitment: keep a plan still under way unless another is clearly better (no route oscillation).
            if (currentPlan != null && currentPlan.Id != best.Id && best.Score < currentPlan.Score + Hysteresis && Fleet(g, polity)?.Moving == true) best = currentPlan;
            if (best.Id != current) { sc.AiPlan[polity] = best.Id; Log(g, polity, $"plan {best.Id} ({best.Score}): {best.Reason}"); }
            Execute(g, polity, best);
        }

        static Fleet Fleet(GalaxyState g, string polity) => g.Fleets.Where(f => f.OwnerId == polity && (f.AIControlled || (polity == g.Player.Id && Sc(g).AutoPlayer)) && f.Ships.Count > 0)
            .OrderByDescending(f => StrategyRules.CanFight(f)).ThenByDescending(f => f.Ships.Sum(s => s.Hp)).ThenBy(f => f.Id, StringComparer.Ordinal).FirstOrDefault();

        static int Days(GalaxyState g, Fleet f, string target)
        {
            if (f == null) return 999;
            if (f.SystemId == target && !f.Moving) return 0;
            var plan = StrategyRules.PlanRoute(g, new Fleet { Id = f.Id, OwnerId = f.OwnerId, SystemId = f.Moving ? f.Order.Path[0] : f.SystemId }, target);
            return plan.Legal ? plan.Days : 999;
        }

        static int DaysFor(GalaxyState g, Belief.SeenFleet f, string target)
        {
            if (f == null) return 999;
            var from = f.Moving ? f.NextSystemId : f.SystemId;
            if (from == target) return 0;
            var plan = StrategyRules.PlanRoute(g, new Fleet { Id = "belief", OwnerId = f.OwnerId, SystemId = from }, target);
            return plan.Legal ? plan.Days : 999;
        }

        // ------------------------------------------------------------------ candidate plans

        static IEnumerable<RivalPlan> EconomicPlans(GalaxyState g, Belief b, RivalDoctrine d)
        {
            string me = b.Polity;
            var pol = g.GetPolity(me);
            if (Economy.InArrears(g, me)) yield break;
            var reserve = new Resources(0, 0, Economy.Upkeep(g, me) * ReserveSettlements, 0);
            var fleet = Fleet(g, me);
            var yard = g.GetSystem(SliceCampaign.HomeOf(me)).Planets.FirstOrDefault(p => p.HasShipyard && p.OwnerId == me);
            if (yard != null && yard.Project == null && !g.GetSystem(SliceCampaign.HomeOf(me)).Blockaded)
            {
                var opts = StrategyRules.ProjectOptionsFor(g, yard.Id, me);
                var flagship = opts.FirstOrDefault(o => o.Kind == ProjectKind.CommissionShip && ShipCatalog.IsLeader(o.ShipDefId));
                if (flagship != null && (fleet == null || !StrategyRules.CanFight(fleet)) && flagship.Available)
                    yield return new RivalPlan { Id = "commission:" + flagship.ShipDefId, Kind = "commission", Target = flagship.ShipDefId, Score = 100, Reason = "no flagship leads our fleet; a replacement is affordable" };
                var escort = opts.Where(o => o.Kind == ProjectKind.CommissionShip && !ShipCatalog.IsLeader(o.ShipDefId) && o.Available && pol.Stock.Covers(o.Cost + reserve))
                                 .OrderByDescending(o => ShipCatalog.Cost(o.ShipDefId)).ThenBy(o => o.ShipDefId, StringComparer.Ordinal).FirstOrDefault();
                int ships = g.Fleets.Where(f => f.OwnerId == me).Sum(f => f.Ships.Count);
                if (escort != null && ships < MatchConfig.RosterMaxUnits)
                    yield return new RivalPlan { Id = "commission:" + escort.ShipDefId, Kind = "commission", Target = escort.ShipDefId, Score = 30 + d.Industry,
                        Reason = $"{ships} hulls; an escort is affordable with {ReserveSettlements} settlements of upkeep in reserve" };
            }
            foreach (var pr in d.PreferredPriorities.Select(id => Economy.Priority(me, id)).Where(x => x != null && !Economy.Built(g, me, x.Id)))
            {
                var planet = g.GetSystem(pr.SystemId).Planets.Where(p => p.OwnerId == me).OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
                if (planet == null) continue;
                var opt = StrategyRules.ProjectOptionsFor(g, planet.Id, me).FirstOrDefault(o => o.PriorityId == pr.Id);
                if (opt == null || !opt.Available || !pol.Stock.Covers(opt.Cost + reserve)) continue;
                int w = Array.IndexOf(d.PreferredPriorities, pr.Id);
                yield return new RivalPlan { Id = "priority:" + pr.Id, Kind = "priority", Target = pr.Id, Score = 25 + Math.Max(d.Industry, d.Commerce) - w * 5, Reason = $"doctrine favours {pr.Name}" };
            }
            // Doctrine (Prompt 18): the next project on the faction's preferred path, kept affordable above the upkeep reserve.
            string prefer = me == "pygmaei" ? "industry" : "commerce";
            var pathNow = Doctrines.PathOf(g, me) ?? prefer;
            var next = Doctrines.All.Where(x => x.Polity == me && x.Path == pathNow && !Doctrines.Has(g, me, x.Id)).OrderBy(x => x.Tier).FirstOrDefault();
            if (next != null && Doctrines.Problem(g, me, next) == null && pol.Stock.Covers(Doctrines.Price(g, me, next).cost + reserve))
                yield return new RivalPlan { Id = "research:" + next.Id, Kind = "research", Target = next.Id, Score = 28 + Math.Max(d.Industry, d.Commerce),
                    Reason = $"{Doctrines.PathNames[pathNow]}: {next.Name}" };
            // Treaties: a commercial power buys passage rights when fighting looks poor; never during a truce.
            var p0 = g.Slice.Passage(SliceCampaign.PassageSystem);
            if (d.TreatyWill > 0 && p0.AccessHolderId != me && !PassageWar.AtPeace(g, me, Other(me)))
            {
                var mine = b.MainOf(me); var theirs = b.MainOf(Other(me));
                int ratio = theirs == null ? 999 : (mine?.Hull ?? 0) * 100 / Math.Max(1, theirs.Hull);
                if (ratio < d.AttackRatio || PassageWar.Attempts(g, me) >= 2)
                {
                    int pay = Math.Min(PassageWar.MaxPayment, Math.Max(0, pol.Stock.Credits - reserve.Credits)) / 25 * 25;
                    if (PassageWar.CanPropose(g, me, pay) == null)
                        yield return new RivalPlan { Id = "treaty:" + pay, Kind = "treaty", Payment = pay, Score = 30 + d.TreatyWill,
                            Reason = $"our fleet is at {ratio}% of theirs; rights bought for {pay} credits cost less than a battle we may lose" };
                }
            }
        }

        /// <summary>A power that holds the passage but is weaker, and values settlements, offers the player a status-quo truce
        /// (the disclosed negotiated end): a war that the stronger side cannot finish need not run until the mandate expires.</summary>
        static IEnumerable<RivalPlan> SettlementPlans(GalaxyState g, Belief b, RivalDoctrine d)
        {
            string me = b.Polity;
            var p0 = g.Slice.Passage(SliceCampaign.PassageSystem);
            if (d.TreatyWill <= 0 || p0.AccessHolderId != me || me == g.Player.Id || Sc(g).AutoPlayer || Sc(g).Pending != null) yield break;
            if (PassageWar.AtPeace(g, me, Other(me)) || g.Slice.RelationOf(me, Other(me))?.Status != "war") yield break;
            var mine = b.MainOf(me); var theirs = b.MainOf(Other(me));
            if (theirs == null) yield break;
            int ratio = (mine?.Hull ?? 0) * 100 / Math.Max(1, theirs.Hull);
            if (ratio < 100 && (!Sc(g).LastProposal.TryGetValue(me, out var last) || g.Day >= last + Sc(g).Interval))
                yield return new RivalPlan { Id = "statusquo", Kind = "statusquo", Score = 30 + d.TreatyWill, Reason = $"we hold the passage but our fleet is at {ratio}% of theirs" };
        }

        static IEnumerable<RivalPlan> MovementPlans(GalaxyState g, Belief b, RivalDoctrine d)
        {
            string me = b.Polity, them = Other(me);
            var fleet = Fleet(g, me);
            yield return new RivalPlan { Id = "hold", Kind = "hold", Score = 20, Reason = "nothing better to do" };
            if (fleet == null || !StrategyRules.CanFight(fleet)) yield break;
            var home = SliceCampaign.HomeOf(me);
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            var mine = b.MainOf(me); var theirs = b.MainOf(them);
            int ratio = theirs == null ? 999 : mine.Hull * 100 / Math.Max(1, theirs.Hull);
            var flag = fleet.Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            int hullPct = flag.Hp * 100 / Math.Max(1, SliceCampaign.MaxHull(g, flag));
            bool truce = PassageWar.AtPeace(g, me, them);

            // Repair or withdraw: a badly damaged flagship goes home to the shipyard.
            if (hullPct < 60)
                yield return new RivalPlan { Id = "repair", Kind = "repair", Target = home, Score = 60 + (100 - hullPct) / 2, Reason = $"flagship hull at {hullPct}%" };

            // Defend critical access: hold the passage when an enemy fleet that can fight is close to it.
            if (p.AccessHolderId == me && !truce)
            {
                int threatDays = DaysFor(g, theirs, p.SystemId);
                if (theirs != null && threatDays <= 25)
                    yield return new RivalPlan { Id = "defend:" + p.SystemId, Kind = "move", Target = p.SystemId, Score = ratio >= 70 ? 80 : 40,
                        Reason = $"their main fleet is {threatDays} days from the passage we hold; our strength is {ratio}% of theirs" };
            }

            // Contest the passage: only with a legal objective, attempts left, no truce, and enough strength.
            if (p.AccessHolderId != me && !truce && PassageWar.Attempts(g, me) < SliceCampaign.MaxAttempts && hullPct >= 60)
            {
                int score = ratio >= d.AttackRatio ? 50 + d.Aggression : 10;
                yield return new RivalPlan { Id = "contest:" + p.SystemId, Kind = "contest", Target = p.SystemId, Score = score,
                    Reason = $"strength {ratio}% against our threshold {d.AttackRatio}%" + (score <= 10 ? ": too weak to attack" : "") };
            }

            // Exploit a known overextension: their main fleet is far from a system their supply to the passage runs through,
            // and nothing of theirs guards it. Holding it there cuts their supply (and their hold) at no battle.
            if (!truce && p.AccessHolderId == them && theirs != null)
            {
                var net = Supply.Of(g, them);
                foreach (var node in net.Path(p.SystemId).Where(id => id != p.SystemId && !net.Sources.Contains(id)))
                {
                    if (b.Fleets.Any(f => f.OwnerId == them && f.SystemId == node && !f.Moving)) continue;
                    int theirDays = DaysFor(g, theirs, node), myDays = Days(g, fleet, node);
                    // Overextended: their main fleet is at least 20 days from the node, and we can reach it within 45.
                    if (myDays > 45 || theirDays < 20) continue;
                    yield return new RivalPlan { Id = "raid:" + node, Kind = "move", Target = node, Score = 60 + d.Aggression - myDays / 5,
                        Reason = $"their supply to the passage runs through {g.GetSystem(node).Name}, unguarded; we arrive in {myDays} days, their fleet needs {theirDays}" };
                }
            }

            // Guard home: a designated world of ours is threatened or blockaded.
            var hurt = PassageWar.DesignatedProblem(g, me);
            if (hurt != null && !truce)
            {
                var sysId = Sc(g).Designated[me].FirstOrDefault(id => g.GetSystem(id).Blockaded || !g.GetSystem(id).Planets.Any(x => x.OwnerId == me));
                if (sysId != null && ratio >= 60)
                    yield return new RivalPlan { Id = "relieve:" + sysId, Kind = "move", Target = sysId, Score = 70, Reason = hurt };
            }
        }

        // ------------------------------------------------------------------ acting through the ordinary commands

        static void Execute(GalaxyState g, string polity, RivalPlan plan)
        {
            var fleet = Fleet(g, polity);
            switch (plan.Kind)
            {
                case "commission":
                case "priority":
                {
                    string planet = plan.Kind == "commission"
                        ? g.GetSystem(SliceCampaign.HomeOf(polity)).Planets.First(p => p.HasShipyard && p.OwnerId == polity).Id
                        : g.GetSystem(Economy.Priority(polity, plan.Target).SystemId).Planets.Where(p => p.OwnerId == polity).OrderBy(p => p.Id, StringComparer.Ordinal).First().Id;
                    var r = StrategyRules.StartProjectFor(g, planet, plan.Kind == "commission" ? ProjectKind.CommissionShip : ProjectKind.Priority, plan.Target, polity);
                    Log(g, polity, $"{plan.Id}: {(r.Ok ? "started" : r.Reason)} ({plan.Reason})");
                    break;
                }
                case "research":
                {
                    var r = Doctrines.Start(g, polity, plan.Target);
                    Log(g, polity, $"{plan.Id}: {r} ({plan.Reason})");
                    break;
                }
                case "statusquo":
                {
                    var r = PassageWar.ProposeStatusQuo(g, polity, plan.Reason);
                    Log(g, polity, $"{plan.Id}: {r} ({plan.Reason})");
                    break;
                }
                case "treaty":
                {
                    var r = PassageWar.ProposeTreaty(g, polity, plan.Payment, out var v);
                    Log(g, polity, $"{plan.Id}: {(r.Ok ? (v.Accepted ? "accepted" : "declined") : r.Reason)} ({plan.Reason})");
                    break;
                }
                case "repair":
                    if (fleet == null) break;
                    if (fleet.SystemId == plan.Target && !fleet.Moving)
                    {
                        var flag = fleet.Ships.First(s => ShipCatalog.IsLeader(s.DefId));
                        var r = SliceCampaign.Repair(g, flag.Uid);
                        if (r.Ok) Log(g, polity, "repaired its flagship");
                    }
                    else MoveTo(g, fleet, plan.Target);
                    break;
                case "contest":
                    if (fleet == null) break;
                    if (!g.Slice.Objectives.Any(o => o.PolityId == polity && o.SystemId == plan.Target && o.Status == ObjectiveStatus.Declared))
                    {
                        var r = SliceCampaign.DeclareObjective(g, polity, plan.Target);
                        Log(g, polity, "declared: " + r);
                    }
                    MoveTo(g, fleet, plan.Target);
                    break;
                case "move":
                    if (fleet != null) MoveTo(g, fleet, plan.Target);
                    break;
            }
        }

        static void MoveTo(GalaxyState g, Fleet f, string target)
        {
            if (f.EngagementId != null) return;
            if (f.AITarget != null && f.AIDepartDay > g.Day) return;   // a disclosed answer to a declaration is kept until it sails
            if (f.SystemId == target && !f.Moving) return;
            if (f.Moving && f.Order.Path.LastOrDefault() == target) return;
            if (f.Moving && f.Order.DaysIntoLane > 0) return;   // redirect only at a system, like the player
            f.AITarget = null;
            var r = StrategyRules.OrderMove(g, f.Id, target, asAI: true);
            // The player learns what the AI could learn of the player: the lane a fleet takes, not its destination.
            if (r.Ok) g.Note("danger", $"Scouts report {f.Name} leaving {g.GetSystem(f.SystemId).Name} toward {g.GetSystem(f.Order.Path[0]).Name}.");
        }
    }
}
