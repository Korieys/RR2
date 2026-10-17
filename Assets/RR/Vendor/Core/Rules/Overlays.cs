using System.Collections.Generic;

namespace Commandfall.Core
{
    /// <summary>Per-hex description of a unit's weapon reach from one hex (see <see cref="Overlays.WeaponBand"/>).</summary>
    [System.Flags]
    public enum BandFlags : byte
    {
        None = 0,
        /// <summary>Within the weapon's minimum..maximum range.</summary>
        InRange = 1 << 0,
        /// <summary>In range, but the line of fire is blocked (terrain, smoke or a unit).</summary>
        NoSight = 1 << 1,
        /// <summary>Only in range thanks to the high-ground bonus (+1 maximum range from an Elevated hex).</summary>
        HighGround = 1 << 2,
        /// <summary>Inside the minimum range: the weapon cannot fire this close.</summary>
        TooClose = 1 << 3,
    }

    /// <summary>
    /// Read-only queries behind the battle overlays (weapon bands, line of fire, threat map, next-activation reach). They
    /// mirror <see cref="Combat.Resolve"/> exactly and change no rules. Callers pass the planning view
    /// (<see cref="Vision.FoggedView"/>), so hidden units never influence what a player is shown.
    /// </summary>
    public static class Overlays
    {
        /// <summary>The line of fire from <paramref name="from"/> to <paramref name="to"/> as <paramref name="u"/>'s
        /// weapon traces it (same arguments as <see cref="Combat.Resolve"/>).</summary>
        public static LosState LineOfFire(MatchState s, UnitState u, Hex from, Hex to)
        {
            var w = u.Weapon;
            if (w.Has(WeaponTraits.Indirect)) return LosState.Clear;
            bool elevated = s.Map.Terrain(from) == TerrainType.Elevated;
            return BoardQueries.LineOfSight(s, from, to, w.Has(WeaponTraits.Phase) || elevated, u.Id, u.Owner);
        }

        /// <summary>
        /// Every board hex classified for <paramref name="u"/>'s weapon fired from <paramref name="from"/>: in range (with or
        /// without a clear line of fire, and whether only the high-ground bonus reaches it) or inside the minimum range.
        /// Obstacles and hexes outside both are omitted.
        /// </summary>
        public static Dictionary<Hex, BandFlags> WeaponBand(MatchState s, UnitState u, Hex from)
        {
            var band = new Dictionary<Hex, BandFlags>();
            var (min, max) = BoardQueries.WeaponRange(s, u, from);
            int baseMax = u.Weapon.MaxRange;
            foreach (var h in s.Map.AllHexes)
            {
                if (s.Map.Terrain(h) == TerrainType.Obstacle) continue;
                int d = Hex.Distance(from, h);
                if (d == 0) continue;
                if (d < min) { band[h] = BandFlags.TooClose; continue; }
                if (d > max) continue;
                var f = BandFlags.InRange;
                if (d > baseMax) f |= BandFlags.HighGround;
                if (LineOfFire(s, u, from, h) != LosState.Clear) f |= BandFlags.NoSight;
                band[h] = f;
            }
            return band;
        }

        /// <summary>
        /// The first hex that blocks the line of fire (the same nudged line whose verdict <see cref="BoardQueries.LineOfSight"/>
        /// reports), or null when the line is clear. <paramref name="why"/> names the blocker for the player.
        /// </summary>
        public static Hex? FirstBlocker(MatchState s, UnitState u, Hex from, Hex to, out string why)
        {
            why = null;
            if (LineOfFire(s, u, from, to) == LosState.Clear) return null;
            bool ignoreUnits = u.Weapon.Has(WeaponTraits.Phase) || s.Map.Terrain(from) == TerrainType.Elevated;
            var line = Hex.Line(from, to, 1);
            for (int i = 1; i < line.Count - 1; i++)
            {
                var h = line[i];
                if (!s.Map.Contains(h)) continue;
                if (s.Map.Terrain(h) == TerrainType.Obstacle) { why = "rock outcrop"; return h; }
                if (s.BulwarkAt(h) != null) { why = "crystal bulwark"; return h; }
                if (s.HasSmoke(h)) { why = "smoke"; return h; }
                if (!ignoreUnits)
                {
                    var occ = s.UnitAt(h);
                    if (occ != null && occ.Id != u.Id && Vision.CanSee(s, u.Owner, occ)) { why = occ.Def.Name + " in the way"; return h; }
                }
            }
            return null;
        }

        /// <summary>Activation index at which <paramref name="e"/> next acts: now if its side is active, else after this activation.</summary>
        public static int NextActivationOf(MatchState s, UnitState e) => s.ActivationIndex + (s.ActiveSeat == e.Owner ? 0 : 1);

        /// <summary>
        /// Could <paramref name="e"/> attack a unit standing on <paramref name="h"/> at its next activation without moving?
        /// Range, line of fire, a ready weapon and (fog of war) sight of the hex — exactly the checks of
        /// <see cref="Combat.Resolve"/> for a target there.
        /// </summary>
        public static bool Threatens(MatchState s, UnitState e, Hex h)
        {
            if (!e.OnBoard || e.Position == h) return false;
            int d = Hex.Distance(e.Position, h);
            var (min, max) = BoardQueries.WeaponRange(s, e, e.Position);
            if (d < min || d > max) return false;
            if (NextActivationOf(s, e) < e.WeaponReadyAt) return false;
            if (s.Config.FogOfWar && !Vision.SeatSeesHex(s, e.Owner, h)) return false;
            return LineOfFire(s, e, e.Position, h) == LosState.Clear;
        }

        /// <summary>Enemy units of <paramref name="seat"/> that threaten <paramref name="h"/> (see <see cref="Threatens"/>).</summary>
        public static List<UnitState> ThreatsTo(MatchState s, int seat, Hex h)
        {
            var list = new List<UnitState>();
            foreach (var e in s.Units)
                if (e.Owner != seat && Threatens(s, e, h)) list.Add(e);
            return list;
        }

        /// <summary>
        /// Threat map for <paramref name="seat"/>: for every hex one of its units could stand on (or stands on), how many
        /// enemy units could attack it there at their next activation. Hexes held by enemies, obstacles and bulwarks are
        /// omitted. At most 37 × 7 checks.
        /// </summary>
        public static Dictionary<Hex, int> ThreatCounts(MatchState s, int seat)
        {
            var counts = new Dictionary<Hex, int>();
            var enemies = new List<UnitState>();
            foreach (var e in s.Units) if (e.Owner != seat && e.OnBoard) enemies.Add(e);
            foreach (var h in s.Map.AllHexes)
            {
                if (s.Map.Terrain(h) == TerrainType.Obstacle || s.BulwarkAt(h) != null) continue;
                var occ = s.UnitAt(h);
                if (occ != null && occ.Owner != seat) continue;
                int n = 0;
                foreach (var e in enemies) if (Threatens(s, e, h)) n++;
                counts[h] = n;
            }
            return counts;
        }

        /// <summary>
        /// Everything <paramref name="u"/> could attack at its next activation if it moved now: the union, over its current
        /// hex and every legal destination, of the hexes in range with a clear line of fire from there.
        /// </summary>
        public static HashSet<Hex> NextActivationReach(MatchState s, UnitState u)
        {
            var reach = new HashSet<Hex>();
            if (!u.OnBoard) return reach;
            var origins = new List<Hex> { u.Position };
            origins.AddRange(BoardQueries.Reachable(s, u).Keys);
            foreach (var from in origins)
            {
                var (min, max) = BoardQueries.WeaponRange(s, u, from);
                foreach (var h in s.Map.AllHexes)
                {
                    if (reach.Contains(h) || s.Map.Terrain(h) == TerrainType.Obstacle) continue;
                    int d = Hex.Distance(from, h);
                    if (d == 0 || d < min || d > max) continue;
                    if (LineOfFire(s, u, from, h) == LosState.Clear) reach.Add(h);
                }
            }
            return reach;
        }

        // ------------------------------------------------------------------ board zones

        /// <summary>Zone of a hex: the seat whose deployment rows it belongs to, or −1 for no-man's land.</summary>
        public static int ZoneOf(MapDefinition map, Hex h) => map.ZoneSeat(h);

        public enum MiddleState : byte { Clear = 0, Held = 1, Contested = 2 }

        /// <summary>
        /// Who is in no-man's land as <paramref name="viewer"/> knows it (pass the viewer's planning view). Held: only one
        /// side has units there; <paramref name="holder"/> is that side and <paramref name="share"/> the fraction of the
        /// middle's hexes its units stand on or threaten.
        /// </summary>
        public static MiddleState Middle(MatchState s, int viewer, out int holder, out float share)
        {
            holder = -1; share = 0f;
            bool[] inside = new bool[2];
            foreach (var u in s.Units)
                if (u.OnBoard && ZoneOf(s.Map, u.Position) < 0 && Vision.CanSee(s, viewer, u)) inside[u.Owner] = true;
            if (inside[0] && inside[1]) return MiddleState.Contested;
            if (!inside[0] && !inside[1]) return MiddleState.Clear;
            holder = inside[0] ? 0 : 1;
            int total = 0, held = 0;
            foreach (var h in s.Map.AllHexes)
            {
                if (ZoneOf(s.Map, h) >= 0) continue;
                total++;
                var occ = s.UnitAt(h);
                bool mine = occ != null && occ.Owner == holder;
                if (!mine)
                    foreach (var u in s.Units)
                        if (u.Owner == holder && u.OnBoard && Threatens(s, u, h)) { mine = true; break; }
                if (mine) held++;
            }
            share = total > 0 ? held / (float)total : 0f;
            return MiddleState.Held;
        }
    }
}
