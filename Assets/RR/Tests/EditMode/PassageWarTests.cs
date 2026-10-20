using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    static class War
    {
        public static GalaxyState New(FactionId f = FactionId.Pygmaei) => PassageWar.Create(f);
        public static ScenarioState Sc(GalaxyState g) => PassageWar.Of(g);
        public static PassageState Passage(GalaxyState g) => g.Slice.Passage(SliceCampaign.PassageSystem);

        /// <summary>Resolves the pending engagement: the player's side is flown by the slice AI against a defender that blunders
        /// (to win), or idles against the slice AI (to lose). Returns the outcome after it is applied.</summary>
        public static BattleOutcome Fight(GalaxyState g, bool win)
        {
            var (setup, b) = Camp.Begin(g);
            var ai = new SliceAI();
            if (win) Camp.Play(b, ai.Choose, Pilots.Blunder); else Camp.Play(b, Camp.Idle, ai.Choose);
            var o = BattleBridge.Extract(setup, b);
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            return o;
        }

        /// <summary>Advances day by day until the scenario ends (or <paramref name="days"/> pass), fighting any encounter.</summary>
        public static void Run(GalaxyState g, int days, bool win)
        {
            for (int i = 0; i < days && !Sc(g).Ended; i++)
            {
                if (g.PendingEngagement != null) { Fight(g, win); continue; }
                StrategyRules.AdvanceDay(g);
            }
        }

        public static void ToDay(GalaxyState g, int day)
        {
            while (g.Day < day && g.PendingEngagement == null && !Sc(g).Ended) Assert.IsTrue(StrategyRules.AdvanceDay(g).Advanced);
        }

        public static Planet Yard(GalaxyState g) => g.GetSystem(SliceCampaign.HomeOf(g.Player.Id)).Planets.First(p => p.HasShipyard && p.OwnerId == g.Player.Id);
    }

    /// <summary>Campaign alpha Prompt 14: a bounded campaign with a beginning, an ending and recovery in between.</summary>
    public sealed class PassageWarTests
    {
        [Test]
        public void TheScenario_IsAConfigurationOfTheExistingCampaign_WithABoundedScope()
        {
            var g = War.New();
            var sc = War.Sc(g);
            Assert.AreEqual(44, g.Systems.Count, "the wider galaxy is kept");
            Assert.That(sc.ActiveSystems.Count, Is.InRange(8, 12));
            Assert.IsTrue(sc.ActiveSystems.All(id => g.GetSystem(id) != null));
            Assert.IsTrue(g.Polities.Any(p => p.Id == "silex") && g.Polities.Any(p => p.Id == "craigon"), "other factions are not deleted");
            var mine = Camp.Mine(g);
            Assert.IsTrue(StrategyRules.PlanRoute(g, mine, "umbra").Legal);
            Assert.IsFalse(StrategyRules.PlanRoute(g, mine, "aurelith").Legal, "outside the scenario's active scope");
            // Alternate routes: the active network still connects both bases with Umbra removed (the northern route).
            Assert.IsTrue(Connected(g, "forgefall", "dividend", sc.ActiveSystems.Where(x => x != "umbra").ToList()));
            Assert.IsTrue(Connected(g, "forgefall", "umbra", sc.ActiveSystems.Where(x => x != "gloam").ToList()), "two ways into the passage");
            var free = SliceCampaign.Create(FactionId.Pygmaei);
            Assert.IsNull(free.Slice.Scenario, "the freeform campaign is unchanged");
            Assert.IsTrue(StrategyRules.PlanRoute(free, Camp.Mine(free), "aurelith").Legal);
            var req = PassageWar.Requirements(g);
            Assert.IsTrue(req.Any(l => l.Contains("3 consecutive settlements")), string.Join("\n", req));
            Assert.IsTrue(req.Any(l => l.Contains("A lost battle alone never ends the campaign")));
            Assert.IsTrue(req.Any(l => l.Contains("not connected to this scenario yet")), "incomplete planetary paths are labelled");
            var back = Camp.Reload(g);
            Assert.AreEqual(sc.ActiveSystems, War.Sc(back).ActiveSystems);
            Assert.AreEqual(sc.Designated["pygmaei"], War.Sc(back).Designated["pygmaei"]);
        }

        static bool Connected(GalaxyState g, string a, string b, List<string> allowed)
        {
            var seen = new HashSet<string> { a };
            var q = new Queue<string>(); q.Enqueue(a);
            while (q.Count > 0)
            {
                var x = q.Dequeue();
                if (x == b) return true;
                foreach (var l in g.LanesOf(x)) { var n = l.Other(x); if (allowed.Contains(n) && seen.Add(n)) q.Enqueue(n); }
            }
            return false;
        }

        [Test]
        public void MilitaryRoute_WinAndHold_EndsInVictory_AndTheEndingRecountsDecisions()
        {
            var g = War.New();
            Camp.ToContact(g);
            var o = War.Fight(g, win: true);
            Assert.AreEqual(0, o.WinnerSeat);
            Assert.AreEqual("pygmaei", War.Passage(g).AccessHolderId);
            Assert.AreEqual(PassageWar.Battle, War.Passage(g).Basis);
            Assert.IsFalse(War.Sc(g).Ended, "winning one battle is not winning the campaign");
            Assert.IsTrue(PassageWar.Progress(g).Contains("by battle"), PassageWar.Progress(g));
            War.Run(g, 300, win: true);
            var sc = War.Sc(g);
            Assert.AreEqual(CampaignOutcome.MilitaryVictory, sc.Outcome, sc.HoldNote + " | " + string.Join(" | ", sc.Reports.Select(r => string.Join("; ", r.Lines))));
            Assert.IsTrue(sc.Reports.Count >= 1);
            Assert.IsFalse(StrategyRules.AdvanceDay(g).Advanced, "nothing passive happens after the ending");
            var ending = PassageWar.Ending(g);
            Assert.IsTrue(ending.Any(l => l.Contains("Declared war over")), string.Join("\n", ending));
            Assert.IsTrue(ending.Any(l => l.Contains("Won the battle of Umbra")), string.Join("\n", ending));
            Assert.IsTrue(g.Slice.History.Any(e => e.Kind == "ending"));
            var back = Camp.Reload(g);
            Assert.AreEqual(CampaignOutcome.MilitaryVictory, War.Sc(back).Outcome);
            Assert.IsTrue(PassageWar.ContinueFreeform(back).Ok);
            Assert.IsTrue(StrategyRules.AdvanceDay(back).Advanced, "freeform continues the same galaxy");
            Assert.AreEqual(CampaignOutcome.MilitaryVictory, War.Sc(back).Outcome, "the recorded outcome stays");
        }

        [Test]
        public void ALostBattle_IsNotTerminal_AndRecoveryUsesDeclaredResourcesPlacesAndTime()
        {
            var g = War.New();
            Camp.ToContact(g);
            var o = War.Fight(g, win: false);
            Assert.AreEqual(1, o.WinnerSeat);
            Assert.AreEqual("inhumanus", War.Passage(g).AccessHolderId);
            War.ToDay(g, PassageWar.NextSettlementDay(g) + 1);
            Assert.IsFalse(War.Sc(g).Ended, "a lost battle never ends the campaign by itself");
            var me = g.Player.Id;
            bool flagshipLost = !g.Fleets.Any(f => f.OwnerId == me && StrategyRules.CanFight(f));
            if (flagshipLost)
            {
                Assert.IsTrue(PassageWar.Warnings(g).Any(w => w.Contains("no fleet led by a flagship")), string.Join(" | ", PassageWar.Warnings(g)));
                // Replacement command: a new flagship from the home shipyard, paid for and built over time.
                var yard = War.Yard(g);
                var opt = StrategyRules.ProjectOptions(g, yard.Id).First(x => ShipCatalog.IsLeader(x.ShipDefId));
                for (int i = 0; i < 200 && !g.Player.Stock.Covers(opt.Cost); i++) StrategyRules.AdvanceDay(g);
                int creditsBefore = g.Player.Stock.Credits;
                Assert.IsTrue(StrategyRules.StartProject(g, yard.Id, ProjectKind.CommissionShip, opt.ShipDefId).Ok);
                Assert.AreEqual(creditsBefore - opt.Cost.Credits, g.Player.Stock.Credits, "the replacement is paid for");
                Assert.IsTrue(PassageWar.FlagshipInProduction(g, me));
                for (int i = 0; i < opt.Days + 1; i++) StrategyRules.AdvanceDay(g);
                Assert.IsTrue(g.Fleets.Any(f => f.OwnerId == me && StrategyRules.CanFight(f)), "the replacement arrived after its declared build time");
            }
            // Rebuild the screen at the home shipyard, one hull at a time, each paid for and built over its declared time.
            var yard2 = War.Yard(g);
            for (int built = 0; built < 6 && g.Fleets.Where(f => f.OwnerId == me).Sum(f => f.Ships.Count) < 6; built++)
            {
                var esc = StrategyRules.ProjectOptions(g, yard2.Id).Where(x => x.ShipDefId != null && !ShipCatalog.IsLeader(x.ShipDefId)).OrderBy(x => x.Cost.Credits).First();
                for (int i = 0; i < 100 && !esc.Available; i++) { StrategyRules.AdvanceDay(g); esc = StrategyRules.ProjectOptions(g, yard2.Id).First(x => x.ShipDefId == esc.ShipDefId); }
                Assert.IsTrue(StrategyRules.StartProject(g, yard2.Id, ProjectKind.CommissionShip, esc.ShipDefId).Ok);
                for (int i = 0; i < esc.Days + 1; i++) StrategyRules.AdvanceDay(g);
            }
            // Bring the survivors home, repair, and try again (bounded by the attempt limit).
            foreach (var f in g.Fleets.Where(f => f.OwnerId == me && f.SystemId != "forgefall").ToList()) Assert.IsTrue(StrategyRules.OrderMove(g, f.Id, "forgefall").Ok);
            for (int i = 0; i < 80 && g.Fleets.Any(f => f.OwnerId == me && (f.Moving || f.SystemId != "forgefall")); i++) StrategyRules.AdvanceDay(g);
            var fleet = Camp.Mine(g);
            Assert.AreEqual("forgefall", fleet.SystemId);
            var flag = fleet.Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            if (flag.Hp < SliceCampaign.MaxHull(g, flag)) Assert.IsTrue(SliceCampaign.Repair(g, flag.Uid).Ok);
            Assert.IsFalse(War.Sc(g).Ended);
            Assert.That(Camp.Mine(g).Ships.Count, Is.GreaterThanOrEqualTo(5), "the screen was rebuilt");
            Camp.ToContact(g);
            var o2 = War.Fight(g, win: true);
            // The rematch is a real battle: either result must leave a coherent campaign (balance is not asserted here).
            if (o2.WinnerSeat == 0)
            {
                Assert.AreEqual(me, War.Passage(g).AccessHolderId, "recovered");
                War.Run(g, 300, win: true);
                Assert.IsTrue(War.Sc(g).Ended, War.Sc(g).HoldNote);
            }
            else
            {
                Assert.AreEqual(2, PassageWar.Attempts(g, me), "the second failure is counted");
                War.ToDay(g, PassageWar.NextSettlementDay(g) + 1);
                Assert.IsFalse(War.Sc(g).Ended, "two lost battles still leave a lawful attempt");
                Assert.IsTrue(PassageWar.NextActions(g).Any(a => a.Action == "declare" || a.Action == "shipyard" || a.Text.Contains("flagship")), string.Join(" | ", PassageWar.NextActions(g).Select(a => a.Text)));
            }
            var ending = War.Sc(g).Ended ? PassageWar.Ending(g) : new List<string>();
            if (War.Sc(g).Ended) Assert.IsTrue(ending.Any(l => l.Contains("Lost the battle of Umbra")), string.Join(" | ", ending));
            Assert.IsTrue(War.Sc(g).Ledger.Any(l => l.Polity == me && l.Kind == "production" && l.Cost.Credits > 0), "recovery was paid for");
            Camp.NoDuplicateShips(g);
        }

        [Test]
        public void Reloads_CannotResetHolds_RepeatSettlements_OrChangeTheOutcome()
        {
            var g = War.New();
            Camp.ToContact(g);
            War.Fight(g, win: true);
            War.ToDay(g, PassageWar.NextSettlementDay(g));
            int held = War.Sc(g).HoldCount;
            int reports = War.Sc(g).Reports.Count;
            var back = Camp.Reload(g);
            Assert.AreEqual(held, War.Sc(back).HoldCount, "the hold survives a reload");
            PassageWar.OnDay(back);   // the same settlement day again: keyed, so nothing changes
            Assert.AreEqual(held, War.Sc(back).HoldCount);
            Assert.AreEqual(reports, War.Sc(back).Reports.Count);
            // Two runs from the same save, one reloaded every day, reach the same outcome on the same day with the same ledger.
            var a = Camp.Reload(g); var b = Camp.Reload(g);
            for (int i = 0; i < 300 && !War.Sc(a).Ended; i++)
            {
                if (a.PendingEngagement != null) break;
                StrategyRules.AdvanceDay(a);
            }
            for (int i = 0; i < 300 && !War.Sc(b).Ended; i++)
            {
                if (b.PendingEngagement != null) break;
                StrategyRules.AdvanceDay(b);
                b = Camp.Reload(b);
            }
            Assert.AreEqual(a.Day, b.Day);
            Assert.AreEqual(War.Sc(a).Outcome, War.Sc(b).Outcome);
            Assert.AreEqual(SaveGame.Digest(a), SaveGame.Digest(b));
            int prize = War.Sc(a).Ledger.Count(l => l.Kind == "battle");
            Assert.AreEqual(prize, War.Sc(b).Ledger.Count(l => l.Kind == "battle"), "no duplicated battle rewards");
        }

        [Test]
        public void TreatyRoute_WithoutAnyBattle_BlockadeAndPayment_ThenRatification()
        {
            var g = War.New();
            var me = g.Player.Id;
            // Day 0: the rival has not been pressed, so a purchase is refused, with every factor stated.
            var v0 = PassageWar.EvaluateTreaty(g, me, 100);
            Assert.IsTrue(v0.Valid);
            Assert.IsFalse(v0.Accepted, v0.Reason);
            Assert.IsTrue(v0.Factors.Any(f => f.Contains("not lost a battle")), v0.Reason);
            Assert.IsTrue(PassageWar.ProposeTreaty(g, me, 100, out var r0).Ok);
            Assert.IsFalse(r0.Accepted);
            Assert.AreEqual(160, g.Player.Stock.Credits, "a declined treaty costs nothing");
            Assert.IsNotNull(PassageWar.CanPropose(g, me, 100), "one proposal per settlement interval");
            // Declare (the rival sails for Umbra), then go round by the northern route and blockade Dividend, its home.
            Assert.IsTrue(SliceCampaign.DeclareObjective(g, me, SliceCampaign.PassageSystem).Ok);
            Assert.IsTrue(StrategyRules.OrderMove(g, Camp.Mine(g).Id, "dividend").Ok);
            for (int i = 0; i < 120 && g.GetSystem("dividend").OrbitalControlId != me; i++)
            {
                Assert.IsNull(g.PendingEngagement, "the northern route avoids the rival fleet");
                StrategyRules.AdvanceDay(g);
            }
            Assert.IsTrue(g.GetSystem("dividend").Blockaded, "an unopposed orbit is taken, not the planets");
            Assert.AreEqual("inhumanus", g.GetSystem("dividend").OwnerId);
            for (int i = 0; i < 20 && PassageWar.CanPropose(g, me, 100) != null; i++) StrategyRules.AdvanceDay(g);
            var v = PassageWar.EvaluateTreaty(g, me, 100);
            Assert.IsTrue(v.Accepted, v.Reason);
            Assert.IsTrue(v.Factors.Any(f => f.Contains("blockaded")), v.Reason);
            int credits = g.Player.Stock.Credits;
            Assert.IsTrue(PassageWar.ProposeTreaty(g, me, 100, out _).Ok);
            Assert.AreEqual(credits - 100, g.Player.Stock.Credits);
            Assert.AreEqual(me, War.Passage(g).AccessHolderId);
            Assert.AreEqual(PassageWar.Treaty, War.Passage(g).Basis);
            Assert.AreEqual("truce", g.Slice.RelationOf("pygmaei", "inhumanus").Status);
            Assert.IsNull(SliceCampaign.CounterOffensiveDay(g, War.Passage(g)), "a truce prevents the counter-offensive");
            War.Run(g, 30, win: true);
            Assert.AreEqual(CampaignOutcome.TreatyVictory, War.Sc(g).Outcome, War.Sc(g).HoldNote);
            Assert.IsFalse(g.Slice.History.Any(e => e.Kind == "battle"), "no battle was fought");
            Assert.IsTrue(PassageWar.Ending(g).Any(l => l.Contains("by treaty")));
        }

        [Test]
        public void ATreaty_AfterAVictory_IsRatifiedAtTheNextSettlement()
        {
            var g = War.New();
            Camp.ToContact(g);
            War.Fight(g, win: true);
            var v = PassageWar.EvaluateTreaty(g, g.Player.Id, 100);
            Assert.IsTrue(v.Factors.Any(f => f.Contains("recognising")), v.Reason);
            Assert.IsFalse(v.Factors.Any(f => f.Contains("not lost a battle")), "the rival has lost a battle");
            Assert.IsTrue(v.Accepted, v.Reason);
            Assert.IsTrue(PassageWar.ProposeTreaty(g, g.Player.Id, 100, out _).Ok);
            Assert.AreEqual(PassageWar.Treaty, War.Passage(g).Basis);
            int next = PassageWar.NextSettlementDay(g);
            War.Run(g, 40, win: true);
            Assert.AreEqual(CampaignOutcome.TreatyVictory, War.Sc(g).Outcome);
            Assert.AreEqual(next, War.Sc(g).EndedDay, "ratified at the next settlement, no passive waiting");
        }

        [Test]
        public void UnderATruce_FleetsMeetingAtThePassage_DoNotFight()
        {
            var g = War.New();
            g.Slice.RelationOf("pygmaei", "inhumanus").Status = "truce";
            var mine = Camp.Mine(g); var theirs = Camp.Theirs(g);
            Assert.IsTrue(StrategyRules.OrderMove(g, mine.Id, "umbra").Ok);
            Assert.IsTrue(StrategyRules.OrderMove(g, theirs.Id, "umbra", asAI: true).Ok);
            for (int i = 0; i < 60; i++) { StrategyRules.AdvanceDay(g); Assert.IsNull(g.PendingEngagement); }
            Assert.AreEqual("umbra", mine.SystemId);
            Assert.AreEqual("umbra", theirs.SystemId);
        }

        [Test]
        public void Defeat_ComesOnlyFromDeclaredConditions_WithAWarningFirst()
        {
            // Exhausted mandate: the objective has failed three times and the rival holds the passage.
            var g = War.New();
            var me = g.Player.Id;
            g.Slice.Objectives.Add(new WarObjective { Id = "obj_fixture", PolityId = me, TargetPolityId = "inhumanus", SystemId = "umbra", Status = ObjectiveStatus.Lost, Attempts = 3 });
            War.Passage(g).AccessHolderId = "inhumanus"; War.Passage(g).Basis = PassageWar.Battle;
            Assert.IsTrue(PassageWar.Warnings(g).Any(w => w.Contains("Unless a treaty")), "warned before the settlement");
            Assert.IsFalse(SliceCampaign.DeclareObjective(g, me, "umbra").Ok, "no attempts left");
            War.ToDay(g, 10);
            Assert.AreEqual(CampaignOutcome.Defeat, War.Sc(g).Outcome);
            Assert.AreEqual(10, War.Sc(g).EndedDay);

            // Collapse: no fleet led by a flagship, none being built and nothing to pay for one.
            var c = War.New();
            var fleet = Camp.Mine(c);
            fleet.Ships.RemoveAll(s => ShipCatalog.IsLeader(s.DefId));
            c.Player.Stock = new Resources(0, 0, 0, 0);
            Assert.IsTrue(PassageWar.Warnings(c).Any(w => w.Contains("cannot pay for one")), string.Join(" | ", PassageWar.Warnings(c)));
            War.ToDay(c, 10);
            Assert.AreEqual(CampaignOutcome.Defeat, War.Sc(c).Outcome, War.Sc(c).EndText);

            // But with the means to rebuild, the same situation is not a defeat.
            var r = War.New();
            Camp.Mine(r).Ships.RemoveAll(s => ShipCatalog.IsLeader(s.DefId));
            r.Player.Stock = PassageWar.FlagshipCost(r.Player.Id);
            War.ToDay(r, 10);
            Assert.IsFalse(War.Sc(r).Ended);
        }

        [Test]
        public void TwoFailedObjectives_CountAsTwo_NotThree()
        {
            // Defect D-20: each objective record carries the running count of failures, so summing them counted the first
            // failure twice and ended the mandate after two lost battles.
            var g = War.New();
            var me = g.Player.Id;
            g.Slice.Objectives.Add(new WarObjective { Id = "o1", PolityId = me, TargetPolityId = "inhumanus", SystemId = "umbra", Status = ObjectiveStatus.Lost, Attempts = 1 });
            g.Slice.Objectives.Add(new WarObjective { Id = "o2", PolityId = me, TargetPolityId = "inhumanus", SystemId = "umbra", Status = ObjectiveStatus.Lost, Attempts = 2 });
            Assert.AreEqual(2, SliceCampaign.FailedAttempts(g, me, "umbra"));
            Assert.AreEqual(2, PassageWar.Attempts(g, me));
            Assert.IsFalse(PassageWar.Warnings(g).Any(w => w.Contains("failed 3 times")));
            Assert.IsTrue(SliceCampaign.DeclareObjective(g, me, "umbra").Ok, "a third attempt remains");
            Assert.AreEqual(2, g.Slice.Objectives.Last().Attempts, "the new record starts from the running count");
        }

        [Test]
        public void TheMandateExpires_AsAnInconclusiveEnd()
        {
            var g = War.New();
            War.Sc(g).MandateDay = 20;
            War.ToDay(g, 20);
            Assert.AreEqual(CampaignOutcome.Inconclusive, War.Sc(g).Outcome);
            Assert.AreEqual(20, War.Sc(g).EndedDay);
            Assert.IsTrue(War.Sc(g).EndText.Contains("contested"));
        }

        [Test]
        public void StrategicTime_StopsForBothSidesDuringAnEncounter()
        {
            var g = War.New();
            Camp.ToContact(g);
            int day = g.Day; var stock = g.Player.Stock; var rival = g.GetPolity("inhumanus").Stock;
            for (int i = 0; i < 5; i++) Assert.IsFalse(StrategyRules.AdvanceDay(g).Advanced);
            Assert.AreEqual(day, g.Day);
            Assert.AreEqual(stock, g.Player.Stock, "no income while the encounter is pending");
            Assert.AreEqual(rival, g.GetPolity("inhumanus").Stock);
        }
    }
}
