using System;
using System.Collections;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Evidence tour for The Passage War (campaign alpha Prompt 14; -rrWarTour <absolute dir>, with -rrSavePath <disposable>):
    // the scenario from the menu entry point, the mandate, a declined treaty, a deliberately lost first battle (the player's
    // side passes every order), the mandate after the loss, recovery through the next-action affordances (replacement
    // flagship or repair), further battles flown by the slice AI, settlements, a save and load in the middle of a hold, and
    // the ending. Player orders come from a pilot; this is functional evidence, not a human playtest.
    public static class WarAutomation
    {
        static void Note(string s) => Automation.Note(s);
        static IEnumerator Wait(float s) => SliceAutomation.Wait(s);
        static IEnumerator Until(Func<bool> c, float t) => SliceAutomation.Until(c, t);

        static IEnumerator Days(GameRoot root, Func<bool> until, float timeout)
        {
            root.SetSpeed(3);
            root.Clock.Paused = false;
            yield return Until(() => until() || root.G.PendingEngagement != null || (PassageWar.Of(root.G)?.Ended ?? false), timeout);
            root.Clock.Paused = true;
        }

        /// <summary>Plays the player's side with <paramref name="choose"/> until the battle ends.</summary>
        static IEnumerator Pilot(GameRoot root, Func<MatchState, Command> choose)
        {
            var b = root.Battle;
            float idle = 0;
            while (b != null && !b.S.IsOver && idle < 120)
            {
                if (b.PlayersTurn)
                {
                    var c = choose(b.S);
                    if (c.UnitId != 0 && b.S.ActiveUnitId == 0) b.Select(c.UnitId);
                    b.Submit(c);
                    idle = 0;
                }
                else idle += Time.unscaledDeltaTime;
                yield return null;
                b = root.Battle;
            }
        }

        static Command Pass(MatchState s) => s.ActiveUnitId != 0 ? Command.Wait(s.ActiveSeat, s.ActiveUnitId)
            : Command.Wait(s.ActiveSeat, s.Units.First(u => u.Owner == s.ActiveSeat && RosterRules.Eligible(s, u)).Id);

        static IEnumerator Battle(GameRoot root, string dir, string tag, bool lose)
        {
            var g = root.G;
            yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60);
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, tag + "_prep");
            root.UI.CloseModal();
            yield return Until(() => !root.Busy, 30);
            var mine = g.Fleets.First(f => f.OwnerId == g.Player.Id && f.EngagementId != null);
            root.StartBattle(BattleBridge.AutoSelect(g, mine));
            yield return Until(() => root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating, 60);
            if (root.Battle == null) { Note("BLOCKER: the battle did not start"); yield break; }
            Note($"{tag}: battle {root.Battle.Setup.BattleId} on {root.Battle.S.Config.MapId}; player orders: {(lose ? "pass every activation" : "slice AI pilot")}");
            float t0 = Time.unscaledTime;
            var ai = new SliceAI();
            yield return Pilot(root, lose ? (Func<MatchState, Command>)Pass : ai.Choose);
            yield return Until(() => root.ResultPending, 120);
            yield return Wait(0.8f);
            Note($"{tag}: over after {root.Battle.History.Count} orders, {Time.unscaledTime - t0:0} s; winner seat {root.Battle.S.Winner} ({root.Battle.S.VictoryReason}); decisive: {root.Battle.Session.Decisive}");
            yield return Automation.Shot(dir, tag + "_result");
            root.CompleteResult();
            yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 40);
            yield return Wait(1.0f);
        }

        static void Status(GameRoot root, string when)
        {
            var g = root.G; var sc = PassageWar.Of(g); var p = g.Slice.Passages[0];
            Note($"{when}: day {g.Day}; access {p.AccessHolderId ?? "contested"} ({p.Basis ?? "-"}); hold {sc.HoldCount}; outcome {sc.Outcome}; stock {g.Player.Stock}; " +
                 $"fleets {string.Join(", ", g.Fleets.Where(f => f.OwnerId == g.Player.Id).Select(f => $"{f.Name} at {f.SystemId} ({f.Ships.Count}{(StrategyRules.CanFight(f) ? ", flagship" : ", no flagship")})"))}");
            Note("  progress: " + PassageWar.Progress(g));
            foreach (var w in PassageWar.Warnings(g)) Note("  warning: " + w);
            foreach (var a in PassageWar.NextActions(g)) Note("  next: " + a.Text + (a.Action != null ? $" [{a.Action}]" : ""));
        }

        public static IEnumerator Tour(GameRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            Note("war tour; rules " + RulesVersion.Id + "; save " + GameRoot.SavePath);
            Automation.Environment();
            float start = Time.unscaledTime;
            yield return Wait(2f);

            // 1. The scenario from the menu's entry point; the mandate opens on its own.
            root.StartPassageWar(FactionId.Pygmaei);
            yield return Until(() => !root.Busy && PassageWar.Of(root.G) != null && root.UI.ModalOpen, 60);
            yield return Wait(1.2f);
            var g = root.G;
            Note($"scenario {PassageWar.Of(g).Title}: active systems {PassageWar.Of(g).ActiveSystems.Count} ({string.Join(", ", PassageWar.Of(g).ActiveSystems)})");
            foreach (var l in PassageWar.Requirements(g)) Note("  condition: " + l);
            yield return Automation.Shot(dir, "w01_mandate");
            root.UI.CloseModal();
            yield return Wait(0.6f);
            yield return Automation.Shot(dir, "w02_panel");
            // Prompt 15: the treasury (settlement preview and the budget choice) and the supply overlay.
            root.OpenTreasury();
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "w02b_treasury");
            foreach (var l in Economy.Preview(g, g.Player.Id)) Note("  preview: " + l);
            foreach (var o in Economy.Budget(g, g.Player.Id)) Note($"  budget: {o.Label}: {PassageWar.CostText(o.Cost)}; {(o.Affordable ? "affordable" : o.Blocked)}");
            root.UI.CloseModal();
            root.ToggleSupply();
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "w02c_supply_overlay");
            foreach (var l in Supply.Describe(g, g.Player.Id)) Note("  supply: " + l);

            // 2. A treaty on day 0 is declined (the rival has not been pressed); every factor is shown.
            root.ProposeTreatyFromMenu(100);
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "w03_treaty_declined");
            Note("treaty on day 0: " + g.Log.LastOrDefault()?.Text);
            root.UI.CloseModal();

            // 3. Declare and sail; the first battle is lost on purpose (the player's side passes).
            root.CampaignDeclare();
            var mine = g.Fleets.First(f => f.OwnerId == g.Player.Id);
            Note("order to Umbra: " + StrategyRules.OrderMove(g, mine.Id, SliceCampaign.PassageSystem));
            yield return Days(root, () => false, 120);
            if (g.PendingEngagement == null) { Note("BLOCKER: no encounter"); Finish(dir); yield break; }
            yield return Battle(root, dir, "w04_first", lose: true);
            g = root.G;
            Status(root, "after the first battle");
            root.OpenMandate();
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "w05_mandate_after_loss");
            root.UI.CloseModal();
            yield return Days(root, () => g.Day % PassageWar.Of(g).Interval == 1, 30);
            Note("after a settlement following the loss: ended " + PassageWar.Of(g).Ended + " (" + PassageWar.Of(g).HoldNote + ")");

            // 4. Recovery through the next-action affordances.
            for (int step = 0; step < 6 && !PassageWar.Of(g).Ended; step++)
            {
                var next = PassageWar.NextActions(g);
                var me = g.Player.Id;
                if (next.Any(a => a.Action == "shipyard"))
                {
                    root.DoNextAction("shipyard");
                    Note("recovery: commissioned a replacement flagship; in production " + PassageWar.FlagshipInProduction(g, me));
                    root.UI.CloseModal();
                    yield return Days(root, () => g.Fleets.Any(f => f.OwnerId == me && StrategyRules.CanFight(f)), 120);
                    continue;
                }
                if (!g.Fleets.Any(f => f.OwnerId == me && StrategyRules.CanFight(f)))
                {
                    yield return Days(root, () => PassageWar.CanAffordFlagship(g, me) || PassageWar.FlagshipInProduction(g, me), 120);
                    continue;
                }
                // Rebuild the screen at the home shipyard (each hull paid for and built over its declared time).
                var yardP = g.GetSystem("forgefall").Planets.First(p => p.HasShipyard && p.OwnerId == me);
                for (int k = 0; k < 4 && g.Fleets.Where(f => f.OwnerId == me).Sum(f => f.Ships.Count) < 6; k++)
                {
                    var esc = StrategyRules.ProjectOptions(g, yardP.Id).Where(o => o.ShipDefId != null && !ShipCatalog.IsLeader(o.ShipDefId)).OrderBy(o => o.Cost.Credits).First();
                    if (!esc.Available) { Note("recovery: escort unavailable: " + esc.UnavailableReason); yield return Days(root, () => StrategyRules.ProjectOptions(g, yardP.Id).Any(o => o.ShipDefId == esc.ShipDefId && o.Available), 60); }
                    root.StartProject(yardP.Id, ProjectKind.CommissionShip, esc.ShipDefId);
                    Note($"recovery: commissioned {ShipCatalog.Name(esc.ShipDefId)} ({PassageWar.CostText(esc.Cost)}, {esc.Days} days)");
                    yield return Days(root, () => yardP.Project == null, 60);
                }
                // Bring every fleet home to merge, then repair at the yard.
                foreach (var f in g.Fleets.Where(f => f.OwnerId == me && f.SystemId != "forgefall" && !f.Moving).ToList()) Note("recovery: " + f.Name + " home: " + StrategyRules.OrderMove(g, f.Id, "forgefall"));
                yield return Days(root, () => g.Fleets.Where(f => f.OwnerId == me).All(f => f.SystemId == "forgefall" && !f.Moving), 120);
                var flagRec = g.Fleets.Where(f => f.OwnerId == me).SelectMany(f => f.Ships).First(s => ShipCatalog.IsLeader(s.DefId));
                if (flagRec.Hp < SliceCampaign.MaxHull(g, flagRec)) Note("recovery: repair " + SliceCampaign.Repair(g, flagRec.Uid));
                break;
            }
            Status(root, "recovered");
            foreach (var r in PassageWar.Of(g).Reports.Skip(System.Math.Max(0, PassageWar.Of(g).Reports.Count - 2))) foreach (var l in r.Lines) Note($"  settlement day {r.Day}: {l}");
            yield return Automation.Shot(dir, "w06_recovered");

            // 5. Contest the passage again until the scenario ends; battles are flown by the slice AI.
            bool reloaded = false;
            for (int round = 0; round < 6 && !PassageWar.Of(g).Ended; round++)
            {
                var me = g.Player.Id;
                var p = g.Slice.Passages[0];
                if (p.AccessHolderId != me && !g.Slice.Objectives.Any(o => o.PolityId == me && o.Status == ObjectiveStatus.Declared))
                {
                    if (PassageWar.Attempts(g, me) >= SliceCampaign.MaxAttempts) { Note("no attempts left; waiting for the settlement"); }
                    else root.CampaignDeclare();
                }
                var fleet = g.Fleets.FirstOrDefault(f => f.OwnerId == me && StrategyRules.CanFight(f));
                if (fleet != null && p.AccessHolderId != me && fleet.SystemId != "umbra" && !fleet.Moving) Note("order to Umbra: " + StrategyRules.OrderMove(g, fleet.Id, "umbra"));
                yield return Days(root, () => false, 200);
                if (g.PendingEngagement != null)
                {
                    yield return Battle(root, dir, $"w07_battle{round + 2}", lose: false);
                    g = root.G;
                    Status(root, $"after battle {round + 2}");
                }
                // A save and load in the middle of a hold must keep the count.
                if (!reloaded && g.Slice.Passages[0].AccessHolderId == me && PassageWar.Of(g).HoldCount >= 1 && !PassageWar.Of(g).Ended)
                {
                    int held = PassageWar.Of(g).HoldCount; ulong digest = SaveGame.Digest(g);
                    root.SaveGame(); root.LoadGame();
                    yield return Until(() => !root.Busy && root.G != g, 30);
                    g = root.G;
                    reloaded = true;
                    Note($"save and load during the hold: count {held} -> {PassageWar.Of(g).HoldCount}; digest {(digest == SaveGame.Digest(g) ? "identical" : "DIFFERENT")}");
                }
            }

            // 6. The ending (shown by the game when the galaxy is in view).
            yield return Until(() => root.UI.ModalOpen && PassageWar.Of(root.G).Ended, 30);
            yield return Wait(1.0f);
            g = root.G;
            Status(root, "ending");
            foreach (var l in PassageWar.Ending(g)) Note("  ending: " + l);
            yield return Automation.Shot(dir, "w09_ending");
            int dayEnd = g.Day;
            root.UI.CloseModal();
            root.SaveGame(); ulong d0 = SaveGame.Digest(g); root.LoadGame();
            yield return Until(() => !root.Busy, 30);
            Note($"save and load after the ending: digest {(d0 == SaveGame.Digest(root.G) ? "identical" : "DIFFERENT")}; outcome {PassageWar.Of(root.G).Outcome}");
            var adv = StrategyRules.AdvanceDay(root.G);
            Note("advance after the ending: " + (adv.Advanced ? "ADVANCED" : adv.BlockedReason));
            Note($"campaign took {dayEnd} strategic days and {Time.unscaledTime - start:0} s of automated real time (not a human session length)");
            Finish(dir);
        }

        static void Finish(string dir)
        {
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            Application.Quit();
        }
    }
}
