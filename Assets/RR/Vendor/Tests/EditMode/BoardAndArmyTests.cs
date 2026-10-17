using System.Linq;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    public class BoardTests
    {
        [Test]
        public void Board_OutlineSizes()
        {
            // Authored battlefields: the 115-hex elongated hexagon (15 wide through the centre, 9 rows deep).
            foreach (var map in MapLibrary.Maps.Values.Where(m => !m.Id.StartsWith("test")))
            {
                Assert.AreEqual(115, map.TileCount, map.Id);
                Assert.IsTrue(map.IsFullOutline, map.Id);
                Assert.AreEqual(4, map.Radius, map.Id);
                Assert.AreEqual(7, map.HalfWidth, map.Id);
            }
            // The regular radius-3 hexagon (rules test maps and the legacy board) still has 37 hexes.
            Assert.AreEqual(37, new MapDefinition("r3", "r3", "").TileCount);
            Assert.AreEqual(37, Hex.Spiral(Hex.Zero, 3).Count);
            CollectionAssert.AreEquivalent(Hex.Spiral(Hex.Zero, 3), new MapDefinition("r3", "r3", "").AllHexes);
        }

        [Test]
        public void Hex_NeighborsAndDistance()
        {
            var h = new Hex(1, -1);
            var n = h.Neighbors().ToList();
            Assert.AreEqual(6, n.Count);
            Assert.IsTrue(n.All(x => Hex.Distance(h, x) == 1));
            Assert.AreEqual(6, n.Distinct().Count());
            Assert.AreEqual(3, Hex.Distance(new Hex(0, 0), new Hex(3, -3)));
            Assert.AreEqual(6, Hex.Distance(new Hex(-3, 3), new Hex(3, -3)));
            Assert.AreEqual(4, Hex.Distance(new Hex(-2, 0), new Hex(2, 0)));
            Assert.AreEqual(Hex.Distance(new Hex(2, 1), new Hex(-1, -2)), Hex.Distance(new Hex(-1, -2), new Hex(2, 1)));
        }

        [Test]
        public void Hex_RingSizes()
        {
            Assert.AreEqual(1, Hex.Ring(Hex.Zero, 0).Count);
            Assert.AreEqual(6, Hex.Ring(Hex.Zero, 1).Count);
            Assert.AreEqual(12, Hex.Ring(Hex.Zero, 2).Count);
            Assert.AreEqual(18, Hex.Ring(Hex.Zero, 3).Count);
        }

        [Test]
        public void Hex_LineIsContiguousAndDeterministic()
        {
            var a = new Hex(-3, 3); var b = new Hex(3, -2);
            for (int nudge = -1; nudge <= 1; nudge += 2)
            {
                var line = Hex.Line(a, b, nudge);
                Assert.AreEqual(Hex.Distance(a, b) + 1, line.Count);
                Assert.AreEqual(a, line[0]); Assert.AreEqual(b, line[line.Count - 1]);
                for (int i = 1; i < line.Count; i++) Assert.AreEqual(1, Hex.Distance(line[i - 1], line[i]));
                CollectionAssert.AreEqual(line, Hex.Line(a, b, nudge));
            }
        }

        [Test]
        public void Board_Boundaries()
        {
            var map = MapLibrary.Get(MapLibrary.DefaultMapId);
            Assert.IsTrue(map.Contains(new Hex(7, 0)) && map.Contains(new Hex(-7, 0)), "centre row is 15 wide");
            Assert.IsTrue(map.Contains(new Hex(-7, 4)) && map.Contains(new Hex(3, 4)), "back row spans q -7..3");
            Assert.IsTrue(map.Contains(new Hex(-3, -4)) && map.Contains(new Hex(7, -4)));
            Assert.IsFalse(map.Contains(new Hex(8, 0)));
            Assert.IsFalse(map.Contains(new Hex(4, 4)));
            Assert.IsFalse(map.Contains(new Hex(-4, -4)));
            Assert.IsFalse(map.Contains(new Hex(0, 5)), "nine rows deep");
            Assert.IsFalse(map.Contains(new Hex(7, 1)));
            Assert.AreEqual(TerrainType.Obstacle, map.Terrain(new Hex(9, 9)), "Off-board hexes are never passable.");
        }

        [Test]
        public void Maps_ArePointSymmetric_AndDeploymentZonesAreFair()
        {
            foreach (var map in MapLibrary.Maps.Values.Where(m => !m.Id.StartsWith("test")))
            {
                Assert.IsTrue(map.IsPointSymmetric(), map.Id);
                var z0 = map.DeploymentZone(0); var z1 = map.DeploymentZone(1);
                Assert.AreEqual(z0.Count, z1.Count, map.Id);
                Assert.GreaterOrEqual(z0.Count, 7, $"{map.Id}: zone must fit the 7-unit cap");
                Assert.IsTrue(z0.All(h => h.R >= map.DeploymentEdge) && z1.All(h => h.R <= -map.DeploymentEdge));
                Assert.IsFalse(z0.Intersect(z1).Any());
                Assert.IsTrue(z0.All(h => map.Terrain(h) != TerrainType.Obstacle && map.Terrain(h) != TerrainType.Hazard));
            }
        }

        [Test]
        public void LineOfSight_IsSymmetricOnEveryMap()
        {
            foreach (var map in MapLibrary.Maps.Values)
            {
                var s = new MatchState { Config = new MatchConfig { MapId = map.Id } };
                foreach (var a in map.AllHexes)
                foreach (var b in map.AllHexes)
                    Assert.AreEqual(BoardQueries.LineOfSight(s, a, b, true) == LosState.Clear,
                                    BoardQueries.LineOfSight(s, b, a, true) == LosState.Clear, $"{map.Id} {a}->{b}");
            }
        }
    }

    public class ArmyTests
    {
        static ArmyRoster Std(FactionId f) => ArmyPresets.Default(f).Clone();

        [Test]
        public void AllPresets_AreLegalInTheirMode()
        {
            foreach (var f in GameContent.AllFactions)
                foreach (var army in ArmyPresets.For(f))
                {
                    var r = ArmyRules.Validate(army, new TechCaps(3));
                    Assert.IsTrue(r.Valid, $"{f} {army.Name}: {string.Join("; ", r.Errors)}");
                    Assert.LessOrEqual(r.TotalCost, 18);
                }
        }

        [Test]
        public void EveryFactionHasAllSixArchetypes()
        {
            foreach (var f in GameContent.AllFactions)
                foreach (var a in GameContent.AllArchetypes)
                    Assert.IsNotNull(GameContent.UnitFor(f, a), $"{f} {a}");
        }

        [Test]
        public void PointBudget_IsEnforced()
        {
            var army = Std(FactionId.Craigon);
            army.Units.Add(new RosterEntry("cra.infantry"));
            var r = ArmyRules.Validate(army, new TechCaps(3));
            Assert.IsFalse(r.Valid);
            Assert.That(r.Errors.Any(e => e.Contains("Over budget")));
            Assert.AreEqual(20, r.TotalCost);
        }

        [Test]
        public void SevenUnitCap_IsEnforced()
        {
            var army = new ArmyRoster { Faction = FactionId.Inhumanus, Doctrine = Doctrine.Assault };
            army.Units.Add(new RosterEntry("inh.commander"));
            for (int i = 0; i < 7; i++) army.Units.Add(new RosterEntry("inh.infantry"));
            var r = ArmyRules.Validate(army, new TechCaps(3));
            Assert.AreEqual(12, r.TotalCost);
            Assert.That(r.Errors.Any(e => e.Contains("Too many units")));
            army.Units.RemoveAt(7);
            Assert.IsTrue(ArmyRules.Validate(army, new TechCaps(3)).Valid);
        }

        [Test]
        public void ExactlyOneCommander_IsRequired()
        {
            var none = new ArmyRoster { Faction = FactionId.Meeples, Units = { new RosterEntry("mee.infantry") } };
            Assert.That(ArmyRules.Validate(none, new TechCaps(3)).Errors.Any(e => e.Contains("exactly one Commander")));
            var two = new ArmyRoster { Faction = FactionId.Meeples, Units = { new RosterEntry("mee.commander"), new RosterEntry("mee.commander") } };
            Assert.That(ArmyRules.Validate(two, new TechCaps(3)).Errors.Any(e => e.Contains("Only one Commander")));
        }

        [Test]
        public void ArmiesWithoutOptionalArchetypes_AreLegal()
        {
            var army = new ArmyRoster { Faction = FactionId.Pygmaei, Units = { new RosterEntry("pyg.commander"), new RosterEntry("pyg.raider"), new RosterEntry("pyg.raider") } };
            Assert.IsTrue(ArmyRules.Validate(army, new TechCaps(3)).Valid);
        }

        [Test]
        public void EquipmentTierCaps_AreEnforcedPerCategory()
        {
            var army = Std(FactionId.Pygmaei);
            army.Units[0] = new RosterEntry("pyg.commander");
            army.Units[1] = new RosterEntry("pyg.guardian");
            army.Units[2] = new RosterEntry("pyg.gunner", "pyg.w.hellforge_cannon"); // Tier V weapon (+3)
            army.Units.RemoveAt(5); army.Units.RemoveAt(4);                          // free budget
            var balanced = ArmyRules.Validate(army, new TechCaps(3));
            Assert.That(balanced.Errors.Any(e => e.Contains("Tier V exceeds")));
            var asym = ArmyRules.Validate(army, new TechCaps(5, 1, 1, 1));            // Tier V weapons, Tier I elsewhere
            Assert.IsTrue(asym.Valid, string.Join(";", asym.Errors));
        }

        [Test]
        public void VehicleChassis_RequiresVehicleTech()
        {
            var army = ArmyPresets.For(FactionId.Craigon).First(a => a.Units.Any(u => u.UnitId == "cra.vehicle")).Clone();
            Assert.IsTrue(ArmyRules.Validate(army, new TechCaps(3)).Valid);
            Assert.That(ArmyRules.Validate(army, new TechCaps(3, 3, 3, 1)).Errors.Any(e => e.Contains("Vehicle technology")));
        }

        [Test]
        public void EquipmentSlotsFactionAndArchetype_AreEnforced()
        {
            var army = Std(FactionId.Meeples);
            army.Units[1] = new RosterEntry("mee.guardian", "sil.w.geode_maul");           // wrong faction
            Assert.That(ArmyRules.Validate(army, new TechCaps(5)).Errors.Any(e => e.Contains("Silex technology")));
            army.Units[1] = new RosterEntry("mee.guardian", "gen.w.long_lance");           // Gunner-only weapon
            Assert.That(ArmyRules.Validate(army, new TechCaps(5)).Errors.Any(e => e.Contains("cannot be mounted")));
            army.Units[1] = new RosterEntry("mee.guardian", "gen.d.plating", "gen.d.ablative"); // two defense items
            Assert.That(ArmyRules.Validate(army, new TechCaps(5)).Errors.Any(e => e.Contains("only one Defense")));
        }

        [Test]
        public void Inhumanus_DiscountAndDoctrineEligibility()
        {
            Assert.AreEqual(1, ArmyRules.EquipmentCost(GameContent.Equip("inh.w.phase_rifle"), FactionId.Inhumanus));
            Assert.AreEqual(1, ArmyRules.EquipmentCost(GameContent.Equip("gen.d.plating"), FactionId.Inhumanus), "Tier I is not discounted");
            Assert.AreEqual(1, ArmyRules.EquipmentCost(GameContent.Equip("gen.w.marksman_rifle"), FactionId.Inhumanus), "Tier II is not discounted");
            var army = new ArmyRoster { Faction = FactionId.Inhumanus, Doctrine = Doctrine.Defense,
                Units = { new RosterEntry("inh.commander", "inh.w.singularity_ledger") } };
            Assert.That(ArmyRules.Validate(army, new TechCaps(5)).Errors.Any(e => e.Contains("Assault doctrine")));
            army.Doctrine = Doctrine.Assault;
            Assert.IsTrue(ArmyRules.Validate(army, new TechCaps(5)).Valid);
            army.Doctrine = Doctrine.None;
            Assert.That(ArmyRules.Validate(army, new TechCaps(5)).Errors.Any(e => e.Contains("Corporate Doctrine")));
        }

        [Test]
        public void Equipment_ModifiesRealStats()
        {
            var plain = UnitFactory.Create(1, 0, new RosterEntry("mee.infantry"), Doctrine.None);
            var kitted = UnitFactory.Create(2, 0, new RosterEntry("mee.infantry", "gen.w.marksman_rifle", "gen.d.plating"), Doctrine.None);
            Assert.AreEqual(plain.MaxHp + 1, kitted.MaxHp);
            Assert.AreEqual(3, kitted.Weapon.MaxRange);
            var gunner = UnitFactory.Create(3, 0, new RosterEntry("mee.gunner", "gen.u.targeting_array"), Doctrine.None);
            Assert.AreEqual(4, gunner.Weapon.MaxRange);
            var vehicle = UnitFactory.Create(4, 0, new RosterEntry("mee.vehicle", "gen.v.heavy_ordnance"), Doctrine.None);
            Assert.AreEqual(3, vehicle.Weapon.Damage);
            Assert.AreEqual(1, vehicle.BaseMove);
        }
    }

    public class MovementAndSightTests
    {
        [Test]
        public void Movement_RespectsAllowance()
        {
            var b = new Scenario();
            int inf = b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 0);
            var s = b.Build();
            var u = s.Unit(b.Id(inf));
            var reach = BoardQueries.Reachable(s, u);
            Assert.AreEqual(6, reach.Count);
            StringAssert.Contains("Too far", Scenario.Rejected(s, Command.Move(0, u.Id, new Hex(0, 2))));
            Scenario.Ok(s, Command.Move(0, u.Id, new Hex(1, 0)));
            Assert.AreEqual(new Hex(1, 0), u.Position);
        }

        [Test]
        public void Movement_PassesFriendsButNotEnemies_AndNeverStacks()
        {
            var b = new Scenario();
            int raider = b.Add(0, FactionId.Meeples, Archetype.Raider, 0, 0);
            int friend = b.Add(0, FactionId.Meeples, Archetype.Infantry, 1, 0);
            var s = b.Build();
            Assert.IsTrue(BoardQueries.Reachable(s, s.Unit(b.Id(raider))).ContainsKey(new Hex(2, 0)), "may pass through an ally");
            StringAssert.Contains("Another of your units", Scenario.Rejected(s, Command.Move(0, b.Id(raider), new Hex(1, 0))));

            var b2 = new Scenario();
            int r2 = b2.Add(0, FactionId.Meeples, Archetype.Raider, 0, 0);
            b2.Add(1, FactionId.Pygmaei, Archetype.Infantry, 1, 0);
            var s2 = b2.Build();
            Assert.IsFalse(BoardQueries.Reachable(s2, s2.Unit(b2.Id(r2))).ContainsKey(new Hex(2, 0)), "cannot pass through an enemy");
        }

        [Test]
        public void Obstacles_AreImpassable()
        {
            var b = new Scenario(Scenario.LosMap);
            int raider = b.Add(0, FactionId.Meeples, Archetype.Raider, 1, 0);
            var s = b.Build();
            Assert.IsFalse(BoardQueries.Reachable(s, s.Unit(b.Id(raider))).ContainsKey(new Hex(2, 0)));
            StringAssert.Contains("Impassable", Scenario.Rejected(s, Command.Move(0, b.Id(raider), new Hex(2, 0))));
        }

        [Test]
        public void Elevated_CostsTwo_ButASingleStepIsAlwaysAllowed()
        {
            var b = new Scenario(Scenario.LosMap);
            int inf = b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 3);
            int raider = b.Add(0, FactionId.Meeples, Archetype.Raider, 1, 2);
            var s = b.Build();
            Assert.IsTrue(BoardQueries.Reachable(s, s.Unit(b.Id(inf))).ContainsKey(new Hex(0, 2)), "move-1 unit can still climb one hex");
            var r = BoardQueries.Reachable(s, s.Unit(b.Id(raider)));
            Assert.AreEqual(2, r[new Hex(0, 2)].Cost);
            Assert.IsFalse(r.ContainsKey(new Hex(-1, 2)), "climbing consumed the whole allowance");
        }

        [Test]
        public void LineOfSight_UnitsBlockDirectFire_NotIndirectOrPhase()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 1);
            int target = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var s = b.Build();
            var o = Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(0, -1));
            Assert.IsFalse(o.Legal);
            Assert.AreEqual(LosState.BlockedByUnit, o.Los);

            var bp = new Scenario();
            int phase = bp.Add(0, FactionId.Inhumanus, Archetype.Gunner, 0, 2, "inh.w.phase_rifle");
            bp.Add(0, FactionId.Inhumanus, Archetype.Infantry, 0, 1);
            bp.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var sp = bp.Build();
            Assert.IsTrue(Combat.Resolve(sp, sp.Unit(bp.Id(phase)), new Hex(0, -1)).Legal, "phase rifle ignores units");

            var bi = new Scenario();
            int mortar = bi.Add(0, FactionId.Pygmaei, Archetype.Vehicle, 0, 2);
            bi.Add(0, FactionId.Pygmaei, Archetype.Infantry, 0, 1);
            bi.Add(1, FactionId.Meeples, Archetype.Infantry, 0, 0);
            var si = bi.Build();
            Assert.IsTrue(Combat.Resolve(si, si.Unit(bi.Id(mortar)), new Hex(0, 0)).Legal, "indirect fire ignores line of sight");
        }

        [Test]
        public void LineOfSight_TerrainBlocks_AndElevationSeesOverUnits()
        {
            var b = new Scenario(Scenario.LosMap);
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 3, 0);
            b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 1, 0);
            var s = b.Build();
            var o = Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(1, 0));
            Assert.AreEqual(LosState.BlockedByTerrain, o.Los);

            var b2 = new Scenario(Scenario.LosMap);
            int perched = b2.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 2);
            b2.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 1);
            b2.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var s2 = b2.Build();
            var o2 = Combat.Resolve(s2, s2.Unit(b2.Id(perched)), new Hex(0, -1));
            Assert.IsTrue(o2.Legal, o2.Reason);
            Assert.AreEqual(4, BoardQueries.WeaponRange(s2, s2.Unit(b2.Id(perched)), new Hex(0, 2)).max, "high ground extends ranged weapons");
        }

        [Test]
        public void AttackRange_MinAndMax()
        {
            var b = new Scenario();
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 0);
            b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 1, 0);
            b.Add(1, FactionId.Pygmaei, Archetype.Infantry, -3, 0);
            b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -3);
            var s = b.Build();
            var u = s.Unit(b.Id(gun));
            StringAssert.Contains("Too close", Combat.Resolve(s, u, new Hex(1, 0)).Reason);
            Assert.IsTrue(Combat.Resolve(s, u, new Hex(-3, 0)).Legal);
            Assert.IsTrue(Combat.Resolve(s, u, new Hex(0, -3)).Legal);
            var b2 = new Scenario();
            int gun2 = b2.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 3);
            b2.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var s2 = b2.Build();
            StringAssert.Contains("Out of range", Combat.Resolve(s2, s2.Unit(b2.Id(gun2)), new Hex(0, -1)).Reason);
        }

        [Test]
        public void Hazard_DamagesUnitsThatEndTheirMoveThere()
        {
            var b = new Scenario(Scenario.LosMap);
            int inf = b.Add(0, FactionId.Meeples, Archetype.Infantry, -2, 2);
            var s = b.Build();
            // (-2,2) is next to the hazard at (-3,2).
            var u = s.Unit(b.Id(inf));
            Scenario.Ok(s, Command.Move(0, u.Id, new Hex(-3, 2)));
            Assert.AreEqual(u.MaxHp - 1, u.Hp);
        }
    }
}
