using System;
using System.Collections;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Evidence tour for war operations (campaign alpha Prompt 16; -rrOpsTour <absolute dir>, with -rrSavePath <disposable>):
    // the Passage War as the Pygmaei; the legion embarks at Forgefall; the raiders sail on Dividend; the encounter there
    // becomes an invasion from battle preparation; orbital engagements; the defender's choice; landing; the planetary series;
    // a save and load between engagements; the settlement and what changed. Orders come from the slice AI (a pilot).
    public static class OpsAutomation
    {
        static void Note(string s) => Automation.Note(s);
        static IEnumerator Wait(float s) => SliceAutomation.Wait(s);
        static IEnumerator Until(Func<bool> c, float t) => SliceAutomation.Until(c, t);

        static IEnumerator Pilot(GameRoot root)
        {
            var ai = new SliceAI();
            var b = root.Battle;
            float idle = 0;
            while (b != null && !b.S.IsOver && idle < 120)
            {
                if (b.PlayersTurn)
                {
                    var c = ai.Choose(b.S);
                    if (c.UnitId != 0 && b.S.ActiveUnitId == 0) b.Select(c.UnitId);
                    b.Submit(c);
                    idle = 0;
                }
                else idle += Time.unscaledDeltaTime;
                yield return null;
                b = root.Battle;
            }
        }

        static void Status(GameRoot root, string when)
        {
            var g = root.G; var op = Operations.Active(g) ?? g.Slice.Operations.LastOrDefault();
            if (op == null) { Note(when + ": no operation"); return; }
            Note($"{when}: day {g.Day}; phase {op.Phase}; orbital {op.OrbitalAttackerWins}-{op.OrbitalDefenderWins} of {op.OrbitalFought}; protected {op.SuppliesProtected}, seized {op.SuppliesSeized}, follow-up {op.FollowUp}; series {op.AttackerWins}-{op.DefenderWins}; world {g.GetPlanet(op.PlanetId).Name} owned by {g.GetPlanet(op.PlanetId).OwnerId}");
            foreach (var l in Operations.Obligations(g, op)) Note("  " + l);
        }

        public static IEnumerator Tour(GameRoot root, string dir)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            Note("operations tour; rules " + RulesVersion.Id + "; save " + GameRoot.SavePath);
            Automation.Environment();
            float start = Time.unscaledTime;
            yield return Wait(2f);
            root.StartPassageWar(FactionId.Pygmaei);
            yield return Until(() => !root.Busy && PassageWar.Of(root.G) != null && root.UI.ModalOpen, 60);
            root.UI.CloseModal();
            var g = root.G;
            var raiders = g.Fleets.First(f => f.OwnerId == g.Player.Id);
            root.EmbarkAll(raiders.Id);
            Note($"embarked: {Operations.Embarked(g, raiders.Id)?.Units.Count ?? 0} of capacity {Operations.Capacity(raiders)}: " +
                 string.Join(", ", Operations.Embarked(g, raiders.Id)?.Units.Select(u => ShipCatalog.Name(u.DefId)) ?? new string[0]));
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "o01_embarked");
            // No declaration first: a declaration would send the escort to Umbra and leave Dividend empty.
            Note("order to Dividend: " + StrategyRules.OrderMove(g, raiders.Id, "dividend"));
            root.SetSpeed(3); root.Clock.Paused = false;
            yield return Until(() => g.PendingEngagement != null || (raiders.SystemId == "dividend" && !raiders.Moving), 60);
            root.Clock.Paused = true;
            Planet target;
            if (g.PendingEngagement != null)
            {
                yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60);
                yield return Wait(1.0f);
                yield return Automation.Shot(dir, "o02_prep_with_invade");
                target = root.InvasionTarget(g.PendingEngagement.SystemId, raiders);
                Note("battle preparation offers: Invade " + target?.Name);
            }
            else
            {
                target = root.InvasionTarget("dividend", raiders);
                Note("Dividend was undefended on arrival; the invasion is launched from the system (uncontested orbit)");
            }
            if (target == null) { Note("BLOCKER: no invasion target"); Finish(dir); yield break; }
            root.Invade(raiders.Id, target.Id);
            yield return Wait(1.0f);
            Status(root, "operation launched");
            foreach (var l in Operations.Stakes(g, Operations.Active(g))) Note("  stake: " + l);
            yield return Automation.Shot(dir, "o03_operation_window");
            bool reloaded = false;
            for (int step = 0; step < 10 && Operations.Active(g) != null; step++)
            {
                var op = Operations.Active(g);
                if (op.Phase == OperationPhase.AttackerChoice)
                {
                    Note("land: " + Operations.Land(g, op));
                    root.UI.CloseModal(); root.OpenOperation();
                    yield return Wait(0.8f);
                    yield return Automation.Shot(dir, "o05_landed");
                    continue;
                }
                if (op.Phase == OperationPhase.DefenderChoice) { Operations.AIStep(g, op); continue; }
                if (!reloaded && op.Battles.Count >= 1)
                {
                    ulong d = SaveGame.Digest(g);
                    root.UI.CloseModal();
                    root.SaveGame(); root.LoadGame();
                    yield return Until(() => !root.Busy && root.G != g, 30);
                    g = root.G; reloaded = true;
                    Note($"save and load between engagements: digest {(d == SaveGame.Digest(g) ? "identical" : "DIFFERENT")}; phase {Operations.Active(g)?.Phase}");
                    continue;
                }
                root.UI.CloseModal();
                yield return Until(() => !root.Busy, 60);   // the system view may still be arriving
                root.StartOperationBattle();
                yield return Until(() => (root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating) || Operations.Active(g)?.Battles.Count > op.Battles.Count, 90);
                if (root.Scale != ViewScale.Battle) { Note("no battle started: " + (op.Battles.LastOrDefault()?.Note ?? "nothing conceded")); continue; }
                string tag = $"o04_{(op.Phase == OperationPhase.Orbital ? "orbital" : "match")}{op.Battles.Count + 1}";
                Note($"{tag}: {root.Battle.Setup.BattleId} on {root.Battle.S.Config.MapId} ({root.Battle.S.Config.Domain}); units {root.Battle.S.Units.Count}");
                yield return Wait(1.0f);
                yield return Automation.Shot(dir, tag + "_start");
                float t0 = Time.unscaledTime;
                yield return Pilot(root);
                yield return Until(() => root.ResultPending, 120);
                yield return Wait(0.8f);
                Note($"{tag}: over in {Time.unscaledTime - t0:0} s after {root.Battle.History.Count} orders; {root.Battle.S.VictoryReason}");
                yield return Automation.Shot(dir, tag + "_result");
                root.CompleteResult();
                yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle && root.UI.ModalOpen, 60);
                g = root.G;
                Status(root, "after " + tag);
            }
            yield return Wait(1.0f);
            var last = g.Slice.Operations.Last();
            Note("operation outcome: " + last.Outcome);
            foreach (var h in g.Slice.History.Where(h => h.Kind == "operation" || h.Kind == "conquest" || h.Kind == "succession" || h.Kind == "prisoners")) Note("  history: day " + h.Day + ": " + h.Text);
            root.UI.CloseModal(); root.OpenOperation();
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "o06_settled");
            Note("time after the operation: " + (StrategyRules.AdvanceDay(g).Advanced ? "advances" : "blocked"));
            Note($"designated-world problem for the Inhumanus: {PassageWar.DesignatedProblem(g, "inhumanus") ?? "none"}");
            Note($"tour took {Time.unscaledTime - start:0} s of automated real time");
            Finish(dir);
        }

        static void Finish(string dir)
        {
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            Application.Quit();
        }
    }
}
