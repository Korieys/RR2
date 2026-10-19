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
        readonly Button _overclock, _turn, _lessonNext;
        readonly List<Label> _worldLabels = new List<Label>();
        int _lesson_ = 0, _sig = -1;
        bool _doctrineOpen;

        static VisualElement Box(float width)
        {
            var v = new VisualElement();
            v.style.backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.88f);
            v.style.borderTopWidth = v.style.borderBottomWidth = v.style.borderLeftWidth = v.style.borderRightWidth = 1;
            var edge = new Color(0.79f, 0.64f, 0.36f, 0.5f);
            v.style.borderTopColor = v.style.borderBottomColor = v.style.borderLeftColor = v.style.borderRightColor = edge;
            v.style.paddingLeft = v.style.paddingRight = 10; v.style.paddingTop = v.style.paddingBottom = 8;
            v.style.width = width;
            return v;
        }

        static Label Line(string text, int size = 13, Color? color = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.whiteSpace = WhiteSpace.Normal; l.style.color = color ?? new Color(0.92f, 0.9f, 0.84f);
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.marginBottom = 4;
            return l;
        }

        public SliceBattlePanel(BattlePresentation b, VisualElement parent)
        {
            _b = b;
            Root = new VisualElement(); Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute; Root.style.left = 0; Root.style.right = 0; Root.style.top = 0; Root.style.bottom = 0;
            parent.Add(Root);
            _labels = new VisualElement(); _labels.pickingMode = PickingMode.Ignore;
            _labels.style.position = Position.Absolute; _labels.style.left = 0; _labels.style.right = 0; _labels.style.top = 0; _labels.style.bottom = 0;
            Root.Add(_labels);

            _panel = Box(380);
            _panel.style.position = Position.Absolute; _panel.style.right = 16; _panel.style.bottom = 96;
            Root.Add(_panel);
            _panel.Add(Line("COMMAND THREAT", 12, new Color(0.91f, 0.78f, 0.47f), true));
            _threat = Line(""); _panel.Add(_threat);
            _exposure = Line("", 12, new Color(0.75f, 0.77f, 0.8f)); _panel.Add(_exposure);
            _opportunity = Line("", 13, new Color(0.65f, 0.85f, 1f)); _panel.Add(_opportunity);
            _contracts = Line("", 12, new Color(0.91f, 0.78f, 0.47f)); _panel.Add(_contracts);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; _panel.Add(row);
            _overclock = UIX.Btn("Overclock (O)", () => _b.ToggleOverclock()); row.Add(_overclock);
            _turn = UIX.Btn("Turn (R)", () => _b.ToggleTurn()); row.Add(_turn);
            row.Add(UIX.Btn("Doctrine", () => { _doctrineOpen = !_doctrineOpen; _sig = -1; }));
            _doctrine = Line("", 12, new Color(0.8f, 0.82f, 0.86f)); _panel.Add(_doctrine);
            _ai = Line("", 11, new Color(0.6f, 0.62f, 0.66f)); _panel.Add(_ai);

            _lesson = Box(520);
            _lesson.style.position = Position.Absolute; _lesson.style.left = 300; _lesson.style.top = 150;
            _lessonText = Line("", 14);
            _lesson.Add(Line("TRAINING", 12, new Color(0.91f, 0.78f, 0.47f), true));
            _lesson.Add(_lessonText);
            _lessonNext = UIX.Btn("Next lesson", () => { _lesson_ = Mathf.Min(_lesson_ + 1, PassageScenario.TrainingSteps.Length - 1); _sig = -1; });
            _lesson.Add(_lessonNext);
            Root.Add(_lesson);
        }

        public void Dispose() { Root.RemoveFromHierarchy(); }

        string LeaderName(int seat) { var l = _b.S.Commander(seat); return l != null ? l.Def.Name : "the flagship"; }

        public void Update(Camera cam)
        {
            var S = _b.S;
            int me = _b.Setup.PlayerSeat;
            var u = _b.Selected != 0 ? S.Unit(_b.Selected) : null;
            bool canOc = u != null && SliceRules.CanOverclock(S, u) == null;
            _overclock.style.display = u != null && SliceRules.OverclockCapable(u) ? DisplayStyle.Flex : DisplayStyle.None;
            _overclock.SetEnabled(_b.PlayersTurn && (canOc || _b.OverclockMode));
            _overclock.text = _b.OverclockMode ? "Cancel overclock (O)" : u != null && SliceRules.OverclockCapable(u) ? $"Overclock (O): heat {SliceRules.Heat(u)}/{SliceRules.OverheatAt}" : "Overclock (O)";
            _turn.style.display = u != null && SliceRules.HasArc(u) ? DisplayStyle.Flex : DisplayStyle.None;
            _turn.SetEnabled(_b.PlayersTurn);
            _turn.text = _b.TurnMode ? "Cancel turn (R)" : "Turn (R)";

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
            _threat.style.color = t != null && t.Lines.Count > 0 ? new Color(1f, 0.55f, 0.45f) : new Color(0.92f, 0.9f, 0.84f);
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
            _ai.text = _b.AiThinking ? "The opponent is considering its order." : string.IsNullOrEmpty(_b.AiReason) ? "" : "Opponent's last decision: " + _b.AiReason + ".";
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
            foreach (var u in S.Units.Where(x => x.OnBoard && SliceRules.Heat(x) > 0))
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
