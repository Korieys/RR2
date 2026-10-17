using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RR.Game
{
    public sealed partial class GameRoot
    {
        Vector2 _pressPos, _rightPress, _lastMouse;
        bool _leftDown, _rightDown, _midDown, _dragged, _rightDragged;
        float _lastClickTime;
        string _lastClickId;
        string _hoverSystem;

        // Synthetic input for automation (drives the same handlers as the mouse).
        public void AutomationClickSystem(string id) { HandleGalaxyClick(id, null, false); }

        void HandleInput()
        {
            var mouse = Mouse.current; var kb = Keyboard.current;
            if (mouse == null || kb == null) return;
            Vector2 pos = mouse.position.ReadValue();
            Vector2 delta = pos - _lastMouse; _lastMouse = pos;
            bool overUI = UI.PointerOverUI(pos);
            bool modal = UI.ModalOpen;

            // ---- keyboard
            if (kb.escapeKey.wasPressedThisFrame) { if (Scale == ViewScale.Battle && Battle != null) Battle.UndoPick(); else Back(); }
            if (kb.tabKey.wasPressedThisFrame && Scale != ViewScale.Battle) ToggleOverlays();
            if (kb.f5Key.wasPressedThisFrame) SaveGame();
            if (kb.f9Key.wasPressedThisFrame) LoadGame();
            if (kb.homeKey.wasPressedThisFrame) Recenter();
            if (Scale == ViewScale.Battle && Battle != null && !modal)
            {
                if (kb.spaceKey.wasPressedThisFrame) Battle.Wait();
                if (kb.digit3Key.wasPressedThisFrame) Battle.ToggleAbility();
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) { if (Battle.AbilityMode) Battle.ConfirmAbility(); }
                if (kb.tKey.wasPressedThisFrame) { Battle.ShowThreat = !Battle.ShowThreat; Battle.RefreshCells(); }
                if (kb.fKey.wasPressedThisFrame && _battleHud != null) _battleHud.CycleSpeed();
            }
            else if (!modal)
            {
                if (kb.spaceKey.wasPressedThisFrame) TogglePause();
                if (kb.digit1Key.wasPressedThisFrame) SetSpeed(1);
                if (kb.digit2Key.wasPressedThisFrame) SetSpeed(2);
                if (kb.digit3Key.wasPressedThisFrame) SetSpeed(3);
            }
            if (Busy || modal) { _leftDown = _rightDown = _midDown = false; return; }

            float dt = Time.unscaledDeltaTime;
            Vector2 pan = Vector2.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) pan.y -= 1;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) pan.y += 1;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) pan.x += 1;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) pan.x -= 1;
            if (pan != Vector2.zero && Scale != ViewScale.Planet) Rig.Pan(pan * 900f * dt);
            if (kb.qKey.isPressed) Rig.Orbit(-90f * dt, 0);
            if (kb.eKey.isPressed) Rig.Orbit(90f * dt, 0);

            // ---- mouse buttons
            if (mouse.leftButton.wasPressedThisFrame && !overUI) { _leftDown = true; _pressPos = pos; _dragged = false; }
            if (mouse.rightButton.wasPressedThisFrame && !overUI) { _rightDown = true; _rightPress = pos; _rightDragged = false; }
            if (mouse.middleButton.wasPressedThisFrame && !overUI) _midDown = true;
            if (_leftDown && (pos - _pressPos).sqrMagnitude > 49) _dragged = true;
            if (_rightDown && (pos - _rightPress).sqrMagnitude > 49) _rightDragged = true;
            if (_leftDown && _dragged && Scale != ViewScale.Planet) Rig.Pan(-delta * 1.0f);
            if (_leftDown && _dragged && Scale == ViewScale.Planet) Rig.Orbit(delta.x * 0.25f, -delta.y * 0.2f);
            if ((_rightDown && _rightDragged) || _midDown) Rig.Orbit(delta.x * 0.25f, -delta.y * 0.2f);
            if (!overUI)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f) { Rig.FinishTransition(); Rig.Zoom(Mathf.Pow(0.86f, scroll / 120f)); }
            }

            // ---- hover and clicks per scale
            if (Scale == ViewScale.Battle && Battle != null)
            {
                var h = overUI ? (Commandfall.Core.Hex?)null : RayHex(pos);
                Battle.Hover(h);
                if (mouse.leftButton.wasReleasedThisFrame && _leftDown && !_dragged && h.HasValue) Battle.ClickHex(h.Value);
                if (mouse.rightButton.wasReleasedThisFrame && _rightDown && !_rightDragged) Battle.UndoPick();
            }
            else if (Scale == ViewScale.Galaxy)
            {
                var sysHit = overUI ? null : PickSystem(pos, 18f);
                var fleetHit = overUI ? null : PickFleet(pos, 22f);
                HoverRoute(sysHit);
                if (mouse.leftButton.wasReleasedThisFrame && _leftDown && !_dragged) HandleGalaxyClick(sysHit, fleetHit, IsDouble(fleetHit ?? sysHit));
                if (mouse.rightButton.wasReleasedThisFrame && _rightDown && !_rightDragged)
                {
                    if (sysHit != null && Selection.FleetId != null && G.GetFleet(Selection.FleetId) != null && G.GetFleet(Selection.FleetId).OwnerId == G.Player.Id) IssueMove(sysHit);
                    else if (Targeting) { Targeting = false; UI.Strategy.Hint(null); }
                }
            }
            else if (LocalSystem != null)
            {
                if (mouse.leftButton.wasReleasedThisFrame && _leftDown && !_dragged && !overUI) HandleSystemClick(pos);
                if (mouse.rightButton.wasReleasedThisFrame && _rightDown && !_rightDragged && Selection.FleetId != null)
                    UI.Strategy.Toast("Fleet movement orders are given on the galaxy map. Press Escape to return to it.", "ok");
            }
            if (mouse.leftButton.wasReleasedThisFrame) _leftDown = false;
            if (mouse.rightButton.wasReleasedThisFrame) _rightDown = false;
            if (mouse.middleButton.wasReleasedThisFrame) _midDown = false;
        }

        bool IsDouble(string id)
        {
            bool dbl = id != null && id == _lastClickId && Time.unscaledTime - _lastClickTime < 0.35f;
            _lastClickId = id; _lastClickTime = Time.unscaledTime;
            return dbl;
        }

        void HandleGalaxyClick(string sysHit, string fleetHit, bool dbl)
        {
            if (Targeting && sysHit != null) { IssueMove(sysHit); return; }
            if (fleetHit != null) { Selection.SelectFleet(G, fleetHit); Audio.Play("ui_select", 0.6f); return; }
            if (sysHit != null)
            {
                if (dbl) { EnterSystem(sysHit); return; }
                bool keepFleet = Selection.FleetId != null && G.GetFleet(Selection.FleetId) != null && G.GetFleet(Selection.FleetId).SystemId == sysHit;
                if (!keepFleet) Selection.SelectSystem(sysHit);
                Audio.Play("ui_select", 0.6f);
            }
        }

        void HoverRoute(string sysHit)
        {
            if (sysHit == _hoverSystem) return;
            _hoverSystem = sysHit;
            var f = Selection.FleetId != null ? G.GetFleet(Selection.FleetId) : null;
            if (f == null || f.OwnerId != G.Player.Id) return;
            if (sysHit == null || sysHit == f.SystemId)
            {
                Galaxy.ShowRoute(f, f.Moving ? f.Order.Path : null);
                if (Targeting) UI.Strategy.Hint("Choose a destination for " + f.Name + ". Click a system to order the move, or press Escape to cancel.");
                return;
            }
            var plan = StrategyRules.PlanRoute(G, f, sysHit);
            if (plan.Legal) Galaxy.ShowRoute(f, plan.Path);
            if (Targeting || plan.Legal)
                UI.Strategy.Hint(plan.Legal ? f.Name + " to " + G.GetSystem(sysHit).Name + ": " + plan.Days + " days through " + plan.Path.Count + " jump" + (plan.Path.Count == 1 ? "" : "s") + ". " + (Targeting ? "Click" : "Right-click") + " to order the move."
                                            : plan.Reason);
        }

        string PickSystem(Vector2 pos, float px)
        {
            string best = null; float bd = px * px;
            foreach (var s in G.Systems)
            {
                var sp = Rig.Cam.WorldToScreenPoint(GalaxyScale.ToWorld(s));
                if (sp.z <= 0) continue;
                float d = ((Vector2)sp - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = s.Id; }
            }
            return best;
        }

        string PickFleet(Vector2 pos, float px)
        {
            string best = null; float bd = px * px;
            foreach (var kv in Galaxy.FleetMarkers)
            {
                var sp = Rig.Cam.WorldToScreenPoint(kv.Value.position);
                if (sp.z <= 0) continue;
                float d = ((Vector2)sp - pos).sqrMagnitude;
                if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }

        void HandleSystemClick(Vector2 pos)
        {
            var cam = Rig.Cam;
            string fleet = null; float bf = 40 * 40;
            foreach (var fv in LocalSystem.Fleets.Values)
            {
                var sp = cam.WorldToScreenPoint(fv.Root.position);
                if (sp.z <= 0) continue;
                float d = ((Vector2)sp - pos).sqrMagnitude;
                if (d < bf) { bf = d; fleet = fv.FleetId; }
            }
            PlanetView planet = null; float bp = float.MaxValue;
            foreach (var pv in LocalSystem.Planets)
            {
                var c = cam.WorldToScreenPoint(pv.Root.position);
                if (c.z <= 0) continue;
                var e = cam.WorldToScreenPoint(pv.Root.position + cam.transform.right * pv.Radius);
                float r = Mathf.Abs(e.x - c.x) + 10;
                float d = ((Vector2)c - pos).magnitude;
                if (d < r && d < bp) { bp = d; planet = pv; }
            }
            if (fleet != null && (planet == null || bf < bp * bp)) { Selection.SelectFleet(G, fleet); Audio.Play("ui_select", 0.6f); IsDouble(fleet); return; }
            if (planet != null)
            {
                bool dbl = IsDouble(planet.Data.Id);
                if (dbl || Scale == ViewScale.Planet) InspectPlanet(planet.Data.Id);
                else { Selection.SelectPlanet(G, planet.Data.Id); Audio.Play("ui_select", 0.6f); }
                return;
            }
            var star = cam.WorldToScreenPoint(LocalSystem.Star.position);
            if (star.z > 0 && ((Vector2)star - pos).magnitude < 40) { Selection.SelectSystem(LocalSystem.Data.Id); Audio.Play("ui_select", 0.6f); }
        }

        Commandfall.Core.Hex? RayHex(Vector2 pos)
        {
            var ray = Rig.Cam.ScreenPointToRay(pos);
            var plane = new Plane(Vector3.up, Battle.transform.position);
            float t;
            if (!plane.Raycast(ray, out t)) return null;
            var local = Battle.transform.InverseTransformPoint(ray.GetPoint(t));
            var h = BattlePresentation.FromWorld(local);
            return Battle.S.Map.Contains(h) ? h : (Commandfall.Core.Hex?)null;
        }
    }
}