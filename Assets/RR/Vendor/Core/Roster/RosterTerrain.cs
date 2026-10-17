using System;
using System.Collections.Generic;

namespace Commandfall.Core
{
    /// <summary>
    /// How the authored terrain types play under the Full Roster rules (codex "terrain"):
    /// planetary — Cover = low cover (−1 ranged damage), Elevated = rough (+1 movement per hex), Obstacle = wall (blocks
    /// movement and sight), Hazard = burning ground (1 damage when a unit ends a move there);
    /// orbital — Cover = asteroid field (−1 ranged damage), Elevated = nebula (−1 basic range for a ship inside), Obstacle =
    /// large wreck (blocks movement and sight), Hazard = ion storm (1 damage when a ship ends a move there).
    /// Blocking barriers count as walls while they stand; cover fields count as low cover.
    /// </summary>
    public static class RosterTerrain
    {
        public static string Name(BattleDomain d, TerrainType t)
        {
            if (d == BattleDomain.Orbital)
                return t switch { TerrainType.Cover => "Asteroid field", TerrainType.Elevated => "Nebula", TerrainType.Obstacle => "Large wreck", TerrainType.Hazard => "Ion storm", _ => "Open space" };
            return t switch { TerrainType.Cover => "Low cover", TerrainType.Elevated => "Rough ground", TerrainType.Obstacle => "Wall", TerrainType.Hazard => "Burning ground", _ => "Open ground" };
        }

        public static string Rule(BattleDomain d, TerrainType t)
        {
            if (d == BattleDomain.Orbital)
                return t switch
                {
                    TerrainType.Cover => "A ship inside takes 1 less damage from ranged attacks (2+ hexes).",
                    TerrainType.Elevated => "A ship inside has −1 basic attack range (minimum 1) unless immune.",
                    TerrainType.Obstacle => "Blocks movement and line of sight.",
                    TerrainType.Hazard => "A ship ending a move here takes 1 damage.",
                    _ => "No effect.",
                };
            return t switch
            {
                TerrainType.Cover => "A unit here takes 1 less damage from ranged attacks (2+ hexes). Does not block sight.",
                TerrainType.Elevated => "Entering costs 2 movement (rough) unless the unit ignores rough terrain.",
                TerrainType.Obstacle => "Blocks movement and line of sight.",
                TerrainType.Hazard => "A unit ending a move here takes 1 damage.",
                _ => "No effect.",
            };
        }

        /// <summary>A wall, wreck or standing blocking barrier: nothing may enter, and sight lines through it are blocked.</summary>
        public static bool Blocks(MatchState s, Hex h)
        {
            if (!s.Map.Contains(h)) return true;
            if (s.Map.Terrain(h) == TerrainType.Obstacle) return true;
            var b = s.BarrierAt(h);
            return b != null && b.Blocking;
        }

        public static bool IsRough(MatchState s, Hex h) => s.Config.Domain == BattleDomain.Planetary && s.Map.Terrain(h) == TerrainType.Elevated;
        public static bool IsNebula(MatchState s, Hex h) => s.Config.Domain == BattleDomain.Orbital && s.Map.Terrain(h) == TerrainType.Elevated;

        public static bool IsBurning(MatchState s, Hex h)
        {
            if (s.Map.Terrain(h) == TerrainType.Hazard) return true;
            foreach (var i in s.Ignited) if (i.Hex == h) return true;
            return false;
        }

        /// <summary>Low cover on the hex (terrain or a cover field).</summary>
        public static string CoverAt(MatchState s, Hex h)
        {
            var t = s.Map.Terrain(h);
            if (t == TerrainType.Cover) return s.Config.Domain == BattleDomain.Orbital ? "asteroids" : "low cover";
            var b = s.BarrierAt(h);
            if (b != null && !b.Blocking) return "cover field";
            return null;
        }

        /// <summary>Codex line of sight: walls, large wrecks and blocking barriers block; low cover never does. Units block
        /// (friend or foe) unless <paramref name="ignoreUnits"/>; <paramref name="passFriendly"/> lets the attacker's sight pass
        /// through allies accepted by the predicate (Bilateral Targeting, Side Cannons), at most once.</summary>
        public static LosState LineOfSight(MatchState s, Hex from, Hex to, bool ignoreUnits, int attackerId = 0, int seat = -1,
            Func<UnitState, bool> passFriendly = null)
        {
            if (from == to || Hex.Distance(from, to) <= 1) return LosState.Clear;
            LosState first = LosState.Clear;
            for (int pass = 0; pass < 2; pass++)
            {
                var line = Hex.Line(from, to, pass == 0 ? 1 : -1);
                LosState blocked = LosState.Clear;
                int passed = 0;
                for (int i = 1; i < line.Count - 1; i++)
                {
                    var h = line[i];
                    if (!s.Map.Contains(h)) continue;
                    if (Blocks(s, h)) { blocked = LosState.BlockedByTerrain; break; }
                    if (ignoreUnits) continue;
                    var occ = s.UnitAt(h);
                    if (occ == null || occ.Id == attackerId) continue;
                    if (seat >= 0 && !Vision.CanSee(s, seat, occ)) continue;
                    if (passFriendly != null && passed == 0 && occ.Owner == seat && passFriendly(occ)) { passed++; continue; }
                    blocked = LosState.BlockedByUnit; break;
                }
                if (blocked == LosState.Clear) return LosState.Clear;
                if (pass == 0) first = blocked;
            }
            return first;
        }
    }
}
