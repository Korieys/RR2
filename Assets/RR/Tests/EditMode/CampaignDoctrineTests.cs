using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Campaign alpha Prompt 18: two strategic paths per faction, with real costs, prerequisites and consequences.</summary>
    public sealed class CampaignDoctrineTests
    {
        static GalaxyState Rich(FactionId f = FactionId.Pygmaei)
        {
            var g = War.New(f);
            g.Player.Stock = new Resources(800, 800, 800, 100);
            return g;
        }

        static void Adopt(GalaxyState g, string id)
        {
            Assert.IsTrue(Doctrines.Start(g, g.Player.Id, id).Ok, Doctrines.Problem(g, g.Player.Id, Doctrines.Get(id)));
            for (int i = 0; i < 80 && !Doctrines.Has(g, g.Player.Id, id); i++) StrategyRules.AdvanceDay(g);
            Assert.IsTrue(Doctrines.Has(g, g.Player.Id, id));
        }

        [Test]
        public void EachFaction_HasTwoPaths_OfThreeProjects_WithStatedEffectsAndVoice()
        {
            foreach (var pol in new[] { "pygmaei", "inhumanus" })
            {
                var mine = Doctrines.All.Where(p => p.Polity == pol).ToList();
                Assert.AreEqual(6, mine.Count);
                Assert.AreEqual(2, mine.Select(p => p.Path).Distinct().Count());
                Assert.IsTrue(mine.Any(p => p.Kind == "route" || p.Kind == "economy"), "at least one changes routes or economic opportunity");
                Assert.IsTrue(mine.Any(p => p.Kind == "tactical" && p.Token != null), "at least one unlocks a tactical sidegrade");
                Assert.IsTrue(mine.All(p => p.Voice.EndsWith(".") && p.Effect.Length > 20), "complete sentences that do not hide the effect");
            }
        }

        [Test]
        public void TheSameFaction_TwoPlans_ChangeDifferentDecisions()
        {
            // Plan A (rapid expedition): the route to Umbra gets shorter. Plan B (industry): materials and build times change.
            var a = Rich(); var b = Rich();
            var mineA = Camp.Mine(a);
            int before = StrategyRules.PlanRoute(a, mineA, "umbra").Days;
            Adopt(a, "pyg_forward_logistics");
            Assert.Less(StrategyRules.PlanRoute(a, mineA, "umbra").Days, before, "forward logistics shortens the supplied route");
            int matBefore = Economy.PriorityYield(b, "pygmaei").Materials;
            Adopt(b, "pyg_foundry_quotas");
            Assert.Greater(Economy.PriorityYield(b, "pygmaei").Materials, matBefore, "foundry quotas add materials");
            Adopt(b, "pyg_yard_expansion");
            var yard = War.Yard(b);
            var esc = StrategyRules.ProjectOptions(b, yard.Id).First(o => o.ShipDefId == "pygmaei_skirmish_frigate");
            var escA = StrategyRules.ProjectOptions(a, War.Yard(a).Id).First(o => o.ShipDefId == "pygmaei_skirmish_frigate");
            Assert.Less(esc.Days, escA.Days, "yard expansion builds faster");
            Assert.AreEqual(StrategyRules.PlanRoute(b, Camp.Mine(b), "umbra").Days, before, "plan B does not shorten the route");
            Assert.AreEqual("expedition", Doctrines.PathOf(a, "pygmaei"));
            Assert.AreEqual("industry", Doctrines.PathOf(b, "pygmaei"));
        }

        [Test]
        public void PathCommitment_DelaysAndForecloses_PrerequisitesHold_AndThereIsNoFreeRespec()
        {
            var g = Rich();
            var me = g.Player.Id;
            var p2 = Doctrines.Get("pyg_field_refits");
            Assert.IsTrue(Doctrines.Problem(g, me, p2).StartsWith("Requires"), "prerequisites");
            Assert.IsTrue(Doctrines.Consequence(g, me, Doctrines.Get("pyg_forward_logistics")).Contains("foreclosed"));
            // Start, cancel: half back and the commitment released, so a change of mind costs half the first project.
            var stock = g.Player.Stock;
            Assert.IsTrue(Doctrines.Start(g, me, "pyg_forward_logistics").Ok);
            Assert.IsFalse(Doctrines.Start(g, me, "pyg_foundry_quotas").Ok, "one project at a time");
            Assert.IsTrue(Doctrines.Cancel(g, me).Ok);
            Assert.IsNull(Doctrines.PathOf(g, me));
            var lost = stock - g.Player.Stock;
            Assert.AreEqual(Doctrines.Get("pyg_forward_logistics").Cost - Doctrines.Get("pyg_forward_logistics").Cost.Half(), lost, "no free respecification");
            Adopt(g, "pyg_foundry_quotas");
            Assert.AreEqual("industry", Doctrines.PathOf(g, me));
            var off = Doctrines.Get("pyg_forward_logistics");
            var (cost, days) = Doctrines.Price(g, me, off);
            Assert.AreEqual(off.Days * 2, days, "the other path is delayed");
            Assert.AreEqual(off.Cost.Credits * 2, cost.Credits);
            Assert.IsTrue(Doctrines.Problem(g, me, Doctrines.Get("pyg_coolant_discipline")).StartsWith("Foreclosed") ||
                          Doctrines.Problem(g, me, Doctrines.Get("pyg_coolant_discipline")).StartsWith("Requires"));
            // Adopted projects are adopted once, and completion survives a reload without repeating.
            Assert.IsFalse(Doctrines.Start(g, me, "pyg_foundry_quotas").Ok, "no duplicate research");
            var back = Camp.Reload(g);
            Assert.IsTrue(Doctrines.Has(back, me, "pyg_foundry_quotas"));
            Assert.AreEqual(1, War.Sc(back).Ledger.Count(l => l.Text == "Adopted Foundry quotas"));
        }

        [Test]
        public void OverspendingAndArrears_AreRefused()
        {
            var g = War.New();
            g.Player.Stock = new Resources(0, 0, 10, 0);
            var why = Doctrines.Problem(g, g.Player.Id, Doctrines.Get("pyg_foundry_quotas"));
            Assert.IsTrue(why.StartsWith("Needs"), why);
            War.Sc(g).Arrears[g.Player.Id] = 5;
            Assert.IsTrue(Doctrines.Problem(g, g.Player.Id, Doctrines.Get("pyg_foundry_quotas")).Contains("arrears"));
        }

        [Test]
        public void TacticalSidegrades_AreTheSameRulesForPreviewAndResult_AndIncompatibleOnesAreRefused()
        {
            // Coolant discipline on a Skirmish Frigate: +1 damage overclock, overheated at 6.
            foreach (var (token, dmg, cap, heat) in new[] { (SliceRules.CoolantDoctrine, 1, 6, 2), (SliceRules.FurnaceDoctrine, 3, 4, 3), ((string)null, 2, 4, 2) })
            {
                var u = new UnitState();
                if (token != null) u.Equipment.Add(token);
                Assert.AreEqual(dmg, SliceRules.OverclockDamageOf(u), token);
                Assert.AreEqual(cap, SliceRules.OverheatAtOf(u), token);
                Assert.AreEqual(heat, SliceRules.OverclockHeatOf(u, attack: true), token);
            }
            var bad = new ArmyRoster { Faction = FactionId.Pygmaei, Name = "fixture" };
            bad.Units.Add(new RosterEntry("pygmaei_revenant_command_ship") { Equipment = { SliceRules.CoolantDoctrine, SliceRules.FurnaceDoctrine } });
            Assert.IsFalse(RosterArmy.Validate(bad, BattleBridge.Caps, BattleDomain.Orbital).Valid, "exclusive doctrines");
            var wrong = new ArmyRoster { Faction = FactionId.Inhumanus, Name = "fixture" };
            wrong.Units.Add(new RosterEntry("inhumanus_executive_flagship") { Equipment = { SliceRules.CoolantDoctrine } });
            Assert.IsFalse(RosterArmy.Validate(wrong, BattleBridge.Caps, BattleDomain.Orbital).Valid, "another faction's doctrine");
            // Contracts: leveraged costs less and arrives later; guaranteed costs full price and arrives earlier.
            var lev = new RosterEntry("inhumanus_proxy_frigate") { Equipment = { SliceRules.ContractToken + "west", SliceRules.LeveragedDoctrine } };
            var gua = new RosterEntry("inhumanus_proxy_frigate") { Equipment = { SliceRules.ContractToken + "west", SliceRules.GuaranteedDoctrine } };
            int printed = RosterContent.Cost(RosterContent.Unit("inhumanus_proxy_frigate"));
            Assert.AreEqual(Math.Max(1, printed - 2), SliceRules.EntryCost(lev, printed));
            Assert.AreEqual(printed, SliceRules.EntryCost(gua, printed));
            var ul = new UnitState(); ul.Equipment.AddRange(lev.Equipment);
            var ug = new UnitState(); ug.Equipment.AddRange(gua.Equipment);
            Assert.AreEqual(4, SliceRules.ArrivalRoundOf(ul));
            Assert.AreEqual(2, SliceRules.ArrivalRoundOf(ug));
        }

        [Test]
        public void AnAdoptedSidegrade_RidesIntoTheCampaignBattle_AndIsPreviewedExactly()
        {
            var g = Rich();
            Adopt(g, "pyg_foundry_quotas"); Adopt(g, "pyg_yard_expansion"); Adopt(g, "pyg_furnace_doctrine");
            Assert.IsTrue(Doctrines.Disclosure(g, "pygmaei").Any(l => l.StartsWith("Furnace doctrine")));
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            Assert.IsTrue(setup.Config.Players[0].Army.Units.All(e => e.Equipment.Contains(SliceRules.FurnaceDoctrine)), "every ship of the faction carries it");
            var rivalTokens = Doctrines.Tokens(g, "inhumanus").ToList();   // the rival planner researches its own path meanwhile
            Assert.IsTrue(setup.Config.Players[1].Army.Units.All(e => e.Equipment.Where(x => x.StartsWith(SliceRules.DoctrineToken)).OrderBy(x => x).SequenceEqual(rivalTokens.OrderBy(x => x))),
                "each side carries exactly its own adopted, disclosed doctrines");
            Assert.IsFalse(rivalTokens.Contains(SliceRules.FurnaceDoctrine), "never the other faction's");
            // An overclocked shot previews +3 and resolves to exactly the preview.
            var ai = new SliceAI();
            for (int i = 0; i < 200 && !b.State.IsOver; i++)
            {
                var shot = LegalActions.EnumerateRoster(b.State).FirstOrDefault(c => c.Type == CommandType.Attack && c.Option == SliceRules.OverclockOption && b.State.ActiveSeat == 0);
                if (shot != null)
                {
                    var u = b.State.Unit(shot.UnitId);
                    var prev = RosterCombat.ResolveBasic(b.State, u, shot.Target, overclock: true);
                    Assert.IsTrue(prev.Roster.Breakdown.Any(x => x.StartsWith("+3 overclock")), string.Join(", ", prev.Roster.Breakdown));
                    var p = b.Preview(shot);
                    int heat = SliceRules.Heat(u);
                    Assert.IsTrue(b.Submit(shot, BattleSession.Host).Accepted);
                    Assert.AreEqual(heat + 3, SliceRules.Heat(b.State.Unit(shot.UnitId)), "a furnace shot costs 3 heat");
                    return;
                }
                b.Submit(ai.Choose(b.State), BattleSession.Host);
            }
            Assert.Inconclusive("no overclocked shot came up in 200 orders");
        }

        [Test]
        public void EffectsReadByTheEconomyAndOperations()
        {
            var g = Rich(FactionId.Inhumanus);
            Adopt(g, "inh_underwritten_escorts");
            var flag = Camp.Mine(g).Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            flag.Hp = 10;
            var h = War.New(FactionId.Inhumanus);
            var flagH = Camp.Mine(h).Ships.First(s => ShipCatalog.IsLeader(s.DefId)); flagH.Hp = 10;
            Assert.AreEqual(SliceCampaign.RepairCost(h, flagH.Uid).Credits / 2, SliceCampaign.RepairCost(g, flag.Uid).Credits, "underwritten repairs halve the credits");
            var c = Rich(FactionId.Inhumanus);
            Adopt(c, "inh_toll_brokerage");
            War.Passage(c).AccessHolderId = "inhumanus"; War.Passage(c).Basis = PassageWar.Battle;
            int credits = c.Player.Stock.Credits;
            var income = StrategyRules.DailyIncome(c, "inhumanus").Credits + Economy.PriorityYield(c, "inhumanus").Credits;
            StrategyRules.AdvanceDay(c);
            Assert.AreEqual(credits + income + War.Passage(c).TollPerDay + 2, c.Player.Stock.Credits, "toll brokerage adds 2 a day to the toll");
        }

        [Test]
        public void TheRival_UsesDoctrinesThroughTheSameCommands()
        {
            var g = War.New(FactionId.Pygmaei);
            g.GetPolity("inhumanus").Stock = new Resources(500, 500, 500, 100);
            for (int i = 0; i < 40; i++) StrategyRules.AdvanceDay(g);
            var path = Doctrines.PathOf(g, "inhumanus");
            Assert.AreEqual("commerce", path, "the commercial doctrine of the Inhumanus planner\n" + string.Join("\n", War.Sc(g).AiLog));
            Assert.IsTrue(War.Sc(g).Ledger.Any(l => l.Polity == "inhumanus" && l.Kind == "doctrine" && l.Cost.Influence > 0), "paid, like the player's");
        }
    }
}
