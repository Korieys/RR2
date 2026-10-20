using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using RR.Strategy;

namespace RR.Tools
{
    /// <summary>
    /// Out-of-editor runner for the slice. `run [filter]`: every NUnit test in the vendored Commandfall EditMode tests and
    /// the slice tests (the same files Unity's test runner compiles). `stress [systems] [days]`: SYNTHETIC strategic load
    /// fixture (not a forecast of a real campaign).
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "run";
            if (mode == "stress") return Stress(args.Length > 1 ? int.Parse(args[1]) : 1000, args.Length > 2 ? int.Parse(args[2]) : 365);
            if (mode == "manifest") return Manifest(args.Length > 1 ? args[1] : ".", args.Length > 2 ? args[2] : "../..");
            if (mode == "perf") return Perf.Run(args.Length > 1 ? int.Parse(args[1]) : 240);
            if (mode == "probe") return Probe(args[1]);
            return RunTests(args.Length > 1 ? args[1] : null);
        }

        /// <summary>Loads a campaign save and reports the active operation and why its next battle can or cannot start.</summary>
        static int Probe(string path)
        {
            var g = SaveGame.FromJson(System.IO.File.ReadAllText(path), out var err);
            if (g == null) { Console.WriteLine("load failed: " + err); return 1; }
            var op = Operations.Active(g);
            Console.WriteLine($"day {g.Day}; active operation: {op?.Id ?? "none"}; ActiveBattle {g.ActiveBattle?.Setup?.BattleId ?? "none"}");
            if (op == null) return 0;
            Console.WriteLine($"phase {op.Phase}; series {op.AttackerWins}-{op.DefenderWins}; pending {op.PendingBattleId}; applied {g.AppliedBattles.Contains(op.PendingBattleId ?? "")}; next: {Operations.Next(g, op)}");
            var setup = Operations.PrepareNext(g, op, out var e, out var conceded);
            Console.WriteLine($"prepare: {(setup != null ? setup.BattleId : "null")}; error {e}; conceded {conceded}");
            return 0;
        }

        /// <summary>Writes the read-only ship manifest (JSON and Markdown) from the live registry (campaign alpha Prompt 19).</summary>
        static int Manifest(string outDir, string projectRoot)
        {
            var rows = ShipManifest.Build(path => System.IO.File.Exists(System.IO.Path.Combine(projectRoot, path)));
            System.IO.Directory.CreateDirectory(outDir);
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "ship_manifest.json"), ShipManifest.ToJson(rows));
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "ship_manifest.md"), ShipManifest.ToMarkdown(rows));
            Console.WriteLine($"manifest: {rows.Count} ships, {ShipManifest.RulesStamp}");
            return 0;
        }

        static int RunTests(string filter)
        {
            var asm = Assembly.GetExecutingAssembly();
            int pass = 0, fail = 0, ignored = 0;
            var failures = new List<string>();
            var perSuite = new SortedDictionary<string, int>();
            var sw = Stopwatch.StartNew();
            foreach (var type in asm.GetTypes().Where(t => t.GetMethods().Any(m => m.GetCustomAttributes<TestAttribute>().Any() || m.GetCustomAttributes<TestCaseAttribute>().Any())).OrderBy(t => t.FullName))
            {
                var setUp = type.GetMethods().FirstOrDefault(m => m.GetCustomAttribute<SetUpAttribute>() != null);
                foreach (var m in type.GetMethods().OrderBy(m => m.Name))
                {
                    var cases = new List<object[]>();
                    if (m.GetCustomAttribute<TestAttribute>() != null) cases.Add(Array.Empty<object>());
                    foreach (var tc in m.GetCustomAttributes<TestCaseAttribute>()) cases.Add(tc.Arguments);
                    foreach (var argsCase in cases)
                    {
                        string name = $"{type.Name}.{m.Name}" + (argsCase.Length > 0 ? $"({string.Join(",", argsCase)})" : "");
                        if (filter != null && !name.Contains(filter)) continue;
                        string suite = type.Namespace?.StartsWith("RR") == true ? "slice" : "commandfall";
                        try
                        {
                            var inst = Activator.CreateInstance(type);
                            setUp?.Invoke(inst, null);
                            var ps = m.GetParameters();
                            var conv = argsCase.Select((a, i) => a == null ? null : Convert.ChangeType(a, ps[i].ParameterType)).ToArray();
                            m.Invoke(inst, conv);
                            pass++;
                            perSuite[suite] = perSuite.TryGetValue(suite, out var n) ? n + 1 : 1;
                        }
                        catch (TargetInvocationException tie) when (tie.InnerException is SuccessException) { pass++; }
                        catch (TargetInvocationException tie) when (tie.InnerException is IgnoreException) { ignored++; }
                        catch (TargetInvocationException tie)
                        {
                            fail++;
                            var ex = tie.InnerException;
                            failures.Add($"FAIL {name}\n   {ex.GetType().Name}: {ex.Message.Trim()}\n   {FirstFrame(ex)}");
                        }
                        catch (Exception ex) { fail++; failures.Add($"FAIL {name}\n   {ex.GetType().Name}: {ex.Message.Trim()}"); }
                    }
                }
            }
            foreach (var f in failures) Console.WriteLine(f);
            Console.WriteLine($"\nTests: {pass} passed, {fail} failed, {ignored} ignored in {sw.Elapsed.TotalSeconds:0.00}s  ({string.Join(", ", perSuite.Select(k => $"{k.Key} {k.Value}"))})");
            return fail == 0 ? 0 : 1;
        }

        static string FirstFrame(Exception ex)
        {
            var st = ex.StackTrace ?? "";
            return (st.Split('\n').FirstOrDefault(l => (l.Contains("RR.") || l.Contains("Commandfall")) && !l.Contains("NUnit")) ?? "").Trim();
        }

        /// <summary>SYNTHETIC: a ring-and-chord galaxy of N systems, 1 fleet per 5 systems wandering between random targets.</summary>
        static int Stress(int n, int days)
        {
            var g = new GalaxyState { Seed = 1 };
            g.Polities.Add(new Polity { Id = "a", Name = "A", PlayerControlled = true, Playable = true, CommandfallFaction = "Silex" });
            g.Polities.Add(new Polity { Id = "b", Name = "B", Playable = true, CommandfallFaction = "Craigon" });
            var rnd = new Random(42);
            for (int i = 0; i < n; i++)
            {
                double a = i * 2 * Math.PI / n, r = 2000 + rnd.NextDouble() * 6000;
                g.Systems.Add(new StarSystem { Id = "s" + i, Name = "S" + i, X = Math.Cos(a) * r, Y = Math.Sin(a) * r, OwnerId = "a", OrbitalControlId = "a",
                    Planets = { new Planet { Id = "p" + i, OwnerId = "a", DailyYield = new Resources(1, 1, 1), Kind = PlanetKind.Barren } } });
            }
            for (int i = 0; i < n; i++)
            {
                g.Lanes.Add(new Lane { A = "s" + i, B = "s" + ((i + 1) % n), Days = 3 + rnd.Next(6) });
                if (i % 7 == 0) g.Lanes.Add(new Lane { A = "s" + i, B = "s" + rnd.Next(n), Days = 5 + rnd.Next(8) });
            }
            for (int i = 0; i < n / 5; i++)
                g.Fleets.Add(new Fleet { Id = "f" + i, Name = "F" + i, OwnerId = "a", SystemId = "s" + rnd.Next(n), Ships = { new ShipRecord { Uid = i + 1, DefId = "silex_prism_escort", Hp = 7 } } });
            var sw = Stopwatch.StartNew();
            long orderTicks = 0;
            var dayMs = new List<double>();
            for (int d = 0; d < days; d++)
            {
                var t0 = Stopwatch.GetTimestamp();
                foreach (var f in g.Fleets.Where(f => !f.Moving).Take(20).ToList())
                {
                    var o0 = Stopwatch.GetTimestamp();
                    StrategyRules.OrderMove(g, f.Id, "s" + rnd.Next(n));
                    orderTicks += Stopwatch.GetTimestamp() - o0;
                }
                StrategyRules.AdvanceDay(g);
                dayMs.Add((Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency);
                g.Log.Clear();
            }
            dayMs.Sort();
            Console.WriteLine($"SYNTHETIC stress fixture: {n} systems, {g.Lanes.Count} lanes, {g.Fleets.Count} fleets, {days} days in {sw.Elapsed.TotalMilliseconds:0} ms");
            Console.WriteLine($"  per simulated day (incl. up to 20 route plans): median {dayMs[dayMs.Count / 2]:0.00} ms, p95 {dayMs[(int)(dayMs.Count * 0.95)]:0.00} ms, max {dayMs[dayMs.Count - 1]:0.00} ms");
            Console.WriteLine($"  route planning total {orderTicks * 1000.0 / Stopwatch.Frequency:0} ms");
            var t = Stopwatch.StartNew();
            var json = SaveGame.ToJson(g);
            var back = SaveGame.FromJson(json, out var err);
            Console.WriteLine($"  save {json.Length / 1024} KB, save+load {t.ElapsedMilliseconds} ms, roundtrip {(back != null ? "ok" : err)}");
            return 0;
        }
    }
}