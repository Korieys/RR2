using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace RR.Strategy
{
    /// <summary>The operation's engagements: preparing each battle from the committed forces, applying its casualties once,
    /// the decisions between engagements, and the final settlement (the only step that can change who owns the world).</summary>
    public static partial class Operations
    {
        static TechCaps Caps => BattleBridge.Caps;

        // ------------------------------------------------------------------ preparing a battle

        /// <summary>The next battle of the operation, or null with a reason. When a side cannot deploy a commander it concedes
        /// that engagement or match instead (recorded, applied, and <paramref name="conceded"/> is set; no battle is fought).</summary>
        public static BattleSetup PrepareNext(GalaxyState g, Operation op, out string error, out bool conceded)
        {
            error = null; conceded = false;
            if (op.Phase != OperationPhase.Orbital && op.Phase != OperationPhase.Series) { error = "No engagement is due: " + Next(g, op); return null; }
            if (op.PendingBattleId != null && !g.AppliedBattles.Contains(op.PendingBattleId) && g.ActiveBattle?.Setup?.BattleId == op.PendingBattleId)
            { error = "This operation's battle is already in progress."; return null; }
            var domain = op.Phase == OperationPhase.Orbital ? BattleDomain.Orbital : BattleDomain.Planetary;
            EnsureCommanders(g, op, domain);
            foreach (var pol in new[] { op.AttackerPolity, op.DefenderPolity })
                if (Commander(g, op, pol, domain).uid == 0)
                {
                    Concede(g, op, pol, domain, $"{g.GetPolity(pol).Name} had no {(domain == BattleDomain.Orbital ? "flagship" : "commander")} and no eligible successor, and conceded.");
                    conceded = true;
                    return null;
                }
            var setup = domain == BattleDomain.Orbital ? Orbital(g, op, out error) : Planetary(g, op, out error);
            if (setup != null) op.PendingBattleId = setup.BattleId;
            return setup;
        }

        static BattleSetup Base(GalaxyState g, Operation op, BattleDomain d, string mapId, string label)
        {
            var cfg = new MatchConfig { ClockSeconds = 0, Label = label }.UseRoster(d);
            cfg.MapId = mapId;
            cfg.TechMode = TechMode.Balanced;
            int index = op.Battles.Count(b => !b.Conceded) + op.Battles.Count(b => b.Conceded);
            int playerSeat = 0;
            var setup = new BattleSetup { BattleId = $"{op.Id}_{(d == BattleDomain.Orbital ? "orb" : "gnd")}{index}", Config = cfg, PlayerSeat = playerSeat, SystemId = op.SystemId,
                Scenario = Scenario, OperationId = op.Id };
            // The player's side is seat 0 (as in BattleBridge); the attacker moves first.
            var seats = SeatPolities(g, op);
            for (int seat = 0; seat < 2; seat++)
                cfg.Players[seat] = new PlayerSetup { DisplayName = g.GetPolity(seats[seat]).Name, Caps = Caps.Clone(), IsAI = seats[seat] != g.Player.Id, AIDifficulty = AIDifficulty.Tactical };
            cfg.FirstSeat = Array.IndexOf(seats, op.AttackerPolity);
            cfg.DefenderSeat = Array.IndexOf(seats, op.DefenderPolity);
            return setup;
        }

        public static string[] SeatPolities(GalaxyState g, Operation op) =>
            op.DefenderPolity == g.Player.Id ? new[] { op.DefenderPolity, op.AttackerPolity } : new[] { op.AttackerPolity, op.DefenderPolity };

        static BattleSetup Orbital(GalaxyState g, Operation op, out string error)
        {
            error = null;
            var sys = g.GetSystem(op.SystemId);
            var setup = Base(g, op, BattleDomain.Orbital, sys.BattlefieldMapId ?? MapLibrary.DefaultOrbitalMapId, $"{sys.Name}, orbital engagement {op.OrbitalFought + 1}");
            var seats = SeatPolities(g, op);
            for (int seat = 0; seat < 2; seat++)
            {
                var fleet = g.GetFleet(seats[seat] == op.AttackerPolity ? op.AttackerFleetId : op.DefenderFleetId);
                var cmd = Commander(g, op, seats[seat], BattleDomain.Orbital);
                var army = new ArmyRoster { Name = fleet.Name, Faction = SliceCampaign.FactionOf(seats[seat]) };
                var picked = new List<ShipRecord>();
                foreach (var s in new[] { fleet.Ships.First(x => x.Uid == cmd.uid) }.Concat(fleet.Ships.Where(x => x.Uid != cmd.uid).OrderByDescending(x => ShipCatalog.Tier(x.DefId)).ThenByDescending(x => x.Hp).ThenBy(x => x.Uid)))
                {
                    var entry = new RosterEntry(s.DefId);
                    var prof = g.Slice.Profile(s.Uid);
                    if (prof != null) foreach (var m in prof.Modules) entry.Equipment.Add(SliceRules.ModuleToken + m);
                    if (s.Uid == cmd.uid && cmd.acting) entry.Equipment.Add(SliceRules.ActingCommandToken);
                    if (s.Uid == cmd.uid && op.FollowUp) entry.Equipment.Add(SliceRules.CapturePolicyToken);   // the follow-up may take prisoners by boarding
                    entry.Equipment.AddRange(Doctrines.Tokens(g, seats[seat]));   // adopted doctrine sidegrades (Prompt 18), disclosed
                    army.Units.Add(entry); picked.Add(s);
                    if (!RosterArmy.Validate(army, Caps, BattleDomain.Orbital).Valid) { army.Units.RemoveAt(army.Units.Count - 1); picked.RemoveAt(picked.Count - 1); }
                }
                var rep = RosterArmy.Validate(army, Caps, BattleDomain.Orbital);
                if (!rep.Valid) { error = $"{fleet.Name} could not form a legal battle line: {rep.Errors.FirstOrDefault()}"; return null; }
                setup.Config.Players[seat].Army = army;
                setup.FleetIds[seat] = fleet.Id;
                foreach (var s in picked) { setup.ShipUids[seat].Add(s.Uid); setup.StartHp[seat].Add(s.Hp); }
            }
            return setup;
        }

        /// <summary>A planetary match: each side's commander first, then its strongest units, at most <see cref="DeployCap"/> and
        /// within the battle budget. The defender's reserves (units beyond the cap) are cut off if its supplies were seized.</summary>
        static BattleSetup Planetary(GalaxyState g, Operation op, out string error)
        {
            error = null;
            int match = op.AttackerWins + op.DefenderWins;
            var mapId = MapFor(op, match);
            var p = g.GetPlanet(op.PlanetId);
            var setup = Base(g, op, BattleDomain.Planetary, mapId, $"{p.Name}, planetary match {match + 1}");
            var seats = SeatPolities(g, op);
            for (int seat = 0; seat < 2; seat++)
            {
                var pol = seats[seat];
                var lineup = Lineup(g, op, pol, out var army);
                if (army == null) { error = $"{g.GetPolity(pol).Name} could not form a legal army."; return null; }
                setup.Config.Players[seat].Army = army;
                setup.FleetIds[seat] = pol == op.AttackerPolity ? op.AttackerFleetId : null;
                foreach (var u in lineup) { setup.ShipUids[seat].Add(u.Uid); setup.StartHp[seat].Add(u.Hp); }
            }
            return setup;
        }

        /// <summary>The units a side deploys in the next match (deterministic; the operation window shows it before the match).</summary>
        public static List<GroundUnit> Lineup(GalaxyState g, Operation op, string pol, out ArmyRoster army)
        {
            var cmd = Commander(g, op, pol, BattleDomain.Planetary);
            var pool = GroundFor(g, op, pol);   // the defender's cut-off reserves are already excluded
            var faction = SliceCampaign.FactionOf(pol);
            army = new ArmyRoster { Name = g.GetPolity(pol).Name, Faction = faction };
            var picked = new List<GroundUnit>();
            var commander = pool.FirstOrDefault(u => u.Uid == cmd.uid);
            if (commander == null) { army = null; return picked; }
            var others = pool.Where(u => u != commander).OrderByDescending(u => ShipCatalog.Cost(u.DefId)).ThenBy(u => u.Uid).ToList();
            if (faction == FactionId.Inhumanus)
            {
                // The Corporate Charter (an existing Inhumanus rule): the charter that most of the force belongs to, its specialists first.
                var charter = new[] { Doctrine.Assault, Doctrine.Defense, Doctrine.Control }
                    .OrderByDescending(d => pool.Count(u => u != commander && RosterContent.InCharter(ShipCatalog.Def(u.DefId), d))).ThenBy(d => (int)d).First();
                army.Doctrine = charter;
                others = others.OrderByDescending(u => RosterContent.InCharter(ShipCatalog.Def(u.DefId), charter)).ThenByDescending(u => ShipCatalog.Cost(u.DefId)).ThenBy(u => u.Uid).ToList();
            }
            foreach (var u in new[] { commander }.Concat(others))
            {
                if (picked.Count >= DeployCap) break;
                var entry = new RosterEntry(u.DefId);
                if (u == commander && cmd.acting) entry.Equipment.Add(SliceRules.ActingCommandToken);
                army.Units.Add(entry); picked.Add(u);
                if (!RosterArmy.Validate(army, Caps, BattleDomain.Planetary).Valid && u != commander) { army.Units.RemoveAt(army.Units.Count - 1); picked.RemoveAt(picked.Count - 1); }
            }
            if (!RosterArmy.Validate(army, Caps, BattleDomain.Planetary).Valid) { army = null; return new List<GroundUnit>(); }
            return picked;
        }

        // ------------------------------------------------------------------ applying a battle

        /// <summary>Applies one engagement or match exactly once (keyed by battle id): casualties, captures and service records
        /// for that battle only; then the operation moves to its next phase. The world never changes hands here.</summary>
        public static StratResult ApplyBattle(GalaxyState g, BattleSetup setup, BattleOutcome o)
        {
            var op = Get(g, setup.OperationId);
            if (op == null || op.Ended) return StratResult.Reject("The operation is no longer in progress.");
            if (o == null || o.BattleId != setup.BattleId || o.WinnerSeat < 0) return StratResult.Reject("The battle result does not match this battle.");
            if (!g.AppliedBattles.Add(o.BattleId)) return StratResult.Reject("This battle result was already applied.");
            var seats = SeatPolities(g, op);
            string winner = seats[o.WinnerSeat], loser = seats[1 - o.WinnerSeat];
            bool orbital = setup.Config.Domain == BattleDomain.Orbital;
            var sys = g.GetSystem(op.SystemId);
            if (orbital)
            {
                var fleets = new[] { g.GetFleet(setup.FleetIds[0]), g.GetFleet(setup.FleetIds[1]) };
                for (int seat = 0; seat < 2; seat++)
                    foreach (var so in o.Ships[seat])
                    {
                        var rec = fleets[seat]?.Ships.FirstOrDefault(x => x.Uid == so.Uid);
                        if (rec == null) continue;
                        if (so.Destroyed || so.Captured) fleets[seat].Ships.Remove(rec); else rec.Hp = Math.Max(1, so.Hp);
                        if (so.Captured && g.Slice.Profile(so.Uid) is ShipProfile prof)
                        {
                            fleets[1 - seat].Ships.Add(new ShipRecord { Uid = so.Uid, DefId = prof.DefId, Hp = Math.Max(1, so.Hp) });
                            prof.OwnerId = fleets[1 - seat].OwnerId;
                            op.Prisoners.Add(so.Uid);
                        }
                    }
                // Service records, wrecks and reactions through the campaign's own transaction (no passage here, so no access change).
                SliceCampaign.OnBattleApplied(g, setup, o, fleets[o.WinnerSeat], fleets[1 - o.WinnerSeat], sys);
                EnforceCapacity(g);
            }
            else
            {
                for (int seat = 0; seat < 2; seat++)
                    foreach (var so in o.Ships[seat])
                    {
                        var unit = AllUnits(g).FirstOrDefault(u => u.Uid == so.Uid);
                        if (unit == null) continue;
                        var force = S(g).Ground.First(x => x.Units.Contains(unit));
                        if (so.Destroyed) force.Units.Remove(unit);
                        else if (so.Captured) { force.Units.Remove(unit); op.Prisoners.Add(unit.Uid); }
                        else unit.Hp = Math.Max(1, so.Hp);
                    }
                int lostW = o.Ships[o.WinnerSeat].Count(x => x.Destroyed), lostL = o.Ships[1 - o.WinnerSeat].Count(x => x.Destroyed);
                PassageWar.ForPolity(g, winner, "battle", $"Won planetary match {op.AttackerWins + op.DefenderWins + 1} at {g.GetPlanet(op.PlanetId).Name}; lost {lostW} unit(s), the enemy {lostL}", default);
                PassageWar.ForPolity(g, loser, "battle", $"Lost planetary match {op.AttackerWins + op.DefenderWins + 1} at {g.GetPlanet(op.PlanetId).Name}; lost {lostL} unit(s), the enemy {lostW}", default);
            }
            var rec2 = new OperationBattle { BattleId = o.BattleId, Kind = orbital ? "orbital" : "planetary", WinnerPolity = winner, Reason = o.Reason, MapId = setup.Config.MapId,
                Index = op.Battles.Count, Note = o.DecisiveText };
            rec2.Deployed.AddRange(setup.ShipUids.SelectMany(x => x));
            op.Battles.Add(rec2);
            op.PendingBattleId = null;
            if (g.ActiveBattle?.Setup?.BattleId == o.BattleId) g.ActiveBattle = null;
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, o.BattleId, $"{(orbital ? $"Orbital engagement {op.OrbitalFought + 1}" : $"Planetary match {op.AttackerWins + op.DefenderWins + 1}")} at {sys.Name}: {g.GetPolity(winner).Name} won ({Why(o.Reason)}).");
            Advance(g, op, winner, orbital);
            g.Fleets.RemoveAll(f => f.Ships.Count == 0);
            AIStep(g, op);
            return StratResult.Accept();
        }

        /// <summary>Decisions the AI side takes between engagements (the same choices and information the player has).</summary>
        public static void AIStep(GalaxyState g, Operation op)
        {
            if (op.Ended) return;
            if (op.Phase == OperationPhase.DefenderChoice && op.DefenderPolity != g.Player.Id)
            {
                if (AIWantsFollowUp(g, op)) DefenderFollowUp(g, op); else DefenderStops(g, op);
            }
        }

        static string Why(string reason) => reason switch
        {
            "CommanderEliminated" => "the commander fell",
            "RoundLimitObjectives" => "on the round limit",
            "LeaderCaptured" => "the flagship was boarded",
            "Resignation" => "the other side yielded",
            "Conceded" => "the other side could not deploy",
            _ => reason,
        };

        /// <summary>A side that cannot deploy loses that engagement or match without a battle (the concession policy).</summary>
        static void Concede(GalaxyState g, Operation op, string pol, BattleDomain d, string note)
        {
            bool orbital = d == BattleDomain.Orbital;
            string winner = pol == op.AttackerPolity ? op.DefenderPolity : op.AttackerPolity;
            op.Battles.Add(new OperationBattle { Index = op.Battles.Count, Kind = orbital ? "orbital" : "planetary", WinnerPolity = winner, Reason = "Conceded", Conceded = true, Note = note });
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, note);
            Advance(g, op, winner, orbital);
        }

        static void Advance(GalaxyState g, Operation op, string winner, bool orbital)
        {
            bool attackerWon = winner == op.AttackerPolity;
            if (orbital)
            {
                op.OrbitalFought++;
                if (attackerWon) op.OrbitalAttackerWins++; else op.OrbitalDefenderWins++;
                if (op.FollowUp)
                {
                    // The defender's elected follow-up: its earlier protection stands whatever happens now.
                    op.FollowUpResolved = true;
                    if (!attackerWon) SeizeAttackerSupplies(g, op);
                    else SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, "The defender's follow-up failed; its supplies stay protected, as earned.");
                    op.Phase = OperationPhase.AttackerChoice;
                }
                else if (op.OrbitalFought == 1 && !attackerWon) { op.SuppliesProtected = true; op.Phase = OperationPhase.DefenderChoice; }
                else if (op.OrbitalFought == 1 && attackerWon) op.Phase = OperationPhase.Orbital;   // the defender's forced second chance
                else
                {
                    if (attackerWon) SeizeDefenderSupplies(g, op); else op.SuppliesProtected = true;
                    op.Phase = OperationPhase.AttackerChoice;
                }
                if (op.Phase == OperationPhase.AttackerChoice && op.SuppliesSeized)
                {
                    var sys = g.GetSystem(op.SystemId);
                    sys.OrbitalControlId = op.AttackerPolity; sys.ConquestBy = op.AttackerPolity;
                    if (sys.Conquest < ConquestStage.OrbitSecured) sys.Conquest = ConquestStage.OrbitSecured;
                }
                // A side whose fleet no longer exists cannot fight on in orbit: the attacker without a fleet withdraws.
                if (g.GetFleet(op.AttackerFleetId) == null || g.GetFleet(op.AttackerFleetId).Ships.Count == 0) { Settle(g, op, op.DefenderPolity, "The attacking fleet was lost; the operation failed."); return; }
                return;
            }
            if (attackerWon) op.AttackerWins++; else op.DefenderWins++;
            if (op.AttackerWins >= SeriesWins) Settle(g, op, op.AttackerPolity, $"{g.GetPolity(op.AttackerPolity).Name} won the planetary series {op.AttackerWins}-{op.DefenderWins}.");
            else if (op.DefenderWins >= SeriesWins) Settle(g, op, op.DefenderPolity, $"{g.GetPolity(op.DefenderPolity).Name} held {g.GetPlanet(op.PlanetId).Name}, winning the series {op.DefenderWins}-{op.AttackerWins}.");
        }

        static void SeizeDefenderSupplies(GalaxyState g, Operation op)
        {
            if (!g.Slice.Applied.Add("opsupply:" + op.Id)) return;
            op.SuppliesSeized = true;
            var d = g.GetPolity(op.DefenderPolity); var a = g.GetPolity(op.AttackerPolity);
            int take = Math.Min(SupplyMaterials, d.Stock.Materials);
            d.Stock = d.Stock - new Resources(0, take, 0, 0); a.Stock = a.Stock + new Resources(0, take, 0, 0);
            PassageWar.ForPolity(g, op.AttackerPolity, "operation", $"Seized {take} materials of {g.GetPlanet(op.PlanetId).Name}'s supplies", new Resources(0, -take, 0, 0));
            PassageWar.ForPolity(g, op.DefenderPolity, "operation", $"Lost {take} materials of {g.GetPlanet(op.PlanetId).Name}'s supplies; its reserves are cut off", new Resources(0, take, 0, 0));
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, $"{a.Name} won both orbital engagements over {g.GetPlanet(op.PlanetId).Name}: it seized {take} materials and cut off the garrison's reserves.");
        }

        static void SeizeAttackerSupplies(GalaxyState g, Operation op)
        {
            if (!g.Slice.Applied.Add("opfollow:" + op.Id)) return;
            var d = g.GetPolity(op.DefenderPolity); var a = g.GetPolity(op.AttackerPolity);
            int take = Math.Min(FollowUpMaterials, a.Stock.Materials);
            a.Stock = a.Stock - new Resources(0, take, 0, 0); d.Stock = d.Stock + new Resources(0, take, 0, 0);
            PassageWar.ForPolity(g, op.DefenderPolity, "operation", $"The follow-up at {g.GetSystem(op.SystemId).Name} seized {take} materials of the attacker's supplies", new Resources(0, -take, 0, 0));
            PassageWar.ForPolity(g, op.AttackerPolity, "operation", $"Lost {take} materials of supplies to the defender's follow-up", new Resources(0, take, 0, 0));
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, $"{d.Name}'s follow-up succeeded: it seized {take} materials of {a.Name}'s supplies" + (op.Prisoners.Count > 0 ? $" and holds {op.Prisoners.Count} prisoner(s)." : "."));
        }

        // ------------------------------------------------------------------ decisions between engagements

        public static StratResult DefenderStops(GalaxyState g, Operation op)
        {
            if (op.Phase != OperationPhase.DefenderChoice) return StratResult.Reject("The defender has no choice to make now.");
            op.Phase = OperationPhase.AttackerChoice;
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, $"{g.GetPolity(op.DefenderPolity).Name} kept its supplies and did not seek a second engagement.");
            return StratResult.Accept();
        }

        public static StratResult DefenderFollowUp(GalaxyState g, Operation op)
        {
            if (op.Phase != OperationPhase.DefenderChoice) return StratResult.Reject("The defender has no choice to make now.");
            var f = g.GetFleet(op.DefenderFleetId);
            if (f == null || !(StrategyRules.CanFight(f) || Successors(g, op, op.DefenderPolity, BattleDomain.Orbital).Count > 0)) return StratResult.Reject("The defending fleet cannot fight again.");
            op.FollowUp = true;
            op.Phase = OperationPhase.Orbital;
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, $"{g.GetPolity(op.DefenderPolity).Name} elected a second engagement: it risks its committed ships for the attacker's supplies; its supply protection is already earned.");
            return StratResult.Accept();
        }

        /// <summary>The AI defender's rule for the follow-up (the same information the player has): elect it only with a
        /// clearly stronger fleet (total hull at least 1.25 times the attacker's).</summary>
        public static bool AIWantsFollowUp(GalaxyState g, Operation op)
        {
            int d = g.GetFleet(op.DefenderFleetId)?.Ships.Sum(s => s.Hp) ?? 0, a = g.GetFleet(op.AttackerFleetId)?.Ships.Sum(s => s.Hp) ?? 0;
            return d * 4 >= a * 5;
        }

        public static StratResult Land(GalaxyState g, Operation op)
        {
            if (op.Phase != OperationPhase.AttackerChoice) return StratResult.Reject("Landing waits for the orbital phase to finish.");
            var carried = Embarked(g, op.AttackerFleetId);
            if (carried == null || carried.Units.Count == 0) return StratResult.Reject("No ground units remain aboard.");
            op.Landed = carried.Units.Select(u => u.Uid).ToList();
            op.HasLanded = true;
            op.Phase = OperationPhase.Series;
            var sys = g.GetSystem(op.SystemId);
            if (sys.Conquest < ConquestStage.LandingEstablished) { sys.Conquest = ConquestStage.LandingEstablished; sys.ConquestBy = op.AttackerPolity; }
            SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, $"{g.GetPolity(op.AttackerPolity).Name} landed {op.Landed.Count} ground unit(s) on {g.GetPlanet(op.PlanetId).Name}.");
            if ((Garrison(g, op.PlanetId)?.Units.Count ?? 0) == 0)
                Concede(g, op, op.DefenderPolity, BattleDomain.Planetary, $"{g.GetPlanet(op.PlanetId).Name} had no garrison to deploy; it conceded the match.");
            return StratResult.Accept();
        }

        /// <summary>The attacker withdraws at any decision point: the fleet and any landed troops leave, nothing changes hands.</summary>
        public static StratResult Withdraw(GalaxyState g, Operation op)
        {
            if (op.Ended) return StratResult.Reject("The operation is over.");
            if (op.PendingBattleId != null && g.ActiveBattle?.Setup?.BattleId == op.PendingBattleId) return StratResult.Reject("Finish the battle in progress first.");
            Settle(g, op, op.DefenderPolity, $"{g.GetPolity(op.AttackerPolity).Name} withdrew from {g.GetPlanet(op.PlanetId).Name}.", withdrawn: true);
            return StratResult.Accept();
        }

        /// <summary>Terms: the defender yields the world, its garrison leaving intact. Decided from what both sides can see.</summary>
        public static OfferVerdict EvaluateSurrender(GalaxyState g, Operation op)
        {
            var v = new OfferVerdict { Valid = !op.Ended };
            if (!v.Valid) { v.Reason = "The operation is over."; return v; }
            int pressure = 0;
            if (op.SuppliesSeized) { pressure += SurrenderPressureSeized; v.Factors.Add($"+{SurrenderPressureSeized}: the world's supplies were seized"); }
            if (op.AttackerWins > op.DefenderWins) { pressure += SurrenderPressureBehind; v.Factors.Add($"+{SurrenderPressureBehind}: the series stands {op.AttackerWins}-{op.DefenderWins} against us"); }
            int ours = GroundFor(g, op, op.DefenderPolity).Sum(u => u.Hp), theirs = GroundFor(g, op, op.AttackerPolity).Sum(u => u.Hp);
            int sp = ours > 0 ? Math.Max(0, Math.Min(30, (theirs - ours) * 30 / ours)) : 30;
            if (sp > 0) { pressure += sp; v.Factors.Add($"+{sp}: their ground forces ({theirs} hull) against ours ({ours})"); }
            v.Factors.Add($"−{WorldValue}: the world");
            v.Pressure = pressure; v.Cost = WorldValue; v.Accepted = pressure >= WorldValue;
            v.Reason = (v.Accepted ? "Accepted" : "Rejected") + $": pressure {pressure} against cost {WorldValue}. " + string.Join("; ", v.Factors) + ".";
            return v;
        }

        public static StratResult OfferSurrenderTerms(GalaxyState g, Operation op, out OfferVerdict v)
        {
            v = EvaluateSurrender(g, op);
            if (!v.Valid) return StratResult.Reject(v.Reason);
            if (op.Phase != OperationPhase.AttackerChoice && op.Phase != OperationPhase.Series) { v.Reason = "Terms can be offered between engagements, after the orbital phase."; return StratResult.Reject(v.Reason); }
            if (!g.Slice.Applied.Add($"opterms:{op.Id}:{op.Battles.Count}")) return StratResult.Reject("Terms were already offered at this point of the operation.");
            if (!v.Accepted) { SliceCampaign.RecordEvent(g, "operation", op.SystemId, null, $"{g.GetPolity(op.DefenderPolity).Name} rejected terms for {g.GetPlanet(op.PlanetId).Name}. {v.Reason}"); return StratResult.Accept(v.Reason); }
            Settle(g, op, op.AttackerPolity, $"{g.GetPolity(op.DefenderPolity).Name} yielded {g.GetPlanet(op.PlanetId).Name} under terms; its garrison left intact.", terms: true);
            return StratResult.Accept(v.Reason);
        }

        // ------------------------------------------------------------------ the final settlement

        /// <summary>The operation's one final outcome, applied once. Only an attacker win (the series, or the defender yielding
        /// under terms) transfers the world. Battle casualties were applied by each battle; nothing here applies them again.</summary>
        static void Settle(GalaxyState g, Operation op, string winner, string text, bool withdrawn = false, bool terms = false)
        {
            if (!g.Slice.Applied.Add("opsettle:" + op.Id)) return;
            op.Phase = OperationPhase.Settled; op.WinnerPolity = winner; op.Outcome = text; op.EndedDay = g.Day;
            var p = g.GetPlanet(op.PlanetId); var sys = g.SystemOf(p);
            var carried = Embarked(g, op.AttackerFleetId);
            var fleet = g.GetFleet(op.AttackerFleetId);
            if (winner == op.AttackerPolity)
            {
                p.OwnerId = op.AttackerPolity;
                p.Project = null;
                var gar = Garrison(g, op.PlanetId);
                if (gar != null)
                {
                    if (terms)
                    {
                        // The garrison leaves intact for the defender's home world (joining any garrison there).
                        var where = RetreatWorld(g, op.DefenderPolity, sys.Id);
                        if (where == null) op.Prisoners.AddRange(gar.Units.Select(u => u.Uid));   // nowhere left to go
                        else ForceFor(g, op.DefenderPolity, where, null).Units.AddRange(gar.Units);
                        gar.Units.Clear(); g.Slice.Ground.Remove(gar);
                    }
                    else { op.Prisoners.AddRange(gar.Units.Select(u => u.Uid)); g.Slice.Ground.Remove(gar); }
                }
                // The landed units become the world's new garrison.
                if (carried != null)
                {
                    var landed = carried.Units.Where(u => op.Landed.Contains(u.Uid) || terms).ToList();
                    if (landed.Count > 0)
                    {
                        var newGar = ForceFor(g, op.AttackerPolity, op.PlanetId, null);
                        foreach (var u in landed) { carried.Units.Remove(u); newGar.Units.Add(u); }
                    }
                    if (carried.Units.Count == 0) g.Slice.Ground.Remove(carried);
                }
                if (sys.Planets.Where(x => x.OwnerId != null).All(x => x.OwnerId == op.AttackerPolity)) sys.OwnerId = op.AttackerPolity;
                sys.OrbitalControlId = op.AttackerPolity; sys.ConquestBy = null; sys.Conquest = ConquestStage.Conquered;
                SliceCampaign.RecordEvent(g, "conquest", sys.Id, null, $"{p.Name} passed to {g.GetPolity(op.AttackerPolity).Name}: {text}");
                PassageWar.ForPolity(g, op.AttackerPolity, "operation", $"Took {p.Name}" + (terms ? " under terms" : " after the planetary series"), default);
                PassageWar.ForPolity(g, op.DefenderPolity, "operation", $"Lost {p.Name}" + (terms ? " under terms" : ""), default);
            }
            else
            {
                // Landed troops re-embark if their fleet is still there; troops left behind become prisoners.
                if (carried != null && op.HasLanded && (fleet == null || fleet.Ships.Count == 0))
                { op.Prisoners.AddRange(carried.Units.Select(u => u.Uid)); g.Slice.Ground.Remove(carried); }
                // The attacker leaves: the orbit returns to the world's owner and the conquest stage is cleared.
                sys.OrbitalControlId = sys.OwnerId; sys.ConquestBy = null; sys.Conquest = ConquestStage.None;
                SliceCampaign.RecordEvent(g, "operation", sys.Id, null, text + $" {p.Name} stays {g.GetPolity(op.DefenderPolity).Name}.");
                PassageWar.ForPolity(g, op.AttackerPolity, "operation", (withdrawn ? "Withdrew from " : "Failed to take ") + p.Name, default);
                if (fleet != null && fleet.Ships.Count > 0) StrategyRules.Withdraw(g, fleet, sys);
            }
            if (op.Prisoners.Count > 0) SliceCampaign.RecordEvent(g, "prisoners", sys.Id, null, $"{op.Prisoners.Count} prisoner(s) taken in operation {op.Id}.");
            g.Note(winner == g.Player.Id ? "result" : "danger", $"Operation over: {text}");
        }

        /// <summary>Where a garrison that yields under terms goes: the polity's designated worlds first, then any world it holds.</summary>
        static string RetreatWorld(GalaxyState g, string polity, string exceptSystem)
        {
            var order = (PassageWar.Of(g)?.Designated.TryGetValue(polity, out var d) == true ? d : new List<string>()).Concat(g.Systems.Select(x => x.Id));
            foreach (var sid in order)
            {
                if (sid == exceptSystem) continue;
                var p = g.GetSystem(sid).Planets.Where(x => x.OwnerId == polity).OrderBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
                if (p != null) return p.Id;
            }
            return null;
        }
    }
}
