using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    static class Ops
    {
        /// <summary>The player defends as the Inhumanus; the Pygmaei raiders embark their legion and sail on Dividend. The
        /// meeting there becomes an invasion of Dividend's first world. Both sides' choices are made through the same API.</summary>
        public static (GalaxyState g, Operation op) InvasionOfDividend()
        {
            var g = War.New(FactionId.Inhumanus);
            War.Sc(g).MandateDay = 999;
            var raiders = g.Fleets.First(f => f.OwnerId == "pygmaei");
            raiders.AIControlled = false;   // the test commands the raiders itself (the rival planner leaves them alone)
            var legion = Operations.Garrison(g, Home(g, "forgefall", "pygmaei"));
            Assert.IsTrue(Operations.Embark(g, raiders.Id, legion.PlanetId, legion.Units.Select(u => u.Uid).ToList()).Ok);
            Assert.IsTrue(StrategyRules.OrderMove(g, raiders.Id, "dividend", asAI: true).Ok);
            for (int i = 0; i < 90 && g.PendingEngagement == null; i++) StrategyRules.AdvanceDay(g);
            Assert.IsNotNull(g.PendingEngagement, "the raiders meet the escort at Dividend");
            Assert.IsTrue(Operations.Launch(g, raiders.Id, Home(g, "dividend", "inhumanus"), out var op).Ok);
            Assert.AreEqual(OperationPhase.Orbital, op.Phase);
            Assert.IsNull(g.PendingEngagement, "the encounter became the operation's first engagement");
            return (g, op);
        }

        public static string Home(GalaxyState g, string sys, string pol) => g.GetSystem(sys).Planets.Where(p => p.OwnerId == pol).OrderBy(p => p.Id).First().Id;

        /// <summary>Fights the operation's next battle: the slice AI flies <paramref name="winner"/>, the other side passes.
        /// Returns false if the battle was conceded (no battle fought).</summary>
        public static bool Fight(GalaxyState g, Operation op, string winner)
        {
            var setup = Operations.PrepareNext(g, op, out var err, out var conceded);
            if (conceded) return false;
            Assert.IsNotNull(setup, err);
            var b = BattleSession.Begin(setup, out err);
            Assert.IsNotNull(b, err);
            var seats = Operations.SeatPolities(g, op);
            int w = Array.IndexOf(seats, winner);
            var ai = new SliceAI();
            for (int i = 0; i < 800 && !b.State.IsOver; i++)
            {
                var c = b.State.ActiveSeat == w ? ai.Choose(b.State) : setup.Config.Domain == BattleDomain.Orbital ? Loser(b.State) : Camp.Idle(b.State);
                Assert.IsTrue(b.Submit(c, BattleSession.Host).Accepted);
            }
            Assert.IsTrue(b.State.IsOver);
            var o = BattleBridge.Extract(setup, b);
            Assert.AreEqual(w, o.WinnerSeat, $"{winner} was meant to win {setup.BattleId} ({o.Reason})");
            Assert.IsTrue(Operations.ApplyBattle(g, setup, o).Ok);
            Assert.IsFalse(Operations.ApplyBattle(g, setup, o).Ok, "applied once");
            return true;
        }

        public static string Owner(GalaxyState g, Operation op) => g.GetPlanet(op.PlanetId).OwnerId;

        /// <summary>The losing side's pilot: the slice's blunder (its leader drives forward, the rest fire at whatever they can),
        /// so a lost engagement ends with the leader's fall rather than the annihilation of a side that only waited.</summary>
        static Command Loser(MatchState s)
        {
            var c = Pilots.Blunder(s);
            return RulesEngine.Validate(s, c).Ok ? c : Camp.Idle(s);
        }
    }

    /// <summary>Campaign alpha Prompt 16: orbital supply defence, the planetary series, succession and the final settlement.</summary>
    public sealed class OperationTests
    {
        [Test]
        public void EarlyStop_ThenA20Series_TransfersTheWorldOnlyAtTheEnd()
        {
            var (g, op) = Ops.InvasionOfDividend();
            var stakes = Operations.Stakes(g, op);
            Assert.IsTrue(stakes.Any(s => s.Contains("one victory in up to 2 orbital engagements")), string.Join("\n", stakes));
            Assert.IsTrue(stakes.Any(s => s.Contains("There is no random capture roll")));
            Assert.IsFalse(StrategyRules.AdvanceDay(g).Advanced, "strategic time stops during an operation");
            Ops.Fight(g, op, "inhumanus");                                   // the defender wins the first orbital engagement
            Assert.AreEqual(OperationPhase.DefenderChoice, op.Phase, op.Outcome);
            Assert.IsTrue(op.SuppliesProtected);
            Assert.IsTrue(Operations.DefenderStops(g, op).Ok);              // early stop: keep the supplies
            Assert.AreEqual(OperationPhase.AttackerChoice, op.Phase);
            Assert.AreEqual(1, op.OrbitalFought, "no second engagement after an early stop");
            Assert.IsTrue(Operations.Land(g, op).Ok);
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            Assert.AreEqual("inhumanus", Ops.Owner(g, op), "one won match takes nothing");
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            Assert.AreEqual(OperationPhase.Settled, op.Phase);
            Assert.AreEqual(2, op.Battles.Count(b => b.Kind == "planetary"), "2-0 ends the series without a third match");
            Assert.AreEqual("pygmaei", Ops.Owner(g, op));
            Assert.AreEqual(ConquestStage.Conquered, g.GetSystem("dividend").Conquest);
            Assert.IsNotNull(Operations.Garrison(g, op.PlanetId));
            Assert.AreEqual("pygmaei", Operations.Garrison(g, op.PlanetId).OwnerId, "the landed troops hold the world");
            Assert.IsTrue(StrategyRules.AdvanceDay(g).Advanced, "time moves again once the operation is settled");
            Assert.IsNotNull(PassageWar.DesignatedProblem(g, "inhumanus"), "the mandate sees the lost world");
            Camp.NoDuplicateShips(g);
            var units = Operations.AllUnits(g).Select(u => u.Uid).ToList();
            Assert.AreEqual(units.Count, units.Distinct().Count(), "no ground unit is in two places");
        }

        [Test]
        public void ForcedSecondDefence_SupplySeizure_Succession_AndA21Series()
        {
            var (g, op) = Ops.InvasionOfDividend();
            int pygMaterials = g.GetPolity("pygmaei").Stock.Materials;
            Ops.Fight(g, op, "pygmaei");
            Assert.AreEqual(OperationPhase.Orbital, op.Phase, "the defender gets a forced second chance");
            // If the defender's flagship fell, an acting flagship from its surviving ships leads the second engagement.
            var cmd = Operations.Commander(g, op, "inhumanus", BattleDomain.Orbital);
            if (!g.Fleets.Any(f => f.OwnerId == "inhumanus" && f.Ships.Any(s => ShipCatalog.IsLeader(s.DefId))))
            {
                Operations.EnsureCommanders(g, op, BattleDomain.Orbital);
                cmd = Operations.Commander(g, op, "inhumanus", BattleDomain.Orbital);
                Assert.IsTrue(cmd.uid == 0 || cmd.acting, "a successor is acting, never the destroyed flagship");
            }
            Ops.Fight(g, op, "pygmaei");
            Assert.IsTrue(op.SuppliesSeized);
            Assert.Greater(g.GetPolity("pygmaei").Stock.Materials, pygMaterials, "the attacker took the world's supplies");
            Assert.AreEqual(OperationPhase.AttackerChoice, op.Phase);
            Assert.IsTrue(Operations.Land(g, op).Ok);
            var garrison = new HashSet<int>(Operations.Garrison(g, op.PlanetId).Units.Select(u => u.Uid));
            Assert.Greater(garrison.Count, Operations.DeployCap, "the garrison has reserves beyond one deployment");
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            var firstDefenders = op.Battles.Last().Deployed.Where(garrison.Contains).ToList();
            Assert.IsTrue(Ops.Fight(g, op, "inhumanus"));
            var secondDefenders = op.Battles.Last().Deployed.Where(garrison.Contains).ToList();
            Assert.IsTrue(secondDefenders.All(firstDefenders.Contains), "cut off: no reserves joined the defender after its supplies were seized");
            Assert.AreEqual("inhumanus", Ops.Owner(g, op), "1-1: nothing has changed hands");
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            Assert.AreEqual(OperationPhase.Settled, op.Phase);
            Assert.AreEqual(3, op.Battles.Count(b => b.Kind == "planetary"));
            Assert.AreEqual("pygmaei", Ops.Owner(g, op), "2-1");
        }

        [Test]
        public void VoluntaryFollowUp_WonSeizesAttackerSupplies_LostKeepsTheEarnedProtection()
        {
            var (g, op) = Ops.InvasionOfDividend();
            Ops.Fight(g, op, "inhumanus");
            Assert.AreEqual(OperationPhase.DefenderChoice, op.Phase);
            var back = Camp.Reload(g);
            var opB = Operations.Get(back, op.Id);
            // Won: the defender seizes some of the attacker's supplies.
            int pyg = g.GetPolity("pygmaei").Stock.Materials;
            Assert.IsTrue(Operations.DefenderFollowUp(g, op).Ok);
            Ops.Fight(g, op, "inhumanus");
            Assert.IsTrue(op.SuppliesProtected);
            Assert.Less(g.GetPolity("pygmaei").Stock.Materials, pyg, "the follow-up seized attacker supplies");
            Assert.AreEqual(OperationPhase.AttackerChoice, op.Phase);
            // Lost (from the same save, with a defender that elects the follow-up with only its battered flagship): the
            // protection already earned is not revoked.
            var thin = back.GetFleet(opB.DefenderFleetId);
            thin.Ships.RemoveAll(x => !ShipCatalog.IsLeader(x.DefId));
            thin.Ships[0].Hp = 2;
            Assert.IsTrue(Operations.DefenderFollowUp(back, opB).Ok);
            Ops.Fight(back, opB, "pygmaei");
            Assert.IsTrue(opB.SuppliesProtected, "an elected follow-up never revokes the earned protection");
            Assert.IsFalse(opB.SuppliesSeized);
            Assert.AreEqual(OperationPhase.AttackerChoice, opB.Phase);
        }

        [Test]
        public void NoDeployableSuccessor_Concedes_AndWithdrawalIsLegal()
        {
            var (g, op) = Ops.InvasionOfDividend();
            // The defender's fleet is only its flagship; once it falls, no successor exists for the second engagement.
            var escort = g.GetFleet(op.DefenderFleetId);
            escort.Ships.RemoveAll(s => !ShipCatalog.IsLeader(s.DefId));
            Ops.Fight(g, op, "pygmaei");
            Assert.AreEqual(0, Operations.Successors(g, op, "inhumanus", BattleDomain.Orbital).Count);
            Assert.IsFalse(Ops.Fight(g, op, "pygmaei"), "conceded: no battle is fought");
            Assert.IsTrue(op.Battles.Last().Conceded);
            Assert.IsTrue(op.SuppliesSeized, "the concession counts as the attacker's second win");
            // The attacker may still withdraw: nothing changes hands, the fleet leaves, time resumes.
            Assert.IsTrue(Operations.Withdraw(g, op).Ok);
            Assert.AreEqual(OperationPhase.Settled, op.Phase);
            Assert.AreEqual("inhumanus", Ops.Owner(g, op));
            Assert.AreEqual(ConquestStage.None, g.GetSystem("dividend").Conquest);
            Assert.IsTrue(StrategyRules.AdvanceDay(g).Advanced);
            Assert.IsNotNull(Operations.Embarked(g, op.AttackerFleetId), "the troops left with their fleet");
        }

        /// <summary>Prompt 20 regression (Pygmaei journey): with its supplies seized and its commander fallen in the first match,
        /// the defender named a successor from its cut-off reserves, which could not deploy, and the operation stalled for good.
        /// Succession now draws only on units that may fight; with none, the match is conceded. Also checked after a reload.</summary>
        [Test]
        public void CutOffReserves_NeverProvideTheSuccessor_TheMatchIsFoughtByASurvivorOrConceded()
        {
            foreach (bool survivor in new[] { true, false })
            {
                var (g, op) = Ops.InvasionOfDividend();
                Ops.Fight(g, op, "pygmaei");
                if (op.Phase == OperationPhase.Orbital) Ops.Fight(g, op, "pygmaei");
                Assert.IsTrue(op.SuppliesSeized);
                Assert.IsTrue(Operations.Land(g, op).Ok);
                Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
                var gar = Operations.Garrison(g, op.PlanetId);
                var deployed = new HashSet<int>(op.Battles.Last().Deployed);
                var keep = survivor ? gar.Units.Where(u => deployed.Contains(u.Uid) && !ShipCatalog.IsLeader(u.DefId)).Select(u => u.Uid).Take(1).ToList() : new List<int>();
                if (survivor) Assert.AreEqual(1, keep.Count, "a non-leader defender deployed in the first match");
                gar.Units.RemoveAll(u => deployed.Contains(u.Uid) && !keep.Contains(u.Uid));   // the commander and the rest fell
                Assert.Greater(gar.Units.Count(u => !deployed.Contains(u.Uid)), 0, "cut-off reserves remain on the world");
                var back = Camp.Reload(g);
                foreach (var (gg, o) in new[] { (g, op), (back, Operations.Active(back)) })
                {
                    var setup = Operations.PrepareNext(gg, o, out var err, out var conceded);
                    if (survivor)
                    {
                        Assert.IsNotNull(setup, err);
                        var cmd = Operations.Commander(gg, o, "inhumanus", BattleDomain.Planetary);
                        Assert.IsTrue(cmd.acting && cmd.uid == keep[0], "the surviving deployed unit commands, not a reserve");
                        Assert.IsTrue(Operations.Lineup(gg, o, "inhumanus", out _).All(u => deployed.Contains(u.Uid)));
                    }
                    else
                    {
                        Assert.IsTrue(conceded, "no defender that may fight survives: conceded, not stalled (" + err + ")");
                        Assert.AreEqual(OperationPhase.Settled, o.Phase);
                        Assert.AreEqual("pygmaei", gg.GetPlanet(o.PlanetId).OwnerId, "2-0 by concession");
                    }
                }
            }
        }

        [Test]
        public void AGarrisonWithoutACommanderOrSuccessor_ConcedesTheMatch()
        {
            var (g, op) = Ops.InvasionOfDividend();
            Operations.Garrison(g, op.PlanetId).Units.RemoveAll(u => !ShipCatalog.IsLeader(u.DefId));
            Ops.Fight(g, op, "inhumanus");
            Assert.IsTrue(Operations.DefenderStops(g, op).Ok);
            Assert.IsTrue(Operations.Land(g, op).Ok);
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));                       // the lone commander falls
            Assert.AreEqual(0, Operations.Garrison(g, op.PlanetId)?.Units.Count ?? 0);
            Assert.IsFalse(Ops.Fight(g, op, "pygmaei"), "no units: the second match is conceded");
            Assert.AreEqual(OperationPhase.Settled, op.Phase);
            Assert.AreEqual("pygmaei", Ops.Owner(g, op));
        }

        [Test]
        public void AcceptedTerms_TransferTheWorld_AndTheGarrisonLeavesIntact()
        {
            var (g, op) = Ops.InvasionOfDividend();
            var early = Operations.EvaluateSurrender(g, op);
            Assert.IsFalse(early.Accepted, early.Reason);
            Ops.Fight(g, op, "pygmaei");
            Ops.Fight(g, op, "pygmaei");
            Assert.IsTrue(op.SuppliesSeized);
            Assert.IsTrue(Operations.Land(g, op).Ok);
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            var v = Operations.EvaluateSurrender(g, op);
            Assert.IsTrue(v.Accepted, v.Reason);
            int garrison = Operations.Garrison(g, op.PlanetId).Units.Count;
            Assert.IsTrue(Operations.OfferSurrenderTerms(g, op, out _).Ok);
            Assert.AreEqual(OperationPhase.Settled, op.Phase);
            Assert.AreEqual("pygmaei", Ops.Owner(g, op));
            var home = Operations.Garrison(g, Ops.Home(g, "ledgerhold", "inhumanus"));
            Assert.IsNotNull(home);
            Assert.GreaterOrEqual(home.Units.Count, garrison, "the yielded garrison joined the next world's garrison");
            Assert.IsFalse(Operations.OfferSurrenderTerms(g, op, out _).Ok, "settled once");
        }

        [Test]
        public void SaveAndLoad_BetweenEngagements_AndDuringABattle()
        {
            var (g, op) = Ops.InvasionOfDividend();
            var back = Camp.Reload(g);
            var opB = Operations.Get(back, op.Id);
            Assert.AreEqual(op.Phase, opB.Phase);
            var s1 = Operations.PrepareNext(g, op, out var e1, out _);
            var s2 = Operations.PrepareNext(back, opB, out var e2, out _);
            Assert.AreEqual(s1.BattleId, s2.BattleId);
            Assert.AreEqual(SaveGame.Digest(g), SaveGame.Digest(back));
            // A battle saved part-way restores exactly and still belongs to its operation.
            var b = BattleSession.Begin(s1, out _);
            var ai = new SliceAI();
            for (int i = 0; i < 10; i++) b.Submit(ai.Choose(b.State), BattleSession.Host);
            g.ActiveBattle = b.Snapshot();
            var mid = Camp.Reload(g);
            Assert.AreEqual(op.Id, mid.ActiveBattle.Setup.OperationId);
            var resumed = BattleSession.Restore(mid.ActiveBattle, out var err);
            Assert.IsNotNull(resumed, err);
            Assert.AreEqual(b.Hash, resumed.Hash);
            g.ActiveBattle = null;
        }

        [Test]
        public void ExhaustedRounds_NeverDraw_AFullTieGoesToTheDefender()
        {
            // Two identical armies that only wait until the round limit: equal objectives, equal value, equal commanders.
            var cfg = new MatchConfig { ClockSeconds = 0 }.UseRoster(BattleDomain.Planetary);
            cfg.MapId = "proving_ground"; cfg.TechMode = TechMode.Balanced; cfg.FirstSeat = 0; cfg.DefenderSeat = 1;
            for (int seat = 0; seat < 2; seat++)
            {
                cfg.Players[seat] = new PlayerSetup { DisplayName = "side " + seat, Caps = BattleBridge.Caps.Clone(), Army = new ArmyRoster { Faction = FactionId.Pygmaei, Name = "side " + seat } };
                foreach (var id in new[] { "pygmaei_iron_marshal", "pygmaei_rush_trooper", "pygmaei_rush_trooper" }) cfg.Players[seat].Army.Units.Add(new RosterEntry(id));
            }
            var setup = new BattleSetup { BattleId = "draw_fixture", Config = cfg, Scenario = "fixture" };
            var b = BattleSession.Begin(setup, out var err);
            Assert.IsNotNull(b, err);
            for (int i = 0; i < 400 && !b.State.IsOver; i++) Assert.IsTrue(b.Submit(Camp.Idle(b.State), BattleSession.Host).Accepted);
            Assert.IsTrue(b.State.IsOver);
            Assert.AreEqual(VictoryReason.RoundLimitObjectives, b.State.VictoryReason);
            Assert.AreEqual(1, b.State.Winner, "a full tie at the round limit goes to the defender, so a series always ends by its third match");
        }

        [Test]
        public void AnActingCommander_LeadsAndEndsTheBattleLikeALeader()
        {
            var army = new ArmyRoster { Faction = FactionId.Pygmaei, Name = "fixture" };
            army.Units.Add(new RosterEntry("pygmaei_ember_vanguard") { Equipment = { SliceRules.ActingCommandToken } });
            army.Units.Add(new RosterEntry("pygmaei_rush_trooper"));
            Assert.IsTrue(RosterArmy.Validate(army, BattleBridge.Caps, BattleDomain.Planetary).Valid, "an acting commander is the army's one leader");
            var twoLeaders = new ArmyRoster { Faction = FactionId.Pygmaei, Name = "fixture" };
            twoLeaders.Units.Add(new RosterEntry("pygmaei_iron_marshal"));
            twoLeaders.Units.Add(new RosterEntry("pygmaei_ember_vanguard") { Equipment = { SliceRules.ActingCommandToken } });
            Assert.IsFalse(RosterArmy.Validate(twoLeaders, BattleBridge.Caps, BattleDomain.Planetary).Valid, "never two leaders");
            var cfg = new MatchConfig { ClockSeconds = 0 }.UseRoster(BattleDomain.Planetary);
            cfg.MapId = "proving_ground"; cfg.TechMode = TechMode.Balanced;
            cfg.Players[0] = new PlayerSetup { Caps = BattleBridge.Caps.Clone(), Army = army };
            cfg.Players[1] = new PlayerSetup { Caps = BattleBridge.Caps.Clone(), Army = new ArmyRoster { Faction = FactionId.Pygmaei, Units = { new RosterEntry("pygmaei_iron_marshal") } } };
            var s = RulesEngine.CreateMatch(cfg, out var err);
            Assert.IsNotNull(s, err);
            Assert.AreEqual(s.Units.First(u => u.Owner == 0 && u.DefId == "pygmaei_ember_vanguard").Id, s.Players[0].CommanderId);
        }

        [Test]
        public void TransportCapacity_IsEnforced_AndLostShipsTakeTheirTroops()
        {
            var g = War.New();
            var raiders = Camp.Mine(g);
            var legion = Operations.Garrison(g, Ops.Home(g, "forgefall", "pygmaei"));
            Assert.AreEqual(raiders.Ships.Count + 1, Operations.Capacity(raiders), "a flagship carries two, other ships one");
            var tooMany = legion.Units.Select(u => u.Uid).ToList();
            var extra = Operations.NewForce(g, "pygmaei", legion.PlanetId, null, "pygmaei_rush_trooper", "pygmaei_rush_trooper");
            legion.Units.AddRange(extra.Units); g.Slice.Ground.Remove(extra);
            var r = Operations.Embark(g, raiders.Id, legion.PlanetId, legion.Units.Select(u => u.Uid).ToList());
            Assert.IsFalse(r.Ok, "over capacity");
            Assert.IsTrue(r.Reason.Contains("carry"), r.Reason);
            Assert.IsTrue(Operations.Embark(g, raiders.Id, legion.PlanetId, tooMany).Ok);
            int aboard = Operations.Embarked(g, raiders.Id).Units.Count;
            raiders.Ships.RemoveAll(s => !ShipCatalog.IsLeader(s.DefId) && s.DefId == "pygmaei_skirmish_frigate");
            Operations.EnforceCapacity(g);
            Assert.AreEqual(Math.Min(aboard, Operations.Capacity(raiders)), Operations.Embarked(g, raiders.Id).Units.Count);
            Assert.IsTrue(g.Slice.History.Any(e => e.Kind == "troops" && e.Text.Contains("lost with the transports")));
        }

        [Test]
        public void BothSides_CanInspectEveryObligation()
        {
            var (g, op) = Ops.InvasionOfDividend();
            var lines = Operations.Obligations(g, op);
            Assert.IsTrue(lines.Any(l => l.StartsWith("Pygmaei")) && lines.Any(l => l.StartsWith("Inhumanus")), string.Join("\n", lines));
            Assert.IsTrue(lines[0].StartsWith("Orbital engagement 1"));
            Assert.IsFalse(Operations.Land(g, op).Ok, "landing waits for the orbital phase");
            Assert.IsFalse(Operations.DefenderStops(g, op).Ok);
        }
    }
}
