using System.Collections.Generic;

namespace Commandfall.Core
{
    /// <summary>
    /// Fog of war (optional, <see cref="MatchConfig.FogOfWar"/>). A player sees an enemy unit only while at least one of
    /// their own units has a clear sight line to it:
    /// * units see <see cref="SightRange"/> (3) hexes, 4 from high ground, so armies start out of each other's sight;
    /// * rock outcrops (obstacles), crystal bulwarks and smoke block every sight line that passes through them;
    /// * a ruin wall (cover hex) hides the unit standing directly behind it (the wall is adjacent to the target) —
    ///   unless the viewer stands on high ground, which sees over walls;
    /// * units never block sight, and adjacent units always see each other.
    /// Hidden units cannot be targeted, and a move that runs into a hidden enemy stops short of it (an ambush).
    /// Everything here is deterministic, so host, client, replay and AI agree.
    /// Tuning notes (AI harness, 4 games per pairing): letting walls block every line through them hid 44–87% of units
    /// and stalled most matches; blockers alone (no range) hid only 4–12% — invisible in practice. Range 3 plus the
    /// blockers hides ~49% (Shattered Citadel) / ~67% (Crossroads) on average while 71% / 65% of matches still end by
    /// Commander kill (80% / 69% without fog).
    /// </summary>
    public static class Vision
    {
        public static bool Enabled(MatchState s) => s.Config.FogOfWar;

        /// <summary>How far a unit sees; high ground adds one.</summary>
        public const int SightRange = 3;

        static bool Blocks(MatchState s, Hex h, Hex to, bool highViewer)
        {
            var t = TerrainRules.Of(s.Map.Terrain(h));
            if (t.BlocksSight) return true;
            if (t.Role == TerrainRole.SightScreen && !highViewer && Hex.Distance(h, to) == 1) return true;
            return s.BulwarkAt(h) != null || s.HasSmoke(h);
        }

        /// <summary>True if a sight line between the two hexes is clear (either of the two nudged hex lines suffices).</summary>
        public static bool SightClear(MatchState s, Hex from, Hex to)
        {
            int dist = Hex.Distance(from, to);
            if (dist <= 1) return true;
            bool highViewer = s.Map.Terrain(from) == TerrainType.Elevated;
            if (dist > SightRange + (highViewer ? 1 : 0)) return false;
            for (int pass = 0; pass < 2; pass++)
            {
                var line = Hex.Line(from, to, pass == 0 ? 1 : -1);
                bool clear = true;
                for (int i = 1; i < line.Count - 1; i++)
                {
                    var h = line[i];
                    if (!s.Map.Contains(h)) continue;
                    if (Blocks(s, h, to, highViewer)) { clear = false; break; }
                }
                if (clear) return true;
            }
            return false;
        }

        /// <summary>Can any living, deployed unit of <paramref name="seat"/> see hex <paramref name="h"/>?</summary>
        public static bool SeatSeesHex(MatchState s, int seat, Hex h)
        {
            foreach (var u in s.Units)
                if (u.Owner == seat && u.OnBoard && SightClear(s, u.Position, h)) return true;
            return false;
        }

        /// <summary>Is <paramref name="u"/> visible to <paramref name="seat"/>? Always true without fog of war.</summary>
        public static bool CanSee(MatchState s, int seat, UnitState u)
        {
            if (u == null) return false;
            if (!s.Config.FogOfWar || u.Owner == seat) return true;
            return u.OnBoard && SeatSeesHex(s, seat, u.Position);
        }

        /// <summary>Hexes <paramref name="seat"/> can currently see (presentation shading).</summary>
        public static HashSet<Hex> VisibleHexes(MatchState s, int seat)
        {
            var set = new HashSet<Hex>();
            foreach (var h in s.Map.AllHexes)
                if (!s.Config.FogOfWar || SeatSeesHex(s, seat, h)) set.Add(h);
            return set;
        }

        /// <summary>
        /// The battlefield as <paramref name="seat"/> knows it: a copy in which enemy units it cannot see are taken off
        /// the board. Planning (reachable hexes, legal targets, previews, AI search) runs on this view so nothing leaks
        /// hidden positions. Returns <paramref name="s"/> itself when fog of war is off.
        /// </summary>
        public static MatchState FoggedView(MatchState s, int seat) => FoggedView(s, seat, false);

        /// <summary>
        /// What an AI may plan on: <see cref="FoggedView(MatchState, int)"/> without the Full Roster secrets as well. Enemy
        /// mines are hidden from their opponent (with or without fog of war) until revealed, so they are removed here;
        /// otherwise a simulated move would trigger them and the AI would steer around mines it cannot know about.
        /// </summary>
        public static MatchState PlanningView(MatchState s, int seat) => FoggedView(s, seat, true);

        static MatchState FoggedView(MatchState s, int seat, bool stripMines)
        {
            bool mines = stripMines && s.Mines.Exists(m => m.Owner != seat && m.RevealedUntilRound < s.Round);
            if (!s.Config.FogOfWar && !mines) return s;
            var hidden = new List<int>();
            if (s.Config.FogOfWar)
                foreach (var u in s.Units)
                    if (u.Owner != seat && u.OnBoard && !SeatSeesHex(s, seat, u.Position)) hidden.Add(u.Id);
            if (hidden.Count == 0 && !mines) return s;
            var v = s.Clone();
            foreach (var id in hidden) v.Unit(id).Deployed = false;
            if (mines) v.Mines.RemoveAll(m => m.Owner != seat && m.RevealedUntilRound < s.Round);
            return v;
        }
    }
}
