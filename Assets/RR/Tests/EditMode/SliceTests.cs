using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;
using Commandfall.Tests;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Strategic layer of the vertical slice: orders, management, contact, battle hand-off, persistence.</summary>
    public sealed class SliceStrategyTests
    {
        static GalaxyState New() => SliceScenario.Create();

        static void Run(GalaxyState g, int days) { for (int i = 0; i < days && g.PendingEngagement == null; i++) StrategyRules.AdvanceDay(g); }

        [Test]
        public void Scenario_IsConnected_AndWithinSliceBudget()
        {
            var g = New();
            Assert.That(g.Systems.Count, Is.InRange(24, 48));
            var seen = new HashSet<string> { g.Systems[0].Id };
            var q = new Queue<string>(seen);
            while (q.Count > 0)
            {
                var at = q.Dequeue();
                foreach (var l in g.LanesOf(at)) if (seen.Add(l.Other(at))) q.Enqueue(l.Other(at));
            }
            Assert.AreEqual(g.Systems.Count, seen.Count, "every system is reachable through jump lanes");
            Assert.IsTrue(g.Sectors.Any(s => s.Eclipse && s.Name == "The Eclipse Sector" && s.OwnerId == null), "The Eclipse Sector exists and is unclaimed");
            Assert.IsFalse(g.Sectors.Any(s => s.Name.Contains("Shattered Veil")), "the old name is gone");
            foreach (var f in g.Fleets) foreach (var s in f.Ships) Assert.IsNotNull(ShipCatalog.Def(s.DefId), s.DefId);
            Assert.IsTrue(g.Systems.Count(s => s.Detail != SystemDetail.Distant) >= 4, "a hero system and three distinct local systems");
        }

        [Test]
        public void Orders_IllegalOnesAreRejectedWithReasons()
        {
            var g = New();
            Assert.IsFalse(StrategyRules.OrderMove(g, SliceScenario.PlayerFleet, SliceScenario.HeroSystem).Ok, "already there");
            Assert.IsFalse(StrategyRules.OrderMove(g, SliceScenario.RaidFleet, "lithar").Ok, "an enemy fleet does not take your orders");
            Assert.IsFalse(StrategyRules.OrderMove(g, "no_such_fleet", "lithar").Ok);
            Assert.IsFalse(StrategyRules.OrderMove(g, SliceScenario.PlayerFleet, "no_such_system").Ok);
            var r = StrategyRules.StartProject(g, "spore_drift_1", ProjectKind.CommissionShip, "silex_prism_escort");
            Assert.IsFalse(r.Ok, "cannot build on an enemy world"); Assert.IsNotEmpty(r.Reason);
            Assert.IsFalse(StrategyRules.StartProject(g, SliceScenario.HeroPlanet, ProjectKind.CommissionShip, "silex_throne_ark").Ok, "not commissionable");
            Assert.IsFalse(StrategyRules.StartProject(g, SliceScenario.HeroPlanet, ProjectKind.CommissionShip, "silex_pillar_destroyer").Ok, "cannot afford it");
        }

        [Test]
        public void Commissioning_DeductsCost_TakesItsTime_AndJoinsTheFleet()
        {
            var g = New();
            var before = g.Player.Stock;
            int ships = g.GetFleet(SliceScenario.PlayerFleet).Ships.Count;
            Assert.IsTrue(StrategyRules.StartProject(g, SliceScenario.HeroPlanet, ProjectKind.CommissionShip, "silex_prism_escort").Ok);
            var cost = ShipCatalog.Commissionable.First(c => c.defId == "silex_prism_escort");
            Assert.AreEqual(before - cost.cost, g.Player.Stock);
            Assert.IsFalse(StrategyRules.StartProject(g, SliceScenario.HeroPlanet, ProjectKind.CrystalFoundry).Ok, "one project at a time");
            Run(g, cost.days - 1);
            Assert.AreEqual(ships, g.GetFleet(SliceScenario.PlayerFleet).Ships.Count, "not finished yet");
            Run(g, 1);
            Assert.AreEqual(ships + 1, g.GetFleet(SliceScenario.PlayerFleet).Ships.Count);
            Assert.IsNull(g.GetPlanet(SliceScenario.HeroPlanet).Project);
        }

        [Test]
        public void Foundry_IsADifferentLegalChoice_WithADifferentOutcome()
        {
            var a = New(); var b = New();
            a.GetFleet(SliceScenario.RaidFleet).AIDepartDay = b.GetFleet(SliceScenario.RaidFleet).AIDepartDay = 999;   // no raid in this test
            Assert.IsTrue(StrategyRules.StartProject(a, SliceScenario.HeroPlanet, ProjectKind.CrystalFoundry).Ok);
            Assert.IsTrue(StrategyRules.StartProject(b, SliceScenario.HeroPlanet, ProjectKind.CommissionShip, "silex_lattice_corvette").Ok);
            Run(a, ShipCatalog.FoundryDays); Run(b, ShipCatalog.FoundryDays);
            Assert.AreEqual(1, a.GetPlanet(SliceScenario.HeroPlanet).Foundries);
            Assert.AreEqual(0, b.GetPlanet(SliceScenario.HeroPlanet).Foundries);
            Assert.AreNotEqual(SaveGame.Digest(a), SaveGame.Digest(b));
            Assert.Greater(StrategyRules.DailyIncome(a, "silex").Materials, StrategyRules.DailyIncome(b, "silex").Materials);
        }

        [Test]
        public void Raid_ArrivesAtTheHeroSystem_WhenThePlayerHolds()
        {
            var g = New();
            Run(g, 60);
            var e = g.PendingEngagement;
            Assert.IsNotNull(e, "the brood reaches Aurelith");
            Assert.AreEqual(SliceScenario.HeroSystem, e.SystemId);
            Assert.AreEqual(SliceScenario.RaidFleet, e.AttackerFleetId);
            Assert.AreEqual(SliceScenario.PlayerFleet, e.DefenderFleetId);
            var day = g.Day;
            Assert.IsFalse(StrategyRules.AdvanceDay(g).Advanced, "time stops while an engagement waits");
            Assert.AreEqual(day, g.Day);
            Assert.IsFalse(StrategyRules.OrderMove(g, "silex_shalecrest_picket", "lithar").Ok, "no orders while an engagement waits");
        }

        [Test]
        public void Intercepting_AtCinderVerge_IsADifferentEncounter()
        {
            var g = New();
            Assert.IsTrue(StrategyRules.OrderMove(g, SliceScenario.PlayerFleet, "cinder_verge").Ok);
            Run(g, 60);
            var e = g.PendingEngagement;
            Assert.IsNotNull(e);
            Assert.AreEqual("cinder_verge", e.SystemId);
        }

        [Test]
        public void Retreat_GivesUpTheOrbit_ButNotThePlanet()
        {
            var g = New();
            Run(g, 60);
            var e = g.PendingEngagement;
            var incomeBefore = StrategyRules.DailyIncome(g, "silex");
            Assert.IsTrue(StrategyRules.Retreat(g, e.Id).Ok);
            var sys = g.GetSystem(SliceScenario.HeroSystem);
            Assert.AreEqual("craigon", sys.OrbitalControlId);
            Assert.AreEqual("silex", sys.OwnerId, "an orbit is not a planet");
            Assert.AreEqual("silex", g.GetPlanet(SliceScenario.HeroPlanet).OwnerId);
            Assert.IsTrue(sys.Blockaded);
            var income = StrategyRules.DailyIncome(g, "silex");
            Assert.AreEqual(incomeBefore.Materials - g.GetPlanet(SliceScenario.HeroPlanet).DailyYield.Materials, income.Materials, "a blockaded world yields nothing");
            Assert.AreEqual(ConquestStage.OrbitSecured, sys.Conquest);
        }

        [Test]
        public void Selection_RespectsBudget_UnitCap_AndNeedsTheFlagship()
        {
            var g = New();
            var f = g.GetFleet(SliceScenario.PlayerFleet);
            var all = f.Ships.Select(s => s.Uid).ToList();
            var rep = BattleBridge.Validate(g, f, all);
            Assert.IsFalse(rep.Valid, "the whole fleet costs more than the battle budget");
            Assert.Greater(rep.Cost, rep.Budget);
            var noLeader = f.Ships.Where(s => !ShipCatalog.IsLeader(s.DefId)).Select(s => s.Uid).ToList();
            Assert.IsFalse(BattleBridge.Validate(g, f, noLeader).Valid, "a battle line needs its flagship");
            var auto = BattleBridge.AutoSelect(g, f);
            var ok = BattleBridge.Validate(g, f, auto);
            Assert.IsTrue(ok.Valid, string.Join("; ", ok.Errors));
            Assert.AreNotEqual(0, ok.LeaderUid);
        }

        internal static (BattleSetup setup, MatchState state) PlayOut(GalaxyState g, IList<int> playerUids, int seed = 7)
        {
            var e = g.PendingEngagement;
            var setup = BattleBridge.Prepare(g, e, playerUids, out var err);
            Assert.IsNotNull(setup, err);
            var s = BattleBridge.CreateState(setup, out err);
            Assert.IsNotNull(s, err);
            var ai = new[] { new RosterAI(AIDifficulty.Tactical, seed), new RosterAI(AIDifficulty.Tactical, seed + 1) };
            for (int i = 0; i < 4000 && !s.IsOver; i++)
            {
                var c = ai[s.ActiveSeat].ChooseAction(s);
                var r = RulesEngine.Apply(s, c);
                Assert.IsTrue(r.Accepted, r.Reason);
            }
            Assert.IsTrue(s.IsOver, "the battle ends");
            return (setup, s);
        }

        [Test]
        public void Battle_IsDeterministic_AndItsResultAppliesExactlyOnce()
        {
            var g1 = New(); Run(g1, 60);
            var g2 = New(); Run(g2, 60);
            var pick = BattleBridge.AutoSelect(g1, g1.GetFleet(SliceScenario.PlayerFleet));
            var (setup1, s1) = PlayOut(g1, pick);
            var (setup2, s2) = PlayOut(g2, pick);
            Assert.AreEqual(Serialization.StateHash(s1), Serialization.StateHash(s2), "identical inputs, identical battle");
            var o = BattleBridge.Extract(setup1, s1);
            int shipsBefore = g1.GetFleet(SliceScenario.PlayerFleet)?.Ships.Count ?? 0;
            Assert.IsTrue(BattleBridge.Apply(g1, setup1, o).Ok);
            var again = BattleBridge.Apply(g1, setup1, o);
            Assert.IsFalse(again.Ok, "a result is applied once");
            Assert.IsNull(g1.PendingEngagement);
            Assert.IsTrue(g1.AppliedBattles.Contains(setup1.BattleId));
            int lost = o.Ships[0].Count(x => x.Destroyed || x.Captured);
            int now = g1.GetFleet(SliceScenario.PlayerFleet)?.Ships.Count ?? 0;
            Assert.AreEqual(shipsBefore - lost, now, "losses match the battle");
            // Unselected ships were not touched.
            var sys = g1.GetSystem(o.EngagementId == null ? SliceScenario.HeroSystem : g1.GetEngagement(o.EngagementId).SystemId);
            Assert.AreEqual("silex", sys.OwnerId, "an orbital battle never transfers the planet");
        }

        [Test]
        public void CompositionMatters_DifferentSelectionsGiveDifferentBattles()
        {
            var g1 = New(); Run(g1, 60);
            var g2 = New(); Run(g2, 60);
            var f = g1.GetFleet(SliceScenario.PlayerFleet);
            var a = BattleBridge.AutoSelect(g1, f);
            var leader = f.Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Uid;
            var b = new List<int> { leader };
            foreach (var s in f.Ships.Where(s => s.Uid != leader).OrderBy(s => ShipCatalog.Tier(s.DefId))) { b.Add(s.Uid); if (!BattleBridge.Validate(g1, f, b).Valid) b.RemoveAt(b.Count - 1); }
            Assert.AreNotEqual(string.Join(",", a.OrderBy(x => x)), string.Join(",", b.OrderBy(x => x)));
            var (_, s1) = PlayOut(g1, a);
            var (setup2, s2) = PlayOut(g2, b);
            Assert.AreNotEqual(Serialization.StateHash(s1), Serialization.StateHash(s2));
            CollectionAssert.AreEquivalent(b, setup2.ShipUids[0], "the battle fields exactly the selected ships");
        }

        [Test]
        public void CarriedDamage_PersistsIntoTheNextBattle()
        {
            var g = New(); Run(g, 60);
            var f = g.GetFleet(SliceScenario.PlayerFleet);
            var flagship = f.Ships.First(s => ShipCatalog.IsLeader(s.DefId));
            flagship.Hp = 5;
            var pick = BattleBridge.AutoSelect(g, f);
            var setup = BattleBridge.Prepare(g, g.PendingEngagement, pick, out var err);
            var s = BattleBridge.CreateState(setup, out err);
            Assert.AreEqual(5, s.Unit(s.Players[0].CommanderId).Hp);
        }

        [Test]
        public void SaveLoad_RoundTripsMidCampaign_AndMidEngagement()
        {
            var g = New();
            StrategyRules.StartProject(g, SliceScenario.HeroPlanet, ProjectKind.CommissionShip, "silex_prism_escort");
            StrategyRules.OrderMove(g, "silex_shalecrest_picket", SliceScenario.HeroSystem);
            Run(g, 5);
            var json = SaveGame.ToJson(g);
            var back = SaveGame.FromJson(json, out var err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(SaveGame.Digest(g), SaveGame.Digest(back));
            Run(g, 60); Run(back, 60);
            Assert.AreEqual(SaveGame.Digest(g), SaveGame.Digest(back), "a loaded game continues identically");
            Assert.IsNotNull(back.PendingEngagement);
            var again = SaveGame.FromJson(SaveGame.ToJson(back), out err);
            Assert.AreEqual(back.PendingEngagement.Id, again.PendingEngagement.Id);
        }

        [Test]
        public void SaveLoad_RejectsForeignAndFutureFiles()
        {
            Assert.IsNull(SaveGame.FromJson("{\"schema\":\"other\"}", out var e1)); Assert.IsNotEmpty(e1);
            Assert.IsNull(SaveGame.FromJson("{\"schema\":\"rr.slice.galaxy\",\"version\":99}", out var e2)); StringAssert.Contains("newer", e2);
            Assert.IsNull(SaveGame.FromJson("{ not json", out var e3)); Assert.IsNotEmpty(e3);
        }

        [Test]
        public void ResultsPersistThroughSaveAndLoad()
        {
            var g = New(); Run(g, 60);
            var (setup, s) = PlayOut(g, BattleBridge.AutoSelect(g, g.GetFleet(SliceScenario.PlayerFleet)));
            Assert.IsTrue(BattleBridge.Apply(g, setup, BattleBridge.Extract(setup, s)).Ok);
            var back = SaveGame.FromJson(SaveGame.ToJson(g), out var err);
            Assert.AreEqual(SaveGame.Digest(g), SaveGame.Digest(back));
            Assert.IsFalse(BattleBridge.Apply(back, setup, BattleBridge.Extract(setup, s)).Ok, "still applied once after reload");
        }
    }

    /// <summary>Commandfall doctrine, checked on the slice's own ships: generals don't roll dice.</summary>
    public sealed class SliceDoctrineTests
    {
        [Test]
        public void LeaderElimination_EndsTheBattle_EvenWithForcesRemaining()
        {
            var sc = new RosterScenario(BattleDomain.Orbital);
            int gun = sc.Add(0, "silex_monolith_battleship", 0, 1);
            sc.Add(0, "silex_throne_ark", -3, 4);
            int flag = sc.Add(1, "craigon_prime_synapse_flagship", 0, -2, hp: 3);
            sc.Add(1, "craigon_synaptic_cruiser", 3, -3); sc.Add(1, "craigon_scuttler_corvette", -3, 0);
            var s = sc.Build();
            var preview = RosterCombat.ResolveBasic(s, s.Unit(sc.Id(gun)), new Hex(0, -2));
            Assert.IsTrue(preview.Legal, preview.Reason);
            var r = RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -2)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.CommanderEliminated, s.VictoryReason);
            Assert.GreaterOrEqual(s.LiveUnits(1).Count(), 2, "remaining enemy ships did not postpone the result");
        }

        [Test]
        public void DefendedLeader_ADeclaredGuardInterceptsTheLethalShot()
        {
            // Silex Citadel Dreadnought, Impenetrable Front (GUARD): intercepts the first direct attack on the leader
            // and reduces it by 3 raw damage. The Throne Ark would die to the unguarded hit.
            var sc = new RosterScenario(BattleDomain.Orbital);
            int gun = sc.Add(0, "craigon_prime_synapse_flagship", 0, 2);   // 7 raw damage, range 2-6
            int idle = sc.Add(0, "craigon_scuttler_corvette", -3, 4);
            int ark = sc.Add(1, "silex_throne_ark", 0, -1, hp: 3);
            int dread = sc.Add(1, "silex_citadel_dreadnought", 1, -1);
            var s = sc.Build();
            var unguarded = RosterCombat.ResolveBasic(s, s.Unit(sc.Id(gun)), new Hex(0, -1));
            Assert.IsTrue(unguarded.Legal, unguarded.Reason);
            Assert.IsTrue(unguarded.TargetDestroyed, "without the guard this shot is lethal");
            Assert.IsTrue(RulesEngine.Apply(s, Command.Wait(0, sc.Id(idle))).Accepted);
            var uses = RosterAbilities.Enumerate(s, s.Unit(sc.Id(dread)));
            var cover = uses.FirstOrDefault(u => u.picks.Contains(new Hex(0, -1)));
            Assert.IsNotNull(cover.picks, "Impenetrable Front can cover the Throne Ark");
            Assert.IsTrue(RulesEngine.Apply(s, Command.Active(1, sc.Id(dread), cover.picks, cover.option)).Accepted);
            int dreadHp = s.Unit(sc.Id(dread)).Hp;
            var r = RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -1)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(r.Events.Any(e => e.Type == GameEventType.Intercepted), "the declared guard intercepted");
            Assert.IsFalse(s.IsOver, "the protected flagship survives");
            Assert.AreEqual(3, s.Unit(sc.Id(ark)).Hp);
            Assert.LessOrEqual(s.Unit(sc.Id(dread)).Hp, dreadHp);
        }

        [Test]
        public void GuardRules_AreSpecific_InterceptionSnapOnlyCoversFighterAttacks()
        {
            var sc = new RosterScenario(BattleDomain.Orbital);
            int gun = sc.Add(0, "silex_monolith_battleship", 0, 2);
            sc.Add(0, "silex_throne_ark", -3, 4);
            sc.Add(1, "craigon_prime_synapse_flagship", 0, -1, hp: 3);
            int snap = sc.Add(1, "craigon_scuttler_interceptor", 1, -1);
            var s = sc.Build();
            Assert.IsTrue(RulesEngine.Apply(s, Command.Wait(0, s.Players[0].CommanderId)).Accepted);
            var uses = RosterAbilities.Enumerate(s, s.Unit(sc.Id(snap)));
            var cover = uses.FirstOrDefault(u => u.picks.Contains(new Hex(0, -1)));
            if (cover.picks != null) Assert.IsTrue(RulesEngine.Apply(s, Command.Active(1, sc.Id(snap), cover.picks, cover.option)).Accepted);
            else Assert.IsTrue(RulesEngine.Apply(s, Command.Wait(1, sc.Id(snap))).Accepted);
            var r = RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -1)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsFalse(r.Events.Any(e => e.Type == GameEventType.Intercepted), "a battleship salvo is not a fighter attack");
            Assert.IsTrue(s.IsOver, "the leader falls");
        }

        [Test]
        public void ProximityAlone_IsNotProtection()
        {
            var sc = new RosterScenario(BattleDomain.Orbital);
            int gun = sc.Add(0, "silex_monolith_battleship", 0, 2);
            sc.Add(0, "silex_throne_ark", -3, 4);
            sc.Add(1, "craigon_prime_synapse_flagship", 0, -1, hp: 3);
            sc.Add(1, "craigon_scuttler_interceptor", 1, -1);   // adjacent, but no guard was declared
            var s = sc.Build();
            var r = RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -1)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(VictoryReason.CommanderEliminated, s.VictoryReason);
        }

        [Test]
        public void BlockedMovement_IsNeverReachable_AndIsRejected()
        {
            var sc = new RosterScenario(BattleDomain.Orbital);
            int mover = sc.Add(0, "silex_prism_escort", 0, 0);
            int blocker = sc.Add(1, "craigon_scuttler_corvette", 1, 0);
            var s = sc.Build();
            var u = s.Unit(sc.Id(mover));
            var reach = RosterMovement.Reachable(s, u);
            Assert.IsFalse(reach.ContainsKey(new Hex(1, 0)), "an occupied hex is not a destination");
            Assert.IsFalse(RulesEngine.Validate(s, Command.Move(0, u.Id, new Hex(1, 0))).Ok);
            Assert.IsFalse(RulesEngine.Validate(s, Command.Move(0, u.Id, new Hex(40, 40))).Ok, "off the board");
        }

        [Test]
        public void RepeatedInput_IsRejected()
        {
            var sc = new RosterScenario(BattleDomain.Orbital);
            int gun = sc.Add(0, "silex_pillar_destroyer", 0, 1);
            sc.Add(1, "craigon_scuttler_corvette", 0, -2);
            var s = sc.Build();
            Assert.IsTrue(RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -2))).Accepted);
            Assert.IsFalse(RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -2))).Accepted, "a second submission is refused");
        }

        [Test]
        public void Preview_EqualsResolution_NoDiceAnywhere()
        {
            for (int trial = 0; trial < 3; trial++)
            {
                var sc = new RosterScenario(BattleDomain.Orbital);
                int gun = sc.Add(0, "silex_pillar_destroyer", 0, 1);
                int t = sc.Add(1, "craigon_pincer_frigate", 0, -2);
                var s = sc.Build();
                var preview = RosterCombat.ResolveBasic(s, s.Unit(sc.Id(gun)), new Hex(0, -2));
                int hp = s.Unit(sc.Id(t)).Hp;
                RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -2)));
                Assert.AreEqual(preview.TargetHpAfter, s.Unit(sc.Id(t)).Hp, "the previewed result is the result");
                Assert.AreEqual(hp - preview.FinalDamage, s.Unit(sc.Id(t)).Hp);
            }
        }
    }
}