using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Campaign alpha Prompt 19: authored replay variants, the content pipeline and the ship manifest.</summary>
    public sealed class ReplayContentTests
    {
        static bool Connected(GalaxyState g, string a, string b, Func<string, bool> allowed)
        {
            var seen = new HashSet<string> { a }; var q = new Queue<string>(); q.Enqueue(a);
            while (q.Count > 0) { var x = q.Dequeue(); if (x == b) return true; foreach (var l in g.LanesOf(x)) { var n = l.Other(x); if (allowed(n) && seen.Add(n)) q.Enqueue(n); } }
            return false;
        }

        [Test]
        public void EveryVariant_PassesValidation_ForBothFactions_AndPersists()
        {
            foreach (var v in Variants.All)
                foreach (var f in new[] { FactionId.Pygmaei, FactionId.Inhumanus })
                {
                    var g = PassageWar.Create(f, v.Id);
                    var problems = Variants.Validate(g);
                    Assert.IsEmpty(problems, $"{v.Id} as {f}: {string.Join("; ", problems)}");
                    Assert.AreEqual(v.Id, War.Sc(g).Variant);
                    Assert.AreEqual(g.Seed, War.Sc(g).SetupSeed, "the setup seed is stored");
                    var back = Camp.Reload(g);
                    Assert.AreEqual(v.Id, War.Sc(back).Variant, "a reload never changes the world");
                    Assert.AreEqual(44, back.Systems.Count, "the wider galaxy stays");
                }
        }

        [Test]
        public void TheVariants_ChangeRoutes_Knowledge_Objectives_AndAffordableForces()
        {
            var std = PassageWar.Create(FactionId.Pygmaei, Variants.Standard);
            var open = PassageWar.Create(FactionId.Pygmaei, Variants.OpenCorridor);
            var narrow = PassageWar.Create(FactionId.Pygmaei, Variants.NarrowNetwork);
            var salvage = PassageWar.Create(FactionId.Pygmaei, Variants.SalvageField);
            // Route choice: a shorter approach in the open corridor; no way around Umbra in the narrow network.
            Assert.Less(StrategyRules.PlanRoute(open, Camp.Mine(open), "umbra").Days, StrategyRules.PlanRoute(std, Camp.Mine(std), "umbra").Days);
            Assert.IsTrue(Connected(std, "forgefall", "dividend", id => PassageWar.InScope(std, id) && id != "umbra"), "standard: the northern route bypasses Umbra");
            Assert.IsFalse(Connected(narrow, "forgefall", "dividend", id => PassageWar.InScope(narrow, id) && id != "umbra"), "narrow: every route runs through Umbra");
            // Initial knowledge: the derelict is charted from the start in the open corridor.
            Assert.IsTrue(Expeditions.Of(open).Scouted);
            Assert.IsFalse(Expeditions.Of(std).Scouted);
            // Objective value and affordable forces.
            Assert.AreEqual(5, open.Slice.Passage("umbra").TollPerDay);
            Assert.Less(narrow.Player.Stock.Credits, std.Player.Stock.Credits);
            // The salvage field: wrecks to take, damaged flagships, scarce materials, the wrecked battlefield, content loadouts.
            Assert.AreEqual(3, salvage.Slice.Wrecks.Count(w => !w.Salvaged));
            Assert.Less(Camp.Mine(salvage).Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Hp, ShipCatalog.MaxHp("pygmaei_revenant_command_ship"));
            Assert.AreEqual(SliceCampaign.WreckedPassageMap, salvage.GetSystem("umbra").BattlefieldMapId);
            Assert.AreEqual("Breaker Crews", Camp.Mine(salvage).Name);
            Assert.AreEqual(60, salvage.Player.Stock.Materials);
            Assert.IsNotNull(salvage.Slice.Profile(Camp.Flagship(Camp.Mine(salvage))).Name, "the flagship keeps its identity in a loadout");
        }

        [Test]
        public void TwoStartingSituations_DifferentDecisions_TheBlockadeTreatyIsOnlyOpenWhereTheNorthernRouteExists()
        {
            foreach (var (v, open) in new[] { (Variants.Standard, true), (Variants.NarrowNetwork, false) })
            {
                var g = PassageWar.Create(FactionId.Pygmaei, v);
                var route = StrategyRules.PlanRoute(g, Camp.Mine(g), "dividend");
                Assert.IsTrue(route.Legal);
                Assert.AreEqual(!open, route.Path.Contains("umbra"), $"{v}: the road to Dividend " + (open ? "can avoid" : "must cross") + " the passage");
            }
        }

        [Test]
        public void EveryVariant_ReachesAnEncounter_ThroughTheOrdinaryCommands()
        {
            foreach (var v in Variants.All)
            {
                var g = PassageWar.Create(FactionId.Pygmaei, v.Id);
                Camp.ToContact(g);
                var (setup, b) = Camp.Begin(g);
                Assert.AreEqual(g.GetSystem("umbra").BattlefieldMapId, setup.Config.MapId);
                Assert.IsFalse(b.State.IsOver);
            }
        }

        [Test]
        public void TheContentPipeline_AddsASidegrade_WithoutRulesCode()
        {
            Assert.IsEmpty(SliceContentCheck.Validate());
            Assert.AreEqual("rr.slice.content-1", SliceContent.Version);
            // The original modules keep their ids and values (save compatibility).
            var ab = SliceRules.Module("afterburners");
            Assert.AreEqual((1, -2), (ab.Move, ab.Hull));
            Assert.AreEqual((1, -1), (SliceRules.Module("heavy_battery").Damage, SliceRules.Module("heavy_battery").Range));
            Assert.AreEqual(new Resources(10, 20, 30, 0), SliceCampaign.ModuleCost("afterburners"));
            // The new sidegrade is offered at the shipyard, fitted, and read by the same stat computation in battle.
            var g = War.New();
            var flag = Camp.Flagship(Camp.Mine(g));
            Assert.IsTrue(SliceCampaign.RefitOptions(g, flag).Any(o => o.Module.Id == "extended_magazines"));
            Assert.IsTrue(SliceCampaign.ApplyRefit(g, flag, "extended_magazines").Ok);
            var cfg = new MatchConfig { ClockSeconds = 0 }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = "orbital_gate"; cfg.TechMode = TechMode.Balanced;
            cfg.Players[0] = new PlayerSetup { Caps = BattleBridge.Caps.Clone(), Army = BattleBridge.ArmyFrom(Camp.Mine(g), new[] { flag }, g) };
            cfg.Players[1] = new PlayerSetup { Caps = BattleBridge.Caps.Clone(), Army = new ArmyRoster { Faction = FactionId.Inhumanus, Units = { new RosterEntry("inhumanus_executive_flagship") } } };
            var s = RulesEngine.CreateMatch(cfg, out var err);
            Assert.IsNotNull(s, err);
            var u = s.Units.First(x => x.Owner == 0);
            var def = RosterContent.Unit("pygmaei_revenant_command_ship");
            Assert.AreEqual(def.Weapon.MaxRange + 1, u.Weapon.MaxRange, "+1 range");
            Assert.AreEqual(Math.Max(1, def.Weapon.Damage - 1), u.Weapon.Damage, "-1 damage");
            // A changed module is a different content fingerprint, so a battle saved under other content is refused.
            var m = SliceRules.Module("extended_magazines");
            ulong before = Serialization.ContentHash();
            m.Range = 2;
            try { Assert.AreNotEqual(before, Serialization.ContentHash()); }
            finally { m.Range = 1; }
            Assert.AreEqual(before, Serialization.ContentHash());
        }

        [Test]
        public void TheShipManifest_MatchesTheRegistry()
        {
            var rows = ShipManifest.Build();
            Assert.AreEqual(SliceRoster.Classes(FactionId.Pygmaei).Count() + SliceRoster.Classes(FactionId.Inhumanus).Count(), rows.Count);
            foreach (var r in rows)
            {
                var d = RosterContent.Unit(r.Id);
                Assert.AreEqual(d.MaxHp, r.Hull, r.Id);
                Assert.AreEqual(d.Armor, r.Armor, r.Id);
                Assert.AreEqual(RosterContent.Cost(d), r.Cost, r.Id);
                Assert.IsTrue(r.Attack.Contains($"{d.Weapon.Damage} damage, range {d.Weapon.MinRange}-{d.Weapon.MaxRange}"), r.Attack);
                Assert.AreEqual(d.Roster.IsLeaderRole, r.Leadership.StartsWith("flagship"), r.Id);
            }
            Assert.IsTrue(ShipManifest.RulesStamp.Contains(RulesVersion.Id));
            var md = ShipManifest.ToMarkdown(rows);
            Assert.IsTrue(md.Contains("provisional"));
            var json = Json.Parse(ShipManifest.ToJson(rows)) as IDictionary<string, object>;
            Assert.AreEqual(rows.Count, ((List<object>)json["ships"]).Count);
        }
    }
}
