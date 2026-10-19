using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tests
{
    /// <summary>Prompt 2: the trade passage battlefield and capital firing arcs.</summary>
    public sealed class PassageBattlefieldTests
    {
        static MapDefinition Passage => MapLibrary.Get("trade_passage");

        [Test]
        public void TradePassage_PassesTheValidator_AsAuthored_WithThreeRoutesAndNoDeadEnds()
        {
            var m = Passage;
            Assert.AreEqual("trade_passage", m.Id);
            Assert.AreEqual(BattleDomain.Orbital, m.Domain);
            Assert.IsTrue(m.Topology.Passed, m.Topology.Describe(m));
            Assert.IsEmpty(m.RepairLog);
            Assert.AreEqual(0, m.Topology.DeadEnds.Count, "no dead ends at all (the cap is two)");
            Assert.AreEqual(3, m.Topology.ViableApproaches(TopologyRules.Default), "west flank, the passage, east flank");
            Assert.IsTrue(m.Topology.CrossLinks.All(c => c >= 2), "routes interconnect across no-man's land");
            Assert.AreEqual(115, m.AllHexes.Count, "the 0.14 board size: already three times the original, not enlarged again");
            Assert.LessOrEqual(m.AllHexes.Count(h => TerrainRules.BlocksMovement(m.Terrain(h))), 7, "limited sight-blocking wrecks");
            CollectionAssert.AreEquivalent(new[] { new Hex(-4, 0), Hex.Zero, new Hex(4, 0) }, RosterMaps.Objectives(m), "the passage and two wing objectives");
        }

        /// <summary>Multi-activation reach of one ship over static terrain, from one start hex.</summary>
        internal static HashSet<Hex> Reach(MapDefinition m, string unitId, Hex start)
        {
            var def = RosterContent.Unit(unitId);
            var cfg = new MatchConfig { ClockSeconds = 0 }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = m.Id; cfg.PointBudget = 999; cfg.MaxUnits = 99;
            for (int seat = 0; seat < 2; seat++)
            {
                var army = new ArmyRoster { Faction = def.Faction };
                if (seat == 0) army.Units.Add(new RosterEntry(unitId));
                army.Units.Add(new RosterEntry(RosterContent.Leaders(def.Faction, BattleDomain.Orbital).First().Id));
                cfg.Players[seat] = new PlayerSetup { Army = army, Caps = new TechCaps(1) { Unlock = 5 } };
            }
            var s = RosterRules.CreateMatch(cfg, out var err, validateArmies: false);
            Assert.IsNotNull(s, err);
            foreach (var x in s.Units) x.Deployed = false;
            var u = s.Units[0];
            u.Deployed = true; s.Phase = MatchPhase.Battle;
            var seen = new HashSet<Hex> { start };
            var q = new Queue<Hex>(); q.Enqueue(start);
            while (q.Count > 0)
            {
                var h = q.Dequeue(); u.Position = h; u.MovedThisActivation = u.ActedThisActivation = false;
                foreach (var d in RosterMovement.Reachable(s, u).Keys) if (seen.Add(d)) q.Enqueue(d);
            }
            return seen;
        }

        [Test]
        public void EverySliceClass_CanReachEveryObjective_FromItsDeploymentZone()
        {
            var m = Passage;
            var deploy = m.AllHexes.Where(h => m.IsDeploymentHex(0, h)).ToList();
            foreach (var id in SliceRoster.Classes(FactionId.Pygmaei).Concat(SliceRoster.Classes(FactionId.Inhumanus)))
                foreach (var obj in RosterMaps.Objectives(m))
                {
                    int from = deploy.Count(h => Reach(m, id, h).Contains(obj));
                    Assert.Greater(from, 0, $"{id} can reach {obj} from some deployment hex");
                    if (RosterContent.Unit(id).Roster.Pattern != MovePattern.Diag)
                        Assert.AreEqual(deploy.Count, from, $"{id} reaches {obj} from every deployment hex");
                }
        }

        [Test]
        public void DiagonalMovers_AreConfinedToOneHexInThree_AndTheRuleSaysSo()
        {
            // An existing codex movement rule, recorded rather than changed: DIAG hops land on one coset of the hex grid.
            var m = Passage;
            var start = m.AllHexes.First(h => m.IsDeploymentHex(0, h));
            var reach = Reach(m, "pygmaei_needle_gunship", start);
            int open = m.AllHexes.Count(h => !TerrainRules.BlocksMovement(m.Terrain(h)));
            Assert.Less(reach.Count, open / 2);
            StringAssert.Contains("one hex in three", SliceRoster.MovementNote("pygmaei_needle_gunship"));
        }

        [Test]
        public void CapitalArc_BlocksShotsAstern_AndTurningCostsTheMove()
        {
            var f = new OrbitalFixture();
            int flag = f.Add(0, "inhumanus_executive_flagship", 0, 0);      // faces north by default
            int behind = f.Add(1, "pygmaei_skirmish_frigate", 0, 3);        // due south, astern
            int ahead = f.Add(1, "pygmaei_furnace_cruiser", 1, -3);         // north
            var s = f.Build();
            var u = s.Unit(f.Id(flag));
            Assert.AreEqual(SliceRules.DefaultFacing(0), SliceRules.Facing(u));
            var astern = RosterCombat.ResolveBasic(s, u, new Hex(0, 3));
            Assert.IsFalse(astern.Legal);
            StringAssert.Contains("astern", astern.Reason);
            Assert.IsTrue(RosterCombat.ResolveBasic(s, u, new Hex(1, -3)).Legal, "the forward arc is open");
            OrbitalFixture.Rejected(s, Command.Attack(0, u.Id, new Hex(0, 3)));
            // The turn order is legal, changes the facing, uses the move, and the ship may still fire.
            var turn = SliceRules.Order(CommandType.Move, 0, u.Id, u.Position + SliceRules.FacingVector(3), SliceRules.TurnOption);
            Assert.IsTrue(LegalActions.EnumerateRoster(s).Any(c => c.Type == CommandType.Move && c.Option == SliceRules.TurnOption && c.Target == turn.Target), "turns are legal actions");
            OrbitalFixture.Ok(s, turn);
            Assert.AreEqual(3, SliceRules.Facing(u));
            Assert.AreEqual(new Hex(0, 0), u.Position, "turning does not move the ship");
            OrbitalFixture.Rejected(s, Command.Move(0, u.Id, new Hex(0, -1)));          // the move is spent
            var shot = RosterCombat.ResolveBasic(s, u, new Hex(0, 3));
            Assert.IsTrue(shot.Legal, shot.Reason);
            OrbitalFixture.Ok(s, Command.Attack(0, u.Id, new Hex(0, 3)));
            Assert.AreEqual(shot.TargetHpAfter, s.Unit(f.Id(behind)).Hp);
            Assert.IsFalse(RosterCombat.ResolveBasic(s, u, new Hex(1, -3), checkAction: false).Legal, "now the north is astern");
            Assert.NotNull(s.Unit(f.Id(ahead)));
        }

        [Test]
        public void CapitalArc_FollowsMovement_AndTheBoundaryIsInclusive()
        {
            var f = new OrbitalFixture();
            int flag = f.Add(0, "pygmaei_revenant_command_ship", 0, 1);     // Ray 2
            f.Add(1, "inhumanus_mercator_corvette", 3, -3);
            var s = f.Build();
            var u = s.Unit(f.Id(flag));
            OrbitalFixture.Ok(s, Command.Move(0, u.Id, new Hex(2, 1)));         // two steps east
            Assert.AreEqual(0, SliceRules.Facing(u), "faces east after moving east");
            // 120° from east: the boundary directions (south-west and north-west) are inside the arc; due west is not.
            Assert.IsTrue(SliceRules.InArc(0, Hex.Zero, Hex.Directions[2] * 3));
            Assert.IsTrue(SliceRules.InArc(0, Hex.Zero, Hex.Directions[4] * 3));
            Assert.IsFalse(SliceRules.InArc(0, Hex.Zero, Hex.Directions[3] * 3));
            Assert.IsFalse(SliceRules.HasArc(s.Unit(s.Players[1].CommanderId) == null ? null : s.Units.First(x => x.DefId == "inhumanus_mercator_corvette")), "escorts have no fixed arc");
        }
    }

    /// <summary>Prompt 3: the command-threat loop. Every fixture uses the ordinary rules; none has a tutorial exception.</summary>
    public sealed class FlagshipProtectionTests
    {
        const string Frigate = "pygmaei_skirmish_frigate";           // Skirmish Guns 4 at 2-3
        const string Flagship = "inhumanus_executive_flagship";       // 20 hull, armour 2

        [Test]
        public void AnExposedFlagship_LosesImmediately_ToALegalLethalStrike()
        {
            var f = new OrbitalFixture();
            int gun = f.Add(0, Frigate, 0, 1);
            int flag = f.Add(1, Flagship, 0, -1, hp: 2);
            f.Add(1, "inhumanus_mercator_corvette", 3, -3); f.Add(1, "inhumanus_proxy_frigate", -3, -1);
            var s = f.Build();
            var lines = AttackExplainer.Explain(s, s.Unit(f.Id(gun)), new Hex(0, -1));
            Assert.IsTrue(lines.Any(l => l.Contains("battle ends")), string.Join(" | ", lines));
            var r = RulesEngine.Apply(s, Command.Attack(0, f.Id(gun), new Hex(0, -1)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.CommanderEliminated, s.VictoryReason);
            Assert.AreEqual(2, s.LiveUnits(1).Count(), "its escorts survive; numbers grant no immunity");
            Assert.IsFalse(RulesEngine.Apply(s, Command.Wait(1, s.LiveUnits(1).First().Id)).Accepted, "no further commands");
        }

        [Test]
        public void InsufficientDamage_DoesNotEndTheBattle_AndThePreviewSaysSo()
        {
            var f = new OrbitalFixture();
            int gun = f.Add(0, Frigate, 0, 1);
            f.Add(1, Flagship, 0, -1, hp: 9);
            var s = f.Build();
            var lines = AttackExplainer.Explain(s, s.Unit(f.Id(gun)), new Hex(0, -1));
            Assert.IsTrue(lines.Any(l => l.Contains("survives")), string.Join(" | ", lines));
            OrbitalFixture.Ok(s, Command.Attack(0, f.Id(gun), new Hex(0, -1)));
            Assert.IsFalse(s.IsOver);
        }

        [Test]
        public void RealCover_BlocksAShot_AndAsteroidsTurnALethalHitIntoASurvivableOne()
        {
            // A wreck on the line of fire: no shot at all.
            var f = new OrbitalFixture();
            int gun = f.Add(0, "pygmaei_annihilator_destroyer", 4, 0);
            f.Add(1, Flagship, 2, 0, hp: 1);
            var s = f.Build();
            Assert.IsFalse(RosterCombat.ResolveBasic(s, s.Unit(f.Id(gun)), new Hex(2, 0)).Legal);
            // Asteroid cover at (0,-2): the same shot that would kill in the open leaves the flagship alive.
            int Dmg(Hex at)
            {
                var g = new OrbitalFixture();
                int a = g.Add(0, "pygmaei_annihilator_destroyer", 0, 1);
                g.Add(1, Flagship, at.Q, at.R);
                var st = g.Build();
                return RosterCombat.ResolveBasic(st, st.Unit(g.Id(a)), at).FinalDamage;
            }
            int open = Dmg(new Hex(3, -2)), covered = Dmg(new Hex(0, -2));
            Assert.AreEqual(open - 1, covered);
            var h = new OrbitalFixture();
            int gun2 = h.Add(0, "pygmaei_annihilator_destroyer", 0, 1);
            h.Add(1, Flagship, 0, -2, hp: open);
            var s2 = h.Build();
            OrbitalFixture.Ok(s2, Command.Attack(0, h.Id(gun2), new Hex(0, -2)));
            Assert.IsFalse(s2.IsOver, "cover saved the flagship by exactly one point");
            Assert.AreEqual(1, s2.Commander(1).Hp);
        }

        static (MatchState s, int[] ids) GuardScene()
        {
            // Pygmaei Rift Interceptor declares Snap Intercept on its flagship; Inhumanus small craft attack it.
            var f = new OrbitalFixture();
            int flag = f.Add(0, "pygmaei_revenant_command_ship", 1, 2, hp: 1);   // open space; one point of hull: any unblocked hit is lethal
            int guard = f.Add(0, "pygmaei_rift_interceptor", -1, 4);
            int fighterA = f.Add(1, "inhumanus_prospect_interceptor", 1, 0);
            int fighterB = f.Add(1, "inhumanus_prospect_interceptor", 2, 0);
            var s = f.Build();
            return (s, new[] { f.Id(flag), f.Id(guard), f.Id(fighterA), f.Id(fighterB) });
        }

        [Test]
        public void AValidGuard_InterceptsOnce_AndAnExhaustedGuardFails()
        {
            var (s, id) = GuardScene();
            var guard = s.Unit(id[1]);
            var use = RosterAbilities.Enumerate(s, guard).FirstOrDefault(u => u.picks.Contains(s.Unit(id[0]).Position));
            Assert.IsNotNull(use.picks, "Snap Intercept can cover the flagship");
            OrbitalFixture.Ok(s, Command.Active(0, guard.Id, use.picks, use.option));
            var protectedBy = s.Unit(id[0]).RStatuses.Where(x => x.Type == RStatusType.Protected || x.Type == RStatusType.Intercepted).ToList();
            Assert.AreEqual(1, protectedBy.Count, "one declared protection, inspectable on the flagship");
            Assert.AreEqual(guard.Id, protectedBy[0].SourceId);
            Assert.AreEqual(1, RosterCombat.InterceptsLeft(s, guard), "one intercept left this round");

            var preview = AttackExplainer.Explain(s, s.Unit(id[2]), s.Unit(id[0]).Position);
            Assert.IsTrue(preview.Any(l => l.Contains("intercepts")), string.Join(" | ", preview));
            var r = RulesEngine.Apply(s, Command.Attack(1, id[2], s.Unit(id[0]).Position));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(r.Events.Any(e => e.Type == GameEventType.Intercepted && e.UnitId == guard.Id));
            Assert.IsFalse(s.IsOver, "the guard took the shot");
            Assert.AreEqual(1, s.Commander(0).Hp);
            Assert.AreEqual(0, RosterCombat.InterceptsLeft(s, guard), "exhausted");

            OrbitalFixture.Ok(s, Command.Wait(0, s.Units.First(x => x.Owner == 0 && RosterRules.Eligible(s, x)).Id));
            var r2 = RulesEngine.Apply(s, Command.Attack(1, id[3], s.Unit(id[0]).Position));
            Assert.IsTrue(r2.Accepted, r2.Reason);
            Assert.IsFalse(r2.Events.Any(e => e.Type == GameEventType.Intercepted), "no second interception, no chaining");
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(1, s.Winner);
        }

        [Test]
        public void ALegalFlank_DefeatsProtectionByPosition_AndTheThreatOverlayFindsIt()
        {
            // The flagship hides behind the wreck at (3,0) from a gun at (4,0)... until the gun moves to a clear line.
            var f = new OrbitalFixture();
            int flag = f.Add(1, Flagship, 2, 0, hp: 3);
            int gun = f.Add(0, Frigate, 4, 0);                                   // Ray 3, guns at 2-3
            f.Add(0, "pygmaei_revenant_command_ship", -4, 4);                    // its own flagship, out of range
            f.Add(1, "inhumanus_mercator_corvette", -3, -1);
            var s = f.Build();
            Assert.IsFalse(RosterCombat.ResolveBasic(s, s.Unit(f.Id(gun)), new Hex(2, 0)).Legal, "screened by the wreck");
            var threat = CommandThreat.Assess(s, 1);
            Assert.IsFalse(threat.LethalNow);
            Assert.IsTrue(threat.LethalAfterMove, threat.Summary("the flagship"));
            var line = threat.Lines.First(l => l.Move != null);
            StringAssert.Contains("can move to", line.Text);
            OrbitalFixture.Ok(s, line.Move);
            OrbitalFixture.Ok(s, line.Action);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
        }

        [Test]
        public void CommandThreat_NeverClaimsSafety_AndNeverTouchesTheBoard()
        {
            var f = new OrbitalFixture();
            f.Add(0, Frigate, -4, 4);
            f.Add(1, Flagship, 3, -4);
            var s = f.Build();
            ulong before = Serialization.StateHash(s);
            var rep = CommandThreat.Assess(s, 1);
            Assert.AreEqual(before, Serialization.StateHash(s));
            Assert.IsEmpty(rep.Lines);
            var text = rep.Summary("Executive Flagship");
            StringAssert.Contains("not a guarantee", text);
            StringAssert.DoesNotContain("safe", text.ToLowerInvariant());
            Assert.Greater(rep.CommandsChecked, 0);
        }

        [Test]
        public void SimultaneousLeaderDestruction_TheActingSideWins_AndSelfDestructionLoses()
        {
            // Saturation Strike: 4 to the chosen hex and 2 to each adjacent hex, allies included (rule C6 decides).
            MatchState Scene(int ownLeaderHp, int enemyLeaderHp, out int aoe)
            {
                var f = new OrbitalFixture();
                int a = f.Add(0, "pygmaei_annihilator_destroyer", 0, 3);
                f.Add(0, "pygmaei_revenant_command_ship", 1, 0, hp: ownLeaderHp);
                f.Add(1, Flagship, 0, 0, hp: enemyLeaderHp);
                var st = f.Build();
                aoe = f.Id(a);
                return st;
            }
            var s = Scene(1, 3, out int gun);
            var u = s.Unit(gun);
            var strike = Command.Active(0, gun, new[] { Hex.Zero });
            Assert.IsTrue(CommandThreat.Assess(s, 1).LethalNow, "the threat analysis finds the area strike three hexes away");
            OrbitalFixture.Ok(s, strike);
            Assert.IsTrue(s.IsOver);
            Assert.IsFalse(s.Commander(0).Alive); Assert.IsFalse(s.Commander(1).Alive);
            Assert.AreEqual(0, s.Winner, "both leaders fell to one action: the acting side wins (C6)");

            var s2 = Scene(1, 9, out gun);
            OrbitalFixture.Ok(s2, Command.Active(0, gun, new[] { Hex.Zero }));
            Assert.IsTrue(s2.IsOver);
            Assert.AreEqual(1, s2.Winner, "destroying only your own leader loses");
        }

        [Test]
        public void DisablingTheFlagship_IsNotAnInstantWin()
        {
            var f = new OrbitalFixture();
            int dd = f.Add(1, "inhumanus_arbitration_destroyer", 0, -2);      // Injunction: DISABLE within 4
            int flag = f.Add(0, "pygmaei_revenant_command_ship", 0, 1);
            var s = f.Build();
            OrbitalFixture.Ok(s, Command.Wait(0, f.Id(flag)));
            var use = RosterAbilities.Enumerate(s, s.Unit(f.Id(dd))).First(x => x.picks.Contains(new Hex(0, 1)));
            OrbitalFixture.Ok(s, Command.Active(1, f.Id(dd), use.picks, use.option));
            Assert.IsTrue(s.Unit(f.Id(flag)).HasR(RStatusType.Disabled));
            Assert.IsFalse(s.IsOver, "a disabled leader is still in command");
        }
    }

    /// <summary>Prompt 4: Pygmaei industrial overclock and Inhumanus fleet contracts.</summary>
    public sealed class DoctrineTests
    {
        static Command Oc(CommandType t, int seat, int id, Hex h) => SliceRules.Order(t, seat, id, h, SliceRules.OverclockOption);

        [Test]
        public void Overclock_TradesVisibleHeatForMovement_AndHeatCoolsPredictably()
        {
            var f = new OrbitalFixture();
            int fr = f.Add(0, "pygmaei_skirmish_frigate", -4, 2);              // Ray 3
            f.Add(1, "inhumanus_mercator_corvette", 4, -4);
            var s = f.Build();
            var u = s.Unit(f.Id(fr));
            Assert.IsFalse(RosterMovement.Reachable(s, u).ContainsKey(new Hex(1, 2)), "five hexes is beyond a normal move");
            Assert.IsTrue(LegalActions.EnumerateRoster(s).Any(c => c.Option == SliceRules.OverclockOption && c.Target == new Hex(1, 2)));
            OrbitalFixture.Ok(s, Oc(CommandType.Move, 0, u.Id, new Hex(1, 2)));
            Assert.AreEqual(2, SliceRules.Heat(u));
            Assert.IsFalse(SliceRules.Overheated(u));
            OrbitalFixture.Ok(s, Command.Wait(0, u.Id));
            Assert.AreEqual(2, SliceRules.Heat(u), "no cooling in the activation that overclocked");
            OrbitalFixture.FinishRound(s);
            while (s.ActiveSeat != 0) OrbitalFixture.Ok(s, Command.Wait(1, s.Units.First(x => x.Owner == 1 && RosterRules.Eligible(s, x)).Id));
            OrbitalFixture.Ok(s, Command.Wait(0, u.Id));
            Assert.AreEqual(0, SliceRules.Heat(u), "a quiet activation sheds 2 heat");
        }

        [Test]
        public void OverheatingAtTheThreshold_CostsArmour_AndBlocksFurtherOverclocks_TheEnemyCanSeeAndExploitIt()
        {
            var f = new OrbitalFixture();
            int fr = f.Add(0, "pygmaei_furnace_cruiser", 0, 2);               // armour 1, Zigzag 2
            int foe = f.Add(1, "inhumanus_arbitration_destroyer", 0, -1);     // 4 at 2-4
            var s = f.Build();
            var u = s.Unit(f.Id(fr));
            var plain = RosterCombat.ResolveBasic(s, u, new Hex(0, -1));
            var hot = RosterCombat.ResolveBasic(s, u, new Hex(0, -1), overclock: true);
            Assert.AreEqual(plain.FinalDamage + SliceRules.OverclockDamage, hot.FinalDamage, "the previewed bonus");
            StringAssert.Contains("overclock", string.Join(" ", hot.Breakdown));
            int enemyShotBefore = RosterCombat.ResolveBasic(s, s.Unit(f.Id(foe)), u.Position, checkAction: false).FinalDamage;
            OrbitalFixture.Ok(s, Oc(CommandType.Move, 0, u.Id, new Hex(0, 1)));
            OrbitalFixture.Ok(s, Oc(CommandType.Attack, 0, u.Id, new Hex(0, -1)));
            Assert.AreEqual(hot.TargetHpAfter, s.Unit(f.Id(foe)).Hp, "preview equals result");
            Assert.AreEqual(SliceRules.OverheatAt, SliceRules.Heat(u));
            Assert.IsTrue(SliceRules.Overheated(u), "heat 4 is the threshold");
            var exploit = RosterCombat.ResolveBasic(s, s.Unit(f.Id(foe)), u.Position);
            Assert.AreEqual(Math.Min(enemyShotBefore + SliceRules.OverheatArmor, exploit.Roster.Raw), exploit.FinalDamage, "the overheated ship loses 2 armour");
            StringAssert.Contains("OVERHEATED", string.Join(" ", exploit.Breakdown));
            OrbitalFixture.Ok(s, Command.Wait(1, f.Id(foe)));
            OrbitalFixture.Ok(s, Command.Wait(0, s.Players[0].CommanderId));
            OrbitalFixture.Ok(s, Command.Wait(1, s.Players[1].CommanderId));
            StringAssert.Contains("OVERHEATED", OrbitalFixture.Rejected(s, Oc(CommandType.Move, 0, u.Id, new Hex(0, 0))));
            OrbitalFixture.Ok(s, Command.Wait(0, u.Id));
            Assert.AreEqual(2, SliceRules.Heat(u));
            Assert.IsFalse(SliceRules.Overheated(u), "one quiet activation brings it back under the threshold");
        }

        [Test]
        public void OnlyPygmaeiShipsOverclock()
        {
            var f = new OrbitalFixture();
            int a = f.Add(0, "inhumanus_mercator_corvette", 0, 2);
            f.Add(1, "pygmaei_skirmish_frigate", 0, -2);
            var s = f.Build();
            StringAssert.Contains("Pygmaei", OrbitalFixture.Rejected(s, Oc(CommandType.Move, 0, f.Id(a), new Hex(0, -1))));
            Assert.IsFalse(LegalActions.EnumerateRoster(s).Any(c => c.Option == SliceRules.OverclockOption));
        }

        static ArmyRoster Inhumanus(params (string id, string lane)[] units)
        {
            var army = new ArmyRoster { Name = "Contract test", Faction = FactionId.Inhumanus };
            foreach (var (id, lane) in units)
            {
                var e = new RosterEntry(id);
                if (lane != null) e.Equipment.Add(SliceRules.ContractToken + lane);
                army.Units.Add(e);
            }
            return army;
        }

        [Test]
        public void ContractAccounting_DiscountsTwoEscortsAtMost_AndNeverTheFlagship()
        {
            var caps = BattleBridge.Caps;
            var plain = RosterArmy.Validate(Inhumanus(("inhumanus_executive_flagship", null), ("inhumanus_proxy_frigate", null), ("inhumanus_mercator_corvette", null)), caps, BattleDomain.Orbital);
            var hired = RosterArmy.Validate(Inhumanus(("inhumanus_executive_flagship", null), ("inhumanus_proxy_frigate", "west"), ("inhumanus_mercator_corvette", "east")), caps, BattleDomain.Orbital);
            Assert.IsTrue(plain.Valid, string.Join("; ", plain.Errors));
            Assert.IsTrue(hired.Valid, string.Join("; ", hired.Errors));
            Assert.AreEqual(plain.TotalCost - 2, hired.TotalCost, "one point off each contracted escort");
            Assert.AreEqual(2, hired.Contracts);
            Assert.IsFalse(RosterArmy.Validate(Inhumanus(("inhumanus_executive_flagship", "west")), caps, BattleDomain.Orbital).Valid, "the flagship deploys at the start");
            Assert.IsFalse(RosterArmy.Validate(Inhumanus(("inhumanus_executive_flagship", null), ("inhumanus_proxy_frigate", "west"), ("inhumanus_mercator_corvette", "east"), ("inhumanus_prospect_interceptor", "centre")), caps, BattleDomain.Orbital).Valid, "at most two contracts");
            Assert.IsFalse(RosterArmy.Validate(Inhumanus(("inhumanus_executive_flagship", null), ("inhumanus_proxy_frigate", "north")), caps, BattleDomain.Orbital).Valid, "unknown lane");
            var pyg = new ArmyRoster { Faction = FactionId.Pygmaei };
            pyg.Units.Add(new RosterEntry("pygmaei_revenant_command_ship"));
            var e = new RosterEntry("pygmaei_skirmish_frigate"); e.Equipment.Add(SliceRules.ContractToken + "west"); pyg.Units.Add(e);
            Assert.IsFalse(RosterArmy.Validate(pyg, caps, BattleDomain.Orbital).Valid, "contracts are an Inhumanus doctrine");
        }

        static MatchState ContractBattle(out int escortId, Action<MatchConfig> tweak = null)
        {
            var cfg = new MatchConfig { ClockSeconds = 0, FirstSeat = 0, DefenderSeat = 1 }.UseRoster(BattleDomain.Orbital);
            cfg.MapId = "trade_passage"; cfg.TechMode = TechMode.Balanced;
            var pyg = new ArmyRoster { Name = "Pygmaei", Faction = FactionId.Pygmaei };
            foreach (var id in new[] { "pygmaei_revenant_command_ship", "pygmaei_skirmish_frigate", "pygmaei_rift_interceptor" }) pyg.Units.Add(new RosterEntry(id));
            cfg.Players[0] = new PlayerSetup { DisplayName = "Pygmaei", Army = pyg, Caps = BattleBridge.Caps.Clone() };
            cfg.Players[1] = new PlayerSetup { DisplayName = "Inhumanus", Army = Inhumanus(("inhumanus_executive_flagship", null), ("inhumanus_mercator_corvette", null), ("inhumanus_proxy_frigate", "centre")), Caps = BattleBridge.Caps.Clone() };
            tweak?.Invoke(cfg);
            var s = RulesEngine.CreateMatch(cfg, out var err);
            Assert.IsNotNull(s, err);
            OrbitalFixture.Ok(s, Command.AutoDeploy(0));
            OrbitalFixture.Ok(s, Command.AutoDeploy(1));
            escortId = s.Units.First(u => u.DefId == "inhumanus_proxy_frigate").Id;
            return s;
        }

        [Test]
        public void ContractedEscorts_AreDisclosed_ArriveAtRoundThree_OnTheirDeclaredHex()
        {
            var s = ContractBattle(out int escort);
            var u = s.Unit(escort);
            Assert.IsTrue(SliceRules.InReserve(u), "not deployed at the start");
            Assert.IsFalse(u.OnBoard);
            var entry = SliceRules.EntryHex(s.Map, 1, "centre");
            Assert.IsTrue(s.Map.IsDeploymentHex(1, entry), "entry is inside the owner's territory, on its back row");
            Assert.IsFalse(LegalActions.EnumerateRoster(s).Any(c => c.Type == CommandType.Attack && c.Target == entry), "nothing to target before arrival");
            OrbitalFixture.FinishRound(s);
            Assert.IsTrue(SliceRules.InReserve(u), "round 2: still under way");
            OrbitalFixture.FinishRound(s);
            Assert.AreEqual(3, s.Round);
            Assert.IsTrue(u.OnBoard, "arrived at the start of round 3");
            Assert.AreEqual(entry, u.Position);
            Assert.IsTrue(RosterRules.Eligible(s, u), "it may activate in its arrival round");
        }

        [Test]
        public void ABlockedEntryHex_DelaysTheArrival_OneRoundAtATime()
        {
            var s = ContractBattle(out int escort);
            var entry = SliceRules.EntryHex(s.Map, 1, "centre");
            var squatter = s.Units.First(u => u.Owner == 0 && !u.R.IsLeaderRole);
            squatter.Position = entry;                                          // the Pygmaei blockade the entry point
            OrbitalFixture.FinishRound(s); OrbitalFixture.FinishRound(s);
            Assert.AreEqual(3, s.Round);
            Assert.IsTrue(SliceRules.InReserve(s.Unit(escort)), "blocked: it waits");
            squatter.Position = entry + new Hex(0, 1);
            if (!s.Map.Contains(squatter.Position) || s.UnitAt(squatter.Position) != squatter) squatter.Position = entry + new Hex(1, 0);
            OrbitalFixture.FinishRound(s);
            Assert.AreEqual(entry, s.Unit(escort).Position, "it enters as soon as the hex is clear");
        }

        [Test]
        public void LeaderElimination_StillEndsTheBattle_WithContractsPending()
        {
            var s = ContractBattle(out int escort);
            s.Commander(1).Hp = 1;
            var gun = s.Units.First(u => u.DefId == "pygmaei_skirmish_frigate");
            gun.Position = s.Commander(1).Position + new Hex(0, 2);
            if (s.UnitAt(gun.Position) != gun || RosterTerrain.Blocks(s, gun.Position)) gun.Position = s.Commander(1).Position + new Hex(-1, 2);
            var o = RosterCombat.ResolveBasic(s, gun, s.Commander(1).Position);
            Assume.That(o.Legal, o.Reason);
            OrbitalFixture.Ok(s, Command.Attack(0, gun.Id, s.Commander(1).Position));
            Assert.IsTrue(s.IsOver);
            Assert.IsTrue(SliceRules.InReserve(s.Unit(escort)), "the contract never arrived");
        }

        [Test]
        public void SliceRosters_AreLegal_AndOfferTwoDistinctCompositionsPerFaction()
        {
            foreach (var f in new[] { FactionId.Pygmaei, FactionId.Inhumanus })
            {
                var classes = SliceRoster.Classes(f).ToList();
                Assert.That(classes.Count, Is.InRange(4, 5));
                Assert.AreEqual(1, classes.Count(id => RosterContent.Unit(id).Roster.IsLeaderRole));
                var comps = SliceRoster.Compositions(f).ToList();
                Assert.GreaterOrEqual(comps.Count, 2);
                foreach (var c in comps)
                {
                    var rep = RosterArmy.Validate(c.Army, BattleBridge.Caps, BattleDomain.Orbital);
                    Assert.IsTrue(rep.Valid, c.Name + ": " + string.Join("; ", rep.Errors));
                    Assert.IsTrue(c.Army.Units.All(e => classes.Contains(e.UnitId)), c.Name);
                    Assert.IsNotEmpty(c.Tradeoff);
                }
                var a = comps[0].Army.Units.Select(e => e.UnitId).OrderBy(x => x).ToList();
                var b = comps[1].Army.Units.Select(e => e.UnitId).OrderBy(x => x).ToList();
                Assert.AreNotEqual(string.Join(",", a), string.Join(",", b), "materially different");
            }
            Assert.IsEmpty(SliceRoster.Validate(), "the slice content validates at load");
        }
    }
}
