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
    // The passage campaign's actions and modals (Prompts 6 to 10). Every action goes through the strategy layer's rules
    // (SliceCampaign, Negotiation, Expeditions, BattleBridge); this file only gathers the player's choice and shows results.
    public sealed partial class GameRoot
    {
        CampaignPanel _campaign;
        HangarPreview _hangar;
        Offer _pendingSettlement;
        int _offerSerial;

        void EnsureCampaignPanel()
        {
            if (_campaign == null) _campaign = new CampaignPanel(this, UI.HudLayer);
        }

        void CampaignUpdate()
        {
            EnsureCampaignPanel();
            _campaign.Update(Rig.Cam);
            MaybeShowEnding();
            SupplyUpdate();
        }

        /// <summary>A paragraph in the Command Glass text roles (SlicePanels.uss): body at reading size, supporting text smaller,
        /// warnings in the theme's danger colour. The size and colour arguments choose the role; no literal colours reach the UI.</summary>
        static Label P(string text, int size = 14, Color? col = null)
        {
            string role = col.HasValue && col.Value.r > 0.9f && col.Value.g < 0.8f ? "text-danger" : size >= 13 && !col.HasValue ? "body" : size >= 12 ? "item-sub" : "small";
            return role == "body" ? UIX.Text(text, "slice-line", "body") : UIX.Text(text, "slice-line", "body", role);
        }

        /// <summary>Light text on a dark field for form inputs (the default theme draws light grey on light grey here).</summary>
        static void Legible(VisualElement input)
        {
            if (input == null) return;
            input.style.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            input.style.color = new Color(0.95f, 0.93f, 0.88f);
            foreach (var t in input.Query<TextElement>().ToList()) t.style.color = new Color(0.95f, 0.93f, 0.88f);
        }

        static VisualElement Row() { var r = new VisualElement(); r.style.flexDirection = FlexDirection.Row; r.style.flexWrap = Wrap.Wrap; r.style.alignItems = Align.Center; return r; }
        static Button B(VisualElement row, string text, Action go, bool enabled = true) { var b = UIX.Btn(text, go); b.style.marginRight = 8; b.style.marginTop = 6; b.SetEnabled(enabled); row.Add(b); return b; }

        void Report(StratResult r, string ok)
        {
            UI.Strategy.Toast(r.Ok ? (r.Reason ?? ok) : r.Reason, r.Ok ? "ok" : "bad");
            Audio.Play(r.Ok ? "ui_select" : "ui_invalid", 0.6f);
            _campaign?.Invalidate();
            UI.Strategy.RefreshAll();
        }

        // ------------------------------------------------------------------ starting the campaign

        public void StartPassageCampaign(FactionId f)
        {
            if (Busy || Scale == ViewScale.Battle) return;
            UI.CloseModal();
            var g = SliceCampaign.Create(f);
            StartCoroutine(ReplaceStateThen(g, "The Umbra passage campaign: declare an objective in the passage panel on the right.", () =>
            {
                var mine = G.Fleets.FirstOrDefault(x => x.OwnerId == G.Player.Id);
                if (mine != null) Selection.SelectFleet(G, mine.Id);
            }));
        }

        IEnumerator ReplaceStateThen(GalaxyState g, string toast, Action after)
        {
            yield return ReplaceState(g, null, toast);
            while (Busy) yield return null;
            after?.Invoke();
            _campaign?.Invalidate();
        }

        public void CampaignDeclare()
        {
            var r = SliceCampaign.DeclareObjective(G, G.Player.Id, SliceCampaign.PassageSystem);
            if (r.Ok) PlaytestMetrics.Decision("declared the objective");
            Report(r, "War declared over the Umbra passage.");
        }

        public void CampaignSalvage(string wreckId)
        {
            var fleet = G.Fleets.FirstOrDefault(f => f.OwnerId == G.Player.Id && G.Slice.Wrecks.Any(w => w.Id == wreckId && w.SystemId == f.SystemId));
            Report(fleet == null ? StratResult.Reject("Move a fleet to the wreck first.") : SliceCampaign.Salvage(G, wreckId, fleet.Id), "Salvaged.");
        }

        // ------------------------------------------------------------------ history

        public void OpenHistory(string systemId)
        {
            var m = UI.OpenModal(820);
            var t = UIX.Text("HISTORY", "modal-title"); UIRoot.Display(t); m.Add(t);
            var events = G.Slice?.History ?? new List<HistoryEvent>();
            var list = (systemId != null ? events.Where(e => e.SystemId == systemId) : events).OrderByDescending(e => e.Day).Take(30).ToList();
            if (list.Count == 0) m.Add(P(systemId != null ? "Nothing has happened here yet." : "Nothing has happened yet."));
            foreach (var e in list) m.Add(P($"Day {e.Day}, {G.GetSystem(e.SystemId)?.Name ?? ""}: {e.Text}", 13));
            var named = G.Slice?.Ships.Values.Where(p => p.Name != null).ToList() ?? new List<ShipProfile>();
            foreach (var p in named)
            {
                m.Add(UIX.Text(p.Name.ToUpperInvariant() + (p.Destroyed ? " (destroyed)" : p.Captured ? " (in custody)" : ""), "h-section"));
                foreach (var e in p.Service.Skip(Math.Max(0, p.Service.Count - 6))) m.Add(P($"Day {e.Day}: {e.Text}", 12));
            }
            var row = Row(); m.Add(row);
            if (systemId != null) B(row, "All history", () => OpenHistory(null));
            B(row, "Close", () => UI.CloseModal());
        }

        // ------------------------------------------------------------------ hangar (Prompt 7)

        public void OpenHangar(int uid)
        {
            var ship = SliceCampaign.Ship(G, uid);
            var prof = G.Slice?.Profile(uid);
            if (ship == null || prof == null) { UI.Strategy.Toast("That ship is not in service.", "bad"); return; }
            if (_hangar == null) _hangar = HangarPreview.Create(transform);
            _hangar.Show(ship.DefId, prof.Modules);
            var m = UI.OpenModal(1100);
            var head = Row(); m.Add(head);
            var img = new VisualElement(); img.style.width = 520; img.style.height = 320; img.style.marginRight = 16;
            img.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(_hangar.Texture));
            head.Add(img);
            var info = new VisualElement(); info.style.width = 500; head.Add(info);
            var t = UIX.Text((prof.Name ?? ShipCatalog.Name(ship.DefId)).ToUpperInvariant(), "modal-title"); UIRoot.Display(t); info.Add(t);
            info.Add(P($"{ShipCatalog.Name(ship.DefId)}, flagship of {SliceCampaign.FleetOf(G, uid)?.Name}. Hull {ship.Hp} of {SliceCampaign.MaxHull(G, ship)}; battle scars {prof.Scars} (cosmetic)."));
            var name = new TextField("Name") { value = prof.Name ?? "", maxLength = SliceCampaign.MaxNameLength };
            name.style.width = 420; info.Add(name);
            Legible(name.Q(className: TextField.inputUssClassName));
            var nr = Row(); info.Add(nr);
            B(nr, "Rename", () => { Report(SliceCampaign.Rename(G, uid, name.value), "Renamed."); OpenHangar(uid); });
            var where = SliceCampaign.RefitLocationProblem(G, SliceCampaign.FleetOf(G, uid));
            var cost = SliceCampaign.RepairCost(G, uid);
            B(nr, ship.Hp < SliceCampaign.MaxHull(G, ship) ? $"Repair ({cost})" : "Hull at full strength", () => { Report(SliceCampaign.Repair(G, uid), "Repaired."); OpenHangar(uid); },
              ship.Hp < SliceCampaign.MaxHull(G, ship) && where == null);
            if (where != null) info.Add(P(where, 12, new Color(1f, 0.7f, 0.55f)));
            m.Add(UIX.Text("REFITS: ONE MODULE PER SLOT, EACH A SIDEGRADE", "h-section"));
            foreach (var o in SliceCampaign.RefitOptions(G, uid))
            {
                var r = Row(); m.Add(r);
                var lbl = P($"{o.Module.Name} ({o.Module.Slot}): {o.Compare} Cost {o.Cost}." + (o.Replaces != null && o.Replaces != o.Module.Id ? $" Replaces {SliceRules.Module(o.Replaces).Name}." : "") +
                            (o.Fitted ? " Fitted." : o.Blocked != null ? " " + o.Blocked : ""), 12);
                lbl.style.width = 860; r.Add(lbl);
                B(r, o.Fitted ? "Fitted" : "Fit", () =>
                {
                    var res = SliceCampaign.ApplyRefit(G, uid, o.Module.Id);
                    PlaytestMetrics.Refit(prof.Name ?? ship.DefId, o.Module.Id, res.Ok, res.Reason);
                    Report(res, "Refitted."); OpenHangar(uid);
                }, !o.Fitted && o.Blocked == null);
                var mo = o.Module.Id;
                B(r, "Preview", () => _hangar.Show(ship.DefId, prof.Modules.Where(x => SliceRules.Module(x).Slot != SliceRules.Module(mo).Slot).Concat(new[] { mo })));
            }
            m.Add(UIX.Text("SERVICE RECORD", "h-section"));
            foreach (var e in prof.Service.Skip(Math.Max(0, prof.Service.Count - 8))) m.Add(P($"Day {e.Day}: {e.Text}", 12));
            m.Add(P("Configuration: " + ship.DefId + (prof.Modules.Count > 0 ? " + " + string.Join(" + ", prof.Modules.Select(x => "module:" + x)) : "") +
                    ". The model is first-pass generated art; modules are separate assemblies on the hull.", 11, new Color(0.6f, 0.62f, 0.66f)));
            var close = Row(); m.Add(close);
            B(close, "Close", () => { _hangar.Hide(); UI.CloseModal(); });
        }

        // ------------------------------------------------------------------ negotiation (Prompt 8)

        VisualElement OfferForm(string title, string intro, Action<Offer> send, bool allowSurrender, Action cancel = null)
        {
            var m = UI.OpenModal(860);
            var t = UIX.Text(title, "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P(intro, 13));
            var who = new DropdownField("Who yields the passage", new List<string> { "They yield to us", "We yield to them" }, 0); who.style.width = 520; m.Add(who);
            Legible(who.Q(className: DropdownField.inputUssClassName));
            var rep = new SliderInt("Reparations (credits, paid by the side that yields)", 0, Negotiation.MaxReparations) { value = 0 }; rep.style.width = 700; m.Add(rep);
            var sur = new Toggle("The yielding side also surrenders its flagship into custody") { value = false }; m.Add(sur);
            sur.SetEnabled(allowSurrender);
            var row = Row(); m.Add(row);
            B(row, "Send the terms", () =>
            {
                int playerSeat = 0;
                var o = new Offer { Id = $"offer_{G.Day}_{++_offerSerial}", FromSeat = playerSeat, ConcederSeat = who.index == 0 ? 1 - playerSeat : playerSeat,
                    Reparations = rep.value / 10 * 10, SurrenderFlagship = sur.value };
                send(o);
            });
            B(row, "Cancel", () => { UI.CloseModal(); cancel?.Invoke(); });
            return m;
        }

        public void CampaignOfferBeforeBattle()
        {
            var e = G.PendingEngagement;
            if (e == null) return;
            OfferForm("TERMS BEFORE BATTLE", "No shots have been fired. The side that yields withdraws intact; the passage goes to the other side. The other power decides by the same rules it uses in battle: pressure against what the terms cost it.",
                o =>
                {
                    var seats = SliceCampaign.Seats(G, e);
                    var setup = new BattleSetup { BattleId = "settle_" + e.Id, EngagementId = e.Id, Config = new MatchConfig(), SystemId = e.SystemId };
                    setup.Config.Players[0] = new PlayerSetup { DisplayName = seats[0].Name }; setup.Config.Players[1] = new PlayerSetup { DisplayName = seats[1].Name };
                    setup.FleetIds[0] = seats[0].Id; setup.FleetIds[1] = seats[1].Id;
                    o.Revision = -1;
                    var v = Negotiation.Decide(G, setup, null, o, decidingSeat: 1);
                    ShowVerdict(v, () =>
                    {
                        var r = SliceCampaign.SettleBeforeBattle(G, e.Id, o);
                        Report(r, "Terms agreed.");
                        OnDay(new DayReport());
                    }, OpenEngagement);
                }, allowSurrender: true, cancel: OpenEngagement);
        }

        void ShowVerdict(OfferVerdict v, Action onAccept, Action onDecline = null)
        {
            var m = UI.OpenModal(820);
            var t = UIX.Text(!v.Valid ? "TERMS REFUSED" : v.Accepted ? "TERMS ACCEPTED" : "TERMS REJECTED", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P(v.Reason, 13));
            var row = Row(); m.Add(row);
            B(row, v.Valid && v.Accepted ? "Conclude" : "Back", () => { UI.CloseModal(); if (v.Valid && v.Accepted) onAccept(); else onDecline?.Invoke(); });
        }

        /// <summary>In battle: offers at the start of the player's activation, once per round (Negotiation.CanOffer).</summary>
        public void OfferInBattle()
        {
            if (Battle == null || _setup == null || _setup.EngagementId == null) return;
            var why = Negotiation.CanOffer(Battle.S, Battle.Offers, Battle.PlayerSeat);
            if (why != null) { Battle.Announce(why, "bad"); return; }
            OfferForm("TERMS", "The battle pauses while you draft terms. They are bound to this exact position: if anything moves, they are void.", o =>
            {
                o.Revision = Battle.S.Revision; o.Round = Battle.S.Round; o.BattleId = _setup.BattleId; o.FromSeat = Battle.PlayerSeat;
                o.ConcederSeat = o.ConcederSeat == 0 ? Battle.PlayerSeat : 1 - Battle.PlayerSeat;
                Battle.Offers.Add(o);
                var v = Negotiation.Decide(G, _setup, Battle.S, o, decidingSeat: 1 - Battle.PlayerSeat);
                ShowVerdict(v, () =>
                {
                    var r = Negotiation.Conclude(Battle.Session, o);
                    if (!r.Accepted) { Battle.Announce(r.Reason, "bad"); return; }
                    _pendingSettlement = o;
                    Battle.SyncAll(true);
                });
            }, allowSurrender: true);
        }

        // ------------------------------------------------------------------ the expedition (Prompt 10)

        public void OpenExpedition()
        {
            var e = Expeditions.Of(G);
            if (e == null) return;
            var m = UI.OpenModal(860);
            var t = UIX.Text(e.Title.ToUpperInvariant(), "modal-title"); UIRoot.Display(t); m.Add(t);
            foreach (var l in Expeditions.Briefing(G, e)) m.Add(P(l, 13));
            var fleet = G.Fleets.FirstOrDefault(f => f.OwnerId == G.Player.Id && f.SystemId == e.SystemId && !f.Moving);
            var row = Row(); m.Add(row);
            B(row, $"Reconnaissance ({Expeditions.ReconCost.Influence} influence)", () => { Report(Expeditions.Scout(G), "The drift is charted."); OpenExpedition(); }, e.State == ExpeditionState.Offered);
            B(row, fleet != null ? "Begin the expedition" : "Hold a fleet at Gloam to begin", () => { UI.CloseModal(); StartExpedition(); }, fleet != null && G.PendingEngagement == null);
            B(row, "Withdraw", () => { Report(Expeditions.Withdraw(G), "Withdrawn."); UI.CloseModal(); });
            B(row, "Close", () => UI.CloseModal());
        }

        public void StartExpedition()
        {
            if (Busy || Scale == ViewScale.Battle) return;
            var e = Expeditions.Of(G);
            var fleet = G.Fleets.FirstOrDefault(f => f.OwnerId == G.Player.Id && f.SystemId == e.SystemId && !f.Moving);
            if (fleet == null) { UI.Strategy.Toast("Hold a fleet at Gloam first.", "bad"); return; }
            var setup = Expeditions.Prepare(G, fleet, BattleBridge.AutoSelect(G, fleet), out var err);
            if (setup == null) { UI.Strategy.Toast(err, "bad"); return; }
            StartCoroutine(StartScenarioBattle(setup));
        }

        IEnumerator StartScenarioBattle(BattleSetup setup)
        {
            string err;
            var session = BattleSession.Begin(setup, out err);
            if (session == null) { UI.Strategy.Toast(err, "bad"); yield break; }
            UI.CloseModal();
            Clock.Paused = true;
            if (Scale == ViewScale.Planet) ExitPlanet();
            if (LocalSystem == null || LocalSystem.Data.Id != setup.SystemId || Scale == ViewScale.Galaxy)
            {
                if (Scale == ViewScale.System) yield return ExitToGalaxyRoutine();
                yield return EnterSystemRoutine(setup.SystemId);
            }
            Audio.Play("battle_commit");
            yield return EnterBattleRoutine(session);
            Battle.Announce("Sensors are short in the drift: you see only ships near your own. The derelict holds the centre.", "ok");
        }

        void FinishExpedition()
        {
            _resultShown = true;
            var o = BattleBridge.Extract(_setup, Battle.Session);
            var r = Expeditions.Resolve(G, _setup, o);
            var e = Expeditions.Of(G);
            var m = UI.OpenModal(860);
            var t = UIX.Text(o.WinnerSeat == _setup.PlayerSeat ? "THE DERELICT IS SECURED" : "DRIVEN OFF", "modal-title"); UIRoot.Display(t); m.Add(t);
            m.Add(P(r.Ok ? e.Outcome : r.Reason));
            m.Add(P("Decisive action: " + (Battle.Session.Decisive ?? BattleNarrator.Ending(Battle.S)), 13));
            var row = Row(); m.Add(row);
            _resultDone = () => { _resultDone = null; StartCoroutine(EndBattleRoutine()); };
            B(row, "Return to the galaxy", () => { UI.CloseModal(); if (_resultDone != null) _resultDone(); });
            Automation.Note("expedition over: " + e.State + "; " + e.Outcome);
        }
    }
}
