using System;
using System.Collections.Generic;
using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // The strategic interface: status strip, outliner, contextual inspector, order feedback and world labels.
    // Every value is read from GalaxyState; every action goes through GameRoot to StrategyRules.
    public sealed class StrategyHud
    {
        readonly GameRoot _root;
        public VisualElement Root { get; private set; }
        VisualElement _top, _outliner, _inspector, _ticker, _toasts, _labels;
        Label _hint;
        ScrollView _insp;
        readonly Dictionary<IconKind, Label> _res = new Dictionary<IconKind, Label>();
        readonly Dictionary<IconKind, Label> _inc = new Dictionary<IconKind, Label>();
        Label _date, _speedLabel, _playerName;
        Button _pause, _s1, _s2, _s3, _overlayBtn;
        readonly List<(VisualElement label, Func<Vector3?> pos, int priority, float minD, float maxD)> _worldLabels = new List<(VisualElement, Func<Vector3?>, int, float, float)>();
        GalaxyState G => _root.G;

        public StrategyHud(GameRoot root, VisualElement parent)
        {
            _root = root;
            Root = UIX.Div("layer");
            Root.pickingMode = PickingMode.Ignore;
            parent.Add(Root);
            _labels = UIX.Div("layer"); _labels.pickingMode = PickingMode.Ignore; Root.Add(_labels);
            BuildTop();
            _outliner = UIX.Div("panel", "outliner"); Root.Add(_outliner);
            _inspector = UIX.Div("panel", "inspector"); Root.Add(_inspector);
            _insp = new ScrollView(ScrollViewMode.Vertical);
            _insp.style.flexShrink = 1;
            _inspector.Add(_insp);
            _ticker = UIX.Div("ticker"); _ticker.pickingMode = PickingMode.Ignore; Root.Add(_ticker);
            _toasts = UIX.Div(); _toasts.style.position = Position.Absolute; _toasts.style.left = 300; _toasts.style.right = 390; _toasts.style.top = 60;
            _toasts.style.alignItems = Align.Center; _toasts.pickingMode = PickingMode.Ignore; Root.Add(_toasts);
            _hint = UIX.Text("", "panel", "body"); _hint.style.position = Position.Absolute; _hint.style.bottom = 64; _hint.style.alignSelf = Align.Center;
            _hint.style.left = new Length(50, LengthUnit.Percent); _hint.style.translate = new Translate(new Length(-50, LengthUnit.Percent), 0);
            _hint.style.display = DisplayStyle.None; _hint.pickingMode = PickingMode.Ignore; Root.Add(_hint);
        }

        // ------------------------------------------------------------------ status strip

        void BuildTop()
        {
            _top = UIX.Div("topbar");
            Root.Add(_top);
            var crest = new Icon(IconKind.Crown, Look.Gold, 20);
            _top.Add(crest);
            var name = UIX.Text(G.Player.Name.ToUpperInvariant(), "emblem-name");
            UIRoot.Display(name);
            _top.Add(name);
            _playerName = name;
            foreach (var (k, col, tip) in new[] { (IconKind.Energy, new Color(1f, 0.85f, 0.4f), "Energy"), (IconKind.Materials, new Color(0.7f, 0.82f, 0.95f), "Materials"),
                         (IconKind.Credits, new Color(0.95f, 0.78f, 0.45f), "Credits"), (IconKind.Influence, new Color(0.78f, 0.65f, 1f), "Influence") })
            {
                var box = UIX.Div("res");
                box.tooltip = tip;
                box.Add(new Icon(k, col, 16));
                var v = UIX.Text("0", "res-value"); _res[k] = v; box.Add(v);
                var inc = UIX.Text("", "res-income"); _inc[k] = inc; box.Add(inc);
                _top.Add(box);
            }
            _top.Add(UIX.Div("spacer"));
            _pause = UIX.IconBtn(IconKind.Pause, () => _root.TogglePause(), "Pause or resume time (Space)");
            _s1 = UIX.IconBtn(IconKind.Play, () => _root.SetSpeed(1), "Normal speed (1)");
            _s2 = UIX.IconBtn(IconKind.Fast, () => _root.SetSpeed(2), "Fast (2)");
            _s3 = UIX.IconBtn(IconKind.Faster, () => _root.SetSpeed(3), "Fastest (3)");
            _date = UIX.Text("", "date");
            _top.Add(_pause); _top.Add(_s1); _top.Add(_s2); _top.Add(_s3); _top.Add(_date);
            _speedLabel = UIX.Text("", "small"); _speedLabel.style.width = 70; _top.Add(_speedLabel);
            _top.Add(UIX.Div("spacer"));
            _overlayBtn = UIX.IconBtn(IconKind.Layers, () => _root.ToggleOverlays(), "Strategic overlay: labels, lanes and borders (Tab)");
            _top.Add(_overlayBtn);
            _top.Add(UIX.IconBtn(IconKind.Save, () => _root.SaveGame(), "Save (F5)"));
            _top.Add(UIX.IconBtn(IconKind.Load, () => _root.LoadGame(), "Load the last save (F9)"));
            _top.Add(UIX.IconBtn(IconKind.Gear, () => _root.ShowSettings(), "Settings"));
        }

        public void RefreshTop()
        {
            var st = G.Player.Stock;
            var inc = StrategyRules.DailyIncome(G, G.Player.Id);
            string who = G.Player.Name.ToUpperInvariant();
            if (_playerName.text != who) _playerName.text = who;
            _res[IconKind.Energy].text = st.Energy.ToString(); _inc[IconKind.Energy].text = "+" + inc.Energy;
            _res[IconKind.Materials].text = st.Materials.ToString(); _inc[IconKind.Materials].text = "+" + inc.Materials;
            _res[IconKind.Credits].text = st.Credits.ToString(); _inc[IconKind.Credits].text = "+" + inc.Credits;
            _res[IconKind.Influence].text = st.Influence.ToString(); _inc[IconKind.Influence].text = "";
            _date.text = GalaxyState.DateLabel(G.Day);
            var clock = _root.Clock;
            bool waiting = G.PendingEngagement != null;
            _speedLabel.text = waiting ? "Waiting" : clock.Paused ? "Paused" : clock.Speed == 1 ? "Normal" : clock.Speed == 2 ? "Fast" : "Fastest";
            Toggle(_pause, clock.Paused); Toggle(_s1, !clock.Paused && clock.Speed == 1); Toggle(_s2, !clock.Paused && clock.Speed == 2); Toggle(_s3, !clock.Paused && clock.Speed == 3);
            Toggle(_overlayBtn, Settings.Overlays);
        }

        static void Toggle(VisualElement b, bool on) { if (on) b.AddToClassList("btn-toggle-on"); else b.RemoveFromClassList("btn-toggle-on"); }

        // ------------------------------------------------------------------ outliner

        public void RefreshOutliner()
        {
            _outliner.Clear();
            _outliner.Add(Section("FLEETS"));
            foreach (var f in G.Fleets.Where(f => f.OwnerId == G.Player.Id && f.Ships.Count > 0))
                _outliner.Add(FleetItem(f));
            var threats = G.Fleets.Where(f => f.OwnerId != G.Player.Id && f.Ships.Count > 0 && (f.Moving || G.GetSystem(f.SystemId).OwnerId == G.Player.Id || f.Id == SliceScenario.RaidFleet)).ToList();
            if (threats.Count > 0)
            {
                _outliner.Add(Section("KNOWN THREATS"));
                foreach (var f in threats) _outliner.Add(FleetItem(f));
            }
            _outliner.Add(Section("KEY SYSTEMS"));
            var keys = G.Slice != null && G.Slice.Active
                ? new[] { SliceCampaign.HomeOf(G.Player.Id), SliceCampaign.PassageSystem, Expeditions.System, SliceCampaign.HomeOf(G.Player.Id == "pygmaei" ? "inhumanus" : "pygmaei") }
                    .Select(G.GetSystem).Where(s => s != null)
                : G.Systems.Where(s => s.Detail != SystemDetail.Distant);
            foreach (var s in keys)
            {
                var it = Item(s.Name, SystemStatus(s), _root.Selection.SystemId == s.Id && _root.Selection.FleetId == null && _root.Selection.PlanetId == null);
                it.RegisterCallback<ClickEvent>(e => _root.SelectAndFocusSystem(s.Id));
                _outliner.Add(it);
            }
            var eng = G.PendingEngagement;
            if (eng != null)
            {
                _outliner.Add(Section("DECISION REQUIRED"));
                var it = Item("Contact at " + G.GetSystem(eng.SystemId).Name, "Prepare your battle line or withdraw.", true);
                it.Q<Label>(className: "item-title").style.color = Look.Danger;
                it.RegisterCallback<ClickEvent>(e => _root.OpenEngagement());
                _outliner.Add(it);
            }
        }

        VisualElement FleetItem(Fleet f)
        {
            string status = FleetStatus(f);
            var it = Item(f.Name, status, _root.Selection.FleetId == f.Id);
            var title = it.Q<Label>(className: "item-title");
            title.style.color = Look.PolityColor(G, f.OwnerId) * 0.6f + Look.Ivory * 0.4f;
            it.RegisterCallback<ClickEvent>(e => _root.SelectAndFocusFleet(f.Id));
            return it;
        }

        public string FleetStatus(Fleet f)
        {
            int ships = f.Ships.Count;
            string where = G.GetSystem(f.SystemId).Name;
            if (f.EngagementId != null) return $"{ships} ships, engaged at {where}.";
            if (f.Moving)
            {
                int eta = EtaDays(f);
                return $"{ships} ships, bound for {G.GetSystem(f.Order.Path.Last()).Name} ({eta} days).";
            }
            return $"{ships} ships, holding at {where}.";
        }

        public int EtaDays(Fleet f)
        {
            if (!f.Moving) return 0;
            int days = 0; string at = f.SystemId;
            for (int i = 0; i < f.Order.Path.Count; i++) { days += G.LaneBetween(at, f.Order.Path[i]).Days; at = f.Order.Path[i]; }
            return days - f.Order.DaysIntoLane;
        }

        string SystemStatus(StarSystem s)
        {
            var owner = s.OwnerId != null ? G.GetPolity(s.OwnerId).Name : "Unclaimed";
            if (s.Blockaded) return owner + ". Orbit held by " + G.GetPolity(s.OrbitalControlId).Name + ".";
            return owner + ".";
        }

        static VisualElement Section(string t) => UIX.Text(t, "h-section");

        static VisualElement Item(string title, string sub, bool selected)
        {
            var it = UIX.Div("item");
            if (selected) it.AddToClassList("item-selected");
            it.Add(UIX.Text(title, "item-title"));
            if (!string.IsNullOrEmpty(sub)) it.Add(UIX.Text(sub, "item-sub"));
            return it;
        }

        // ------------------------------------------------------------------ inspector

        public void RefreshInspector()
        {
            _insp.Clear();
            var sel = _root.Selection;
            if (sel.FleetId != null && G.GetFleet(sel.FleetId) != null) FleetInspector(G.GetFleet(sel.FleetId));
            else if (sel.PlanetId != null && G.GetPlanet(sel.PlanetId) != null) PlanetInspector(G.GetPlanet(sel.PlanetId));
            else if (sel.SystemId != null) SystemInspector(G.GetSystem(sel.SystemId));
            _inspector.style.display = _insp.contentContainer.childCount > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void Title(string name, string sub, Color accent)
        {
            var t = UIX.Text(name.ToUpperInvariant(), "h-display");
            UIRoot.Display(t);
            _insp.Add(t);
            var bar = UIX.Div(); bar.style.height = 2; bar.style.width = 64; bar.style.backgroundColor = accent; bar.style.marginTop = 3; bar.style.marginBottom = 6;
            _insp.Add(bar);
            if (sub != null) _insp.Add(UIX.Text(sub, "subtitle"));
        }

        void KV(string k, string v, Color? vc = null)
        {
            var r = UIX.Div("row");
            r.style.marginTop = 2;
            r.Add(UIX.Text(k, "small"));
            r.Add(UIX.Div("spacer"));
            var l = UIX.Text(v, "body");
            if (vc.HasValue) l.style.color = vc.Value;
            r.Add(l);
            _insp.Add(r);
        }

        void SystemInspector(StarSystem s)
        {
            var sector = s.SectorId != null ? G.GetSector(s.SectorId) : null;
            Title(s.Name, (sector != null ? sector.Name : "Unclaimed space") + ", " + StarText(s.Star) + (s.ProvisionalName ? " (provisional name)" : ""),
                s.OwnerId != null ? Look.PolityColor(G, s.OwnerId) : Look.Muted);
            KV("Sovereign", s.OwnerId != null ? G.GetPolity(s.OwnerId).Name : "Unclaimed");
            if (s.OrbitalControlId != s.OwnerId && s.OrbitalControlId != null)
                KV("Orbit held by", G.GetPolity(s.OrbitalControlId).Name, Look.Danger);
            if (s.Conquest != ConquestStage.None && s.ConquestBy != null)
                KV("Conquest", ConquestText(s), Look.Danger);
            if (sector != null && sector.Eclipse)
                _insp.Add(UIX.Text("The Eclipse Sector swallows light. Few have charted it and fewer have returned to speak of it.", "subtitle"));
            _insp.Add(Section("WORLDS"));
            foreach (var p in s.Planets)
            {
                var it = Item(p.Name, KindText(p.Kind) + (p.OwnerId != null ? ", " + G.GetPolity(p.OwnerId).Name : ""), false);
                it.RegisterCallback<ClickEvent>(e => _root.InspectPlanet(p.Id));
                _insp.Add(it);
            }
            var here = G.Fleets.Where(f => f.SystemId == s.Id && f.Ships.Count > 0 && !f.Moving).ToList();
            if (here.Count > 0)
            {
                _insp.Add(Section("FLEETS PRESENT"));
                foreach (var f in here) _insp.Add(FleetItem(f));
            }
            _insp.Add(UIX.Div("divider"));
            if (_root.Scale == ViewScale.Galaxy)
                _insp.Add(UIX.Btn("Enter system", () => _root.EnterSystem(s.Id), "btn-primary"));
            else
                _insp.Add(UIX.Btn("Return to the galaxy", () => _root.ExitToGalaxy()));
        }

        void PlanetInspector(Planet p)
        {
            var s = G.SystemOf(p);
            Title(p.Name, KindText(p.Kind) + " in the " + s.Name + " system" + (p.ProvisionalName ? " (provisional name)" : ""),
                p.OwnerId != null ? Look.PolityColor(G, p.OwnerId) : Look.Muted);
            KV("Ruled by", p.OwnerId != null ? G.GetPolity(p.OwnerId).Name : "No one");
            if (p.Population > 0) KV("Population", UIX.Thousands(p.Population));
            if (p.OwnerId != null)
            {
                KV("Development", p.Development + " of 5");
                var y = p.DailyYield;
                KV("Daily yield", $"{y.Energy} energy, {y.Materials + p.Foundries * ShipCatalog.FoundryYield.Materials} materials, {y.Credits} credits");
                if (p.Foundries > 0) KV("Crystal Foundries", p.Foundries.ToString());
                if (s.Blockaded) _insp.Add(UIX.Text("Enemy ships hold the orbit. This world produces nothing and its shipyard is idle until the orbit is retaken.", "subtitle"));
            }
            if (p.Project != null)
            {
                _insp.Add(Section("CURRENT PROJECT"));
                string label = p.Project.Kind == ProjectKind.CommissionShip ? "Commissioning " + ShipCatalog.Name(p.Project.ShipDefId) : "Raising a Crystal Foundry";
                _insp.Add(UIX.Text(label, "body"));
                _insp.Add(UIX.Bar(p.Project.DaysDone / (float)p.Project.DaysTotal));
                _insp.Add(UIX.Text($"{p.Project.DaysTotal - p.Project.DaysDone} days remaining.", "small"));
                if (p.OwnerId == G.Player.Id) _insp.Add(UIX.Btn("Cancel the project (half of the cost is recovered)", () => _root.CancelProject(p.Id), "btn-danger"));
            }
            var options = StrategyRules.ProjectOptions(G, p.Id);
            if (options.Count > 0 && p.OwnerId == G.Player.Id)
            {
                _insp.Add(Section(p.HasShipyard ? "SHIPYARD AND DEVELOPMENT" : "DEVELOPMENT"));
                foreach (var o in options) _insp.Add(ProjectRow(p, o));
            }
            _insp.Add(UIX.Div("divider"));
            if (_root.Scale == ViewScale.Planet) _insp.Add(UIX.Btn("Return to the system", () => _root.ExitPlanet()));
            else if (_root.Scale == ViewScale.System) _insp.Add(UIX.Btn("Inspect this world", () => _root.InspectPlanet(p.Id), "btn-primary"));
        }

        VisualElement ProjectRow(Planet p, ProjectOption o)
        {
            var box = UIX.Div("item");
            var top = UIX.Div("row");
            top.Add(UIX.Text(o.Label, "item-title"));
            top.Add(UIX.Div("spacer"));
            top.Add(UIX.Text(o.Days + " days", "small"));
            box.Add(top);
            var cost = UIX.Div("row");
            void C(IconKind k, int v, Color col)
            {
                if (v <= 0) return;
                var ic = new Icon(k, col, 12); ic.style.marginRight = 3; cost.Add(ic);
                var l = UIX.Text(v.ToString(), "small"); l.style.marginRight = 10; cost.Add(l);
            }
            C(IconKind.Energy, o.Cost.Energy, new Color(1f, 0.85f, 0.4f));
            C(IconKind.Materials, o.Cost.Materials, new Color(0.7f, 0.82f, 0.95f));
            C(IconKind.Credits, o.Cost.Credits, new Color(0.95f, 0.78f, 0.45f));
            cost.Add(UIX.Div("spacer"));
            var b = UIX.Btn(o.Kind == ProjectKind.CommissionShip ? "Commission" : "Build", () => _root.StartProject(p.Id, o.Kind, o.ShipDefId));
            b.SetEnabled(o.Available);
            cost.Add(b);
            box.Add(cost);
            if (o.Kind == ProjectKind.CommissionShip)
            {
                var d = ShipCatalog.Def(o.ShipDefId);
                box.Add(UIX.Text($"{RoleText(d)}, tier {d.Roster.Tier}. Hull {d.MaxHp}, armor {d.Armor}, battle cost {ShipCatalog.Cost(o.ShipDefId)}. {d.Roster.ActiveName}: {d.Roster.ActiveText}", "item-sub"));
            }
            else box.Add(UIX.Text($"Adds {ShipCatalog.FoundryYield.Materials} materials per day once complete.", "item-sub"));
            if (!o.Available && o.UnavailableReason != null)
            {
                var why = UIX.Text(o.UnavailableReason, "item-sub");
                why.style.color = new Color(0.95f, 0.7f, 0.65f);
                box.Add(why);
            }
            return box;
        }

        void FleetInspector(Fleet f)
        {
            bool mine = f.OwnerId == G.Player.Id;
            Title(f.Name, G.GetPolity(f.OwnerId).Name + (mine ? "" : ", hostile"), Look.PolityColor(G, f.OwnerId));
            _insp.Add(UIX.Text(FleetStatus(f), "body"));
            int cost = f.Ships.Sum(s => ShipCatalog.Cost(s.DefId));
            KV("Ships", f.Ships.Count.ToString());
            KV("Total battle cost", cost + " (a battle line may field up to " + MatchConfigBudget + ")");
            if (!StrategyRules.CanFight(f)) _insp.Add(UIX.Text("This fleet has no flagship. It cannot form a battle line and will withdraw from any contact.", "subtitle"));
            _insp.Add(Section("COMPOSITION"));
            foreach (var s in f.Ships.OrderByDescending(s => ShipCatalog.IsLeader(s.DefId)).ThenByDescending(s => ShipCatalog.Tier(s.DefId)))
            {
                var d = ShipCatalog.Def(s.DefId);
                var it = UIX.Div("item");
                var row = UIX.Div("row");
                if (ShipCatalog.IsLeader(s.DefId)) { var c = new Icon(IconKind.Crown, Look.GoldBright, 13); c.style.marginRight = 5; row.Add(c); }
                row.Add(UIX.Text(d.Name, "item-title"));
                row.Add(UIX.Div("spacer"));
                row.Add(UIX.Text($"{s.Hp}/{d.MaxHp}", "small"));
                it.Add(row);
                float hp = s.Hp / (float)d.MaxHp;
                it.Add(UIX.Bar(hp, hp < 0.4f ? "bar-hp-low" : "bar-hp"));
                it.Add(UIX.Text($"{RoleText(d)}, tier {d.Roster.Tier}, moves {MoveText(d)}.", "item-sub"));
                _insp.Add(it);
            }
            if (mine)
            {
                _insp.Add(UIX.Div("divider"));
                var row = UIX.Div("row");
                var mv = UIX.Btn(_root.Targeting ? "Choose a destination" : "Order a move", () => _root.BeginMoveTargeting(), "btn-primary");
                mv.SetEnabled(f.EngagementId == null && G.PendingEngagement == null);
                row.Add(mv);
                if (f.Moving) row.Add(UIX.Btn("Hold position", () => _root.CancelMove(f.Id)));
                _insp.Add(row);
                _insp.Add(UIX.Text("Right-click a star system on the galaxy map to order this fleet there.", "small"));
            }
        }

        public const int MatchConfigBudget = 30;

        public static string RoleText(Commandfall.Core.UnitDef d)
        {
            var s = d.Roster.Role.ToString();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < s.Length; i++) { if (i > 0 && char.IsUpper(s[i])) sb.Append(' '); sb.Append(i > 0 && char.IsUpper(s[i]) ? char.ToLowerInvariant(s[i]) : s[i]); }
            return sb.ToString();
        }

        public static string MoveText(Commandfall.Core.UnitDef d)
        {
            var r = d.Roster;
            string hx = r.MoveHexes == 1 ? "1 hex" : r.MoveHexes + " hexes";
            switch (r.Pattern)
            {
                case Commandfall.Core.MovePattern.Ray: return $"up to {hx} in a straight line";
                case Commandfall.Core.MovePattern.Leap: return $"by jumping up to {hx}";
                case Commandfall.Core.MovePattern.Knight: return "in a knight's arc";
                case Commandfall.Core.MovePattern.Diag: return $"diagonally up to {hx}";
                case Commandfall.Core.MovePattern.Zigzag: return $"in a zigzag up to {hx}";
                default: return $"up to {hx}";
            }
        }

        static string StarText(StarClass c) => c == StarClass.BlueGiant ? "a blue giant" : c == StarClass.White ? "a white star" : c == StarClass.Yellow ? "a yellow star"
            : c == StarClass.Orange ? "an orange star" : c == StarClass.RedDwarf ? "a red dwarf" : c == StarClass.Violet ? "a violet star" : "a dim, failing star";

        public static string KindText(PlanetKind k) => k == PlanetKind.GasGiant ? "Gas giant" : k == PlanetKind.Crystal ? "Crystal world" : k == PlanetKind.Toxic ? "Bio-forged world" : k + " world";

        string ConquestText(StarSystem s)
        {
            var by = G.GetPolity(s.ConquestBy).Name;
            return s.Conquest == ConquestStage.OrbitSecured ? by + " holds orbit (1 of 3: orbit, landing, ground)" : by + ", stage " + (int)s.Conquest + " of 3";
        }

        // ------------------------------------------------------------------ feedback

        public void Toast(string text, string kind = "ok")
        {
            var t = UIX.Text(text, "panel", "toast", kind == "bad" ? "toast-bad" : kind == "danger" ? "toast-danger" : "toast-ok");
            t.pickingMode = PickingMode.Ignore;
            _toasts.Add(t);
            while (_toasts.childCount > 3) _toasts.RemoveAt(0);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(kind == "bad" ? 4200 : 3400);
        }

        public void Hint(string text)
        {
            _hint.text = text ?? "";
            _hint.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void RefreshTicker()
        {
            _ticker.Clear();
            foreach (var e in G.Log.Skip(Math.Max(0, G.Log.Count - 3)))
            {
                var l = UIX.Text(GalaxyState.DateLabel(e.Day) + ".  " + e.Text, "ticker-line");
                if (e.Kind == "danger") l.style.color = new Color(0.95f, 0.72f, 0.66f);
                if (e.Kind == "result") l.style.color = Look.GoldBright;
                _ticker.Add(l);
            }
        }

        public void RefreshAll() { RefreshTop(); RefreshOutliner(); RefreshInspector(); RefreshTicker(); }

        // ------------------------------------------------------------------ world labels

        public void ClearLabels() { _labels.Clear(); _worldLabels.Clear(); }

        public void AddLabel(string text, Func<Vector3?> pos, int priority, float minDist, float maxDist, params string[] classes)
        {
            var l = UIX.Text(text, "label");
            foreach (var c in classes) l.AddToClassList(c);
            l.pickingMode = PickingMode.Ignore;
            if (classes.Contains("label-sector")) UIRoot.Display(l);
            _labels.Add(l);
            _worldLabels.Add((l, pos, priority, minDist, maxDist));
        }

        /// <summary>A faction emblem anchored in the world, placed and decluttered like a label.</summary>
        public void AddEmblem(Texture2D tex, Func<Vector3?> pos, int priority, float minDist, float maxDist, float sizePx)
        {
            var e = new VisualElement();
            e.AddToClassList("label");
            e.AddToClassList("label-emblem");
            e.style.width = sizePx * tex.width / Mathf.Max(1f, tex.height);
            e.style.height = sizePx;
            e.style.backgroundImage = new StyleBackground(tex);
            e.pickingMode = PickingMode.Ignore;
            _labels.Add(e);
            _worldLabels.Add((e, pos, priority, minDist, maxDist));
        }

        readonly List<Rect> _placed = new List<Rect>();

        public void UpdateLabels(Camera cam, bool visible)
        {
            _labels.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (!visible) return;
            float dist = _root.Rig.Current.Distance;
            var panel = Root.panel;
            if (panel == null) return;
            _placed.Clear();
            // The HUD panels are translucent, so a label behind one would show through: treat visible panels as occupied.
            foreach (var panelEl in new[] { _top, _outliner, _inspector })
                if (panelEl != null && panelEl.resolvedStyle.display != DisplayStyle.None && panelEl.worldBound.width > 0) _placed.Add(panelEl.worldBound);
            // Highest priority first; anything overlapping an already placed label is hidden (semantic declutter).
            foreach (var w in _worldLabels.OrderByDescending(x => x.priority))
            {
                var p = w.pos();
                bool show = p.HasValue && dist >= w.minD && dist <= w.maxD;
                Vector3 sp = Vector3.zero;
                if (show)
                {
                    sp = cam.WorldToScreenPoint(p.Value);
                    show = sp.z > 0 && sp.x > -50 && sp.x < cam.pixelWidth + 50 && sp.y > -50 && sp.y < cam.pixelHeight + 50;
                }
                if (show)
                {
                    var pp = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
                    float wdt = Mathf.Max(40, w.label.resolvedStyle.width), hgt = Mathf.Max(14, w.label.resolvedStyle.height);
                    var rect = new Rect(pp.x - wdt / 2, pp.y - hgt / 2, wdt, hgt);
                    if (_placed.Any(r => r.Overlaps(rect))) show = false;
                    else
                    {
                        _placed.Add(rect);
                        w.label.style.left = rect.x; w.label.style.top = rect.y;
                    }
                }
                w.label.style.visibility = show ? Visibility.Visible : Visibility.Hidden;
            }
        }
    }
}