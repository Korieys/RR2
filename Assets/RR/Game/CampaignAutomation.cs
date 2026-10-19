using System;
using System.Collections;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Evidence tour for the passage campaign (Prompts 6 to 10; -rrCampaignTour <absolute dir>): a new campaign, the
    // hangar and a refit, the objective and the encounter preview, the terms form, the campaign battle with a save and
    // load in the middle, its consequences in history, a strategic save and load, and the fogged Eclipse expedition.
    // Player orders come from the slice AI (a pilot); this is functional evidence, not a human playtest.
    public static class CampaignAutomation
    {
        static void Note(string s) => Automation.Note(s);
        static IEnumerator Wait(float s) => SliceAutomation.Wait(s);
        static IEnumerator Until(Func<bool> c, float t) => SliceAutomation.Until(c, t);

        static IEnumerator Days(GameRoot root, Func<bool> until, float timeout)
        {
            root.SetSpeed(3);
            yield return Until(() => until() || root.G.PendingEngagement != null, timeout);
            root.Clock.Paused = true;
        }

        public static IEnumerator Tour(GameRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            Note("campaign tour; rules " + RulesVersion.Id + "; save " + GameRoot.SavePath);
            Automation.Environment();
            yield return Wait(2f);
            yield return Automation.Measure("galaxy, idle", 300);

            // 1. A new passage campaign, as the Pygmaei.
            root.StartPassageCampaign(FactionId.Pygmaei);
            yield return Until(() => !root.Busy && root.G.Slice != null, 60);
            yield return Wait(1.5f);
            var g = root.G;
            var mine = g.Fleets.First(f => f.OwnerId == g.Player.Id);
            Note($"campaign: player {g.Player.Id}, fleet {mine.Name} at {mine.SystemId} with {mine.Ships.Count} ships; passage {g.Slice.Passages[0].Name}; expedition {Expeditions.Of(g)?.MapId}");
            yield return Automation.Shot(dir, "c01_campaign_start");
            yield return Automation.Measure("galaxy with the campaign panel", 300);

            // 2. The hangar: the flagship, then a refit (afterburners) and its preview on the hull.
            int flag = mine.Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Uid;
            root.OpenHangar(flag);
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "c02_hangar");
            var opts = SliceCampaign.RefitOptions(g, flag);
            Note("refit options: " + string.Join("; ", opts.Select(o => $"{o.Module.Id} cost {o.Cost}{(o.Blocked != null ? " blocked: " + o.Blocked : "")}")));
            var fit = SliceCampaign.ApplyRefit(g, flag, "afterburners");
            PlaytestMetrics.Refit(g.Slice.Profile(flag).Name, "afterburners", fit.Ok, fit.Reason);
            Note("refit afterburners: " + (fit.Ok ? "fitted" : fit.Reason) + "; modules now " + string.Join(",", g.Slice.Profile(flag).Modules));
            root.UI.CloseModal();
            root.OpenHangar(flag);
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "c03_hangar_refitted");
            root.UI.CloseModal();

            // 3. Declare the objective and sail for the passage; days run until the encounter.
            root.CampaignDeclare();
            var mv = StrategyRules.OrderMove(g, mine.Id, SliceCampaign.PassageSystem);
            Note("declared; order to Umbra: " + (mv.Ok ? "accepted" : mv.Reason));
            int day0 = g.Day;
            yield return Days(root, () => false, 180);
            Note($"after {g.Day - day0} days: pending engagement {(g.PendingEngagement != null ? g.PendingEngagement.Id + " at " + g.PendingEngagement.SystemId : "none")}");
            foreach (var l in g.PendingEngagement != null ? SliceCampaign.PreviewEncounter(g, g.PendingEngagement) : new System.Collections.Generic.List<string>()) Note("  stake: " + l);
            // The game enters the system and opens battle preparation with the stakes; wait for it.
            yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60);
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "c04_battle_prep_stakes");

            // 4. The terms form before battle (shown, then cancelled), and the battle.
            if (g.PendingEngagement != null)
            {
                root.CampaignOfferBeforeBattle();
                yield return Wait(0.8f);
                yield return Automation.Shot(dir, "c05_terms_form");
                root.UI.CloseModal();
                yield return Until(() => !root.Busy, 30);
                root.StartBattle(BattleBridge.AutoSelect(g, mine));
                yield return Until(() => root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating, 60);
                yield return Wait(0.8f);
                var b = root.Battle;
                if (b == null) { Note("BLOCKER: the campaign battle did not start"); File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString()); Application.Quit(); yield break; }
                Note($"campaign battle {b.Setup.BattleId} on {b.S.Config.MapId}; capture policy {SliceRules.CaptureEnabled(b.S)}; player modules {string.Join(",", b.S.Units.Where(u => u.Owner == b.PlayerSeat).SelectMany(u => SliceRules.ModulesOf(u.Equipment)))}");
                yield return Automation.Shot(dir, "c06_campaign_battle");
                yield return Automation.Measure("campaign battle, idle on the player's turn", 300);
                yield return SliceAutomation.Pilot(root, 6);
                yield return Until(() => root.Battle == null || root.Battle.S.IsOver || (!root.Battle.Animating && root.Battle.PlayersTurn), 60);
                if (root.Battle != null && !root.Battle.S.IsOver)
                {
                    b = root.Battle;
                    ulong h = b.Session.Hash; int n = b.History.Count;
                    root.SaveGame(); root.LoadGame();
                    yield return Until(() => root.Battle != null && root.Battle != b && root.Scale == ViewScale.Battle && !root.Busy, 40);
                    Note("campaign battle save and load after " + n + " orders: " + (root.Battle.Session.Hash == h && root.Battle.History.Count == n ? "restored the same battle" : "DIFFERENT BATTLE"));
                    g = root.G;
                }
                // Delegation (Prompt 12): the same slice AI plays the player's side for the rest of the battle.
                if (root.Battle != null && !root.Battle.S.IsOver)
                {
                    root.Battle.Delegate(true);
                    Note("delegated the rest of the battle: " + root.Battle.Delegated + "; player input blocked: " + !root.Battle.PlayersTurn);
                    yield return Wait(1.2f);
                    yield return Automation.Shot(dir, "c06b_delegated");
                }
                // The decisive-action camera runs between the end of the battle and the result screen.
                bool sawReview = false;
                float t0 = Time.unscaledTime;
                while (!root.ResultPending && Time.unscaledTime - t0 < 300)
                {
                    if (root.Battle != null && root.Battle.Over && !root.ResultPending && root.Rig.Transitioning && !sawReview)
                    {
                        sawReview = true;
                        yield return Wait(1.0f);
                        if (!root.ResultPending) yield return Automation.Shot(dir, "c06c_decisive_camera");
                    }
                    yield return null;
                }
                Note("decisive-action camera shown before the result: " + sawReview);
                yield return Wait(0.8f);
                Note("decisive: " + root.Battle.Session.Decisive);
                yield return Automation.Shot(dir, "c07_campaign_result");
                root.CompleteResult();
                yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 40);
                yield return Wait(1.0f);
                g = root.G;
            }
            var passage = g.Slice.Passages[0];
            Note($"after the battle: access {passage.AccessHolderId ?? "contested"}; objective {g.Slice.Objectives.LastOrDefault()?.Status}; wrecks {g.Slice.Wrecks.Count}; " +
                 $"standing {g.Slice.RelationOf("pygmaei", "inhumanus").Opinion(g.Day)}; history {g.Slice.History.Count} events");
            foreach (var hev in g.Slice.History.Skip(Math.Max(0, g.Slice.History.Count - 6))) Note("  history: day " + hev.Day + ": " + hev.Text);
            yield return Automation.Shot(dir, "c08_after_battle");
            root.OpenHistory(null);
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "c09_history");
            root.UI.CloseModal();

            // 5. A strategic save and load reproduces the campaign exactly.
            ulong digest = SaveGame.Digest(g);
            root.SaveGame(); root.LoadGame();
            yield return Until(() => root.G != g && !root.Busy, 40);
            yield return Wait(1f);
            g = root.G;
            Note("strategic save and load: " + (SaveGame.Digest(g) == digest ? "identical digest" : "DIFFERENT"));

            // 6. The Eclipse expedition: sail to Gloam, chart the drift, fight under fog of war.
            mine = g.Fleets.FirstOrDefault(f => f.OwnerId == g.Player.Id);
            var ex = Expeditions.Of(g);
            if (mine != null && ex != null && g.PendingEngagement == null)
            {
                var go = StrategyRules.OrderMove(g, mine.Id, Expeditions.System);
                Note("order to Gloam: " + (go.Ok ? "accepted" : go.Reason));
                yield return Days(root, () => !mine.Moving && mine.SystemId == Expeditions.System, 180);
                Note("fleet at " + mine.SystemId + (g.PendingEngagement != null ? "; an engagement interrupted the voyage" : ""));
                root.OpenExpedition();
                yield return Wait(0.8f);
                yield return Automation.Shot(dir, "c10_expedition_briefing");
                var sc = Expeditions.Scout(g);
                Note("reconnaissance: " + (sc.Ok ? "charted" : sc.Reason));
                root.UI.CloseModal();
                if (mine.SystemId == Expeditions.System && g.PendingEngagement == null)
                {
                    yield return Until(() => !root.Busy, 30);
                    root.StartExpedition();
                    yield return Until(() => root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating, 60);
                    yield return Wait(1f);
                    var b = root.Battle;
                    if (b == null) { Note("BLOCKER: the expedition battle did not start"); File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString()); Application.Quit(); yield break; }
                    int enemyOn = b.S.Units.Count(u => u.Owner != b.PlayerSeat && u.OnBoard);
                    int enemyDrawn = b.Units.Values.Count(v => v.Owner != b.PlayerSeat && !v.Dead && v.T.gameObject.activeSelf);
                    int enemySeen = b.Seen.Units.Count(u => u.Owner != b.PlayerSeat && u.OnBoard);
                    Note($"expedition battle on {b.S.Config.MapId}, fog {b.S.Config.FogOfWar}: enemy ships on board {enemyOn}, visible to the player {enemySeen}, drawn {enemyDrawn}");
                    yield return Automation.Shot(dir, "c11_expedition_fog");
                    yield return Automation.Measure("expedition battle under fog", 300);
                    yield return SliceAutomation.Pilot(root, 400);
                    yield return Until(() => root.ResultPending, 120);
                    yield return Wait(0.8f);
                    yield return Automation.Shot(dir, "c12_expedition_result");
                    root.CompleteResult();
                    yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 40);
                    Note("expedition: " + Expeditions.Of(root.G).State + "; " + Expeditions.Of(root.G).Outcome);
                }
            }
            else Note("expedition skipped: " + (mine == null ? "no fleet" : ex == null ? "no expedition" : "an engagement is pending"));

            // 7. Home to the shipyard: repair, and fit the salvaged spare module (free once); then the rival's counter-offensive.
            g = root.G;
            mine = g.Fleets.FirstOrDefault(f => f.OwnerId == g.Player.Id);
            if (mine != null && g.PendingEngagement == null)
            {
                string home = SliceCampaign.HomeOf(g.Player.Id);
                if (mine.SystemId != home)
                {
                    var back = StrategyRules.OrderMove(g, mine.Id, home);
                    Note("order home to " + home + ": " + (back.Ok ? "accepted" : back.Reason));
                    yield return Days(root, () => !mine.Moving && mine.SystemId == home, 180);
                }
                var flagRec = mine.Ships.FirstOrDefault(s => ShipCatalog.IsLeader(s.DefId));
                if (flagRec != null)
                {
                    int hpBefore = flagRec.Hp;
                    var rep = SliceCampaign.Repair(g, flagRec.Uid);
                    Note($"repair: hull {hpBefore} -> {flagRec.Hp} of {SliceCampaign.MaxHull(g, flagRec)}: " + (rep.Ok ? "repaired" : rep.Reason));
                    var spare = SliceCampaign.RefitOptions(g, flagRec.Uid).FirstOrDefault(o => o.Module.Id == "ablative_plating");
                    var credits = g.Player.Stock;
                    var fitPlating = SliceCampaign.ApplyRefit(g, flagRec.Uid, "ablative_plating");
                    PlaytestMetrics.Refit(g.Slice.Profile(flagRec.Uid).Name, "ablative_plating", fitPlating.Ok, fitPlating.Reason);
                    Note("fit the salvaged ablative plating: " + (fitPlating.Ok ? "fitted" : fitPlating.Reason) + "; listed cost " + spare?.Cost + "; paid " + (credits - g.Player.Stock) +
                         "; modules now " + string.Join(",", g.Slice.Profile(flagRec.Uid).Modules));
                    root.OpenHangar(flagRec.Uid);
                    yield return Wait(1.2f);
                    yield return Automation.Shot(dir, "c13_hangar_after_campaign");
                    root.UI.CloseModal();
                }
                Note("forecast: " + (SliceCampaign.CounterOffensiveForecast(g) ?? "none"));
                yield return Automation.Shot(dir, "c14_forecast");
                var go2 = StrategyRules.OrderMove(g, mine.Id, SliceCampaign.PassageSystem);
                Note("order back to Umbra: " + (go2.Ok ? "accepted" : go2.Reason));
                int d0 = g.Day;
                yield return Days(root, () => false, 240);
                Note($"after {g.Day - d0} more days: " + (g.PendingEngagement != null ? "second encounter " + g.PendingEngagement.Id + " at " + g.PendingEngagement.SystemId : "no second encounter"));
                if (g.PendingEngagement != null)
                {
                    yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60);
                    yield return Wait(1.0f);
                    yield return Automation.Shot(dir, "c15_second_encounter_prep");
                    root.UI.CloseModal();
                    yield return Until(() => !root.Busy, 30);
                    root.StartBattle(BattleBridge.AutoSelect(g, mine));
                    yield return Until(() => root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating, 60);
                    if (root.Battle != null)
                    {
                        Note("second battle " + root.Battle.Setup.BattleId + ": player modules " + string.Join(",", root.Battle.S.Units.Where(u => u.Owner == root.Battle.PlayerSeat).SelectMany(u => SliceRules.ModulesOf(u.Equipment))));
                        yield return SliceAutomation.Pilot(root, 400);
                        yield return Until(() => root.ResultPending, 300);
                        yield return Wait(0.8f);
                        Note("second battle decisive: " + root.Battle.Session.Decisive);
                        yield return Automation.Shot(dir, "c16_second_result");
                        root.CompleteResult();
                        yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 40);
                        g = root.G;
                        var p2 = g.Slice.Passages[0];
                        Note($"after the second battle: access {p2.AccessHolderId}; history {g.Slice.History.Count} events; standing {g.Slice.RelationOf("pygmaei", "inhumanus").Opinion(g.Day)}");
                    }
                }
            }

            var metrics = PlaytestMetrics.FilePath;
            Note("local metrics: " + (metrics != null && File.Exists(metrics) ? File.ReadAllLines(metrics).Length + " lines in " + metrics : "none"));
            if (metrics != null && File.Exists(metrics)) foreach (var l in File.ReadAllLines(metrics).Where(x => x.Contains("battle_end") || x.Contains("first_decision") || x.Contains("refit"))) Note("  " + l);
            Note("campaign tour complete");
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            yield return Wait(1f);
            Application.Quit();
        }
    }
}
