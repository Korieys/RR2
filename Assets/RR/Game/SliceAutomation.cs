using System;
using System.Collections;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Evidence tour for the vertical slice's trade-passage encounter (-rrPassageTour <absolute dir>). It plays through
    // the real game with the ordinary controls' entry points: menu actions, selection, modes, hover previews and
    // BattlePresentation.Submit. The player's orders come from the same slice AI the opponent uses (a pilot), so this is
    // functional evidence, not a human playtest.
    public static class SliceAutomation
    {
        static IEnumerator Wait(float s) { float t = 0; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }
        static IEnumerator Until(Func<bool> cond, float timeout) { float t = 0; while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; } }
        static void Note(string s) => Automation.Note(s);

        static IEnumerator Ready(GameRoot root)
        {
            yield return Until(() => root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating, 60);
            yield return Wait(0.6f);
        }

        /// <summary>The pilot plays the player's side until <paramref name="orders"/> orders have been given or the battle ends.</summary>
        static IEnumerator Pilot(GameRoot root, int orders, Action<BattlePresentation, int> each = null)
        {
            var ai = new SliceAI();
            var b = root.Battle;
            int given = 0;
            float idle = 0;
            while (b != null && !b.S.IsOver && given < orders && idle < 120)
            {
                if (b.PlayersTurn)
                {
                    var c = ai.Choose(b.S);
                    if (c.UnitId != 0 && b.S.ActiveUnitId == 0) b.Select(c.UnitId);
                    b.Submit(c);
                    given++;
                    each?.Invoke(b, given);
                    idle = 0;
                }
                else idle += Time.unscaledDeltaTime;
                yield return null;
                b = root.Battle;
            }
        }

        public static IEnumerator PassageTour(GameRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            Note("passage tour; rules " + RulesVersion.Id + "; content problems " + SliceRoster.Validate().Count);
            yield return Wait(2f);

            // 1. The challenge, as the Pygmaei raiders.
            root.StartPassage(PassageVariant.Challenge, FactionId.Pygmaei, false);
            yield return Ready(root);
            var b = root.Battle;
            Note("battle " + b.Setup.BattleId + " on " + b.S.Config.MapId + " in " + b.Setup.SystemId + ": " + b.S.Units.Count(u => u.Owner == 0) + " Pygmaei against " +
                 b.S.Units.Count(u => u.Owner == 1) + " Inhumanus; topology " + b.S.Map.Topology.Summary());
            yield return Automation.Shot(dir, "p01_passage_start");
            yield return Automation.Measure("passage battle", 240);

            // 2. A flank: the western frigate, its reach, then its overclocked reach and a heat preview.
            var west = b.S.Units.Where(u => u.Owner == 0 && u.DefId == "pygmaei_skirmish_frigate").OrderBy(u => u.Position.Lateral).First();
            b.Select(west.Id);
            yield return Wait(0.4f);
            yield return Automation.Shot(dir, "p02_flank_frigate_selected");
            b.ToggleOverclock();
            Note("overclock mode: " + b.OverclockMode);
            var far = RosterMovement.Reachable(b.S, west, SliceRules.OverclockMove).Keys.OrderByDescending(h => -h.R).ThenBy(h => h).First();
            b.ForcedHover = far;
            yield return Wait(0.3f);
            Note("overclock preview at " + far + ": " + b.PreviewText);
            yield return Automation.Shot(dir, "p03_overclock_reach");
            b.ForcedHover = null;
            b.ToggleOverclock();

            // 3. The capital arc and the turn order.
            var flag = b.S.Commander(0);
            b.Select(flag.Id);
            yield return Wait(0.4f);
            yield return Automation.Shot(dir, "p04_capital_arc");
            b.ToggleTurn();
            b.ForcedHover = flag.Position + SliceRules.FacingVector(3);
            yield return Wait(0.3f);
            Note("turn mode: " + b.TurnMode + "; preview: " + b.PreviewText);
            yield return Automation.Shot(dir, "p04b_turn_preview");
            b.ForcedHover = null;
            b.ToggleTurn();

            // 4. The central route and the threat view, after a few orders.
            yield return Pilot(root, 6);
            yield return Until(() => !root.Battle.Animating && root.Battle.PlayersTurn, 30);
            b = root.Battle;
            b.ShowThreat = true; b.RefreshCells();
            yield return Wait(0.6f);
            Note("command threat: " + (b.MyThreat != null ? b.MyThreat.Summary("your flagship") : "none") + " | " + (b.MyExposure != null ? b.MyExposure.Summary("your flagship") : ""));
            Note("opportunity: " + (b.Opportunity != null ? (b.Opportunity.Lines.Count > 0 ? b.Opportunity.Lines[0].Text : "none") : "n/a"));
            yield return Automation.Shot(dir, "p05_threat_view");
            b.ShowThreat = false; b.RefreshCells();

            // 5. An attack preview on a real target, then the committed result.
            var shooter = b.S.Units.Where(u => u.Owner == 0 && RosterRules.Eligible(b.S, u)).FirstOrDefault(u => RosterCombat.LegalTargets(b.S, u).Count > 0);
            if (shooter != null)
            {
                b.Select(shooter.Id);
                var tgt = RosterCombat.LegalTargets(b.S, shooter).OrderBy(o => o.TargetHpAfter).First();
                b.ForcedHover = tgt.TargetHex;
                yield return Wait(0.4f);
                Note("attack preview: " + b.PreviewText);
                yield return Automation.Shot(dir, "p06_attack_preview");
                b.ForcedHover = null;
                int hpBefore = b.S.UnitAt(tgt.TargetHex).Hp;
                b.ClickHex(tgt.TargetHex);
                yield return Until(() => !root.Battle.Animating, 20);
                var after = b.S.Unit(tgt.Roster?.TargetId ?? tgt.TargetUnitId);
                Note("committed: previewed hull " + tgt.TargetHpAfter + ", committed hull " + (after != null && after.Alive ? after.Hp : 0) + (tgt.TargetHpAfter == (after != null && after.Alive ? after.Hp : 0) || (tgt.TargetDestroyed && (after == null || !after.Alive)) ? " (equal)" : " (DIFFERENT)"));
            }
            else Note("no legal attack yet for the preview shot");

            // 6. Save and load in the middle of the skirmish.
            yield return Until(() => !root.Battle.Animating && root.Battle.PlayersTurn, 30);
            b = root.Battle;
            ulong h = b.Session.Hash; int n = b.History.Count;
            root.SaveGame();
            root.LoadGame();
            yield return Until(() => root.Battle != null && root.Battle != b && root.Scale == ViewScale.Battle && !root.Busy, 40);
            b = root.Battle;
            Note("skirmish save and load after " + n + " orders: " + (b.Session.Hash == h && b.History.Count == n ? "restored the same battle" : "DIFFERENT BATTLE"));

            // 7. Play to the end; record duration and the decisive action.
            float t0 = Time.unscaledTime;
            yield return Pilot(root, 400);
            yield return Until(() => root.ResultPending, 120);
            yield return Wait(0.6f);
            Note($"battle played out in {Time.unscaledTime - t0:0} s of automated play (pilot and AI, animations on): not a human duration");
            yield return Automation.Shot(dir, "p07_result");
            b = root.Battle;
            Note("decisive: " + b.Session.Decisive);
            foreach (var line in b.Session.Narration.Skip(Math.Max(0, b.Session.Narration.Count - 5))) Note("  " + line);
            var div = BattleSession.FindDivergence(b.Session.Snapshot(), out var replayed);
            Note("replay from the record: " + (div == null && replayed.Hash == b.Session.Hash ? "identical" : "DIVERGED " + div));

            // 8. Swap sides: the Inhumanus escort, a few orders, then restart from the start.
            root.CompleteResult();
            yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 30);
            root.StartPassage(PassageVariant.Challenge, FactionId.Inhumanus, false);
            yield return Ready(root);
            yield return Pilot(root, 4);
            yield return Until(() => !root.Battle.Animating && root.Battle.PlayersTurn, 60);
            yield return Wait(0.5f);
            yield return Automation.Shot(dir, "p08_inhumanus_side");
            string before = root.Battle.Setup.BattleId;
            root.RestartSkirmish();
            yield return Until(() => root.Battle != null && root.Battle.Setup.BattleId != before && !root.Busy, 40);
            Note("restart: " + root.Battle.Setup.BattleId + " at " + root.Battle.History.Count + " orders, player seat " + root.Battle.Setup.PlayerSeat);

            // 9. Training: the enemy holds position; the lesson panel shows.
            root.Battle.Session.Submit(Command.Resign(root.Battle.Setup.PlayerSeat), root.Battle.Setup.PlayerSeat);
            yield return Until(() => root.ResultPending, 30);
            root.CompleteResult();
            yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 30);
            root.StartPassage(PassageVariant.Training, FactionId.Pygmaei, false);
            yield return Ready(root);
            yield return Automation.Shot(dir, "p09_training");
            yield return Pilot(root, 60);
            yield return Until(() => root.ResultPending, 60);
            Note("training: " + BattleNarrator.Ending(root.Battle.S) + " after " + root.Battle.History.Count + " orders");
            root.CompleteResult();
            yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 30);

            // 10. Two players at one machine: both sides' orders through the same interface.
            root.StartPassage(PassageVariant.Challenge, FactionId.Pygmaei, true);
            yield return Ready(root);
            b = root.Battle;
            var ai2 = new SliceAI();
            int seats = 0;
            for (int i = 0; i < 4 && !b.S.IsOver; i++)
            {
                yield return Until(() => !b.Animating, 20);
                int seat = b.S.ActiveSeat;
                var c = ai2.Choose(b.S);
                b.Submit(c);
                seats |= 1 << seat;
                yield return null;
            }
            Note("hotseat: orders given for seats " + (seats == 3 ? "0 and 1" : seats.ToString()) + "; AI never acted: " + (b.AiReason == "" ? "true" : "false"));
            yield return Automation.Shot(dir, "p10_hotseat");
            Note("passage tour complete");
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            yield return Wait(1f);
            Application.Quit();
        }
    }
}
