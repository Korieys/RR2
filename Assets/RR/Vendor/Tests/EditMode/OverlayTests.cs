using System.Collections.Generic;
using System.Linq;
using Commandfall.AI;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    /// <summary>
    /// The battle overlays (weapon bands, line of fire, threat map, no-man's land) are presentation only, so they must
    /// agree exactly with the rules they describe and never reveal hidden units.
    /// </summary>
    public class OverlayTests
    {
        /// <summary>Positions from real play: preset armies on every map, a few AI activations in.</summary>
        static IEnumerable<MatchState> PlayedStates(bool fog)
        {
            var pairs = new[] { (FactionId.Pygmaei, FactionId.Inhumanus), (FactionId.SpacePirates, FactionId.Craigon), (FactionId.Silex, FactionId.Meeples) };
            int seed = 3;
            foreach (var map in MapLibrary.Maps.Keys.Where(k => !k.StartsWith("test_")).ToList())
                foreach (var (a, b) in pairs)
                {
                    var cfg = Scenario.PresetConfig(a, b, map, 0);
                    cfg.FogOfWar = fog;
                    var s = RulesEngine.CreateMatch(cfg);
                    Scenario.Ok(s, Command.AutoDeploy(0));
                    Scenario.Ok(s, Command.AutoDeploy(1));
                    var ai = new TacticalAI(AIDifficulty.Beginner, seed++);
                    for (int i = 0; i < 14 && !s.IsOver; i++)
                    {
                        if (i % 3 == 0) yield return s.Clone();
                        Scenario.Ok(s, ai.ChooseAction(s));
                    }
                }
        }

        /// <summary>A copy of <paramref name="s"/> with an extra unit of <paramref name="seat"/> standing on <paramref name="h"/>.</summary>
        static MatchState WithDummy(MatchState s, int seat, Hex h, out UnitState dummy)
        {
            var c = s.Clone();
            dummy = c.UnitAt(h);
            if (dummy != null) return c;
            dummy = c.Units.First(u => u.Owner == seat).Clone();
            dummy.Id = c.NextUnitId++;
            dummy.Position = h; dummy.Deployed = true; dummy.Alive = true;
            dummy.Statuses.Clear();
            c.Units.Add(dummy);
            return c;
        }

        [Test]
        public void WeaponBand_MatchesCombatResolveOnEveryHex()
        {
            int checks = 0;
            foreach (var s in PlayedStates(false))
            {
                foreach (var u in s.Units.Where(x => x.OnBoard))
                {
                    var band = Overlays.WeaponBand(s, u, u.Position);
                    foreach (var h in s.Map.AllHexes)
                    {
                        if (h == u.Position || s.Map.Terrain(h) == TerrainType.Obstacle || s.BulwarkAt(h) != null) continue;
                        var occ = s.UnitAt(h);
                        if (occ != null && occ.Owner == u.Owner) continue;
                        var c = WithDummy(s, 1 - u.Owner, h, out _);
                        c.ActivationIndex = System.Math.Max(c.ActivationIndex, u.WeaponReadyAt);   // judge range and sight, not recharge
                        var o = Combat.Resolve(c, c.Unit(u.Id), h);
                        band.TryGetValue(h, out var f);
                        bool attackable = (f & BandFlags.InRange) != 0 && (f & BandFlags.NoSight) == 0;
                        Assert.AreEqual(o.Legal, attackable, $"{u.Def.Name} {u.Position} -> {h}: band {f}, rules say {(o.Legal ? "legal" : o.Reason)}");
                        Assert.AreEqual(o.Distance < BoardQueries.WeaponRange(s, u, u.Position).min, (f & BandFlags.TooClose) != 0);
                        checks++;
                    }
                }
            }
            Assert.Greater(checks, 2000);
        }

        [Test]
        public void FirstBlocker_ExistsExactlyWhenTheLineIsBlocked()
        {
            foreach (var s in PlayedStates(false).Take(12))
                foreach (var u in s.Units.Where(x => x.OnBoard))
                    foreach (var h in s.Map.AllHexes)
                    {
                        if (h == u.Position) continue;
                        bool blocked = Overlays.LineOfFire(s, u, u.Position, h) != LosState.Clear;
                        var b = Overlays.FirstBlocker(s, u, u.Position, h, out var why);
                        Assert.AreEqual(blocked, b.HasValue, $"{u.Def.Name} {u.Position} -> {h}");
                        if (b.HasValue) { Assert.IsNotEmpty(why); Assert.AreNotEqual(u.Position, b.Value); Assert.AreNotEqual(h, b.Value); }
                    }
        }

        [Test]
        public void ThreatCounts_MatchBruteForceCombatResolve()
        {
            int nonZero = 0;
            foreach (bool fog in new[] { false, true })
                foreach (var full in PlayedStates(fog))
                    for (int seat = 0; seat < 2; seat++)
                    {
                        var s = Vision.FoggedView(full, seat);
                        var counts = Overlays.ThreatCounts(s, seat);
                        foreach (var kv in counts)
                        {
                            var c = WithDummy(s, seat, kv.Key, out _);
                            int brute = 0;
                            foreach (var e in c.Units.Where(x => x.OnBoard && x.Owner != seat))
                            {
                                var at = c.Clone();
                                at.ActivationIndex = Overlays.NextActivationOf(s, e);
                                if (Combat.Resolve(at, at.Unit(e.Id), kv.Key).Legal) brute++;
                            }
                            Assert.AreEqual(brute, kv.Value, $"seat {seat} hex {kv.Key} fog {fog}");
                            if (brute > 0) nonZero++;
                        }
                    }
            Assert.Greater(nonZero, 50, "the positions should contain real threats");
        }

        [Test]
        public void Overlays_NeverDependOnHiddenUnits()
        {
            // Move every enemy that seat 0 cannot see somewhere else it still cannot see: nothing seat 0 is shown may change.
            int tried = 0;
            foreach (var full in PlayedStates(true))
            {
                var hidden = full.Units.Where(u => u.Owner == 1 && u.OnBoard && !Vision.CanSee(full, 0, u)).ToList();
                if (hidden.Count == 0) continue;
                var moved = full.Clone();
                foreach (var h in hidden)
                {
                    var hu = moved.Unit(h.Id);
                    foreach (var spot in moved.Map.AllHexes.OrderByDescending(x => x.R))
                    {
                        if (moved.UnitAt(spot) != null || !BoardQueries.IsPassableTerrain(moved, spot)) continue;
                        var old = hu.Position; hu.Position = spot;
                        if (!Vision.SeatSeesHex(moved, 0, spot) && Vision.FoggedView(moved, 0).Units.Count(u => u.OnBoard) == Vision.FoggedView(full, 0).Units.Count(u => u.OnBoard)) break;
                        hu.Position = old;
                    }
                }
                var a = Vision.FoggedView(full, 0);
                var b = Vision.FoggedView(moved, 0);
                string Known(MatchState v) => string.Join(";", v.Units.Where(u => u.OnBoard).OrderBy(u => u.Id).Select(u => $"{u.Id}@{u.Position}"));
                if (Known(a) != Known(b)) continue;   // a move revealed something; not this test's case
                tried++;
                CollectionAssert.AreEquivalent(Overlays.ThreatCounts(a, 0), Overlays.ThreatCounts(b, 0));
                foreach (var u in a.Units.Where(x => x.OnBoard))
                {
                    CollectionAssert.AreEquivalent(Overlays.WeaponBand(a, u, u.Position), Overlays.WeaponBand(b, b.Unit(u.Id), u.Position));
                    CollectionAssert.AreEquivalent(Overlays.NextActivationReach(a, u), Overlays.NextActivationReach(b, b.Unit(u.Id)));
                }
                Assert.AreEqual(Overlays.Middle(a, 0, out var ha, out var sa), Overlays.Middle(b, 0, out var hb, out var sb));
                Assert.AreEqual(ha, hb); Assert.AreEqual(sa, sb);
            }
            Assert.Greater(tried, 3, "fogged positions with hidden enemies should occur");
        }

        [Test]
        public void HighGround_AddsTheBonusRingAndMinimumRangeLeavesADeadZone()
        {
            var b = new Scenario(Scenario.LosMap);
            int mortar = b.Add(0, FactionId.Pygmaei, Archetype.Gunner, 0, 2);   // Elevated perch on the LoS map
            var s = b.Build();
            var u = s.Unit(b.Id(mortar));
            var (min, max) = BoardQueries.WeaponRange(s, u, u.Position);
            Assert.AreEqual(u.Weapon.MaxRange + 1, max, "the perch is high ground");
            var band = Overlays.WeaponBand(s, u, u.Position);
            Assert.IsTrue(band.Where(kv => (kv.Value & BandFlags.HighGround) != 0).All(kv => Hex.Distance(u.Position, kv.Key) == max));
            Assert.IsTrue(band.Any(kv => (kv.Value & BandFlags.HighGround) != 0));
            if (min > 1) Assert.IsTrue(band.Where(kv => Hex.Distance(u.Position, kv.Key) < min).All(kv => kv.Value == BandFlags.TooClose));
        }

        [Test]
        public void Middle_ReportsClearHeldAndContested()
        {
            var b = new Scenario();
            int mine = b.Add(0, FactionId.Meeples, Archetype.Infantry, 0, 3);
            int theirs = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -3);
            var s = b.Build();
            Assert.AreEqual(Overlays.MiddleState.Clear, Overlays.Middle(s, 0, out _, out _));
            s.Unit(b.Id(mine)).Position = new Hex(0, 1);
            Assert.AreEqual(Overlays.MiddleState.Held, Overlays.Middle(s, 0, out int holder, out float share));
            Assert.AreEqual(0, holder);
            Assert.Greater(share, 0f);
            s.Unit(b.Id(theirs)).Position = new Hex(0, -1);
            Assert.AreEqual(Overlays.MiddleState.Contested, Overlays.Middle(s, 0, out _, out _));
            Assert.AreEqual(19, s.Map.AllHexes.Count(h => Overlays.ZoneOf(s.Map, h) < 0));
        }
    }
}
