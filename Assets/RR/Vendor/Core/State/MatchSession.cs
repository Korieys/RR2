using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Commandfall.Core
{
    /// <summary>
    /// Owns one authoritative match: the state, the accepted command history (the replay log) and playtest statistics.
    /// The local controller, the network host and replays all wrap this.
    /// </summary>
    public sealed class MatchSession
    {
        public MatchConfig Config { get; }
        public MatchState State { get; private set; }
        public readonly List<Command> History = new List<Command>();
        public readonly MatchStats Stats;
        public event Action<CommandResult> Applied;
        public event Action<Command, string> Rejected;

        public MatchSession(MatchConfig config)
        {
            Config = config;
            State = RulesEngine.CreateMatch(config);
            Stats = new MatchStats(config, State);
        }

        public MatchSession(MatchConfig config, MatchState state)
        {
            Config = config;
            State = state;
            Stats = new MatchStats(config, state);
        }

        public CommandResult Submit(Command c)
        {
            var result = RulesEngine.Apply(State, c);
            if (!result.Accepted)
            {
                Stats.RecordRejected(c);
                Rejected?.Invoke(c, result.Reason);
                return result;
            }
            History.Add(c.Clone());
            Stats.Record(State, c, result);
            Applied?.Invoke(result);
            return result;
        }

        /// <summary>Replace the state wholesale (client recovery from a host snapshot).</summary>
        public void ReplaceState(MatchState s) => State = s;

        public ulong Hash => Serialization.StateHash(State);

        /// <summary>Re-simulates a match from its config and command log. Every command must be accepted.</summary>
        public static MatchState Replay(MatchConfig config, IList<Command> commands, int count = -1, List<CommandResult> results = null)
        {
            var s = RulesEngine.CreateMatch(config);
            int n = count < 0 ? commands.Count : Math.Min(count, commands.Count);
            for (int i = 0; i < n; i++)
            {
                var r = RulesEngine.Apply(s, commands[i].Clone());
                if (!r.Accepted) throw new InvalidDataException($"Replay diverged at command {i} ({commands[i]}): {r.Reason}");
                results?.Add(r);
            }
            return s;
        }
    }

    /// <summary>Serialized replay: config + accepted command log + final hash.</summary>
    public sealed class ReplayFile
    {
        public const uint Magic = 0x43465250; // "CFRP"
        public MatchConfig Config;
        public List<Command> Commands = new List<Command>();
        public ulong FinalHash;
        public ulong ContentHash;
        public string Title = "";
        public long CreatedUtcTicks;

        public byte[] ToBytes() => Serialization.ToBytes(w =>
        {
            w.Write(Magic); w.Write(ContentHash); w.Write(Title ?? ""); w.Write(CreatedUtcTicks);
            Serialization.Write(w, Config);
            w.Write(Commands.Count);
            foreach (var c in Commands) Serialization.Write(w, c);
            w.Write(FinalHash);
        });

        public static ReplayFile FromBytes(byte[] data) => Serialization.FromBytes(data, r =>
        {
            if (r.ReadUInt32() != Magic) throw new InvalidDataException("Not a Commandfall replay.");
            var f = new ReplayFile { ContentHash = r.ReadUInt64(), Title = r.ReadString(), CreatedUtcTicks = r.ReadInt64() };
            f.Config = Serialization.ReadConfig(r);
            int n = r.ReadInt32();
            for (int i = 0; i < n; i++) f.Commands.Add(Serialization.ReadCommand(r));
            f.FinalHash = r.ReadUInt64();
            return f;
        });

        public static ReplayFile From(MatchSession session, string title) => new ReplayFile
        {
            Config = session.Config, Commands = session.History.Select(c => c.Clone()).ToList(), FinalHash = session.Hash,
            ContentHash = Serialization.ContentHash(), Title = title, CreatedUtcTicks = DateTime.UtcNow.Ticks,
        };
    }

    /// <summary>Local, non-personal playtest instrumentation. Never leaves the machine unless the player exports it.</summary>
    public sealed class MatchStats
    {
        public string MatchId = Guid.NewGuid().ToString("N").Substring(0, 12);
        public string Mode = "local";
        public string MapId;
        public string[] Factions = new string[2];
        public string[] Doctrines = new string[2];
        public string[] TechCaps = new string[2];
        public List<string>[] Armies = { new List<string>(), new List<string>() };
        public int[] ArmyCost = new int[2];
        public int[] MaxTierUsed = new int[2];
        public double DurationSeconds;
        public int Activations;
        public int Winner = -1;
        public string WinningFaction = "";
        public string VictoryReason = "";
        public int[] DamageDealt = new int[2];
        public int[] UnitsLost = new int[2];
        public int[] Guards = new int[2];
        public int[] Passes = new int[2];
        public int[] IllegalAttempts = new int[2];
        public int[] CommanderThreatTurns = new int[2];
        public Dictionary<string, int> AbilityUses = new Dictionary<string, int>();
        public Dictionary<string, int> DamageByUnitType = new Dictionary<string, int>();
        public List<string>[] Survivors = { new List<string>(), new List<string>() };
        public string RematchChoice = "";
        public Feedback PlayerFeedback;

        public sealed class Feedback
        {
            public int Enjoyment, Clarity, Fairness; // 1-5
            public bool WouldPlayAgain;
            public string Strongest = "", Frustrating = "";
        }

        public MatchStats(MatchConfig config, MatchState initial)
        {
            MapId = config.MapId;
            for (int i = 0; i < 2; i++)
            {
                var p = config.Players[i];
                Factions[i] = p.Army.Faction.ToString();
                Doctrines[i] = p.Army.Doctrine.ToString();
                TechCaps[i] = p.Caps.ToString();
                foreach (var e in p.Army.Units)
                {
                    Armies[i].Add(e.Equipment.Count == 0 ? e.UnitId : $"{e.UnitId}[{string.Join("+", e.Equipment)}]");
                    foreach (var eq in e.Equipment) MaxTierUsed[i] = Math.Max(MaxTierUsed[i], GameContent.Equip(eq)?.Tier ?? 0);
                }
                ArmyCost[i] = ArmyRules.Validate(p.Army, p.Caps, config.PointBudget, config.MaxUnits).TotalCost;
            }
        }

        public void RecordRejected(Command c) { if (c.Seat == 0 || c.Seat == 1) IllegalAttempts[c.Seat]++; }

        public void Record(MatchState s, Command c, CommandResult r)
        {
            if (c.Type == CommandType.Guard) Guards[c.Seat]++;
            if (c.Type == CommandType.Pass) Passes[c.Seat]++;
            foreach (var e in r.Events)
            {
                switch (e.Type)
                {
                    case GameEventType.AbilityActivated:
                        AbilityUses.TryGetValue(e.Ability.ToString(), out var n); AbilityUses[e.Ability.ToString()] = n + 1; break;
                    case GameEventType.DamageApplied:
                    {
                        var src = s.Unit(e.UnitId);
                        if (src != null)
                        {
                            DamageDealt[src.Owner] += e.Amount;
                            DamageByUnitType.TryGetValue(src.DefId, out var d); DamageByUnitType[src.DefId] = d + e.Amount;
                        }
                        break;
                    }
                    case GameEventType.UnitDestroyed: UnitsLost[e.Seat]++; break;
                    case GameEventType.TurnChanged:
                    {
                        // Commander threat: can the newly active seat legally attack the enemy Commander right now?
                        var enemyCmd = s.Commander(1 - s.ActiveSeat);
                        if (enemyCmd != null && enemyCmd.Alive &&
                            s.LiveUnits(s.ActiveSeat).Any(u => u.OnBoard && Combat.Resolve(s, u, enemyCmd.Position).Legal))
                            CommanderThreatTurns[1 - s.ActiveSeat]++;
                        break;
                    }
                }
            }
            Activations = s.ActivationIndex;
            if (s.IsOver)
            {
                Winner = s.Winner;
                WinningFaction = s.Winner >= 0 ? s.Players[s.Winner].Faction.ToString() : "";
                VictoryReason = s.VictoryReason.ToString();
                for (int i = 0; i < 2; i++)
                {
                    Survivors[i].Clear();
                    foreach (var u in s.LiveUnits(i)) Survivors[i].Add($"{u.DefId}:{u.Hp}/{u.MaxHp}");
                }
            }
        }

        public string ToJson()
        {
            var j = new JsonWriter();
            j.Begin();
            j.Prop("matchId", MatchId); j.Prop("mode", Mode); j.Prop("map", MapId); j.Prop("contentVersion", GameContent.ContentVersion);
            j.Prop("durationSeconds", DurationSeconds); j.Prop("activations", Activations); j.Prop("winnerSeat", Winner);
            j.Prop("winningFaction", WinningFaction); j.Prop("victoryReason", VictoryReason); j.Prop("rematch", RematchChoice);
            for (int i = 0; i < 2; i++)
            {
                j.BeginObj($"seat{i}");
                j.Prop("faction", Factions[i]); j.Prop("doctrine", Doctrines[i]); j.Prop("techCaps", TechCaps[i]); j.Prop("armyCost", ArmyCost[i]);
                j.Prop("maxTierUsed", MaxTierUsed[i]); j.Prop("damageDealt", DamageDealt[i]); j.Prop("unitsLost", UnitsLost[i]);
                j.Prop("guards", Guards[i]); j.Prop("passes", Passes[i]); j.Prop("illegalAttempts", IllegalAttempts[i]);
                j.Prop("commanderThreatTurns", CommanderThreatTurns[i]);
                j.Array("army", Armies[i]); j.Array("survivors", Survivors[i]);
                j.End();
            }
            j.Dict("abilityUses", AbilityUses); j.Dict("damageByUnitType", DamageByUnitType);
            if (PlayerFeedback != null)
            {
                j.BeginObj("feedback");
                j.Prop("enjoyment", PlayerFeedback.Enjoyment); j.Prop("clarity", PlayerFeedback.Clarity); j.Prop("fairness", PlayerFeedback.Fairness);
                j.Prop("wouldPlayAgain", PlayerFeedback.WouldPlayAgain); j.Prop("strongest", PlayerFeedback.Strongest); j.Prop("frustrating", PlayerFeedback.Frustrating);
                j.End();
            }
            j.End();
            return j.ToString();
        }

        public static string CsvHeader =>
            "matchId,mode,map,durationSeconds,activations,winnerSeat,winningFaction,victoryReason,faction0,faction1,doctrine0,doctrine1,techCaps0,techCaps1," +
            "damage0,damage1,lost0,lost1,guards0,guards1,illegal0,illegal1,threat0,threat1,abilityUses,rematch,enjoyment,clarity,fairness,again,strongest,frustrating";

        public string ToCsvRow()
        {
            string Q(string v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";
            var ab = string.Join(";", AbilityUses.OrderBy(k => k.Key).Select(k => $"{k.Key}={k.Value}"));
            var f = PlayerFeedback;
            return string.Join(",", MatchId, Mode, MapId, DurationSeconds.ToString("0.0", CultureInfo.InvariantCulture), Activations, Winner, WinningFaction, VictoryReason,
                Factions[0], Factions[1], Doctrines[0], Doctrines[1], Q(TechCaps[0]), Q(TechCaps[1]), DamageDealt[0], DamageDealt[1], UnitsLost[0], UnitsLost[1],
                Guards[0], Guards[1], IllegalAttempts[0], IllegalAttempts[1], CommanderThreatTurns[0], CommanderThreatTurns[1], Q(ab), RematchChoice,
                f?.Enjoyment ?? 0, f?.Clarity ?? 0, f?.Fairness ?? 0, f != null && f.WouldPlayAgain ? 1 : 0, Q(f?.Strongest), Q(f?.Frustrating));
        }
    }

    /// <summary>Minimal dependency-free JSON writer (Core cannot reference Unity or Newtonsoft).</summary>
    public sealed class JsonWriter
    {
        readonly StringBuilder _sb = new StringBuilder();
        bool _needComma;
        int _depth;

        void Comma() { if (_needComma) _sb.Append(','); _sb.Append('\n').Append(' ', _depth * 2); _needComma = true; }
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");
        public void Begin() { _sb.Append('{'); _depth++; _needComma = false; }
        public void BeginObj(string name) { Comma(); _sb.Append($"\"{Esc(name)}\": {{"); _depth++; _needComma = false; }
        public void End() { _depth--; _sb.Append('\n').Append(' ', _depth * 2).Append('}'); _needComma = true; }
        public void Prop(string name, string v) { Comma(); _sb.Append($"\"{Esc(name)}\": \"{Esc(v)}\""); }
        public void Prop(string name, int v) { Comma(); _sb.Append($"\"{Esc(name)}\": {v}"); }
        public void Prop(string name, bool v) { Comma(); _sb.Append($"\"{Esc(name)}\": {(v ? "true" : "false")}"); }
        public void Prop(string name, double v) { Comma(); _sb.Append($"\"{Esc(name)}\": {v.ToString("0.###", CultureInfo.InvariantCulture)}"); }
        public void Array(string name, IEnumerable<string> items) { Comma(); _sb.Append($"\"{Esc(name)}\": [{string.Join(", ", items.Select(i => $"\"{Esc(i)}\""))}]"); }
        public void Dict(string name, Dictionary<string, int> d)
        {
            Comma(); _sb.Append($"\"{Esc(name)}\": {{{string.Join(", ", d.OrderBy(k => k.Key).Select(k => $"\"{Esc(k.Key)}\": {k.Value}"))}}}");
        }
        public override string ToString() => _sb.ToString();
    }
}
