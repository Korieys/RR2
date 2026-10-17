using System;
using System.Collections.Generic;
using System.Linq;

namespace Commandfall.Core
{
    [Serializable]
    public sealed class TechCaps
    {
        public int Weapon = 3, Defense = 3, Utility = 3, Vehicle = 3;
        /// <summary>Full Roster: the highest unit unlock tier the army may field (Weapon = weapon research, Defense = armor research).</summary>
        public int Unlock = 3;

        public TechCaps() { }
        public TechCaps(int all) { Weapon = Defense = Utility = Vehicle = Unlock = all; }
        public TechCaps(int weapon, int defense, int utility, int vehicle) { Weapon = weapon; Defense = defense; Utility = utility; Vehicle = vehicle; Unlock = Math.Max(utility, vehicle); }

        public int CapFor(EquipmentCategory c) => c switch
        {
            EquipmentCategory.Weapon => Weapon,
            EquipmentCategory.Defense => Defense,
            EquipmentCategory.Utility => Utility,
            _ => Vehicle,
        };

        public TechCaps Clone() => new TechCaps(Weapon, Defense, Utility, Vehicle) { Unlock = Unlock };
        public override string ToString() => $"W{Weapon}/D{Defense}/U{Utility}/V{Vehicle}";
        public static TechCaps ForMode(TechMode mode) => mode == TechMode.Advanced ? new TechCaps(5) : new TechCaps(3);
    }

    /// <summary>One unit in an army list: a unit definition plus the equipment mounted in its slots.</summary>
    [Serializable]
    public sealed class RosterEntry
    {
        public string UnitId;
        public List<string> Equipment = new List<string>();

        public RosterEntry() { }
        public RosterEntry(string unitId, params string[] equipment) { UnitId = unitId; Equipment = equipment.Where(e => e != null).ToList(); }
        public RosterEntry Clone() => new RosterEntry { UnitId = UnitId, Equipment = new List<string>(Equipment) };
    }

    [Serializable]
    public sealed class ArmyRoster
    {
        public string Name = "Custom Army";
        public FactionId Faction;
        public Doctrine Doctrine;
        public List<RosterEntry> Units = new List<RosterEntry>();

        public ArmyRoster Clone() => new ArmyRoster
        {
            Name = Name, Faction = Faction, Doctrine = Doctrine, Units = Units.Select(u => u.Clone()).ToList()
        };
    }

    [Serializable]
    public sealed class PlayerSetup
    {
        public string DisplayName = "Commander";
        public ArmyRoster Army = new ArmyRoster();
        public TechCaps Caps = new TechCaps();
        public bool IsAI;
        public AIDifficulty AIDifficulty = AIDifficulty.Tactical;

        public PlayerSetup Clone() => new PlayerSetup
        {
            DisplayName = DisplayName, Army = Army.Clone(), Caps = Caps.Clone(), IsAI = IsAI, AIDifficulty = AIDifficulty
        };
    }

    [Serializable]
    public sealed class MatchConfig
    {
        public const int ProtocolVersion = 4;

        /// <summary>Classic (archetype/equipment) or Full Roster (the 156-unit Unit Codex).</summary>
        public Ruleset Ruleset = Ruleset.Classic;
        /// <summary>Full Roster: planetary (infantry and vehicles) or orbital (ships) battle.</summary>
        public BattleDomain Domain = BattleDomain.Planetary;
        /// <summary>Full Roster: rounds before the battle is decided on objectives (0 = unlimited).</summary>
        public int RoundLimit = 12;

        public string MapId = MapLibrary.DefaultMapId;
        public PlayerSetup[] Players = { new PlayerSetup(), new PlayerSetup() };
        /// <summary>Seat that takes the first activation (the attacker).</summary>
        public int FirstSeat = 0;
        /// <summary>Per-player decision time in seconds; 0 = untimed.</summary>
        public int ClockSeconds = 150;
        /// <summary>Grace period after every committed action during which the next player's clock does not run (covers resolution animation).</summary>
        public int ClockGraceMs = 1500;
        /// <summary>Standard activation limit for the 115-hex battlefields (was 60 on the 37-hex board).</summary>
        public const int StandardActivationLimit = 90;
        public int ActivationLimit = StandardActivationLimit;
        public StalemateRule Stalemate = StalemateRule.DefenderDenial;
        /// <summary>Explicit defender for <see cref="StalemateRule.DefenderDenial"/>. Default: the seat that moves second.</summary>
        public int DefenderSeat = 1;
        public TechMode TechMode = TechMode.Balanced;
        public int PointBudget = 18;
        public int MaxUnits = 7;
        public string Label = "";
        /// <summary>Hide enemy units your army cannot see (see <see cref="Vision"/>).</summary>
        public bool FogOfWar;

        public bool Timed => ClockSeconds > 0;

        public MatchConfig Clone() => new MatchConfig
        {
            MapId = MapId, Players = Players.Select(p => p.Clone()).ToArray(), FirstSeat = FirstSeat, ClockSeconds = ClockSeconds,
            ClockGraceMs = ClockGraceMs, ActivationLimit = ActivationLimit, Stalemate = Stalemate, DefenderSeat = DefenderSeat,
            TechMode = TechMode, PointBudget = PointBudget, MaxUnits = MaxUnits, Label = Label, FogOfWar = FogOfWar,
            Ruleset = Ruleset, Domain = Domain, RoundLimit = RoundLimit,
        };

        public bool IsRoster => Ruleset == Ruleset.FullRoster;

        /// <summary>Full Roster defaults: 30 points, 8 units, 12 rounds (see Docs/UNIT_CODEX_IMPLEMENTATION.md).</summary>
        public const int RosterBudget = 30, RosterMaxUnits = 8, RosterRounds = 12;

        /// <summary>Switches this config to the Full Roster ruleset with its default army limits.</summary>
        public MatchConfig UseRoster(BattleDomain domain)
        {
            Ruleset = Ruleset.FullRoster;
            Domain = domain;
            PointBudget = RosterBudget;
            MaxUnits = RosterMaxUnits;
            RoundLimit = RosterRounds;
            if (domain == BattleDomain.Orbital && !MapId.StartsWith("orbital")) MapId = MapLibrary.DefaultOrbitalMapId;
            if (domain == BattleDomain.Planetary && MapId.StartsWith("orbital")) MapId = MapLibrary.DefaultMapId;
            return this;
        }

        /// <summary>For a rematch the attacker/defender roles rotate.</summary>
        public MatchConfig ForRematch()
        {
            var c = Clone();
            c.FirstSeat = 1 - FirstSeat;
            c.DefenderSeat = 1 - c.FirstSeat;
            return c;
        }
    }
}
