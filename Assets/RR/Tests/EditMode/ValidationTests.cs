using System;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    // Prompt 12: failure and abuse cases across the whole slice, and incentive checks kept as regressions. Leader defeat,
    // failed boarding, legal retreat, rejected terms and simultaneous destruction are covered by the Prompt 3 to 8 suites;
    // these add damaged saves, repeated submissions, rapid repeated actions, duplication and the "always superior" checks.
    public sealed class ValidationTests
    {
        [Test]
        public void DamagedSaves_AreRefusedWithAReason_NeverAnException()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            string json = SaveGame.ToJson(g);
            foreach (var bad in new[] { "", "{", json.Substring(0, json.Length / 2), "[1,2,3]", "{\"schema\":\"something else\"}", System.Text.RegularExpressions.Regex.Replace(json, "\"version\":[ ]*2", "\"version\": 99") })
            {
                GalaxyState back = null; string err = null;
                Assert.DoesNotThrow(() => back = SaveGame.FromJson(bad, out err), "a damaged save never crashes the loader");
                Assert.IsNull(back, "damaged: " + (bad.Length > 40 ? bad.Substring(0, 40) : bad));
                Assert.IsNotEmpty(err, "the player is told why");
            }
        }

        [Test]
        public void ATamperedBattleRecord_IsRefused_NotReplayedDifferently()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            var (_, b) = Camp.Begin(g);
            var ai = new SliceAI();
            for (int i = 0; i < 6 && !b.State.IsOver; i++) Assert.IsTrue(b.Submit(ai.Choose(b.State), BattleSession.Host).Accepted);
            var snap = b.Snapshot();
            snap.Hashes[snap.Hashes.Count / 2] ^= 1UL;
            Assert.IsNull(BattleSession.Restore(snap, out var err), "a record whose hashes do not match is refused");
            Assert.IsNotEmpty(err);
            var ok = BattleSession.Restore(b.Snapshot(), out err);
            Assert.IsNotNull(ok, err);
            Assert.AreEqual(b.Hash, ok.Hash, "the untouched record restores exactly");
        }

        [Test]
        public void RepeatedResultSubmission_AppliesOnce()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            Camp.Play(b, s => new SliceAI().Choose(s), Camp.Idle);
            var o = BattleBridge.Extract(setup, b);
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            ulong after = SaveGame.Digest(g);
            int history = g.Slice.History.Count;
            for (int i = 0; i < 3; i++) BattleBridge.Apply(g, setup, o);   // rapid repeated confirmation of the result
            Assert.AreEqual(after, SaveGame.Digest(g), "a result applies exactly once");
            Assert.AreEqual(history, g.Slice.History.Count);
            Camp.NoDuplicateShips(g);
        }

        [Test]
        public void RapidRepeatedCampaignActions_TakeEffectOnce()
        {
            var g = Camp.New();
            int flag = Camp.Flagship(Camp.Mine(g));
            var first = SliceCampaign.ApplyRefit(g, flag, "heavy_battery");
            Assert.IsTrue(first.Ok, first.Reason);
            var stock = g.Player.Stock;
            for (int i = 0; i < 3; i++) Assert.IsFalse(SliceCampaign.ApplyRefit(g, flag, "heavy_battery").Ok, "already fitted");
            Assert.AreEqual(stock.Credits, g.Player.Stock.Credits, "paid once");
            Assert.AreEqual(1, g.Slice.Profile(flag).Modules.Count(m => m == "heavy_battery"));
            Assert.IsTrue(SliceCampaign.DeclareObjective(g, g.Player.Id, SliceCampaign.PassageSystem).Ok);
            Assert.IsFalse(SliceCampaign.DeclareObjective(g, g.Player.Id, SliceCampaign.PassageSystem).Ok, "declared once");
            Assert.AreEqual(1, g.Slice.Objectives.Count);
        }

        [Test]
        public void DepletedResources_RefuseRefitsAndRepairs_Cleanly()
        {
            var g = Camp.New();
            int flag = Camp.Flagship(Camp.Mine(g));
            g.Player.Stock = new Resources(0, 0, 0, 0);
            var r = SliceCampaign.ApplyRefit(g, flag, "ablative_plating");
            Assert.IsFalse(r.Ok); Assert.IsNotEmpty(r.Reason);
            Assert.IsEmpty(g.Slice.Profile(flag).Modules);
            Assert.AreEqual(new Resources(0, 0, 0, 0).Credits, g.Player.Stock.Credits, "nothing went negative");
            Camp.Reload(g);
        }

        [Test]
        public void ContractsAndModules_CannotBeDuplicated()
        {
            var caps = BattleBridge.Caps;
            var inh = new ArmyRoster { Faction = FactionId.Inhumanus };
            inh.Units.Add(new RosterEntry("inhumanus_executive_flagship"));
            var esc = new RosterEntry("inhumanus_proxy_frigate");
            esc.Equipment.Add(SliceRules.ContractToken + "west"); esc.Equipment.Add(SliceRules.ContractToken + "west");
            inh.Units.Add(esc);
            Assert.IsFalse(RosterArmy.Validate(inh, caps, BattleDomain.Orbital).Valid, "one escort cannot hold two contracts (double discount)");

            var pyg = new ArmyRoster { Faction = FactionId.Pygmaei };
            var flag = new RosterEntry("pygmaei_revenant_command_ship");
            flag.Equipment.Add(SliceRules.ModuleToken + "heavy_battery"); flag.Equipment.Add(SliceRules.ModuleToken + "heavy_battery");
            pyg.Units.Add(flag);
            Assert.IsFalse(RosterArmy.Validate(pyg, caps, BattleDomain.Orbital).Valid, "the same module twice fills one slot twice");
        }

        [Test]
        public void Overclocking_CannotBeSustained_EveryActivation()
        {
            // Heat exploit check with the rule functions themselves: overclocking whenever allowed for twelve activations of a
            // real Pygmaei frigate. Two in a row overheat it (armour −2, no overclock) and a quiet activation sheds only 2.
            var g = Camp.New();
            Camp.ToContact(g);
            var (_, b) = Camp.Begin(g);
            var u = b.State.Units.First(x => x.DefId == "pygmaei_skirmish_frigate");
            var ev = new System.Collections.Generic.List<GameEvent>();
            int overclocks = 0, overheatedActivations = 0;
            for (int act = 0; act < 12; act++)
            {
                if (SliceRules.CanOverclock(b.State, u) == null) { SliceRules.AddHeat(b.State, u, SliceRules.OverclockHeat, ev); overclocks++; }
                if (SliceRules.Overheated(u)) overheatedActivations++;
                SliceRules.Cool(u, ev);
            }
            Assert.LessOrEqual(overclocks, 7, "after the first two, at most one overclock in every two activations");
            Assert.GreaterOrEqual(overheatedActivations, overclocks - 1, "nearly every overclock is paid for with an overheated activation");
        }

        [Test]
        public void Negotiation_IsNotAFreeWin_AtTheStartOfABattle()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            var demand = new Offer { Id = "demand", FromSeat = setup.PlayerSeat, ConcederSeat = 1 - setup.PlayerSeat, Revision = b.State.Revision, Round = b.State.Round };
            var v = Negotiation.Decide(g, setup, b.State, demand, decidingSeat: 1 - setup.PlayerSeat);
            Assert.IsTrue(v.Valid, v.Reason);
            Assert.IsFalse(v.Accepted, "with no pressure on its flagship, the other side does not hand over the passage: " + v.Reason);
        }
    }
}
