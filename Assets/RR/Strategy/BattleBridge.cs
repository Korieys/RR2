using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>Everything needed to (re)create one orbital Commandfall battle from strategic state.</summary>
    public sealed class BattleSetup
    {
        public string BattleId;
        public string EngagementId;
        public MatchConfig Config;
        public int PlayerSeat;
        /// <summary>Per seat, ship uids in army-entry order; Commandfall creates units in that order.</summary>
        public List<int>[] ShipUids = { new List<int>(), new List<int>() };
        /// <summary>Hull points carried in from earlier battles, per seat and entry index.</summary>
        public List<int>[] StartHp = { new List<int>(), new List<int>() };
        public string[] FleetIds = new string[2];
        /// <summary>Per seat, the hex for each entry in army order, or null for Commandfall's automatic deployment.
        /// Stored with the setup so a reload can never redeploy differently.</summary>
        public List<Hex>[] Deployment = { null, null };
    }

    public sealed class ShipOutcome
    {
        public int Uid;
        public bool Destroyed;
        public bool Captured;
        public int Hp;
    }

    public sealed class BattleOutcome
    {
        public string BattleId;
        public string EngagementId;
        public int WinnerSeat = -1;
        public string Reason;
        public int Rounds;
        public List<ShipOutcome>[] Ships = { new List<ShipOutcome>(), new List<ShipOutcome>() };
    }

    public sealed class SelectionReport
    {
        public bool Valid;
        public int Cost;
        public int Budget = MatchConfig.RosterBudget;
        public int MaxUnits = MatchConfig.RosterMaxUnits;
        public List<string> Errors = new List<string>();
        public int LeaderUid;
    }

    /// <summary>
    /// Translates strategic fleets into Commandfall Full Roster orbital battles and battle results back into strategic
    /// consequences. The battle itself is resolved only by the vendored Commandfall rules engine.
    /// </summary>
    public static class BattleBridge
    {
        public static TechCaps Caps => RosterArmy.CapsFor(TechMode.Balanced);

        public static ArmyRoster ArmyFrom(Fleet f, IEnumerable<int> uids, GalaxyState g)
        {
            var pol = g.GetPolity(f.OwnerId);
            var army = new ArmyRoster { Name = f.Name, Faction = (FactionId)Enum.Parse(typeof(FactionId), pol.CommandfallFaction) };
            foreach (var uid in uids)
            {
                var ship = f.Ships.First(s => s.Uid == uid);
                army.Units.Add(new RosterEntry(ship.DefId));
            }
            return army;
        }

        public static SelectionReport Validate(GalaxyState g, Fleet f, IList<int> uids)
        {
            var rep = new SelectionReport();
            if (uids.Distinct().Count() != uids.Count) { rep.Errors.Add("A ship was selected twice."); return rep; }
            if (uids.Any(u => f.Ships.All(s => s.Uid != u))) { rep.Errors.Add("A selected ship is not in this fleet."); return rep; }
            var army = ArmyFrom(f, uids, g);
            var r = RosterArmy.Validate(army, Caps, BattleDomain.Orbital);
            rep.Cost = r.TotalCost;
            rep.Errors.AddRange(r.Errors);
            rep.Valid = r.Valid;
            rep.LeaderUid = uids.FirstOrDefault(u => ShipCatalog.IsLeader(f.Ships.First(s => s.Uid == u).DefId));
            return rep;
        }

        /// <summary>Deterministic composition: the flagship first, then the strongest ships that keep the army legal.</summary>
        public static List<int> AutoSelect(GalaxyState g, Fleet f)
        {
            var pick = new List<int>();
            var leader = f.Ships.Where(s => ShipCatalog.IsLeader(s.DefId)).OrderByDescending(s => s.Hp).ThenBy(s => s.Uid).FirstOrDefault();
            if (leader == null) return pick;
            pick.Add(leader.Uid);
            foreach (var s in f.Ships.Where(s => s != leader).OrderByDescending(s => ShipCatalog.Tier(s.DefId)).ThenByDescending(s => s.Hp).ThenBy(s => s.Uid))
            {
                pick.Add(s.Uid);
                if (!Validate(g, f, pick).Valid) pick.RemoveAt(pick.Count - 1);
            }
            return pick;
        }

        public static BattleSetup Prepare(GalaxyState g, Engagement e, IList<int> playerUids, out string error)
        {
            error = null;
            var att = g.GetFleet(e.AttackerFleetId);
            var def = g.GetFleet(e.DefenderFleetId);
            var player = att.OwnerId == g.Player.Id ? att : def;
            var enemy = player == att ? def : att;
            var pv = Validate(g, player, playerUids);
            if (!pv.Valid) { error = pv.Errors.FirstOrDefault() ?? "The selected force is not a legal battle line."; return null; }
            var enemyUids = AutoSelect(g, enemy);
            var ev = Validate(g, enemy, enemyUids);
            if (!ev.Valid) { error = "The enemy could not form a battle line: " + ev.Errors.FirstOrDefault(); return null; }

            var sys = g.GetSystem(e.SystemId);
            var cfg = new MatchConfig { ClockSeconds = 0, Label = $"{sys.Name}, day {g.Day}" }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = sys.BattlefieldMapId ?? MapLibrary.DefaultOrbitalMapId;
            cfg.TechMode = TechMode.Balanced;
            const int playerSeat = 0;
            var setup = new BattleSetup { BattleId = $"battle_{e.Id}_d{g.Day}", EngagementId = e.Id, Config = cfg, PlayerSeat = playerSeat };
            var fleets = new[] { player, enemy };
            var uids = new[] { playerUids.ToList(), enemyUids };
            for (int seat = 0; seat < 2; seat++)
            {
                cfg.Players[seat] = new PlayerSetup
                {
                    DisplayName = fleets[seat].Name, Army = ArmyFrom(fleets[seat], uids[seat], g), Caps = Caps.Clone(),
                    IsAI = seat != playerSeat, AIDifficulty = AIDifficulty.Tactical,
                };
                setup.FleetIds[seat] = fleets[seat].Id;
                foreach (var uid in uids[seat])
                {
                    setup.ShipUids[seat].Add(uid);
                    setup.StartHp[seat].Add(fleets[seat].Ships.First(s => s.Uid == uid).Hp);
                }
            }
            // The arriving fleet takes the first activation.
            int attackerSeat = att == player ? playerSeat : 1 - playerSeat;
            cfg.FirstSeat = attackerSeat;
            cfg.DefenderSeat = 1 - attackerSeat;
            e.BattleId = setup.BattleId;
            return setup;
        }

        /// <summary>Creates the authoritative match state, deployed and ready for the first activation. Deterministic:
        /// the same setup always produces the same state (carried-in damage is applied before deployment).</summary>
        public static MatchState CreateState(BattleSetup setup, out string error)
        {
            var s = RulesEngine.CreateMatch(setup.Config, out error);
            if (s == null) return null;
            for (int seat = 0; seat < 2; seat++)
            {
                var units = s.Units.Where(u => u.Owner == seat).OrderBy(u => u.Id).ToList();
                for (int i = 0; i < units.Count && i < setup.StartHp[seat].Count; i++)
                    units[i].Hp = Math.Max(1, Math.Min(units[i].MaxHp, setup.StartHp[seat][i]));
            }
            for (int guard = 0; guard < 4 && s.Phase == MatchPhase.Deployment; guard++)
            {
                foreach (int seat in new[] { setup.Config.FirstSeat, 1 - setup.Config.FirstSeat })
                    if (!s.Players[seat].DeploymentCommitted)
                    {
                        var r = RulesEngine.Apply(s, DeployCommand(setup, s, seat));
                        if (!r.Accepted) { error = "Deployment failed: " + r.Reason; return null; }
                    }
            }
            return s;
        }

        static Command DeployCommand(BattleSetup setup, MatchState s, int seat)
        {
            var hexes = setup.Deployment[seat];
            if (hexes == null) return Command.AutoDeploy(seat);
            var units = s.Units.Where(u => u.Owner == seat).OrderBy(u => u.Id).ToList();
            return Command.Deploy(seat, units.Select((u, i) => new Placement(u.Id, i < hexes.Count ? hexes[i] : default)));
        }

        public static int ShipUidFor(BattleSetup setup, MatchState s, int unitId)
        {
            var u = s.Unit(unitId);
            if (u == null) return 0;
            var units = s.Units.Where(x => x.Owner == u.Owner).OrderBy(x => x.Id).ToList();
            int i = units.IndexOf(u);
            return i >= 0 && i < setup.ShipUids[u.Owner].Count ? setup.ShipUids[u.Owner][i] : 0;   // tokens map to 0
        }

        public static BattleOutcome Extract(BattleSetup setup, MatchState s)
        {
            var o = new BattleOutcome { BattleId = setup.BattleId, EngagementId = setup.EngagementId, WinnerSeat = s.Winner, Reason = s.VictoryReason.ToString(), Rounds = s.Round };
            for (int seat = 0; seat < 2; seat++)
            {
                var units = s.Units.Where(u => u.Owner == seat).OrderBy(u => u.Id).ToList();
                for (int i = 0; i < setup.ShipUids[seat].Count && i < units.Count; i++)
                {
                    var u = units[i];
                    bool captured = u.Alive && u.HasR(RStatusType.Captured);
                    o.Ships[seat].Add(new ShipOutcome { Uid = setup.ShipUids[seat][i], Destroyed = !u.Alive, Captured = captured, Hp = u.Alive ? u.Hp : 0 });
                }
            }
            return o;
        }

        /// <summary>Applies a finished battle to the galaxy exactly once (keyed by battle id).</summary>
        public static StratResult Apply(GalaxyState g, BattleSetup setup, BattleOutcome o)
        {
            if (o == null || o.BattleId != setup.BattleId) return StratResult.Reject("The battle result does not match this battle.");
            if (g.AppliedBattles.Contains(o.BattleId)) return StratResult.Reject("This battle result was already applied.");
            var e = g.GetEngagement(o.EngagementId);
            if (e == null || e.State != EngagementState.Pending) return StratResult.Reject("The engagement is no longer pending.");
            if (o.WinnerSeat < 0) return StratResult.Reject("The battle has not ended.");
            var sys = g.GetSystem(e.SystemId);
            var fleets = new[] { g.GetFleet(setup.FleetIds[0]), g.GetFleet(setup.FleetIds[1]) };
            var lost = new int[2];
            var captured = new int[2];
            for (int seat = 0; seat < 2; seat++)
                foreach (var so in o.Ships[seat])
                {
                    var rec = fleets[seat].Ships.FirstOrDefault(x => x.Uid == so.Uid);
                    if (rec == null) continue;
                    if (so.Destroyed || so.Captured)
                    {
                        fleets[seat].Ships.Remove(rec);
                        if (so.Captured) captured[seat]++; else lost[seat]++;
                    }
                    else rec.Hp = Math.Max(1, so.Hp);
                }
            var winner = fleets[o.WinnerSeat];
            var loser = fleets[1 - o.WinnerSeat];
            winner.EngagementId = null;
            Withdraw(g, loser, sys);
            if (Hostile(sys.OwnerId, winner.OwnerId))
            {
                // Orbital victory: orbit and blockade only. The planets stay with their owner until landing and ground
                // phases succeed, which this slice does not implement (Docs/DECISIONS.md, conquest stages).
                sys.OrbitalControlId = winner.OwnerId; sys.ConquestBy = winner.OwnerId;
                if (sys.Conquest < ConquestStage.OrbitSecured) sys.Conquest = ConquestStage.OrbitSecured;
            }
            else if (sys.OwnerId == winner.OwnerId && sys.Blockaded)
            {
                sys.OrbitalControlId = winner.OwnerId; sys.ConquestBy = null; sys.Conquest = ConquestStage.None;
            }
            string why = o.Reason switch
            {
                "CommanderEliminated" => "a flagship was destroyed",
                "RoundLimitObjectives" => "the round limit was reached and objectives decided it",
                "Resignation" => "the other side yielded",
                _ => o.Reason,
            };
            e.State = EngagementState.Resolved;
            e.ResultSummary = $"{winner.Name} won at {sys.Name} ({why}). Losses: {winner.Name} {lost[o.WinnerSeat] + captured[o.WinnerSeat]}, " +
                              $"{loser.Name} {lost[1 - o.WinnerSeat] + captured[1 - o.WinnerSeat]}" +
                              (captured[0] + captured[1] > 0 ? $" ({captured[0] + captured[1]} captured)." : ".");
            g.AppliedBattles.Add(o.BattleId);
            if (g.ActiveBattle?.Setup?.BattleId == o.BattleId) g.ActiveBattle = null;
            g.Note(winner.OwnerId == g.Player.Id ? "result" : "danger", e.ResultSummary);
            if (sys.Blockaded) g.Note("danger", $"{g.GetPolity(sys.OrbitalControlId).Name} holds the orbit of {sys.Name}. Its worlds are blockaded but not conquered.");
            g.Fleets.RemoveAll(f => f.Ships.Count == 0);
            return StratResult.Accept();
        }

        static bool Hostile(string a, string b) => StrategyRules.Hostile(a, b);
        static void Withdraw(GalaxyState g, Fleet f, StarSystem from) => StrategyRules.Withdraw(g, f, from);
    }
}