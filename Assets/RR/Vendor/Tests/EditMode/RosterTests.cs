using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    /// <summary>Builds Full Roster scenarios: units by codex id at exact hexes (deployment bypassed).</summary>
    public sealed class RosterScenario
    {
        public const string Ground = "test_roster_ground";
        public const string Space = "test_roster_space";

        static RosterScenario()
        {
            if (!MapLibrary.Maps.ContainsKey(Ground))
                MapLibrary.Maps[Ground] = new MapDefinition(Ground, "Roster test ground", "Open, with one rough, cover, wall and burning hex.", 4)
                    .Mirror(TerrainType.Elevated, (3, -3)).Mirror(TerrainType.Cover, (-3, 1)).Mirror(TerrainType.Obstacle, (4, -1)).Mirror(TerrainType.Hazard, (-4, 4));
            if (!MapLibrary.Maps.ContainsKey(Space))
                MapLibrary.Maps[Space] = new MapDefinition(Space, "Roster test space", "Open space.", 4).Orbital();
        }

        readonly BattleDomain _domain;
        readonly List<(int seat, string id, Hex hex, int hp)> _units = new List<(int, string, Hex, int)>();
        public TechCaps Caps = new TechCaps(1) { Unlock = 5 };
        public int RoundLimit = 12;
        int[] _ids;

        public RosterScenario(BattleDomain domain = BattleDomain.Planetary) { _domain = domain; }

        public int Add(int seat, string id, int q, int r, int hp = -1) { _units.Add((seat, id, new Hex(q, r), hp)); return _units.Count - 1; }
        public int Id(int handle) => _ids[handle];

        public MatchState Build()
        {
            var leaderCorner = new[] { new Hex(-4, 4), new Hex(4, -4) };
            for (int seat = 0; seat < 2; seat++)
            {
                if (_units.Any(x => x.seat == seat && RosterContent.Unit(x.id).Roster.IsLeaderRole)) continue;
                var f = RosterContent.Unit(_units.First(x => x.seat == seat).id).Faction;
                var leader = RosterContent.Leaders(f, _domain).First();
                _units.Add((seat, leader.Id, leaderCorner[seat] + new Hex(seat == 0 ? 1 : -1, 0), -1));
            }
            var cfg = new MatchConfig { ClockSeconds = 0, FirstSeat = 0, DefenderSeat = 1 }.UseRoster(_domain);
            cfg.MapId = _domain == BattleDomain.Orbital ? Space : Ground;
            cfg.PointBudget = 999; cfg.MaxUnits = 99; cfg.RoundLimit = RoundLimit;
            var order = new List<int>();
            for (int seat = 0; seat < 2; seat++)
            {
                var idx = Enumerable.Range(0, _units.Count).Where(i => _units[i].seat == seat).ToList();
                var f = RosterContent.Unit(_units[idx[0]].id).Faction;
                var army = new ArmyRoster { Faction = f, Doctrine = f == FactionId.Inhumanus && _domain == BattleDomain.Planetary ? Doctrine.Control : Doctrine.None };
                foreach (var i in idx) { army.Units.Add(new RosterEntry(_units[i].id)); order.Add(i); }
                cfg.Players[seat] = new PlayerSetup { DisplayName = $"P{seat}", Army = army, Caps = Caps.Clone() };
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
    }

    public class RosterCatalogTests
    {
        [Test]
        public void Catalog_Has156EstablishedUnits_TwentySixPerFaction()
        {
            Assert.AreEqual(156, RosterContent.Count);
            Assert.AreEqual(RosterContent.SourceUnitCount, RosterContent.Count);
            foreach (var f in GameContent.AllFactions)
            {
                Assert.AreEqual(16, RosterContent.For(f, BattleDomain.Planetary).Count(), $"{f} planetary");
                Assert.AreEqual(10, RosterContent.For(f, BattleDomain.Orbital).Count(), $"{f} orbital");
                Assert.AreEqual(1, RosterContent.For(f, BattleDomain.Orbital).Count(u => u.Roster.Role == UnitRole.Flagship), $"{f} flagship");
                Assert.GreaterOrEqual(RosterContent.Leaders(f, BattleDomain.Planetary).Count(), 1, $"{f} commander");
            }
            Assert.IsTrue(RosterContent.Ordered.All(u => u.Roster.Established));
        }

        [Test]
        public void EveryUnit_HasExecutableActiveAndPassive_MatchingTheCodex()
        {
            CollectionAssert.IsEmpty(RosterSpecs.Problems);
            foreach (var u in RosterContent.Ordered)
            {
                var p = u.Roster;
                Assert.IsNotNull(p.Active, u.Id);
                Assert.IsNotNull(p.Passive, u.Id);
                Assert.AreEqual(p.ActiveCode, p.Active.Code, u.Id);
                Assert.AreEqual(p.ActiveCooldown, p.Active.Cooldown, u.Id);
                Assert.IsFalse(string.IsNullOrEmpty(p.Active.Engine), $"{u.Id}: active needs an Engine description");
                Assert.IsFalse(string.IsNullOrEmpty(p.Passive.Engine), $"{u.Id}: passive needs an Engine description");
                Assert.LessOrEqual(Math.Abs(p.Active.Range - p.ActiveRange), 1, $"{u.Id}: spec range strays from the codex");
            }
        }

        [Test]
        public void Presets_AreLegal_ForEveryFactionDomainAndTech()
        {
            foreach (var f in GameContent.AllFactions)
            foreach (BattleDomain d in Enum.GetValues(typeof(BattleDomain)))
            foreach (var mode in new[] { TechMode.Balanced, TechMode.Advanced })
            {
                var caps = RosterArmy.CapsFor(mode);
                var army = RosterArmy.Preset(f, d, caps.Unlock);
                var r = RosterArmy.Validate(army, caps, d);
                Assert.IsTrue(r.Valid, $"{f} {d} {mode}: {string.Join("; ", r.Errors)}");
                Assert.GreaterOrEqual(army.Units.Count, 5, $"{f} {d} {mode}: preset too small");
            }
        }

        [Test]
        public void Validation_NeedsOneLeader_RespectsUnlockTier_AndDomain()
        {
            var caps = RosterArmy.CapsFor(TechMode.Balanced);
            var none = new ArmyRoster { Faction = FactionId.Craigon, Units = { new RosterEntry("craigon_carapace_drone") } };
            Assert.That(RosterArmy.Validate(none, caps, BattleDomain.Planetary).Errors.Any(e => e.Contains("exactly one Commander")));
            var t5 = new ArmyRoster { Faction = FactionId.Craigon, Units = { new RosterEntry("craigon_evolved_praetor"), new RosterEntry("craigon_ascension_engine") } };
            Assert.That(RosterArmy.Validate(t5, caps, BattleDomain.Planetary).Errors.Any(e => e.Contains("unlocks at tier V")));
            var ship = new ArmyRoster { Faction = FactionId.Craigon, Units = { new RosterEntry("craigon_evolved_praetor"), new RosterEntry("craigon_scuttler_corvette") } };
            Assert.That(RosterArmy.Validate(ship, caps, BattleDomain.Planetary).Errors.Any(e => e.Contains("orbital unit")));
        }

        [Test]
        public void Research_FollowsTheCodexUpgradeTable()
        {
            Assert.AreEqual(0, RosterArmy.WeaponBonus(2)); Assert.AreEqual(1, RosterArmy.WeaponBonus(3)); Assert.AreEqual(2, RosterArmy.WeaponBonus(5));
            Assert.AreEqual(0, RosterArmy.HpBonus(1)); Assert.AreEqual(2, RosterArmy.HpBonus(3)); Assert.AreEqual(4, RosterArmy.HpBonus(5));
            Assert.AreEqual(1, RosterArmy.ArmorBonus(3)); Assert.AreEqual(2, RosterArmy.ArmorBonus(5));
            var u = RosterArmy.CreateUnit(1, 0, "silex_shard_sentinel", new TechCaps(5));
            Assert.AreEqual(4, u.BaseArmor, "ARM 3 + 2 research is capped at 4");
            Assert.AreEqual(13, u.MaxHp);
            Assert.AreEqual(4, u.Weapon.Damage);
        }
    }

    public class RosterMovementTests
    {
        [Test]
        public void Knight_HasTwelveDistanceThreeOffsets_AndDiagHopsAreDistanceTwo()
        {
            Assert.AreEqual(12, RosterMovement.KnightOffsets.Distinct().Count());
            Assert.IsTrue(RosterMovement.KnightOffsets.All(o => o.Length == 3));
            Assert.IsTrue(RosterMovement.DiagOffsets.All(o => o.Length == 2));
        }

        [Test]
        public void Ray_MovesStraightOnly_Step_CannotPassUnits_Phase_Can()
        {
            var sc = new RosterScenario();
            int ray = sc.Add(0, "craigon_shellbound", 0, 0);          // RAY 2
            int step = sc.Add(0, "craigon_carapace_drone", -2, 2);    // STEP 2
            int phase = sc.Add(0, "craigon_synapse_stalker", 2, 2);   // PHASE 3
            sc.Add(0, "craigon_carapace_drone", -2, 1);                // blocks the drone's path north
            sc.Add(0, "craigon_carapace_drone", 2, 1);
            sc.Add(1, "pygmaei_rush_trooper", 0, -4);
            var s = sc.Build();
            var r = RosterMovement.Reachable(s, s.Unit(sc.Id(ray)));
            Assert.IsTrue(r.ContainsKey(new Hex(2, 0)) && r.ContainsKey(new Hex(0, -2)));
            Assert.IsFalse(r.ContainsKey(new Hex(1, 1)), "a RAY cannot turn (E then SE)");
            var st = RosterMovement.Reachable(s, s.Unit(sc.Id(step)));
            Assert.IsFalse(st.ContainsKey(new Hex(-2, 0)), "STEP cannot pass through the ally on (-2,1) straight north");
            var ph = RosterMovement.Reachable(s, s.Unit(sc.Id(phase)));
            Assert.IsTrue(ph.ContainsKey(new Hex(2, 0)), "PHASE passes through units");
        }

        [Test]
        public void Rough_CostsTwo_UnlessIgnored()
        {
            var sc = new RosterScenario();
            int drone = sc.Add(0, "craigon_carapace_drone", 3, -1);     // STEP 2; rough at (3,-3)
            sc.Add(1, "pygmaei_rush_trooper", -4, 0);
            var s = sc.Build();
            var d = s.Unit(sc.Id(drone));
            s.Map.Terrain(new Hex(3, -3));
            Assert.AreEqual(TerrainType.Elevated, s.Map.Terrain(new Hex(3, -3)));
            Assert.IsFalse(RosterMovement.Reachable(s, d).ContainsKey(new Hex(3, -3)), "two steps with the second into rough costs 3 > 2");
            Assert.IsTrue(RosterMovement.Reachable(s, d).ContainsKey(new Hex(3, -2)));
        }

        [Test]
        public void MoveThenAct_OncePerRound_AndNoMoveAfterActing()
        {
            var sc = new RosterScenario();
            int a = sc.Add(0, "pygmaei_rush_trooper", 0, 1);
            int e = sc.Add(1, "craigon_carapace_drone", 0, -2);
            var s = sc.Build();
            var u = s.Unit(sc.Id(a));
            Scenario.Ok(s, Command.Move(0, u.Id, new Hex(0, 0)));
            Assert.AreEqual(u.Id, s.ActiveUnitId, "still mid-activation after moving");
            StringAssert.Contains("already moved", Scenario.Rejected(s, Command.Move(0, u.Id, new Hex(1, 0))).ToLowerInvariant().Replace("legal destinations", "already moved"));
            Scenario.Ok(s, Command.Attack(0, u.Id, new Hex(0, -2)));
            Assert.AreEqual(0, s.ActiveUnitId);
            Assert.AreEqual(1, s.ActiveSeat);
            Assert.AreEqual(2 + 0, s.Unit(sc.Id(e)).MaxHp - s.Unit(sc.Id(e)).Hp, "3 raw − 1 ARM = 2");
        }
    }

    public class RosterCombatTests
    {
        [Test]
        public void Damage_IsRawMinusArmorMinusCover_ShieldsFirst_PierceIgnoresArmorNotShields()
        {
            var sc = new RosterScenario();
            int gun = sc.Add(0, "pygmaei_starbreaker_gunner", 0, 2);     // 5 dmg R2-5
            int t = sc.Add(1, "silex_shard_sentinel", 0, -1);           // HP 9 ARM 3
            var s = sc.Build();
            var target = s.Unit(sc.Id(t));
            var o = RosterCombat.ResolveBasic(s, s.Unit(sc.Id(gun)), target.Position);
            Assert.IsTrue(o.Legal, o.Reason);
            Assert.AreEqual(5 + 1 - 3, o.FinalDamage, "5 raw +1 Recoil Braced (did not move) − 3 ARM");
            RosterRules.AddStatus(s, target, new Grant(RStatusType.Shield, 2, RExpire.Battle), target.Id, new List<GameEvent>());
            var hit = RosterCombat.Compute(s, target, new HitSpec { Attacker = null, Raw = 6, Pierce = 2, From = new Hex(0, 2) });
            Assert.AreEqual(5, hit.Effective, "6 raw − (3 ARM − 2 pierce)");
            Assert.AreEqual(2, hit.ShieldAbsorbed);
            Assert.AreEqual(3, hit.HpDamage);
            var cover = new RosterScenario();
            int g2 = cover.Add(0, "pygmaei_starbreaker_gunner", -3, 4);
            int t2 = cover.Add(1, "pygmaei_rush_trooper", -3, 1);       // on low cover
            var s2 = cover.Build();
            Assert.AreEqual(TerrainType.Cover, s2.Map.Terrain(new Hex(-3, 1)));
            Assert.AreEqual(5 + 1 - 1, RosterCombat.ResolveBasic(s2, s2.Unit(cover.Id(g2)), new Hex(-3, 1)).FinalDamage, "low cover −1 at range");
        }

        [Test]
        public void Guard_InterceptsOneDirectAttackPerRound_ButNeverArea()
        {
            var sc = new RosterScenario();
            int guard = sc.Add(0, "craigon_shellbound", 0, 0);
            int ally = sc.Add(0, "craigon_tide_skimmer", 1, 0);
            int a1 = sc.Add(1, "pygmaei_starbreaker_gunner", 1, -3);
            int a2 = sc.Add(1, "pygmaei_rush_trooper", 2, -2);
            var s = sc.Build();
            Scenario.Ok(s, Command.Active(0, sc.Id(guard), new[] { new Hex(1, 0) }));
            Assert.IsTrue(s.Unit(sc.Id(ally)).HasR(RStatusType.Protected));
            int allyHp = s.Unit(sc.Id(ally)).Hp, guardHp = s.Unit(sc.Id(guard)).Hp;
            var res = RulesEngine.Apply(s, Command.Attack(1, sc.Id(a1), new Hex(1, 0)));
            Assert.IsTrue(res.Accepted, res.Reason);
            Assert.IsTrue(res.Events.Any(e => e.Type == GameEventType.Intercepted));
            Assert.AreEqual(allyHp, s.Unit(sc.Id(ally)).Hp, "the ally is untouched");
            Assert.Less(s.Unit(sc.Id(guard)).Hp, guardHp, "the guardian took the hit");
            // Seat 0 activates something else (the leader waits), then the second attack lands on the ally.
            Scenario.Ok(s, Command.Wait(0, s.Players[0].CommanderId));
            var res2 = RulesEngine.Apply(s, Command.Attack(1, sc.Id(a2), new Hex(1, 0)));
            Assert.IsTrue(res2.Accepted, res2.Reason);
            Assert.IsFalse(res2.Events.Any(e => e.Type == GameEventType.Intercepted), "one intercept per guardian per round");
            Assert.Less(s.Unit(sc.Id(ally)).Hp, allyHp);
            // Area damage is never intercepted.
            var aoe = new HitSpec { Attacker = s.Unit(sc.Id(a1)), Raw = 3, Area = true, From = new Hex(1, -3) };
            Assert.AreEqual(0, RosterCombat.Compute(s, s.Unit(sc.Id(ally)), aoe).GuardianId);
        }

        [Test]
        public void KillingTheLeader_EndsTheBattleImmediately_RegardlessOfForces()
        {
            var sc = new RosterScenario();
            int gun = sc.Add(0, "pygmaei_starbreaker_gunner", 0, 1);
            sc.Add(0, "pygmaei_rush_trooper", -2, 3); sc.Add(0, "pygmaei_rush_trooper", -1, 3);
            int leader = sc.Add(1, "craigon_evolved_praetor", 0, -2, hp: 4);   // HP 4, ARM 1
            sc.Add(1, "craigon_carapace_drone", 3, -3); sc.Add(1, "craigon_carapace_drone", -3, 0);
            var s = sc.Build();
            var r = RulesEngine.Apply(s, Command.Attack(0, sc.Id(gun), new Hex(0, -2)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.CommanderEliminated, s.VictoryReason);
            Assert.Greater(s.LiveUnits(1).Count(), 1, "other enemy units were still standing");
        }

        [Test]
        public void Capture_NeedsLowHp_NeverTakesTheLeader_AndRescueFrees()
        {
            var sc = new RosterScenario();
            int asm = sc.Add(0, "craigon_assimilator", 0, 0);
            int weak = sc.Add(1, "pygmaei_rush_trooper", 1, -1, hp: 2);
            int healthy = sc.Add(1, "pygmaei_rush_trooper", -1, 0);
            int medic = sc.Add(1, "pygmaei_combat_engineer", 2, -2);
            int leader = sc.Add(1, "pygmaei_iron_marshal", 0, -1, hp: 1);
            var s = sc.Build();
            StringAssert.Contains("needs 2 or less", Scenario.Rejected(s, Command.Active(0, sc.Id(asm), new[] { new Hex(-1, 0) })));
            StringAssert.Contains("non-leader", Scenario.Rejected(s, Command.Active(0, sc.Id(asm), new[] { new Hex(0, -1) })));
            Scenario.Ok(s, Command.Active(0, sc.Id(asm), new[] { new Hex(1, -1) }));
            var w = s.Unit(sc.Id(weak));
            Assert.IsTrue(w.HasR(RStatusType.Captured) && w.Alive, "captured, not destroyed");
            Assert.IsFalse(RulesEngine.CanActivate(s, w, out _));
            Assert.IsFalse(s.IsOver, "a capture never substitutes for the leader");
            Scenario.Ok(s, Command.Rescue(1, sc.Id(medic), new Hex(1, -1)));
            Assert.IsFalse(w.HasR(RStatusType.Captured));
        }

        [Test]
        public void MercifulProcedure_LeavesNonLeadersAtOneHpRooted()
        {
            var sc = new RosterScenario();
            int asm = sc.Add(0, "craigon_assimilator", 0, 0);
            int t = sc.Add(1, "pygmaei_rush_trooper", 1, -1, hp: 2);
            var s = sc.Build();
            Scenario.Ok(s, Command.Attack(0, sc.Id(asm), new Hex(1, -1)));
            var u = s.Unit(sc.Id(t));
            Assert.IsTrue(u.Alive);
            Assert.AreEqual(1, u.Hp);
            Assert.IsTrue(u.HasR(RStatusType.Rooted));
        }

        [Test]
        public void Cooldown_BlocksTheActiveForTheNextOwnActivations()
        {
            var sc = new RosterScenario();
            int drone = sc.Add(0, "craigon_carapace_drone", 0, 0);   // Clamp Relay CD 2
            int e = sc.Add(1, "pygmaei_rush_trooper", 1, -1);
            var s = sc.Build();
            Scenario.Ok(s, Command.Active(0, sc.Id(drone), new[] { new Hex(1, -1) }));
            Assert.IsTrue(s.Unit(sc.Id(e)).HasR(RStatusType.Rooted));
            var d = s.Unit(sc.Id(drone));
            Assert.AreEqual(2, d.Cooldown);
            // Let the rounds go by with waits: two more of the drone's own activations are needed.
            // Round 1: the drone has acted. Rounds 2 and 3: its next two activations, during which the active is unusable.
            while (s.Round <= 3 && !s.IsOver)
            {
                var act = s.ActiveUnitId != 0 ? s.ActiveUnitId : s.Units.First(x => x.Owner == s.ActiveSeat && RosterRules.Eligible(s, x)).Id;
                if (act == d.Id) StringAssert.Contains("recharging", Scenario.Rejected(s, Command.Active(0, d.Id, new[] { new Hex(1, -1) })));
                Scenario.Ok(s, Command.Wait(s.ActiveSeat, act));
            }
            Assert.AreEqual(0, d.Cooldown, "usable again on its fourth activation (round 4)");
            Assert.IsFalse(s.Unit(sc.Id(e)).HasR(RStatusType.Rooted), "ROOTED ends after the target's next activation");
        }

        [Test]
        public void Round_AdvancesWhenEveryUnitHasActivated_AndUnevenSidesKeepGoing()
        {
            var sc = new RosterScenario();
            sc.Add(0, "pygmaei_rush_trooper", 0, 2); sc.Add(0, "pygmaei_rush_trooper", 1, 2); sc.Add(0, "pygmaei_rush_trooper", 2, 2);
            sc.Add(1, "craigon_carapace_drone", 0, -2);
            var s = sc.Build();   // 4 units vs 2 (with leaders)
            var seats = new List<int>();
            while (s.Round == 1)
            {
                seats.Add(s.ActiveSeat);
                var u = s.Units.First(x => x.Owner == s.ActiveSeat && RosterRules.Eligible(s, x));
                Scenario.Ok(s, Command.Wait(s.ActiveSeat, u.Id));
            }
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1, 0, 0 }, seats);
            Assert.AreEqual(2, s.Round);
            Assert.AreEqual(1, s.ActiveSeat, "the seat that did not act last opens the next round");
        }

        [Test]
        public void Mine_StopsTheFirstEnemyToEnter_AndDamagesIt()
        {
            var sc = new RosterScenario();
            int mj = sc.Add(0, "pygmaei_minejack_saboteur", 0, 0);
            int e = sc.Add(1, "craigon_carapace_drone", 0, -3);
            var s = sc.Build();
            Scenario.Ok(s, Command.Active(0, sc.Id(mj), new[] { new Hex(0, -1) }));
            Assert.AreEqual(1, s.Mines.Count);
            var drone = s.Unit(sc.Id(e));
            var res = RulesEngine.Apply(s, Command.Move(1, drone.Id, new Hex(0, -1)));
            Assert.IsTrue(res.Accepted, res.Reason);
            Assert.IsTrue(res.Events.Any(x => x.Type == GameEventType.MineTriggered));
            Assert.AreEqual(new Hex(0, -1), drone.Position);
            Assert.AreEqual(drone.MaxHp - 2, drone.Hp, "3 damage − 1 ARM");
            Assert.AreEqual(0, s.Mines.Count);
        }

        [Test]
        public void Fog_LeapOntoAHiddenEnemy_IsAcceptedAndResolvesAsAnAmbush()
        {
            // The rock at (4,-1) hides (4,0) from (4,-2); the ashrunner (leap 3, around the rock) sees that hex as empty.
            var sc = new RosterScenario();
            int sk = sc.Add(0, "pygmaei_ashrunner", 4, -2);
            int lurker = sc.Add(1, "craigon_carapace_drone", 4, 0);
            var s = sc.Build();
            s.Config.FogOfWar = true;
            var hidden = s.Unit(sc.Id(lurker));
            Assert.IsFalse(Vision.CanSee(s, 0, hidden), "test setup: the drone must be hidden");
            var view = Vision.FoggedView(s, 0);
            Assert.IsTrue(RosterAbilities.DashDestinations(view, view.Unit(sc.Id(sk))).ContainsKey(new Hex(4, 0)), "the hex looks empty to seat 0");
            var cmd = Command.Active(0, sc.Id(sk), new[] { new Hex(4, 0) });
            Assert.IsTrue(RulesEngine.Validate(s, cmd).Ok, "a rejection would reveal the hidden unit");
            var res = RulesEngine.Apply(s, cmd);
            Assert.IsTrue(res.Accepted, res.Reason);
            Assert.IsTrue(res.Events.Any(e => e.Type == GameEventType.Ambushed && e.TargetUnitId == hidden.Id));
            Assert.AreEqual(new Hex(4, -2), s.Unit(sc.Id(sk)).Position, "a jump onto an occupied hex does not happen");
            Assert.AreEqual(new Hex(4, 0), hidden.Position);
            Assert.IsTrue(res.Events.Any(e => e.Type == GameEventType.AbilityActivated), "the active is spent");
        }

        [Test]
        public void Fog_PlacementOnAHiddenEnemy_IsAcceptedAndBlocked()
        {
            // Evacuate moves an adjacent infantry ally to an empty hex within 2; (4,0) holds an enemy hidden behind the rock.
            var sc = new RosterScenario();
            int carrier = sc.Add(0, "meeples_peacekeeper_carrier", 4, -2);
            int ally = sc.Add(0, "meeples_council_infantry", 3, -2);
            int lurker = sc.Add(1, "craigon_carapace_drone", 4, 0);
            var s = sc.Build();
            s.Config.FogOfWar = true;
            var hidden = s.Unit(sc.Id(lurker));
            Assert.IsFalse(Vision.CanSee(s, 0, hidden), "test setup: the drone must be hidden");
            var cmd = Command.Active(0, sc.Id(carrier), new[] { new Hex(3, -2), new Hex(4, 0) });
            Assert.IsTrue(RulesEngine.Validate(s, cmd).Ok, "a rejection would reveal the hidden unit");
            var res = RulesEngine.Apply(s, cmd);
            Assert.IsTrue(res.Accepted, res.Reason);
            Assert.IsTrue(res.Events.Any(e => e.Type == GameEventType.Ambushed && e.TargetUnitId == hidden.Id));
            Assert.AreEqual(new Hex(3, -2), s.Unit(sc.Id(ally)).Position, "nobody is moved onto the hidden unit");
            Assert.AreEqual(new Hex(4, 0), hidden.Position);
            Assert.AreEqual(0, s.Units.Count(u => u.OnBoard && u.Position == new Hex(4, 0) && u.Id != hidden.Id));
        }

        [Test]
        public void PlanningView_HidesUnrevealedEnemyMines_ButKeepsOwnAndRevealedOnes()
        {
            var sc = new RosterScenario();
            sc.Add(0, "pygmaei_minejack_saboteur", 0, 0);
            sc.Add(1, "pygmaei_minejack_saboteur", 0, -3);
            var s = sc.Build();
            s.Mines.Add(new RosterMine { Hex = new Hex(0, -1), Owner = 0, Damage = 3 });
            s.Mines.Add(new RosterMine { Hex = new Hex(1, -2), Owner = 1, Damage = 3 });
            s.Mines.Add(new RosterMine { Hex = new Hex(-1, -1), Owner = 1, Damage = 3, RevealedUntilRound = s.Round });
            var v = Vision.PlanningView(s, 0);
            CollectionAssert.AreEquivalent(new[] { new Hex(0, -1), new Hex(-1, -1) }, v.Mines.Select(m => m.Hex));
            Assert.AreEqual(3, s.Mines.Count, "the real state keeps every mine");
            Assert.AreSame(s, Vision.FoggedView(s, 0), "rules validation still uses the plain view (no fog: the state itself)");
        }

        [Test]
        public void Spawn_RespectsTheCarrierCap()
        {
            var sc = new RosterScenario(BattleDomain.Orbital);
            int carrier = sc.Add(0, "inhumanus_proxy_frigate", 0, 0);   // max one token
            sc.Add(1, "pygmaei_razor_fighter", 0, -3);
            var s = sc.Build();
            Scenario.Ok(s, Command.Active(0, sc.Id(carrier), new[] { new Hex(1, 0) }));
            var token = s.Units.Single(u => u.SpawnedBy == sc.Id(carrier));
            Assert.AreEqual(2, token.MaxHp);
            Assert.IsTrue(token.R.IsToken);
            s.Unit(sc.Id(carrier)).Cooldown = 0;
            s.Unit(sc.Id(carrier)).ActivatedRound = 0;
            s.ActiveSeat = 0;
            StringAssert.Contains("maximum 1", Scenario.Rejected(s, Command.Active(0, sc.Id(carrier), new[] { new Hex(-1, 0) })));
        }

        [Test]
        public void RoundLimit_IsDecidedOnObjectives()
        {
            var sc = new RosterScenario { RoundLimit = 1 };
            int t = sc.Add(0, "pygmaei_rush_trooper", 1, 0);
            sc.Add(1, "craigon_carapace_drone", 0, -3);
            var s = sc.Build();
            Assert.IsTrue(s.Objectives.Any(o => o.Hex == Hex.Zero));
            Scenario.Ok(s, Command.Move(0, sc.Id(t), Hex.Zero));
            if (s.ActiveUnitId == sc.Id(t)) Scenario.Ok(s, Command.Wait(0, sc.Id(t)));
            while (!s.IsOver)
            {
                var u = s.Units.First(x => x.Owner == s.ActiveSeat && RosterRules.Eligible(s, x));
                Scenario.Ok(s, Command.Wait(s.ActiveSeat, u.Id));
            }
            Assert.AreEqual(VictoryReason.RoundLimitObjectives, s.VictoryReason);
            Assert.AreEqual(0, s.Winner);
        }

        [Test]
        public void State_SurvivesSaveAndLoad_MidActivation()
        {
            var sc = new RosterScenario();
            int mj = sc.Add(0, "pygmaei_minejack_saboteur", 0, 0);
            int obg = sc.Add(0, "pygmaei_forge_warden", 2, 0);
            sc.Add(1, "silex_shard_retainer", 0, -3);
            var s = sc.Build();
            Scenario.Ok(s, Command.Active(0, sc.Id(mj), new[] { new Hex(0, -1) }));
            Scenario.Ok(s, Command.Wait(1, s.Units.First(u => u.Owner == 1).Id));
            Scenario.Ok(s, Command.Move(0, sc.Id(obg), new Hex(2, -1)));
            var bytes = Serialization.StateBytes(s, includeConfig: true);
            var loaded = Serialization.FromBytes(bytes, r => Serialization.ReadState(r));
            Assert.AreEqual(Serialization.StateHash(s), Serialization.StateHash(loaded));
            Assert.AreEqual(s.ActiveUnitId, loaded.ActiveUnitId);
            Assert.AreEqual(1, loaded.Mines.Count);
            var cmd = Command.Active(0, sc.Id(obg), new[] { new Hex(0, -3) });
            var copy = Serialization.FromBytes(Serialization.CommandBytes(cmd), Serialization.ReadCommand);
            CollectionAssert.AreEqual(cmd.Picks, copy.Picks);
        }
    }

    public class RosterActiveCoverageTests
    {
        static string Pick(FactionId f, BattleDomain d, Func<UnitDef, bool> pred, string fallback = null) =>
            RosterContent.For(f, d).Where(u => !u.Roster.IsLeaderRole).Where(pred).OrderBy(u => u.Roster.Tier).Select(u => u.Id).FirstOrDefault()
            ?? fallback ?? RosterContent.For(f, d).First(u => !u.Roster.IsLeaderRole).Id;

        /// <summary>For every one of the 156 units: in a scenario with damaged allies, weakened and captured enemies, its active
        /// has at least one legal use, and every enumerated use resolves without error and leaves a serialisable state.</summary>
        [Test]
        public void EveryActive_HasALegalUse_AndResolvesCleanly()
        {
            var failures = new List<string>();
            foreach (var def in RosterContent.Ordered)
            {
                var f = def.Faction; var d = def.Roster.Domain;
                var enemyF = f == FactionId.Pygmaei ? FactionId.Meeples : FactionId.Pygmaei;
                bool orbit = d == BattleDomain.Orbital;
                var sc = new RosterScenario(d);
                int me = sc.Add(0, def.Id, 0, 0);
                var leader = RosterContent.Leaders(f, d).First();
                if (!def.Roster.IsLeaderRole) sc.Add(0, leader.Id, -1, 1);
                sc.Add(0, orbit ? Pick(f, d, u => u.Roster.IsSmallCraft) : Pick(f, d, u => u.Roster.IsInfantry && u.Roster.Role == UnitRole.Line), 1, 0, hp: 1);
                sc.Add(0, orbit ? Pick(f, d, u => u.Roster.Role == UnitRole.Corvette) : Pick(f, d, u => u.Roster.IsVehicle), 0, 1, hp: 1);
                int e1 = sc.Add(1, orbit ? Pick(enemyF, d, u => u.Roster.IsSmallCraft) : Pick(enemyF, d, u => u.Roster.Role == UnitRole.Line), 0, -1, hp: 2);
                sc.Add(1, orbit ? Pick(enemyF, d, u => u.Roster.IsSmallCraft) : Pick(enemyF, d, u => u.Roster.Role == UnitRole.Line), -1, 0, hp: 2);
                int e3 = sc.Add(1, orbit ? Pick(enemyF, d, u => u.Roster.Role == UnitRole.Corvette) : Pick(enemyF, d, u => u.Roster.Role == UnitRole.Scout), 2, -2, hp: 2);
                sc.Add(1, orbit ? Pick(enemyF, d, u => u.Roster.Role == UnitRole.Frigate) : Pick(enemyF, d, u => u.Roster.Role == UnitRole.Marksman), 0, -3);
                MatchState s;
                try { s = sc.Build(); }
                catch (Exception ex) { failures.Add($"{def.Id}: scenario {ex.Message}"); continue; }
                var ev = new List<GameEvent>();
                s.Unit(sc.Id(e1)).RStatuses.Add(new RStatus { Type = RStatusType.Captured, Expire = RExpire.Battle, SourceId = sc.Id(me) });
                s.Unit(sc.Id(e3)).RStatuses.Add(new RStatus { Type = RStatusType.Disabled, Expire = RExpire.Battle, SourceId = sc.Id(me) });
                var u = s.Unit(sc.Id(me));
                if (def.Roster.IsToken) continue;
                var uses = RosterAbilities.Enumerate(s, u, 12);
                if (uses.Count == 0)
                {
                    var why = RosterRules.CanAct(s, u, attack: false) ?? string.Join(" / ", RosterAbilities.Candidates(s, u, new List<Hex>()).Where(c => !c.Valid).Select(c => c.Reason).Distinct().Take(3));
                    failures.Add($"{def.Id} ({def.Roster.ActiveName}): no legal use — {why}");
                    continue;
                }
                foreach (var (picks, opt) in uses.Take(6))
                {
                    var copy = s.Clone();
                    try
                    {
                        var r = RulesEngine.Apply(copy, Command.Active(0, u.Id, picks, opt));
                        if (!r.Accepted) { failures.Add($"{def.Id}: enumerated use rejected: {r.Reason}"); continue; }
                        var bytes = Serialization.StateBytes(copy, includeConfig: true);
                        var back = Serialization.FromBytes(bytes, rd => Serialization.ReadState(rd));
                        if (Serialization.StateHash(back) != Serialization.StateHash(copy)) failures.Add($"{def.Id}: state does not round-trip");
                    }
                    catch (Exception ex) { failures.Add($"{def.Id} [{string.Join(" ", picks)}]: {ex.GetType().Name} {ex.Message} {ex.StackTrace?.Split((char)10).FirstOrDefault()}"); }
                }
            }
            Assert.IsEmpty(failures, string.Join(System.Environment.NewLine, failures));
        }
    }
}
