using System.IO;
using System.Linq;
using Commandfall.Core;
using NUnit.Framework;

namespace Commandfall.Tests
{
    public class FogOfWarTests
    {
        const string CoverMap = "test_fog_cover", PerchMap = "test_fog_perch", RockMap = "test_fog_rock";

        static FogOfWarTests()
        {
            if (!MapLibrary.Maps.ContainsKey(CoverMap))
                MapLibrary.Maps[CoverMap] = new MapDefinition(CoverMap, "Fog: ruin wall", "Cover in the centre.").Mirror(TerrainType.Cover, (0, 0));
            if (!MapLibrary.Maps.ContainsKey(PerchMap))
                MapLibrary.Maps[PerchMap] = new MapDefinition(PerchMap, "Fog: perch", "Ruin wall in the centre, high ground north and south.")
                    .Mirror(TerrainType.Cover, (0, 0)).Mirror(TerrainType.Elevated, (0, 2));
            if (!MapLibrary.Maps.ContainsKey(RockMap))
                MapLibrary.Maps[RockMap] = new MapDefinition(RockMap, "Fog: rock", "Outcrop in the centre.").Mirror(TerrainType.Obstacle, (0, 0));
        }

        [Test]
        public void RockOutcrop_HidesTheUnitBehindIt()
        {
            var b = new Scenario(RockMap) { FogOfWar = true };
            b.Add(0, FactionId.Meeples, Archetype.Commander, 0, 2);
            int hidden = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -2);
            var s = b.Build();
            Assert.IsFalse(Vision.CanSee(s, 0, s.Unit(b.Id(hidden))), "the outcrop at (0,0) is between them");
            Assert.IsTrue(Vision.CanSee(s, 1, s.Unit(b.Id(hidden))), "a player always sees their own units");
            s.Config.FogOfWar = false;
            Assert.IsTrue(Vision.CanSee(s, 0, s.Unit(b.Id(hidden))), "without fog of war everything is visible");
        }

        [Test]
        public void RuinWall_HidesTheUnitDirectlyBehindIt()
        {
            var b = new Scenario(CoverMap) { FogOfWar = true };
            b.Add(0, FactionId.Meeples, Archetype.Commander, 0, 3);
            int gun = b.Add(0, FactionId.Meeples, Archetype.Gunner, 0, 1);
            int behind = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            int inRuins = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, -1, 0);
            var s = b.Build();
            Assert.IsFalse(Vision.CanSee(s, 0, s.Unit(b.Id(behind))), "the wall at (0,0) is right in front of it");
            Assert.IsTrue(Vision.SightClear(s, new Hex(0, 1), new Hex(0, -2)), "a unit two hexes past the wall is not hidden by it");
            Assert.IsTrue(Vision.CanSee(s, 0, s.Unit(b.Id(inRuins))), "adjacent units are always seen");
            // Attack line of sight ignores cover, but you still cannot target what your army cannot see.
            var o = Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(0, -1));
            Assert.IsFalse(o.Legal);
            StringAssert.Contains("can't see", o.Reason);
            s.Config.FogOfWar = false;
            Assert.IsTrue(Combat.Resolve(s, s.Unit(b.Id(gun)), new Hex(0, -1)).Legal, "the same shot is legal without fog");
        }

        [Test]
        public void HighGround_SeesOverRuinWalls()
        {
            var b = new Scenario(PerchMap) { FogOfWar = true };
            b.Add(0, FactionId.Meeples, Archetype.Commander, 0, 3);
            int target = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var s = b.Build();
            Assert.IsFalse(Vision.SightClear(s, new Hex(0, 3), new Hex(0, -1)), "low ground: hidden behind the wall");
            Assert.IsTrue(Vision.SightClear(s, new Hex(0, 2), new Hex(0, -1)), "from the mesa the wall does not hide it");
            Assert.IsFalse(Vision.CanSee(s, 0, s.Unit(b.Id(target))));
            Scenario.Ok(s, Command.Move(0, s.Commander(0).Id, new Hex(0, 2)));
            Assert.IsTrue(Vision.CanSee(s, 0, s.Unit(b.Id(target))), "climbing the mesa reveals it");
        }

        [Test]
        public void SightRange_IsThreeHexes_FourFromHighGround()
        {
            var b = new Scenario(PerchMap) { FogOfWar = true };
            b.Add(0, FactionId.Meeples, Archetype.Commander, -3, 3);
            var s = b.Build();
            Assert.IsTrue(Vision.SightClear(s, new Hex(3, 0), new Hex(0, 0)), "3 hexes on open ground");
            Assert.IsFalse(Vision.SightClear(s, new Hex(3, 1), new Hex(-1, 1)), "4 hexes is out of sight from low ground");
            Assert.IsTrue(Vision.SightClear(s, new Hex(0, 2), new Hex(-1, -1)), "high ground sees 4 hexes");
            Assert.IsFalse(Vision.SightClear(s, new Hex(0, 2), new Hex(0, -3)), "but not 5");
        }

        [Test]
        public void Move_IntoHiddenEnemy_IsAnAmbush_NotARejection()
        {
            var b = new Scenario(CoverMap) { FogOfWar = true };
            b.Add(0, FactionId.Pygmaei, Archetype.Commander, 0, 3);
            int mover = b.Add(0, FactionId.Pygmaei, Archetype.Infantry, 0, 1);
            int hidden = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var s = b.Build();
            var r = Scenario.Do(s, Command.Move(0, b.Id(mover), new Hex(0, -1)));
            Assert.IsTrue(r.Accepted, r.Reason);
            Assert.AreEqual(new Hex(0, 0), s.Unit(b.Id(mover)).Position, "stops on the last hex before the hidden enemy");
            var ambush = r.Events.Single(e => e.Type == GameEventType.Ambushed);
            Assert.AreEqual(b.Id(hidden), ambush.TargetUnitId);
            Assert.AreEqual(1, s.ActiveSeat, "the activation is spent");
            Assert.IsTrue(Vision.CanSee(s, 0, s.Unit(b.Id(hidden))), "standing next to it now, the mover sees the ambusher");
        }

        [Test]
        public void Move_IntoEnemy_IsRejected_WithoutFog()
        {
            var b = new Scenario(CoverMap);
            b.Add(0, FactionId.Pygmaei, Archetype.Commander, 0, 3);
            int mover = b.Add(0, FactionId.Pygmaei, Archetype.Infantry, 0, 1);
            b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -1);
            var s = b.Build();
            StringAssert.Contains("enemy", Scenario.Rejected(s, Command.Move(0, b.Id(mover), new Hex(0, -1))));
        }

        [Test]
        public void FoggedView_RemovesOnlyHiddenEnemies_AndLeavesTheRealStateAlone()
        {
            var b = new Scenario(RockMap) { FogOfWar = true };
            b.Add(0, FactionId.Meeples, Archetype.Commander, 0, 2);
            int hidden = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 0, -2);
            int seen = b.Add(1, FactionId.Pygmaei, Archetype.Infantry, 1, 1);
            var s = b.Build();
            var view = Vision.FoggedView(s, 0);
            Assert.AreNotSame(s, view);
            Assert.IsNull(view.UnitAt(new Hex(0, -2)));
            Assert.IsNotNull(view.UnitAt(new Hex(1, 1)));
            Assert.IsNotNull(s.UnitAt(new Hex(0, -2)), "the authoritative state is untouched");
            Assert.AreEqual(Serialization.StateHash(s), Serialization.StateHash(s.Clone()));
            Assert.IsFalse(LegalActions.Enumerate(view).Any(c => c.Type == CommandType.Attack && c.Target == new Hex(0, -2)));
            Assert.AreEqual(b.Id(seen), view.UnitAt(new Hex(1, 1)).Id);
            Assert.AreEqual(b.Id(hidden), s.UnitAt(new Hex(0, -2)).Id);
        }

        [Test]
        public void FogSetting_SurvivesSerialization()
        {
            var cfg = new MatchConfig { FogOfWar = true };
            var copy = Serialization.FromBytes(Serialization.ConfigBytes(cfg), Serialization.ReadConfig);
            Assert.IsTrue(copy.FogOfWar);
            Assert.IsTrue(cfg.Clone().FogOfWar);
        }
    }
}
