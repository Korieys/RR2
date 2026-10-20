using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Campaign alpha Prompt 17: the strategic rival plans from what its side can know and acts through the same commands.</summary>
    public sealed class RivalAITests
    {
        static Fleet AiFleet(GalaxyState g) => g.Fleets.First(f => f.OwnerId != g.Player.Id && f.AIControlled);
        static void Hold(GalaxyState g, string polity) { var p = War.Passage(g); p.AccessHolderId = polity; p.Basis = PassageWar.Battle; p.SinceDay = g.Day; }
        static string Plan(GalaxyState g, string polity) => War.Sc(g).AiPlan.TryGetValue(polity, out var p) ? p : null;

        [Test]
        public void ItDefendsCriticalAccess_WhenAnEnemyFleetApproaches()
        {
            var g = War.New(FactionId.Pygmaei);
            Hold(g, "inhumanus");
            RivalAI.Act(g, "inhumanus");
            Assert.AreNotEqual("defend:umbra", Plan(g, "inhumanus"), "no threat yet: no need to sit on the passage");
            Assert.IsTrue(StrategyRules.OrderMove(g, Camp.Mine(g).Id, "umbra").Ok);
            for (int i = 0; i < 3; i++) StrategyRules.AdvanceDay(g);
            Assert.AreEqual("defend:umbra", Plan(g, "inhumanus"), string.Join("\n", War.Sc(g).AiLog));
            var ai = AiFleet(g);
            Assert.IsTrue(ai.Moving && ai.Order.Path.Last() == "umbra", "it sails to hold the passage");
        }

        [Test]
        public void ItExploitsAKnownOverextension_AndReactsDifferentlyToADifferentPlan()
        {
            // The Inhumanus player holds the passage. Plan A: its fleet guards Umbra. Plan B: its fleet is far off at Gloam.
            var a = War.New(FactionId.Inhumanus); Hold(a, "inhumanus");
            var b = War.New(FactionId.Inhumanus); Hold(b, "inhumanus");
            Camp.Mine(a).SystemId = "umbra";
            Camp.Mine(b).SystemId = "gloam";
            RivalAI.Act(a, "pygmaei");
            RivalAI.Act(b, "pygmaei");
            Assert.AreNotEqual(Plan(a, "pygmaei"), Plan(b, "pygmaei"), "different player plans, different reactions");
            Assert.AreEqual("raid:shoal_haven", Plan(b, "pygmaei"), string.Join("\n", War.Sc(b).AiLog));
            Assert.IsFalse((Plan(a, "pygmaei") ?? "").StartsWith("raid"), "a guarded route is not raided");
            var ai = AiFleet(b);
            Assert.IsTrue(ai.Moving && ai.Order.Path.Last() == "shoal_haven");
            // Once it holds Shoal Haven, the player's supply to Umbra is cut and its hold stops counting.
            ai.SystemId = "shoal_haven"; ai.Order = null;
            Assert.IsFalse(Supply.Of(b, "inhumanus").Has("umbra"));
            Assert.IsTrue(PassageWar.HoldBlocker(b, War.Passage(b)).Contains("supply"));
        }

        [Test]
        public void ItRecoversLawfully_PayingForAReplacement_AndRepairingAtItsYard()
        {
            var g = War.New(FactionId.Pygmaei);
            var ai = AiFleet(g);
            var rival = g.GetPolity("inhumanus");
            ai.Ships.RemoveAll(s => ShipCatalog.IsLeader(s.DefId));
            rival.Stock = new Resources(500, 500, 500, 30);
            var before = rival.Stock;
            RivalAI.Act(g, "inhumanus");
            var yard = g.GetSystem("dividend").Planets.First(p => p.HasShipyard);
            Assert.IsNotNull(yard.Project, "it commissions a replacement through the shipyard");
            Assert.IsTrue(ShipCatalog.IsLeader(yard.Project.ShipDefId));
            Assert.AreEqual(before - yard.Project.Cost, rival.Stock, "paid for, not spawned");
            Assert.IsFalse(g.Fleets.Where(f => f.OwnerId == "inhumanus").Any(StrategyRules.CanFight), "and it does not appear until built");
            // A damaged flagship goes home and repairs, paying the same price the player would.
            var h = War.New(FactionId.Pygmaei);
            var ai2 = AiFleet(h);
            ai2.SystemId = "shoal_haven";
            var flag = ai2.Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            flag.Hp = 5;
            for (int i = 0; i < 60 && flag.Hp < SliceCampaign.MaxHull(h, flag); i++) StrategyRules.AdvanceDay(h);
            Assert.AreEqual(SliceCampaign.MaxHull(h, flag), flag.Hp, string.Join("\n", War.Sc(h).AiLog));
            Assert.IsTrue(War.Sc(h).Ledger.Any(l => l.Polity == "inhumanus" && l.Kind == "repair" && l.Cost.Materials > 0), "a paid repair");
        }

        [Test]
        public void TreatyResolution_TheRivalProposes_ThePlayerAnswers_AndATruceIsHonoured()
        {
            var g = War.New(FactionId.Pygmaei);
            // The Inhumanus are weaker than their threshold: their commercial doctrine buys rights instead of attacking.
            AiFleet(g).Ships.RemoveAll(s => !ShipCatalog.IsLeader(s.DefId));
            RivalAI.Act(g, "inhumanus");
            var pending = War.Sc(g).Pending;
            Assert.IsNotNull(pending, string.Join("\n", War.Sc(g).AiLog));
            Assert.AreEqual("rights", pending.Kind);
            Assert.IsTrue(g.Log.Any(l => l.Text.Contains("offers") && l.Text.Contains("passage rights")));
            RivalAI.Act(g, "inhumanus");
            Assert.AreSame(pending, War.Sc(g).Pending, "no negotiation spam: one proposal per interval");
            int pyg = g.Player.Stock.Credits;
            Assert.IsTrue(PassageWar.AnswerProposal(g, accept: true).Ok);
            Assert.AreEqual(pyg + pending.Payment, g.Player.Stock.Credits);
            Assert.AreEqual("inhumanus", War.Passage(g).AccessHolderId);
            Assert.AreEqual("truce", g.Slice.RelationOf("pygmaei", "inhumanus").Status);
            // The rival honours the truce: no declaration, no contest plan.
            for (int i = 0; i < 25; i++) StrategyRules.AdvanceDay(g);
            Assert.IsFalse(g.Slice.Objectives.Any(o => o.PolityId == "inhumanus" && o.Status == ObjectiveStatus.Declared));
            Assert.IsNull(g.PendingEngagement);
            // A violation by the player is recorded, costs standing, and the rival answers it as war.
            int standing = g.Slice.RelationOf("pygmaei", "inhumanus").Opinion(g.Day);
            Assert.IsTrue(SliceCampaign.DeclareObjective(g, "pygmaei", "umbra").Ok);
            Assert.IsTrue(g.Slice.History.Any(e => e.Kind == "violation"));
            Assert.AreEqual(standing + SliceCampaign.TruceBrokenOpinion, g.Slice.RelationOf("pygmaei", "inhumanus").Opinion(g.Day));
            Assert.AreEqual("war", g.Slice.RelationOf("pygmaei", "inhumanus").Status);
        }

        [Test]
        public void AStatusQuoTruce_IsANegotiatedInconclusiveEnd()
        {
            var g = War.New(FactionId.Pygmaei);
            Hold(g, "inhumanus");
            g.Slice.RelationOf("pygmaei", "inhumanus").Status = "war";
            AiFleet(g).Ships.RemoveAll(s => !ShipCatalog.IsLeader(s.DefId));
            AiFleet(g).SystemId = "umbra";
            RivalAI.Act(g, "inhumanus");
            Assert.IsNotNull(War.Sc(g).Pending, string.Join("\n", War.Sc(g).AiLog));
            Assert.AreEqual("statusquo", War.Sc(g).Pending.Kind);
            var back = Camp.Reload(g);
            Assert.IsTrue(PassageWar.AnswerProposal(g, accept: true).Ok);
            Assert.AreEqual(CampaignOutcome.Inconclusive, War.Sc(g).Outcome);
            Assert.IsTrue(War.Sc(g).EndText.Contains("negotiated status quo"));
            Assert.IsTrue(PassageWar.AnswerProposal(back, accept: false).Ok, "declining is also an answer");
            Assert.IsFalse(War.Sc(back).Ended);
        }

        [Test]
        public void ItCannotReadHiddenInformation()
        {
            // Its decisions are the same whatever the player's destination beyond the visible lane, treasury, projects, fitted
            // modules or dock stores: none of these is in its belief.
            var a = War.New(FactionId.Pygmaei); Hold(a, "pygmaei");
            Assert.IsTrue(StrategyRules.OrderMove(a, Camp.Mine(a).Id, "umbra").Ok);
            StrategyRules.AdvanceDay(a);
            var b = Camp.Reload(a);
            var mine = Camp.Mine(b);
            mine.Order.Path = new List<string> { mine.Order.Path[0] };   // a different destination beyond the visible lane
            b.Player.Stock = new Resources(1, 2, 3, 4);
            War.Sc(b).Stores["cinderhold"] = 55;
            b.Slice.Profile(Camp.Flagship(mine)).Modules.Add("heavy_battery");
            RivalAI.Act(a, "inhumanus");
            RivalAI.Act(b, "inhumanus");
            Assert.AreEqual(Plan(a, "inhumanus"), Plan(b, "inhumanus"));
            Assert.AreEqual(AiFleet(a).Order?.Path.LastOrDefault(), AiFleet(b).Order?.Path.LastOrDefault());
            Assert.AreEqual(a.GetPolity("inhumanus").Stock, b.GetPolity("inhumanus").Stock);
            var belief = Belief.Of(b, "inhumanus");
            var seen = belief.Fleets.First(f => f.OwnerId == "pygmaei");
            Assert.AreEqual(mine.Order.Path[0], seen.NextSystemId, "only the lane a fleet is visibly on");
        }

        [Test]
        public void ItDoesNotOscillate_AndItsReasonsStayInTheDeveloperLog()
        {
            var g = War.New(FactionId.Pygmaei);
            var changes = 0; string last = null;
            for (int i = 0; i < 90 && g.PendingEngagement == null; i++)
            {
                StrategyRules.AdvanceDay(g);
                var p = Plan(g, "inhumanus");
                if (p != last) { changes++; last = p; }
            }
            Assert.LessOrEqual(changes, 6, string.Join("\n", War.Sc(g).AiLog));
            Assert.IsTrue(War.Sc(g).AiLog.Count > 0);
            Assert.IsFalse(g.Log.Any(l => l.Text.Contains("plan ")), "reasons are not in the player's dispatch");
        }

        /// <summary>Automated campaign runs with the planner on both sides (seat swap): the same commands, budgets, supply and
        /// battle rules for each side. They check errors and softlocks, not fun or difficulty.</summary>
        [TestCase(FactionId.Pygmaei)]
        [TestCase(FactionId.Inhumanus)]
        public void AutomatedCampaign_BothSidesPlanned_EndsWithoutErrorsOrSoftlocks(FactionId player)
        {
            var g = War.New(player);
            War.Sc(g).AutoPlayer = true;
            War.Sc(g).MandateDay = 160;
            int battles = 0, lastProgressDay = 0;
            for (int i = 0; i < 400 && !War.Sc(g).Ended; i++)
            {
                if (g.PendingEngagement != null)
                {
                    var e = g.PendingEngagement;
                    var mine = SliceCampaign.Seats(g, e)[0];
                    var setup = BattleBridge.Prepare(g, e, BattleBridge.AutoSelect(g, mine), out var err);
                    Assert.IsNotNull(setup, err);
                    var bs = BattleSession.Begin(setup, out err);
                    var ai = new SliceAI();
                    for (int k = 0; k < 900 && !bs.State.IsOver; k++) Assert.IsTrue(bs.Submit(ai.Choose(bs.State), BattleSession.Host).Accepted);
                    Assert.IsTrue(bs.State.IsOver);
                    Assert.IsTrue(BattleBridge.Apply(g, setup, BattleBridge.Extract(setup, bs)).Ok);
                    battles++; lastProgressDay = g.Day;
                    continue;
                }
                var rep = StrategyRules.AdvanceDay(g);
                Assert.IsTrue(rep.Advanced || War.Sc(g).Ended, rep.BlockedReason);
                foreach (var pol in g.Polities.Where(p => p.Playable))
                    Assert.IsTrue(pol.Stock.Energy >= 0 && pol.Stock.Materials >= 0 && pol.Stock.Credits >= 0 && pol.Stock.Influence >= 0, pol.Id + " went negative");
                Camp.NoDuplicateShips(g);
            }
            Assert.IsTrue(War.Sc(g).Ended, $"no softlock: the run reached an ending (day {g.Day}, {battles} battles)\n" + string.Join("\n", War.Sc(g).AiLog.TakeLast(20)));
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
                Assert.IsTrue(War.Sc(g).Ledger.Any(l => l.Polity == pol && l.Kind == "upkeep"), pol + " paid upkeep under the same rule");
            Assert.IsNotNull(Camp.Reload(g));
        }
    }
}
