using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Orbital units placed at exact hexes on a small authored test map (deployment bypassed on purpose).</summary>
    sealed class OrbitalFixture
    {
        public const string MapId = "test_vs_orbit";

        // Radius-4 orbital board (point-symmetric):
        //   large wrecks (block movement and sight) at (3,0) and (-3,0); asteroid cover at (0,-2) and (0,2).
        static OrbitalFixture()
        {
            if (!MapLibrary.Maps.ContainsKey(MapId))
                MapLibrary.Maps[MapId] = new MapDefinition(MapId, "Vertical slice rules board", "Two wrecks and two asteroid fields.", 4)
                    .Orbital().Mirror(TerrainType.Obstacle, (3, 0)).Mirror(TerrainType.Cover, (0, -2));
        }

        readonly List<(int seat, string id, Hex hex, int hp)> _units = new List<(int, string, Hex, int)>();
        int[] _ids;

        public int Add(int seat, string id, int q, int r, int hp = -1) { _units.Add((seat, id, new Hex(q, r), hp)); return _units.Count - 1; }
        public int Id(int handle) => _ids[handle];

        public MatchState Build()
        {
            var corner = new[] { new Hex(-3, 4), new Hex(3, -4) };
            for (int seat = 0; seat < 2; seat++)
            {
                if (_units.Any(x => x.seat == seat && RosterContent.Unit(x.id).Roster.IsLeaderRole)) continue;
                var f = RosterContent.Unit(_units.First(x => x.seat == seat).id).Faction;
                _units.Add((seat, RosterContent.Leaders(f, BattleDomain.Orbital).First().Id, corner[seat], -1));
            }
            var cfg = new MatchConfig { ClockSeconds = 0, FirstSeat = 0, DefenderSeat = 1 }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = MapId; cfg.PointBudget = 999; cfg.MaxUnits = 99;
            var order = new List<int>();
            for (int seat = 0; seat < 2; seat++)
            {
                var idx = Enumerable.Range(0, _units.Count).Where(i => _units[i].seat == seat).ToList();
                var army = new ArmyRoster { Faction = RosterContent.Unit(_units[idx[0]].id).Faction };
                foreach (var i in idx) { army.Units.Add(new RosterEntry(_units[i].id)); order.Add(i); }
                cfg.Players[seat] = new PlayerSetup { DisplayName = $"P{seat}", Army = army, Caps = new TechCaps(1) { Unlock = 5 } };
            }
            var s = RosterRules.CreateMatch(cfg, out var err, validateArmies: false);
            if (s == null) throw new InvalidOperationException(err);
            _ids = new int[_units.Count];
            for (int k = 0; k < order.Count; k++)
            {
                var spec = _units[order[k]];
                var u = s.Units[k];
                _ids[order[k]] = u.Id;
                u.Position = spec.hex; u.Deployed = true;
                if (spec.hp > 0) u.Hp = spec.hp;
            }
            s.Players[0].DeploymentCommitted = s.Players[1].DeploymentCommitted = true;
            s.Phase = MatchPhase.Battle;
            s.ActiveSeat = 0;
            return s;
        }

        public static string Rejected(MatchState s, Command c)
        {
            ulong before = Serialization.StateHash(s);
            var r = RulesEngine.Apply(s, c);
            Assert.IsFalse(r.Accepted, $"{c} should be rejected");
            Assert.IsNotEmpty(r.Reason, "a rejection explains itself");
            Assert.AreEqual(before, Serialization.StateHash(s), "a rejected command leaves the state untouched");
            return r.Reason;
        }

        public static void Ok(MatchState s, Command c)
        {
            var r = RulesEngine.Apply(s, c);
            Assert.IsTrue(r.Accepted, $"{c}: {r.Reason}");
        }

        /// <summary>Every eligible unit waits until the round number changes.</summary>
        public static void FinishRound(MatchState s)
        {
            int round = s.Round;
            for (int guard = 0; guard < 64 && s.Round == round && !s.IsOver; guard++)
            {
                var u = s.ActiveUnitId != 0 ? s.Unit(s.ActiveUnitId)
                      : s.Units.Where(x => x.Owner == s.ActiveSeat && RosterRules.Eligible(s, x)).OrderBy(x => x.Id).First();
                Ok(s, Command.Wait(s.ActiveSeat, u.Id));
            }
            Assert.AreEqual(round + 1, s.Round);
        }
    }

    /// <summary>Prompt 1 rules matrix on orbital units: the authoritative engine decides movement, sight, defences,
    /// activations, cooldowns and rounds, with no randomness, and a rejected command never changes anything.</summary>
    public sealed class OrbitalRulesMatrixTests
    {
        const string Frigate = "pygmaei_skirmish_frigate";          // Ray 3, Skirmish Guns 4 at 2-3, Flanking Run cooldown 2
        const string Destroyer = "pygmaei_annihilator_destroyer";   // Ray 3, Annihilator Array 5 at 2-4
        const string Target = "inhumanus_arbitration_destroyer";    // 11 hull, armour 1

        [Test]
        public void Movement_FollowsThePattern_AndStopsAtWrecksAndShips()
        {
            var f = new OrbitalFixture();
            int mover = f.Add(0, Frigate, -1, 0);
            f.Add(1, Target, 0, -1);
            var s = f.Build();
            var u = s.Unit(f.Id(mover));
            var reach = RosterMovement.Reachable(s, u);
            Assert.IsTrue(reach.ContainsKey(new Hex(2, 0)), "three hexes in a straight line");
            Assert.AreEqual(3, reach[new Hex(2, 0)].Cost);
            Assert.IsTrue(reach.ContainsKey(new Hex(-2, 0)));
            Assert.IsFalse(reach.ContainsKey(new Hex(-3, 0)), "a large wreck is not a destination");
            Assert.IsFalse(reach.ContainsKey(new Hex(-4, 0)), "a ray cannot pass through a wreck");
            Assert.IsFalse(reach.ContainsKey(new Hex(0, 1)), "a ray cannot bend");
            Assert.IsFalse(reach.ContainsKey(new Hex(1, -2)), "a ray cannot pass through a ship");
            StringAssert.DoesNotContain("Exception", OrbitalFixture.Rejected(s, Command.Move(0, u.Id, new Hex(-4, 0))));
            OrbitalFixture.Rejected(s, Command.Move(0, u.Id, new Hex(0, 1)));
            OrbitalFixture.Rejected(s, Command.Move(0, u.Id, new Hex(9, 9)));
            OrbitalFixture.Ok(s, Command.Move(0, u.Id, new Hex(2, 0)));
            Assert.AreEqual(new Hex(2, 0), u.Position);
        }

        [Test]
        public void RangeAndLineOfSight_AreEnforced()
        {
            var f = new OrbitalFixture();
            int gun = f.Add(0, Destroyer, 4, -1);                   // range 2 to 4
            int behindWreck = f.Add(1, Target, 2, 0);
            var s = f.Build();
            var g = s.Unit(f.Id(gun));
            g.Position = new Hex(4, 0);                             // the wreck at (3,0) now sits exactly between gun and target
            var blocked = RosterCombat.ResolveBasic(s, g, new Hex(2, 0));
            Assert.IsFalse(blocked.Legal, "the wreck at (3,0) blocks the line of fire");
            OrbitalFixture.Rejected(s, Command.Attack(0, g.Id, new Hex(2, 0)));
            // Too close and too far.
            s.Unit(f.Id(behindWreck)).Position = new Hex(4, -1);
            Assert.IsFalse(RosterCombat.ResolveBasic(s, g, new Hex(4, -1)).Legal, "inside minimum range 2");
            OrbitalFixture.Rejected(s, Command.Attack(0, g.Id, new Hex(4, -1)));
            s.Unit(f.Id(behindWreck)).Position = new Hex(-1, 0);
            Assert.AreEqual(5, Hex.Distance(g.Position, new Hex(-1, 0)));
            Assert.IsFalse(RosterCombat.ResolveBasic(s, g, new Hex(-1, 0)).Legal, "beyond maximum range 4");
            // In range with a clear line: legal, and the preview is the result.
            s.Unit(f.Id(behindWreck)).Position = new Hex(4, -2);
            var ok = RosterCombat.ResolveBasic(s, g, new Hex(4, -2));
            Assert.IsTrue(ok.Legal, ok.Reason);
            OrbitalFixture.Ok(s, Command.Attack(0, g.Id, new Hex(4, -2)));
            Assert.AreEqual(ok.TargetHpAfter, s.Unit(f.Id(behindWreck)).Hp);
        }

        static (int dmg, int hpLoss, HitResult hit) Shot(Action<OrbitalFixture> extra, Hex targetAt, Action<MatchState, UnitState> prepare = null)
        {
            var f = new OrbitalFixture();
            int gun = f.Add(0, Destroyer, 0, 1);
            int t = f.Add(1, Target, targetAt.Q, targetAt.R);
            extra?.Invoke(f);
            var s = f.Build();
            var target = s.Unit(f.Id(t));
            prepare?.Invoke(s, target);
            var preview = RosterCombat.ResolveBasic(s, s.Unit(f.Id(gun)), targetAt);
            Assert.IsTrue(preview.Legal, preview.Reason);
            int hp = target.Hp;
            OrbitalFixture.Ok(s, Command.Attack(0, f.Id(gun), targetAt));
            Assert.AreEqual(preview.TargetHpAfter, target.Hp, "the previewed hull is the committed hull");
            return (preview.FinalDamage, hp - target.Hp, preview.Roster);
        }

        [Test]
        public void Defences_ArmourShieldsAndCover_ReduceDamageExactly()
        {
            var open = Shot(null, new Hex(3, -2));                  // distance 3, clear line, open space
            var cover = Shot(null, new Hex(0, -2));                 // distance 3, asteroid field
            Assert.AreEqual(open.hpLoss - 1, cover.hpLoss, "asteroid cover removes exactly 1 damage");
            Assert.AreEqual(1, cover.hit.Cover);

            var armoured = Shot(null, new Hex(3, -2), (s, t) => t.RStatuses.Add(new RStatus { Type = RStatusType.ArmorBuff, Amount = 1, Expire = RExpire.Battle }));
            Assert.AreEqual(open.hpLoss - 1, armoured.hpLoss, "one more armour removes exactly 1 damage");
            Assert.AreEqual(open.hit.Armor + 1, armoured.hit.Armor);

            var shielded = Shot(null, new Hex(3, -2), (s, t) => t.RStatuses.Add(new RStatus { Type = RStatusType.Shield, Amount = 2, Expire = RExpire.Battle }));
            Assert.AreEqual(open.hpLoss - 2, shielded.hpLoss, "shield points are absorbed before hull");
            Assert.AreEqual(2, shielded.hit.ShieldAbsorbed);
            Assert.AreEqual(open.dmg, shielded.hit.Total, "the shield changes where damage lands, not how much");
        }

        [Test]
        public void Activation_IsOneMoveAndOneAction_AndAnAbilityNeverGrantsASecondAttack()
        {
            var f = new OrbitalFixture();
            int a = f.Add(0, Frigate, -1, 1);
            int b = f.Add(0, Frigate, 1, 1);
            int foe = f.Add(1, Target, 1, -1);
            var s = f.Build();
            // Move then attack in one activation.
            OrbitalFixture.Ok(s, Command.Move(0, f.Id(a), new Hex(-1, 0)));
            Assert.AreEqual(f.Id(a), s.ActiveUnitId, "the activation continues after a move");
            OrbitalFixture.Rejected(s, Command.Move(0, f.Id(a), new Hex(-2, 0)));
            OrbitalFixture.Rejected(s, Command.Attack(0, f.Id(b), new Hex(1, -1)));   // another unit cannot act mid-activation
            OrbitalFixture.Ok(s, Command.Attack(0, f.Id(a), new Hex(1, -1)));
            Assert.AreEqual(0, s.ActiveUnitId, "the attack ends the activation");
            Assert.AreEqual(1, s.ActiveSeat, "control alternates");
            OrbitalFixture.Rejected(s, Command.Attack(0, f.Id(a), new Hex(1, -1)));
            OrbitalFixture.Ok(s, Command.Wait(1, f.Id(foe)));
            OrbitalFixture.Rejected(s, Command.Attack(0, f.Id(a), new Hex(1, -1)));   // already activated this round
            // The second frigate uses Flanking Run; it may not also fire in that activation.
            var bu = s.Unit(f.Id(b));
            var use = RosterAbilities.Enumerate(s, bu).First();
            OrbitalFixture.Ok(s, Command.Active(0, bu.Id, use.picks, use.option));
            int foeHp = s.Unit(f.Id(foe)).Hp;
            OrbitalFixture.Rejected(s, Command.Attack(0, bu.Id, new Hex(1, -1)));
            Assert.AreEqual(foeHp, s.Unit(f.Id(foe)).Hp);
        }

        [Test]
        public void Cooldowns_SkipExactlyTheirOwnActivations()
        {
            var f = new OrbitalFixture();
            int a = f.Add(0, Frigate, -1, 1);
            f.Add(1, Target, 2, -3);
            var s = f.Build();
            var u = s.Unit(f.Id(a));
            int cd = u.R.ActiveCooldown;
            Assert.AreEqual(2, cd);
            var use = RosterAbilities.Enumerate(s, u).First();
            OrbitalFixture.Ok(s, Command.Active(0, u.Id, use.picks, use.option));
            for (int k = 0; k < cd; k++)
            {
                OrbitalFixture.FinishRound(s);
                while (s.ActiveSeat != 0) OrbitalFixture.Ok(s, Command.Wait(1, s.Units.First(x => x.Owner == 1 && RosterRules.Eligible(s, x)).Id));
                var again = RosterAbilities.Enumerate(s, u).FirstOrDefault();
                string why = again.picks == null ? RosterRules.CanAct(s, u, attack: false)
                                                 : OrbitalFixture.Rejected(s, Command.Active(0, u.Id, again.picks, again.option));
                StringAssert.Contains("recharging", why, $"own activation {k + 1} after use");
                OrbitalFixture.Ok(s, Command.Wait(0, u.Id));
            }
            OrbitalFixture.FinishRound(s);
            while (s.ActiveSeat != 0) OrbitalFixture.Ok(s, Command.Wait(1, s.Units.First(x => x.Owner == 1 && RosterRules.Eligible(s, x)).Id));
            var ready = RosterAbilities.Enumerate(s, u).First();
            OrbitalFixture.Ok(s, Command.Active(0, u.Id, ready.picks, ready.option));
        }

        [Test]
        public void Rounds_ChangeWhenEveryUnitHasActivated_AndEveryoneMayActAgain()
        {
            var f = new OrbitalFixture();
            int a = f.Add(0, Frigate, -1, 1);
            int b = f.Add(1, Target, 2, -3);
            var s = f.Build();
            Assert.AreEqual(1, s.Round);
            OrbitalFixture.Ok(s, Command.Wait(0, f.Id(a)));
            OrbitalFixture.Ok(s, Command.Wait(1, f.Id(b)));
            Assert.AreEqual(1, s.Round, "the leaders have not activated yet");
            OrbitalFixture.Ok(s, Command.Wait(0, s.Players[0].CommanderId));
            OrbitalFixture.Ok(s, Command.Wait(1, s.Players[1].CommanderId));
            Assert.AreEqual(2, s.Round);
            Assert.IsTrue(s.Units.Where(u => u.Alive).All(u => RosterRules.Eligible(s, u)), "every surviving unit activates once per round");
            Assert.AreEqual(0, s.ActiveSeat, "the side that did not act last opens the new round");
        }

        [Test]
        public void NoDice_TheSameShotAlwaysResolvesTheSame()
        {
            var results = Enumerable.Range(0, 5).Select(_ => Shot(null, new Hex(3, -2))).Select(r => (r.dmg, r.hpLoss)).Distinct().ToList();
            Assert.AreEqual(1, results.Count);
        }
    }

    /// <summary>Prompt 1: the authoritative battle session, its record, replay, divergence report and mid-battle saves.</summary>
    public sealed class BattleSessionTests
    {
        static GalaxyState Contact()
        {
            var g = SliceScenario.Create();
            for (int i = 0; i < 60 && g.PendingEngagement == null; i++) StrategyRules.AdvanceDay(g);
            Assert.IsNotNull(g.PendingEngagement);
            return g;
        }

        static BattleSession Begin(GalaxyState g)
        {
            var setup = BattleBridge.Prepare(g, g.PendingEngagement, BattleBridge.AutoSelect(g, g.GetFleet(SliceScenario.PlayerFleet)), out var err);
            Assert.IsNotNull(setup, err);
            var session = BattleSession.Begin(setup, out err);
            Assert.IsNotNull(session, err);
            return session;
        }

        static void Play(BattleSession b, int commands, int seed)
        {
            var ai = new[] { new RosterAI(AIDifficulty.Tactical, seed), new RosterAI(AIDifficulty.Tactical, seed + 1) };
            for (int i = 0; i < commands && !b.State.IsOver; i++)
            {
                var c = ai[b.State.ActiveSeat].ChooseAction(b.State);
                var r = b.Submit(c, c.Seat);
                Assert.IsTrue(r.Accepted, r.Reason);
            }
        }

        [Test]
        public void RulesVersion_NamesEveryInput()
        {
            StringAssert.Contains("commandfall-0.15.0", RulesVersion.Id);
            StringAssert.Contains(GameContent.ContentVersion, RulesVersion.Id);
            StringAssert.Contains(RosterContent.ContentVersion, RulesVersion.Id);
            StringAssert.Contains("+vs-", RulesVersion.Id);
            Assert.AreEqual(16, RulesVersion.ContentFingerprint.Length);
        }

        [Test]
        public void AFullBattle_ReplaysFromItsRecord_ToTheSameAuthoritativeState()
        {
            var b = Begin(Contact());
            Play(b, 4000, 7);
            Assert.IsTrue(b.State.IsOver);
            Assert.AreEqual(b.Log.Count, b.Hashes.Count);
            var snap = b.Snapshot();
            Assert.IsNull(BattleSession.FindDivergence(snap, out var replayed));
            Assert.AreEqual(b.Hash, Serialization.StateHash(replayed.State));
            Assert.AreEqual(b.State.Winner, replayed.State.Winner);
            Assert.AreEqual(b.Log.Count + 1, b.DescribeLog().Count());
        }

        [Test]
        public void TheSameStartAndCommands_GiveTheSameBattle_InTwoIndependentGames()
        {
            var b1 = Begin(Contact()); Play(b1, 4000, 7);
            var b2 = Begin(Contact());
            foreach (var c in b1.Log) Assert.IsTrue(b2.Submit(c, BattleSession.Host).Accepted);
            CollectionAssert.AreEqual(b1.Hashes, b2.Hashes, "identical after every single command");
        }

        [Test]
        public void Divergence_NamesTheFirstDifferingCommand()
        {
            var b = Begin(Contact());
            Play(b, 40, 7);
            var snap = b.Snapshot();
            snap.Hashes[17] ^= 1;
            var d = BattleSession.FindDivergence(snap, out _);
            Assert.IsNotNull(d);
            Assert.AreEqual(17, d.Index);
            Assert.AreEqual("different result", d.Kind);

            var forged = b.Snapshot();
            forged.Log[5] = Command.Move(forged.Log[5].Seat, forged.Log[5].UnitId, new Hex(40, 40));
            d = BattleSession.FindDivergence(forged, out _);
            Assert.AreEqual(5, d.Index);
            Assert.AreEqual("rejected on replay", d.Kind);

            var moved = b.Snapshot();
            moved.Setup = CloneSetup(moved.Setup);
            moved.Setup.StartHp[0][0] = 1;
            d = BattleSession.FindDivergence(moved, out _);
            Assert.AreEqual(-1, d.Index, "a changed setup is caught before the first command");
        }

        /// <summary>A deep copy through the save format itself (JSON text and back).</summary>
        static BattleSetup CloneSetup(BattleSetup s) => RoundTrip(new BattleSnapshot { RulesVersionId = RulesVersion.Id, ContentFingerprint = RulesVersion.ContentFingerprint, Setup = s }).Setup;

        static BattleSnapshot RoundTrip(BattleSnapshot b)
        {
            var back = BattleSession.FromData((IDictionary<string, object>)Json.Parse(Json.Write(BattleSession.ToData(b))), out var err);
            Assert.IsNotNull(back, err);
            return back;
        }

        [Test]
        public void MidBattleSave_ReloadsTheSameBattle_AndPreviewsStillEqualResults()
        {
            var g = Contact();
            var b = Begin(g);
            Play(b, 30, 11);
            g.ActiveBattle = b.Snapshot();
            var json = SaveGame.ToJson(g);
            var back = SaveGame.FromJson(json, out var err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(SaveGame.Digest(g), SaveGame.Digest(back), "the battle record survives the round trip");
            var restored = BattleSession.Restore(back.ActiveBattle, out err);
            Assert.IsNotNull(restored, err);
            Assert.AreEqual(b.Hash, restored.Hash);
            CollectionAssert.AreEqual(b.Setup.ShipUids[0], restored.Setup.ShipUids[0], "the same ships, not a reroll");

            // Every legal command previews identically in both, and each preview equals the committed result.
            var legal = LegalActions.EnumerateRoster(restored.State);
            Assert.IsNotEmpty(legal);
            foreach (var c in legal.Take(25))
            {
                var p1 = b.Preview(c); var p2 = restored.Preview(c);
                Assert.AreEqual(p1.Accepted, p2.Accepted, c.ToString());
                Assert.AreEqual(p1.HashAfter, p2.HashAfter, c.ToString());
            }
            Assert.AreEqual(b.Hash, Serialization.StateHash(b.State), "previews never touch the live state");
            var attack = legal.FirstOrDefault(c => c.Type == CommandType.Attack) ?? legal[0];
            var preview = restored.Preview(attack);
            HitResult basic = attack.Type == CommandType.Attack ? RosterCombat.ResolveBasic(restored.State, restored.State.Unit(attack.UnitId), attack.Target).Roster : null;
            Assert.IsTrue(restored.Submit(attack, attack.Seat).Accepted);
            Assert.AreEqual(preview.HashAfter, restored.Hash, "the previewed state is the committed state");
            if (basic != null && !basic.Lethal) Assert.AreEqual(basic.HpAfter, restored.State.Unit(basic.TargetId)?.Hp ?? 0, "the attack preview's hull is the committed hull");

            // Both continue identically to the end.
            Assert.IsTrue(b.Submit(attack, attack.Seat).Accepted);
            Play(b, 4000, 23); Play(restored, 4000, 23);
            Assert.AreEqual(b.Hash, restored.Hash);
        }

        [Test]
        public void StoredDeployment_IsReproduced_SoAReloadCannotRedeploy()
        {
            var g = Contact();
            var auto = Begin(g);
            var setup = CloneSetup(auto.Setup);
            var mine = auto.State.Units.Where(u => u.Owner == 0).OrderBy(u => u.Id).Select(u => u.Position).ToList();
            mine.Reverse();
            setup.Deployment[0] = mine;
            var b = BattleSession.Begin(setup, out var err);
            Assert.IsNotNull(b, err);
            CollectionAssert.AreEqual(mine, b.State.Units.Where(u => u.Owner == 0).OrderBy(u => u.Id).Select(u => u.Position).ToList());
            var restored = BattleSession.Restore(RoundTrip(b.Snapshot()), out err);
            Assert.IsNotNull(restored, err);
            Assert.AreEqual(b.InitialHash, restored.InitialHash);
        }

        [Test]
        public void Stale_Duplicate_Unauthorized_AndIllegalCommands_AreRejected_WithoutCorruptingTheBattle()
        {
            var b = Begin(Contact());
            int seat = b.State.ActiveSeat;
            var legal = LegalActions.EnumerateRoster(b.State).First(c => c.Type != CommandType.Pass);
            void Refused(Command c, int controller, string because)
            {
                ulong h = b.Hash; int n = b.Log.Count;
                var r = b.Submit(c, controller);
                Assert.IsFalse(r.Accepted, because);
                Assert.IsNotEmpty(r.Reason);
                Assert.AreEqual(h, b.Hash); Assert.AreEqual(h, Serialization.StateHash(b.State)); Assert.AreEqual(n, b.Log.Count);
            }
            Refused(legal, 1 - seat, "the other side cannot issue this side's orders");
            var wrongSeat = legal.Clone(); wrongSeat.Seat = 1 - seat;
            Refused(wrongSeat, 1 - seat, "it is not that side's turn");
            Refused(Command.Forfeit(seat), seat, "only the host forfeits a side");
            var stale = legal.Clone(); stale.ExpectedRevision = b.State.Revision - 1;
            Refused(stale, seat, "stale revision");
            Refused(Command.Move(seat, legal.UnitId, new Hex(40, 40)), seat, "off the battlefield");
            Refused(Command.Attack(seat, b.State.Units.First(u => u.Owner != seat).Id, new Hex(0, 0)), seat, "an enemy unit");
            Refused(null, seat, "empty");

            var first = legal.Clone(); first.Nonce = 4242; first.ExpectedRevision = b.State.Revision;
            Assert.IsTrue(b.Submit(first, seat).Accepted, "a fresh revision and a new nonce are accepted");
            var again = first.Clone(); again.ExpectedRevision = -1;
            Refused(again, seat, "the same delivery twice");
            Play(b, 3, 5);
            var reused = LegalActions.EnumerateRoster(b.State).First().Clone(); reused.Nonce = 4242;
            Refused(reused, reused.Seat, "a reused nonce");
            Assert.IsNull(BattleSession.FindDivergence(b.Snapshot(), out _), "the record is still exact");
        }

        [Test]
        public void Restore_RefusesOtherRules_AndDamagedOrOrphanedRecords()
        {
            var g = Contact();
            var b = Begin(g);
            Play(b, 10, 3);
            var other = b.Snapshot(); other.RulesVersionId = "commandfall-0.14.0+old";
            Assert.IsNull(BattleSession.Restore(other, out var err)); StringAssert.Contains("rules", err);
            var content = b.Snapshot(); content.ContentFingerprint = "0000000000000000";
            Assert.IsNull(BattleSession.Restore(content, out err)); StringAssert.Contains("definitions", err);

            var data = BattleSession.ToData(b.Snapshot());
            data["config"] = "not base64!";
            Assert.IsNull(BattleSession.FromData(data, out err)); StringAssert.Contains("damaged", err);

            g.ActiveBattle = b.Snapshot();
            g.GetEngagement(b.Setup.EngagementId).State = EngagementState.Resolved;
            Assert.IsNull(SaveGame.FromJson(SaveGame.ToJson(g), out err), "a battle whose engagement was resolved cannot come back");
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void ApplyingTheResult_ClearsTheBattleInProgress_Once()
        {
            var g = Contact();
            var b = Begin(g);
            Play(b, 4000, 7);
            g.ActiveBattle = b.Snapshot();
            Assert.IsTrue(BattleBridge.Apply(g, b.Setup, BattleBridge.Extract(b.Setup, b.State)).Ok);
            Assert.IsNull(g.ActiveBattle);
            Assert.IsFalse(BattleBridge.Apply(g, b.Setup, BattleBridge.Extract(b.Setup, b.State)).Ok);
        }
    }
}
