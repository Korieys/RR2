using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Campaign alpha Prompt 16: war operations in the game. The operation window states the stakes before every commitment
    // and every remaining obligation for both sides; its buttons call the strategy layer (Operations). Orbital engagements
    // and planetary matches run through the ordinary battle flow (BattleSession) and come back here when they end.
    public sealed partial class GameRoot
    {
        /// <summary>A rival world in <paramref name="systemId"/> the player's fleet there could invade, or null.</summary>
        public Planet InvasionTarget(string systemId, Fleet fleet)
        {
            if (fleet == null || PassageWar.Of(G) == null) return null;
            return G.GetSystem(systemId)?.Planets.Where(p => p.OwnerId != null && p.OwnerId != fleet.OwnerId && (p.OwnerId == "pygmaei" || p.OwnerId == "inhumanus"))
                    .OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        }

        public void Invade(string fleetId, string planetId)
        {
            var r = Operations.Launch(G, fleetId, planetId, out var op);
            PlaytestMetrics.Decision("launched an operation: " + (r.Ok ? op.Id : r.Reason));
            Report(r, "Operation begun.");
            if (r.Ok) { UI.CloseModal(); OpenOperation(); }
        }

        public void EmbarkAll(string fleetId)
        {
            var f = G.GetFleet(fleetId);
            var gar = f == null ? null : G.GetSystem(f.SystemId).Planets.Select(p => Operations.Garrison(G, p.Id)).FirstOrDefault(x => x != null && x.OwnerId == f.OwnerId && x.Units.Count > 0);
            if (gar == null) { UI.Strategy.Toast("No garrison of yours is here.", "bad"); return; }
            int room = Operations.Capacity(f) - (Operations.Embarked(G, fleetId)?.Units.Count ?? 0);
            var take = gar.Units.OrderByDescending(u => ShipCatalog.IsLeader(u.DefId)).ThenByDescending(u => ShipCatalog.Cost(u.DefId)).ThenBy(u => u.Uid).Take(Math.Max(0, room)).Select(u => u.Uid).ToList();
            Report(Operations.Embark(G, fleetId, gar.PlanetId, take), $"{take.Count} ground unit(s) embarked.");
        }

        public void OpenOperation()
        {
            var op = Operations.Active(G) ?? G.Slice?.Operations.LastOrDefault();
            if (op == null) return;
            string me = G.Player.Id;
            var p = G.GetPlanet(op.PlanetId);
            var m = UI.OpenModal(1040);
            var t = UIX.Text($"OPERATION: {p.Name.ToUpperInvariant()}", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P($"{G.GetPolity(op.AttackerPolity).Name} invades {p.Name} ({G.GetSystem(op.SystemId).Name}); {G.GetPolity(op.DefenderPolity).Name} defends. " +
                    $"You are the {(op.AttackerPolity == me ? "attacker" : "defender")}. Provisional rules.", 13));
            m.Add(UIX.Text(op.Ended ? "OUTCOME" : "NOW", "h-section"));
            foreach (var l in Operations.Obligations(G, op)) m.Add(P(l, 13));
            if (op.Battles.Count > 0)
            {
                m.Add(UIX.Text("SO FAR", "h-section"));
                foreach (var b in op.Battles)
                    m.Add(P($"{(b.Kind == "orbital" ? "Orbital" : "Planetary")} {(b.Conceded ? "(conceded)" : "")}: {G.GetPolity(b.WinnerPolity)?.Name ?? "-"} won" +
                            (b.MapId != null ? $" on {MapLibrary.Get(b.MapId).Name}" : "") + (b.Note != null && b.Conceded ? ". " + b.Note : "."), 12));
            }
            var row = Row(); m.Add(row);
            if (!op.Ended)
            {
                if (op.Phase == OperationPhase.Orbital || op.Phase == OperationPhase.Series)
                {
                    var domain = op.Phase == OperationPhase.Orbital ? BattleDomain.Orbital : BattleDomain.Planetary;
                    var cmd = Operations.Commander(G, op, me, domain);
                    if (cmd.uid == 0)
                    {
                        var succ = Operations.Successors(G, op, me, domain);
                        m.Add(P(succ.Count == 0 ? "You have no commander and no eligible successor: you would concede the next engagement." :
                                "Your commander is lost. Name an acting commander from your surviving units (nobody is resurrected):", 13, new Color(1f, 0.6f, 0.55f)));
                        foreach (var uid in succ.Take(6))
                        {
                            var def = domain == BattleDomain.Orbital ? G.Fleets.SelectMany(f => f.Ships).First(s => s.Uid == uid).DefId : Operations.AllUnits(G).First(u => u.Uid == uid).DefId;
                            B(row, "Acting: " + ShipCatalog.Name(def), () => { Report(Operations.NameSuccessor(G, op, me, domain, uid), "Acting commander named."); OpenOperation(); });
                        }
                    }
                    if (domain == BattleDomain.Planetary)
                    {
                        var lineup = Operations.Lineup(G, op, me, out _);
                        m.Add(P("Your next deployment: " + (lineup.Count == 0 ? "none (you would concede)" : string.Join(", ", lineup.Select(u => $"{ShipCatalog.Name(u.DefId)} {u.Hp}/{ShipCatalog.MaxHp(u.DefId)}"))) + ".", 12));
                    }
                    B(row, op.Phase == OperationPhase.Orbital ? "Fight the orbital engagement" : "Fight the planetary match", StartOperationBattle);
                }
                if (op.Phase == OperationPhase.DefenderChoice && op.DefenderPolity == me)
                {
                    B(row, "Keep the supplies (stop)", () => { Report(Operations.DefenderStops(G, op), "Supplies kept."); AfterOperationDecision(op); });
                    B(row, "Elect a second engagement", () => { Report(Operations.DefenderFollowUp(G, op), "A second engagement is elected."); AfterOperationDecision(op); });
                }
                if (op.AttackerPolity == me && (op.Phase == OperationPhase.AttackerChoice || op.Phase == OperationPhase.Series))
                {
                    if (op.Phase == OperationPhase.AttackerChoice) B(row, "Land the troops", () => { Report(Operations.Land(G, op), "Troops landed."); OpenOperation(); });
                    B(row, "Offer terms (they yield the world)", () =>
                    {
                        var r = Operations.OfferSurrenderTerms(G, op, out var v);
                        Report(r, v.Accepted ? "They yielded the world." : "Terms rejected.");
                        UI.CloseModal(); var mm = UI.OpenModal(820);
                        var tt = UIX.Text(r.Ok && v.Accepted ? "TERMS ACCEPTED" : "TERMS REJECTED", "modal-title"); UIRoot.Display(tt); mm.Add(tt);
                        mm.Add(P(r.Ok ? v.Reason : r.Reason, 13));
                        var rr = Row(); mm.Add(rr); B(rr, "The operation", OpenOperation);
                    });
                }
                if (op.AttackerPolity == me) B(row, "Withdraw", () => { Report(Operations.Withdraw(G, op), "Withdrawn."); OpenOperation(); });
            }
            B(row, "Close", () => UI.CloseModal());
            m.Add(UIX.Text("STAKES AND RULES", "h-section"));
            foreach (var l in Operations.Stakes(G, op)) m.Add(P(l, 11));
        }

        void AfterOperationDecision(Operation op)
        {
            Operations.AIStep(G, op);
            OpenOperation();
        }

        public void StartOperationBattle()
        {
            var op = Operations.Active(G);
            if (op == null || Scale == ViewScale.Battle) return;
            if (Busy) { UI.Strategy.Toast("The view is still moving; try again in a moment.", "bad"); return; }
            var setup = Operations.PrepareNext(G, op, out var err, out var conceded);
            if (conceded) { Report(StratResult.Accept(op.Battles.Last().Note), "Conceded."); OpenOperation(); return; }
            if (setup == null) { UI.Strategy.Toast(err, "bad"); return; }
            _setup = setup;
            PlaytestMetrics.Decision("operation battle " + setup.BattleId);
            StartCoroutine(StartScenarioBattle(setup));
        }

        void FinishOperationBattle()
        {
            _resultShown = true;
            var o = BattleBridge.Extract(_setup, Battle.Session);
            var op = Operations.Get(G, _setup.OperationId);
            int history = G.Slice.History.Count;
            var r = Operations.ApplyBattle(G, _setup, o);
            if (!r.Ok) Automation.Note("operation battle rejected: " + r.Reason);
            var m = UI.OpenModal(900);
            bool won = Operations.SeatPolities(G, op)[o.WinnerSeat] == G.Player.Id;
            var t = UIX.Text(won ? "ENGAGEMENT WON" : "ENGAGEMENT LOST", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P("Decisive action: " + (Battle.Session.Decisive ?? BattleNarrator.Ending(Battle.S)), 13));
            foreach (var h in G.Slice.History.Skip(history)) m.Add(P(h.Text, 13));
            m.Add(P("Next: " + Operations.Next(G, op), 13));
            Automation.Note($"operation battle {_setup.BattleId}: {o.Reason}; next: {Operations.Next(G, op)}");
            var row = Row(); m.Add(row);
            _resultDone = () => { _resultDone = null; StartCoroutine(EndOperationBattle()); };
            B(row, "Continue", () => { UI.CloseModal(); if (_resultDone != null) _resultDone(); });
        }

        IEnumerator EndOperationBattle()
        {
            yield return EndBattleRoutine();
            while (Busy) yield return null;
            OpenOperation();
        }
    }
}
