using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    public enum LosState : byte { Clear = 0, BlockedByTerrain = 1, BlockedByUnit = 2, BlockedBySmoke = 3 }

    public sealed class MoveOption
    {
        public Hex Destination;
        public int Cost;
        public List<Hex> Path; // includes start and destination
    }

    /// <summary>Pure spatial queries over a match state: stats in context, pathfinding, line of sight, cover.</summary>
    public static class BoardQueries
    {
        public static bool IsPassableTerrain(MatchState s, Hex h)
        {
            if (!s.Map.Contains(h)) return false;
            if (TerrainRules.BlocksMovement(s.Map.Terrain(h))) return false;
            return s.BulwarkAt(h) == null;
        }

        public static int EnterCost(MatchState s, UnitState u, Hex from, Hex to)
        {
            if (u.Hover) return 1;
            var tt = s.Map.Terrain(to);
            int climb = TerrainRules.Of(tt).ClimbCost;
            return climb > 1 && s.Map.Terrain(from) != tt ? climb : 1;
        }

        public static bool IsLiened(MatchState s, UnitState u) => u.HasStatus(StatusType.Lien);

        public static int EffectiveMove(MatchState s, UnitState u)
        {
            if (s.IsRoster) return RosterMovement.Budget(s, u);
            int move = u.BaseMove;
            var lien = u.GetStatus(StatusType.Lien);
            if (lien.HasValue)
            {
                var holder = s.Unit(lien.Value.SourceUnitId);
                if (holder != null && s.Players[holder.Owner].Doctrine == Doctrine.Control) move = Math.Min(move, 1);
            }
            return move;
        }

        public static bool HiveLinkActive(MatchState s, UnitState u)
        {
            if (u.Def.Faction != FactionId.Craigon || !u.OnBoard) return false;
            foreach (var n in u.Position.Neighbors())
            {
                var other = s.UnitAt(n);
                if (other != null && other.Owner == u.Owner && other.Def.Faction == FactionId.Craigon) return true;
            }
            return false;
        }

        public static bool SharedCommandActive(MatchState s, UnitState u)
        {
            if (u.Def.Faction != FactionId.Meeples || !u.IsCommander || !u.OnBoard) return false;
            foreach (var n in u.Position.Neighbors())
            {
                var other = s.UnitAt(n);
                if (other != null && other.Owner == u.Owner) return true;
            }
            return false;
        }

        public static int EffectiveArmor(MatchState s, UnitState u)
        {
            if (s.IsRoster) return RosterCombat.Armor(s, u, new HitSpec { From = u.Position });
            int a = u.BaseArmor;
            if (HiveLinkActive(s, u)) a += 1;
            if (SharedCommandActive(s, u)) a += 1;
            return a;
        }

        /// <summary>Why the unit counts as in cover (null if not). Cover reduces ranged attacks (distance ≥ 2) by 1.</summary>
        public static string CoverSource(MatchState s, UnitState u)
        {
            if (!u.OnBoard) return null;
            if (s.IsRoster) return RosterTerrain.CoverAt(s, u.Position) ?? (RosterPassives.AuraCover(s, u) ? "aura" : null);
            var h = u.Position;
            if (s.Map.Terrain(h) == TerrainType.Cover) return "ruins";
            if (s.WreckIndex(h) >= 0) return "wreck";
            if (s.HasSmoke(h)) return "smoke";
            foreach (var n in h.Neighbors())
            {
                var b = s.BulwarkAt(n);
                if (b != null && b.Owner == u.Owner) return "bulwark";
                var ally = s.UnitAt(n);
                if (ally != null && ally.Owner == u.Owner && (ally.Flags & EquipmentFlags.CouncilAegis) != 0) return "council aegis";
            }
            return null;
        }

        /// <summary>Effective weapon range for a unit standing on <paramref name="from"/> (Elevated grants +1 max range to ranged weapons).</summary>
        public static (int min, int max) WeaponRange(MatchState s, UnitState u, Hex from)
        {
            if (s.IsRoster) return RosterCombat.BasicRange(s, u, from);
            var w = u.Weapon;
            int max = w.MaxRange;
            if (w.Kind == WeaponKind.Ranged && w.MaxRange >= 2 && s.Map.Terrain(from) == TerrainType.Elevated) max += 1;
            return (w.MinRange, max);
        }

        // ------------------------------------------------------------------ pathfinding

        /// <summary>
        /// All legal move destinations for <paramref name="u"/> (Dijkstra over terrain costs). Units may pass through allies but not enemies,
        /// never end on an occupied hex, and may always step one hex into an adjacent passable empty hex regardless of terrain cost.
        /// </summary>
        public static Dictionary<Hex, MoveOption> Reachable(MatchState s, UnitState u, int? moveOverride = null)
        {
            if (s.IsRoster) return RosterMovement.Reachable(s, u);
            var result = new Dictionary<Hex, MoveOption>();
            if (!u.OnBoard) return result;
            int budget = moveOverride ?? EffectiveMove(s, u);
            var start = u.Position;
            var dist = new Dictionary<Hex, int> { [start] = 0 };
            var prev = new Dictionary<Hex, Hex>();
            var open = new SortedSet<(int cost, Hex hex)>(Comparer<(int cost, Hex hex)>.Create((a, b) =>
            {
                int c = a.cost.CompareTo(b.cost);
                return c != 0 ? c : a.hex.CompareTo(b.hex);
            }));
            open.Add((0, start));
            while (open.Count > 0)
            {
                var cur = open.Min; open.Remove(cur);
                if (cur.cost > dist[cur.hex]) continue;
                for (int d = 0; d < 6; d++)
                {
                    var n = cur.hex.Neighbor(d);
                    if (!IsPassableTerrain(s, n)) continue;
                    var occ = s.UnitAt(n);
                    if (occ != null && occ.Owner != u.Owner) continue; // cannot pass through enemies
                    int step = EnterCost(s, u, cur.hex, n);
                    int nc = cur.cost + step;
                    bool firstStepException = cur.hex == start && nc > budget && budget >= 1;
                    if (nc > budget && !firstStepException) continue;
                    if (firstStepException) nc = budget; // a single step is always allowed but ends movement
                    if (dist.TryGetValue(n, out var old) && old <= nc) continue;
                    dist[n] = nc; prev[n] = cur.hex;
                    open.Add((nc, n));
                }
            }
            foreach (var kv in dist)
            {
                if (kv.Key == start) continue;
                if (s.UnitAt(kv.Key) != null) continue; // cannot end on an occupied hex
                var path = new List<Hex> { kv.Key };
                var h = kv.Key;
                while (prev.TryGetValue(h, out var p)) { path.Add(p); h = p; }
                path.Reverse();
                result[kv.Key] = new MoveOption { Destination = kv.Key, Cost = kv.Value, Path = path };
            }
            return result;
        }

        // ------------------------------------------------------------------ line of sight

        /// <param name="viewerSeat">With fog of war, units hidden from this seat do not block the line (a player can't be
        /// told about an obstacle they cannot see). −1 = everyone blocks.</param>
        public static LosState LineOfSight(MatchState s, Hex from, Hex to, bool ignoreUnits, int ignoreUnitId = 0, int viewerSeat = -1)
        {
            if (s.IsRoster) return RosterTerrain.LineOfSight(s, from, to, ignoreUnits, ignoreUnitId, viewerSeat);
            if (from == to || Hex.Distance(from, to) <= 1) return LosState.Clear;
            LosState first = LosState.Clear;
            for (int pass = 0; pass < 2; pass++)
            {
                var line = Hex.Line(from, to, pass == 0 ? 1 : -1);
                LosState blocked = LosState.Clear;
                for (int i = 1; i < line.Count - 1; i++)
                {
                    var h = line[i];
                    if (!s.Map.Contains(h)) continue;
                    if (TerrainRules.BlocksSight(s.Map.Terrain(h)) || s.BulwarkAt(h) != null) { blocked = LosState.BlockedByTerrain; break; }
                    if (s.HasSmoke(h)) { blocked = LosState.BlockedBySmoke; break; }
                    if (!ignoreUnits)
                    {
                        var occ = s.UnitAt(h);
                        if (occ != null && occ.Id != ignoreUnitId && (viewerSeat < 0 || Vision.CanSee(s, viewerSeat, occ))) { blocked = LosState.BlockedByUnit; break; }
                    }
                }
                if (blocked == LosState.Clear) return LosState.Clear;
                if (pass == 0) first = blocked;
            }
            return first;
        }

        public static string LosText(LosState l) => l switch
        {
            LosState.BlockedByTerrain => "Line of sight blocked by terrain",
            LosState.BlockedByUnit => "Line of sight blocked by a unit",
            LosState.BlockedBySmoke => "Line of sight blocked by smoke",
            _ => "Clear line of sight",
        };
    }
}
