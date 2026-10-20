using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>Prompts 6 to 8: shipyard lists, replacement flagships and settlements made before a battle.</summary>
    public static partial class SliceCampaign
    {
        /// <summary>What a passage-campaign shipyard builds (null outside the passage campaign). Experimental costs. The
        /// flagship class is offered only to a side that has lost its flagship: the declared recovery rule.</summary>
        public static (string defId, Resources cost, int days)[] Commissionable(GalaxyState g, string polity)
        {
            if (g.Slice == null || !g.Slice.Active) return null;
            var f = FactionOf(polity);
            var list = new List<(string, Resources, int)>();
            foreach (var id in SliceRoster.Classes(f))
            {
                int cost = RosterContent.Cost(RosterContent.Unit(id));
                bool leader = ShipCatalog.IsLeader(id);
                if (leader && g.Fleets.Any(x => x.OwnerId == polity && x.Ships.Any(s => ShipCatalog.IsLeader(s.DefId)))) continue;
                list.Add((id, Economy.CommissionCost(g, polity, id, new Resources(15 * cost, 30 * cost, 12 * cost, 0)), leader ? 20 : 4 + 2 * cost));
            }
            return list.ToArray();
        }

        /// <summary>A newly commissioned flagship is a new ship with a new identity; it inherits no history.</summary>
        public static void OnCommissioned(GalaxyState g, Fleet fleet, ShipRecord ship, StarSystem at)
        {
            if (g.Slice == null || !IsCapital(ship.DefId)) return;
            var lost = g.Slice.Ships.Values.Where(p => p.OwnerId == fleet.OwnerId && p.Destroyed && p.DefId == ship.DefId).Select(p => p.Name).LastOrDefault();
            g.Slice.Ships[ship.Uid] = new ShipProfile
            {
                Uid = ship.Uid, DefId = ship.DefId, OwnerId = fleet.OwnerId, Name = null,
                Service = { new ServiceEntry { Day = g.Day, Kind = "commissioned", Text = $"Commissioned at {at.Name}" + (lost != null ? $" to replace {lost}." : ".") } },
            };
        }

        /// <summary>The pending engagement's sides as battle seats (seat 0 is the player's fleet, as in BattleBridge.Prepare).</summary>
        public static Fleet[] Seats(GalaxyState g, Engagement e)
        {
            var a = g.GetFleet(e.AttackerFleetId); var d = g.GetFleet(e.DefenderFleetId);
            return a.OwnerId == g.Player.Id ? new[] { a, d } : new[] { d, a };
        }

        /// <summary>Settles an engagement before any battle is fought (Prompt 8): the conceding fleet withdraws intact,
        /// the passage goes to the other side, reparations are paid and, if agreed, the flagship changes custody. Once only.</summary>
        public static StratResult SettleBeforeBattle(GalaxyState g, string engagementId, Offer o)
        {
            var e = g.GetEngagement(engagementId);
            if (e == null || e.State != EngagementState.Pending) return StratResult.Reject("There is no pending engagement to settle.");
            var bySeat = Seats(g, e);
            var setup = new BattleSetup { BattleId = "settle_" + e.Id, EngagementId = e.Id, PlayerSeat = 0, SystemId = e.SystemId, Config = new MatchConfig() };
            setup.Config.Players[0] = new PlayerSetup { DisplayName = bySeat[0].Name };
            setup.Config.Players[1] = new PlayerSetup { DisplayName = bySeat[1].Name };
            setup.FleetIds[0] = bySeat[0].Id; setup.FleetIds[1] = bySeat[1].Id;
            o.Revision = -1;
            var invalid = Negotiation.Validate(g, setup, null, o);
            if (invalid != null) return StratResult.Reject(invalid);
            if (g.AppliedBattles.Contains(setup.BattleId)) return StratResult.Reject("This engagement was already settled.");
            if (o.SurrenderFlagship && !bySeat[o.ConcederSeat].Ships.Any(s => ShipCatalog.IsLeader(s.DefId))) return StratResult.Reject("That fleet has no flagship to surrender.");
            var outcome = new BattleOutcome { BattleId = setup.BattleId, EngagementId = e.Id, WinnerSeat = 1 - o.ConcederSeat, Reason = "Settlement", Settlement = o };
            for (int seat = 0; seat < 2; seat++)
                foreach (var s in bySeat[seat].Ships) { outcome.Ships[seat].Add(new ShipOutcome { Uid = s.Uid, Hp = s.Hp }); setup.ShipUids[seat].Add(s.Uid); setup.StartHp[seat].Add(s.Hp); }
            e.BattleId = setup.BattleId;
            return BattleBridge.Apply(g, setup, outcome);
        }
    }
}
