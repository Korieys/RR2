using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    /// <summary>The topology detector itself, on small synthetic layouts with known answers.</summary>
    public class TopologyDetectorTests
    {
        /// <summary>Detector-only thresholds: the small boards have no room for three approaches or seven-unit zones.</summary>
        static readonly TopologyRules Small = new TopologyRules
        {
            RequireApproaches = false, MinDeployable = 0, MinTraversableFraction = 0, PocketFraction = 0.25,
        };

        static MapDefinition R3(params (int q, int r)[] obstacles)
        {
            var m = new MapDefinition("t", "t", "");
            m.Set(TerrainType.Obstacle, obstacles.Select(o => new Hex(o.q, o.r)).ToArray());
            return m;
        }

        static TopologyReport Run(MapDefinition m) => MapTopology.Analyze(m, MovementProfile.Ground, Small);

        [Test]
        public void CleanBoards_HaveNoDeadEnds_CornersAndEdgesIncluded()
        {
            var r = Run(new MapDefinition("t", "t", ""));
            Assert.AreEqual(0, r.DeadEnds.Count);
            Assert.AreEqual(0, r.ArticulationPoints.Count);
            Assert.AreEqual(0, r.TerminalHexes.Count);
            var big = MapTopology.Analyze(new MapDefinition("t", "t", "", 7, 4), MovementProfile.Ground);
            Assert.AreEqual(0, big.DeadEnds.Count, "corner hexes have three contiguous exits: not a dead end");
            Assert.IsTrue(big.Passed, string.Join("\n", big.AllErrors()));
        }

        [Test]
        public void TerminalCell_WithOneExit()
        {
            var r = Run(R3((3, -2), (2, -3)));
            Assert.AreEqual(1, r.DeadEnds.Count, r.Describe(R3((3, -2), (2, -3))));
            Assert.AreEqual(DeadEndKind.TerminalCell, r.DeadEnds[0].Kind);
            CollectionAssert.AreEqual(new[] { new Hex(3, -3) }, r.DeadEnds[0].Hexes);
            CollectionAssert.Contains(r.TerminalHexes, new Hex(3, -3));
            CollectionAssert.Contains(r.ArticulationPoints, new Hex(2, -2));
            Assert.IsTrue(r.Errors.Any(e => e.Contains("Accidental dead end")));
        }

        [Test]
        public void Nook_WithTwoAdjacentExits_IsADeadEnd()
        {
            // (3,-3) keeps two exits, (2,-2) and (2,-3), which touch each other: one mouth, one way back out.
            var r = Run(R3((3, -2)));
            Assert.AreEqual(1, r.DeadEnds.Count);
            Assert.AreEqual(DeadEndKind.TerminalCell, r.DeadEnds[0].Kind);
            Assert.AreEqual(2, r.DeadEnds[0].Entrance.Count);
            Assert.AreEqual(0, r.TerminalHexes.Count, "no hex has a single neighbour: degree alone would miss it");
        }

        [Test]
        public void MultiHexPocket_IsFound_EvenThoughItsHexesInterconnect()
        {
            // A triangle of mutually adjacent hexes walled in mid-board, reachable only through (0,2).
            var m = new MapDefinition("t", "t", "", 7, 4);
            var pocket = new[] { new Hex(0, 0), new Hex(1, 0), new Hex(0, 1) };
            var walls = pocket.SelectMany(h => h.Neighbors()).Distinct().Where(h => !pocket.Contains(h) && h != new Hex(0, 2)).ToArray();
            Assert.AreEqual(8, walls.Length);
            m.Set(TerrainType.Obstacle, walls);
            var r = MapTopology.Analyze(m, MovementProfile.Ground);
            Assert.AreEqual(1, r.DeadEnds.Count, r.Describe(m));
            var d = r.DeadEnds[0];
            Assert.AreEqual(DeadEndKind.Pocket, d.Kind);
            CollectionAssert.AreEquivalent(pocket, d.Hexes);
            CollectionAssert.AreEqual(new[] { new Hex(0, 2) }, d.Entrance);
            Assert.IsFalse(r.ArticulationPoints.Count == 0);
            Assert.AreEqual(0, r.TerminalHexes.Count, "every pocket hex has two or more neighbours");
        }

        [Test]
        public void Corridor_LeadingNowhere_IsOneRegion()
        {
            // A wall one row in from the south edge turns the edge row into a single-file corridor.
            var m = R3((-2, 2), (-1, 2), (0, 2), (1, 2), (-2, 1));
            var r = Run(m);
            Assert.AreEqual(1, r.DeadEnds.Count, r.Describe(m));
            var d = r.DeadEnds[0];
            Assert.AreEqual(DeadEndKind.Corridor, d.Kind);
            foreach (var h in new[] { new Hex(-3, 2), new Hex(-3, 3), new Hex(-2, 3), new Hex(-1, 3), new Hex(0, 3) })
                CollectionAssert.Contains(d.Hexes, h);
        }

        [Test]
        public void ThreeWideMouth_IsNotADeadEnd()
        {
            // The centre hex walled on three sides keeps three contiguous exits: a recess, not a dead end.
            var m = R3((1, 0), (0, 1), (-1, 1));
            var r = Run(m);
            Assert.AreEqual(0, r.DeadEnds.Count, r.Describe(m));
            Assert.AreEqual(0, r.Errors.Count, string.Join("; ", r.Errors));
        }

        [Test]
        public void Narrows_AreLegitimateChokepoints_NotDeadEnds()
        {
            var m = R3((-1, 0), (1, 0));
            var r = Run(m);
            Assert.AreEqual(0, r.DeadEnds.Count);
            Assert.IsTrue(r.Chokepoints.Any(c => c.Kind == ChokeKind.Narrows && c.Hexes.Contains(Hex.Zero)));
            Assert.IsFalse(r.Chokepoints.Any(c => c.Kind == ChokeKind.Mandatory));
            Assert.AreEqual(0, r.Errors.Count, string.Join("\n", r.Errors));
        }

        [Test]
        public void MandatorySingleHexPassage_IsRejected()
        {
            var m = R3((-3, 0), (-2, 0), (-1, 0), (1, 0), (2, 0), (3, 0));
            var r = Run(m);
            CollectionAssert.Contains(r.ArticulationPoints, Hex.Zero);
            Assert.IsTrue(r.Chokepoints.Any(c => c.Kind == ChokeKind.Mandatory && c.Hexes.SequenceEqual(new[] { Hex.Zero })), r.Describe(m));
            Assert.IsFalse(r.DeadEnds.Any(d => d.Hexes.Count > 3), "a cut between two halves is a chokepoint problem, not a pocket");
            Assert.IsTrue(r.Errors.Any(e => e.Contains("Single narrow passage")));
        }

        [Test]
        public void EnclosedHex_IsUnreachable()
        {
            var ring = Hex.Ring(Hex.Zero, 1).Select(h => (h.Q, h.R)).ToArray();
            var r = Run(R3(ring));
            CollectionAssert.AreEqual(new[] { Hex.Zero }, r.Unreachable);
            Assert.AreEqual(2, r.Components.Count);
            Assert.IsTrue(r.Errors.Any(e => e.Contains("cannot be reached")));
        }

        [Test]
        public void IrregularOutline_TrimmedCorner_CreatesNooks()
        {
            // Trimming a hex beside a corner leaves that corner with two adjacent exits (and its mirror likewise).
            var m = new MapDefinition("t", "t", "").Trim((3, -2));
            Assert.AreEqual(35, m.TileCount);
            Assert.IsFalse(m.IsFullOutline);
            var r = Run(m);
            Assert.AreEqual(2, r.DeadEnds.Count, r.Describe(m));
            CollectionAssert.AreEquivalent(new[] { new Hex(3, -3), new Hex(-3, 3) }, r.DeadEnds.SelectMany(d => d.Hexes));
            // A trimmed corner (the corner hex itself) leaves a clean outline.
            var clean = MapTopology.Analyze(new MapDefinition("t", "t", "", 7, 4).Trim((7, 0), (3, 4)), MovementProfile.Ground);
            Assert.AreEqual(0, clean.DeadEnds.Count);
            Assert.IsTrue(clean.Passed, string.Join("\n", clean.AllErrors()));
        }

        [Test]
        public void LargeFootprint_InTheCentre_KeepsTheBoardValid()
        {
            var m = new MapDefinition("t", "t", "", 7, 4);
            m.Set(TerrainType.Obstacle, Hex.Spiral(Hex.Zero, 1).ToArray());
            var r = TopologyValidator.Validate(m);
            Assert.IsTrue(r.Passed, r.Describe(m));
            Assert.AreEqual(0, r.DeadEnds.Count);
            // One hex in from the edge it leaves a single-file path round the outside: narrows, but still no dead end.
            var near = new MapDefinition("t", "t", "", 7, 4);
            near.Set(TerrainType.Obstacle, Hex.Spiral(new Hex(5, 0), 1).ToArray());
            var rn = TopologyValidator.Validate(near);
            Assert.AreEqual(0, rn.DeadEnds.Count, rn.Describe(near));
            Assert.IsTrue(rn.Chokepoints.Any(c => c.Kind == ChokeKind.Narrows && c.Hexes.Contains(new Hex(7, 0))));
            // Against the edge it pinches off the hexes beside it.
            var edge = new MapDefinition("t", "t", "", 7, 4);
            edge.Set(TerrainType.Obstacle, Hex.Spiral(new Hex(6, 0), 1).ToArray());
            var re = TopologyValidator.Validate(edge);
            Assert.IsFalse(re.Passed, re.Describe(edge));
            Assert.IsTrue(re.DeadEnds.Any(d => d.Hexes.SequenceEqual(new[] { new Hex(7, -2) })), re.Describe(edge));
            Assert.IsTrue(re.DeadEnds.Any(d => d.Hexes.SequenceEqual(new[] { new Hex(5, 2) })), re.Describe(edge));
        }

        [Test]
        public void HazardRing_SealsSafeGround_ForHazardAverseUnitsOnly()
        {
            var m = new MapDefinition("t", "t", "", 7, 4);
            m.Set(TerrainType.Hazard, Hex.Ring(Hex.Zero, 1).ToArray());
            var r = TopologyValidator.Validate(m);
            Assert.AreEqual(0, r.Unreachable.Count, "ground units walk through conduits");
            var averse = r.Profiles.Single(p => p.Profile == MovementProfile.HazardAverse.Name);
            CollectionAssert.AreEqual(new[] { Hex.Zero }, averse.Unreachable);
            Assert.IsFalse(r.Passed);
            Assert.AreEqual(r.Unreachable.Count, r.Profiles.Single(p => p.Profile == MovementProfile.Hover.Name).Unreachable.Count);
        }

        [Test]
        public void Repair_MovesOrRemovesTheOffendingObstacle_Deterministically()
        {
            // Citadel plus a rock pair that seals the south-west corner hex.
            var broken = MapLibrary.Get(MapLibrary.DefaultMapId).Clone().Mirror(TerrainType.Obstacle, (-6, 3), (-7, 3));
            var before = TopologyValidator.Validate(broken);
            Assert.IsFalse(before.Passed);
            Assert.IsTrue(before.DeadEnds.Any(d => d.Hexes.Contains(new Hex(-7, 4))));
            var fixedMap = TopologyValidator.Repair(broken, out var after);
            Assert.IsTrue(after.Passed, after.Describe(fixedMap));
            Assert.IsNotEmpty(fixedMap.RepairLog);
            Assert.IsTrue(fixedMap.IsPointSymmetric(), "repairs are mirrored");
            var again = TopologyValidator.Repair(broken, out _);
            Assert.AreEqual(fixedMap.LayoutSignature(), again.LayoutSignature(), "deterministic");
            Assert.AreEqual(TerrainType.Obstacle, broken.Terrain(new Hex(-7, 3)), "the source layout is not modified");
        }

        [Test]
        public void BlockedSpawningRegion_IsRepaired_ByClearingConduits()
        {
            // All but six deployment hexes per side are unstable conduits: too few places to deploy seven units.
            var m = new MapDefinition("t", "t", "", 7, 4);
            m.Mirror(TerrainType.Hazard, m.AllHexes.Where(h => m.ZoneSeat(h) == 0).Skip(6).Select(h => (h.Q, h.R)).ToArray());
            var before = TopologyValidator.Validate(m);
            Assert.IsFalse(before.Passed);
            Assert.AreEqual(6, before.DeployableHexes[0]);
            var fixedMap = TopologyValidator.Repair(m, out var after);
            Assert.IsTrue(after.Passed, after.Describe(fixedMap));
            Assert.GreaterOrEqual(fixedMap.DeploymentZone(0).Count, 7);
            Assert.IsTrue(fixedMap.RepairLog.All(l => l.Contains("Hazard")), string.Join("; ", fixedMap.RepairLog));
        }

        [Test]
        public void UnrepairableLayout_IsRejected_AndNoBattleStartsOnIt()
        {
            // The outline itself leaves each side five deployment hexes: no blocker to move, so it cannot be repaired.
            var m = new MapDefinition("test_reject", "Reject", "", 7, 4);
            m.Trim(m.AllHexes.Where(h => h.R == 4 || (h.R == 3 && h.Q > -3)).Select(h => (h.Q, h.R)).ToArray());
            Assert.AreEqual(5, m.DeploymentZone(0).Count);
            try
            {
                var reg = MapLibrary.Register(m);
                Assert.IsFalse(reg.Topology.Passed);
                Assert.IsTrue(reg.RepairLog.Last().StartsWith("Rejected"), string.Join("; ", reg.RepairLog));
                var cfg = Scenario.PresetConfig(FactionId.Meeples, FactionId.Silex, "test_reject");
                Assert.IsNull(RulesEngine.CreateMatch(cfg, out var err));
                StringAssert.Contains("failed validation", err);
            }
            finally { MapLibrary.Maps.Remove("test_reject"); }
        }

        [Test]
        public void StaticPaths_GoAroundWalls()
        {
            // A wall across the middle row with one gap at the east end.
            var m = new MapDefinition("t", "t", "", 7, 4);
            m.Set(TerrainType.Obstacle, Enumerable.Range(-7, 12).Select(q => new Hex(q, 0)).ToArray());
            var a = new Hex(0, 2); var b = new Hex(0, -2);
            Assert.AreEqual(4, Hex.Distance(a, b));
            int route = m.Paths.Distance(a, b);
            Assert.Greater(route, 4);
            // Breadth-first check of the same route.
            var dist = new Dictionary<Hex, int> { [a] = 0 };
            var q = new Queue<Hex>(); q.Enqueue(a);
            while (q.Count > 0) { var h = q.Dequeue(); foreach (var n in h.Neighbors()) if (m.Contains(n) && m.Terrain(n) != TerrainType.Obstacle && !dist.ContainsKey(n)) { dist[n] = dist[h] + 1; q.Enqueue(n); } }
            Assert.AreEqual(dist[b], route);
        }
    }

    /// <summary>The authored battlefields against the acceptance criteria of the 0.14 expansion.</summary>
    public class BattlefieldTests
    {
        static IEnumerable<MapDefinition> Authored() => MapLibrary.Maps.Values.Where(m => !m.Id.StartsWith("test")).OrderBy(m => m.Id);

        [Test]
        public void EveryAuthoredMap_PassesTopologyValidation_AsAuthored()
        {
            foreach (var m in Authored())
            {
                Assert.IsNotNull(m.Topology, m.Id);
                Assert.IsTrue(m.Topology.Passed, m.Topology.Describe(m));
                Assert.IsEmpty(m.RepairLog, $"{m.Id} needed repairs: {string.Join("; ", m.RepairLog)}");
                Assert.IsTrue(m.IsPointSymmetric(), m.Id);
            }
        }

        [Test]
        public void Size_IsAboutThreeTimesTheOriginalTraversableArea()
        {
            foreach (var legacy in LegacyMaps.All())
            {
                var now = MapLibrary.Get(legacy.Id);
                Assert.AreEqual(legacy.Id, now.Id);
                int before = MapTopology.Analyze(legacy, MovementProfile.Ground, LegacyMaps.Rules, full: false).TraversableCount;
                int after = now.Topology.TraversableCount;
                Assert.GreaterOrEqual(after, 3 * before, $"{now.Id}: {before} -> {after}");
                Assert.LessOrEqual(after, 3.5 * before, $"{now.Id}: tripled, not nine times: {before} -> {after}");
            }
        }

        [Test]
        public void DeadEnds_AtMostTwo_AndAllIntentional()
        {
            foreach (var m in Authored())
            {
                Assert.LessOrEqual(m.Topology.DeadEnds.Count, 2, m.Topology.Describe(m));
                Assert.IsTrue(m.Topology.DeadEnds.All(d => d.Intentional), m.Topology.Describe(m));
            }
            Assert.AreEqual(0, MapLibrary.Get(MapLibrary.DefaultMapId).Topology.DeadEnds.Count, "the default battlefield has none");
            Assert.AreEqual(0, MapLibrary.Get("proving_ground").Topology.DeadEnds.Count);
            var cross = MapLibrary.Get("citadel_crossroads").Topology;
            Assert.AreEqual(2, cross.DeadEnds.Count, "Crossroads' two walled alcoves");
        }

        [Test]
        public void Connectivity_OneRegion_ForEveryMovementProfile()
        {
            foreach (var m in Authored())
                foreach (var p in MovementProfile.All)
                {
                    var r = MapTopology.Analyze(m, p, full: false);
                    Assert.AreEqual(1, r.Components.Count, $"{m.Id} {p}");
                    Assert.IsEmpty(r.Unreachable, $"{m.Id} {p}");
                }
        }

        [Test]
        public void RouteDiversity_ThreeBroadApproaches_WithCrossConnections()
        {
            foreach (var m in Authored())
            {
                var r = m.Topology;
                Assert.AreEqual(3, r.Approaches.Count);
                foreach (var a in r.Approaches)
                {
                    Assert.IsTrue(a.Connects, $"{m.Id} {a}");
                    Assert.GreaterOrEqual(a.Width, 2, $"{m.Id} {a}");
                }
                int middleRows = m.AllHexes.Where(h => m.ZoneSeat(h) < 0).Select(h => h.R).Distinct().Count();
                foreach (int links in r.CrossLinks) Assert.GreaterOrEqual(links, middleRows - 1, $"{m.Id}: approaches connect on (almost) every row of no-man's land");
                Assert.IsFalse(r.Chokepoints.Any(c => c.Kind == ChokeKind.Mandatory), m.Id);
                Assert.IsEmpty(r.ArticulationPoints, $"{m.Id}: no single hex whose loss splits the board");
                Assert.GreaterOrEqual(r.ZoneRoutes, 9, m.Id);
            }
        }

        [Test]
        public void Density_MostlyOpen_BlockersSpreadOut()
        {
            foreach (var m in Authored())
            {
                Assert.GreaterOrEqual(m.Topology.TraversableFraction, 0.85, m.Id);
                var blockers = m.AllHexes.Where(h => TerrainRules.BlocksMovement(m.Terrain(h))).ToList();
                Assert.LessOrEqual(blockers.Count, 7, m.Id);
                var central = blockers.Where(h => h.Length <= 2).ToList();
                Assert.LessOrEqual(central.Count, 2, $"{m.Id}: no cluster of blockers in the centre");
                Assert.IsFalse(central.Any(a => central.Any(b => Hex.Distance(a, b) == 1)), $"{m.Id}: central blockers stand apart");
                Assert.GreaterOrEqual(blockers.Select(h => h.Lateral < -4 ? 0 : h.Lateral > 4 ? 2 : 1).Distinct().Count(), 2, $"{m.Id}: blockers spread across the lanes");
                // No blocking cluster larger than three hexes (separate rocks never merge into walls).
                var seen = new HashSet<Hex>();
                foreach (var b in blockers)
                {
                    if (!seen.Add(b)) continue;
                    int size = 1; var stack = new Stack<Hex>(); stack.Push(b);
                    while (stack.Count > 0) foreach (var n in stack.Pop().Neighbors()) if (blockers.Contains(n) && seen.Add(n)) { size++; stack.Push(n); }
                    Assert.LessOrEqual(size, 3, $"{m.Id}: blocking cluster at {b}");
                }
                // Scenery is decoration only: never on blocking hexes, and it changes nothing in the rules.
                Assert.IsTrue(m.Scenery.All(sc => !TerrainRules.BlocksMovement(m.Terrain(sc.At))), m.Id);
            }
        }

        [Test]
        public void Deployment_BothArmiesDeployLegally_OnEveryMap()
        {
            var factions = new[] { FactionId.Craigon, FactionId.Silex, FactionId.Pygmaei, FactionId.Meeples, FactionId.Inhumanus, FactionId.SpacePirates };
            foreach (var m in Authored())
            {
                for (int seat = 0; seat < 2; seat++)
                {
                    var zone = m.DeploymentZone(seat);
                    Assert.GreaterOrEqual(zone.Count, 7, m.Id);
                    Assert.IsTrue(zone.All(h => TerrainRules.Deployable(m.Terrain(h))));
                }
                foreach (var a in factions)
                {
                    var b = factions[(System.Array.IndexOf(factions, a) + 2) % factions.Length];
                    var s = Scenario.DeployedPreset(a, b, m.Id);
                    Assert.AreEqual(MatchPhase.Battle, s.Phase, $"{m.Id} {a} v {b}");
                    foreach (var u in s.Units)
                        Assert.IsTrue(m.IsDeploymentHex(u.Owner, u.Position), $"{m.Id}: {u.Def.Name} at {u.Position}");
                    var ai = new TacticalAI(AIDifficulty.Tactical, 3);
                    var s2 = RulesEngine.CreateMatch(Scenario.PresetConfig(a, b, m.Id));
                    for (int seat = 0; seat < 2; seat++) Scenario.Ok(s2, ai.ChooseDeployment(s2, seat));
                }
            }
        }

        [Test]
        public void Navigation_EveryTraversableHex_IsReachableByRealMoves()
        {
            foreach (var m in Authored())
            foreach (var arch in new[] { Archetype.Infantry, Archetype.Vehicle })
            {
                var b = new Scenario(m.Id);
                int walker = b.Add(0, FactionId.Silex, arch, 0, 3);
                var s = b.Build();
                var u = s.Unit(b.Id(walker));
                var occupied = new HashSet<Hex>(s.Units.Where(x => x.Id != u.Id).Select(x => x.Position));
                var reached = new HashSet<Hex> { u.Position };
                var frontier = new Queue<Hex>(); frontier.Enqueue(u.Position);
                while (frontier.Count > 0)
                {
                    u.Position = frontier.Dequeue();
                    foreach (var kv in BoardQueries.Reachable(s, u))
                    {
                        var path = kv.Value.Path;
                        for (int i = 1; i < path.Count; i++)
                        {
                            Assert.AreEqual(1, Hex.Distance(path[i - 1], path[i]), "contiguous path");
                            Assert.IsTrue(BoardQueries.IsPassableTerrain(s, path[i]), $"path crosses {path[i]}");
                        }
                        if (reached.Add(kv.Key)) frontier.Enqueue(kv.Key);
                    }
                }
                var expected = m.AllHexes.Where(h => !TerrainRules.BlocksMovement(m.Terrain(h)) && !occupied.Contains(h)).ToList();
                CollectionAssert.IsSubsetOf(expected, reached, $"{m.Id} {arch} (move {u.BaseMove})");
            }
        }

        [Test]
        public void Combat_DecisiveVictory_StillWorksOnTheExpandedBoard()
        {
            // A Meeples gunner on the Citadel's south high ground shoots an exposed 1-HP Commander four rows away.
            var b = new Scenario(MapLibrary.DefaultMapId);
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, -2, 2);
            int cmd = b.Add(1, FactionId.Pygmaei, Archetype.Commander, 0, -1);
            b.WithHp(cmd, 1);
            var s = b.Build();
            Assert.AreEqual(TerrainType.Elevated, s.Map.Terrain(new Hex(-2, 2)));
            var preview = Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(0, -1));
            Assert.IsTrue(preview.Legal, preview.Reason);
            var r = Scenario.Do(s, Command.Attack(0, b.Id(gun), new Hex(0, -1)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(0, s.Winner);
            Assert.AreEqual(VictoryReason.CommanderEliminated, s.VictoryReason);
            Assert.IsFalse(s.Unit(b.Id(cmd)).Alive);
        }

        [TestCase("shattered_citadel", FactionId.Craigon, FactionId.Pygmaei)]
        [TestCase("citadel_crossroads", FactionId.Silex, FactionId.SpacePirates)]
        [TestCase("proving_ground", FactionId.Meeples, FactionId.Inhumanus)]
        public void AIvsAI_OnEveryBattlefield_CompletesLegally_AndReplaysExactly(string map, FactionId a, FactionId b)
        {
            var cfg = Scenario.PresetConfig(a, b, map, clock: 0);
            var session = new MatchSession(cfg);
            var ai = new[] { new TacticalAI(AIDifficulty.Tactical, 21) { Depth = 1 }, new TacticalAI(AIDifficulty.Tactical, 22) { Depth = 1 } };
            for (int seat = 0; seat < 2; seat++) Assert.IsTrue(session.Submit(ai[seat].ChooseDeployment(session.State, seat)).Accepted);
            int engaged = -1;
            while (!session.State.IsOver)
            {
                var cmd = ai[session.State.ActiveSeat].ChooseAction(session.State);
                var r = session.Submit(cmd);
                Assert.IsTrue(r.Accepted, $"AI chose an illegal command {cmd}: {r.Reason}");
                if (engaged < 0 && r.Events.Any(e => e.Type == GameEventType.AttackResolved)) engaged = session.State.ActivationIndex;
            }
            Assert.LessOrEqual(session.State.ActivationIndex, cfg.ActivationLimit);
            Assert.GreaterOrEqual(engaged, 0, "the armies met");
            Assert.AreEqual(session.Hash, Serialization.StateHash(MatchSession.Replay(cfg, session.History)));
        }

        [Test]
        public void AI_RoutesAroundAWall_InsteadOfWalkingIntoIt()
        {
            // test map: a wall across the middle row with a single gap at the far east end.
            const string id = "test_wall";
            var m = new MapDefinition(id, "Wall", "", 7, 4);
            m.Set(TerrainType.Obstacle, Enumerable.Range(-7, 12).Select(q => new Hex(q, 0)).ToArray());
            MapLibrary.Maps[id] = m;
            try
            {
                var b = new Scenario(id);
                int raider = b.Add(0, FactionId.Pygmaei, Archetype.Raider, -2, 2);
                b.Add(0, FactionId.Pygmaei, Archetype.Commander, -6, 4);
                b.Add(1, FactionId.Meeples, Archetype.Commander, -2, -3);
                var s = b.Build();
                var start = s.Unit(b.Id(raider)).Position;
                var target = new Hex(-2, -3);
                var cmd = new TacticalAI(AIDifficulty.Tactical, 5) { Depth = 1 }.ChooseAction(s);
                Assert.AreEqual(CommandType.Move, cmd.Type, cmd.ToString());
                Assert.AreEqual(b.Id(raider), cmd.UnitId);
                Assert.Less(m.Paths.Distance(cmd.Target, target), m.Paths.Distance(start, target), $"moved to {cmd.Target}: closer by route");
                Assert.Greater(cmd.Target.Lateral, start.Lateral, "heads east, toward the gap");
            }
            finally { MapLibrary.Maps.Remove(id); }
        }

        [Test]
        public void AI_RouteField_TreatsEnemyUnitsAsWalls()
        {
            const string id = "test_gaps";
            // A wall across the middle row with two gaps, west (-7,0) and east (7,0).
            var m = new MapDefinition(id, "Gaps", "", 7, 4);
            m.Set(TerrainType.Obstacle, Enumerable.Range(-6, 13).Select(q => new Hex(q, 0)).ToArray());
            MapLibrary.Maps[id] = m;
            try
            {
                var b = new Scenario(id);
                b.Add(0, FactionId.Pygmaei, Archetype.Commander, 3, 3);
                b.Add(1, FactionId.Meeples, Archetype.Commander, 3, -3);
                // Seat 1 plugs the east gap.
                b.Add(1, FactionId.Meeples, Archetype.Guardian, 7, 0);
                b.Add(1, FactionId.Meeples, Archetype.Infantry, 6, 1);
                b.Add(1, FactionId.Meeples, Archetype.Infantry, 7, -1);
                var s = b.Build();
                var field = TacticalAI.RouteField(s, new Hex(3, -3), 1);
                int from = m.Paths.IndexOf(new Hex(3, 3));
                Assert.Greater(field[from], m.Paths.Distance(new Hex(3, 3), new Hex(3, -3)), "the open east gap is plugged: the route goes west");
                Assert.Less(field[from], int.MaxValue);
            }
            finally { MapLibrary.Maps.Remove(id); }
        }

        [Test]
        public void ContentHash_CoversTheBattlefieldLayouts()
        {
            var before = Serialization.ContentHash();
            var m = MapLibrary.Get("proving_ground");
            var backup = m.Terrain(new Hex(0, 1));
            m.Set(TerrainType.Cover, new Hex(0, 1));
            try { Assert.AreNotEqual(before, Serialization.ContentHash()); }
            finally { m.Set(backup, new Hex(0, 1)); m.Topology = TopologyValidator.Validate(m); }
            Assert.AreEqual(before, Serialization.ContentHash());
        }
    }
}
