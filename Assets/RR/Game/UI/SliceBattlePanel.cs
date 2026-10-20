using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // The vertical slice's command panel beside the battle HUD (Prompts 2 to 5): the Command Threat assessment, the
    // opportunity on the enemy flagship, the faction doctrine, overclock and turn controls, contracts under way, training
    // lessons, and world labels for heat and contract entry hexes. It reads the authoritative state and the analysis
    // computed by BattlePresentation; it never decides anything.
    public sealed class SliceBattlePanel
    {
        readonly BattlePresentation _b;
        public VisualElement Root { get; private set; }
        readonly VisualElement _panel, _lesson, _labels;
        readonly Label _threat, _opportunity, _exposure, _doctrine, _contracts, _ai, _lessonText;
        readonly Button _overclock, _turn, _board, _offer, _delegate, _delegateAll, _lessonNext;
        readonly Label _delegation;
        /// <summary>Set by GameRoot: opens the terms form (campaign battles only).</summary>
        public Action OfferTerms;
        readonly List<Label> _worldLabels = new List<Label>();
        int _lesson_ = 0, _sig = -1;
        bool _doctrineOpen;

        // Panels use the Command Glass surfaces (.panel, decorated by Prismatic) and semantic line roles (SlicePanels.uss).
        static Label Line(string text, string role = "body") => UIX.Text(text, "slice-line", role);

        public SliceBattlePanel(BattlePresentation b, VisualElement parent)
        {
            _b = b;
            Root = new VisualElement(); Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute; Root.style.left = 0; Root.style.right = 0; Root.style.top = 0; Root.style.bottom = 0;
            parent.Add(Root);
            parent.AddToClassList("slice-battle");   // the battle detail panel makes room for the Command Threat panel
            _parent = parent;
            _labels = new VisualElement(); _labels.pickingMode = PickingMode.Ignore;
            _labels.style.position = Position.Absolute; _labels.style.left = 0; _labels.style.right = 0; _labels.style.top = 0; _labels.style.bottom = 0;
            Root.Add(_labels);

            _panel = UIX.Div("panel", "slice-threat");
            Root.Add(_panel);
            _panel.Add(UiKit.Section("01", "Command threat"));
            _threat = Line(""); _panel.Add(_threat);
            _exposure = Line("", "item-sub"); _panel.Add(_exposure);
            _opportunity = Line("", "slice-accent"); _panel.Add(_opportunity);
            _contracts = Line("", "slice-notice"); _panel.Add(_contracts);
            var row = UIX.Div("slice-actions"); _panel.Add(row);
            _overclock = UIX.Btn("Overclock (O)", () => _b.ToggleOverclock()); row.Add(_overclock);
            _turn = UIX.Btn("Turn (R)", () => _b.ToggleTurn()); row.Add(_turn);
            _board = UIX.Btn("Board (B)", () => _b.ToggleBoard()); row.Add(_board);
            _offer = UIX.Btn("Offer terms", () => OfferTerms?.Invoke()); row.Add(_offer);
            _delegate = UIX.Btn("Delegate this round (G)", () => { if (_b.Delegated) _b.StopDelegating(); else _b.Delegate(false); _sig = -1; }); row.Add(_delegate);
            _delegateAll = UIX.Btn("Delegate the battle", () => { _b.Delegate(true); _sig = -1; }); row.Add(_delegateAll);
            _delegation = Line("", "slice-accent"); _panel.Add(_delegation);
            row.Add(UIX.Btn("Doctrine", () => { _doctrineOpen = !_doctrineOpen; _sig = -1; }));
            _doctrine = Line("", "item-sub"); _panel.Add(_doctrine);
            _ai = Line("", "small"); _panel.Add(_ai);

            _lesson = UIX.Div("panel", "slice-lesson");
            _lessonText = Line("");
            _lesson.Add(UiKit.Section("01", "Training"));
            _lesson.Add(_lessonText);
            _lessonNext = UIX.Btn("Next lesson", () => { _lesson_ = Mathf.Min(_lesson_ + 1, PassageScenario.TrainingSteps.Length - 1); _sig = -1; });
            _lesson.Add(_lessonNext);
            Root.Add(_lesson);
        }

        readonly VisualElement _parent;
        public void Dispose() { Root.RemoveFromHierarchy(); _parent?.RemoveFromClassList("slice-battle"); }

        string LeaderName(int seat) { var l = _b.S.Commander(seat); return l != null ? l.Def.Name : "the flagship"; }

        public void Update(Camera cam)
        {
            var S = _b.S;
            int me = _b.Setup.PlayerSeat;
            var u = _b.Selected != 0 ? S.Unit(_b.Selected) : null;
            bool canOc = u != null && SliceRules.CanOverclock(S, u) == null;
            _overclock.style.display = u != null && SliceRules.OverclockCapable(u) ? DisplayStyle.Flex : DisplayStyle.None;
            _overclock.SetEnabled(_b.PlayersTurn && (canOc || _b.OverclockMode));
            _overclock.text = _b.OverclockMode ? "Cancel overclock (O)" : u != null && SliceRules.OverclockCapable(u) ? $"Overclock (O): heat {SliceRules.Heat(u)}/{SliceRules.OverheatAtOf(u)}" : "Overclock (O)";
            _turn.style.display = u != null && SliceRules.HasArc(u) ? DisplayStyle.Flex : DisplayStyle.None;
            _turn.SetEnabled(_b.PlayersTurn);
            _turn.text = _b.TurnMode ? "Cancel turn (R)" : "Turn (R)";
            bool canDelegate = !_b.Setup.Hotseat && !S.IsOver;
            _delegate.style.display = canDelegate ? DisplayStyle.Flex : DisplayStyle.None;
            _delegate.text = _b.Delegated ? "Take command back (G)" : "Delegate this round (G)";
            _delegate.tooltip = _b.Delegated ? "Your orders return to you from the next decision." : BattlePresentation.DelegateDisclosure;
            _delegateAll.style.display = canDelegate && !_b.Delegated ? DisplayStyle.Flex : DisplayStyle.None;
            _delegateAll.tooltip = BattlePresentation.DelegateDisclosure;
            _delegation.style.display = _b.Delegated ? DisplayStyle.Flex : DisplayStyle.None;
            _delegation.text = _b.Delegated ? (_b.DelegatedForBattle ? "Delegated for the rest of the battle" : $"Delegated until the end of round {S.Round}") +
                ": the opponent's AI plays your ships by the same rules with your side's view. Not guaranteed best play." : "";
            _board.style.display = u != null && SliceRules.CaptureEnabled(S) ? DisplayStyle.Flex : DisplayStyle.None;
            _board.SetEnabled(_b.PlayersTurn);
            _board.text = _b.BoardMode ? "Cancel boarding (B)" : "Board (B)";
            _offer.style.display = _b.Setup.EngagementId != null && OfferTerms != null ? DisplayStyle.Flex : DisplayStyle.None;
            var whyNot = Negotiation.CanOffer(S, _b.Offers, me);
            _offer.SetEnabled(_b.PlayersTurn && whyNot == null);
            _offer.tooltip = whyNot ?? "Propose terms to end the battle now. They are bound to this exact position.";

            int sig = S.Revision * 31 + _b.AnalysisRevision * 17 + (_doctrineOpen ? 1 : 0) + _lesson_ * 7 + (_b.MyThreat != null ? _b.MyThreat.Lines.Count * 3 : 0) + (_b.AiThinking ? 5 : 0);
            if (sig != _sig)
            {
                _sig = sig;
                Refresh(S, me);
            }
            UpdateWorldLabels(cam);
        }

        void Refresh(MatchState S, int me)
        {
            if (S.IsOver) { _panel.style.display = DisplayStyle.None; _lesson.style.display = DisplayStyle.None; return; }
            _panel.style.display = DisplayStyle.Flex;
            var t = _b.MyThreat;
            _threat.text = t == null ? "" : t.Summary("your " + LeaderName(me));
            _threat.EnableInClassList("text-danger", t != null && t.Lines.Count > 0);
            var x = _b.MyExposure;
            _exposure.text = x == null ? "" : x.Summary("your " + LeaderName(me));
            var o = _b.Opportunity;
            _opportunity.text = o == null ? "" : o.Lines.Count > 0
                ? "Opportunity: " + o.Lines[0].Text + (o.Lines.Count > 1 ? $" ({o.Lines.Count} lethal options in all.)" : "")
                : $"No lethal strike on their {LeaderName(1 - me)} this activation ({o.CommandsChecked} orders checked).";
            var reserves = S.Units.Where(SliceRules.InReserve).ToList();
            _contracts.text = reserves.Count == 0 ? "" : "Contracts under way: " + string.Join("; ", reserves.Select(r =>
                $"{(r.Owner == me ? "your" : "their")} {r.Def.Name} enters at the {SliceRules.ContractLane(r)} entry {SliceRules.EntryHex(S.Map, r.Owner, SliceRules.ContractLane(r))} from round {System.Math.Max(S.Round + 1, SliceRules.ContractArrivalRound)} if the hex is clear")) + ".";
            _contracts.style.display = reserves.Count == 0 ? DisplayStyle.None : DisplayStyle.Flex;
            _doctrine.text = _doctrineOpen ? SliceRoster.DoctrineCard(S.Players[me].Faction) + " " + SliceRoster.DoctrineCard(S.Players[1 - me].Faction) +
                " Capital ships (flagships, battleships, bastions) cannot fire their main battery into the 120° arc astern; turning in place uses the move." : "";
            _doctrine.style.display = _doctrineOpen ? DisplayStyle.Flex : DisplayStyle.None;
            _ai.text = _b.AiThinking ? (_b.Delegated && S.ActiveSeat == me ? "The delegate is considering your order." : "The opponent is considering its order.")
                     : string.IsNullOrEmpty(_b.AiReason) ? "" : "Last AI decision: " + _b.AiReason + ".";
            if (_b.Training)
            {
                _lesson.style.display = DisplayStyle.Flex;
                _lessonText.text = PassageScenario.TrainingSteps[_lesson_];
                _lessonNext.SetEnabled(_lesson_ < PassageScenario.TrainingSteps.Length - 1);
            }
            else _lesson.style.display = DisplayStyle.None;
        }

        void UpdateWorldLabels(Camera cam)
        {
            var S = _b.S;
            var items = new List<(Vector3 pos, string text, Color col)>();
            foreach (var u in S.Units.Where(x => x.OnBoard && SliceRules.Heat(x) > 0 && _b.Visible(x)))
            {
                var p = _b.ViewPosition(u.Id);
                if (p.HasValue) items.Add((p.Value + Vector3.up * 1.0f, SliceRules.Overheated(u) ? $"OVERHEATED {SliceRules.Heat(u)}" : $"heat {SliceRules.Heat(u)}",
                    SliceRules.Overheated(u) ? new Color(1f, 0.4f, 0.3f) : new Color(1f, 0.65f, 0.3f)));
            }
            foreach (var (hex, text) in _b.EntryLabels())
                items.Add((_b.transform.TransformPoint(BattlePresentation.World(hex)) + Vector3.up * 0.6f, "Contract: " + text, Look.GoldBright));
            while (_worldLabels.Count < items.Count)
            {
                var l = new Label(); l.pickingMode = PickingMode.Ignore; l.style.position = Position.Absolute; l.style.fontSize = 12;
                l.style.unityFontStyleAndWeight = FontStyle.Bold; l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.width = 220;
                _labels.Add(l); _worldLabels.Add(l);
            }
            var panel = Root.panel;
            for (int i = 0; i < _worldLabels.Count; i++)
            {
                var l = _worldLabels[i];
                if (i >= items.Count || panel == null || cam == null) { l.style.display = DisplayStyle.None; continue; }
                var sp = cam.WorldToScreenPoint(items[i].pos);
                if (sp.z <= 0) { l.style.display = DisplayStyle.None; continue; }
                var pp = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
                l.style.display = DisplayStyle.Flex;
                l.text = items[i].text; l.style.color = items[i].col;
                l.style.left = pp.x - 110; l.style.top = pp.y - 10;
            }
        }
    }
}
