using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    public class DeploymentTests
    {
        [Test]
        public void Deployment_ValidatesZoneOverlapAndCompleteness()
        {
            var s = RulesEngine.CreateMatch(Scenario.PresetConfig(FactionId.Craigon, FactionId.Silex));
            Assert.AreEqual(MatchPhase.Deployment, s.Phase);
            var mine = s.LiveUnits(0).ToList();
            var zone = s.Map.DeploymentZone(0);
            var good = mine.Select((u, i) => new Placement(u.Id, zone[i])).ToList();

            var partial = good.Take(good.Count - 1).ToList();
            StringAssert.Contains("Deploy all", Scenario.Rejected(s, Command.Deploy(0, partial)));
            var outside = good.ToList(); outside[0] = new Placement(outside[0].UnitId, new Hex(0, 0));
            StringAssert.Contains("deployment zone", Scenario.Rejected(s, Command.Deploy(0, outside)));
            var stacked = good.ToList(); stacked[1] = new Placement(stacked[1].UnitId, stacked[0].Hex);
            StringAssert.Contains("share a hex", Scenario.Rejected(s, Command.Deploy(0, stacked)));
            var enemyZone = good.ToList(); enemyZone[0] = new Placement(enemyZone[0].UnitId, s.Map.DeploymentZone(1)[0]);
            Scenario.Rejected(s, Command.Deploy(0, enemyZone));
            StringAssert.Contains("not started", Scenario.Rejected(s, Command.Pass(0)));

            Scenario.Ok(s, Command.Deploy(0, good));
            StringAssert.Contains("already committed", Scenario.Rejected(s, Command.Deploy(0, good)));
            Assert.AreEqual(MatchPhase.Deployment, s.Phase);
            Scenario.Ok(s, Command.AutoDeploy(1));
            Assert.AreEqual(MatchPhase.Battle, s.Phase);
            Assert.AreEqual(s.Config.FirstSeat, s.ActiveSeat);
            Assert.IsTrue(s.Units.All(u => u.Deployed));
        }

        [Test]
        public void AutoDeploy_IsLegalForEveryPresetOnEveryMap()
        {
            foreach (var map in MapLibrary.Maps.Keys.Where(k => !k.StartsWith("test")).ToList())
                foreach (var f in GameContent.AllFactions)
                    foreach (var army in ArmyPresets.For(f))
                    {
                        var cfg = Scenario.PresetConfig(f, FactionId.Meeples, map);
                        cfg.Players[0].Army = army.Clone();
                        var s = RulesEngine.CreateMatch(cfg);
                        var placements = RulesEngine.AutoDeployment(s, 0);
                        Assert.IsTrue(RulesEngine.ValidateDeployment(s, 0, placements).Ok, $"{map} {f} {army.Name}");
                        var cmd = s.Unit(s.Players[0].CommanderId);
                        var cmdHex = placements.First(p => p.UnitId == cmd.Id).Hex;
                        Assert.AreEqual(s.Map.Radius, cmdHex.R, "Commander deploys on the back row");
                    }
        }
    }

    public class TurnAndClockTests
    {
        [Test]
        public void Turns_Alternate_AndWrongSeatIsRejected()
        {
            var s = Scenario.DeployedPreset(FactionId.Meeples, FactionId.Pygmaei);
            Assert.AreEqual(0, s.ActiveSeat);
            StringAssert.Contains("not your turn", Scenario.Rejected(s, Command.Pass(1)));
            var enemy = s.LiveUnits(1).First();
            StringAssert.Contains("not your turn", Scenario.Rejected(s, Command.Guard(1, enemy.Id)));
            var mine = s.LiveUnits(0).First(u => !u.IsCommander);
            StringAssert.Contains("belongs to your opponent", Scenario.Rejected(s, Command.Guard(0, enemy.Id)));
            Scenario.Ok(s, Command.Guard(0, mine.Id));
            Assert.AreEqual(1, s.ActiveSeat);
            Assert.AreEqual(1, s.ActivationIndex);
            Scenario.Ok(s, Command.Pass(1));
            Assert.AreEqual(0, s.ActiveSeat);
            Assert.AreEqual(2, s.ActivationIndex);
        }

        [Test]
        public void DuplicateAndStaleCommands_AreRejected()
        {
            var s = Scenario.DeployedPreset(FactionId.Meeples, FactionId.Pygmaei);
            var c = Command.Pass(0); c.Nonce = 42; c.ExpectedRevision = s.Revision;
            Scenario.Ok(s, c);
            var c1 = Command.Pass(1); c1.Nonce = 42;
            StringAssert.Contains("Duplicate", Scenario.Rejected(s, c1));
            var stale = Command.Pass(1); stale.ExpectedRevision = s.Revision - 1;
            StringAssert.Contains("Stale", Scenario.Rejected(s, stale));
            var fresh = Command.Pass(1); fresh.ExpectedRevision = s.Revision; fresh.Nonce = 43;
            Scenario.Ok(s, fresh);
        }

        [Test]
        public void Clock_DeductsOnlyActivePlayersTime_AndExpiryForfeits()
        {
            var s = Scenario.DeployedPreset(FactionId.Meeples, FactionId.Pygmaei, clock: 150);
            Assert.AreEqual(150000, s.Players[0].ClockRemainingMs);
            var c = Command.Pass(0); c.ElapsedMs = 20000;
            Scenario.Ok(s, c);
            Assert.AreEqual(130000, s.Players[0].ClockRemainingMs);
            Assert.AreEqual(150000, s.Players[1].ClockRemainingMs);
            var late = Command.Pass(1); late.ElapsedMs = 150000;
            StringAssert.Contains("clock has run out", Scenario.Rejected(s, late));
            StringAssert.Contains("active player", Scenario.Rejected(s, Command.ClockExpire(0)));
            Scenario.Ok(s, Command.ClockExpire(1));
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.ClockExpired, s.VictoryReason);
        }

        [Test]
        public void Untimed_MatchesNeverExpire()
        {
            var s = Scenario.DeployedPreset(FactionId.Meeples, FactionId.Pygmaei, clock: 0);
            var c = Command.Pass(0); c.ElapsedMs = 999999;
            Scenario.Ok(s, c);
            StringAssert.Contains("untimed", Scenario.Rejected(s, Command.ClockExpire(1)));
        }

        [Test]
        public void ActivationLimit_DefenderWinsByDenial()
        {
            var b = new Scenario { ActivationLimit = 4, FirstSeat = 0 };
            b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 2);
            var s = b.Build();
            for (int i = 0; i < 3; i++) Scenario.PassTurn(s);
            Assert.IsFalse(s.IsOver);
            Scenario.PassTurn(s);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(1, s.Winner, "seat 1 moved second and defends");
            Assert.AreEqual(VictoryReason.ActivationLimitDefender, s.VictoryReason);
        }

        [Test]
        public void ActivationLimit_AttackerHoldingNoMansLand_BreaksDenial()
        {
            var b = new Scenario { ActivationLimit = 4, FirstSeat = 0 };
            b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 0);   // the attacker stands alone in the middle rows
            var s = b.Build();
            Assert.IsTrue(RulesEngine.HoldsNoMansLand(s, 0));
            for (int i = 0; i < 4; i++) Scenario.PassTurn(s);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner, "the attacker held the field");
            Assert.AreEqual(VictoryReason.ActivationLimitHeldGround, s.VictoryReason);
        }

        [Test]
        public void ActivationLimit_ContestedOrClearNoMansLand_KeepsDenial()
        {
            var b = new Scenario { ActivationLimit = 4, FirstSeat = 0 };
            b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 0);
            b.Add(1, FactionId.Silex, Archetype.Infantry, 2, -1);    // one defender unit contests the middle
            var s = b.Build();
            Assert.IsFalse(RulesEngine.HoldsNoMansLand(s, 0));
            Assert.IsTrue(RulesEngine.HoldsNoMansLand(new Scenario().Build(), 0) == false, "nobody in the middle: clear");
            for (int i = 0; i < 4; i++) Scenario.PassTurn(s);
            Assert.AreEqual(1, s.Winner);
            Assert.AreEqual(VictoryReason.ActivationLimitDefender, s.VictoryReason);
            // The exhibition tiebreak ignores the middle.
            var e = new Scenario { ActivationLimit = 4, FirstSeat = 0, Stalemate = StalemateRule.ExhibitionTiebreak };
            e.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 0);
            var se = e.Build();
            for (int i = 0; i < 4; i++) Scenario.PassTurn(se);
            Assert.AreEqual(VictoryReason.ActivationLimitTiebreak, se.VictoryReason);
        }

        [Test]
        public void ActivationLimit_StandardIs90_ForTheLargeBattlefields()
        {
            Assert.AreEqual(90, new MatchConfig().ActivationLimit);
            Assert.AreEqual(MatchConfig.StandardActivationLimit, Scenario.PresetConfig(FactionId.Craigon, FactionId.Silex).ActivationLimit);
        }

        [Test]
        public void ActivationLimit_ExhibitionTiebreak_IsNeverAmbiguous()
        {
            var b = new Scenario { ActivationLimit = 2, Stalemate = StalemateRule.ExhibitionTiebreak };
            b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 2);
            b.Add(1, FactionId.Meeples, Archetype.Infantry, 0, -2);
            var s = b.Build();
            Scenario.PassTurn(s); Scenario.PassTurn(s);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(1, s.Winner, "perfect tie → the seat that moved second");
            Assert.AreEqual(VictoryReason.ActivationLimitTiebreak, s.VictoryReason);

            var b2 = new Scenario { ActivationLimit = 2, Stalemate = StalemateRule.ExhibitionTiebreak };
            b2.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            b2.Add(1, FactionId.Meeples, Archetype.Infantry, 0, -2);
            var s2 = b2.Build();
            Scenario.PassTurn(s2); Scenario.PassTurn(s2);
            Assert.AreEqual(0, s2.Winner, "higher surviving army value wins");
        }

        [Test]
        public void TerminalState_IsImmutable()
        {
            var s = Scenario.DeployedPreset(FactionId.Meeples, FactionId.Pygmaei);
            Scenario.Ok(s, Command.Resign(1));
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.Resignation, s.VictoryReason);
            var hash = Serialization.StateHash(s);
            var u = s.LiveUnits(0).First();
            foreach (var c in new[] { Command.Pass(0), Command.Guard(0, u.Id), Command.Resign(0), Command.ClockExpire(0), Command.Forfeit(1), Command.Move(0, u.Id, u.Position.Neighbor(4)) })
                StringAssert.Contains("over", Scenario.Rejected(s, c));
            Assert.AreEqual(hash, Serialization.StateHash(s));
        }

        [Test]
        public void InvalidActions_ExplainThemselves_WithoutConsumingTheActivation()
        {
            var s = Scenario.DeployedPreset(FactionId.Meeples, FactionId.Pygmaei);
            var gunner = s.LiveUnits(0).First(u => u.Def.Archetype == Archetype.Gunner);
            var rev = s.Revision;
            var reason = Scenario.Rejected(s, Command.Move(0, gunner.Id, new Hex(0, -3)));
            Assert.IsFalse(string.IsNullOrEmpty(reason));
            Assert.AreEqual(rev, s.Revision);
            Assert.AreEqual(0, s.ActiveSeat, "still the same player's activation");
        }
    }

    public class DeterminismTests
    {
        [Test]
        public void Serialization_RoundTripsStateConfigAndCommands()
        {
            var s = Scenario.DeployedPreset(FactionId.Inhumanus, FactionId.SpacePirates);
            Scenario.Ok(s, Command.Guard(0, s.LiveUnits(0).First(u => !u.IsCommander).Id));
            var bytes = Serialization.StateBytes(s, includeConfig: true);
            var back = Serialization.FromBytes(bytes, r => Serialization.ReadState(r));
            Assert.AreEqual(Serialization.StateHash(s), Serialization.StateHash(back));
            CollectionAssert.AreEqual(Serialization.ConfigBytes(s.Config), Serialization.ConfigBytes(back.Config));
            var cmd = Command.Overclock(1, 7, new Hex(1, -2), new Hex(0, 1)); cmd.Nonce = 99; cmd.ElapsedMs = 1234; cmd.ExpectedRevision = 5;
            var cb = Serialization.FromBytes(Serialization.CommandBytes(cmd), Serialization.ReadCommand);
            CollectionAssert.AreEqual(Serialization.CommandBytes(cmd), Serialization.CommandBytes(cb));
            Assert.AreEqual(new Hex(0, 1), cb.Secondary);
        }

        [Test]
        public void ClonesAreIndependent()
        {
            var s = Scenario.DeployedPreset(FactionId.Craigon, FactionId.Silex);
            var h = Serialization.StateHash(s);
            var c = s.Clone();
            Scenario.Ok(c, Command.Pass(0));
            Assert.AreEqual(h, Serialization.StateHash(s));
            Assert.AreNotEqual(h, Serialization.StateHash(c));
        }

        [TestCase(FactionId.Craigon, FactionId.Pygmaei)]
        [TestCase(FactionId.Silex, FactionId.SpacePirates)]
        [TestCase(FactionId.Meeples, FactionId.Inhumanus)]
        [TestCase(FactionId.Inhumanus, FactionId.Inhumanus)]
        public void AIvsAI_CompletesWithOnlyLegalCommands_AndReplaysExactly(FactionId a, FactionId b)
        {
            var cfg = Scenario.PresetConfig(a, b, clock: 0);
            var session = new MatchSession(cfg);
            var ai = new[] { new TacticalAI(AIDifficulty.Tactical, 11) { Depth = 1 }, new TacticalAI(AIDifficulty.Beginner, 12) };
            for (int seat = 0; seat < 2; seat++) Assert.IsTrue(session.Submit(ai[seat].ChooseDeployment(session.State, seat)).Accepted);
            int guard = 0;
            while (!session.State.IsOver && guard++ < 200)
            {
                var cmd = ai[session.State.ActiveSeat].ChooseAction(session.State);
                var r = session.Submit(cmd);
                Assert.IsTrue(r.Accepted, $"AI chose an illegal command {cmd}: {r.Reason}");
            }
            Assert.IsTrue(session.State.IsOver, "match terminated");
            Assert.LessOrEqual(session.State.ActivationIndex, cfg.ActivationLimit);
            var replay = MatchSession.Replay(cfg, session.History);
            Assert.AreEqual(session.Hash, Serialization.StateHash(replay));
            var file = ReplayFile.FromBytes(ReplayFile.From(session, "test").ToBytes());
            Assert.AreEqual(session.History.Count, file.Commands.Count);
            Assert.AreEqual(session.Hash, Serialization.StateHash(MatchSession.Replay(file.Config, file.Commands)));
            StringAssert.Contains("\"activations\"", session.Stats.ToJson());
        }

        [Test]
        public void TacticalAI_TakesAnImmediateCommanderKill()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            int cmd = b.Add(1, FactionId.Pygmaei, Archetype.Commander, 0, 0);
            b.Add(0, FactionId.Meeples, Archetype.Infantry, -2, 2);
            b.WithHp(cmd, 1);
            var s = b.Build();
            var choice = new TacticalAI(AIDifficulty.Tactical).ChooseAction(s);
            Assert.AreEqual(CommandType.Attack, choice.Type);
            Assert.AreEqual(b.Id(gun), choice.UnitId);
            Assert.AreEqual(new Hex(0, 0), choice.Target);
        }

        [Test]
        public void ContentHash_IsStable()
        {
            Assert.AreEqual(Serialization.ContentHash(), Serialization.ContentHash());
        }
    }
}
