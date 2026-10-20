using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // The Passage War scenario (campaign alpha Prompt 14): starting it, the mandate window, the treaty proposal and the ending.
    // Every action goes through the strategy layer (PassageWar, SliceCampaign, StrategyRules); this file gathers choices and
    // shows the authoritative results.
    public sealed partial class GameRoot
    {
        bool _endingShown;

        public void StartPassageWar(FactionId f) => StartPassageWar(f, Variants.Standard);

        public void StartPassageWar(FactionId f, string variant)
        {
            if (Busy || Scale == ViewScale.Battle) return;
            UI.CloseModal();
            var g = PassageWar.Create(f, variant);
            _endingShown = false;
            StartCoroutine(ReplaceStateThen(g, $"{PassageWar.Title}: your mandate is in the passage panel.", () =>
            {
                var mine = G.Fleets.FirstOrDefault(x => x.OwnerId == G.Player.Id);
                if (mine != null) Selection.SelectFleet(G, mine.Id);
                PlaytestMetrics.Decision("started " + PassageWar.Id + " (" + variant + ") as " + f);
                OpenMandate();
            }));
        }

        /// <summary>Shows the ending once when a running scenario has just ended (after the galaxy is back in view).</summary>
        void MaybeShowEnding()
        {
            var sc = PassageWar.Of(G);
            if (sc == null || !sc.Ended || sc.Freeform) { if (sc == null || !sc.Ended) _endingShown = false; return; }
            if (_endingShown || UI.ModalOpen || Busy || Scale == ViewScale.Battle) return;
            _endingShown = true;
            Clock.Paused = true;
            ShowEnding();
        }

        public void OpenMandate()
        {
            var sc = PassageWar.Of(G);
            if (sc == null) return;
            var m = UI.OpenModal(960);
            var t = UIX.Text(sc.Title.ToUpperInvariant() + ": THE MANDATE", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P("Secure durable access through the Umbra passage and keep " + PassageWar.DesignatedNames(G, G.Player.Id) + " operational. " +
                    "Working scenario title; the conditions below are provisional design values.", 13));
            m.Add(P(PassageWar.Progress(G)));
            foreach (var w in PassageWar.Warnings(G)) m.Add(P(w, 13, new Color(1f, 0.55f, 0.5f)));
            if (sc.Pending != null)
            {
                // Prompt 17: a proposal from the rival waits for the player's answer (it expires at the next settlement).
                var pr = sc.Pending;
                m.Add(UIX.Text("PROPOSAL", "h-section"));
                m.Add(P(pr.Kind == "rights"
                    ? $"{G.GetPolity(pr.From).Name} offers {pr.Payment} credits for passage rights through Umbra, with a truce. Accepting gives them access by treaty (your mandate then needs access back, by treaty or by breaking the truce, which costs standing)."
                    : $"{G.GetPolity(pr.From).Name} proposes a truce that leaves the passage as it stands. Accepting ends the campaign as an inconclusive, negotiated status quo.", 13));
                m.Add(P($"Answer before day {pr.Expires}; unanswered, it lapses.", 12));
                var prow = Row(); m.Add(prow);
                B(prow, "Accept", () => { Report(PassageWar.AnswerProposal(G, true), "Accepted."); PlaytestMetrics.Decision("accepted a " + pr.Kind + " proposal"); OpenMandate(); });
                B(prow, "Decline", () => { Report(PassageWar.AnswerProposal(G, false), "Declined."); PlaytestMetrics.Decision("declined a " + pr.Kind + " proposal"); OpenMandate(); });
            }
            m.Add(UIX.Text("NEXT", "h-section"));
            foreach (var a in PassageWar.NextActions(G))
            {
                var r = Row(); m.Add(r);
                var l = P(a.Text, 13); l.style.width = 700; r.Add(l);
                if (a.Action != null) B(r, ActionLabel(a.Action), () => DoNextAction(a.Action));
            }
            m.Add(UIX.Text("CONDITIONS", "h-section"));
            foreach (var l in PassageWar.Requirements(G)) m.Add(P(l, 12));
            var last = sc.Reports.LastOrDefault();
            if (last != null)
            {
                m.Add(UIX.Text($"SETTLEMENT, DAY {last.Day}", "h-section"));
                foreach (var l in last.Lines.Take(8)) m.Add(P(l, 12));
            }
            var row = Row(); m.Add(row);
            if (sc.Ended) B(row, "The ending", ShowEnding);
            B(row, "Propose a treaty", OpenTreaty, PassageWar.CanPropose(G, G.Player.Id, 0) == null);
            B(row, "Close", () => UI.CloseModal());
        }

        static string ActionLabel(string action) => action switch
        {
            "declare" => "Declare war",
            "treaty" => "Treaty",
            "engagement" => "Battle preparation",
            "hangar" => "Hangar",
            "shipyard" => "Commission",
            "move" => "Send the fleet",
            _ => "Go",
        };

        public void DoNextAction(string action)
        {
            string me = G.Player.Id;
            switch (action)
            {
                case "declare": UI.CloseModal(); CampaignDeclare(); break;
                case "treaty": OpenTreaty(); break;
                case "engagement": UI.CloseModal(); OpenEngagement(); break;
                case "hangar":
                {
                    var flag = G.Fleets.Where(f => f.OwnerId == me).SelectMany(f => f.Ships).FirstOrDefault(s => SliceCampaign.IsCapital(s.DefId));
                    if (flag != null) OpenHangar(flag.Uid);
                    break;
                }
                case "shipyard":
                {
                    var yard = G.GetSystem(SliceCampaign.HomeOf(me)).Planets.FirstOrDefault(p => p.HasShipyard && p.OwnerId == me);
                    var opt = yard == null ? null : StrategyRules.ProjectOptions(G, yard.Id).FirstOrDefault(o => o.ShipDefId != null && ShipCatalog.IsLeader(o.ShipDefId));
                    if (opt == null) { UI.Strategy.Toast("No shipyard offers a flagship now.", "bad"); break; }
                    StartProject(yard.Id, ProjectKind.CommissionShip, opt.ShipDefId);
                    OpenMandate();
                    break;
                }
                case "move":
                {
                    var f = G.Fleets.FirstOrDefault(x => x.OwnerId == me && StrategyRules.CanFight(x));
                    if (f != null) Report(StrategyRules.OrderMove(G, f.Id, SliceCampaign.PassageSystem), f.Name + " sails for Umbra.");
                    OpenMandate();
                    break;
                }
            }
        }

        public void OpenTreaty()
        {
            var sc = PassageWar.Of(G);
            if (sc == null) return;
            string me = G.Player.Id, them = PassageWar.Other(me);
            var m = UI.OpenModal(860);
            var t = UIX.Text("A PASSAGE TREATY", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P($"Ask {G.GetPolity(them).Name} to grant you passage rights through Umbra, with a payment if you choose. They decide by the same rule as terms in battle: " +
                    "pressure against what the treaty costs them, from what both sides can see (fleets on the map, blockades, standing, payment). " +
                    "Accepted rights bring a truce and are ratified at the next settlement if they still stand. One proposal per settlement interval; a refusal costs nothing.", 13));
            var why = PassageWar.CanPropose(G, me, 0);
            if (why != null) m.Add(P(why, 13, new Color(1f, 0.55f, 0.5f)));
            var row = Row(); m.Add(row);
            foreach (var pay in new[] { 0, 25, 50, 75, 100 })
            {
                int amount = pay;
                B(row, amount == 0 ? "Propose without payment" : $"Offer {amount} credits", () => ProposeTreatyFromMenu(amount), why == null && PassageWar.CanPropose(G, me, amount) == null);
            }
            var close = Row(); m.Add(close);
            B(close, "Back to the mandate", OpenMandate);
            B(close, "Close", () => UI.CloseModal());
        }

        /// <summary>The treaty window's buttons: propose with this payment and show the answer with every factor.</summary>
        public void ProposeTreatyFromMenu(int amount)
        {
            var r = PassageWar.ProposeTreaty(G, G.Player.Id, amount, out var v);
            PlaytestMetrics.Decision($"proposed a treaty ({amount} credits): {(v.Valid && v.Accepted ? "accepted" : "declined")}");
            _campaign?.Invalidate();
            UI.Strategy.RefreshAll();
            TreatyAnswer(r, v);
        }

        void TreatyAnswer(StratResult r, OfferVerdict v)
        {
            var m = UI.OpenModal(820);
            var t = UIX.Text(!r.Ok ? "PROPOSAL REFUSED" : v.Accepted ? "TREATY ACCEPTED" : "TREATY DECLINED", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P(r.Ok ? v.Reason : r.Reason, 13));
            if (r.Ok && v.Accepted) m.Add(P($"Passage rights are ratified at the settlement on day {PassageWar.NextSettlementDay(G)} if the treaty still stands and your designated worlds are operational."));
            Audio.Play(r.Ok && v.Accepted ? "ui_select" : "ui_invalid", 0.6f);
            var row = Row(); m.Add(row);
            B(row, "The mandate", OpenMandate);
            B(row, "Close", () => UI.CloseModal());
        }

        public void ShowEnding()
        {
            var sc = PassageWar.Of(G);
            if (sc == null || !sc.Ended) return;
            var m = UI.OpenModal(980);
            var t = UIX.Text(PassageWar.OutcomeLabel(sc.Outcome).ToUpperInvariant(), "modal-title"); UIRoot.Display(t); m.Add(t);
            var lines = PassageWar.Ending(G);
            int shown = 0;
            foreach (var l in lines)
            {
                bool detail = l.StartsWith("  ", StringComparison.Ordinal);
                if (detail && ++shown > 24) continue;
                m.Add(P(l.Trim(), detail ? 12 : 14));
            }
            if (shown > 24) m.Add(P($"(and {shown - 24} earlier decisions in the history)", 11));
            Automation.Note("ending: " + PassageWar.OutcomeLabel(sc.Outcome) + "; " + sc.EndText);
            PlaytestMetrics.Decision("campaign ended: " + sc.Outcome);
            var row = Row(); m.Add(row);
            if (!sc.Freeform) B(row, "Continue freeform", () => { Report(PassageWar.ContinueFreeform(G), "The galaxy continues."); UI.CloseModal(); });
            B(row, "Scenario menu", () => { UI.CloseModal(); ShowPassageMenu(); });
            B(row, "History", () => OpenHistory(null));
            B(row, "Close", () => UI.CloseModal());
        }
    }
}
