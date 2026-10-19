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
        VisualElement _modalLayer, _fade, _loading;
        Label _loadingText;
        VisualElement _loadingBar;
        GameRoot _game;
        IVisualElementScheduledItem _fadeItem;
        public bool ModalOpen { get { return _modalLayer.childCount > 0; } }

        public static void Display(VisualElement e)
        {
            var f = ArtLibrary.I.displayFont;
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
            ApplyScale();
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
            var back = UIX.Div("modal-back");
            var m = UIX.Div("panel", "panel-solid", "modal");
            m.style.width = width;
            m.style.maxWidth = Length.Percent(96);
            back.Add(m);
            _modalLayer.Add(back);
            return m;
        }
        public void CloseModal() { _modalLayer.Clear(); }

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
            var rightScroll = new ScrollView(ScrollViewMode.Vertical); rightScroll.style.width = 330; rightScroll.style.flexShrink = 1; cols.Add(rightScroll);
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
                    var row = UIX.Div("ship-row", on ? "ship-row-on" : "ship-row-off");
                    var nameCol = UIX.Div("col-name");
                    var nm = UIX.Div("row");
                    if (ShipCatalog.IsLeader(s.DefId)) { var c = new Icon(IconKind.Crown, Look.GoldBright, 13); c.style.marginRight = 5; nm.Add(c); }
                    var prof = G.Slice?.Profile(s.Uid);
                    nm.Add(UIX.Text(prof?.Name != null ? prof.Name + " (" + d.Name + ")" : d.Name, "item-title"));
                    nameCol.Add(nm);
                    if (prof != null && prof.Modules.Count > 0)
                        nameCol.Add(UIX.Text("Fitted: " + string.Join(", ", prof.Modules.Select(x => Commandfall.Core.SliceRules.Module(x).Name)) + ".", "item-sub"));
                    nameCol.Add(UIX.Text(StrategyHud.RoleText(d) + ", tier " + d.Roster.Tier + (ShipCatalog.IsLeader(s.DefId) ? ". Leader: its loss ends the battle." : ""), "item-sub"));
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
                errors.style.color = report.Valid ? Look.Positive : new Color(0.95f, 0.7f, 0.65f);
                if (go != null) go.SetEnabled(report.Valid);
            };
            left.Add(UIX.Text("YOUR FORCE: " + mine.Name.ToUpperInvariant(), "h-section"));
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; sv.style.flexShrink = 1; sv.Add(rows); left.Add(sv);
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
                var it = UIX.Div("item");
                var line = UIX.Div("row");
                if (ShipCatalog.IsLeader(s.DefId)) { var c = new Icon(IconKind.Crown, Look.GoldBright, 13); c.style.marginRight = 5; line.Add(c); }
                line.Add(UIX.Text(d.Name, "item-title")); line.Add(UIX.Div("spacer")); line.Add(UIX.Text(s.Hp + "/" + d.MaxHp, "small"));
                it.Add(line);
                it.Add(UIX.Text(StrategyHud.RoleText(d) + ", armor " + d.Armor + ", " + d.Weapon.Damage + " damage at " + d.Weapon.MinRange + " to " + d.Weapon.MaxRange + ". " + d.Roster.ActiveName + ".", "item-sub"));
                right.Add(it);
            }
            right.Add(UIX.Text("BATTLEFIELD", "h-section"));
            right.Add(UIX.Text(map.Name + ". " + map.Description, "item-sub"));
            right.Add(UIX.Text("Asteroid fields give cover, nebulae shorten weapon range, wrecks block movement and sight, and ion storms damage ships that stop inside them.", "item-sub"));
            var foot = UIX.Div("row"); foot.style.marginTop = 12; foot.style.flexShrink = 0;
            var info = UIX.Div(); info.style.flexShrink = 1; info.Add(budget); info.Add(errors); foot.Add(info);
            foot.Add(UIX.Div("spacer"));
            foot.Add(UIX.Btn("Suggest a line", () => { selection.Clear(); selection.AddRange(BattleBridge.AutoSelect(G, mine)); rebuild(); }));
            if (G.Slice != null && G.Slice.Active) foot.Add(UIX.Btn("Offer terms instead", () => _game.CampaignOfferBeforeBattle()));
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
            t.style.color = won ? Look.GoldBright : Look.Danger;
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
                    var line = UIX.Text(ShipCatalog.Name(defId) + ": " + state + ".", "item-sub");
                    if (so.Destroyed || so.Captured) line.style.color = new Color(0.95f, 0.7f, 0.65f);
                    col.Add(line);
                }
                cols.Add(col);
            }
            m.Add(cols);
            var foot = UIX.Div("row"); foot.style.marginTop = 12; foot.Add(UIX.Div("spacer"));
            foot.Add(UIX.Btn("Return to the system", () => { CloseModal(); done(); }, "btn-primary"));
            m.Add(foot);
        }

        // ------------------------------------------------------------------ settings

        public void ShowSettings()
        {
            var m = OpenModal(560);
            var t = UIX.Text("SETTINGS", "modal-title"); Display(t); m.Add(t);
            var reduced = new Toggle("Reduced motion: shorter transitions and calmer ambient movement") { value = Settings.ReducedMotion };
            reduced.RegisterValueChangedCallback(e => { Settings.ReducedMotion = e.newValue; Settings.Save(); });
            m.Add(reduced);
            var mute = new Toggle("Mute all sound") { value = Settings.Muted };
            mute.RegisterValueChangedCallback(e => { Settings.Muted = e.newValue; Settings.Save(); _game.Audio.ApplyVolumes(); });
            m.Add(mute);
            m.Add(Slider("Interface size", 0.85f, 1.4f, Settings.UiScale, v => { Settings.UiScale = v; Settings.Save(); ApplyScale(); }));
            m.Add(Slider("Master volume", 0, 1, Settings.Master, v => { Settings.Master = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            m.Add(Slider("Music", 0, 1, Settings.Music, v => { Settings.Music = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            m.Add(Slider("Effects", 0, 1, Settings.Effects, v => { Settings.Effects = v; Settings.Save(); _game.Audio.ApplyVolumes(); }));
            m.Add(UIX.Text("Controls: left click selects, double click enters a system, right click orders the selected fleet, the mouse wheel zooms, " +
                           "right or middle drag orbits, WASD pans, Home recenters, Escape returns to the previous scale, Space pauses and Tab toggles the overlay.", "small"));
            // Planetary Commandfall stays its own game (Commandfall 0.15); the slice launches the existing build when present.
            m.Add(UIX.Text("PLANETARY COMMANDFALL", "h-section"));
            string exe = CommandfallExe();
            var launch = UIX.Btn("Open planetary Commandfall (separate game)", () =>
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { WorkingDirectory = System.IO.Path.GetDirectoryName(exe) }); }
                catch (Exception ex) { Debug.LogWarning("[RR] could not start Commandfall: " + ex.Message); }
            });
            launch.SetEnabled(exe != null);
            m.Add(launch);
            m.Add(UIX.Text(exe != null ? "Ground battles use the existing Commandfall build at " + exe + "." : "The Commandfall build was not found. Pass -rrCommandfallExe with its path to enable this.", "small"));
            var foot = UIX.Div("row"); foot.Add(UIX.Div("spacer")); foot.Add(UIX.Btn("Close", CloseModal, "btn-primary")); m.Add(foot);
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

        static VisualElement Slider(string label, float lo, float hi, float v, Action<float> set)
        {
            var s = new Slider(label, lo, hi) { value = v };
            s.RegisterValueChangedCallback(e => set(e.newValue));
            return s;
        }
    }
}