using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    public enum ExpeditionState { Offered, Scouted, Committed, Secured, Withdrawn, Failed }

    /// <summary>
    /// The optional Eclipse Sector expedition (Prompt 10). Its conditions are fixed when it is created and saved, so no
    /// reload or choice rerolls them. What the player does not know is hidden information, not chance: reconnaissance
    /// reveals the actual conditions; a direct approach meets the same conditions without knowing them in advance.
    /// </summary>
    public sealed class Expedition
    {
        public string Id, SystemId, Title;
        public ExpeditionState State;
        public bool Scouted;
        /// <summary>Fixed conditions (set at creation): which drift field the derelict lies in, who else is there, and the asset.</summary>
        public string MapId;               // eclipse_drift_a or eclipse_drift_b
        public string RivalComposition;    // "light" or "heavy"
        public string Asset;               // module id recovered on success
        public int CreatedDay;
        public string Outcome;
        public string BattleId;
    }

    public static class Expeditions
    {
        public const string System = "gloam";
        public static readonly Resources ReconCost = new Resources(0, 0, 0, 15);
        public static readonly Resources WithdrawCost = new Resources(0, 20, 0, 5);

        /// <summary>Creates the expedition with conditions fixed from the galaxy seed (deterministic; never rolled again).</summary>
        public static Expedition Create(GalaxyState g)
        {
            uint h = (uint)g.Seed * 2654435761u ^ 0x9E3779B9u;
            var e = new Expedition
            {
                Id = "exp_gloam", SystemId = System, Title = "The silent derelict at Gloam", State = ExpeditionState.Offered, CreatedDay = g.Day,
                MapId = (h & 1) == 0 ? "eclipse_drift_a" : "eclipse_drift_b",
                RivalComposition = ((h >> 1) & 1) == 0 ? "light" : "heavy",
                Asset = "ablative_plating",
            };
            return e;
        }

        public static Expedition Of(GalaxyState g) => g.Slice?.Expeditions.FirstOrDefault();

        /// <summary>What the player knows now. Without reconnaissance the unknowns are named as unknowns, never guessed.</summary>
        public static List<string> Briefing(GalaxyState g, Expedition e)
        {
            var lines = new List<string>
            {
                "A derelict on the edge of The Eclipse Sector answers no hails; its signal repeats a short pattern with uneven gaps. Sensors in the drift are limited: ships see only what is near them.",
                "Securing it means holding the derelict's position at the end of the battle or destroying the rival flagship; recovering it yields a damaged module for your flagship.",
            };
            if (e.Scouted)
            {
                lines.Add($"Reconnaissance: the derelict lies in {(e.MapId == "eclipse_drift_a" ? "a broken drift with ion storms on the west" : "a dense drift with ion storms on the east")}; " +
                          $"a {(e.RivalComposition == "light" ? "light" : "heavy")} {RivalName(g)} prospecting group is already there ({string.Join(", ", RivalArmy(g, e).Units.Select(u => RosterContent.Unit(u.UnitId).Name))}).");
            }
            else lines.Add("Unknown without reconnaissance: the layout of the drift, and the strength of any other force already there.");
            lines.Add($"Reconnaissance costs {ReconCost.Influence} influence. Withdrawing costs {WithdrawCost.Materials} materials and {WithdrawCost.Influence} influence (equipment and probes left behind) and ends the expedition.");
            return lines;
        }

        static string RivalPolity(GalaxyState g) => g.Slice?.PlayerPolityId == "pygmaei" ? "inhumanus" : "pygmaei";
        static string RivalName(GalaxyState g) => g.GetPolity(RivalPolity(g))?.Name ?? "rival";

        public static ArmyRoster RivalArmy(GalaxyState g, Expedition e)
        {
            var f = SliceCampaign.FactionOf(RivalPolity(g));
            var a = new ArmyRoster { Name = g.GetPolity(RivalPolity(g)).Name + " prospectors", Faction = f };
            var ids = f == FactionId.Inhumanus
                ? (e.RivalComposition == "light" ? new[] { "inhumanus_executive_flagship", "inhumanus_mercator_corvette", "inhumanus_proxy_frigate" }
                                                 : new[] { "inhumanus_executive_flagship", "inhumanus_parallax_cruiser", "inhumanus_mercator_corvette", "inhumanus_proxy_frigate", "inhumanus_arbitration_destroyer" })
                : (e.RivalComposition == "light" ? new[] { "pygmaei_revenant_command_ship", "pygmaei_skirmish_frigate", "pygmaei_rift_interceptor" }
                                                 : new[] { "pygmaei_revenant_command_ship", "pygmaei_furnace_cruiser", "pygmaei_skirmish_frigate", "pygmaei_skirmish_frigate", "pygmaei_annihilator_destroyer" });
            foreach (var id in ids) a.Units.Add(new RosterEntry(id));
            return a;
        }

        public static StratResult Scout(GalaxyState g)
        {
            var e = Of(g);
            if (e == null || e.State != ExpeditionState.Offered) return StratResult.Reject("There is nothing to scout.");
            var pol = g.Player;
            if (!pol.Stock.Covers(ReconCost)) return StratResult.Reject($"Reconnaissance needs {ReconCost.Influence} influence; you have {pol.Stock.Influence}.");
            pol.Stock = pol.Stock - ReconCost;
            e.Scouted = true; e.State = ExpeditionState.Scouted;
            SliceCampaign.RecordEvent(g, "expedition", e.SystemId, null, "Probes charted the drift at Gloam and the force waiting there.");
            return StratResult.Accept();
        }

        public static StratResult Withdraw(GalaxyState g)
        {
            var e = Of(g);
            if (e == null || (e.State != ExpeditionState.Offered && e.State != ExpeditionState.Scouted && e.State != ExpeditionState.Committed))
                return StratResult.Reject("There is no expedition to withdraw from.");
            if (!g.Slice.Applied.Add("expedition_withdraw:" + e.Id)) return StratResult.Reject("Already withdrawn.");
            var pol = g.Player;
            pol.Stock = new Resources(Math.Max(0, pol.Stock.Energy - WithdrawCost.Energy), Math.Max(0, pol.Stock.Materials - WithdrawCost.Materials),
                                      Math.Max(0, pol.Stock.Credits - WithdrawCost.Credits), Math.Max(0, pol.Stock.Influence - WithdrawCost.Influence));
            e.State = ExpeditionState.Withdrawn;
            e.Outcome = $"Withdrew from Gloam, leaving {WithdrawCost.Materials} materials of equipment behind.";
            SliceCampaign.RecordEvent(g, "expedition", e.SystemId, e.BattleId, e.Outcome);
            return StratResult.Accept();
        }

        /// <summary>The expedition battle: fog of war on (sensor limits), the drift layout and rival fixed at creation.</summary>
        public static BattleSetup Prepare(GalaxyState g, Fleet player, IList<int> uids, out string error)
        {
            error = null;
            var e = Of(g);
            if (e == null || (e.State != ExpeditionState.Offered && e.State != ExpeditionState.Scouted)) { error = "The expedition is not open."; return null; }
            if (player.SystemId != e.SystemId || player.Moving) { error = "Your fleet must hold at Gloam to begin."; return null; }
            var v = BattleBridge.Validate(g, player, uids);
            if (!v.Valid) { error = v.Errors.FirstOrDefault(); return null; }
            var cfg = new MatchConfig { ClockSeconds = 0, Label = e.Title }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = e.MapId; cfg.TechMode = TechMode.Balanced; cfg.FogOfWar = true;
            cfg.FirstSeat = 0; cfg.DefenderSeat = 1;
            var setup = new BattleSetup { BattleId = $"battle_{e.Id}_d{g.Day}", EngagementId = null, Config = cfg, PlayerSeat = 0, Scenario = "expedition", SystemId = e.SystemId };
            cfg.Players[0] = new PlayerSetup { DisplayName = player.Name, Army = BattleBridge.ArmyFrom(player, uids, g), Caps = BattleBridge.Caps.Clone() };
            cfg.Players[1] = new PlayerSetup { DisplayName = RivalArmy(g, e).Name, Army = RivalArmy(g, e), Caps = BattleBridge.Caps.Clone(), IsAI = true, AIDifficulty = AIDifficulty.Tactical };
            setup.FleetIds[0] = player.Id;
            foreach (var uid in uids) { setup.ShipUids[0].Add(uid); setup.StartHp[0].Add(player.Ships.First(s => s.Uid == uid).Hp); }
            foreach (var _ in cfg.Players[1].Army.Units) { setup.ShipUids[1].Add(0); setup.StartHp[1].Add(int.MaxValue); }
            e.State = ExpeditionState.Committed; e.BattleId = setup.BattleId;
            return setup;
        }

        /// <summary>Applies the expedition battle once: the player's losses and damage, and the asset or the failure.</summary>
        public static StratResult Resolve(GalaxyState g, BattleSetup setup, BattleOutcome o)
        {
            var e = Of(g);
            if (e == null || e.BattleId != setup.BattleId) return StratResult.Reject("This battle does not belong to the expedition.");
            if (!g.Slice.Applied.Add("expedition:" + setup.BattleId)) return StratResult.Reject("The expedition result was already applied.");
            var fleet = g.GetFleet(setup.FleetIds[0]);
            foreach (var so in o.Ships[0])
            {
                var rec = fleet?.Ships.FirstOrDefault(x => x.Uid == so.Uid);
                if (rec == null) continue;
                if (so.Destroyed || so.Captured) { fleet.Ships.Remove(rec); var p = g.Slice.Profile(so.Uid); if (p != null) { p.Destroyed = so.Destroyed; p.Captured = so.Captured; p.Fate = $"Lost at Gloam on day {g.Day}."; } }
                else rec.Hp = Math.Max(1, so.Hp);
            }
            if (o.WinnerSeat == 0)
            {
                e.State = ExpeditionState.Secured;
                g.Slice.Applied.Add("spare:" + fleet.OwnerId + ":" + e.Asset + ":" + e.Id);
                e.Outcome = $"Secured the derelict at Gloam and recovered a damaged {SliceRules.Module(e.Asset).Name}, fitted free at your next shipyard refit.";
            }
            else
            {
                e.State = ExpeditionState.Failed;
                e.Outcome = "Driven off from the derelict at Gloam. The expedition is over; the primary campaign is unaffected.";
                if (fleet != null) StrategyRules.Withdraw(g, fleet, g.GetSystem(e.SystemId));
            }
            SliceCampaign.RecordEvent(g, "expedition", e.SystemId, setup.BattleId, e.Outcome);
            g.Fleets.RemoveAll(f => f.Ships.Count == 0);
            return StratResult.Accept();
        }
    }
}
