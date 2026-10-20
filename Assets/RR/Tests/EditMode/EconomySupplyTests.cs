using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Campaign alpha Prompt 15: supply derived from routes and access, an economy with upkeep, arrears, priorities
    /// and finite stores, and the scenario consuming real supply state.</summary>
    public sealed class EconomySupplyTests
    {
        static void HoldAccess(GalaxyState g, string polity, string basis = PassageWar.Battle)
        {
            var p = War.Passage(g); p.AccessHolderId = polity; p.Basis = basis; p.SinceDay = g.Day;
        }

        static Fleet Park(GalaxyState g, Fleet f, string systemId) { f.SystemId = systemId; f.Order = null; f.AIControlled = true; f.AITarget = null; return f; }

        static void ToSettlement(GalaxyState g) => War.ToDay(g, PassageWar.NextSettlementDay(g));

        [Test]
        public void AWorkingRoute_SuppliesThePassage_OnlyForItsAccessHolder()
        {
            var g = War.New();
            var net = Supply.Of(g, "pygmaei");
            Assert.IsTrue(net.Has("forgefall") && net.Has("cinderhold") && net.Has("gloam"));
            Assert.IsFalse(net.Has("umbra"), "a contested passage carries nobody's supply");
            Assert.IsTrue(net.Node("umbra").Reason.Contains("contested"), net.Node("umbra").Reason);
            Assert.IsFalse(net.Has("dividend"), "rival territory");
            Assert.IsTrue(net.Has(Economy.FreePort), "the neutral free port by the northern route");
            HoldAccess(g, "pygmaei");
            net = Supply.Of(g, "pygmaei");
            Assert.IsTrue(net.Has("umbra"));
            CollectionAssert.AreEqual(new[] { "forgefall", "cinderhold", "gloam", "umbra" }, net.Path("umbra"));
            Assert.IsNull(PassageWar.HoldBlocker(g, War.Passage(g)), "a supplied hold counts");
            Assert.IsFalse(Supply.Of(g, "inhumanus").Has("umbra"), "access is the holder's alone");
        }

        [Test]
        public void ADisruption_IsAnnouncedBeforeItHappens_ThenTheRouteReroutes_AndRecovers()
        {
            var g = War.New();
            HoldAccess(g, "pygmaei");
            War.Sc(g).HoldRequired = 99;   // fixture: keep the scenario running while the route is tested
            StrategyRules.AdvanceDay(g);   // the first announcement baseline
            var rival = Camp.Theirs(g);
            rival.AIControlled = true; rival.AITarget = null;
            Assert.IsTrue(StrategyRules.OrderMove(g, rival.Id, "gloam", asAI: true).Ok);
            var net = Supply.Of(g, "pygmaei");
            var threat = net.Node("gloam").Threat ?? net.Node("umbra").Threat;
            Assert.IsNotNull(threat, "the threatened segment is named");
            Assert.IsTrue(threat.Contains("expected at") && threat.Contains("on day"), threat);
            StrategyRules.AdvanceDay(g);
            Assert.IsTrue(g.Log.Any(l => l.Text.StartsWith("Supply threatened")), "announced once, before the cut");
            for (int i = 0; i < 60 && !(rival.SystemId == "gloam" && !rival.Moving); i++) StrategyRules.AdvanceDay(g);
            Assert.AreEqual("gloam", rival.SystemId);
            StrategyRules.AdvanceDay(g);
            net = Supply.Of(g, "pygmaei");
            Assert.IsFalse(net.Has("gloam"));
            Assert.IsTrue(net.Node("gloam").Reason.Contains("holds position"), net.Node("gloam").Reason);
            Assert.IsTrue(g.Log.Any(l => l.Text.StartsWith("Supply to Gloam is cut")), string.Join(" | ", g.Log.Select(l => l.Text).Reverse().Take(6)));
            // Rerouting: the northern route still reaches Umbra, by a longer path.
            Assert.IsTrue(net.Has("umbra"), "rerouted");
            Assert.IsFalse(net.Path("umbra").Contains("gloam"));
            Assert.IsTrue(net.Path("umbra").Contains(Economy.FreePort), string.Join(">", net.Path("umbra")));
            // Block the northern route too: the hold stops counting and says why.
            var second = new Fleet { Id = "fixture_picket", Name = "Fixture Picket", OwnerId = "inhumanus", SystemId = Economy.FreePort, HomeSystemId = "dividend", AIControlled = true };
            second.Ships.Add(new ShipRecord { Uid = g.NextUid++, DefId = "inhumanus_mercator_corvette", Hp = 7 });
            g.Fleets.Add(second);
            var why = PassageWar.HoldBlocker(g, War.Passage(g));
            Assert.IsNotNull(why);
            Assert.IsTrue(why.Contains("supply does not reach Umbra"), why);
            // Recovery: the picket leaves; supply returns by the northern route.
            g.Fleets.Remove(second);
            Assert.IsTrue(Supply.Of(g, "pygmaei").Has("umbra"));
            StrategyRules.AdvanceDay(g);
            Assert.IsTrue(g.Log.Any(l => l.Text.StartsWith("Supply reaches Gloam again")) || Supply.Of(g, "pygmaei").Has("umbra"));
        }

        [Test]
        public void TreatyAccess_OpensThePassage_AndATruceOpensTerritory()
        {
            var g = War.New();
            HoldAccess(g, "pygmaei", PassageWar.Treaty);
            g.Slice.RelationOf("pygmaei", "inhumanus").Status = "truce";
            var net = Supply.Of(g, "pygmaei");
            Assert.IsTrue(net.Has("umbra"));
            Assert.IsTrue(net.Has("dividend"), "under a truce, rival territory does not stop supply");
            Park(g, Camp.Theirs(g), "gloam");
            Assert.IsTrue(Supply.Of(g, "pygmaei").Has("gloam"), "a fleet at peace does not block");
        }

        [Test]
        public void ABlockadedOwnWorld_CutsSupply_UntilTheOrbitIsRetaken()
        {
            var g = War.New();
            var cinder = g.GetSystem("cinderhold");
            cinder.OrbitalControlId = "inhumanus"; cinder.ConquestBy = "inhumanus"; cinder.Conquest = ConquestStage.OrbitSecured;
            var net = Supply.Of(g, "pygmaei");
            Assert.IsFalse(net.Has("cinderhold"));
            Assert.IsTrue(net.Node("cinderhold").Reason.Contains("holds the orbit"), net.Node("cinderhold").Reason);
            Assert.IsFalse(net.Has("gloam"), "Gloam is reached only through Cinderhold (Umbra is contested)");
            Assert.AreEqual("no supplied route reaches it", net.Node("gloam").Reason);
            Assert.IsTrue(StrategyRules.OrderMove(g, Camp.Mine(g).Id, "cinderhold").Ok);
            for (int i = 0; i < 20 && cinder.Blockaded; i++) StrategyRules.AdvanceDay(g);
            Assert.IsFalse(cinder.Blockaded, "a fleet restores the orbit");
            Assert.IsTrue(Supply.Of(g, "pygmaei").Has("cinderhold"));
        }

        [Test]
        public void TheForwardDock_RepairsFromFiniteStores_ThatOnlySupplyRefills()
        {
            var g = War.New();
            g.Player.Stock = new Resources(500, 500, 500, 30);
            var cinderPlanet = g.GetSystem("cinderhold").Planets.Where(p => p.OwnerId == "pygmaei").OrderBy(p => p.Id).First();
            var opt = StrategyRules.ProjectOptions(g, cinderPlanet.Id).FirstOrDefault(o => o.Kind == ProjectKind.Priority && o.PriorityId == "forward_dock");
            Assert.IsNotNull(opt, "the dock is offered at Cinderhold");
            Assert.IsTrue(opt.Detail.Contains("Opportunity cost"), opt.Detail);
            Assert.IsTrue(StrategyRules.StartProject(g, cinderPlanet.Id, ProjectKind.Priority, "forward_dock").Ok);
            Assert.IsFalse(StrategyRules.StartProject(g, cinderPlanet.Id, ProjectKind.Priority, "forward_dock").Ok, "a double click starts nothing twice");
            for (int i = 0; i < opt.Days + 1; i++) StrategyRules.AdvanceDay(g);
            Assert.IsTrue(Economy.Built(g, "pygmaei", "forward_dock"));
            Assert.AreEqual(0, Economy.Stores(g, "cinderhold"), "a new dock starts empty");
            ToSettlement(g);
            Assert.AreEqual(Economy.DockRestock, Economy.Stores(g, "cinderhold"), "restocked at the settlement while supplied");
            // A damaged flagship at Cinderhold repairs from the stores until they run out.
            var fleet = Camp.Mine(g);
            Assert.IsTrue(StrategyRules.OrderMove(g, fleet.Id, "cinderhold").Ok);
            for (int i = 0; i < 20 && (fleet.Moving || fleet.SystemId != "cinderhold"); i++) StrategyRules.AdvanceDay(g);
            var flag = fleet.Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            flag.Hp = 10;
            int stores = Economy.Stores(g, "cinderhold");
            int materials = g.Player.Stock.Materials;
            var cost = SliceCampaign.RepairCost(g, flag.Uid);
            Assert.IsTrue(SliceCampaign.Repair(g, flag.Uid).Ok);
            Assert.AreEqual(stores - cost.Materials, Economy.Stores(g, "cinderhold"), "materials came from the dock's stores");
            Assert.AreEqual(materials, g.Player.Stock.Materials, "not from the national stock");
            flag.Hp = 1;
            var r = SliceCampaign.Repair(g, flag.Uid);
            Assert.IsFalse(r.Ok, "exhausted stores");
            Assert.IsTrue(r.Reason.Contains("stores hold"), r.Reason);
            // Cut off: the dock is not restocked; restored supply refills it.
            var cinder = g.GetSystem("cinderhold");
            fleet.SystemId = "forgefall";
            cinder.OrbitalControlId = "inhumanus";
            int before = Economy.Stores(g, "cinderhold");
            ToSettlement(g);
            Assert.AreEqual(before, Economy.Stores(g, "cinderhold"), "no restock while cut off");
            Assert.IsTrue(War.Sc(g).Reports.Last().Lines.Any(l => l.Contains("not restocked")), string.Join(" | ", War.Sc(g).Reports.Last().Lines));
            cinder.OrbitalControlId = "pygmaei";
            ToSettlement(g);
            Assert.Greater(Economy.Stores(g, "cinderhold"), before, "recovery: restocked once supply returns");
        }

        [Test]
        public void Upkeep_And_Insolvency_NeverGoNegative_AndPauseProjectsUntilPaid()
        {
            var g = War.New();
            var me = g.Player.Id;
            int upkeep = Economy.Upkeep(g, me);
            Assert.Greater(upkeep, 0);
            var preview = Economy.Preview(g, me);
            Assert.IsTrue(preview.Any(l => l.Contains("upkeep") && l.Contains(upkeep.ToString())), string.Join(" | ", preview));
            var yard = War.Yard(g);
            var esc = StrategyRules.ProjectOptions(g, yard.Id).Where(o => o.ShipDefId != null && !ShipCatalog.IsLeader(o.ShipDefId) && o.Available).OrderByDescending(o => o.Days).First();
            Assert.Greater(esc.Days, 10, "still in production at the settlement");
            Assert.IsTrue(StrategyRules.StartProject(g, yard.Id, ProjectKind.CommissionShip, esc.ShipDefId).Ok);
            g.Player.Stock = new Resources(g.Player.Stock.Energy, g.Player.Stock.Materials, 0, g.Player.Stock.Influence);
            // Skip income: no worlds pay credits for this fixture, so the settlement cannot be paid.
            foreach (var p in g.Systems.SelectMany(s => s.Planets).Where(p => p.OwnerId == me)) p.DailyYield = new Resources(p.DailyYield.Energy, p.DailyYield.Materials, 0, 0);
            Assert.IsTrue(Economy.Preview(g, me).Any(l => l.StartsWith("Insolvent")), "the preview warns before it happens");
            int done = yard.Project.DaysDone;
            ToSettlement(g);
            Assert.AreEqual(0, g.Player.Stock.Credits, "never below zero");
            Assert.AreEqual(upkeep, Economy.Arrears(g, me));
            int doneAtSettlement = yard.Project.DaysDone;
            StrategyRules.AdvanceDay(g);
            Assert.AreEqual(doneAtSettlement, yard.Project.DaysDone, "projects pause while in arrears");
            var r = StrategyRules.StartProject(g, g.GetSystem("ironmouth").Planets.First(p => p.OwnerId == me).Id, ProjectKind.Priority, "ore_drive");
            Assert.IsFalse(r.Ok);
            Assert.IsTrue(r.Reason.Contains("arrears"), r.Reason);
            // The settlement is keyed: running it again (or after a reload) charges nothing more.
            var back = Camp.Reload(g);
            PassageWar.OnDay(back);
            Assert.AreEqual(upkeep, Economy.Arrears(back, me));
            // Income pays the arrears first, then work resumes.
            back.Player.Stock = back.Player.Stock + new Resources(0, 0, upkeep + 5, 0);
            StrategyRules.AdvanceDay(back);
            Assert.AreEqual(0, Economy.Arrears(back, me));
            Assert.AreEqual(5, back.Player.Stock.Credits);
            int d0 = War.Yard(back).Project.DaysDone;
            StrategyRules.AdvanceDay(back);
            Assert.AreEqual(d0 + 1, War.Yard(back).Project.DaysDone, "work resumes");
        }

        [Test]
        public void TheRival_PaysTheSameUpkeep_AndCannotCommissionInArrears()
        {
            var g = War.New();
            var rival = g.GetPolity("inhumanus");
            int up = Economy.Upkeep(g, "inhumanus");
            rival.Stock = new Resources(1000, 1000, up, 0);
            foreach (var p in g.Systems.SelectMany(s => s.Planets).Where(p => p.OwnerId == "inhumanus")) p.DailyYield = default;
            ToSettlement(g);
            Assert.AreEqual(0, rival.Stock.Credits, "the rival paid its upkeep");
            Assert.IsFalse(Economy.InArrears(g, "inhumanus"));
            ToSettlement(g);
            Assert.IsTrue(Economy.InArrears(g, "inhumanus"), "the same arrears rule");
            Assert.IsTrue(War.Sc(g).Ledger.Any(l => l.Polity == "inhumanus" && l.Kind == "upkeep"));
        }

        [Test]
        public void TwoAffordablePlans_WithDifferentTradeoffs_AndNotBothAtOnce()
        {
            var g = War.New();
            var me = g.Player.Id;
            var flag = Camp.Mine(g).Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            flag.Hp = 8;   // a veteran flagship back from a hard fight
            var budget = Economy.Budget(g, me);
            var repair = budget.First(b => b.Label.StartsWith("Repair"));
            var escort = budget.First(b => b.Label.StartsWith("Commission"));
            var dock = budget.First(b => b.Label == "Forward repair dock");
            var convoy = budget.First(b => b.Label == "Convoy office");
            Assert.IsTrue(repair.Affordable && escort.Affordable && dock.Affordable && convoy.Affordable, string.Join(" | ", budget.Select(b => b.Label + ": " + (b.Blocked ?? "ok"))));
            var planA = repair.Cost + dock.Cost;     // fix the veteran now and build a dock at the front
            var planB = escort.Cost + convoy.Cost;   // another hull, paid for over time by trade
            // A treasury that covers either plan (the larger of each resource), as a player might hold after a costly battle.
            g.Player.Stock = new Resources(Math.Max(planA.Energy, planB.Energy), Math.Max(planA.Materials, planB.Materials), Math.Max(planA.Credits, planB.Credits), 30);
            Assert.IsTrue(g.Player.Stock.Covers(planA), $"plan A {planA} with {g.Player.Stock}");
            Assert.IsTrue(g.Player.Stock.Covers(planB), $"plan B {planB} with {g.Player.Stock}");
            Assert.IsFalse(g.Player.Stock.Covers(planA + planB), "the budget forces a choice");
            // Carry out each plan from the same save; both are legal and leave different campaigns.
            var a = Camp.Reload(g); var b = Camp.Reload(g);
            var fa = Camp.Mine(a).Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            Assert.IsTrue(SliceCampaign.Repair(a, fa.Uid).Ok);
            var cinderA = a.GetSystem("cinderhold").Planets.Where(p => p.OwnerId == me).OrderBy(p => p.Id).First();
            Assert.IsTrue(StrategyRules.StartProject(a, cinderA.Id, ProjectKind.Priority, "forward_dock").Ok);
            var yardB = War.Yard(b);
            var escId = StrategyRules.ProjectOptions(b, yardB.Id).Where(o => o.ShipDefId != null && !ShipCatalog.IsLeader(o.ShipDefId)).OrderBy(o => o.Cost.Credits).First().ShipDefId;
            Assert.IsTrue(StrategyRules.StartProject(b, yardB.Id, ProjectKind.CommissionShip, escId).Ok);
            Assert.IsFalse(StrategyRules.StartProject(b, yardB.Id, ProjectKind.Priority, "convoy_office").Ok, "the convoy office needs the same yard: an opportunity cost");
            Assert.AreNotEqual(SaveGame.Digest(a), SaveGame.Digest(b));
            Assert.AreEqual(SliceCampaign.MaxHull(a, fa), fa.Hp);
            Assert.Less(Camp.Mine(b).Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Hp, SliceCampaign.MaxHull(b, fa), "plan B fights with the damaged veteran");
        }

        [Test]
        public void ConvoyIncome_DependsOnARealRoute()
        {
            var g = War.New();
            War.Sc(g).Built.Add("pygmaei:convoy_office");
            Assert.AreEqual(Economy.PygmaeiConvoyYield, Economy.PriorityYield(g, "pygmaei").Credits);
            var picket = new Fleet { Id = "fixture_picket", Name = "Fixture Picket", OwnerId = "inhumanus", SystemId = Economy.FreePort, HomeSystemId = "dividend", AIControlled = true };
            picket.Ships.Add(new ShipRecord { Uid = g.NextUid++, DefId = "inhumanus_mercator_corvette", Hp = 7 });
            g.Fleets.Add(picket);
            Assert.AreEqual(0, Economy.PriorityYield(g, "pygmaei").Credits, "no route to the free port, no trade");
            Assert.IsNotNull(Economy.ConvoyProblem(g, "pygmaei"));
        }

        [Test]
        public void Settlements_AreIdempotent_AcrossReloads_AndRepeatedCalls()
        {
            var g = War.New();
            War.ToDay(g, 9);
            var a = Camp.Reload(g); var b = Camp.Reload(g);
            StrategyRules.AdvanceDay(a);
            StrategyRules.AdvanceDay(b); PassageWar.OnDay(b); b = Camp.Reload(b); PassageWar.OnDay(b);
            Assert.AreEqual(SaveGame.Digest(a), SaveGame.Digest(b), "a repeated settlement changes nothing");
            Assert.AreEqual(1, War.Sc(b).Reports.Count);
        }
    }
}
