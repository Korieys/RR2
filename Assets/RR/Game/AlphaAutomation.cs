using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // The campaign alpha journey (Prompt 20; -rrAlphaTour <absolute dir> -rrJourney pygmaei|inhumanus, with -rrSavePath).
    // Through the game's own entry points: start the Passage War, choose an economic priority, inspect supply, name and refit
    // the flagship, choose a doctrine, then either the conquest route (Pygmaei: embark, invade Dividend, orbital engagements,
    // a planetary series with a deliberately lost first match as the setback, the settlement) or the negotiated route
    // (Inhumanus: a refused treaty as the setback, a blockade by the northern route, a treaty and its ratification), and on to
    // a valid ending, with saves and loads at the boundaries. Battles are flown by the slice AI: functional evidence only.
    public static class AlphaAutomation
    {
        static void Note(string s) => Automation.Note(s);
        static IEnumerator Wait(float s) => SliceAutomation.Wait(s);
        static IEnumerator Until(Func<bool> c, float t) => SliceAutomation.Until(c, t);
        static float _start;
        static readonly List<string> Steps = new List<string>();

        static void Step(string name, bool ok, string detail = null)
        {
            Steps.Add($"{(ok ? "PASS" : "FAIL")} {name}" + (detail != null ? ": " + detail : ""));
            Note($"STEP {(ok ? "PASS" : "FAIL")} {name}" + (detail != null ? ": " + detail : ""));
        }

        static IEnumerator Days(GameRoot root, Func<bool> until, float timeout)
        {
            root.SetSpeed(3); root.Clock.Paused = false;
            yield return Until(() => until() || root.G.PendingEngagement != null || (PassageWar.Of(root.G)?.Ended ?? false) || Operations.Active(root.G) != null, timeout);
            root.Clock.Paused = true;
        }

        static IEnumerator Pilot(GameRoot root, bool pass)
        {
            var ai = new SliceAI();
            var b = root.Battle; float idle = 0;
            while (b != null && !b.S.IsOver && idle < 120)
            {
                if (b.PlayersTurn)
                {
                    var s = b.S;
                    var c = pass ? (s.ActiveUnitId != 0 ? Command.Wait(s.ActiveSeat, s.ActiveUnitId) : Command.Wait(s.ActiveSeat, s.Units.First(u => u.Owner == s.ActiveSeat && RosterRules.Eligible(s, u)).Id)) : ai.Choose(s);
                    if (c.UnitId != 0 && s.ActiveUnitId == 0) b.Select(c.UnitId);
                    b.Submit(c); idle = 0;
                }
                else idle += Time.unscaledDeltaTime;
                yield return null;
                b = root.Battle;
            }
        }

        /// <summary>Fights whatever battle is pending (an encounter or the operation's next engagement) and returns to the galaxy.</summary>
        static IEnumerator FightPending(GameRoot root, string dir, string tag, bool pass = false)
        {
            var g = root.G;
            if (g.PendingEngagement != null)
            {
                yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60);
                root.UI.CloseModal();
                yield return Until(() => !root.Busy, 30);
                var mine = g.Fleets.First(f => f.OwnerId == g.Player.Id && f.EngagementId != null);
                root.StartBattle(BattleBridge.AutoSelect(g, mine));
            }
            else if (Operations.Active(g) != null)
            {
                root.UI.CloseModal();
                yield return Until(() => !root.Busy, 60);   // the system view may still be arriving
                root.StartOperationBattle();
            }
            yield return Until(() => (root.Scale == ViewScale.Battle && root.Battle != null && !root.Busy && !root.Battle.Animating) || !root.Busy && root.Scale != ViewScale.Battle && Operations.Active(g)?.PendingBattleId == null && g.PendingEngagement == null, 90);
            if (root.Scale != ViewScale.Battle) { Note(tag + ": no battle (conceded or nothing pending)"); yield break; }
            Note($"{tag}: {root.Battle.Setup.BattleId} ({root.Battle.S.Config.Domain}, {root.Battle.S.Config.MapId}); player orders: {(pass ? "pass (deliberate setback)" : "slice AI pilot")}");
            float t0 = Time.unscaledTime;
            yield return Pilot(root, pass);
            yield return Until(() => root.ResultPending, 120);
            yield return Wait(0.8f);
            Note($"{tag}: winner seat {root.Battle.S.Winner} ({root.Battle.S.VictoryReason}) after {root.Battle.History.Count} orders, {Time.unscaledTime - t0:0} s");
            yield return Automation.Shot(dir, tag);
            root.CompleteResult();
            yield return Until(() => !root.Busy && root.Scale != ViewScale.Battle, 60);
            yield return Wait(0.6f);
        }

        static IEnumerator SaveLoad(GameRoot root, string boundary)
        {
            var g = root.G; ulong d = SaveGame.Digest(g); int hist = g.Slice.History.Count; int ledger = PassageWar.Of(g).Ledger.Count;
            root.UI.CloseModal();
            root.SaveGame(); root.LoadGame();
            yield return Until(() => !root.Busy && root.G != g, 40);
            var h = root.G;
            bool same = d == SaveGame.Digest(h) && hist == h.Slice.History.Count && ledger == PassageWar.Of(h).Ledger.Count;
            var uids = h.Fleets.SelectMany(f => f.Ships).Select(s => s.Uid).Concat(Operations.AllUnits(h).Select(u => u.Uid)).ToList();
            Step("save and load at " + boundary, same && uids.Count == uids.Distinct().Count(), $"digest {(d == SaveGame.Digest(h) ? "identical" : "DIFFERENT")}, history {hist}->{h.Slice.History.Count}, no duplicated ships or units: {uids.Count == uids.Distinct().Count()}");
        }

        public static IEnumerator Tour(GameRoot root, string dir, string journey)
        {
            Directory.CreateDirectory(dir);
            Automation.UseDir(dir);
            _start = Time.unscaledTime; Steps.Clear();
            var f = journey == "inhumanus" || journey == "inhumanus_war" ? FactionId.Inhumanus : FactionId.Pygmaei;
            bool war = f == FactionId.Pygmaei || journey == "inhumanus_war";   // inhumanus_war: the Inhumanus on the conquest route
            Note($"alpha journey ({f}); rules {RulesVersion.Id}; content {RulesVersion.ContentFingerprint}; save {GameRoot.SavePath}");
            Automation.Environment();
            yield return Wait(2f);

            // 1. A configured campaign from the menu entry points.
            root.ShowVariantMenu(f);
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "a00_variant_menu");
            root.StartPassageWar(f, Variants.Standard);
            yield return Until(() => !root.Busy && PassageWar.Of(root.G) != null && root.UI.ModalOpen, 60);
            yield return Wait(0.8f);
            var g = root.G; string me = g.Player.Id, them = PassageWar.Other(me);
            Step("start a configured campaign", PassageWar.Of(g) != null, PassageWar.Of(g).Title + " / " + PassageWar.Of(g).Variant);
            yield return Automation.Shot(dir, "a01_mandate");
            root.UI.CloseModal();

            // 2. An economic priority, chosen from the treasury's budget.
            root.OpenTreasury();
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "a02_treasury");
            var pr = Economy.Priorities.First(x => x.Polity == me && x.Id == (me == "pygmaei" ? "ore_drive" : "convoy_office"));
            var planet = g.GetSystem(pr.SystemId).Planets.Where(p => p.OwnerId == me).OrderBy(p => p.Id).First();
            root.StartProject(planet.Id, ProjectKind.Priority, pr.Id);
            Step("choose an economic priority", planet.Project?.PriorityId == pr.Id, pr.Name);
            root.UI.CloseModal();

            // 3. Inspect supply.
            root.ToggleSupply();
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "a03_supply");
            Step("inspect supply", root.SupplyOverlay && root.SupplyNet != null, string.Join(" | ", Supply.Describe(g, me).Take(4)));

            // 4. A named, refitted flagship.
            var fleet = g.Fleets.First(x => x.OwnerId == me);
            int flag = fleet.Ships.First(s => ShipCatalog.IsLeader(s.DefId)).Uid;
            var rn = SliceCampaign.Rename(g, flag, me == "pygmaei" ? "Cinder Oath" : "Clause of Dawn");
            var rf = SliceCampaign.ApplyRefit(g, flag, me == "pygmaei" ? "afterburners" : "extended_magazines");
            root.OpenHangar(flag);
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "a04_hangar");
            root.UI.CloseModal();
            Step("prepare a named ship", rn.Ok && rf.Ok, $"{g.Slice.Profile(flag).Name}, modules {string.Join(",", g.Slice.Profile(flag).Modules)}");

            // 5. A progression choice.
            string doctrine = me == "pygmaei" ? "pyg_foundry_quotas" : war ? "inh_underwritten_escorts" : "inh_toll_brokerage";
            var dr = Doctrines.Start(g, me, doctrine);
            root.OpenDoctrine();
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "a05_doctrine");
            root.UI.CloseModal();
            Step("make a progression choice", dr.Ok, Doctrines.Get(doctrine).Name + " (" + Doctrines.PathNames[Doctrines.Get(doctrine).Path] + ")");
            yield return SaveLoad(root, "pending production and research");
            g = root.G; fleet = g.Fleets.First(x => x.OwnerId == me);

            if (war) yield return Conquest(root, dir); else yield return Negotiation(root, dir);
            g = root.G;

            // The ending.
            yield return Days(root, () => false, 120);
            for (int i = 0; i < 6 && !PassageWar.Of(root.G).Ended; i++)
            {
                if (root.G.PendingEngagement != null || Operations.Active(root.G) != null) { yield return FightPending(root, dir, "a90_late_battle" + i); continue; }
                yield return Days(root, () => false, 120);
            }
            g = root.G;
            var sc = PassageWar.Of(g);
            yield return Until(() => root.UI.ModalOpen, 20);
            yield return Wait(1.0f);
            yield return Automation.Shot(dir, "a99_ending");
            Step("reach a valid ending", sc.Ended, sc.Ended ? PassageWar.OutcomeLabel(sc.Outcome) + ": " + sc.EndText : "not ended by day " + g.Day);
            foreach (var l in PassageWar.Ending(g)) Note("  ending: " + l);
            if (sc.Ended) yield return SaveLoad(root, "after the ending");
            Note($"journey: {g.Day} strategic days, {Time.unscaledTime - _start:0} s of automated time (not a human session length)");
            Note("SUMMARY");
            foreach (var s in Steps) Note("  " + s);
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Automation.Log.ToString());
            Application.Quit();
        }

        // ------------------------------------------------------------------ the conquest route (Pygmaei on Dividend, Inhumanus on Ironmouth)

        static IEnumerator Conquest(GameRoot root, string dir)
        {
            var g = root.G; string me = g.Player.Id;
            bool pyg = me == "pygmaei";
            string home = SliceCampaign.HomeOf(me), goal = pyg ? "dividend" : "ironmouth", escort = pyg ? "pygmaei_skirmish_frigate" : "inhumanus_proxy_frigate";
            var fleet = g.Fleets.First(x => x.OwnerId == me);
            // Preparation for the invasion: two more frigates from the home yard (paid, built over their declared days), so the
            // transports survive the orbital phase with room for the whole legion.
            var yard = g.GetSystem(home).Planets.First(p => p.HasShipyard && p.OwnerId == me);
            for (int k = 0; k < 2; k++)
            {
                var esc = StrategyRules.ProjectOptions(g, yard.Id).FirstOrDefault(o => o.ShipDefId == escort);
                if (esc == null || !esc.Available) { Note("escort unavailable: " + esc?.UnavailableReason); yield return Days(root, () => StrategyRules.ProjectOptions(g, yard.Id).Any(o => o.ShipDefId == escort && o.Available), 30); }
                root.StartProject(yard.Id, ProjectKind.CommissionShip, escort);
                yield return Days(root, () => yard.Project == null, 30);
            }
            fleet = g.Fleets.Where(x => x.OwnerId == me && x.SystemId == home).OrderByDescending(x => x.Ships.Count).First();
            int legion = Operations.Garrison(g, yard.Id)?.Units.Count ?? 0;
            Step("prepare the invasion force", legion > 0 && Operations.Capacity(fleet) >= legion, $"{fleet.Name}: {fleet.Ships.Count} ships, transport capacity {Operations.Capacity(fleet)}");
            root.EmbarkAll(fleet.Id);
            Step("embark the legion", (Operations.Embarked(g, fleet.Id)?.Units.Count ?? 0) > 0, $"{Operations.Embarked(g, fleet.Id)?.Units.Count} units, capacity {Operations.Capacity(fleet)}");
            Note($"order to {g.GetSystem(goal).Name}: " + StrategyRules.OrderMove(g, fleet.Id, goal));
            yield return Days(root, () => fleet.SystemId == goal && !fleet.Moving, 60);
            // Battles on the way (the rival may intercept) are fought; then the invasion is launched.
            for (int i = 0; i < 3 && g.PendingEngagement != null && Operations.Active(g) == null; i++)
            {
                var e = g.PendingEngagement;
                var target = root.InvasionTarget(e.SystemId, fleet);
                if (target != null && e.SystemId == goal) { yield return Until(() => !root.Busy && root.UI.ModalOpen && root.Scale == ViewScale.System, 60); yield return Automation.Shot(dir, "a10_prep_invade"); root.Invade(fleet.Id, target.Id); break; }
                yield return FightPending(root, dir, "a10_encounter" + i);
                g = root.G; fleet = g.Fleets.FirstOrDefault(x => x.OwnerId == me && Operations.Embarked(g, x.Id) != null) ?? g.Fleets.First(x => x.OwnerId == me);
                if (fleet.SystemId != goal || fleet.Moving) { StrategyRules.OrderMove(g, fleet.Id, goal); yield return Days(root, () => fleet.SystemId == goal && !fleet.Moving, 60); }
            }
            if (Operations.Active(g) == null && fleet.SystemId == goal)
            {
                var target = root.InvasionTarget(goal, fleet);
                if (target != null) root.Invade(fleet.Id, target.Id);
            }
            var op = Operations.Active(g);
            Step("launch an operation", op != null, op != null ? $"{op.Id} against {g.GetPlanet(op.PlanetId).Name}; phase {op.Phase}" : "none");
            if (op == null) yield break;
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "a11_operation");
            bool setbackDone = false, reloadBetween = false;
            for (int step = 0; step < 12 && Operations.Active(root.G) != null; step++)
            {
                g = root.G; op = Operations.Active(g);
                if (op.Phase == OperationPhase.AttackerChoice) { Step("land the troops", Operations.Land(g, op).Ok, $"{op.Landed.Count} landed; supplies protected {op.SuppliesProtected}, seized {op.SuppliesSeized}"); continue; }
                if (op.Phase == OperationPhase.DefenderChoice) { Operations.AIStep(g, op); continue; }
                if (op.Phase == OperationPhase.Series && op.AttackerWins + op.DefenderWins == 1 && !reloadBetween)
                { reloadBetween = true; yield return SaveLoad(root, "between series rounds"); continue; }
                string tag = op.Phase == OperationPhase.Orbital ? $"a12_orbital{op.OrbitalFought + 1}" : $"a13_match{op.AttackerWins + op.DefenderWins + 1}";
                int lostBefore = op.Battles.Count(x => x.WinnerPolity != g.Player.Id);
                yield return FightPending(root, dir, tag);
                g = root.G; op = g.Slice.Operations.Last();
                if (!setbackDone && op.Battles.Count(x => x.WinnerPolity != g.Player.Id) > lostBefore && !op.Ended)
                {
                    setbackDone = true;
                    Step("recover from a nonterminal setback", true, $"{tag} was lost; the operation continued ({Operations.Next(g, op)})");
                }
            }
            g = root.G; op = g.Slice.Operations.Last();
            if (!setbackDone) Note("STEP NOTE recover from a nonterminal setback: not exercised in this run (no engagement was lost before the operation ended)");
            Step("complete the operation", op.Ended, op.Outcome);
            Note(op.WinnerPolity == g.Player.Id ? "conquest route: the planetary series was won" : "conquest route: the defender held the world");
            Step("ownership changed only by the final outcome", op.WinnerPolity == g.Player.Id ? g.GetPlanet(op.PlanetId).OwnerId == g.Player.Id : g.GetPlanet(op.PlanetId).OwnerId == op.DefenderPolity, $"{g.GetPlanet(op.PlanetId).Name}: {g.GetPlanet(op.PlanetId).OwnerId}");
            yield return Until(() => root.UI.ModalOpen, 20);
            yield return Automation.Shot(dir, "a14_settled");
            root.UI.CloseModal();
            yield return SaveLoad(root, "after the operation settlement");
            g = root.G;
            Step("return to the correct strategic state", StrategyRules.AdvanceDay(g).Advanced || PassageWar.Of(g).Ended, $"day {g.Day}; {PassageWar.Progress(g)}");
            // A conquered rival world is leverage: a treaty can now close the war.
            var v = PassageWar.EvaluateTreaty(g, g.Player.Id, 100);
            Note("treaty after the conquest: " + v.Reason);
            if (v.Valid && v.Accepted) root.ProposeTreatyFromMenu(100);
            else { root.CampaignDeclare(); var fl = g.Fleets.FirstOrDefault(x => x.OwnerId == g.Player.Id && StrategyRules.CanFight(x)); if (fl != null) StrategyRules.OrderMove(g, fl.Id, "umbra"); }
            yield return Wait(0.6f);
            root.UI.CloseModal();
        }

        // ------------------------------------------------------------------ the negotiated route (Inhumanus)

        static IEnumerator Negotiation(GameRoot root, string dir)
        {
            var g = root.G; string me = g.Player.Id, them = PassageWar.Other(me);
            root.ProposeTreatyFromMenu(100);
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "a20_treaty_refused");
            root.UI.CloseModal();
            Step("recover from a nonterminal setback", !PassageWar.Of(g).Ended && g.Slice.Passage("umbra").AccessHolderId != me, "the first treaty was refused: " + g.Log.LastOrDefault()?.Text);
            // A blockade of the Pygmaei second world by the northern route, avoiding the passage.
            var fleet = g.Fleets.First(x => x.OwnerId == me);
            Note("order to Ironmouth: " + StrategyRules.OrderMove(g, fleet.Id, "ironmouth"));
            for (int i = 0; i < 4 && !(fleet.SystemId == "ironmouth" && !fleet.Moving); i++)
            {
                yield return Days(root, () => fleet.SystemId == "ironmouth" && !fleet.Moving, 60);
                if (root.G.PendingEngagement != null) { yield return FightPending(root, dir, "a21_encounter" + i); g = root.G; fleet = g.Fleets.FirstOrDefault(x => x.OwnerId == me && StrategyRules.CanFight(x)) ?? g.Fleets.First(x => x.OwnerId == me); if (!fleet.Moving && fleet.SystemId != "ironmouth") StrategyRules.OrderMove(g, fleet.Id, "ironmouth"); }
            }
            g = root.G;
            Step("blockade by the northern route", g.GetSystem("ironmouth").Blockaded, $"Ironmouth orbit: {g.GetSystem("ironmouth").OrbitalControlId}");
            yield return Days(root, () => PassageWar.CanPropose(g, me, 100) == null, 60);
            var v = PassageWar.EvaluateTreaty(g, me, 100);
            Note("treaty under the blockade: " + v.Reason);
            root.ProposeTreatyFromMenu(100);
            yield return Wait(0.8f);
            yield return Automation.Shot(dir, "a22_treaty");
            root.UI.CloseModal();
            Step("choose negotiation (no conquest step)", g.Slice.Passage("umbra").AccessHolderId == me && g.Slice.Passage("umbra").Basis == PassageWar.Treaty && g.Slice.Operations.Count == 0,
                 $"access {g.Slice.Passage("umbra").AccessHolderId} ({g.Slice.Passage("umbra").Basis}); operations {g.Slice.Operations.Count}");
            yield return SaveLoad(root, "interrupted access and a treaty awaiting ratification");
        }
    }
}
