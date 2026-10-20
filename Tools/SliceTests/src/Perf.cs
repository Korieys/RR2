using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;

namespace RR.Tools
{
    /// <summary>
    /// `perf [days]`: whole-session cost of the strategic layer on this machine (campaign alpha Prompt 20). One automated Passage
    /// War with both sides planned and every battle flown by the slice AI: the cost of each strategic day (economy, supply,
    /// the rival planner, doctrines, settlements), of each AI battle decision, of the supply network, and of saving and
    /// loading the long history at the end. Engine-free code only; frame times are measured in the player by the tours.
    /// </summary>
    public static class Perf
    {
        static string Stats(List<double> ms) =>
            ms.Count == 0 ? "none" : $"n {ms.Count}, median {Pct(ms, 50):0.###} ms, p95 {Pct(ms, 95):0.###} ms, max {ms.Max():0.###} ms";

        static double Pct(List<double> xs, int p) { var s = xs.OrderBy(x => x).ToList(); return s[Math.Min(s.Count - 1, s.Count * p / 100)]; }

        public static int Run(int days)
        {
            var g = PassageWar.Create(FactionId.Pygmaei);
            PassageWar.Of(g).AutoPlayer = true;
            PassageWar.Of(g).MandateDay = days;
            var day = new List<double>(); var ai = new List<double>(); var supply = new List<double>();
            int battles = 0, orders = 0;
            var sw = new Stopwatch();
            for (int i = 0; i < days * 3 && !PassageWar.Of(g).Ended; i++)
            {
                if (g.PendingEngagement != null)
                {
                    var e = g.PendingEngagement;
                    var setup = BattleBridge.Prepare(g, e, BattleBridge.AutoSelect(g, SliceCampaign.Seats(g, e)[0]), out _);
                    var bs = BattleSession.Begin(setup, out _);
                    var pilot = new SliceAI();
                    for (int k = 0; k < 900 && !bs.State.IsOver; k++)
                    {
                        sw.Restart(); var c = pilot.Choose(bs.State); sw.Stop(); ai.Add(sw.Elapsed.TotalMilliseconds);
                        bs.Submit(c, BattleSession.Host); orders++;
                    }
                    BattleBridge.Apply(g, setup, BattleBridge.Extract(setup, bs));
                    battles++;
                    continue;
                }
                sw.Restart(); StrategyRules.AdvanceDay(g); sw.Stop(); day.Add(sw.Elapsed.TotalMilliseconds);
                sw.Restart(); Supply.Of(g, "pygmaei"); Supply.Of(g, "inhumanus"); sw.Stop(); supply.Add(sw.Elapsed.TotalMilliseconds / 2);
            }
            sw.Restart(); var json = SaveGame.ToJson(g); sw.Stop(); double save = sw.Elapsed.TotalMilliseconds;
            sw.Restart(); var back = SaveGame.FromJson(json, out var err); sw.Stop(); double load = sw.Elapsed.TotalMilliseconds;
            var sc = PassageWar.Of(g);
            // The battle view runs the Command Threat analysis on the main thread when a battle opens (suspected cause of the
            // 3.6 s frame on entering a battle in the player): time it at a campaign battle's opening position.
            var t = PassageWar.Create(FactionId.Pygmaei);
            SliceCampaign.DeclareObjective(t, "pygmaei", "umbra");
            StrategyRules.OrderMove(t, t.Fleets.First(f => f.OwnerId == "pygmaei").Id, "umbra");
            for (int i = 0; i < 80 && t.PendingEngagement == null; i++) StrategyRules.AdvanceDay(t);
            string threat = "no encounter";
            if (t.PendingEngagement != null)
            {
                var e = t.PendingEngagement;
                var su = BattleBridge.Prepare(t, e, BattleBridge.AutoSelect(t, SliceCampaign.Seats(t, e)[0]), out _);
                var b0 = BattleSession.Begin(su, out _);
                sw.Restart(); var a1 = CommandThreat.Assess(b0.State, 0); var x1 = CommandThreat.RoundExposure(b0.State, 0); var a2 = CommandThreat.Assess(b0.State, 1); sw.Stop();
                threat = $"{sw.Elapsed.TotalMilliseconds:0} ms for both assessments and the exposure ({a1.CommandsChecked + a2.CommandsChecked} orders checked)";
            }
            Console.WriteLine("command threat at a battle's opening position: " + threat);
            Console.WriteLine($"perf: {Environment.ProcessorCount} logical processors, {Environment.OSVersion}, .NET {Environment.Version} (outside Unity; Mono in the player is slower)");
            Console.WriteLine($"campaign: {g.Day} days, {battles} battles, {orders} AI orders, outcome {sc.Outcome}");
            Console.WriteLine($"strategic day (economy, supply, rival planner, doctrines, settlement): {Stats(day)}");
            Console.WriteLine($"supply network per power: {Stats(supply)}");
            Console.WriteLine($"slice AI battle decision: {Stats(ai)}");
            Console.WriteLine($"history {g.Slice.History.Count} events, ledger {sc.Ledger.Count} entries, AI log {sc.AiLog.Count} lines, save {json.Length / 1024} KB: save {save:0.#} ms, load {load:0.#} ms ({(back != null ? "ok" : err)})");
            return 0;
        }
    }
}
