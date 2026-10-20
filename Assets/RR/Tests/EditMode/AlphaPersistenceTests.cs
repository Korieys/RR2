using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Campaign alpha Prompt 20: reloads at the campaign's boundaries change nothing and duplicate nothing.</summary>
    public sealed class AlphaPersistenceTests
    {
        static void NothingDuplicated(GalaxyState g)
        {
            Camp.NoDuplicateShips(g);
            var units = Operations.AllUnits(g).Select(u => u.Uid).ToList();
            Assert.AreEqual(units.Count, units.Distinct().Count(), "no ground unit in two places");
            var ships = g.Fleets.SelectMany(f => f.Ships).Select(s => s.Uid);
            Assert.IsFalse(units.Intersect(ships).Any(), "no uid is both a ship and a ground unit");
            Assert.AreEqual(g.Slice.History.Count, g.Slice.History.Select(h => h.Id).Distinct().Count(), "no narrative event twice");
            Assert.AreEqual(g.Slice.Wrecks.Count, g.Slice.Wrecks.Select(w => w.Id).Distinct().Count(), "no wreck twice");
            foreach (var op in g.Slice.Operations) Assert.AreEqual(op.Prisoners.Count, op.Prisoners.Distinct().Count(), "no prisoner twice");
            foreach (var p in g.Polities.Where(p => p.Playable)) Assert.IsTrue(p.Stock.Energy >= 0 && p.Stock.Materials >= 0 && p.Stock.Credits >= 0 && p.Stock.Influence >= 0);
        }

        [Test]
        public void PendingProduction_PriorityAndDoctrine_CompleteOnce_WhetherOrNotTheGameIsReloaded()
        {
            var g = War.New();
            g.Player.Stock = new Resources(600, 600, 600, 60);
            var yard = War.Yard(g);
            var esc = StrategyRules.ProjectOptions(g, yard.Id).First(o => o.ShipDefId == "pygmaei_skirmish_frigate");
            Assert.IsTrue(StrategyRules.StartProject(g, yard.Id, ProjectKind.CommissionShip, esc.ShipDefId).Ok);
            var iron = g.GetSystem("ironmouth").Planets.Where(p => p.OwnerId == "pygmaei").OrderBy(p => p.Id).First();
            Assert.IsTrue(StrategyRules.StartProject(g, iron.Id, ProjectKind.Priority, "ore_drive").Ok);
            Assert.IsTrue(Doctrines.Start(g, "pygmaei", "pyg_foundry_quotas").Ok);
            var a = Camp.Reload(g); var b = Camp.Reload(g);
            for (int i = 0; i < 30; i++) { StrategyRules.AdvanceDay(a); StrategyRules.AdvanceDay(b); b = Camp.Reload(b); }
            Assert.AreEqual(SaveGame.Digest(a), SaveGame.Digest(b));
            Assert.AreEqual(1, a.Fleets.Where(f => f.OwnerId == "pygmaei").SelectMany(f => f.Ships).Count(s => s.DefId == "pygmaei_skirmish_frigate") - Camp.Mine(g).Ships.Count(s => s.DefId == "pygmaei_skirmish_frigate"), "one ship built");
            Assert.IsTrue(Economy.Built(b, "pygmaei", "ore_drive") && Doctrines.Has(b, "pygmaei", "pyg_foundry_quotas"));
            Assert.AreEqual(1, War.Sc(b).Ledger.Count(l => l.Text == "Adopted Foundry quotas"));
            NothingDuplicated(b);
        }

        [Test]
        public void InterruptedAccess_ReloadedBeforeTheSettlement_CountsTheSame()
        {
            var g = War.New();
            var p = War.Passage(g); p.AccessHolderId = "pygmaei"; p.Basis = PassageWar.Battle;
            War.Sc(g).HoldRequired = 99;
            var rival = Camp.Theirs(g); rival.AIControlled = false;
            rival.SystemId = "gloam";
            var picket = new Fleet { Id = "fixture_picket", Name = "Fixture Picket", OwnerId = "inhumanus", SystemId = Economy.FreePort, HomeSystemId = "dividend" };
            picket.Ships.Add(new ShipRecord { Uid = g.NextUid++, DefId = "inhumanus_mercator_corvette", Hp = 7 });
            g.Fleets.Add(picket);
            Assert.IsTrue(PassageWar.HoldBlocker(g, p).Contains("supply"));
            var a = Camp.Reload(g); var b = Camp.Reload(g);
            War.ToDay(a, 10);
            b = Camp.Reload(b); War.ToDay(b, 9); b = Camp.Reload(b); War.ToDay(b, 10); b = Camp.Reload(b);
            Assert.AreEqual(War.Sc(a).HoldNote, War.Sc(b).HoldNote);
            Assert.AreEqual(0, War.Sc(b).HoldCount);
            Assert.AreEqual(SaveGame.Digest(a), SaveGame.Digest(b));
        }

        [Test]
        public void AfterTheOperationSettlement_NothingCanBeAppliedAgain()
        {
            var (g, op) = Ops.InvasionOfDividend();
            Ops.Fight(g, op, "inhumanus");
            Assert.IsTrue(Operations.DefenderStops(g, op).Ok);
            Assert.IsTrue(Operations.Land(g, op).Ok);
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            // Between series rounds: a reload keeps the series, the next lineup and the refusal of a re-applied battle.
            var mid = Camp.Reload(g);
            var opMid = Operations.Get(mid, op.Id);
            Assert.AreEqual((1, 0), (opMid.AttackerWins, opMid.DefenderWins));
            CollectionAssert.AreEqual(Operations.Lineup(g, op, "inhumanus", out _).Select(u => u.Uid), Operations.Lineup(mid, opMid, "inhumanus", out _).Select(u => u.Uid));
            Assert.IsTrue(Ops.Fight(g, op, "pygmaei"));
            Assert.AreEqual(OperationPhase.Settled, op.Phase);
            var back = Camp.Reload(g);
            var opB = Operations.Get(back, op.Id);
            Assert.AreEqual(OperationPhase.Settled, opB.Phase);
            Assert.IsFalse(Operations.OfferSurrenderTerms(back, opB, out _).Ok, "settled once");
            Assert.IsFalse(Operations.Withdraw(back, opB).Ok);
            Assert.AreEqual("pygmaei", back.GetPlanet(opB.PlanetId).OwnerId);
            int history = back.Slice.History.Count;
            PassageWar.OnDay(back);
            Assert.AreEqual(history, back.Slice.History.Count);
            NothingDuplicated(back);
        }

        /// <summary>The strongest invariant: a whole campaign, both sides planned and every battle fought by the slice AI, reaches
        /// exactly the same state whether or not it is saved and reloaded every five days.</summary>
        [Test]
        public void AWholeCampaign_ReloadedEveryFiveDays_EndsIdentically()
        {
            GalaxyState Run(bool reload)
            {
                var g = War.New(FactionId.Inhumanus);
                War.Sc(g).AutoPlayer = true;
                War.Sc(g).MandateDay = 120;
                for (int i = 0; i < 400 && !War.Sc(g).Ended; i++)
                {
                    if (g.PendingEngagement != null)
                    {
                        var e = g.PendingEngagement;
                        var setup = BattleBridge.Prepare(g, e, BattleBridge.AutoSelect(g, SliceCampaign.Seats(g, e)[0]), out var err);
                        Assert.IsNotNull(setup, err);
                        var bs = BattleSession.Begin(setup, out err);
                        var ai = new SliceAI();
                        for (int k = 0; k < 900 && !bs.State.IsOver; k++) bs.Submit(ai.Choose(bs.State), BattleSession.Host);
                        Assert.IsTrue(BattleBridge.Apply(g, setup, BattleBridge.Extract(setup, bs)).Ok);
                        continue;
                    }
                    StrategyRules.AdvanceDay(g);
                    if (reload && g.Day % 5 == 0) g = Camp.Reload(g);
                }
                return g;
            }
            var plain = Run(false);
            var reloaded = Run(true);
            Assert.IsTrue(War.Sc(plain).Ended);
            Assert.AreEqual(plain.Day, reloaded.Day);
            Assert.AreEqual(SaveGame.Digest(plain), SaveGame.Digest(reloaded), "reloads change nothing: income, production, AI plans, battles and endings");
            NothingDuplicated(reloaded);
        }
    }
}
