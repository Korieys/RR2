using System;
using System.Collections.Generic;
using System.Linq;

namespace RR.Strategy
{
    // Engine-free strategic state for the vertical slice. Presentation reads it and submits commands through
    // StrategyRules; it never mutates these objects directly. Ids are stable strings shared by the galaxy view,
    // system view, inspectors, battle preparation, results and save data.

    public enum StarClass { BlueGiant, White, Yellow, Orange, RedDwarf, Violet, Dim }
    public enum PlanetKind { Terran, Ocean, Crystal, Barren, Volcanic, Ice, GasGiant, Toxic }
    public enum SystemDetail { Distant, Local, Hero }
    public enum ProjectKind { CommissionShip, CrystalFoundry }
    public enum EngagementState { Pending, Resolved, Retreated }
    public enum ConquestStage { None = 0, OrbitSecured = 1, LandingEstablished = 2, Conquered = 3 }

    public struct Resources : IEquatable<Resources>
    {
        public int Energy, Materials, Credits, Influence;
        public Resources(int energy, int materials, int credits, int influence = 0) { Energy = energy; Materials = materials; Credits = credits; Influence = influence; }
        public static Resources operator +(Resources a, Resources b) => new Resources(a.Energy + b.Energy, a.Materials + b.Materials, a.Credits + b.Credits, a.Influence + b.Influence);
        public static Resources operator -(Resources a, Resources b) => new Resources(a.Energy - b.Energy, a.Materials - b.Materials, a.Credits - b.Credits, a.Influence - b.Influence);
        public bool Covers(Resources cost) => Energy >= cost.Energy && Materials >= cost.Materials && Credits >= cost.Credits && Influence >= cost.Influence;
        public Resources Half() => new Resources(Energy / 2, Materials / 2, Credits / 2, Influence / 2);
        public bool Equals(Resources o) => Energy == o.Energy && Materials == o.Materials && Credits == o.Credits && Influence == o.Influence;
        public override bool Equals(object obj) => obj is Resources r && Equals(r);
        public override int GetHashCode() => (Energy * 397) ^ (Materials * 31) ^ (Credits * 7) ^ Influence;
        public override string ToString() => $"E{Energy} M{Materials} C{Credits} I{Influence}";
    }

    public sealed class Polity
    {
        public string Id;
        public string Name;
        /// <summary>Faction accent as 0xRRGGBB (sampled from the approved faction map where available).</summary>
        public int Color;
        public bool PlayerControlled;
        /// <summary>Has a Commandfall roster (the two slice factions); others are present as territory only.</summary>
        public bool Playable;
        public string CommandfallFaction;   // Commandfall.Core.FactionId name, or null
        public Resources Stock;
    }

    public sealed class Sector
    {
        public string Id;
        public string Name;
        public string Subtitle;
        public string OwnerId;              // null = unclaimed
        public double X, Y;                  // galactic data coordinates (light years, +Y = galactic north on the map)
        public double Radius;                // east-west radius (light years)
        public double Aspect = 1;            // east-west radius over north-south radius (the map's sectors are wide ovals)
        public bool Eclipse;                 // the Eclipse Sector's presentation mood
        public bool ProvisionalName;
    }

    public enum MapFeatureKind { GalacticHeart, OriginVortex, Disputed, Incursion }

    /// <summary>
    /// Something the approved faction map draws that is not a sector, system or lane: the galactic heart inside the Eclipse,
    /// the Craigon origin vortex, disputed zones and incursion routes. Presentation only; no rule reads these.
    /// </summary>
    public sealed class MapFeature
    {
        public string Id;
        public MapFeatureKind Kind;
        public string PolityId;              // the incursion or origin owner; null for neutral features
        public double Radius;                // light years (zones, vortex, heart)
        public List<double> Points = new List<double>();   // x0, y0, x1, y1, ... in light years; one point for zones
    }

    public sealed class Planet
    {
        public string Id;
        public string Name;
        public PlanetKind Kind;
        public double OrbitRadius;          // local-system units (presentation maps them)
        public double Size;                  // relative radius
        public double OrbitPhase;           // radians at day 0
        public bool Rings;
        public int Moons;
        public string OwnerId;
        public long Population;              // inhabitants (display only in this slice)
        public int Development;              // 0..5
        public Resources DailyYield;
        public int Foundries;                // Crystal Foundry districts built
        public bool HasShipyard;
        public ProjectState Project;         // one active project
        public bool ProvisionalName;
        public int Seed;
    }

    public sealed class ProjectState
    {
        public ProjectKind Kind;
        public string ShipDefId;             // for CommissionShip
        public Resources Cost;
        public int DaysTotal;
        public int DaysDone;
    }

    public sealed class StarSystem
    {
        public string Id;
        public string Name;
        public string SectorId;
        public double X, Y, Z;               // data coordinates (Z = small disk-height offset)
        public StarClass Star;
        public SystemDetail Detail;
        public string OwnerId;               // sovereign polity (planets), null = unclaimed
        public string OrbitalControlId;      // who holds orbit; differs from OwnerId after a lost orbital battle
        public string ConquestBy;            // polity pressing a conquest here
        public ConquestStage Conquest;
        public bool HasStation;
        public string BattlefieldMapId;      // Commandfall orbital map used for engagements here
        public List<Planet> Planets = new List<Planet>();
        public bool ProvisionalName;
        public int Seed;

        public bool Blockaded => OwnerId != null && OrbitalControlId != null && OrbitalControlId != OwnerId;
    }

    public sealed class Lane
    {
        public string A, B;
        public bool Major;
        public int Days;                     // travel time for a standard fleet
        public bool Connects(string a, string b) => (A == a && B == b) || (A == b && B == a);
        public string Other(string id) => A == id ? B : B == id ? A : null;
    }

    public sealed class ShipRecord
    {
        public int Uid;
        public string DefId;                 // Commandfall roster codex id
        public int Hp;                       // carried between battles; repaired only by explicit rules (none in the slice)
    }

    public sealed class MoveOrder
    {
        public List<string> Path = new List<string>();   // systems to visit after the current one, in order
        public int DaysIntoLane;                           // progress on the lane toward Path[0]
        public string FromSystemId;                        // system the fleet left
    }

    public sealed class Fleet
    {
        public string Id;
        public string Name;
        public string OwnerId;
        public string SystemId;              // current system, or the system it departed while moving
        public List<ShipRecord> Ships = new List<ShipRecord>();
        public MoveOrder Order;              // null = holding
        public string HomeSystemId;          // retreat destination
        public string LastSystemId;          // previous system (retreat fallback)
        public string EngagementId;          // committed to an engagement
        public bool AIControlled;
        public string AITarget;              // AI intent (raid target system)
        public int AIDepartDay;

        public bool Moving => Order != null && Order.Path.Count > 0;
    }

    public sealed class Engagement
    {
        public string Id;
        public string SystemId;
        public string AttackerFleetId;       // the fleet that arrived
        public string DefenderFleetId;
        public int Day;
        public EngagementState State;
        public string BattleId;              // set when a battle is committed
        public string ResultSummary;
    }

    public sealed class LogEntry
    {
        public int Day;
        public string Kind;                  // "info", "order", "danger", "result", "build"
        public string Text;
    }

    public sealed class GalaxyState
    {
        public const string Schema = "rr.slice.galaxy";
        /// <summary>2: the vertical-slice campaign container (<see cref="Slice"/>); version 1 saves migrate with none.</summary>
        public const int CurrentVersion = 2;

        public int Version = CurrentVersion;
        public string ScenarioId = "eclipse_frontier";
        public int Seed;
        public int Day;
        public uint Rng = 0x9E3779B9u;
        public int NextUid = 1;
        public int NextId = 1;
        public List<Polity> Polities = new List<Polity>();
        public List<Sector> Sectors = new List<Sector>();
        public List<StarSystem> Systems = new List<StarSystem>();
        public List<Lane> Lanes = new List<Lane>();
        public List<MapFeature> Features = new List<MapFeature>();
        public List<Fleet> Fleets = new List<Fleet>();
        public List<Engagement> Engagements = new List<Engagement>();
        public HashSet<string> AppliedBattles = new HashSet<string>(StringComparer.Ordinal);
        public List<LogEntry> Log = new List<LogEntry>();
        /// <summary>A battle in progress when the game was saved (setup, accepted commands and hashes), or null. Optional
        /// in the version 1 format: older saves have none.</summary>
        public BattleSnapshot ActiveBattle;
        /// <summary>The passage campaign's state (objectives, passages, relations, ship profiles, history, wrecks, settlements,
        /// expeditions), or null in the original Aurelith campaign.</summary>
        public SliceCampaignState Slice;

        public Polity GetPolity(string id) => Polities.FirstOrDefault(p => p.Id == id);
        public Polity Player => Polities.First(p => p.PlayerControlled);
        public StarSystem GetSystem(string id) => Systems.FirstOrDefault(s => s.Id == id);
        public Sector GetSector(string id) => Sectors.FirstOrDefault(s => s.Id == id);
        public MapFeature GetFeature(MapFeatureKind kind) => Features.FirstOrDefault(f => f.Kind == kind);
        public Fleet GetFleet(string id) => Fleets.FirstOrDefault(f => f.Id == id);
        public Engagement GetEngagement(string id) => Engagements.FirstOrDefault(e => e.Id == id);
        public Planet GetPlanet(string id) => Systems.SelectMany(s => s.Planets).FirstOrDefault(p => p.Id == id);
        public StarSystem SystemOf(Planet p) => Systems.FirstOrDefault(s => s.Planets.Contains(p));
        public IEnumerable<Fleet> FleetsAt(string systemId) => Fleets.Where(f => f.SystemId == systemId && !f.Moving && f.Ships.Count > 0);
        public Engagement PendingEngagement => Engagements.FirstOrDefault(e => e.State == EngagementState.Pending);
        public IEnumerable<Lane> LanesOf(string systemId) => Lanes.Where(l => l.A == systemId || l.B == systemId);
        public Lane LaneBetween(string a, string b) => Lanes.FirstOrDefault(l => l.Connects(a, b));

        public uint NextRandom()
        {
            uint x = Rng; x ^= x << 13; x ^= x >> 17; x ^= x << 5; Rng = x == 0 ? 0x6D2B79F5u : x; return Rng;
        }

        public void Note(string kind, string text) => Log.Add(new LogEntry { Day = Day, Kind = kind, Text = text });

        public static string DateLabel(int day)
        {
            // Provisional calendar: 30-day months, 12-month years, starting year 3104.
            int year = 3104 + day / 360, month = day / 30 % 12 + 1, d = day % 30 + 1;
            return $"Day {d}, Month {month}, {year}";
        }
    }
}