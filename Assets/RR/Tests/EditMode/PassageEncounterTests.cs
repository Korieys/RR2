using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Scripted pilots for the encounter tests. They choose only legal commands; they are test opponents, not AI.</summary>
    static class Pilots
    {
        /// <summary>The simplistic rush: shoot the enemy flagship if possible, otherwise shoot anything, otherwise drive at it.</summary>
        public static Command Rush(MatchState s)
        {
            int me = s.ActiveSeat; var flag = s.Commander(1 - me);
            var legal = LegalActions.EnumerateRoster(s);
            var shot = legal.FirstOrDefault(c => c.Type == CommandType.Attack && c.Target == flag.Position);
            if (shot != null) return shot;
            var any = legal.FirstOrDefault(c => c.Type == CommandType.Attack);
            if (s.ActiveUnitId != 0 && any != null) return any;
            var mv = legal.Where(c => c.Type == CommandType.Move && c.Option == 0).OrderBy(c => Hex.Distance(c.Target, flag.Position)).ThenBy(c => c.UnitId).FirstOrDefault();
            return mv ?? any ?? legal.First();
        }

        /// <summary>The defensive mistake: the flagship drives for the passage, the escort fires at whatever it can.</summary>
        public static Command Blunder(MatchState s)
        {
            int me = s.ActiveSeat; var flag = s.Commander(me);
            var legal = LegalActions.EnumerateRoster(s);
            var fm = legal.Where(c => c.UnitId == flag.Id && c.Type == CommandType.Move && c.Option == 0).OrderBy(c => Hex.Distance(c.Target, Hex.Zero)).FirstOrDefault();
            if (fm != null && RosterRules.Eligible(s, flag)) return fm;
            return legal.FirstOrDefault(c => c.Type == CommandType.Attack) ?? Command.Pass(me);
        }

        /// <summary>A flank plan: strike craft rally toward <paramref name="rally"/> (overclocking if allowed) for
        /// <paramref name="rounds"/> rounds, taking any lethal line or shot at the flagship; then the slice AI.</summary>
        public static Func<MatchState, Command> Flank(Hex rally, int rounds, bool overclock)
        {
            var ai = new SliceAI();
            return s =>
            {
                if (s.Round > rounds) return ai.Choose(s);
                int me = s.ActiveSeat;
                var win = CommandThreat.Assess(s, 1 - me);
                if (win.Lines.Count > 0) return win.Lines[0].Move ?? win.Lines[0].Action;
                var legal = LegalActions.EnumerateRoster(s, 64, 24);
                var leaderShot = legal.FirstOrDefault(c => c.Type == CommandType.Attack && c.Target == s.Commander(1 - me).Position);
                if (leaderShot != null) return leaderShot;
                if (s.ActiveUnitId != 0) return legal.FirstOrDefault(c => c.Type == CommandType.Attack) ?? Command.Wait(me, s.ActiveUnitId);
                var leaderId = s.Players[me].CommanderId;
                var best = legal.Where(c => c.Type == CommandType.Move && c.UnitId != leaderId && (overclock ? c.Option != SliceRules.TurnOption : c.Option == 0))
                                .OrderBy(c => Hex.Distance(c.Target, rally)).ThenBy(c => c.UnitId).FirstOrDefault();
                return best ?? ai.Choose(s);
            };
        }

        public static BattleSession Play(BattleSetup setup, Func<MatchState, Command> p0, Func<MatchState, Command> p1, int max = 600)
        {
            var b = BattleSession.Begin(setup, out var err);
            Assert.IsNotNull(b, err);
            for (int i = 0; i < max && !b.State.IsOver; i++)
            {
                var c = (b.State.ActiveSeat == 0 ? p0 : p1)(b.State);
                var r = b.Submit(c, BattleSession.Host);
                Assert.IsTrue(r.Accepted, $"order {i} ({c}) was refused: {r.Reason}");
            }
            Assert.IsTrue(b.State.IsOver, "the battle reaches a result");
            return b;
        }
    }

    /// <summary>Prompt 5: the curated trade-passage encounter and its opponent.</summary>
    public sealed class PassageEncounterTests
    {
        static BattleSetup Challenge(Hex? flagshipAt = null) => PassageScenario.Create(PassageVariant.Challenge, FactionId.Pygmaei, 0, null, flagshipAt);

        [Test]
        public void TheEncounter_UsesOrdinaryForceCaps_TheRaidersAreOutnumbered_AndTheSetupIsData()
        {
            var setup = Challenge();
            var reports = Enumerable.Range(0, 2).Select(seat => RosterArmy.Validate(setup.Config.Players[seat].Army, BattleBridge.Caps, BattleDomain.Orbital)).ToList();
            Assert.IsTrue(reports.All(r => r.Valid), string.Join("; ", reports.SelectMany(r => r.Errors)));
            Assert.Less(reports[0].UnitCount, reports[1].UnitCount);
            Assert.Less(reports[0].TotalCost, reports[1].TotalCost);
            Assert.AreEqual("trade_passage", setup.Config.MapId);
            var b = BattleSession.Begin(setup, out var err);
            Assert.IsNotNull(b, err);
            Assert.AreEqual(PassageScenario.FlagshipCarriedHull, b.State.Commander(1).Hp, "the convoy flagship carries earlier damage");
            CollectionAssert.AreEqual(setup.Deployment[0], b.State.Units.Where(u => u.Owner == 0).OrderBy(u => u.Id).Select(u => u.Position).ToList());
            var back = BattleSession.Restore(BattleSession.FromData((IDictionary<string, object>)Json.Parse(Json.Write(BattleSession.ToData(b.Snapshot()))), out err), out err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(b.InitialHash, back.InitialHash, "a reload rebuilds the same start, never a reroll");
            Assert.AreEqual("passage_challenge", back.Setup.Scenario);
        }

        [Test]
        public void TheAI_TakesALegalLethalLine_WhenItHasOne()
        {
            var f = new OrbitalFixture();
            int gun = f.Add(0, "pygmaei_skirmish_frigate", 3, 0);
            f.Add(1, "inhumanus_executive_flagship", 0, -2, hp: 2);
            var s = f.Build();
            var c = new SliceAI().Choose(s);
            var after = s.Clone();
            Assert.IsTrue(RulesEngine.Apply(after, c).Accepted);
            if (!after.IsOver) Assert.IsTrue(RulesEngine.Apply(after, new SliceAI().Choose(after)).Accepted);
            Assert.IsTrue(after.IsOver, "it finishes the line it started");
            Assert.AreEqual(0, after.Winner);
        }

        [Test]
        public void TheAI_RespondsToAnAvoidableThreat_OnItsFlagship()
        {
            // The Inhumanus flagship sits in a Pygmaei frigate's lethal reach; the escort acts first.
            var f = new OrbitalFixture();
            f.Add(1, "inhumanus_executive_flagship", 0, -2, hp: 4);
            f.Add(1, "inhumanus_mercator_corvette", -2, -3);
            f.Add(0, "pygmaei_skirmish_frigate", 1, 0);
            var s = f.Build();
            s.ActiveSeat = 1;
            var before = CommandThreat.Assess(s, 1);
            Assume.That(before.Lines.Count, Is.GreaterThan(0), "the fixture starts with a lethal threat");
            var ai = new SliceAI();
            var c = ai.Choose(s);
            var after = s.Clone();
            Assert.IsTrue(RulesEngine.Apply(after, c).Accepted, ai.LastReason);
            if (after.ActiveSeat == 1 && after.ActiveUnitId != 0) RulesEngine.Apply(after, Command.Wait(1, after.ActiveUnitId));
            Assert.Less(CommandThreat.Assess(after, 1).Lines.Count, before.Lines.Count, "its order removed lethal lines: " + ai.LastReason);
            StringAssert.StartsWith("Defence", ai.LastReason);
        }

        [Test]
        public void TheAI_DependsOnlyOnTheState()
        {
            var b = BattleSession.Begin(Challenge(), out _);
            var ai = new SliceAI();
            for (int i = 0; i < 6; i++) b.Submit(ai.Choose(b.State), BattleSession.Host);
            var one = new SliceAI().Choose(b.State);
            var two = new SliceAI().Choose(b.State.Clone());
            var restored = BattleSession.Restore(b.Snapshot(), out var err);
            var three = new SliceAI().Choose(restored.State);
            Assert.AreEqual(one.ToString(), two.ToString());
            Assert.AreEqual(one.ToString(), three.ToString(), "identical after a reload (D-12 fixed)");
        }

        [Test]
        public void ASimplisticRush_IsCounteredByACorrectlyProtectedFlagship()
        {
            var ai = new SliceAI();
            var b = Pilots.Play(Challenge(), Pilots.Rush, ai.Choose);
            Assert.AreEqual(1, b.State.Winner, "the defended convoy wins: " + b.Decisive);
            Assert.IsTrue(b.State.Commander(1).Alive);
        }

        [Test]
        public void ViablePlan1_TheRaidersPunishAFlagshipThatDrivesForThePassage()
        {
            var ai = new SliceAI();
            var b = Pilots.Play(Challenge(), ai.Choose, Pilots.Blunder);
            Assert.AreEqual(0, b.State.Winner, b.Decisive);
            Assert.AreEqual(VictoryReason.CommanderEliminated, b.State.VictoryReason);
            Assert.Greater(b.State.LiveUnits(1).Count(), 0, "the escort survives; the flagship's exposure decided it");
        }

        [Test]
        public void ViablePlan2_AnEastFlankBeatsAFlagshipParkedInTheEastCorner()
        {
            var ai = new SliceAI();
            var b = Pilots.Play(Challenge(new Hex(6, -4)), Pilots.Flank(new Hex(6, -2), 2, true), ai.Choose);
            Assert.AreEqual(0, b.State.Winner, b.Decisive);
            Assert.AreEqual(VictoryReason.CommanderEliminated, b.State.VictoryReason);
        }

        [Test]
        public void AnAIMirror_ReachesAValidResult_AndReplaysExactly()
        {
            var ai = new SliceAI();
            var b = Pilots.Play(Challenge(), ai.Choose, ai.Choose);
            Assert.That(b.State.Winner, Is.EqualTo(0).Or.EqualTo(1));
            Assert.IsNotNull(b.Decisive);
            Assert.IsNull(BattleSession.FindDivergence(b.Snapshot(), out _));
            Assert.LessOrEqual(b.State.Round, b.State.Config.RoundLimit);
        }

        [Test]
        public void Training_TheEnemyHolds_AndTheLessonsUseTheOrdinaryRules()
        {
            var setup = PassageScenario.Create(PassageVariant.Training, FactionId.Pygmaei);
            Assert.AreEqual("passage_training", setup.Scenario);
            var ai = new SliceAI();
            Func<MatchState, Command> hold = s => s.ActiveUnitId != 0 ? Command.Wait(s.ActiveSeat, s.ActiveUnitId)
                : Command.Wait(s.ActiveSeat, s.Units.First(u => u.Owner == s.ActiveSeat && RosterRules.Eligible(s, u)).Id);
            var b = Pilots.Play(setup, ai.Choose, hold);
            Assert.AreEqual(0, b.State.Winner);
            Assert.AreEqual(VictoryReason.CommanderEliminated, b.State.VictoryReason);
            Assert.AreEqual(3, PassageScenario.TrainingSteps.Length);
            // The training flagship is screened from the front by a wreck: the lesson needs a flank, by the real rules.
            var start = BattleSession.Begin(setup, out _).State;
            var frigate = start.Units.First(u => u.Owner == 0 && u.Position == new Hex(2, 2));
            Assert.AreNotEqual(LosState.Clear, RosterCombat.LineOfSight(start, frigate, frigate.Position, start.Commander(1).Position), "the wreck at (2,0) blocks the frontal line");
            Assert.AreEqual(0, CommandThreat.Assess(start, 1).Lines.Count, "no single order wins the training, so the lessons cannot be skipped");
            Assert.IsTrue(start.Units.Any(u => u.Owner == 0 && u.R.ActiveCode == EffectCode.INTERCEPT), "the protection lesson has a real guard to declare");
        }

        [Test]
        public void Narration_ExplainsTheDecisiveAction()
        {
            var ai = new SliceAI();
            var b = Pilots.Play(Challenge(), ai.Choose, Pilots.Blunder);
            StringAssert.Contains("flagship", b.Decisive);
            StringAssert.Contains("battle ended", b.Decisive);
            Assert.AreEqual(b.Log.Count, b.Narration.Count);
        }
    }
}
