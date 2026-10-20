using System;
using System.Collections.Generic;
using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Owns the UI Toolkit document, fonts, style sheet and the modal, loading and cross-fade layers.
    public sealed class UIRoot : MonoBehaviour
    {
        public UIDocument Doc { get; private set; }
        public VisualElement Root { get; private set; }
        public VisualElement HudLayer { get; private set; }
        public StrategyHud Strategy { get; private set; }
        public TooltipLayer Tooltips { get; private set; }
        VisualElement _modalLayer, _fade, _loading;
        Label _loadingText;
        VisualElement _loadingBar;
        GameRoot _game;
        IVisualElementScheduledItem _fadeItem;
        public bool ModalOpen { get { return _modalLayer.childCount > 0; } }

        public static void Display(VisualElement e)
        {
            var f = ArtLibrary.I.bodySemiBold != null ? ArtLibrary.I.bodySemiBold : ArtLibrary.I.bodyBold;
            if (f != null) e.style.unityFontDefinition = FontDefinition.FromFont(f);
        }

        public void Init(GameRoot game)
        {
            _game = game;
            var lib = ArtLibrary.I;
            Doc = gameObject.AddComponent<UIDocument>();
            Doc.panelSettings = lib.panelSettings;
            Root = Doc.rootVisualElement;
            Root.styleSheets.Add(UnityEngine.Resources.Load<StyleSheet>("RR/RR_Style"));
            if (lib.bodyFont != null) Root.style.unityFontDefinition = FontDefinition.FromFont(lib.bodyFont);
            UiKit.Install(Root, game.G.Player.Id);
            Root.pickingMode = PickingMode.Ignore;
            HudLayer = UIX.Div("layer"); HudLayer.pickingMode = PickingMode.Ignore; Root.Add(HudLayer);
            Strategy = new StrategyHud(game, HudLayer);
            _modalLayer = UIX.Div("layer"); _modalLayer.pickingMode = PickingMode.Ignore; Root.Add(_modalLayer);
            _loading = UIX.Div("loading"); _loading.pickingMode = PickingMode.Ignore;
            var lp = UIX.Div("panel"); lp.style.width = 420; lp.style.alignItems = Align.Center;
            _loadingText = UIX.Text("", "body"); lp.Add(_loadingText);
            var bar = UIX.Div("bar"); bar.style.width = 380; bar.style.marginTop = 6;
            _loadingBar = UIX.Div("bar-fill"); bar.Add(_loadingBar); lp.Add(bar);
            _loading.Add(lp);
            _loading.style.display = DisplayStyle.None;
            Root.Add(_loading);
            _fade = UIX.Div("fade"); _fade.pickingMode = PickingMode.Ignore; _fade.style.display = DisplayStyle.None; Root.Add(_fade);
            Tooltips = new TooltipLayer(Root);   // last child: above modals, loading and the cross-fade
            ApplyScale();
        }

        public static void StyleScroll(ScrollView view)
        {
            view.AddToClassList("kit-scroll");
            // A vertical dossier must not acquire a horizontal bar during a row reveal.
            if (view.mode == ScrollViewMode.Vertical)
                view.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            foreach (var sc in new[] { view.verticalScroller, view.horizontalScroller })
            {
                bool vertical = sc == view.verticalScroller;
                if (vertical)
                {
                    sc.style.width = sc.style.minWidth = sc.style.maxWidth = 6;
                    sc.slider.style.width = sc.slider.style.minWidth = sc.slider.style.maxWidth = 6;
                }
                else
                {
                    sc.style.height = sc.style.minHeight = sc.style.maxHeight = 6;
                    sc.slider.style.height = sc.slider.style.minHeight = sc.slider.style.maxHeight = 6;
                }
                sc.slider.style.marginLeft = sc.slider.style.marginRight = sc.slider.style.marginTop = sc.slider.style.marginBottom = 0;
                sc.lowButton.style.display = DisplayStyle.None; sc.highButton.style.display = DisplayStyle.None;
                foreach (var name in new[] { "unity-tracker", "unity-dragger-border", "unity-dragger" })
                {
                    var part = sc.slider.Q(name); if (part == null) continue;
                    part.style.borderLeftWidth = part.style.borderRightWidth = part.style.borderTopWidth = part.style.borderBottomWidth = 0;
                    if (vertical) part.style.minWidth = part.style.maxWidth = 6;
                    else part.style.minHeight = part.style.maxHeight = 6;
                }
            }
        }

        public void ApplyScale()
        {
            if (Doc == null || Doc.panelSettings == null) return;
            float s = Mathf.Clamp(Settings.UiScale, 0.8f, 1.5f);
            Doc.panelSettings.referenceResolution = new Vector2Int(Mathf.RoundToInt(1920 / s), Mathf.RoundToInt(1080 / s));
        }

        public bool PointerOverUI(Vector2 screen)
        {
            var panel = Root.panel;
            if (panel == null) return false;
            int ph = Camera.main != null ? Camera.main.pixelHeight : Screen.height;
            var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, ph - screen.y));
            var picked = panel.Pick(p);
            return picked != null && picked != Root;
        }

        public void ShowLoading(string text, float progress)
        {
            _loading.style.display = DisplayStyle.Flex;
            _loadingText.text = text;
            _loadingBar.style.width = Length.Percent(Mathf.Clamp01(progress) * 100);
        }
        public void HideLoading() { _loading.style.display = DisplayStyle.None; }

        // Cross-fade from a captured frame to the live view (scale transitions).
        public void FadeFrom(Texture tex, float seconds)
        {
            _fade.style.backgroundColor = new Color(0, 0, 0, 0);
            _fade.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(tex as RenderTexture));
            // ScreenCapture into a render texture is upside down where UVs start at the top (Direct3D).
            bool flip = SystemInfo.graphicsUVStartsAtTop && Camera.main != null && Camera.main.targetTexture == null;
            _fade.style.scale = new Scale(new Vector3(1, flip ? -1 : 1, 1));
            _fade.style.opacity = 1;
            _fade.style.display = DisplayStyle.Flex;
            float t0 = Time.unscaledTime;
            if (_fadeItem != null) _fadeItem.Pause();
            _fadeItem = _fade.schedule.Execute(() =>
            {
                float k = Mathf.Clamp01((Time.unscaledTime - t0) / Mathf.Max(0.01f, seconds));
                _fade.style.opacity = 1 - k * k * (3 - 2 * k);
                if (k >= 1) { _fade.style.display = DisplayStyle.None; _fade.style.backgroundImage = StyleKeyword.None; }
            }).Every(16).Until(() => _fade.style.display == DisplayStyle.None);
        }

        public void FadeToBlack(bool on)
        {
            if (_fadeItem != null) _fadeItem.Pause();
            _fade.style.backgroundImage = StyleKeyword.None;
            _fade.style.backgroundColor = new Color(0.01f, 0.012f, 0.02f, 1);
            _fade.style.opacity = on ? 1 : 0;
            _fade.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;
        }

        public VisualElement OpenModal(float width = 1180)
        {
            CloseModal();
            var back = UIX.Div("modal-back"); CinematicUi.Backdrop(back);
            var m = UIX.Div("panel", "panel-solid", "modal");
            m.style.width = width;
            m.style.maxWidth = Length.Percent(96);
            back.Add(m);
            _modalLayer.Add(back);
            return m;
        }
        public void CloseModal() { Tooltips?.Hide(); _modalLayer.Clear(); }

        // ------------------------------------------------------------------ battle preparation

        public void ShowBattlePrep(Engagement e, List<int> selection, Action<List<int>> commit, Action withdraw)
        {
            var G = _game.G;
            var sys = G.GetSystem(e.SystemId);
            var att = G.GetFleet(e.AttackerFleetId);
            var def = G.GetFleet(e.DefenderFleetId);
            var mine = att.OwnerId == G.Player.Id ? att : def;
            var enemy = mine == att ? def : att;
            var enemyLine = BattleBridge.AutoSelect(G, enemy);
            var map = Commandfall.Core.MapLibrary.Get(sys.BattlefieldMapId);
            var m = OpenModal(1260);
            var head = UIX.Div("row");
            var title = UIX.Text("ORBITAL COMMANDFALL", "modal-title"); Display(title); head.Add(title);
            head.Add(UIX.Div("spacer"));
            head.Add(UIX.Text(sys.Name + ", " + map.Name, "h-display"));
            m.Add(head);
            m.Add(UIX.Text((mine == def ? enemy.Name + " has arrived over " + sys.Name + ". " : mine.Name + " has reached " + sys.Name + " and found " + enemy.Name + ". ") +
                "Choose the ships that will form your battle line. The battle is decided by positioning and known rules; destroying the enemy flagship ends it at once.", "subtitle"));
            // Columns take the remaining height and scroll; the footer below never shrinks, so its controls stay on screen.
            var cols = UIX.Div("row"); cols.style.alignItems = Align.Stretch; cols.style.flexGrow = 1; cols.style.flexShrink = 1; cols.style.minHeight = 0;
            m.Add(cols);
            var left = UIX.Div(); left.style.flexGrow = 1; left.style.flexShrink = 1; left.style.marginRight = 14; left.style.minHeight = 0; cols.Add(left);
            var rightScroll = new ScrollView(ScrollViewMode.Vertical); StyleScroll(rightScroll); rightScroll.style.width = 330; rightScroll.style.flexShrink = 1; cols.Add(rightScroll);
            var right = rightScroll.contentContainer;
            var budget = UIX.Text("", "budget"); var errors = UIX.Text("", "small");
            Button go = null;
            var rows = new VisualElement();
            Action rebuild = null;
            rebuild = () =>
            {
                rows.Clear();
                foreach (var s in mine.Ships.OrderByDescending(x => ShipCatalog.IsLeader(x.DefId)).ThenByDescending(x => ShipCatalog.Tier(x.DefId)))
                {
                    var d = ShipCatalog.Def(s.DefId);
                    bool on = selection.Contains(s.Uid);
                    var row = UIX.Div("ship-row", on ? "ship-row-on" : "ship-row-off"); UiHover.Track(row);
                    row.Add(UiKit.Ship(s.DefId, true));
                    var nameCol = UIX.Div("col-name");
                    var nm = UIX.Div("row");
                    if (ShipCatalog.IsLeader(s.DefId)) { var c = UiKit.ContentIcon(IconKind.Crown, 13); c.style.marginRight = 5; nm.Add(c); }
                    var prof = G.Slice?.Profile(s.Uid);
                    nm.Add(UIX.Text(prof?.Name != null ? prof.Name + " (" + d.Name + ")" : d.Name, "item-title"));
                    nameCol.Add(nm);
                    if (prof != null && prof.Modules.Count > 0)
                        nameCol.Add(UIX.Text("Fitted: " + string.Join(", ", prof.Modules.Select(x => Commandfall.Core.SliceRules.Module(x).Name)) + ".", "item-sub"));
                    nameCol.Add(UIX.Text(StrategyHud.RoleText(d) + ", tier " + d.Roster.Tier + (ShipCatalog.IsLeader(s.DefId) ? ". Leader: its loss ends the battle." : ""), "item-sub"));
                    nameCol.Add(UIX.Tag(on ? "IN BATTLE LINE" : "IN RESERVE", on ? "tag-line" : "tag-reserve"));
                    row.Add(nameCol);
                    row.Add(Stat("HULL", s.Hp + "/" + d.MaxHp));
                    row.Add(Stat("ARM", d.Armor.ToString()));
                    row.Add(Stat("COST", ShipCatalog.Cost(s.DefId).ToString()));
                    var txt = UIX.Div("col-text");
                    txt.Add(UIX.Text("Moves " + StrategyHud.MoveText(d) + ". " + d.Weapon.Name + ": " + d.Weapon.Damage + " damage at range " + d.Weapon.MinRange + " to " + d.Weapon.MaxRange + ".", "item-sub"));
                    txt.Add(UIX.Text(d.Roster.ActiveName + ": " + d.Roster.ActiveText, "item-sub"));
                    txt.Add(UIX.Text(d.Roster.PassiveName + ": " + d.Roster.PassiveText, "item-sub"));
                    row.Add(txt);
                    int uid = s.Uid;
                    row.RegisterCallback<ClickEvent>(ev =>
                    {
                        if (selection.Contains(uid)) selection.Remove(uid); else selection.Add(uid);
                        _game.Audio.Play("ui_select");
                        rebuild();
                    });
                    rows.Add(row);
                }
                var report = BattleBridge.Validate(G, mine, selection);
                budget.text = "Battle line cost " + report.Cost + " of " + report.Budget + ", ships " + selection.Count + " of " + report.MaxUnits;
                if (report.Cost > report.Budget) budget.AddToClassList("budget-over"); else budget.RemoveFromClassList("budget-over");
                errors.text = report.Valid ? "The battle line is ready." : string.Join(" ", report.Errors);
                errors.style.color = report.Valid ? new Color(.43f, .86f, .74f) : new Color(.96f, .57f, .55f);
                if (go != null) go.SetEnabled(report.Valid);
            };
            left.Add(UIX.Text("YOUR FORCE: " + mine.Name.ToUpperInvariant(), "h-section"));
            var sv = new ScrollView(ScrollViewMode.Vertical); StyleScroll(sv); sv.style.flexGrow = 1; sv.style.flexShrink = 1; sv.Add(rows); left.Add(sv);
            if (G.Slice != null && G.Slice.Active)
            {
                right.Add(UIX.Text("WHAT IS AT STAKE", "h-section"));
                foreach (var l in SliceCampaign.PreviewEncounter(G, e)) right.Add(UIX.Text(l, "item-sub"));
            }
            right.Add(UIX.Text("SCOUTED ENEMY LINE", "h-section"));
            foreach (var uid in enemyLine)
            {
                var s = enemy.Ships.First(x => x.Uid == uid);
                var d = ShipCatalog.Def(s.DefId);
                var it = UIX.Div("enemy-line-item");
                var body = UIX.Div("row", "enemy-line-body"); it.Add(body);
                body.Add(UiKit.Ship(s.DefId, true));
                var enemyInfo = UIX.Div("enemy-line-info"); body.Add(enemyInfo);
                enemyInfo.Add(UIX.Text(d.Name, "item-title", "enemy-name"));
                // The crown sits on the small-caps meta line, centred on it, so a wrapped name never pushes it out of line.
                var meta = UIX.Div("row", "enemy-meta");
                if (ShipCatalog.IsLeader(s.DefId))
                {
                    var c = UiKit.ContentIcon(IconKind.Crown, 11); c.AddToClassList("enemy-crown"); meta.Add(c);
                    meta.Add(UIX.Text("FLAGSHIP", "enemy-hull", "enemy-flagship"));
                }
                meta.Add(UIX.Text("HULL  " + s.Hp + " / " + d.MaxHp, "enemy-hull"));
                enemyInfo.Add(meta);
                enemyInfo.Add(UIX.Text(StrategyHud.RoleText(d) + ", armor " + d.Armor + ", " + d.Weapon.Damage + " damage at " + d.Weapon.MinRange + " to " + d.Weapon.MaxRange + ". " + d.Roster.ActiveName + ".", "item-sub"));
                right.Add(it);
            }
            // Prompt 18: adopted doctrine sidegrades are disclosed to both sides before deployment.
            var ours = RR.Strategy.Doctrines.Disclosure(G, mine.OwnerId).Where(l => l.Contains("Tactical")).ToList();
            var theirs = RR.Strategy.Doctrines.Disclosure(G, enemy.OwnerId).Where(l => l.Contains("Tactical")).ToList();
            if (ours.Count + theirs.Count > 0)
            {
                right.Add(UIX.Text("DOCTRINES IN FORCE", "h-section"));
                foreach (var l in ours) right.Add(UIX.Text("Yours: " + l, "item-sub"));
                foreach (var l in theirs) right.Add(UIX.Text("Theirs: " + l, "item-sub"));
            }
            right.Add(UIX.Text("BATTLEFIELD", "h-section"));
            right.Add(UIX.Text(map.Name + ". " + map.Description, "item-sub"));
            right.Add(UIX.Text("Asteroid fields give cover, nebulae shorten weapon range, wrecks block movement and sight, and ion storms damage ships that stop inside them.", "item-sub"));
            var foot = UIX.Div("row"); foot.style.marginTop = 12; foot.style.flexShrink = 0;
            var info = UIX.Div(); info.style.flexShrink = 1; info.Add(budget); info.Add(errors); foot.Add(info);
            foot.Add(UIX.Div("spacer"));
            foot.Add(UIX.Btn("Suggest a line", () => { selection.Clear(); selection.AddRange(BattleBridge.AutoSelect(G, mine)); rebuild(); }));
            if (G.Slice != null && G.Slice.Active) foot.Add(UIX.Btn("Offer terms instead", () => _game.CampaignOfferBeforeBattle()));
            // Prompt 16: with troops aboard, the encounter can become an invasion (its first orbital engagement) instead.
            var invasion = _game.InvasionTarget(e.SystemId, mine);
            if (invasion != null && (RR.Strategy.Operations.Embarked(G, mine.Id)?.Units.Count ?? 0) > 0)
                foot.Add(UIX.Btn("Invade " + invasion.Name, () => _game.Invade(mine.Id, invasion.Id)));
            foot.Add(UIX.Btn("Withdraw without fighting", () => withdraw(), "btn-danger"));
            go = UIX.Btn("Commit to battle", () => commit(new List<int>(selection)), "btn-primary");
            foot.Add(go);
            m.Add(foot);
            rebuild();
        }

        static VisualElement Stat(string k, string v)
        {
            var c = UIX.Div("col-stat");
            c.Add(UIX.Text(k, "small"));
            c.Add(UIX.Text(v, "num"));
            return c;
        }

        // ------------------------------------------------------------------ result

        public void ShowResult(BattleOutcome o, BattleSetup setup, string summary, string consequence, Action done)
        {
            var G = _game.G;
            bool won = o.WinnerSeat == setup.PlayerSeat;
            var m = OpenModal(860);
            var t = UIX.Text(won ? "VICTORY" : "DEFEAT", "modal-title");
            if (!won) t.AddToClassList("text-danger");
            Display(t);
            m.Add(t);
            m.Add(UIX.Text(summary, "body"));
            m.Add(UIX.Text(consequence, "subtitle"));
            var cols = UIX.Div("row"); cols.style.alignItems = Align.FlexStart;
            for (int seat = 0; seat < 2; seat++)
            {
                var col = UIX.Div(); col.style.flexGrow = 1; col.style.marginRight = 10;
                col.Add(UIX.Text((setup.Config.Players[seat].DisplayName ?? "").ToUpperInvariant(), "h-section"));
                for (int i = 0; i < o.Ships[seat].Count; i++)
                {
                    var so = o.Ships[seat][i];
                    var defId = setup.Config.Players[seat].Army.Units[i].UnitId;
                    string state = so.Destroyed ? "destroyed" : so.Captured ? "captured" : so.Hp + " hull remaining";
                    var entry = UIX.Div("row", "result-ship"); entry.Add(UiKit.Ship(defId, true));
                    var line = UIX.Text(ShipCatalog.Name(defId) + ": " + state + ".", "item-sub");
                    if (so.Destroyed || so.Captured) { line.AddToClassList("text-danger"); entry.AddToClassList("result-ship-lost"); }
                    entry.Add(line); col.Add(entry);
                }
                cols.Add(col);
            }
            m.Add(cols);
            var foot = UIX.Div("row"); foot.style.marginTop = 12; foot.Add(UIX.Div("spacer"));
            foot.Add(UIX.Btn(_game.GroundExercise?"Return to the galaxy":"Return to the system", () => { CloseModal(); done(); }, "btn-primary"));
            m.Add(foot);
        }

        // ------------------------------------------------------------------ settings

        public void ShowSettings()
        {
            var m = OpenModal(820); m.AddToClassList("settings-console");
            m.Add(UIX.Text(_game.G.Player.Name.ToUpperInvariant() + " / COMMAND CONSOLE", "eyebrow"));
            var t = UIX.Text("SETTINGS", "modal-title"); Display(t); m.Add(t);
            m.Add(UIX.Text("Shape your view of the expanse.", "subtitle"));
            var tabs = UIX.Div("settings-tabs"); m.Add(tabs);
            var scroll = new ScrollView(ScrollViewMode.Vertical); StyleScroll(scroll);
            scroll.AddToClassList("settings-scroll"); m.Add(scroll);
            var preferences = UIX.Div("settings-page"); preferences.name = "settings-preferences";
            var controls = UIX.Div("settings-page"); controls.name = "settings-controls";
            scroll.Add(preferences); scroll.Add(controls);
            Button preferencesTab = null, controlsTab = null;
            void SelectPage(bool shortcuts)
            {
                preferences.style.display = shortcuts ? DisplayStyle.None : DisplayStyle.Flex;
                controls.style.display = shortcuts ? DisplayStyle.Flex : DisplayStyle.None;
                preferencesTab.EnableInClassList("btn-primary", !shortcuts);
                controlsTab.EnableInClassList("btn-primary", shortcuts);
                preferencesTab.MarkDirtyRepaint(); controlsTab.MarkDirtyRepaint();
                scroll.scrollOffset = Vector2.zero;
            }
            preferencesTab = UIX.Btn("Preferences", () => SelectPage(false)); preferencesTab.name = "settings-tab-preferences";
            controlsTab = UIX.Btn("Controls & companion", () => SelectPage(true)); controlsTab.name = "settings-tab-controls";
            tabs.Add(preferencesTab); tabs.Add(controlsTab);
            preferences.Add(UiKit.Section("01", "DISPLAY & COMFORT"));
            preferences.Add(PreferenceSlider("Interface size", "Scale text and controls together.", "settings-scale", .85f, 1.4f, Settings.UiScale,
                v => { Settings.UiScale = v; Settings.Save(); ApplyScale(); }));
            preferences.Add(PreferenceToggle("Reduced motion", "Calmer movement and immediate interface transitions.", "settings-motion", Settings.ReducedMotion,
                v => { Settings.ReducedMotion = v; Root.EnableInClassList("reduced-motion", v); Settings.Save(); }));
            preferences.Add(UiKit.Section("02", "AUDIO"));
            preferences.Add(PreferenceToggle("Mute all sound", "Keep your volume mix while listening silently.", "settings-mute", Settings.Muted,
                v => { Settings.Muted = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            preferences.Add(PreferenceSlider("Master volume", null, "settings-master", 0, 1, Settings.Master,
                v => { Settings.Master = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            preferences.Add(PreferenceSlider("Music", null, "settings-music", 0, 1, Settings.Music,
                v => { Settings.Music = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            preferences.Add(PreferenceSlider("Effects", null, "settings-effects", 0, 1, Settings.Effects,
                v => { Settings.Effects = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            controls.Add(UiKit.Section("01", "NAVIGATION & ORDERS"));
            foreach (var pair in new[] { ("Left click", "Select a world or fleet"), ("Double click", "Enter a star system"),
                ("Right click", "Order the selected fleet"), ("Mouse wheel", "Zoom in or out"), ("Right / middle drag", "Orbit the view"),
                ("W A S D", "Pan the camera"), ("Home", "Recenter the view"), ("Escape", "Close a panel or return to the previous scale"),
                ("Space", "Pause or resume time"), ("Tab", "Toggle the strategic overlay") })
            {
                var row = UIX.Div("shortcut-row"); row.Add(UIX.Text(pair.Item1, "shortcut-key")); row.Add(UIX.Text(pair.Item2, "shortcut-action")); controls.Add(row);
            }
            controls.Add(UiKit.Section("02", "PLANETARY COMMANDFALL"));
            controls.Add(UIX.Text("Play a Silex versus Craigon exercise. Campaign progress is preserved.", "setting-hint"));
            foreach(var item in new[]{("Line infantry",0),("Specialists",1),("War engines",2)})
            { int g=item.Item2;var btn=UIX.Btn(item.Item1,()=>_game.StartGroundExercise(g));btn.SetEnabled(_game.Scale!=ViewScale.Battle);controls.Add(btn); }
            controls.Add(UIX.Btn("Inspect units and animations",()=>{CloseModal();UnitGallery.Open(_game);}));

            string exe = CommandfallExe();
            controls.Add(UIX.Text(exe != null ? "Companion game ready. Opens in its own window." : "Companion game unavailable on this installation.", "setting-hint"));
            var launch = UIX.Btn("Open planetary Commandfall", () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { WorkingDirectory = System.IO.Path.GetDirectoryName(exe) }); }
                catch (Exception ex) { Debug.LogWarning("[RR] could not start Commandfall: " + ex.Message); Strategy.Toast("Commandfall could not open. Check the companion installation.", "bad"); }
            });
            launch.SetEnabled(exe != null); controls.Add(launch);
            var foot = UIX.Div("settings-footer");
            foot.Add(UIX.Text("Changes save automatically", "setting-hint")); foot.Add(UIX.Div("spacer"));
            var close = UIX.Btn("Done", CloseModal, "btn-primary"); close.name = "settings-done"; foot.Add(close); m.Add(foot);
            SelectPage(false);
        }

        static VisualElement PreferenceToggle(string title, string hint, string name, bool value, Action<bool> set)
        {
            var row = UIX.Div("setting-row"); var copy = UIX.Div("setting-copy");
            copy.Add(UIX.Text(title, "setting-title")); copy.Add(UIX.Text(hint, "setting-hint")); row.Add(copy);
            var toggle = new Toggle { name = name, value = value, tooltip = title }; toggle.AddToClassList("setting-switch");
            var state = UIX.Text(value ? "ON" : "OFF", "setting-state"); state.name = name + "-state";
            toggle.RegisterValueChangedCallback(e => { state.text = e.newValue ? "ON" : "OFF"; set(e.newValue); });
            row.Add(state); row.Add(toggle); return row;
        }

        static VisualElement PreferenceSlider(string title, string hint, string name, float lo, float hi, float value, Action<float> set)
        {
            var row = UIX.Div("setting-row", "setting-range"); var heading = UIX.Div("row");
            heading.Add(UIX.Text(title, "setting-title")); heading.Add(UIX.Div("spacer"));
            var number = UIX.Text(Mathf.RoundToInt(value * 100) + "%", "setting-value"); number.name = name + "-value"; heading.Add(number); row.Add(heading);
            if (hint != null) row.Add(UIX.Text(hint, "setting-hint"));
            var slider = new Slider(lo, hi) { name = name, value = value, tooltip = title }; slider.AddToClassList("setting-slider");
            slider.RegisterValueChangedCallback(e => { number.text = Mathf.RoundToInt(e.newValue * 100) + "%"; set(e.newValue); });
            row.Add(slider); return row;
        }

        public static string CommandfallExe()
        {
            var candidates = new[]
            {
                Args.Value("-rrCommandfallExe"),
                "C:/Users/jacob/Documents/ClaudeCodeProj/Commandfall/.claude/worktrees/rule-ruin-commandfall-units-e88455/Builds/Windows/Commandfall.exe",
                "C:/Users/jacob/Documents/ClaudeCodeProj/Commandfall/Builds/Windows/Commandfall.exe",
            };
            foreach (var c in candidates) if (!string.IsNullOrEmpty(c) && System.IO.File.Exists(c)) return c;
            return null;
        }

    }
}
