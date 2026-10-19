using System;
using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // The passage campaign's strategic panel (Prompts 6 to 10), bottom-left in the galaxy and system views: the passage and
    // its access, the political situation with cause labels, the declared objective, a pending encounter's stakes, the
    // flagship and its hangar, wrecks and salvage, recent history, and the Eclipse expedition. Also world labels for the
    // passage, wrecks and the expedition. It reads authoritative state and calls GameRoot actions; it changes nothing itself.
    public sealed class CampaignPanel
    {
        readonly GameRoot _root;
        public VisualElement Root { get; private set; }
        readonly VisualElement _body, _labels;
        readonly List<Label> _world = new List<Label>();
        string _sig;

        public CampaignPanel(GameRoot root, VisualElement parent)
        {
            _root = root;
            Root = new VisualElement(); Root.pickingMode = PickingMode.Ignore;
            Root.style.position = Position.Absolute; Root.style.left = 0; Root.style.right = 0; Root.style.top = 0; Root.style.bottom = 0;
            parent.Add(Root);
            _labels = new VisualElement(); _labels.pickingMode = PickingMode.Ignore;
            _labels.style.position = Position.Absolute; _labels.style.left = 0; _labels.style.right = 0; _labels.style.top = 0; _labels.style.bottom = 0;
            Root.Add(_labels);
            _body = new VisualElement();
            _body.style.position = Position.Absolute; _body.style.left = 14; _body.style.bottom = 70; _body.style.width = 400;
            _body.style.backgroundColor = new Color(0.05f, 0.07f, 0.1f, 0.9f);
            _body.style.borderTopWidth = _body.style.borderBottomWidth = _body.style.borderLeftWidth = _body.style.borderRightWidth = 1;
            var edge = new Color(0.79f, 0.64f, 0.36f, 0.5f);
            _body.style.borderTopColor = _body.style.borderBottomColor = _body.style.borderLeftColor = _body.style.borderRightColor = edge;
            _body.style.paddingLeft = _body.style.paddingRight = 10; _body.style.paddingTop = _body.style.paddingBottom = 8;
            Root.Add(_body);
        }

        public void Dispose() { Root.RemoveFromHierarchy(); }
        int _version;
        bool _expanded;   // compact by default: the battlefield and the galaxy stay dominant; details on request
        /// <summary>Forces a rebuild after an action changed what the panel shows.</summary>
        public void Invalidate() { _version++; }

        static Label Line(string text, int size = 12, Color? col = null, bool bold = false)
        {
            var l = new Label(text);
            l.style.fontSize = size; l.style.whiteSpace = WhiteSpace.Normal; l.style.marginBottom = 3;
            l.style.color = col ?? new Color(0.9f, 0.88f, 0.82f);
            if (bold) l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        static Color Gold => new Color(0.91f, 0.78f, 0.47f);
        static Color Muted => new Color(0.66f, 0.68f, 0.72f);

        public void Update(Camera cam)
        {
            var g = _root.G;
            bool show = g.Slice != null && g.Slice.Active && _root.Scale != ViewScale.Battle && _root.Scale != ViewScale.Planet && !_root.UI.ModalOpen && !_root.Busy;
            Root.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (!show) return;
            var s = g.Slice;
            string sig = $"{g.Day}|{g.Log.Count}|{s.History.Count}|{s.Wrecks.Count(w => w.Salvaged)}|{g.Player.Stock}|{g.PendingEngagement?.Id}|{_root.Scale}|" +
                         $"{(_root.LocalSystem != null ? _root.LocalSystem.Data.Id : "-")}|{string.Join(",", g.Fleets.Select(f => f.SystemId + f.Ships.Count + (f.Moving ? "m" : "")))}|" +
                         $"{string.Join(",", s.Ships.Values.Select(x => x.Name + x.Modules.Count))}|{s.Objectives.Count}|{(Expeditions.Of(g)?.State.ToString() ?? "")}|{_version}|{_expanded}";
            if (sig != _sig) { _sig = sig; Rebuild(g, s); }
            Labels(g, s, cam);
        }

        void Btn(VisualElement row, string text, Action click, bool enabled = true, string tip = null)
        {
            var b = UIX.Btn(text, click);
            b.style.marginRight = 6; b.style.marginTop = 4;
            b.SetEnabled(enabled);
            if (tip != null) b.tooltip = tip;
            row.Add(b);
        }

        void Rebuild(GalaxyState g, SliceCampaignState s)
        {
            _body.Clear();
            var passage = s.Passages.FirstOrDefault();
            string me = g.Player.Id, them = me == "pygmaei" ? "inhumanus" : "pygmaei";
            _body.Add(Line("THE UMBRA PASSAGE", 12, Gold, true));
            if (passage != null)
                _body.Add(Line(passage.AccessHolderId == null ? "Access: contested. Neither power may use the passage freely."
                    : $"Access: {g.GetPolity(passage.AccessHolderId).Name} (+{passage.TollPerDay} credits a day to {(passage.AccessHolderId == me ? "you" : "them")}). The system itself stays unclaimed.", 12));
            var rel = s.RelationOf(me, them);
            if (rel != null && _expanded)
            {
                int op = rel.Opinion(g.Day);
                _body.Add(Line($"{g.GetPolity(them).Name}: {rel.Status}; standing {op:+0;-0;0}.", 12));
                foreach (var m in rel.Modifiers.Where(x => x.ExpiresDay > g.Day))
                    _body.Add(Line($"  {m.Amount:+0;-0}: {m.Cause} ({m.ExpiresDay - g.Day} days left)", 11, Muted));
            }
            var forecast = SliceCampaign.CounterOffensiveForecast(g);
            if (forecast != null) _body.Add(Line(forecast, 12, new Color(1f, 0.7f, 0.55f)));
            var obj = s.Objectives.LastOrDefault(o => o.PolityId == me);
            var row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap;
            if (obj == null || obj.Status == ObjectiveStatus.Lost)
            {
                _body.Add(Line(obj == null ? "Objective: none declared. Declaring war over the passage sends the other power's fleet there in two days."
                    : "Objective lost: " + obj.Note, 12, obj == null ? (Color?)null : new Color(1f, 0.6f, 0.5f)));
                Btn(row, "Declare war over the passage", () => _root.CampaignDeclare());
            }
            else _body.Add(Line($"Objective: secure access through the passage. {obj.Status}{(obj.Note != null ? ". " + obj.Note : ".")}", 12));
            var e = g.PendingEngagement;
            if (e != null)
            {
                _body.Add(Line("An encounter at " + g.GetSystem(e.SystemId).Name + " awaits your decision; its stakes are shown in the battle preparation.", 12, Gold));
                Btn(row, "Battle preparation", () => _root.OpenEngagement());
            }
            var flagRec = g.Fleets.Where(f => f.OwnerId == me).SelectMany(f => f.Ships).FirstOrDefault(x => SliceCampaign.IsCapital(x.DefId));
            if (flagRec != null && !_expanded) Btn(row, "Hangar", () => _root.OpenHangar(flagRec.Uid));
            else if (flagRec != null)
            {
                var prof = s.Profile(flagRec.Uid);
                _body.Add(Line($"Flagship: {SliceCampaign.ShipName(g, flagRec)} ({ShipCatalog.Name(flagRec.DefId)}), hull {flagRec.Hp} of {SliceCampaign.MaxHull(g, flagRec)}" +
                               (prof != null && prof.Modules.Count > 0 ? ", " + string.Join(", ", prof.Modules.Select(m => SliceRules.Module(m).Name)) : "") + ".", 12));
                Btn(row, "Hangar", () => _root.OpenHangar(flagRec.Uid));
            }
            else _body.Add(Line("Your flagship is lost. A shipyard can build a replacement (a new ship, not the old one).", 12, new Color(1f, 0.6f, 0.5f)));
            string here = _root.LocalSystem != null ? _root.LocalSystem.Data.Id : null;
            foreach (var w in s.Wrecks.Where(w => !w.Salvaged && (here == null || w.SystemId == here)).Where(_ => _expanded || here != null))
            {
                _body.Add(Line($"Wreck of {w.Name} at {g.GetSystem(w.SystemId).Name}. {SliceCampaign.SalvagePreview(g, w.Id)}", 11));
                var fleet = g.Fleets.FirstOrDefault(f => f.OwnerId == me && f.SystemId == w.SystemId);
                string why = fleet == null ? "Move a fleet to " + g.GetSystem(w.SystemId).Name + " to salvage." : SliceCampaign.SalvageProblem(g, w.Id, fleet.Id);
                Btn(row, "Salvage " + w.Name, () => _root.CampaignSalvage(w.Id), why == null, why);
            }
            var ex = Expeditions.Of(g);
            if (ex != null && _expanded)
            {
                _body.Add(Line("THE ECLIPSE EXPEDITION (optional)", 12, Gold, true));
                _body.Add(Line(ex.State == ExpeditionState.Secured || ex.State == ExpeditionState.Withdrawn || ex.State == ExpeditionState.Failed
                    ? ex.Outcome : $"{ex.Title}: {ex.State}. Hold a fleet at Gloam to begin.", 11));
                if (ex.State == ExpeditionState.Offered || ex.State == ExpeditionState.Scouted)
                    Btn(row, "Expedition briefing", () => _root.OpenExpedition());
            }
            if (ex != null && !_expanded && (ex.State == ExpeditionState.Offered || ex.State == ExpeditionState.Scouted)) Btn(row, "Expedition", () => _root.OpenExpedition());
            Btn(row, "History", () => _root.OpenHistory(here));
            Btn(row, _expanded ? "Less" : "Details", () => { _expanded = !_expanded; });
            _body.Add(row);
        }

        void Labels(GalaxyState g, SliceCampaignState s, Camera cam)
        {
            var items = new List<(Vector3 pos, string text, Color col)>();
            if (_root.Scale == ViewScale.Galaxy && _root.Galaxy != null)
            {
                Vector3 W(string id) => _root.Galaxy.transform.TransformPoint(GalaxyScale.ToWorld(g.GetSystem(id)));
                foreach (var p in s.Passages)
                    items.Add((W(p.SystemId) + Vector3.up * 6, p.AccessHolderId == null ? "Passage: contested" : "Passage: " + g.GetPolity(p.AccessHolderId).Name, Gold));
                foreach (var w in s.Wrecks.Where(w => !w.Salvaged))
                    items.Add((W(w.SystemId) - Vector3.up * 6, "Wreck: " + w.Name, new Color(0.9f, 0.6f, 0.45f)));
                var ex = Expeditions.Of(g);
                if (ex != null && (ex.State == ExpeditionState.Offered || ex.State == ExpeditionState.Scouted))
                    items.Add((W(ex.SystemId) + Vector3.up * 6, "Derelict signal", new Color(0.7f, 0.75f, 1f)));
            }
            while (_world.Count < items.Count)
            {
                var l = new Label(); l.pickingMode = PickingMode.Ignore; l.style.position = Position.Absolute; l.style.fontSize = 12;
                l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.width = 200; l.style.unityFontStyleAndWeight = FontStyle.Bold;
                _labels.Add(l); _world.Add(l);
            }
            var panel = Root.panel;
            for (int i = 0; i < _world.Count; i++)
            {
                var l = _world[i];
                if (i >= items.Count || panel == null || cam == null) { l.style.display = DisplayStyle.None; continue; }
                var sp = cam.WorldToScreenPoint(items[i].pos);
                if (sp.z <= 0) { l.style.display = DisplayStyle.None; continue; }
                var pp = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
                l.style.display = DisplayStyle.Flex; l.text = items[i].text; l.style.color = items[i].col;
                l.style.left = pp.x - 100; l.style.top = pp.y - 24;
            }
        }
    }
}
