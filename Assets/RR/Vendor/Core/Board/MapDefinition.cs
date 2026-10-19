using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Commandfall.Core
{
    /// <summary>
    /// Authored battlefield layout. Every playable hex has exactly one canonical terrain entry.
    /// The outline is a (possibly elongated) hexagon: rows run from R = −<see cref="Radius"/> (North, seat 1) to
    /// R = +<see cref="Radius"/> (South, seat 0) and |Q|, |S| ≤ <see cref="HalfWidth"/>; <see cref="Trim"/> can cut it into
    /// an irregular shape. Seat 0 deploys in the South rows, seat 1 in the North rows (<see cref="DeploymentDepth"/> deep).
    /// </summary>
    public sealed class MapDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        /// <summary>Half depth of the board: the largest |R|.</summary>
        public int Radius = 3;
        /// <summary>Half width of the outline: the largest |Q| and |S| (equal to <see cref="Radius"/> for a regular hexagon).</summary>
        public int HalfWidth = 3;
        public int DeploymentDepth = 2;
        /// <summary>Planetary ground or an orbital (space) battlefield. In orbit the terrain types read as: Cover = asteroid
        /// field, Elevated = nebula, Obstacle = large wreck, Hazard = ion storm (see <see cref="RosterTerrain"/>).</summary>
        public BattleDomain Domain = BattleDomain.Planetary;
        readonly Dictionary<Hex, TerrainType> _tiles = new Dictionary<Hex, TerrainType>();
        readonly List<Scenery> _scenery = new List<Scenery>();
        readonly List<Hex> _deadEndAnchors = new List<Hex>();
        List<Hex> _ordered;
        MapPaths _paths;

        public IReadOnlyList<Hex> AllHexes => _ordered ??= _tiles.Keys.OrderBy(h => h).ToList();
        public int TileCount => _tiles.Count;
        public bool Contains(Hex h) => _tiles.ContainsKey(h);
        public TerrainType Terrain(Hex h) => _tiles.TryGetValue(h, out var t) ? t : TerrainType.Obstacle;
        /// <summary>Decorative, non-blocking dressing (see <see cref="SceneryKind"/>).</summary>
        public IReadOnlyList<Scenery> Scenery => _scenery;
        /// <summary>Hexes inside the dead ends this map has on purpose (alcoves); the topology validator accepts a
        /// dead-end region only if it contains one of these.</summary>
        public IReadOnlyList<Hex> IntentionalDeadEnds => _deadEndAnchors;
        /// <summary>Result of the topology validation done when the map was registered (null for ad-hoc maps).</summary>
        public TopologyReport Topology { get; set; }
        /// <summary>Repairs the validator applied at registration (empty when the authored layout passed as is).</summary>
        public List<string> RepairLog { get; } = new List<string>();
        /// <summary>Static shortest-path distances over the terrain (cached; units and bulwarks are not considered).</summary>
        public MapPaths Paths => _paths ??= new MapPaths(this);

        /// <summary>A regular hexagon of the given radius (37 hexes for radius 3).</summary>
        public MapDefinition(string id, string name, string description, int radius = 3) : this(id, name, description, radius, radius) { }

        /// <summary>An elongated hexagon: <paramref name="halfDepth"/> rows each side of the centre line, and
        /// <paramref name="halfWidth"/> ≥ <paramref name="halfDepth"/> bounding |Q| and |S|. The centre row is
        /// 2·halfWidth + 1 hexes wide, the back rows 2·halfWidth − halfDepth + 1.</summary>
        public MapDefinition(string id, string name, string description, int halfWidth, int halfDepth)
        {
            if (halfWidth < halfDepth) throw new ArgumentException("Half width must be at least the half depth.");
            Id = id; Name = name; Description = description; Radius = halfDepth; HalfWidth = halfWidth;
            for (int r = -halfDepth; r <= halfDepth; r++)
                for (int q = Math.Max(-halfWidth, -halfWidth - r); q <= Math.Min(halfWidth, halfWidth - r); q++)
                    _tiles[new Hex(q, r)] = TerrainType.Open;
        }

        /// <summary>True when the outline is exactly the hexagon given by <see cref="Radius"/> and <see cref="HalfWidth"/>.</summary>
        public bool IsFullOutline
        {
            get
            {
                int expected = 0;
                for (int r = -Radius; r <= Radius; r++) expected += Math.Min(HalfWidth, HalfWidth - r) - Math.Max(-HalfWidth, -HalfWidth - r) + 1;
                return expected == _tiles.Count;
            }
        }

        void Invalidate() { _ordered = null; _paths = null; Topology = null; }

        /// <summary>Sets terrain on a hex and on its 180° rotation so the layout stays balanced.</summary>
        public MapDefinition Mirror(TerrainType t, params (int q, int r)[] hexes)
        {
            foreach (var (q, r) in hexes)
            {
                var h = new Hex(q, r);
                if (!_tiles.ContainsKey(h)) throw new ArgumentException($"{h} is off the board");
                _tiles[h] = t;
                _tiles[h.Rotate180()] = t;
            }
            Invalidate();
            return this;
        }

        /// <summary>Sets terrain on single hexes without mirroring (test maps and repairs only).</summary>
        public MapDefinition Set(TerrainType t, params Hex[] hexes)
        {
            foreach (var h in hexes)
            {
                if (!_tiles.ContainsKey(h)) throw new ArgumentException($"{h} is off the board");
                _tiles[h] = t;
            }
            Invalidate();
            return this;
        }

        /// <summary>Removes hexes (and their 180° rotations) from the board: irregular outlines.</summary>
        public MapDefinition Trim(params (int q, int r)[] hexes)
        {
            foreach (var (q, r) in hexes)
            {
                var h = new Hex(q, r);
                _tiles.Remove(h);
                _tiles.Remove(h.Rotate180());
            }
            _scenery.RemoveAll(sc => !_tiles.ContainsKey(sc.At));
            Invalidate();
            return this;
        }

        /// <summary>Adds non-blocking decorative scenery on hexes and their 180° rotations.</summary>
        public MapDefinition Decorate(SceneryKind kind, params (int q, int r)[] hexes)
        {
            foreach (var (q, r) in hexes)
            {
                var h = new Hex(q, r);
                if (!_tiles.ContainsKey(h)) throw new ArgumentException($"{h} is off the board");
                _scenery.Add(new Scenery(h, kind));
                if (h != h.Rotate180()) _scenery.Add(new Scenery(h.Rotate180(), kind));
            }
            return this;
        }

        /// <summary>Sets how many rows deep each deployment territory is.</summary>
        public MapDefinition Deployment(int depth)
        {
            DeploymentDepth = depth;
            Invalidate();
            return this;
        }

        /// <summary>Marks the battlefield as an orbital (space) map.</summary>
        public MapDefinition Orbital()
        {
            Domain = BattleDomain.Orbital;
            return this;
        }

        /// <summary>Marks a deliberate dead end (an alcove) by a hex inside it, and its 180° rotation.</summary>
        public MapDefinition DeclareDeadEnd(params (int q, int r)[] hexes)
        {
            foreach (var (q, r) in hexes)
            {
                var h = new Hex(q, r);
                if (!_deadEndAnchors.Contains(h)) _deadEndAnchors.Add(h);
                if (!_deadEndAnchors.Contains(h.Rotate180())) _deadEndAnchors.Add(h.Rotate180());
            }
            Topology = null;
            return this;
        }

        public bool IsDeploymentHex(int seat, Hex h)
        {
            if (!Contains(h)) return false;
            if (!TerrainRules.Deployable(Terrain(h))) return false;
            return ZoneSeat(h) == seat;
        }

        /// <summary>First row (by |R|) of the deployment territories: 2 for radius 3, 3 for radius 4.</summary>
        public int DeploymentEdge => Radius - DeploymentDepth + 1;

        /// <summary>The seat whose deployment rows contain <paramref name="h"/>, or −1 for the rows between them.</summary>
        public int ZoneSeat(Hex h) => h.R >= DeploymentEdge ? 0 : h.R <= -DeploymentEdge ? 1 : -1;

        public List<Hex> DeploymentZone(int seat) => AllHexes.Where(h => IsDeploymentHex(seat, h)).ToList();

        /// <summary>True when the layout is identical under 180° rotation (fairness invariant, unit tested).</summary>
        public bool IsPointSymmetric() =>
            _tiles.All(kv => _tiles.TryGetValue(kv.Key.Rotate180(), out var t) && t == kv.Value);

        public MapDefinition Clone()
        {
            var m = new MapDefinition(Id, Name, Description, HalfWidth, Radius) { DeploymentDepth = DeploymentDepth, Domain = Domain };
            m._tiles.Clear();
            foreach (var kv in _tiles) m._tiles[kv.Key] = kv.Value;
            m._scenery.AddRange(_scenery);
            m._deadEndAnchors.AddRange(_deadEndAnchors);
            return m;
        }

        /// <summary>Stable text of everything that affects play (outline, terrain, deployment): part of the content hash,
        /// so two builds with different layouts refuse to play each other.</summary>
        public string LayoutSignature()
        {
            var sb = new StringBuilder();
            sb.Append(Id).Append(':').Append(Domain == BattleDomain.Orbital ? "orbit:" : "").Append(HalfWidth).Append('x').Append(Radius).Append(':').Append(DeploymentDepth).Append(':');
            foreach (var h in AllHexes) sb.Append(h.Q).Append(',').Append(h.R).Append('=').Append((int)Terrain(h)).Append(';');
            return sb.ToString();
        }

        /// <summary>Rows as text (South at the bottom): '.' open, 'c' cover, '^' elevated, '#' obstacle, '~' hazard;
        /// optional per-hex overrides (diagnostics).</summary>
        public string Ascii(Func<Hex, char?> overlay = null)
        {
            var sb = new StringBuilder();
            int minLat = AllHexes.Min(h => h.Lateral), maxLat = AllHexes.Max(h => h.Lateral);
            for (int r = AllHexes.Min(h => h.R); r <= AllHexes.Max(h => h.R); r++)
            {
                sb.Append(r >= 0 ? " " : "").Append(r).Append(' ');
                for (int lat = minLat; lat <= maxLat; lat++)
                {
                    if (((lat - r) & 1) != 0) { sb.Append(' '); continue; }
                    var h = new Hex((lat - r) / 2, r);
                    if (!Contains(h)) { sb.Append(' '); continue; }
                    var o = overlay?.Invoke(h);
                    sb.Append(o ?? Glyph(Terrain(h)));
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static char Glyph(TerrainType t) => t switch
        {
            TerrainType.Cover => 'c', TerrainType.Elevated => '^', TerrainType.Obstacle => '#', TerrainType.Hazard => '~', _ => '.',
        };
    }

    public static class MapLibrary
    {
        public const string DefaultMapId = "shattered_citadel";
        public const string DefaultOrbitalMapId = "orbital_gate";

        /// <summary>Authored battlefields of one domain (test maps excluded).</summary>
        public static IEnumerable<MapDefinition> For(BattleDomain domain) =>
            Maps.Values.Where(m => m.Domain == domain && !m.Id.StartsWith("test") && !m.Id.StartsWith("legacy")).OrderBy(m => m.Id == DefaultMapId || m.Id == DefaultOrbitalMapId ? 0 : 1).ThenBy(m => m.Id, StringComparer.Ordinal);
        public static readonly Dictionary<string, MapDefinition> Maps = new Dictionary<string, MapDefinition>();

        /// <summary>Board outline shared by the authored battlefields (0.14): an elongated hexagon 9 rows deep and 15 hexes
        /// wide through the centre, 115 hexes — about three times the original 37-hex board's playable area.</summary>
        public const int BoardHalfWidth = 7, BoardHalfDepth = 4;
        /// <summary>Deployment territories are three rows deep (rows 2–4 from the centre line): an army may form up as
        /// close to the enemy as on the original board (front rows 4 apart) or hold back, and no-man's land is the three
        /// rows between. (With two-row territories at the back, AI matches made first contact at activation ~27 instead
        /// of ~8 and 92–100% ended at the activation limit.)</summary>
        public const int BoardDeploymentDepth = 3;

        static MapLibrary()
        {
            // Legend for the comments below (South, seat 0, at the bottom; Q grows East, lateral = 2Q + R):
            //   # obstacle   ^ high ground   c ruin cover   ~ unstable conduit   . open
            // Every map: point-symmetric, ≤ 7 blocking hexes, three approaches (west flank, centre, east flank) with
            // cross-connections on every row of no-man's land, no single-hex passages, and at most two dead ends.

            // The Shattered Citadel: the ruin's high heart in the centre, two shattered pillars standing between the centre
            // and each flank, low ruins to fight from, and conduits out on the wings.
            Register(new MapDefinition(DefaultMapId, "The Shattered Citadel",
                    "High ground at the heart of the ruin, shattered pillars between the centre and the wings, ruins to fight from and broad flanks to manoeuvre on.",
                    BoardHalfWidth, BoardHalfDepth)
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Elevated, (0, 0), (-2, 2), (5, 1))
                .Mirror(TerrainType.Obstacle, (-3, 0), (-6, 2))
                .Mirror(TerrainType.Cover, (-1, 1), (2, 1), (-5, 1), (4, 2), (-2, -1))
                .Mirror(TerrainType.Hazard, (-6, 0), (6, 1))
                .Decorate(SceneryKind.Rubble, (-4, 1), (1, 2), (-7, 3), (3, 3), (-1, 3))
                .Decorate(SceneryKind.Stump, (-2, 0), (6, -1), (-4, 4))
                .Decorate(SceneryKind.Crystals, (-7, 0), (5, 2), (7, -4))
                .Decorate(SceneryKind.Debris, (-3, 2), (1, 0), (-5, 3)));

            // Citadel Crossroads: a central monolith with ruins at its feet splits the middle; high ground, ruins and
            // conduits on the wings; and in each army's back corner a walled alcove (the board's only dead ends, on purpose):
            // a fortified nook behind a rock ridge, entered from one side — a place to hold, and a place to be trapped.
            Register(new MapDefinition("citadel_crossroads", "Citadel Crossroads",
                    "A central monolith splits the middle; high ground, ruins and conduits on the wings, and a walled alcove in each army's corner.",
                    BoardHalfWidth, BoardHalfDepth)
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (0, 0), (-7, 2), (-6, 2))
                .Mirror(TerrainType.Elevated, (-4, 1), (2, 1))
                .Mirror(TerrainType.Cover, (1, 0), (-3, 2), (4, 1), (-7, 3))
                .Mirror(TerrainType.Hazard, (0, 1), (-5, 0))
                .DeclareDeadEnd((-7, 3))
                .Decorate(SceneryKind.Rubble, (-2, 0), (3, 2), (-5, 2), (1, 3))
                .Decorate(SceneryKind.Stump, (-1, 1), (5, 0), (-3, 4))
                .Decorate(SceneryKind.Crystals, (-6, 0), (6, -1), (-1, -1))
                .Decorate(SceneryKind.Debris, (2, 2), (-4, 3)));

            // Proving Ground: an open training floor for the guided introduction, with one of everything.
            Register(new MapDefinition("proving_ground", "Proving Ground",
                    "An open training floor used for the guided introduction.",
                    BoardHalfWidth, BoardHalfDepth)
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Cover, (-1, 1), (4, 1))
                .Mirror(TerrainType.Obstacle, (2, 0), (-5, 1))
                .Mirror(TerrainType.Elevated, (-3, 2))
                .Mirror(TerrainType.Hazard, (6, 0))
                .Decorate(SceneryKind.Rubble, (-2, 3), (3, 2), (-6, 2))
                .Decorate(SceneryKind.Stump, (5, -2), (-7, 1))
                .Decorate(SceneryKind.Crystals, (6, 1)));

            RegisterOrbital();
        }

        static void RegisterOrbital()
        {
            // Orbital battlefields (Full Roster, orbital domain). Legend as above, read in space:
            //   # large wreck (blocks movement and sight)   c asteroid field (low cover)   ^ nebula (−1 range inside)
            //   ~ ion storm (1 damage when a ship ends a move in it)   . open space
            // The centre hex of each is an orbital gate (objective).

            // The Orbital Gate: an ancient gate ringed by wreckage, nebula banks on both wings.
            Register(new MapDefinition(DefaultOrbitalMapId, "The Orbital Gate",
                    "An ancient gate at the centre, ringed by the wrecks of earlier fleets; asteroid fields to hide in and nebula banks on the wings.",
                    BoardHalfWidth, BoardHalfDepth)
                .Orbital()
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (-2, 0), (-5, 2))
                .Mirror(TerrainType.Cover, (-1, -1), (3, -1), (-6, 1), (0, 2))
                .Mirror(TerrainType.Elevated, (-4, 0), (1, -2), (5, 1))
                .Mirror(TerrainType.Hazard, (-7, 1)));

            // The Wreck Field: the broken hulls of a capital-ship battle lie diagonally across the middle.
            Register(new MapDefinition("orbital_wreckfield", "The Wreck Field",
                    "The broken hulls of a capital-ship battle lie across the middle; asteroid cover between them and ion storms on the flanks.",
                    BoardHalfWidth, BoardHalfDepth)
                .Orbital()
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (-1, 0), (-4, 2), (3, 1))
                .Mirror(TerrainType.Cover, (0, 1), (3, -2), (-6, 0), (2, 2))
                .Mirror(TerrainType.Elevated, (-5, 1), (1, -1))
                .Mirror(TerrainType.Hazard, (6, 1)));

            // Rule and Ruin vertical slice: the contested trade passage (provisional name). Two short wreck banks pinch the
            // central lane into a three-hex passage over the centre objective; the wing objectives sit just outside the
            // banks; asteroid fields give firing positions at the passage mouths and on the flanks; nebulae and an ion
            // storm shape the wings. Validated like every authored map: three approaches, two cross-links, no dead ends.
            Register(new MapDefinition("trade_passage", "The Trade Passage",
                    "A charted trade lane pinched between two wreck banks; the passage itself is the prize, with open flanks around the banks.",
                    BoardHalfWidth, BoardHalfDepth)
                .Orbital()
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (-2, 0), (-3, 0))
                .Mirror(TerrainType.Cover, (0, 1), (-5, 1), (-2, -1))
                .Mirror(TerrainType.Elevated, (-4, -1), (-1, 2))
                .Mirror(TerrainType.Hazard, (-6, 2)));

            // The trade passage after a capital ship was destroyed there (Prompt 9): the same layout with the new wreckage
            // across the passage mouths, through the same validator (still no dead ends).
            Register(new MapDefinition("trade_passage_wrecked", "The Trade Passage, among the wreckage",
                    "The trade passage after a flagship died in it: fresh wreckage lies across both central approaches.",
                    BoardHalfWidth, BoardHalfDepth)
                .Orbital()
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (-2, 0), (-3, 0), (1, -3))
                .Mirror(TerrainType.Cover, (0, 1), (-5, 1), (-2, -1))
                .Mirror(TerrainType.Elevated, (-4, -1))
                .Mirror(TerrainType.Hazard, (-6, 2)));

            // The Eclipse expedition drifts (Prompt 10): two layouts; which one the derelict lies in is fixed when the
            // expedition is created. Nebula banks everywhere (sensor ranges are short in fog of war).
            Register(new MapDefinition("eclipse_drift_a", "The broken drift",
                    "A broken drift on the edge of The Eclipse Sector: ion storms on both flanks, nebula banks and a silent derelict.",
                    BoardHalfWidth, BoardHalfDepth)
                .Orbital()
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (0, -2), (3, -1))
                .Mirror(TerrainType.Cover, (-2, 1), (1, -1), (-6, 2))
                .Mirror(TerrainType.Elevated, (-3, 0), (2, 1), (-1, -1))
                .Mirror(TerrainType.Hazard, (-6, 1), (-4, -1)));
            Register(new MapDefinition("eclipse_drift_b", "The dense drift",
                    "A dense drift on the edge of The Eclipse Sector: ion storms close around the derelict at the centre.",
                    BoardHalfWidth, BoardHalfDepth)
                .Orbital()
                .Deployment(BoardDeploymentDepth)
                .Mirror(TerrainType.Obstacle, (-4, 0), (5, -1))
                .Mirror(TerrainType.Cover, (-6, 1), (3, 1))
                .Mirror(TerrainType.Elevated, (0, -2), (-3, 2), (2, -3))
                .Mirror(TerrainType.Hazard, (1, -1), (-2, 1)));
        }

        /// <summary>Validates a map's topology (repairing it deterministically if needed) and adds it to the library. A map
        /// that still fails is kept for diagnostics but <see cref="RulesEngine.CreateMatch"/> refuses to start on it.</summary>
        public static MapDefinition Register(MapDefinition m)
        {
            var report = TopologyValidator.Validate(m);
            if (!report.Passed) m = TopologyValidator.Repair(m, out report);
            m.Topology = report;
            Maps[m.Id] = m;
            return m;
        }

        public static MapDefinition Get(string id) => id != null && Maps.TryGetValue(id, out var m) ? m : Maps[DefaultMapId];
    }
}
