using System;
using System.Collections.Generic;
using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Fleet Operations: fixed dossier, open viewport, fleet strip and contextual command dock.
    // Read-only presentation of GalaxyState. All commands retain their GameRoot / StrategyRules path.
    public sealed class StrategyHud
    {
        readonly GameRoot _root;
        public VisualElement Root { get; private set; }
        VisualElement _top, _outliner, _inspector, _ticker, _toasts, _labels, _dock;
        VisualElement _heading, _back, _actions, _context;
        Label _hint, _contextTitle, _contextMode;
        ScrollView _outScroll, _insp;
        string _tab = "Overview", _selectionKey, _projectKey, _motionKey;
        readonly Dictionary<IconKind, Label> _res = new Dictionary<IconKind, Label>();
        readonly Dictionary<IconKind, Label> _inc = new Dictionary<IconKind, Label>();
        Label _date, _speedLabel, _playerName;
        Button _pause, _s1, _s2, _s3, _overlayBtn;
        readonly List<(VisualElement label, Func<Vector3?> pos, int priority, float minD, float maxD)> _worldLabels = new List<(VisualElement, Func<Vector3?>, int, float, float)>();
        GalaxyState G => _root.G;

        public StrategyHud(GameRoot root, VisualElement parent)
        {
            _root = root;
            Root = UIX.Div("layer"); Root.pickingMode = PickingMode.Ignore; parent.Add(Root);
            _labels = UIX.Div("layer"); _labels.pickingMode = PickingMode.Ignore; Root.Add(_labels);
            BuildTop();
            _inspector = UIX.Div("panel", "inspector"); Root.Add(_inspector);
            _heading = UIX.Div("dossier-heading"); _inspector.Add(_heading);
            _insp = new ScrollView(ScrollViewMode.Vertical); _insp.style.flexGrow = 1; _insp.style.flexShrink = 1;
            _inspector.Add(_insp); UIRoot.StyleScroll(_insp);
            _back = UIX.Div("dossier-footer"); _inspector.Add(_back);
            _dock = UIX.Div("command-dock", "row"); Root.Add(_dock);
            _outliner = UIX.Div("outliner"); _dock.Add(_outliner);
            _outliner.Add(UiKit.Section("01", "Fleet operations"));
            _outScroll = new ScrollView(ScrollViewMode.Horizontal);
            _outScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _outScroll.style.flexGrow = 1; _outScroll.style.flexShrink = 1;
            _outliner.Add(_outScroll); UIRoot.StyleScroll(_outScroll);
            _actions = UIX.Div("inspector-actions"); _dock.Add(_actions);
            _context = UIX.Div("context-strip"); _context.pickingMode = PickingMode.Ignore;
            _contextMode = UIX.Text("GALAXY", "eyebrow");
            _contextTitle = UIX.Text("The known galaxy", "context-title");
            _context.Add(_contextMode); _context.Add(_contextTitle); Root.Add(_context);
            _ticker = UIX.Div("ticker"); Root.Add(_ticker);
            _toasts = UIX.Div("strategy-toasts"); _toasts.pickingMode = PickingMode.Ignore; Root.Add(_toasts);
            _hint = UIX.Text("", "panel", "body", "strategy-hint");
            _hint.style.display = DisplayStyle.None; _hint.pickingMode = PickingMode.Ignore; Root.Add(_hint);
            _dock.RegisterCallback<GeometryChangedEvent>(e => _hint.style.bottom = e.newRect.height + 16);
        }

        void BuildTop()
        {
            _top = UIX.Div("topbar"); Root.Add(_top);
            var identity = UIX.Btn("", OpenNavigation, "empire-identity");
            identity.tooltip = "Open the system directory and known contacts";
            identity.Add(UiKit.Crest(G.Player.Id));
            var words = UIX.Div(); _playerName = UIX.Text(G.Player.Name.ToUpperInvariant(), "emblem-name"); words.Add(_playerName);
            words.Add(UIX.Text("FLEET OPERATIONS  /  DIRECTORY", "identity-sub"));
            identity.Add(words); _top.Add(identity);
            foreach (var (k, col, tip) in new[] { (IconKind.Energy, new Color(1f, .85f, .4f), "Energy"),
                (IconKind.Materials, new Color(.7f, .82f, .95f), "Materials"), (IconKind.Credits, new Color(.95f, .78f, .45f), "Credits"),
                (IconKind.Influence, new Color(.78f, .65f, 1f), "Influence") })
            {
                var box = UIX.Div("res"); box.tooltip = tip + " stockpile and daily income"; box.Add(new Icon(k, col, 17));
                var detail = UIX.Div("res-detail"); detail.Add(UIX.Text(tip.ToUpperInvariant(), "res-label"));
                var values = UIX.Div("row"); var v = UIX.Text("0", "res-value"); _res[k] = v; values.Add(v);
                var inc = UIX.Text("", "res-income"); _inc[k] = inc; values.Add(inc); detail.Add(values); box.Add(detail); _top.Add(box);
            }
            _top.Add(UIX.Div("spacer"));
            _pause = UIX.IconBtn(IconKind.Pause, () => _root.TogglePause(), "Pause or resume (Space)");
            _s1 = UIX.IconBtn(IconKind.Play, () => _root.SetSpeed(1), "Normal speed (1)");
            _s2 = UIX.IconBtn(IconKind.Fast, () => _root.SetSpeed(2), "Fast (2)");
            _s3 = UIX.IconBtn(IconKind.Faster, () => _root.SetSpeed(3), "Fastest (3)");
            _top.Add(_pause); _top.Add(_s1); _top.Add(_s2); _top.Add(_s3);
            var time = UIX.Div("time-status"); _date = UIX.Text("", "date"); _speedLabel = UIX.Text("", "speed-label");
            time.Add(_date); time.Add(_speedLabel); _top.Add(time); _top.Add(UIX.Div("spacer"));
            _overlayBtn = UIX.IconBtn(IconKind.Layers, () => _root.ToggleOverlays(), "Strategic overlay (Tab)"); _top.Add(_overlayBtn);
            _top.Add(UIX.IconBtn(IconKind.Save, () => _root.SaveGame(), "Save (F5)"));
            _top.Add(UIX.IconBtn(IconKind.Load, () => _root.LoadGame(), "Load last save (F9)"));
            _top.Add(UIX.IconBtn(IconKind.Gear, () => _root.ShowSettings(), "Settings"));
        }

        public void RefreshTop()
        {
            var st = G.Player.Stock; var inc = StrategyRules.DailyIncome(G, G.Player.Id);
            string who = G.Player.Name.ToUpperInvariant();
            if (_playerName.text != who) _playerName.text = who;   // a new campaign changes the player's power
            UiMotion.Number(_res[IconKind.Energy], st.Energy); _inc[IconKind.Energy].text = "+" + inc.Energy + " /d";
            UiMotion.Number(_res[IconKind.Materials], st.Materials); _inc[IconKind.Materials].text = "+" + inc.Materials + " /d";
            UiMotion.Number(_res[IconKind.Credits], st.Credits); _inc[IconKind.Credits].text = "+" + inc.Credits + " /d";
            UiMotion.Number(_res[IconKind.Influence], st.Influence); _inc[IconKind.Influence].text = "";
            _date.text = GalaxyState.DateLabel(G.Day);
            var c = _root.Clock;
            _speedLabel.text = G.PendingEngagement != null ? "DECISION REQUIRED" : c.Paused ? "PAUSED" : c.Speed == 1 ? "NORMAL" : c.Speed == 2 ? "FAST" : "FASTEST";
            Toggle(_pause, c.Paused); Toggle(_s1, !c.Paused && c.Speed == 1); Toggle(_s2, !c.Paused && c.Speed == 2); Toggle(_s3, !c.Paused && c.Speed == 3);
            Toggle(_overlayBtn, Settings.Overlays);
        }
        static void Toggle(VisualElement b, bool on) => b.EnableInClassList("btn-toggle-on", on);

        public void RefreshOutliner()
        {
            _outScroll.Clear();
            foreach (var f in G.Fleets.Where(f => f.OwnerId == G.Player.Id && f.Ships.Count > 0))
            {
                var card = UIX.Btn("", () => _root.SelectAndFocusFleet(f.Id), "fleet-card");
                card.name = "fleet-card-" + f.Id;
                bool selected = _root.Selection.FleetId == f.Id;
                card.EnableInClassList("fleet-card-selected", selected);
                card.Add(UIX.Text(selected ? "SELECTED FLEET" : "FLEET ASSET", "fleet-selection"));
                var title = UIX.Div("row"); title.Add(UiKit.AccentIcon(IconKind.Fleet, 14)); title.Add(UIX.Text(f.Name, "item-title")); card.Add(title);
                var lead = f.Ships.OrderByDescending(s => ShipCatalog.IsLeader(s.DefId)).First();
                var line = UIX.Div("row", "fleet-card-body"); line.Add(UiKit.InspectShip(lead.DefId, true));
                var stats = UIX.Div("fleet-card-stats"); stats.Add(UIX.Text(f.Ships.Count + " SHIPS", "fleet-count"));
                stats.Add(UIX.Text(f.EngagementId != null ? "IN COMBAT" : f.Moving ? "IN TRANSIT" : "HOLDING", "fleet-status")); line.Add(stats); card.Add(line);
                var location=UIX.Div("row","fleet-location");
                location.Add(UIX.Text(f.Moving ? G.GetSystem(f.Order.Path.Last()).Name + " / " + EtaDays(f) + " days" : G.GetSystem(f.SystemId).Name, "item-sub")); stats.Add(location);
                card.tooltip = FleetStatus(f); _outScroll.Add(card);
            }
            if (_outScroll.contentContainer.childCount == 0) _outScroll.Add(UIX.Text("No active fleets", "small"));
        }
        public string FleetStatus(Fleet f)
        {
            string where = G.GetSystem(f.SystemId).Name;
            if (f.EngagementId != null) return $"{f.Ships.Count} ships engaged at {where}.";
            if (f.Moving) return $"{f.Ships.Count} ships bound for {G.GetSystem(f.Order.Path.Last()).Name}. Arrival in {EtaDays(f)} days.";
            return $"{f.Ships.Count} ships holding at {where}.";
        }
        public int EtaDays(Fleet f)
        {
            if (!f.Moving) return 0;
            int days = 0; string at = f.SystemId;
            foreach (var step in f.Order.Path) { days += G.LaneBetween(at, step).Days; at = step; }
            return days - f.Order.DaysIntoLane;
        }

        VisualElement FleetItem(Fleet f)
        {
            var item = Item(f.Name, FleetStatus(f), _root.Selection.FleetId == f.Id, () => { _root.UI.CloseModal(); _root.SelectAndFocusFleet(f.Id); });
            if (f.OwnerId != G.Player.Id) item.Add(UIX.Tag("HOSTILE", "tag-danger"));
            return item;
        }
        static VisualElement Item(string title, string sub, bool selected, Action action)
        {
            var it = UIX.Btn("", action, "item"); it.EnableInClassList("item-selected", selected);
            it.Add(UIX.Text(title, "item-title")); if (!string.IsNullOrEmpty(sub)) it.Add(UIX.Text(sub, "item-sub")); return it;
        }
        void Navigation(VisualElement target, bool contacts)
        {
            if (contacts)
            {
                target.Add(UiKit.Section("02", "Known contacts"));
                foreach (var f in G.Fleets.Where(f => f.OwnerId != G.Player.Id && f.Ships.Count > 0 && (f.Moving || G.GetSystem(f.SystemId).OwnerId == G.Player.Id || f.Id == SliceScenario.RaidFleet))) target.Add(FleetItem(f));
                if (G.PendingEngagement != null) target.Add(UIX.Btn("Resolve contact at " + G.GetSystem(G.PendingEngagement.SystemId).Name, () => _root.OpenEngagement(), "btn-primary"));
            }
            else
            {
                target.Add(UiKit.Section("02", "System directory"));
                // In the passage campaign the directory lists the systems the campaign is about.
                var keys = G.Slice != null && G.Slice.Active
                    ? new[] { SliceCampaign.HomeOf(G.Player.Id), SliceCampaign.PassageSystem, Expeditions.System, SliceCampaign.HomeOf(G.Player.Id == "pygmaei" ? "inhumanus" : "pygmaei") }
                        .Select(G.GetSystem).Where(s => s != null)
                    : G.Systems.Where(s => s.Detail != SystemDetail.Distant);
                foreach (var s in keys)
                    target.Add(Item(s.Name, s.OwnerId != null ? G.GetPolity(s.OwnerId).Name : "Unclaimed", _root.Selection.SystemId == s.Id,
                        () => { _root.UI.CloseModal(); _root.SelectAndFocusSystem(s.Id); }));
            }
        }
        void OpenNavigation()
        {
            var modal = _root.UI.OpenModal(820); var head = UIX.Div("row");
            head.Add(UIX.Text("COMMAND DIRECTORY", "modal-title")); head.Add(UIX.Div("spacer")); head.Add(UIX.Btn("Close", _root.UI.CloseModal)); modal.Add(head);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; UIRoot.StyleScroll(scroll); modal.Add(scroll);
            Navigation(scroll, false); Navigation(scroll, true);
        }

        public void ShowTab(string tab) { _tab = tab; RefreshInspector(); }
        public void RefreshInspector()
        {
            var sel = _root.Selection; string key = sel.FleetId ?? sel.PlanetId ?? sel.SystemId ?? "galaxy";
            if (_selectionKey != key) { _selectionKey = key; _tab = "Overview"; _projectKey = null; _insp.scrollOffset = Vector2.zero; }
            _heading.Clear(); _insp.Clear(); _back.Clear(); _actions.Clear(); RefreshContext();
            if (sel.FleetId != null && G.GetFleet(sel.FleetId) != null) FleetInspector(G.GetFleet(sel.FleetId));
            else if (sel.PlanetId != null && G.GetPlanet(sel.PlanetId) != null) PlanetInspector(G.GetPlanet(sel.PlanetId));
            else if (sel.SystemId != null) SystemInspector(G.GetSystem(sel.SystemId));
            else { Title("The known galaxy", "Sovereignty begins with perspective"); Navigation(_insp, false); Command("STRATEGIC OVERVIEW", "Select a system", "Inspect worlds, manage fleets and set your next course."); }
            RevealChangedSelection();
        }
        void RevealChangedSelection()
        {
            string key = _selectionKey + "|" + _tab + "|" + _projectKey;
            if (key == _motionKey) return;
            _motionKey = key;
            UiMotion.Enter(_insp.contentContainer, 6); UiMotion.Enter(_actions, -6); UiMotion.Cascade(_insp.contentContainer);
            Prismatic.Pulse(_inspector); Prismatic.Pulse(_actions.Q(className:"command-card"));
        }
        void Title(string title, string sub, string category = "SYSTEM INTELLIGENCE")
        {
            _heading.Add(UIX.Text(category, "eyebrow"));
            var t = UIX.Text(title.ToUpperInvariant(), "h-display"); UIRoot.Display(t); _heading.Add(t);
            _heading.Add(UIX.Text(sub, "subtitle"));
        }
        void Tabs(params string[] labels) => _heading.Add(UiKit.Tabs(_tab, ShowTab, labels));
        void KV(string label, string value, bool warning = false)
        {
            var row = UiKit.Data(label, value); if (warning) row.Q<Label>(className: "data-value").AddToClassList("text-danger"); _insp.Add(row);
        }
        VisualElement Command(string eyebrow, string title, string description, string ship = null)
        {
            _actions.Add(UIX.Text(eyebrow, "eyebrow"));
            var row = UIX.Div("row", "command-body", "command-card", "surface-light"); _actions.Add(row);
            if (ship != null) row.Add(UiKit.InspectShip(ship));
            var info = UIX.Div("command-info"); row.Add(info);
            info.Add(UIX.Text(title, "command-title"));
            if (!string.IsNullOrEmpty(description)) info.Add(UIX.Text(description, "small"));
            return info;
        }
        void SystemInspector(StarSystem s)
        {
            var sector = s.SectorId != null ? G.GetSector(s.SectorId) : null;
            Title(s.Name, (sector != null ? sector.Name : "Unclaimed space") + " / " + StarText(s.Star)); Tabs("Overview", "Systems", "Contacts");
            if (_tab != "Overview") Navigation(_insp, _tab == "Contacts");
            else
            {
                _insp.Add(UiKit.Section("01", "System intelligence"));
                KV("Sovereign", s.OwnerId != null ? G.GetPolity(s.OwnerId).Name : "Unclaimed"); KV("Worlds", s.Planets.Count.ToString());
                if (s.OrbitalControlId != s.OwnerId && s.OrbitalControlId != null) KV("Orbit held by", G.GetPolity(s.OrbitalControlId).Name, true);
                if (s.Conquest != ConquestStage.None && s.ConquestBy != null) KV("Conquest", ConquestText(s), true);
                _insp.Add(UiKit.Section("02", "Worlds"));
                foreach (var p in s.Planets)
                {
                    var it = Item(p.Name, KindText(p.Kind) + (p.HasShipyard ? " / Shipyard" : ""), false, () => _root.InspectPlanet(p.Id));
                    it.AddToClassList("world-row"); it.Insert(0, new WorldGlyph(p.Kind)); _insp.Add(it);
                }
                var here = G.Fleets.Where(f => f.SystemId == s.Id && f.Ships.Count > 0 && !f.Moving).ToList();
                if (here.Count > 0) { _insp.Add(UiKit.Section("03", "Fleets present")); foreach (var f in here) _insp.Add(FleetItem(f)); }
            }
            var info = Command("02  /  SYSTEM COMMAND", s.Name, s.Blockaded ? "Hostile ships control this system's orbit." : "Select a world to inspect its surface and available projects.");
            if (_root.Scale == ViewScale.Galaxy) info.Add(UIX.Btn("Enter system", () => _root.EnterSystem(s.Id), "btn-primary"));
            else info.Add(UIX.Btn("Return to galaxy", () => _root.ExitToGalaxy(), "btn-primary"));
            _back.Add(UIX.Btn("System directory", OpenNavigation, "btn-quiet"));
        }
        void PlanetInspector(Planet p)
        {
            var s = G.SystemOf(p); bool mine = p.OwnerId == G.Player.Id;
            Title(p.Name, KindText(p.Kind) + " / " + s.Name, "STRATEGIC DOSSIER");
            Tabs("Overview", "Shipyard", "Development");
            var options = StrategyRules.ProjectOptions(G, p.Id);
            var choices = options.Where(o => _tab == "Development" ? o.Kind != ProjectKind.CommissionShip : o.Kind == ProjectKind.CommissionShip).ToList();
            if (choices.Count > 0 && !choices.Any(o => ProjectKey(o) == _projectKey)) _projectKey = ProjectKey(choices[0]);
            if (_tab == "Overview")
            {
                _insp.Add(UiKit.Section("01", "World intelligence")); KV("Sovereign", p.OwnerId != null ? G.GetPolity(p.OwnerId).Name : "Unclaimed");
                KV("Population", p.Population > 0 ? UIX.Thousands(p.Population) : "Uninhabited");
                if (p.OwnerId != null)
                {
                    _insp.Add(UiKit.Development(p.Development)); KV("Shipyard", p.HasShipyard ? "Established" : "None"); KV("Crystal Foundries", p.Foundries + " / " + ShipCatalog.MaxFoundries);
                    _insp.Add(UiKit.Section("02", "Daily production")); var y = p.DailyYield;
                    _insp.Add(UiKit.Production(y.Energy, y.Materials + p.Foundries * ShipCatalog.FoundryYield.Materials, y.Credits, s.Blockaded));
                }
                _insp.Add(UiKit.Section("03", "Orbital status"));
                _insp.Add(UiKit.Orbit(s.Blockaded));
            }
            else
            {
                _insp.Add(UiKit.Section("01", _tab == "Shipyard" ? "Available hulls" : "Infrastructure"));
                if (!mine) _insp.Add(UIX.Text("Projects are managed by this world's sovereign.", "body"));
                else if (choices.Count == 0) _insp.Add(UIX.Text(_tab == "Shipyard" ? "This world has no shipyard." : "No development projects are available.", "body"));
                else foreach (var o in choices) _insp.Add(ProjectRow(o));
            }
            if (p.Project != null)
            {
                var project = p.Project; string title = project.Kind == ProjectKind.CommissionShip ? ShipCatalog.Name(project.ShipDefId) : "Crystal Foundry";
                var info = Command(s.Blockaded ? "02  /  PRODUCTION SUSPENDED" : "02  /  PROJECT IN PROGRESS", title,
                    (project.DaysTotal - project.DaysDone) + " days remaining" + (s.Blockaded ? " / Blockaded" : ""), project.ShipDefId);
                info.Add(UIX.Bar(project.DaysDone / (float)project.DaysTotal));
                if (mine) { var cancel = UIX.Btn("Cancel project", () => _root.CancelProject(p.Id), "btn-danger"); cancel.name = "project-cancel"; cancel.tooltip = "Cancel construction and recover half the original cost"; info.Add(cancel); info.Add(UIX.Text("Cancellation returns half the cost.", "small")); }
            }
            else if (mine && options.Count > 0)
            {
                var chosen = choices.FirstOrDefault(o => ProjectKey(o) == _projectKey) ?? choices.FirstOrDefault();
                if (chosen == null && _tab == "Overview") chosen = options.FirstOrDefault();
                if (chosen != null) ProjectCommand(p, chosen);
                else Command("02  /  WORLD COMMAND", "No project available", "Review development or inspect another world.");
            }
            else Command("02  /  WORLD COMMAND", mine ? "No project available" : "Foreign world", mine ? "This world has no available construction projects." : "Inspect this world's sovereign and orbital status in the dossier.");
            _back.Add(UIX.Btn(_root.Scale == ViewScale.Planet ? "Return to " + s.Name : "Inspect world", () => { if (_root.Scale == ViewScale.Planet) _root.ExitPlanet(); else _root.InspectPlanet(p.Id); }, "btn-quiet"));
        }
        static string ProjectKey(ProjectOption o) => o.ShipDefId ?? o.Kind.ToString();
        VisualElement ProjectRow(ProjectOption o)
        {
            bool selected = _projectKey == ProjectKey(o);
            var box = Item("", "", selected, () => { _projectKey = ProjectKey(o); RefreshInspector(); });
            box.Clear(); box.AddToClassList("project-card"); box.name = "project-option-" + ProjectKey(o);
            var head = UIX.Div("row", "project-head"); box.Add(head);
            if (o.Kind == ProjectKind.CommissionShip) head.Add(UiKit.Ship(o.ShipDefId, true));
            else { var icon = UiKit.ContentIcon(IconKind.Foundry, 30); icon.AddToClassList("project-icon"); head.Add(icon); }
            var copy = UIX.Div("project-copy"); head.Add(copy);
            copy.Add(UIX.Text(o.Kind == ProjectKind.CommissionShip ? ShipCatalog.Name(o.ShipDefId) : "Crystal Foundry", "item-title"));
            copy.Add(UIX.Text(o.Days + " DAYS" + (selected ? " / SELECTED" : ""), "project-meta"));
            if (o.Kind == ProjectKind.CommissionShip)
            {
                var d = ShipCatalog.Def(o.ShipDefId);
                box.Add(UIX.Text(RoleText(d) + " / Tier " + d.Roster.Tier + " / " + ShipCatalog.Cost(o.ShipDefId) + " battle points", "item-sub"));
                box.Add(UIX.Text(d.MaxHp + " hull  /  " + d.Armor + " armor", "item-sub"));
                box.tooltip = d.Roster.ActiveName + ": " + d.Roster.ActiveText;
                if (selected) box.Add(UIX.Text(d.Roster.ActiveName + ": " + d.Roster.ActiveText, "item-sub", "project-ability"));
            }
            else box.Add(UIX.Text("+" + ShipCatalog.FoundryYield.Materials + " materials per day", "item-sub"));
            var cost = UiKit.Cost(o.Cost); cost.AddToClassList("project-cost"); box.Add(cost);
            if (!o.Available) box.Add(UIX.Text(o.UnavailableReason ?? "Unavailable", "item-sub", "text-danger"));
            return box;
        }
        void ProjectCommand(Planet p, ProjectOption o)
        {
            _projectKey = ProjectKey(o);
            var info = Command(o.Kind == ProjectKind.CommissionShip ? "02  /  SHIPYARD REQUISITION" : "02  /  DEVELOPMENT ORDER", o.Label,
                o.Days + " days to complete" + (o.Kind == ProjectKind.CommissionShip ? " / " + RoleText(ShipCatalog.Def(o.ShipDefId)) : " / +4 materials per day"), o.ShipDefId);
            info.Add(UiKit.Cost(o.Cost));
            var button = UIX.Btn(o.Kind == ProjectKind.CommissionShip ? "Commission ship" : "Build foundry", () => _root.StartProject(p.Id, o.Kind, o.ShipDefId), "btn-primary");
            button.name = "project-commit"; button.SetEnabled(o.Available); button.tooltip = o.UnavailableReason ?? "Begin this project"; info.Add(button);
            if (!o.Available) info.Add(UIX.Text(o.UnavailableReason ?? "Unavailable", "small", "text-danger"));
        }
        void FleetInspector(Fleet f)
        {
            bool mine = f.OwnerId == G.Player.Id; Title(f.Name, G.GetPolity(f.OwnerId).Name + (mine ? " / Expeditionary force" : " / Hostile contact"), "FLEET DOSSIER");
            Tabs("Overview", "Systems", "Contacts");
            if (_tab != "Overview") Navigation(_insp, _tab == "Contacts");
            else
            {
                _insp.Add(UiKit.Summary(f.EngagementId != null ? "Engaged / Orders locked" : f.Moving ? "In transit / " + EtaDays(f) + " days to arrival" : "Holding / " + G.GetSystem(f.SystemId).Name,
                    "Ships in fleet", f.Ships.Count.ToString("D2"), "Battle points", f.Ships.Sum(s => ShipCatalog.Cost(s.DefId)).ToString(), f.EngagementId != null));
                int hull = f.Ships.Sum(ship => ship.Hp), maxHull = f.Ships.Sum(ship => ShipCatalog.Def(ship.DefId).MaxHp);
                KV("Hull integrity", hull + " / " + maxHull, hull < maxHull);
                var readiness = UIX.Text("Deploy up to " + MatchConfigBudget + " battle points. Choose your battle line at contact.", "small", "readiness-note"); _insp.Add(readiness);
                if (!StrategyRules.CanFight(f)) _insp.Add(UIX.Text("No flagship. This fleet must withdraw from contact.", "body", "text-danger"));
                _insp.Add(UiKit.Section("01", "Fleet composition"));
                foreach (var ship in f.Ships.OrderByDescending(s => ShipCatalog.IsLeader(s.DefId)).ThenByDescending(s => ShipCatalog.Tier(s.DefId)))
                {
                    var d = ShipCatalog.Def(ship.DefId); var it = UIX.Div("roster-item"); var row = UIX.Div("row", "roster-body");
                    row.Add(UiKit.InspectShip(ship.DefId, true)); var words = UIX.Div("roster-info"); row.Add(words);
                    words.Add(UIX.Text(d.Name, "item-title")); words.Add(UIX.Text(ShipCatalog.IsLeader(ship.DefId) ? "COMMAND FLAGSHIP" : RoleText(d).ToUpperInvariant(), "item-sub"));
                    var health = UIX.Div("row", "roster-health"); health.Add(UIX.Text("HULL", "small")); health.Add(UIX.Div("spacer"));
                    health.Add(UIX.Text(ship.Hp + " / " + d.MaxHp, "roster-hull")); words.Add(health);
                    float hp = ship.Hp / (float)d.MaxHp; words.Add(UIX.Bar(hp, hp < .4f ? "bar-hp-low" : "bar-hp")); it.Add(row);
                    it.EnableInClassList("roster-flagship", ShipCatalog.IsLeader(ship.DefId));
                    it.tooltip = "Moves " + MoveText(d) + ". " + d.Roster.ActiveName + ": " + d.Roster.ActiveText; _insp.Add(it);
                }
            }
            var lead = f.Ships.OrderByDescending(s => ShipCatalog.IsLeader(s.DefId)).FirstOrDefault();
            var info = Command("02  /  FLEET COMMAND", f.Name, FleetStatus(f), lead?.DefId);
            if (mine)
            {
                var row = UIX.Div("row", "command-buttons"); var move = UIX.Btn(_root.Targeting ? "Choose destination" : "Set course", () => _root.BeginMoveTargeting(), "btn-primary");
                move.name = "fleet-move";
                move.SetEnabled(f.EngagementId == null && G.PendingEngagement == null); row.Add(move);
                if (f.Moving) { var hold = UIX.Btn("Hold", () => _root.CancelMove(f.Id), "command-secondary"); hold.name = "fleet-hold"; row.Add(hold); } info.Add(row);
                info.Add(UIX.Text(f.EngagementId != null || G.PendingEngagement != null ? "Resolve the current contact before issuing orders." : _root.Targeting ? "Select a destination on the map. Escape cancels." : "Right-click a system to issue a move order.", "small", "command-hint"));
            }
            else info.Add(UIX.Text("Foreign fleet / Orders unavailable", "small", "text-danger"));
            _back.Add(UIX.Btn("System directory", OpenNavigation, "btn-quiet"));
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


        public void Toast(string text, string kind = "ok")
        {
            var t = UIX.Text(text, "panel", "toast", kind == "bad" ? "toast-bad" : kind == "danger" ? "toast-danger" : "toast-ok");
            t.pickingMode = PickingMode.Ignore; _toasts.Add(t); while (_toasts.childCount > 3) _toasts.RemoveAt(0);
            t.schedule.Execute(() => UiMotion.Leave(t, t.RemoveFromHierarchy)).StartingIn(kind == "bad" ? 4200 : 3400);
        }
        public void Hint(string text) { _hint.text = text ?? ""; _hint.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex; }
        public void RefreshTicker()
        {
            _ticker.Clear(); var e = G.Log.LastOrDefault();
            if (e == null) { _ticker.style.display = DisplayStyle.None; return; }
            _ticker.style.display = DisplayStyle.Flex;
            var top = UIX.Div("row"); top.Add(UIX.Text("DISPATCH / " + GalaxyState.DateLabel(e.Day), "eyebrow")); top.Add(UIX.Div("spacer"));
            top.Add(UIX.Btn("Log", OpenLog, "btn-quiet", "log-button")); _ticker.Add(top);
            var line = UIX.Text(e.Text, "ticker-line"); if (e.Kind == "danger") line.AddToClassList("text-warning"); _ticker.Add(line);
            if (G.PendingEngagement != null) _ticker.Add(UIX.Btn("Review contact", () => _root.OpenEngagement(), "btn-primary"));
        }
        void OpenLog()
        {
            var modal = _root.UI.OpenModal(800); var title = UIX.Div("row"); title.Add(UIX.Text("COMMAND DISPATCHES", "modal-title"));
            title.Add(UIX.Div("spacer")); title.Add(UIX.Btn("Close", _root.UI.CloseModal)); modal.Add(title);
            var scroll = new ScrollView(); scroll.style.flexShrink = 1; UIRoot.StyleScroll(scroll); modal.Add(scroll);
            foreach (var e in G.Log.AsEnumerable().Reverse()) { scroll.Add(UIX.Text(GalaxyState.DateLabel(e.Day), "h-section")); scroll.Add(UIX.Text(e.Text, "body")); scroll.Add(UIX.Div("divider")); }
        }
        void RefreshContext()
        {
            Root.EnableInClassList("world-inspection", _root.Scale == ViewScale.Planet);
            _contextMode.text = _root.Scale == ViewScale.Galaxy ? "GALACTIC THEATRE" : _root.Scale == ViewScale.Planet ? "SYSTEM / WORLD INSPECTION" : "GALAXY / STAR SYSTEM";
            _contextTitle.text = _root.Scale == ViewScale.Galaxy ? "The known galaxy" : _root.LocalSystem != null ? _root.LocalSystem.Data.Name : "Local space";
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
            foreach (var panelEl in new[] { _top, _outliner, _inspector, _context, _ticker, _dock })
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