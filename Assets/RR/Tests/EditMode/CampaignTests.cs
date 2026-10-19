using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    static class Camp
    {
        public static GalaxyState New(FactionId f = FactionId.Pygmaei) => SliceCampaign.Create(f);
        public static Fleet Mine(GalaxyState g) => g.Fleets.First(f => f.OwnerId == g.Player.Id);
        public static Fleet Theirs(GalaxyState g) => g.Fleets.First(f => f.OwnerId != g.Player.Id);
        public static int Flagship(Fleet f) => f.Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Uid;

        public static GalaxyState Reload(GalaxyState g)
        {
            var back = SaveGame.FromJson(SaveGame.ToJson(g), out var err);
            Assert.IsNotNull(back, err);
            Assert.AreEqual(SaveGame.Digest(g), SaveGame.Digest(back), "a save reloads exactly");
            return back;
        }

        /// <summary>Declare the passage objective, send the player's fleet, and run days until the engagement.</summary>
        public static Engagement ToContact(GalaxyState g)
        {
            Assert.IsTrue(SliceCampaign.DeclareObjective(g, g.Player.Id, SliceCampaign.PassageSystem).Ok);
            Assert.IsTrue(StrategyRules.OrderMove(g, Mine(g).Id, SliceCampaign.PassageSystem).Ok);
            for (int i = 0; i < 80 && g.PendingEngagement == null; i++) StrategyRules.AdvanceDay(g);
            Assert.IsNotNull(g.PendingEngagement, "the fleets meet at the passage");
            Assert.AreEqual(SliceCampaign.PassageSystem, g.PendingEngagement.SystemId);
            return g.PendingEngagement;
        }

        public static (BattleSetup setup, BattleSession b) Begin(GalaxyState g)
        {
            var e = g.PendingEngagement;
            var setup = BattleBridge.Prepare(g, e, BattleBridge.AutoSelect(g, Mine(g)), out var err);
            Assert.IsNotNull(setup, err);
            var b = BattleSession.Begin(setup, out err);
            Assert.IsNotNull(b, err);
            return (setup, b);
        }

        public static void Play(BattleSession b, Func<MatchState, Command> me, Func<MatchState, Command> them, int max = 600)
        {
            for (int i = 0; i < max && !b.State.IsOver; i++)
            {
                var c = (b.State.ActiveSeat == b.Setup.PlayerSeat ? me : them)(b.State);
                var r = b.Submit(c, BattleSession.Host);
                Assert.IsTrue(r.Accepted, r.Reason);
            }
            Assert.IsTrue(b.State.IsOver);
        }

        public static Command Idle(MatchState s) => s.ActiveUnitId != 0 ? Command.Wait(s.ActiveSeat, s.ActiveUnitId)
            : Command.Wait(s.ActiveSeat, s.Units.First(u => u.Owner == s.ActiveSeat && RosterRules.Eligible(s, u)).Id);

        public static void NoDuplicateShips(GalaxyState g)
        {
            var uids = g.Fleets.SelectMany(f => f.Ships).Select(s => s.Uid).ToList();
            Assert.AreEqual(uids.Count, uids.Distinct().Count(), "no ship is in two inventories");
        }
    }

    /// <summary>Prompt 6: the passage campaign loop.</summary>
    public sealed class PassageCampaignTests
    {
        [Test]
        public void TheCampaign_StartsFromTheExistingGalaxy_WithAContestedPassage()
        {
            var g = Camp.New();
            Assert.AreEqual(SliceCampaign.ScenarioId, g.ScenarioId);
            Assert.AreEqual("pygmaei", g.Player.Id);
            Assert.AreEqual(44, g.Systems.Count, "the existing galaxy, not a new one");
            Assert.AreEqual("trade_passage", g.GetSystem("umbra").BattlefieldMapId);
            Assert.IsNull(g.Slice.Passage("umbra").AccessHolderId, "contested");
            Assert.AreEqual("tension", g.Slice.RelationOf("pygmaei", "inhumanus").Status);
            Assert.IsTrue(g.GetSystem("forgefall").Planets.Any(p => p.HasShipyard && p.OwnerId == "pygmaei"));
            Assert.AreEqual(1, g.Slice.Expeditions.Count);
            Assert.IsNotNull(g.Slice.Profile(Camp.Flagship(Camp.Mine(g))).Name, "the flagship has an identity from the start");
            var back = Camp.Reload(g);
            Assert.AreEqual(g.Slice.Expeditions[0].MapId, back.Slice.Expeditions[0].MapId);
        }

        [Test]
        public void Success_GalaxyToBattleToGalaxy_SecuresTheObjectiveOnce_AndSavesAtEveryStage()
        {
            var g = Camp.New();
            var e = Camp.ToContact(g);
            Assert.AreEqual("war", g.Slice.RelationOf("pygmaei", "inhumanus").Status);
            var preview = SliceCampaign.PreviewEncounter(g, e);
            Assert.IsTrue(preview.Any(l => l.Contains("not ownership")), string.Join(" | ", preview));
            g = Camp.Reload(g);                                                     // before commitment
            var (setup, b) = Camp.Begin(g);
            Assert.IsTrue(setup.Config.Players.All(p => p.Army.Units.Where(u => ShipCatalog.IsLeader(u.UnitId)).All(u => u.Equipment.Contains(SliceRules.CapturePolicyToken))), "the declared capture policy");
            var ai = new SliceAI();
            for (int i = 0; i < 12; i++) b.Submit(ai.Choose(b.State), BattleSession.Host);
            g.ActiveBattle = b.Snapshot();
            var mid = Camp.Reload(g);                                               // during battle
            var resumed = BattleSession.Restore(mid.ActiveBattle, out var err);
            Assert.IsNotNull(resumed, err);
            Assert.AreEqual(b.Hash, resumed.Hash);
            g.ActiveBattle = null;
            Camp.Play(b, ai.Choose, Pilots.Blunder);
            var o = BattleBridge.Extract(setup, b);
            int creditsBefore = g.Player.Stock.Credits;
            var enemyBefore = Camp.Theirs(g).Ships.Count;
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            Assert.IsFalse(BattleBridge.Apply(g, setup, o).Ok, "applied once");
            Assert.AreEqual(0, o.WinnerSeat);
            Assert.AreEqual("pygmaei", g.Slice.Passage("umbra").AccessHolderId);
            Assert.AreEqual(ObjectiveStatus.Secured, g.Slice.Objectives.First(x => x.PolityId == "pygmaei").Status);
            Assert.AreEqual(creditsBefore + SliceCampaign.VictoryPrize, g.Player.Stock.Credits);
            var rival = g.Fleets.FirstOrDefault(f => f.OwnerId == "inhumanus");
            Assert.IsNotNull(rival, "surviving enemy ships withdraw; they are not deleted");
            Assert.AreEqual(enemyBefore - o.Ships[1].Count(x => x.Destroyed || x.Captured), rival.Ships.Count);
            Assert.IsTrue(rival.Moving || rival.SystemId != "umbra");
            Assert.IsNull(g.GetSystem("umbra").OwnerId, "an orbital battle conquers nothing");
            Camp.NoDuplicateShips(g);
            var after = Camp.Reload(g);                                             // after results
            Assert.IsFalse(BattleBridge.Apply(after, setup, o).Ok, "still once after a reload");
            StrategyRules.AdvanceDay(after);
            Assert.GreaterOrEqual(after.Player.Stock.Credits, g.Player.Stock.Credits + 3, "the passage toll is paid daily");
        }

        [Test]
        public void Failure_ReturnsToACoherentCampaign_WithABoundedRecoveryRoute()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            Camp.Play(b, Camp.Idle, new SliceAI().Choose);
            var o = BattleBridge.Extract(setup, b);
            Assert.AreEqual(1, o.WinnerSeat);
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            var obj = g.Slice.Objectives.First(x => x.PolityId == "pygmaei");
            Assert.AreEqual(ObjectiveStatus.Lost, obj.Status);
            Assert.AreEqual(1, obj.Attempts);
            Assert.AreEqual("inhumanus", g.Slice.Passage("umbra").AccessHolderId);
            Assert.IsNull(g.PendingEngagement, "no softlock: nothing waits for a decision");
            var mine = g.Fleets.FirstOrDefault(f => f.OwnerId == "pygmaei");
            if (mine == null || !StrategyRules.CanFight(mine))
            {
                // The flagship was lost: the declared recovery rule offers a replacement at the shipyard.
                var yard = g.GetSystem("forgefall").Planets.First(p => p.HasShipyard);
                var opt = StrategyRules.ProjectOptions(g, yard.Id).First(x => ShipCatalog.IsLeader(x.ShipDefId));
                Assert.IsTrue(opt.Available, opt.UnavailableReason);
            }
            var back = Camp.Reload(g);
            Assert.IsTrue(SliceCampaign.DeclareObjective(back, "pygmaei", "umbra").Ok, "the objective can be tried again");
            back.Slice.Objectives.ForEach(x => x.Attempts = SliceCampaign.MaxAttempts);
            back.Slice.Objectives.Where(x => x.Status == ObjectiveStatus.Declared).ToList().ForEach(x => x.Status = ObjectiveStatus.Lost);
            Assert.IsFalse(SliceCampaign.DeclareObjective(back, "pygmaei", "umbra").Ok, "but not without limit");
        }

        [Test]
        public void Retreat_IsALegalOutcome_ThatLeavesThePassageContested()
        {
            var g = Camp.New();
            var e = Camp.ToContact(g);
            Assert.IsTrue(StrategyRules.Retreat(g, e.Id).Ok);
            Assert.IsNull(g.Slice.Passage("umbra").AccessHolderId);
            Assert.AreEqual(EngagementState.Retreated, e.State);
            Camp.Reload(g);
        }
    }

    /// <summary>Prompt 7: persistent capital ships.</summary>
    public sealed class CapitalShipTests
    {
        [Test]
        public void RenameRefitFightReturnRepairSaveAndDeployAgain_TheSameShip()
        {
            var g = Camp.New();
            int uid = Camp.Flagship(Camp.Mine(g));
            Assert.IsTrue(SliceCampaign.Rename(g, uid, "Cinder Matriarch").Ok);
            Assert.IsFalse(SliceCampaign.Rename(g, uid, "   ").Ok);
            Assert.IsFalse(SliceCampaign.Rename(g, uid, new string('x', 40)).Ok);
            var opts = SliceCampaign.RefitOptions(g, uid);
            Assert.AreEqual(3, opts.Count);
            Assert.IsTrue(SliceCampaign.ApplyRefit(g, uid, "heavy_battery").Ok, opts.First(o => o.Module.Id == "heavy_battery").Blocked);
            Assert.IsFalse(SliceCampaign.ApplyRefit(g, uid, "heavy_battery").Ok, "already fitted");
            Assert.IsFalse(SliceCampaign.ApplyRefit(g, uid, "warp_cannon").Ok, "unknown module");
            Camp.ToContact(g);
            Assert.IsFalse(SliceCampaign.ApplyRefit(g, uid, "afterburners").Ok, "not away from a shipyard");
            var (setup, b) = Camp.Begin(g);
            var flag = b.State.Commander(0);
            Assert.AreEqual(RosterContent.Unit("pygmaei_revenant_command_ship").Weapon.Damage + 1, flag.Weapon.Damage, "the battle uses the fitted ship");
            Assert.AreEqual(uid, BattleBridge.ShipUidFor(setup, b.State, flag.Id), "the same identity");
            Camp.Play(b, new SliceAI().Choose, Pilots.Blunder);
            var o = BattleBridge.Extract(setup, b);
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            var p = g.Slice.Profile(uid);
            Assert.IsFalse(p.Destroyed);
            Assert.IsTrue(p.Service.Any(x => x.Kind == "battle" && x.BattleId == setup.BattleId), "history from the resolved battle");
            Assert.IsTrue(p.Service.Any(x => x.Kind == "refit"));
            Assert.IsTrue(p.Service.Any(x => x.Kind == "renamed"));
            // Home to the shipyard and repair.
            Assert.IsTrue(StrategyRules.OrderMove(g, Camp.Mine(g).Id, "forgefall").Ok || Camp.Mine(g).Moving);
            for (int i = 0; i < 60 && (Camp.Mine(g).Moving || Camp.Mine(g).SystemId != "forgefall"); i++) StrategyRules.AdvanceDay(g);
            var ship = SliceCampaign.Ship(g, uid);
            if (ship.Hp < SliceCampaign.MaxHull(g, ship))
            {
                g.Player.Stock = g.Player.Stock + new Resources(0, 500, 500, 0);
                Assert.IsTrue(SliceCampaign.Repair(g, uid).Ok);
                Assert.AreEqual(SliceCampaign.MaxHull(g, ship), ship.Hp);
            }
            var back = Camp.Reload(g);
            Assert.AreEqual("Cinder Matriarch", back.Slice.Profile(uid).Name);
            CollectionAssert.AreEqual(new[] { "heavy_battery" }, back.Slice.Profile(uid).Modules);
            Assert.AreEqual(uid, Camp.Flagship(Camp.Mine(back)), "its id is stable");
        }

        [Test]
        public void IllegalAndUnaffordableRefits_FailCleanly()
        {
            var g = Camp.New();
            int uid = Camp.Flagship(Camp.Mine(g));
            g.Player.Stock = new Resources(0, 0, 0, 0);
            ulong before = SaveGame.Digest(g);
            var r = SliceCampaign.ApplyRefit(g, uid, "ablative_plating");
            Assert.IsFalse(r.Ok); StringAssert.Contains("Not enough", r.Reason);
            Assert.AreEqual(before, SaveGame.Digest(g), "nothing changed");
            int escort = Camp.Mine(g).Ships.First(s => !ShipCatalog.IsLeader(s.DefId)).Uid;
            Assert.IsEmpty(SliceCampaign.RefitOptions(g, escort), "escorts stay simple");
            var army = new ArmyRoster { Faction = FactionId.Pygmaei };
            var e = new RosterEntry("pygmaei_skirmish_frigate"); e.Equipment.Add(SliceRules.ModuleToken + "afterburners"); army.Units.Add(e);
            army.Units.Add(new RosterEntry("pygmaei_revenant_command_ship"));
            Assert.IsFalse(RosterArmy.Validate(army, BattleBridge.Caps, BattleDomain.Orbital).Valid, "the rules refuse a module on an escort");
        }

        [Test]
        public void Destruction_KeepsTheRecord_DoesNotDuplicateModules_AndAReplacementIsANewShip()
        {
            var g = Camp.New();
            int uid = Camp.Flagship(Camp.Mine(g));
            Assert.IsTrue(SliceCampaign.ApplyRefit(g, uid, "afterburners").Ok);
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            Camp.Play(b, Camp.Idle, new SliceAI().Choose);
            var o = BattleBridge.Extract(setup, b);
            Assume.That(o.Ships[0].First(x => x.Uid == uid).Destroyed, "the flagship fell in this line");
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            var p = g.Slice.Profile(uid);
            Assert.IsTrue(p.Destroyed);
            Assert.IsTrue(p.Service.Any(x => x.Kind == "destroyed"));
            Assert.IsNull(SliceCampaign.Ship(g, uid), "it is not resurrected in any fleet");
            var wrecks = g.Slice.Wrecks.Where(w => w.Uid == uid).ToList();
            Assert.AreEqual(1, wrecks.Count);
            Assert.AreEqual("afterburners", wrecks[0].Module, "its module is in the wreck, once");
            Assert.AreEqual(1, g.Slice.Ships.Values.Count(x => x.Modules.Contains("afterburners")), "not duplicated into another ship");
            var yard = g.GetSystem("forgefall").Planets.First(x => x.HasShipyard);
            g.Player.Stock = g.Player.Stock + new Resources(500, 500, 500, 0);
            Assert.IsTrue(StrategyRules.StartProject(g, yard.Id, ProjectKind.CommissionShip, "pygmaei_revenant_command_ship").Ok);
            for (int i = 0; i < 25; i++) StrategyRules.AdvanceDay(g);
            var replacement = g.Fleets.SelectMany(f => f.Ships).First(s => ShipCatalog.IsLeader(s.DefId) && g.Slice.Profile(s.Uid)?.OwnerId == "pygmaei");
            Assert.AreNotEqual(uid, replacement.Uid, "a new identity");
            StringAssert.Contains("replace", g.Slice.Profile(replacement.Uid).Service[0].Text);
            Assert.IsEmpty(g.Slice.Profile(replacement.Uid).Modules);
            Camp.Reload(g);
        }
    }

    /// <summary>Prompt 8: negotiation and capture.</summary>
    public sealed class NegotiationTests
    {
        static Offer Concede(int conceder, int reparations = 0, bool surrender = false) =>
            new Offer { Id = "offer_test_" + conceder + "_" + reparations + "_" + surrender, FromSeat = 0, ConcederSeat = conceder, Reparations = reparations, SurrenderFlagship = surrender };

        [Test]
        public void AnAcceptedSettlement_ChangesTheCampaign_Atomically_AndOnce()
        {
            var g = Camp.New();
            var e = Camp.ToContact(g);
            var o = Concede(0, reparations: 20);                     // the player yields and pays
            int pCredits = g.Player.Stock.Credits, iCredits = g.GetPolity("inhumanus").Stock.Credits;
            var r = SliceCampaign.SettleBeforeBattle(g, e.Id, o);
            Assert.IsTrue(r.Ok, r.Reason);
            Assert.AreEqual("inhumanus", g.Slice.Passage("umbra").AccessHolderId);
            Assert.AreEqual(pCredits - 20, g.Player.Stock.Credits);
            Assert.AreEqual(iCredits + 20, g.GetPolity("inhumanus").Stock.Credits);
            Assert.AreEqual("truce", g.Slice.RelationOf("pygmaei", "inhumanus").Status);
            Assert.AreEqual(1, g.Slice.Settlements.Count);
            Assert.IsEmpty(g.Slice.Wrecks, "no one died");
            Assert.IsFalse(SliceCampaign.SettleBeforeBattle(g, e.Id, o).Ok, "repeated confirmation does nothing");
            Assert.AreEqual(pCredits - 20, g.Player.Stock.Credits, "no repeat payment");
            Camp.NoDuplicateShips(g);
            Camp.Reload(g);
        }

        [Test]
        public void ImpossibleOrStaleTerms_AreRefused()
        {
            var g = Camp.New();
            var e = Camp.ToContact(g);
            g.Player.Stock = new Resources(0, 0, 5, 0);
            var poor = SliceCampaign.SettleBeforeBattle(g, e.Id, Concede(0, reparations: 50));
            Assert.IsFalse(poor.Ok); StringAssert.Contains("cannot pay", poor.Reason);
            Assert.IsFalse(SliceCampaign.SettleBeforeBattle(g, e.Id, Concede(0, reparations: 500)).Ok, "beyond the bound");
            var (setup, b) = Camp.Begin(g);
            var stale = Concede(1); stale.Revision = b.State.Revision - 1;
            StringAssert.Contains("earlier position", Negotiation.Validate(g, setup, b.State, stale));
            var dead = Concede(1, surrender: true); dead.Revision = b.State.Revision;
            b.State.Commander(1).Alive = false;
            StringAssert.Contains("no longer exists", Negotiation.Validate(g, setup, b.State, dead), "invalid custody");
        }

        [Test]
        public void ARejectedOffer_IsFollowedByCombat_AndTheVerdictIsExplained()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            var demand = Concede(1, surrender: true); demand.Revision = b.State.Revision; demand.Round = b.State.Round;
            var v = Negotiation.Decide(g, setup, b.State, demand, decidingSeat: 1);
            Assert.IsTrue(v.Valid);
            Assert.IsFalse(v.Accepted, v.Reason);
            StringAssert.Contains("pressure", v.Reason);
            Assert.IsTrue(v.Factors.Any(f => f.Contains("flagship in their custody")));
            // Combat continues on the same record.
            var before = b.Hash;
            Assert.IsTrue(b.Submit(new SliceAI().Choose(b.State), BattleSession.Host).Accepted);
            Assert.AreNotEqual(before, b.Hash);
        }

        [Test]
        public void UnderPressure_TheAIConcedes_AndTheBattleEndsOnTheTerms()
        {
            var g = Camp.New();
            Camp.ToContact(g);
            var (setup, b) = Camp.Begin(g);
            // Put the enemy flagship in a lethal position by playing until our side has a lethal line, without taking it.
            var ai = new SliceAI();
            for (int i = 0; i < 200 && !b.State.IsOver; i++)
            {
                if (b.State.ActiveSeat == 0 && b.State.ActiveUnitId == 0 && CommandThreat.Assess(b.State, 1).Lines.Count > 0) break;
                b.Submit(b.State.ActiveSeat == 0 ? ai.Choose(b.State) : Pilots.Blunder(b.State), BattleSession.Host);
            }
            Assume.That(!b.State.IsOver && b.State.ActiveSeat == 0, "reached a position with leverage");
            var demand = Concede(1, reparations: 0); demand.Revision = b.State.Revision; demand.Round = b.State.Round;
            var v = Negotiation.Decide(g, setup, b.State, demand, decidingSeat: 1);
            Assert.IsTrue(v.Accepted, v.Reason);
            Assert.IsTrue(Negotiation.Conclude(b, demand).Accepted);
            var o = BattleBridge.Extract(setup, b, demand);
            Assert.IsTrue(BattleBridge.Apply(g, setup, o).Ok);
            Assert.AreEqual("pygmaei", g.Slice.Passage("umbra").AccessHolderId);
            Assert.AreEqual(ObjectiveStatus.Secured, g.Slice.Objectives.First(x => x.PolityId == "pygmaei").Status);
            Assert.IsTrue(Camp.Theirs(g).Ships.Any(s => ShipCatalog.IsLeader(s.DefId)), "their flagship withdrew intact");
            Assert.IsNull(BattleSession.FindDivergence(b.Snapshot(), out _), "the yield is part of the battle record");
        }

        [Test]
        public void LawfulCapture_NeedsThePolicy_ADisabledCrippledFlagship_AndAnAdjacentBoarder()
        {
            MatchState Scene(bool policy, bool disabled, int hp, int q, int r, out int boarder)
            {
                var f = new OrbitalFixture();
                boarder = f.Add(0, "pygmaei_skirmish_frigate", q, r);
                int flag = f.Add(1, "inhumanus_executive_flagship", 0, -2, hp: hp);
                var s = f.Build();
                if (policy) s.Commander(1).Equipment.Add(SliceRules.CapturePolicyToken);
                if (disabled) s.Commander(1).RStatuses.Add(new RStatus { Type = RStatusType.Disabled, Expire = RExpire.Battle });
                return s;
            }
            var target = new Hex(0, -2);
            int b;
            var s1 = Scene(false, true, 5, 0, -1, out b);
            StringAssert.Contains("not allowed", OrbitalFixture.Rejected(s1, SliceRules.Order(CommandType.Attack, 0, s1.Units.First(u => u.Owner == 0 && !u.R.IsLeaderRole).Id, target, SliceRules.BoardOption)));
            var s2 = Scene(true, false, 5, 0, -1, out b);
            StringAssert.Contains("DISABLED", SliceRules.ValidateBoarding(s2, s2.Units.First(u => u.Owner == 0 && !u.R.IsLeaderRole), target));
            var s3 = Scene(true, true, 12, 0, -1, out b);
            StringAssert.Contains("third", SliceRules.ValidateBoarding(s3, s3.Units.First(u => u.Owner == 0 && !u.R.IsLeaderRole), target));
            var s4 = Scene(true, true, 5, 0, 1, out b);
            StringAssert.Contains("adjacent", SliceRules.ValidateBoarding(s4, s4.Units.First(u => u.Owner == 0 && !u.R.IsLeaderRole), target));
            var s5 = Scene(true, true, 5, 0, -1, out b);
            var boarderUnit = s5.Units.First(u => u.Owner == 0 && !u.R.IsLeaderRole);
            Assert.IsTrue(LegalActions.EnumerateRoster(s5).Any(c => c.Option == SliceRules.BoardOption), "a legal action the AI and threat analysis can see");
            OrbitalFixture.Ok(s5, SliceRules.Order(CommandType.Attack, 0, boarderUnit.Id, target, SliceRules.BoardOption));
            Assert.IsTrue(s5.IsOver);
            Assert.AreEqual(VictoryReason.LeaderCaptured, s5.VictoryReason);
            Assert.IsTrue(s5.Commander(1).Alive && s5.Commander(1).HasR(RStatusType.Captured), "captured, not destroyed");
            Assert.IsTrue(boarderUnit.RStatuses.Any(x => x.Type == RStatusType.Exposed), "boarding has a cost");
        }

        [Test]
        public void EachEnding_ChangesTheCampaignDifferently()
        {
            string Fingerprint(GalaxyState g) => $"holder={g.Slice.Passage("umbra").AccessHolderId};wrecks={g.Slice.Wrecks.Count};settle={g.Slice.Settlements.Count};" +
                $"rel={g.Slice.RelationOf("pygmaei", "inhumanus").Status};inh_flag_with_pyg={g.Fleets.Where(f => f.OwnerId == "pygmaei").SelectMany(f => f.Ships).Count(s => s.DefId == "inhumanus_executive_flagship")}";
            // Destruction.
            var gd = Camp.New(); Camp.ToContact(gd);
            var (sd, bd) = Camp.Begin(gd); Camp.Play(bd, new SliceAI().Choose, Pilots.Blunder);
            Assert.IsTrue(BattleBridge.Apply(gd, sd, BattleBridge.Extract(sd, bd)).Ok);
            // Settlement.
            var gs = Camp.New(); var es = Camp.ToContact(gs);
            Assert.IsTrue(SliceCampaign.SettleBeforeBattle(gs, es.Id, Concede(1)).Ok);
            // Capture by surrender.
            var gc = Camp.New(); var ec = Camp.ToContact(gc);
            Assert.IsTrue(SliceCampaign.SettleBeforeBattle(gc, ec.Id, Concede(1, surrender: true)).Ok);
            var prints = new[] { Fingerprint(gd), Fingerprint(gs), Fingerprint(gc) };
            Assert.AreEqual(3, prints.Distinct().Count(), string.Join(" | ", prints));
            Camp.NoDuplicateShips(gc);
            Assert.IsTrue(gc.Slice.Ships.Values.Any(p => p.Captured && p.OwnerId == "pygmaei"), "custody changed hands");
            Assert.IsEmpty(gc.Slice.Wrecks, "a captured ship leaves no wreck");
        }
    }

    /// <summary>Prompt 9: consequences that persist.</summary>
    public sealed class ConsequenceTests
    {
        [Test]
        public void TwoEndings_FromSeparateSaves_LeaveDifferentHistories_MarkersSalvageAndPolitics()
        {
            var ga = Camp.New(); Camp.ToContact(ga);
            var gb = Camp.Reload(ga);
            // A: the enemy flagship is destroyed.
            var (sa, ba) = Camp.Begin(ga); Camp.Play(ba, new SliceAI().Choose, Pilots.Blunder);
            var oa = BattleBridge.Extract(sa, ba);
            Assume.That(oa.Ships[1].Any(x => x.Destroyed && ShipCatalog.IsLeader(ga.Slice.Profile(x.Uid)?.DefId ?? "")), "their flagship was destroyed");
            Assert.IsTrue(BattleBridge.Apply(ga, sa, oa).Ok);
            // B: they yield by settlement.
            Assert.IsTrue(SliceCampaign.SettleBeforeBattle(gb, gb.PendingEngagement.Id, new Offer { Id = "o_b", ConcederSeat = 1 }).Ok);
            ga = Camp.Reload(ga); gb = Camp.Reload(gb);
            Assert.AreEqual(1, ga.Slice.Wrecks.Count); Assert.AreEqual(0, gb.Slice.Wrecks.Count);
            Assert.AreEqual("trade_passage_wrecked", ga.GetSystem("umbra").BattlefieldMapId, "later battles there are among the wreckage");
            Assert.AreEqual("trade_passage", gb.GetSystem("umbra").BattlefieldMapId);
            Assert.IsTrue(MapLibrary.Get("trade_passage_wrecked").Topology.Passed && MapLibrary.Get("trade_passage_wrecked").Topology.DeadEnds.Count <= 2);
            Assert.Less(ga.Slice.RelationOf("pygmaei", "inhumanus").Opinion(ga.Day), 0, "destroying a named flagship costs standing");
            Assert.Greater(gb.Slice.RelationOf("pygmaei", "inhumanus").Opinion(gb.Day), 0, "an honoured settlement earns some");
            Assert.IsTrue(ga.Slice.History.Any(h => h.Kind == "loss") && !gb.Slice.History.Any(h => h.Kind == "loss"));
            Assert.IsTrue(gb.Slice.History.Any(h => h.Kind == "settlement") && !ga.Slice.History.Any(h => h.Kind == "settlement"));
            Assert.AreEqual("pygmaei", ga.Slice.Passage("umbra").AccessHolderId);
            Assert.AreEqual("pygmaei", gb.Slice.Passage("umbra").AccessHolderId);
            Assert.IsNull(ga.GetSystem("umbra").OwnerId, "no conquest decoration where only access changed");
            foreach (var h in ga.Slice.History) Assert.IsNotEmpty(h.Text);
        }

        [Test]
        public void Salvage_IsFinite_ShownBeforehand_AndStaysExhaustedAfterReload()
        {
            var g = Camp.New(); Camp.ToContact(g);
            var (s, b) = Camp.Begin(g); Camp.Play(b, new SliceAI().Choose, Pilots.Blunder);
            var o = BattleBridge.Extract(s, b);
            Assume.That(o.Ships[1].Any(x => x.Destroyed && g.Slice.Profile(x.Uid) != null));
            Assert.IsTrue(BattleBridge.Apply(g, s, o).Ok);
            var w = g.Slice.Wrecks.Single();
            StringAssert.Contains("once", SliceCampaign.SalvagePreview(g, w.Id));
            var fleet = Camp.Mine(g);
            Assume.That(fleet.SystemId == "umbra" && !fleet.Moving);
            int m = g.Player.Stock.Materials;
            Assert.IsTrue(SliceCampaign.Salvage(g, w.Id, fleet.Id).Ok);
            Assert.AreEqual(m + w.Materials.Materials, g.Player.Stock.Materials);
            Assert.IsFalse(SliceCampaign.Salvage(g, w.Id, fleet.Id).Ok, "no infinite salvage");
            var back = Camp.Reload(g);
            Assert.IsFalse(SliceCampaign.Salvage(back, w.Id, Camp.Mine(back).Id).Ok, "still exhausted after a reload");
        }

        [Test]
        public void Reactions_ApplyOnce_AndAreBounded()
        {
            var g = Camp.New();
            Assert.IsTrue(SliceCampaign.React(g, "k1", "pygmaei", "inhumanus", -20, "destroyed a named flagship"));
            Assert.IsFalse(SliceCampaign.React(g, "k1", "pygmaei", "inhumanus", -20, "destroyed a named flagship"), "the same event never applies twice");
            SliceCampaign.React(g, "k2", "pygmaei", "inhumanus", -20, "destroyed a named flagship");
            SliceCampaign.React(g, "k3", "pygmaei", "inhumanus", -20, "destroyed a named flagship");
            Assert.AreEqual(SliceCampaign.OpinionFloor, g.Slice.RelationOf("pygmaei", "inhumanus").Opinion(g.Day), "one cause cannot stack without limit");
            for (int i = 0; i <= SliceCampaign.ReactionDays; i++) StrategyRules.AdvanceDay(g);
            Assert.AreEqual(0, g.Slice.RelationOf("pygmaei", "inhumanus").Opinion(g.Day), "and it fades");
        }
    }

    /// <summary>Prompt 10: the Eclipse expedition.</summary>
    public sealed class ExpeditionTests
    {
        static GalaxyState AtGloam()
        {
            var g = Camp.New();
            Assert.IsTrue(StrategyRules.OrderMove(g, Camp.Mine(g).Id, Expeditions.System).Ok);
            for (int i = 0; i < 60 && (Camp.Mine(g).Moving || Camp.Mine(g).SystemId != Expeditions.System); i++) StrategyRules.AdvanceDay(g);
            Assert.AreEqual(Expeditions.System, Camp.Mine(g).SystemId);
            return g;
        }

        [Test]
        public void Conditions_AreFixedAtCreation_AndReloadsCannotRerollThem()
        {
            var a = Camp.New(); var b = Camp.New();
            var ea = Expeditions.Of(a); var eb = Expeditions.Of(b);
            Assert.AreEqual(ea.MapId + ea.RivalComposition, eb.MapId + eb.RivalComposition, "deterministic from the galaxy");
            for (int i = 0; i < 5; i++) a = Camp.Reload(a);
            Assert.AreEqual(ea.MapId, Expeditions.Of(a).MapId);
            Assert.AreEqual(ea.RivalComposition, Expeditions.Of(a).RivalComposition);
            Assert.IsTrue(MapLibrary.Get(ea.MapId).Topology.Passed, "a validated drift");
        }

        [Test]
        public void Reconnaissance_ChangesWhatIsKnown_NotTheOdds()
        {
            var g = Camp.New();
            var before = Expeditions.Briefing(g, Expeditions.Of(g));
            Assert.IsTrue(before.Any(l => l.StartsWith("Unknown")));
            Assert.IsFalse(before.Any(l => l.Contains(RosterContent.Unit(Expeditions.RivalArmy(g, Expeditions.Of(g)).Units[1].UnitId).Name)), "the rival is not leaked");
            var map = Expeditions.Of(g).MapId;
            int influence = g.Player.Stock.Influence;
            Assert.IsTrue(Expeditions.Scout(g).Ok);
            Assert.AreEqual(influence - Expeditions.ReconCost.Influence, g.Player.Stock.Influence);
            var after = Expeditions.Briefing(g, Expeditions.Of(g));
            Assert.IsTrue(after.Any(l => l.StartsWith("Reconnaissance")));
            Assert.AreEqual(map, Expeditions.Of(g).MapId, "scouting reveals; it does not change");
            Assert.IsFalse(Expeditions.Scout(g).Ok, "once");
        }

        [Test]
        public void Withdrawal_HasADeclaredCost_AndTheExpeditionIsOptional()
        {
            var g = Camp.New();
            int m = g.Player.Stock.Materials;
            Assert.IsTrue(Expeditions.Withdraw(g).Ok);
            Assert.AreEqual(m - Expeditions.WithdrawCost.Materials, g.Player.Stock.Materials);
            Assert.IsFalse(Expeditions.Withdraw(g).Ok);
            Assert.AreEqual(ExpeditionState.Withdrawn, Expeditions.Of(g).State);
            Camp.ToContact(g);   // the primary objective is unaffected
            Camp.Reload(g);
        }

        [Test]
        public void TheExpeditionBattle_UsesFog_TheFixedDrift_AndResolvesOnce()
        {
            var g = AtGloam();
            var mine = Camp.Mine(g);
            var setup = Expeditions.Prepare(g, mine, BattleBridge.AutoSelect(g, mine), out var err);
            Assert.IsNotNull(setup, err);
            Assert.IsTrue(setup.Config.FogOfWar, "sensor limits");
            Assert.AreEqual(Expeditions.Of(g).MapId, setup.Config.MapId);
            var b = BattleSession.Begin(setup, out err);
            Assert.IsNotNull(b, err);
            var ai = new SliceAI();
            Camp.Play(b, ai.Choose, ai.Choose);
            var o = BattleBridge.Extract(setup, b);
            var passageBefore = g.Slice.Passage("umbra").AccessHolderId;
            Assert.IsTrue(Expeditions.Resolve(g, setup, o).Ok);
            Assert.IsFalse(Expeditions.Resolve(g, setup, o).Ok, "once");
            var e = Expeditions.Of(g);
            Assert.That(e.State, Is.EqualTo(ExpeditionState.Secured).Or.EqualTo(ExpeditionState.Failed));
            if (e.State == ExpeditionState.Secured) CollectionAssert.Contains(SliceCampaign.SpareModules(g, "pygmaei"), e.Asset);
            Assert.AreEqual(passageBefore, g.Slice.Passage("umbra").AccessHolderId, "the primary campaign is untouched");
            Camp.Reload(g);
        }

        [Test]
        public void UnderFog_TheAIPlansFromWhatItCanSee()
        {
            var g = AtGloam();
            var mine = Camp.Mine(g);
            var setup = Expeditions.Prepare(g, mine, BattleBridge.AutoSelect(g, mine), out var err);
            var b = BattleSession.Begin(setup, out err);
            var s = b.State;
            // Hide-and-seek check: the AI's choice must not change when a ship it cannot see is moved elsewhere.
            s.ActiveSeat = 1;
            // The drift is small, so a ship may start in sight; put a non-leader player ship somewhere the rival cannot see.
            var unseen = s.Map.AllHexes.Where(h => s.UnitAt(h) == null && !RosterTerrain.Blocks(s, h) && !Vision.SeatSeesHex(s, 1, h)).OrderBy(h => h).ToList();
            Assume.That(unseen.Count, Is.GreaterThanOrEqualTo(2), "the drift has at least two empty hexes the rival cannot see");
            var hidden = s.Units.Where(u => u.Owner == 0 && u.OnBoard && !RosterCombat.IsLeader(s, u)).OrderBy(u => u.Id).First();
            hidden.Position = unseen[0];
            unseen = s.Map.AllHexes.Where(h => s.UnitAt(h) == null && !RosterTerrain.Blocks(s, h) && !Vision.SeatSeesHex(s, 1, h)).OrderBy(h => h).ToList();
            Assume.That(Vision.PlanningView(s, 1).Unit(hidden.Id).OnBoard, Is.False, "the ship is out of the rival's sight");
            var c1 = new SliceAI().Choose(s);
            var moved = s.Clone();
            moved.Unit(hidden.Id).Position = unseen.First(h => h != hidden.Position);
            Assume.That(Vision.PlanningView(moved, 1).Unit(hidden.Id).OnBoard, Is.False, "still unseen after the move");
            var c2 = new SliceAI().Choose(moved);
            Assert.AreEqual(c1.ToString(), c2.ToString(), "no knowledge of unseen ships");
        }
    }
}
