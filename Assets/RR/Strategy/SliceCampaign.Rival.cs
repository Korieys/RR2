using System;
using System.Linq;

namespace RR.Strategy
{
    // The rival's counter-offensive (Prompt 12, "enter another encounter"): a power that lost the passage in battle comes back
    // for it, on a schedule the player is told in advance. If its flagship was destroyed it first commissions a replacement
    // (a new ship with a new identity, paid from its own stock; it waits while it cannot pay). A truce, from a settlement,
    // prevents it. Bounded by the objective's attempt limit; each step is keyed and happens once. Experimental values.
    public static partial class SliceCampaign
    {
        public const int CounterDelayDays = 20;

        /// <summary>The power that lost <paramref name="p"/> and may come back for it, or null.</summary>
        static string Challenger(GalaxyState g, PassageState p)
        {
            if (p.AccessHolderId == null) return null;
            string other = p.AccessHolderId == "pygmaei" ? "inhumanus" : "pygmaei";
            var rel = g.Slice.RelationOf(p.AccessHolderId, other);
            if (rel == null || rel.Status == "truce") return null;
            return other;
        }

        static Fleet RivalFleet(GalaxyState g, string polity) => g.Fleets.FirstOrDefault(f => f.OwnerId == polity && f.AIControlled);

        /// <summary>The day the challenger is expected to move against the passage, or null if it will not (shown to the player).</summary>
        public static int? CounterOffensiveDay(GalaxyState g, PassageState p)
        {
            var who = Challenger(g, p);
            if (who == null || RivalFleet(g, who) == null) return null;
            if (PassageWar.Running(g)) return null;   // the Passage War's rival plans its own return (RivalAI)
            int tried = FailedAttempts(g, who, p.SystemId);
            if (tried >= MaxAttempts) return null;
            if (g.Slice.Applied.Contains(CounterKey(who, p))) return null;
            return p.SinceDay + CounterDelayDays;
        }

        static string CounterKey(string who, PassageState p) => $"counter_{who}_{p.SystemId}_{p.SinceDay}";

        /// <summary>One line for the campaign panel, or null.</summary>
        public static string CounterOffensiveForecast(GalaxyState g)
        {
            var p = g.Slice?.Passages.FirstOrDefault();
            if (p == null) return null;
            var day = CounterOffensiveDay(g, p);
            if (day == null) return null;
            var who = Challenger(g, p);
            bool rebuild = !RivalFleet(g, who).Ships.Any(s => ShipCatalog.IsLeader(s.DefId));
            return $"Expect {g.GetPolity(who).Name} to contest {p.Name} again from day {day}" + (rebuild ? ", once it has replaced its flagship." : ".");
        }

        static void CounterOffensives(GalaxyState g)
        {
            foreach (var p in g.Slice.Passages)
            {
                var day = CounterOffensiveDay(g, p);
                if (day == null || g.Day < day || g.PendingEngagement != null) continue;
                var who = Challenger(g, p);
                var fleet = RivalFleet(g, who);
                if (fleet.Moving || fleet.EngagementId != null) continue;
                if (!fleet.Ships.Any(s => ShipCatalog.IsLeader(s.DefId)) && !CommissionReplacementFlagship(g, fleet)) continue;   // cannot pay yet
                g.Slice.Applied.Add(CounterKey(who, p));
                string target = p.AccessHolderId;
                var obj = new WarObjective { Id = "obj_" + g.NextId++, PolityId = who, TargetPolityId = target, SystemId = p.SystemId, Status = ObjectiveStatus.Declared,
                    DeclaredDay = g.Day, Attempts = FailedAttempts(g, who, p.SystemId) };
                g.Slice.Objectives.Add(obj);
                g.Slice.RelationOf(who, target).Status = "war";
                fleet.AITarget = p.SystemId; fleet.AIDepartDay = g.Day + 2;
                var sys = g.GetSystem(p.SystemId);
                Record(g, new HistoryEvent { Kind = "declaration", SystemId = p.SystemId, Text = $"{g.GetPolity(who).Name} declared war to retake {p.Name} ({sys.Name})." });
                g.Note("danger", $"{g.GetPolity(who).Name} has declared war to retake {p.Name}. Scouts expect {fleet.Name} to sail for {sys.Name} in two days.");
            }
        }

        /// <summary>The challenger commissions a replacement flagship at its home yard, if it can pay; a new ship, a new identity.</summary>
        static bool CommissionReplacementFlagship(GalaxyState g, Fleet fleet)
        {
            var option = Commissionable(g, fleet.OwnerId)?.FirstOrDefault(o => ShipCatalog.IsLeader(o.defId));
            if (option == null || option.Value.defId == null) return false;
            var pol = g.GetPolity(fleet.OwnerId);
            if (!pol.Stock.Covers(option.Value.cost)) return false;
            if (Economy.Active(g) && Economy.InArrears(g, fleet.OwnerId)) return false;   // the same accounting as the player
            pol.Stock = pol.Stock - option.Value.cost;
            PassageWar.ForPolity(g, fleet.OwnerId, "commission", $"Commissioned a replacement {ShipCatalog.Name(option.Value.defId)}", option.Value.cost);
            var ship = new ShipRecord { Uid = g.NextUid++, DefId = option.Value.defId, Hp = ShipCatalog.MaxHp(option.Value.defId) };
            fleet.Ships.Add(ship);
            var home = g.GetSystem(fleet.SystemId) ?? g.GetSystem(HomeOf(fleet.OwnerId));
            OnCommissioned(g, fleet, ship, home);
            Record(g, new HistoryEvent { Kind = "commissioned", SystemId = home.Id, Text = $"{pol.Name} commissioned a new {ShipCatalog.Name(ship.DefId)} at {home.Name} to lead {fleet.Name}." });
            return true;
        }
    }
}
