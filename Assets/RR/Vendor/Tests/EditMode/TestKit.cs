using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;

namespace Commandfall.Tests
{
    /// <summary>Builds precise battlefield scenarios for rules tests (bypasses deployment zones on purpose).</summary>
    public sealed class Scenario
    {
        public const string OpenMap = "test_open";
        public const string LosMap = "test_los";

        static Scenario()
        {
            if (!MapLibrary.Maps.ContainsKey(OpenMap))
                MapLibrary.Maps[OpenMap] = new MapDefinition(OpenMap, "Test Open", "All open ground.");
            if (!MapLibrary.Maps.ContainsKey(LosMap))
                MapLibrary.Maps[LosMap] = new MapDefinition(LosMap, "Test LoS", "Elevated perch, obstacle, cover, hazard.")
                    .Mirror(TerrainType.Elevated, (0, 2))
                    .Mirror(TerrainType.Obstacle, (2, 0))
                    .Mirror(TerrainType.Cover, (-2, 1))
                    .Mirror(TerrainType.Hazard, (-3, 2));
        }

        sealed class Spec { public int Seat; public FactionId Faction; public Archetype Arch; public Hex Hex; public string[] Equip; public int Hp = -1; }

        readonly string _map;
        readonly List<Spec> _specs = new List<Spec>();
        public int Budget = 200;
        public int MaxUnits = 7;
        public int ClockSeconds = 0;
        public int ActivationLimit = 60;
        public StalemateRule Stalemate = StalemateRule.DefenderDenial;
        public int FirstSeat = 0;
        public bool FogOfWar;
        public Doctrine[] Doctrines = { Doctrine.Assault, Doctrine.Assault };
        public TechCaps Caps = new TechCaps(5);
        int[] _ids;

        public Scenario(string map = OpenMap) { _map = map; }

        /// <summary>Adds a unit and returns a handle; resolve it with <see cref="Id"/> after <see cref="Build"/>.</summary>
        public int Add(int seat, FactionId f, Archetype a, int q, int r, params string[] equip)
        {
            _specs.Add(new Spec { Seat = seat, Faction = f, Arch = a, Hex = new Hex(q, r), Equip = equip });
            return _specs.Count - 1;
        }

        public Scenario WithHp(int handle, int hp) { _specs[handle].Hp = hp; return this; }

        public int Id(int handle) => _ids[handle];

        public MatchState Build()
        {
            // Every seat needs exactly one Commander; add one in a far corner if the test did not place it.
            var corners = new[] { new Hex(-3, 3), new Hex(3, -3) };
            for (int seat = 0; seat < 2; seat++)
            {
                if (_specs.Any(sp => sp.Seat == seat && sp.Arch == Archetype.Commander)) continue;
                var f = _specs.FirstOrDefault(sp => sp.Seat == seat)?.Faction ?? (seat == 0 ? FactionId.Meeples : FactionId.Meeples);
                _specs.Add(new Spec { Seat = seat, Faction = f, Arch = Archetype.Commander, Hex = corners[seat], Equip = Array.Empty<string>() });
            }
            var cfg = new MatchConfig
            {
                MapId = _map, PointBudget = Budget, MaxUnits = MaxUnits, ClockSeconds = ClockSeconds, ActivationLimit = ActivationLimit,
                Stalemate = Stalemate, FirstSeat = FirstSeat, DefenderSeat = 1 - FirstSeat, TechMode = TechMode.Advanced,
                FogOfWar = FogOfWar,
            };
            var order = new List<int>();
            for (int seat = 0; seat < 2; seat++)
            {
                var mine = Enumerable.Range(0, _specs.Count).Where(i => _specs[i].Seat == seat).ToList();
                var faction = _specs[mine[0]].Faction;
                var army = new ArmyRoster { Name = "Test", Faction = faction, Doctrine = faction == FactionId.Inhumanus ? Doctrines[seat] : Doctrine.None };
                foreach (var i in mine)
                {
                    var sp = _specs[i];
                    if (sp.Faction != faction) throw new ArgumentException("One faction per seat in scenarios.");
                    army.Units.Add(new RosterEntry(GameContent.UnitFor(sp.Faction, sp.Arch).Id, sp.Equip));
                    order.Add(i);
                }
                cfg.Players[seat] = new PlayerSetup { DisplayName = $"P{seat}", Army = army, Caps = Caps.Clone() };
            }
            var s = RulesEngine.CreateMatch(cfg, out var err);
            if (s == null) throw new InvalidOperationException(err);
            _ids = new int[_specs.Count];
            for (int k = 0; k < order.Count; k++)
            {
                var sp = _specs[order[k]];
                var u = s.Units[k];
                _ids[order[k]] = u.Id;
                u.Position = sp.Hex;
                u.Deployed = true;
                if (sp.Hp > 0) u.Hp = sp.Hp;
            }
            s.Players[0].DeploymentCommitted = s.Players[1].DeploymentCommitted = true;
            s.Phase = MatchPhase.Battle;
            s.ActiveSeat = FirstSeat;
            return s;
        }

        // ------------------------------------------------------------------ shortcuts

        public static CommandResult Do(MatchState s, Command c) => RulesEngine.Apply(s, c);

        public static void Ok(MatchState s, Command c)
        {
            var r = RulesEngine.Apply(s, c);
            if (!r.Accepted) throw new NUnit.Framework.AssertionException($"Expected {c} to be accepted but: {r.Reason}");
        }

        public static string Rejected(MatchState s, Command c)
        {
            var before = Serialization.StateHash(s);
            var r = RulesEngine.Apply(s, c);
            if (r.Accepted) throw new NUnit.Framework.AssertionException($"Expected {c} to be rejected.");
            if (Serialization.StateHash(s) != before) throw new NUnit.Framework.AssertionException("A rejected command mutated the state.");
            return r.Reason;
        }

        /// <summary>Hands the turn to the other seat by passing (keeps tests readable).</summary>
        public static void PassTurn(MatchState s) => Ok(s, Command.Pass(s.ActiveSeat));

        public static MatchConfig PresetConfig(FactionId a, FactionId b, string map = MapLibrary.DefaultMapId, int clock = 150)
        {
            var cfg = new MatchConfig { MapId = map, ClockSeconds = clock };
            cfg.Players[0] = new PlayerSetup { DisplayName = "A", Army = ArmyPresets.Default(a).Clone(), Caps = new TechCaps(3) };
            cfg.Players[1] = new PlayerSetup { DisplayName = "B", Army = ArmyPresets.Default(b).Clone(), Caps = new TechCaps(3) };
            return cfg;
        }

        /// <summary>Creates a preset match and auto-deploys both sides into the battle phase.</summary>
        public static MatchState DeployedPreset(FactionId a, FactionId b, string map = MapLibrary.DefaultMapId, int clock = 150)
        {
            var s = RulesEngine.CreateMatch(PresetConfig(a, b, map, clock));
            Ok(s, Command.AutoDeploy(0));
            Ok(s, Command.AutoDeploy(1));
            return s;
        }
    }
}
