using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace RR.Strategy
{
    /// <summary>Minimal JSON value model, reader and writer (Strategy cannot reference Unity or external packages).</summary>
    public static class Json
    {
        public static object Parse(string text) { int i = 0; var v = Value(text, ref i); Ws(text, ref i); if (i != text.Length) throw new FormatException("Trailing data at " + i); return v; }

        static void Ws(string t, ref int i) { while (i < t.Length && char.IsWhiteSpace(t[i])) i++; }

        static object Value(string t, ref int i)
        {
            Ws(t, ref i);
            if (i >= t.Length) throw new FormatException("Unexpected end of JSON.");
            char c = t[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(StringComparer.Ordinal); i++; Ws(t, ref i);
                if (t[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(t, ref i); var k = Str(t, ref i); Ws(t, ref i);
                    if (t[i] != ':') throw new FormatException("Expected ':' at " + i); i++;
                    d[k] = Value(t, ref i); Ws(t, ref i);
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == '}') { i++; return d; }
                    throw new FormatException("Expected ',' or '}' at " + i);
                }
            }
            if (c == '[')
            {
                var l = new List<object>(); i++; Ws(t, ref i);
                if (t[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(t, ref i)); Ws(t, ref i);
                    if (t[i] == ',') { i++; continue; }
                    if (t[i] == ']') { i++; return l; }
                    throw new FormatException("Expected ',' or ']' at " + i);
                }
            }
            if (c == '"') return Str(t, ref i);
            if (t.Length - i >= 4 && string.CompareOrdinal(t, i, "true", 0, 4) == 0) { i += 4; return true; }
            if (t.Length - i >= 5 && string.CompareOrdinal(t, i, "false", 0, 5) == 0) { i += 5; return false; }
            if (t.Length - i >= 4 && string.CompareOrdinal(t, i, "null", 0, 4) == 0) { i += 4; return null; }
            int st = i;
            while (i < t.Length && "+-0123456789.eE".IndexOf(t[i]) >= 0) i++;
            if (st == i) throw new FormatException("Unexpected character '" + c + "' at " + i);
            return double.Parse(t.Substring(st, i - st), CultureInfo.InvariantCulture);
        }

        static string Str(string t, ref int i)
        {
            if (t[i] != '"') throw new FormatException("Expected string at " + i);
            i++;
            var sb = new StringBuilder();
            while (t[i] != '"')
            {
                if (t[i] == '\\')
                {
                    i++;
                    switch (t[i])
                    {
                        case 'n': sb.Append('\n'); break;
                        case 't': sb.Append('\t'); break;
                        case 'r': sb.Append('\r'); break;
                        case 'u': sb.Append((char)int.Parse(t.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; break;
                        default: sb.Append(t[i]); break;
                    }
                    i++;
                }
                else sb.Append(t[i++]);
            }
            i++;
            return sb.ToString();
        }

        public static string Write(object v) { var sb = new StringBuilder(); Write(sb, v, 0); return sb.ToString(); }

        static void Write(StringBuilder sb, object v, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case string s: Quote(sb, s); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case long n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case uint n: sb.Append(n.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> d:
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in d)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        sb.Append('\n').Append(' ', (depth + 1) * 2);
                        Quote(sb, kv.Key); sb.Append(": ");
                        Write(sb, kv.Value, depth + 1);
                    }
                    if (!first) sb.Append('\n').Append(' ', depth * 2);
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    bool f2 = true;
                    foreach (var item in list) { if (!f2) sb.Append(", "); f2 = false; Write(sb, item, depth + 1); }
                    sb.Append(']');
                    break;
                default: throw new ArgumentException("Cannot serialise " + v.GetType());
            }
        }

        static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c == '\n') sb.Append("\\n");
                else if (c < 32 || c > 126) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            sb.Append('"');
        }

        // ---- typed accessors
        public static string S(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v as string : null;
        public static int I(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double x ? (int)x : 0;
        public static long L(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double x ? (long)x : 0;
        public static double D(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is double x ? x : 0;
        public static bool B(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is bool x && x;
        public static List<object> A(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) && v is List<object> l ? l : new List<object>();
        public static IDictionary<string, object> O(this IDictionary<string, object> d, string k) => d.TryGetValue(k, out var v) ? v as IDictionary<string, object> : null;
    }

    /// <summary>Versioned save format for the slice ("rr.slice.galaxy", version 1).</summary>
    public static class SaveGame
    {
        public static string ToJson(GalaxyState g)
        {
            var root = new Dictionary<string, object>
            {
                ["schema"] = GalaxyState.Schema, ["version"] = GalaxyState.CurrentVersion, ["scenario"] = g.ScenarioId, ["seed"] = g.Seed,
                ["day"] = g.Day, ["rng"] = (long)g.Rng, ["nextUid"] = g.NextUid, ["nextId"] = g.NextId,
                ["polities"] = g.Polities.Select(p => (object)new Dictionary<string, object>
                {
                    ["id"] = p.Id, ["name"] = p.Name, ["color"] = p.Color, ["player"] = p.PlayerControlled, ["playable"] = p.Playable,
                    ["cfFaction"] = p.CommandfallFaction, ["stock"] = Res(p.Stock),
                }).ToList(),
                ["sectors"] = g.Sectors.Select(s => (object)new Dictionary<string, object>
                {
                    ["id"] = s.Id, ["name"] = s.Name, ["subtitle"] = s.Subtitle, ["owner"] = s.OwnerId, ["x"] = s.X, ["y"] = s.Y, ["r"] = s.Radius,
                    ["aspect"] = s.Aspect, ["eclipse"] = s.Eclipse, ["provisional"] = s.ProvisionalName,
                }).ToList(),
                ["features"] = g.Features.Select(f => (object)new Dictionary<string, object>
                {
                    ["id"] = f.Id, ["kind"] = f.Kind.ToString(), ["polity"] = f.PolityId, ["r"] = f.Radius, ["points"] = f.Points.Cast<object>().ToList(),
                }).ToList(),
                ["systems"] = g.Systems.Select(s => (object)new Dictionary<string, object>
                {
                    ["id"] = s.Id, ["name"] = s.Name, ["sector"] = s.SectorId, ["x"] = s.X, ["y"] = s.Y, ["z"] = s.Z, ["star"] = s.Star.ToString(),
                    ["detail"] = s.Detail.ToString(), ["owner"] = s.OwnerId, ["orbit"] = s.OrbitalControlId, ["conquestBy"] = s.ConquestBy,
                    ["conquest"] = s.Conquest.ToString(), ["station"] = s.HasStation, ["map"] = s.BattlefieldMapId, ["provisional"] = s.ProvisionalName,
                    ["seed"] = s.Seed,
                    ["planets"] = s.Planets.Select(p => (object)new Dictionary<string, object>
                    {
                        ["id"] = p.Id, ["name"] = p.Name, ["kind"] = p.Kind.ToString(), ["orbit"] = p.OrbitRadius, ["size"] = p.Size, ["phase"] = p.OrbitPhase,
                        ["rings"] = p.Rings, ["moons"] = p.Moons, ["owner"] = p.OwnerId, ["pop"] = p.Population, ["dev"] = p.Development,
                        ["yield"] = Res(p.DailyYield), ["foundries"] = p.Foundries, ["shipyard"] = p.HasShipyard, ["provisional"] = p.ProvisionalName,
                        ["seed"] = p.Seed,
                        ["project"] = p.Project == null ? null : new Dictionary<string, object>
                        {
                            ["kind"] = p.Project.Kind.ToString(), ["ship"] = p.Project.ShipDefId, ["cost"] = Res(p.Project.Cost),
                            ["days"] = p.Project.DaysTotal, ["done"] = p.Project.DaysDone,
                        },
                    }).ToList(),
                }).ToList(),
                ["lanes"] = g.Lanes.Select(l => (object)new Dictionary<string, object> { ["a"] = l.A, ["b"] = l.B, ["major"] = l.Major, ["days"] = l.Days }).ToList(),
                ["fleets"] = g.Fleets.Select(f => (object)new Dictionary<string, object>
                {
                    ["id"] = f.Id, ["name"] = f.Name, ["owner"] = f.OwnerId, ["system"] = f.SystemId, ["home"] = f.HomeSystemId, ["last"] = f.LastSystemId,
                    ["engagement"] = f.EngagementId, ["ai"] = f.AIControlled, ["aiTarget"] = f.AITarget, ["aiDepart"] = f.AIDepartDay,
                    ["ships"] = f.Ships.Select(s => (object)new Dictionary<string, object> { ["uid"] = s.Uid, ["def"] = s.DefId, ["hp"] = s.Hp }).ToList(),
                    ["order"] = f.Order == null ? null : new Dictionary<string, object>
                    {
                        ["path"] = f.Order.Path.Cast<object>().ToList(), ["into"] = f.Order.DaysIntoLane, ["from"] = f.Order.FromSystemId,
                    },
                }).ToList(),
                ["engagements"] = g.Engagements.Select(e => (object)new Dictionary<string, object>
                {
                    ["id"] = e.Id, ["system"] = e.SystemId, ["attacker"] = e.AttackerFleetId, ["defender"] = e.DefenderFleetId, ["day"] = e.Day,
                    ["state"] = e.State.ToString(), ["battle"] = e.BattleId, ["summary"] = e.ResultSummary,
                }).ToList(),
                ["appliedBattles"] = g.AppliedBattles.OrderBy(x => x, StringComparer.Ordinal).Cast<object>().ToList(),
                ["battle"] = g.ActiveBattle == null ? null : BattleSession.ToData(g.ActiveBattle),
                ["log"] = g.Log.Skip(Math.Max(0, g.Log.Count - 200)).Select(l => (object)new Dictionary<string, object> { ["day"] = l.Day, ["kind"] = l.Kind, ["text"] = l.Text }).ToList(),
            };
            return Json.Write(root);
        }

        static Dictionary<string, object> Res(Resources r) => new Dictionary<string, object>
            { ["energy"] = r.Energy, ["materials"] = r.Materials, ["credits"] = r.Credits, ["influence"] = r.Influence };

        static Resources Res(IDictionary<string, object> d) => d == null ? default : new Resources(d.I("energy"), d.I("materials"), d.I("credits"), d.I("influence"));

        static T En<T>(string s) where T : struct => Enum.TryParse<T>(s, out var v) ? v : default;

        public static GalaxyState FromJson(string json, out string error)
        {
            error = null;
            IDictionary<string, object> r;
            try { r = Json.Parse(json) as IDictionary<string, object>; }
            catch (Exception ex) { error = "The save file is damaged: " + ex.Message; return null; }
            if (r == null || r.S("schema") != GalaxyState.Schema) { error = "This file is not a Rule and Ruin slice save."; return null; }
            int version = r.I("version");
            if (version > GalaxyState.CurrentVersion) { error = $"This save was written by a newer build (version {version})."; return null; }
            if (version < 1) { error = "This save version is not supported."; return null; }
            // Version 1 is the first format; future migrations go here, one step per version.
            var g = new GalaxyState
            {
                Version = GalaxyState.CurrentVersion, ScenarioId = r.S("scenario"), Seed = r.I("seed"), Day = r.I("day"), Rng = (uint)r.L("rng"),
                NextUid = r.I("nextUid"), NextId = r.I("nextId"),
            };
            foreach (IDictionary<string, object> p in r.A("polities"))
                g.Polities.Add(new Polity { Id = p.S("id"), Name = p.S("name"), Color = p.I("color"), PlayerControlled = p.B("player"), Playable = p.B("playable"),
                    CommandfallFaction = p.S("cfFaction"), Stock = Res(p.O("stock")) });
            foreach (IDictionary<string, object> s in r.A("sectors"))
                g.Sectors.Add(new Sector { Id = s.S("id"), Name = s.S("name"), Subtitle = s.S("subtitle"), OwnerId = s.S("owner"), X = s.D("x"), Y = s.D("y"),
                    Radius = s.D("r"), Aspect = s.D("aspect") > 0 ? s.D("aspect") : 1, Eclipse = s.B("eclipse"), ProvisionalName = s.B("provisional") });
            // Map features arrived without a version bump: they are optional presentation data, and older saves simply have none.
            foreach (IDictionary<string, object> f in r.A("features"))
                g.Features.Add(new MapFeature { Id = f.S("id"), Kind = En<MapFeatureKind>(f.S("kind")), PolityId = f.S("polity"), Radius = f.D("r"),
                    Points = f.A("points").Select(v => v is double d ? d : 0).ToList() });
            foreach (IDictionary<string, object> s in r.A("systems"))
            {
                var sys = new StarSystem
                {
                    Id = s.S("id"), Name = s.S("name"), SectorId = s.S("sector"), X = s.D("x"), Y = s.D("y"), Z = s.D("z"), Star = En<StarClass>(s.S("star")),
                    Detail = En<SystemDetail>(s.S("detail")), OwnerId = s.S("owner"), OrbitalControlId = s.S("orbit"), ConquestBy = s.S("conquestBy"),
                    Conquest = En<ConquestStage>(s.S("conquest")), HasStation = s.B("station"), BattlefieldMapId = s.S("map"), ProvisionalName = s.B("provisional"),
                    Seed = s.I("seed"),
                };
                foreach (IDictionary<string, object> p in s.A("planets"))
                {
                    var pr = p.O("project");
                    sys.Planets.Add(new Planet
                    {
                        Id = p.S("id"), Name = p.S("name"), Kind = En<PlanetKind>(p.S("kind")), OrbitRadius = p.D("orbit"), Size = p.D("size"), OrbitPhase = p.D("phase"),
                        Rings = p.B("rings"), Moons = p.I("moons"), OwnerId = p.S("owner"), Population = p.L("pop"), Development = p.I("dev"),
                        DailyYield = Res(p.O("yield")), Foundries = p.I("foundries"), HasShipyard = p.B("shipyard"), ProvisionalName = p.B("provisional"), Seed = p.I("seed"),
                        Project = pr == null ? null : new ProjectState { Kind = En<ProjectKind>(pr.S("kind")), ShipDefId = pr.S("ship"), Cost = Res(pr.O("cost")),
                            DaysTotal = pr.I("days"), DaysDone = pr.I("done") },
                    });
                }
                g.Systems.Add(sys);
            }
            foreach (IDictionary<string, object> l in r.A("lanes"))
                g.Lanes.Add(new Lane { A = l.S("a"), B = l.S("b"), Major = l.B("major"), Days = l.I("days") });
            foreach (IDictionary<string, object> f in r.A("fleets"))
            {
                var o = f.O("order");
                var fl = new Fleet
                {
                    Id = f.S("id"), Name = f.S("name"), OwnerId = f.S("owner"), SystemId = f.S("system"), HomeSystemId = f.S("home"), LastSystemId = f.S("last"),
                    EngagementId = f.S("engagement"), AIControlled = f.B("ai"), AITarget = f.S("aiTarget"), AIDepartDay = f.I("aiDepart"),
                    Order = o == null ? null : new MoveOrder { Path = o.A("path").Cast<string>().ToList(), DaysIntoLane = o.I("into"), FromSystemId = o.S("from") },
                };
                foreach (IDictionary<string, object> s in f.A("ships")) fl.Ships.Add(new ShipRecord { Uid = s.I("uid"), DefId = s.S("def"), Hp = s.I("hp") });
                g.Fleets.Add(fl);
            }
            foreach (IDictionary<string, object> e in r.A("engagements"))
                g.Engagements.Add(new Engagement { Id = e.S("id"), SystemId = e.S("system"), AttackerFleetId = e.S("attacker"), DefenderFleetId = e.S("defender"),
                    Day = e.I("day"), State = En<EngagementState>(e.S("state")), BattleId = e.S("battle"), ResultSummary = e.S("summary") });
            foreach (var b in r.A("appliedBattles")) g.AppliedBattles.Add((string)b);
            foreach (IDictionary<string, object> l in r.A("log")) g.Log.Add(new LogEntry { Day = l.I("day"), Kind = l.S("kind"), Text = l.S("text") });
            // A battle in progress arrived without a version bump: it is optional, and older saves have none.
            var battle = r.O("battle");
            if (battle != null)
            {
                g.ActiveBattle = BattleSession.FromData(battle, out error);
                if (g.ActiveBattle == null) return null;
                if (g.ActiveBattle.Setup.Scenario == null)
                {
                    var e = g.GetEngagement(g.ActiveBattle.Setup.EngagementId);
                    if (e == null || e.State != EngagementState.Pending || g.AppliedBattles.Contains(g.ActiveBattle.Setup.BattleId))
                    { error = "The save holds a battle whose engagement is no longer pending."; return null; }
                }
                if (g.GetSystem(g.ActiveBattle.Setup.SystemId ?? g.GetEngagement(g.ActiveBattle.Setup.EngagementId)?.SystemId) == null)
                { error = "The save holds a battle in an unknown system."; return null; }
            }
            return g;
        }

        /// <summary>Stable digest of the strategic state (log excluded) for determinism and round-trip checks.</summary>
        public static ulong Digest(GalaxyState g)
        {
            var copy = FromJson(ToJson(g), out _);
            copy.Log.Clear();
            var text = ToJson(copy);
            unchecked
            {
                ulong h = 1469598103934665603UL;
                foreach (char c in text) { h ^= c; h *= 1099511628211UL; }
                return h;
            }
        }
    }
}