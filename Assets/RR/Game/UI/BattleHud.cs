using System.Collections.Generic;
using System.Linq;
using Commandfall.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Command instruments for orbital Commandfall. Reads the authoritative MatchState; actions go to BattlePresentation.
    public sealed class BattleHud
    {
        readonly BattlePresentation _b;
        readonly GameRoot _root;
        public VisualElement Root { get; private set; }
        Label _round, _turn, _obj, _preview;
        VisualElement _left, _right, _rightPanel, _toasts, _floaters, _options;
        Button _ability, _wait, _threat, _speed;
        readonly List<Label> _floatLabels = new List<Label>();
        int _lastSig = -1;

        public BattleHud(GameRoot root, BattlePresentation b, VisualElement parent)
        {
            _root = root; _b = b;
            Root = UIX.Div("layer"); Root.pickingMode = PickingMode.Ignore; parent.Add(Root);
            _floaters = UIX.Div("layer"); _floaters.pickingMode = PickingMode.Ignore; Root.Add(_floaters);
            var top = UIX.Div("battle-top"); Root.Add(top);
            var title = UIX.Text(b.S.Config.Domain==BattleDomain.Planetary?"PLANETARY COMMANDFALL":"ORBITAL COMMANDFALL", "emblem-name"); UIRoot.Display(title); top.Add(title);
            _round = UIX.Text("", "body"); _round.style.marginRight = 24; top.Add(_round);
            _turn = UIX.Text("", "body"); _turn.style.marginRight = 24; top.Add(_turn);
            _obj = UIX.Text("", "body"); top.Add(_obj);
            var rosterPanel = UIX.Div("panel", "battle-left"); Root.Add(rosterPanel);
            var rosterScroll = new ScrollView(ScrollViewMode.Vertical); rosterScroll.style.flexShrink = 1;
            UIRoot.StyleScroll(rosterScroll); rosterPanel.Add(rosterScroll); _left = rosterScroll.contentContainer;
            _rightPanel = UIX.Div("panel", "battle-right"); Root.Add(_rightPanel);
            var detailScroll = new ScrollView(); detailScroll.style.flexShrink = 1; UIRoot.StyleScroll(detailScroll);
            _rightPanel.Add(detailScroll); _right = detailScroll.contentContainer;
            var prev = UIX.Div("battle-preview"); prev.pickingMode = PickingMode.Ignore; Root.Add(prev);
            _preview = UIX.Text("", "panel", "body"); _preview.style.maxWidth = 900; _preview.pickingMode = PickingMode.Ignore; prev.Add(_preview);
            var bar = UIX.Div("battle-actions"); Root.Add(bar);
            _options = UIX.Div("row"); bar.Add(_options);
            _ability = UIX.Btn("Ability (3)", () => _b.ToggleAbility()); bar.Add(_ability);
            _wait = UIX.Btn("Wait (Space)", () => _b.Wait()); bar.Add(_wait);
            _threat = UIX.Btn("Threat map (T)", () => { _b.ShowThreat = !_b.ShowThreat; _b.RefreshCells(); }); bar.Add(_threat);
            if(root.GroundExercise)bar.Add(UIX.Btn("Leave exercise",root.LeaveGroundExercise));
            _speed = UIX.Btn("Animations 1x (F)", () => CycleSpeed()); bar.Add(_speed);
            _toasts = UIX.Div(); _toasts.style.position = Position.Absolute; _toasts.style.top = 62; _toasts.style.left = 300; _toasts.style.right = 370;
            _toasts.style.alignItems = Align.Center; _toasts.pickingMode = PickingMode.Ignore; Root.Add(_toasts);
            _b.Notice += Toast;
        }

        public void CycleSpeed()
        {
            _b.AnimSpeed = _b.AnimSpeed < 1.5f ? 2f : _b.AnimSpeed < 3f ? 4f : 1f;
            _speed.text = "Animations " + _b.AnimSpeed.ToString("0") + "x (F)";
        }

        public void Toast(string text, string kind)
        {
            // Repeated threat notices share one visible message during their lifetime.
            if (_toasts.Children().OfType<Label>().Any(t => t.text == text)) return;
            var t = UIX.Text(text, "panel", "toast", kind == "bad" ? "toast-bad" : kind == "danger" ? "toast-danger" : "toast-ok");
            t.pickingMode = PickingMode.Ignore;
            _toasts.Add(t);
            while (_toasts.childCount > 3) _toasts.RemoveAt(0);
            t.schedule.Execute(() => UiMotion.Leave(t, t.RemoveFromHierarchy)).StartingIn(3200);
        }

        public void Dispose() { _b.Notice -= Toast; Root.RemoveFromHierarchy(); }

        public void Update(Camera cam)
        {
            var S = _b.S;
            int seat = _b.PlayerSeat;
            string speedText = "Animations " + _b.AnimSpeed.ToString("0") + "x (F)";
            if (_speed.text != speedText) _speed.text = speedText;
            _round.text = "Round " + S.Round + " of " + S.Config.RoundLimit;
            bool mine = S.ActiveSeat == seat;
            _turn.text = S.IsOver ? "The battle is over." : mine ? (S.ActiveUnitId != 0 ? "Your activation: finish with " + S.Unit(S.ActiveUnitId).Def.Name : "Your activation: choose a unit") : "Enemy activation";
            _turn.EnableInClassList("turn-friendly", mine); _turn.EnableInClassList("turn-enemy", !mine);
            int heldMine = S.Objectives.Count(o => o.Owner == seat), heldTheirs = S.Objectives.Count(o => o.Owner == 1 - seat);
            _obj.text = "Objectives: you " + heldMine + ", enemy " + heldTheirs + " of " + S.Objectives.Count;
            _preview.text = _b.PreviewText ?? (mine && !S.IsOver ? (_b.Selected == 0 ? "Select one of your units that has not acted this round." : _b.AbilityMode ? AbilityPrompt() : "Gold hexes are legal moves; red hexes are legal targets. Right-click to deselect.") : "");
            _preview.style.display = string.IsNullOrEmpty(_preview.text) ? DisplayStyle.None : DisplayStyle.Flex;
            var u = _b.Selected != 0 ? S.Unit(_b.Selected) : null;
            _ability.text = u != null ? (_b.AbilityMode ? "Cancel " + u.R.ActiveName : u.R.ActiveName + (u.Cooldown > 0 ? " (" + u.Cooldown + ")" : "") + " (3)") : "Ability (3)";
            _ability.SetEnabled(mine && u != null && !_b.Animating);
            _wait.SetEnabled(mine && !_b.Animating);
            if (_b.ShowThreat) _threat.AddToClassList("btn-toggle-on"); else _threat.RemoveFromClassList("btn-toggle-on");
            int sig = S.ActivationIndex * 1000 + _b.Selected * 7 + (_b.AbilityMode ? 1 : 0) + (_b.Animating ? 3 : 0) + S.Units.Sum(x => x.Hp) * 13;
            if (sig != _lastSig) { _lastSig = sig; RebuildPanels(); }
            UpdateFloaters(cam);
        }

        string AbilityPrompt()
        {
            var u = _b.S.Unit(_b.Selected);
            var steps = RosterAbilities.Steps(u, _b.AbilityOption);
            if (_b.Picks.Count < steps.Count) return u.R.ActiveName + ": " + steps[_b.Picks.Count].Prompt + (steps[_b.Picks.Count].Optional ? " (optional; press Enter to confirm)" : "") + ". Right-click undoes the last pick.";
            return u.R.ActiveName + ": press Enter to confirm.";
        }

        void RebuildPanels()
        {
            var S = _b.S;
            int seat = _b.PlayerSeat;
            _left.Clear();
            _left.Add(UIX.Text(S.Config.Players[seat].DisplayName.ToUpperInvariant(), "h-section"));
            foreach (var u in S.Units.Where(x => x.Owner == seat).OrderByDescending(x => S.Players[seat].CommanderId == x.Id).ThenBy(x => x.Id))
                _left.Add(Card(u, true));
            _left.Add(UIX.Text(S.Config.Players[1 - seat].DisplayName.ToUpperInvariant(), "h-section"));
            foreach (var u in S.Units.Where(x => x.Owner != seat).OrderByDescending(x => S.Players[1 - seat].CommanderId == x.Id).ThenBy(x => x.Id))
                _left.Add(Card(u, false));
            _right.Clear();
            var sel = _b.Selected != 0 ? S.Unit(_b.Selected) : null;
            if (sel == null && _b.HoverHex.HasValue) sel = S.UnitAt(_b.HoverHex.Value);
            if (sel == null) { _rightPanel.style.display = DisplayStyle.None; }
            else { _rightPanel.style.display = DisplayStyle.Flex; Detail(sel); }
            _options.Clear();
            var su = _b.Selected != 0 ? S.Unit(_b.Selected) : null;
            if (su != null && su.R.Active != null && su.R.Active.Options != null && su.R.Active.Options.Length > 1)
                for (int i = 0; i < su.R.Active.Options.Length; i++)
                {
                    int opt = i;
                    var b = UIX.Btn(su.R.Active.Options[i], () => { _b.AbilityOption = opt; _b.Picks.Clear(); _b.RefreshCells(); _lastSig = -1; });
                    if (_b.AbilityOption == i) b.AddToClassList("btn-toggle-on");
                    _options.Add(b);
                }
        }

        VisualElement Card(UnitState u, bool mine)
        {
            var S = _b.S;
            var c = UIX.Div("unit-card");
            if (!u.Alive) c.AddToClassList("unit-card-dead");
            else if (RosterRules.Done(S, u)) c.AddToClassList("unit-card-done");
            if (u.Id == _b.Selected || u.Id == S.ActiveUnitId) c.AddToClassList("unit-card-active");
            var row = UIX.Div("row");
            if (S.Players[u.Owner].CommanderId == u.Id) { var ic = UiKit.ContentIcon(IconKind.Crown, 12); ic.style.marginRight = 4; row.Add(ic); }
            var name = UIX.Text(u.Def.Name, "item-title", "unit-name");
            name.style.fontSize = 13;
            row.Add(name);
            row.Add(UIX.Text(u.Alive ? u.Hp + "/" + u.MaxHp : (u.HasR(RStatusType.Captured) ? "captured" : "lost"), "small", "unit-status"));
            c.Add(row);
            if (u.Alive)
            {
                float f = u.Hp / (float)Mathf.Max(1, u.MaxHp);
                c.Add(UIX.Bar(f, f < 0.4f ? "bar-hp-low" : "bar-hp"));
            }
            if (mine && u.Alive) c.RegisterCallback<ClickEvent>(e => _b.Select(u.Id));
            var inspect=UIX.Btn("Inspect",()=>_root.InspectUnit(u.Def.Id),"btn-quiet");inspect.name="battle-inspect-"+u.Id;
            inspect.RegisterCallback<ClickEvent>(e=>e.StopPropagation());c.Add(inspect);
            return c;
        }

        void Detail(UnitState u)
        {
            var S = _b.S;
            var t = UIX.Text(u.Def.Name.ToUpperInvariant(), "h-display"); UIRoot.Display(t); _right.Add(t);
            _right.Add(UIX.Btn("Inspect unit",()=>_root.InspectUnit(u.Def.Id)));
            bool leader = S.Players[u.Owner].CommanderId == u.Id;
            _right.Add(UIX.Text(StrategyHud.RoleText(u.Def) + (leader ? ". Leader: if it is destroyed, its side loses at once." : "."), "subtitle"));
            Row(u.R.IsShip?"Hull":"Health", u.Hp + " of " + u.MaxHp + (u.ShieldHp > 0 ? ", shields " + u.ShieldHp : ""));
            Row("Armor", u.BaseArmor.ToString());
            Row("Movement", StrategyHud.MoveText(u.Def));
            Row("Weapon", u.Weapon.Name + ": " + u.Weapon.Damage + " at " + u.Weapon.MinRange + " to " + u.Weapon.MaxRange);
            Row("Active", u.R.ActiveName + (u.Cooldown > 0 ? ", recharging (" + u.Cooldown + ")" : ", ready"));
            _right.Add(UIX.Text(u.R.ActiveText, "item-sub"));
            Row("Passive", u.R.PassiveName);
            _right.Add(UIX.Text(u.R.PassiveText, "item-sub"));
            var statuses = u.RStatuses != null ? string.Join(", ", u.RStatuses.Select(x => x.Type.ToString())) : "";
            if (!string.IsNullOrEmpty(statuses)) Row("Statuses", statuses);
            if (RosterRules.Done(S, u)) _right.Add(UIX.Text("This unit has already acted this round.", "small"));
        }

        void Row(string k, string v)
        {
            var r = UIX.Div("row"); r.style.marginTop = 2;
            r.Add(UIX.Text(k, "small")); r.Add(UIX.Div("spacer"));
            var l = UIX.Text(v, "body"); l.style.unityTextAlign = TextAnchor.MiddleRight; l.style.maxWidth = 230; r.Add(l);
            _right.Add(r);
        }

        void UpdateFloaters(Camera cam)
        {
            var list = _b.Floaters;
            float now = Time.unscaledTime;
            list.RemoveAll(f => now - f.Born > 1.4f);
            while (_floatLabels.Count < list.Count) { var l = UIX.Text("", "label"); l.style.fontSize = 16; l.style.unityFontStyleAndWeight = FontStyle.Bold; l.pickingMode = PickingMode.Ignore; _floaters.Add(l); _floatLabels.Add(l); }
            var panel = Root.panel;
            for (int i = 0; i < _floatLabels.Count; i++)
            {
                var l = _floatLabels[i];
                if (i >= list.Count || panel == null) { l.style.display = DisplayStyle.None; continue; }
                var f = list[i];
                float age = now - f.Born;
                var sp = cam.WorldToScreenPoint(f.Pos + Vector3.up * (Settings.ReducedMotion?0:MotionCurve.Out(age/.8f)*.55f));
                if (sp.z <= 0) { l.style.display = DisplayStyle.None; continue; }
                var pp = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(sp.x, cam.pixelHeight - sp.y));
                l.style.display = DisplayStyle.Flex;
                l.text = f.Text;
                l.style.color = new Color(f.Color.r, f.Color.g, f.Color.b, Mathf.Clamp01(age/.06f)*Mathf.Clamp01((1.4f-age)/.35f));
                l.style.left = pp.x - 60; l.style.top = pp.y - 10; l.style.width = 120;
            }
        }
    }
}