using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    /// <summary>A next step the player can take, with the action the interface offers for it (null when it is advice only).</summary>
    public sealed class NextAction
    {
        public string Text;
        public string Action;   // "declare", "treaty", "engagement", "hangar", "shipyard", "move", or null
        public NextAction(string text, string action = null) { Text = text; Action = action; }
    }

    public static partial class PassageWar
    {
        /// <summary>Clear next-action affordances for the mandate panel, computed from authoritative state (never a hint
        /// that reads hidden information: everything here is the player's own state or public map facts).</summary>
        public static List<NextAction> NextActions(GalaxyState g)
        {
            var list = new List<NextAction>();
            var sc = Of(g);
            if (sc == null) return list;
            if (sc.Ended) { list.Add(new NextAction("The campaign has ended: read the ending, continue freeform, or start again from the scenario menu (F6).")); return list; }
            string me = g.Player.Id;
            var p = g.Slice.Passage(SliceCampaign.PassageSystem);
            var e = g.PendingEngagement;
            if (e != null)
            {
                list.Add(new NextAction($"An encounter at {g.GetSystem(e.SystemId).Name} waits: fight, offer terms or withdraw in battle preparation.", "engagement"));
                return list;
            }
            var home = g.GetSystem(SliceCampaign.HomeOf(me));
            var yard = home.Planets.FirstOrDefault(x => x.HasShipyard && x.OwnerId == me);
            var fighting = g.Fleets.Where(f => f.OwnerId == me && StrategyRules.CanFight(f)).ToList();
            if (fighting.Count == 0)
            {
                var building = g.Systems.SelectMany(s => s.Planets).FirstOrDefault(x => x.OwnerId == me && x.Project != null && x.Project.Kind == ProjectKind.CommissionShip && ShipCatalog.IsLeader(x.Project.ShipDefId));
                if (building != null)
                    list.Add(new NextAction($"A replacement flagship is being built at {building.Name}: ready in {building.Project.DaysTotal - building.Project.DaysDone} days."));
                else if (CanAffordFlagship(g, me) && yard != null)
                    list.Add(new NextAction($"Commission a replacement flagship at {yard.Name} ({CostText(FlagshipCost(me))}). It is a new ship; the old one stays lost.", "shipyard"));
                else
                    list.Add(new NextAction($"A replacement flagship costs {CostText(FlagshipCost(me))}; you have {CostText(g.Player.Stock)}. Income arrives daily; salvage can also help."));
            }
            foreach (var f in fighting)
            {
                var flag = f.Ships.First(x => ShipCatalog.IsLeader(x.DefId));
                int max = SliceCampaign.MaxHull(g, flag);
                if (flag.Hp < max)
                {
                    bool atYard = SliceCampaign.RefitLocationProblem(g, f) == null;
                    list.Add(new NextAction(atYard ? $"Repair {SliceCampaign.ShipName(g, flag)} here (hull {flag.Hp} of {max}): {CostText(SliceCampaign.RepairCost(g, flag.Uid))}."
                                                   : $"{SliceCampaign.ShipName(g, flag)} is damaged (hull {flag.Hp} of {max}). Repairs need your shipyard at {home.Name}.", atYard ? "hangar" : null));
                }
            }
            var obj = g.Slice.Objectives.LastOrDefault(o => o.PolityId == me && o.SystemId == p.SystemId);
            if (p.AccessHolderId != me)
            {
                int left = SliceCampaign.MaxAttempts - Attempts(g, me);
                if ((obj == null || obj.Status != ObjectiveStatus.Declared) && left > 0)
                    list.Add(new NextAction($"Declare war over the passage ({left} attempt(s) left), or propose a treaty.", "declare"));
                else if (obj != null && obj.Status == ObjectiveStatus.Declared)
                {
                    var f = fighting.FirstOrDefault();
                    if (f != null && f.SystemId != p.SystemId && !(f.Moving && f.Order.Path.LastOrDefault() == p.SystemId))
                    {
                        var plan = StrategyRules.PlanRoute(g, f, p.SystemId);
                        if (plan.Legal) list.Add(new NextAction($"Send {f.Name} to {g.GetSystem(p.SystemId).Name} ({plan.Days} days).", "move"));
                    }
                    else if (f != null && f.SystemId == p.SystemId && !f.Moving)
                        list.Add(new NextAction($"Hold {g.GetSystem(p.SystemId).Name}: unopposed at the settlement on day {NextSettlementDay(g)}, you take access by occupation."));
                }
            }
            else
            {
                var why = HoldBlocker(g, p);
                list.Add(new NextAction(why == null ? $"Hold the passage: the settlement on day {NextSettlementDay(g)} counts if nothing interrupts it."
                                                    : $"Your hold will not count while {why}."));
                var forecast = SliceCampaign.CounterOffensiveForecast(g);
                if (forecast != null) list.Add(new NextAction(forecast));
            }
            var canPropose = CanPropose(g, me, 0);
            if (canPropose == null) list.Add(new NextAction("A treaty proposal is possible now (one per settlement interval).", "treaty"));
            return list;
        }
    }
}
