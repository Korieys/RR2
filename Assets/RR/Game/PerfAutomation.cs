using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Whole-session performance for the campaign alpha (Prompt 20; -rrPerfTour <absolute dir> -rrUncapped, with -rrSavePath).
    // Frame times in quiet and crowded scenes on this machine: the galaxy idle and with every campaign layer on, the open
    // windows, a long event history after a whole automated campaign (both sides planned, battles resolved headless by the
    // slice AI), travel into The Eclipse Sector, the transition into a battle, an orbital battle while the AI thinks, and a
    // planetary match. The rival planner's day cost is timed inside the player too. Not a favourable opening frame only.
    public static class PerfAutomation
    {
        static void Note(string s) => Automation.Note(s);
        static IEnumerator Wait(float s) => SliceAutomation.Wait(s);
        static IEnumerator Until(Func<bool> c, float t) => SliceAutomation.Until(c, t);

        public static IEnumerator Tour(GameRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            Note("performance tour; rules " + RulesVersion.Id + "; uncapped " + Args.Has("-rrUncapped"));
            Automation.Environment();
            yield return Wait(2f);
            root.StartPassageWar(FactionId.Pygmaei);
            yield return Until(() => !root.Busy && PassageWar.Of(root.G) != null && root.UI.ModalOpen, 60);
            root.UI.CloseModal();
            yield return Wait(1f);
            yield return Automation.Measure("galaxy, quiet (campaign start, panels compact)", 400);
            root.ToggleSupply();
            yield return Wait(0.5f);
            yield return Automation.Measure("galaxy, crowded (supply overlay, labels, campaign panel)", 400);
            root.OpenTreasury(); yield return Wait(0.5f);
            yield return Automation.Measure("treasury window open over the galaxy", 300);
            root.UI.CloseModal(); root.OpenMandate(); yield return Wait(0.5f);
            yield return Automation.Measure("mandate window open", 300);
            root.UI.CloseModal(); root.OpenDoctrine(); yield return Wait(0.5f);
            yield return Automation.Measure("doctrine window open", 300);
            root.UI.CloseModal();

            // A long history: a whole automated campaign in the strategic layer (both sides planned, battles headless).
            var g = root.G;
            PassageWar.Of(g).AutoPlayer = true;
            // A measurement fixture: no ending may stop the long campaign early (it ended on day 60 in the first run).
            PassageWar.Of(g).MandateDay = 900; PassageWar.Of(g).HoldRequired = 999; PassageWar.Of(g).TreatyHoldRequired = 999;
            var sw = new Stopwatch(); var days = new System.Collections.Generic.List<double>(); int battles = 0;
            for (int i = 0; i < 1400 && g.Day < 200 && !PassageWar.Of(g).Ended; i++)
            {
                if (g.PendingEngagement != null)
                {
                    var e = g.PendingEngagement;
                    var setup = BattleBridge.Prepare(g, e, BattleBridge.AutoSelect(g, SliceCampaign.Seats(g, e)[0]), out _);
                    var bs = BattleSession.Begin(setup, out _);
                    var ai = new SliceAI();
                    for (int k = 0; k < 900 && !bs.State.IsOver; k++) bs.Submit(ai.Choose(bs.State), BattleSession.Host);
                    BattleBridge.Apply(g, setup, BattleBridge.Extract(setup, bs));
                    battles++;
                    if (i % 4 == 0) yield return null;
                    continue;
                }
                sw.Restart(); StrategyRules.AdvanceDay(g); sw.Stop(); days.Add(sw.Elapsed.TotalMilliseconds);
                if (i % 10 == 0) yield return null;
            }
            PassageWar.Of(g).AutoPlayer = false;
            days.Sort();
            Note($"strategic day in the player (economy, supply, rival planner, doctrines, settlement): median {days[days.Count / 2]:0.00} ms, p95 {days[days.Count * 95 / 100]:0.00} ms, max {days[days.Count - 1]:0.00} ms over {days.Count} days; {battles} headless battles");
            Note($"long history: day {g.Day}, {g.Slice.History.Count} history events, {PassageWar.Of(g).Ledger.Count} ledger entries, {g.Log.Count} dispatch lines, {PassageWar.Of(g).AiLog.Count} AI log lines");
            sw.Restart(); var json = SaveGame.ToJson(g); sw.Stop();
            Note($"save of the long campaign: {json.Length / 1024} KB in {sw.Elapsed.TotalMilliseconds:0} ms");
            root.UI.Strategy.RefreshAll();
            yield return Wait(1f);
            yield return Automation.Measure("galaxy after 200 days (long history, crowded overlay)", 400);
            root.OpenHistory(null); yield return Wait(0.5f);
            yield return Automation.Measure("history window over a long campaign", 300);
            root.UI.CloseModal();

            // Eclipse travel at the fastest speed.
            var mine = g.Fleets.FirstOrDefault(f => f.OwnerId == g.Player.Id && !f.Moving && f.EngagementId == null);
            if (mine != null && g.PendingEngagement == null && !PassageWar.Of(g).Ended && Operations.Active(g) == null)
            {
                var r = StrategyRules.OrderMove(g, mine.Id, mine.SystemId == "gloam" ? "cinderhold" : "gloam");
                Note("Eclipse travel order: " + r);
                root.SetSpeed(3); root.Clock.Paused = false;
                yield return Automation.Measure("galaxy, fleet travelling at speed 3 toward the Eclipse", 400);
                root.Clock.Paused = true;
            }

            // A battle: the transition, an idle player turn, and the AI's turns (worker-thread decisions).
            root.StartPassageWar(FactionId.Pygmaei);
            yield return Until(() => !root.Busy && root.UI.ModalOpen, 60);
            root.UI.CloseModal();
            g = root.G;
            root.CampaignDeclare();
            var fl = g.Fleets.First(f => f.OwnerId == g.Player.Id);
            StrategyRules.OrderMove(g, fl.Id, "umbra");
            root.SetSpeed(3); root.Clock.Paused = false;
            yield return Until(() => g.PendingEngagement != null, 60);
            root.Clock.Paused = true;
            yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60);
            root.UI.CloseModal();
            yield return Until(() => !root.Busy, 30);
            root.StartBattle(BattleBridge.AutoSelect(g, fl));
            yield return Automation.Measure("transition into the orbital battle", 240);
            yield return Until(() => root.Battle != null && !root.Busy && !root.Battle.Animating, 60);
            if (root.Battle != null)
            {
                yield return Automation.Measure("orbital battle, idle on the player's turn", 300);
                var ai = new SliceAI();
                var b = root.Battle;
                for (int i = 0; i < 6 && b != null && !b.S.IsOver; i++)
                {
                    yield return Until(() => b.PlayersTurn || b.S.IsOver, 30);
                    if (b.S.IsOver) break;
                    var c = ai.Choose(b.S);
                    if (c.UnitId != 0 && b.S.ActiveUnitId == 0) b.Select(c.UnitId);
                    b.Submit(c);
                }
                yield return Automation.Measure("orbital battle, AI turns and animations", 400);
            }
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            Application.Quit();
        }
    }
}
