using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    public sealed partial class GameRoot
    {
        // ------------------------------------------------------------------ scale transitions

        public void EnterSystem(string id) { if (!Busy) StartCoroutine(EnterSystemRoutine(id)); }
        public void ExitToGalaxy() { if (!Busy) StartCoroutine(ExitToGalaxyRoutine()); }

        RenderTexture _frame;

        // Captures the 3D scene without the HUD (hidden for one frame) for the cross-fade between scales.
        IEnumerator CaptureClean()
        {
            UI.HudLayer.style.visibility = UnityEngine.UIElements.Visibility.Hidden;
            yield return null;
            yield return new WaitForEndOfFrame();
            int fw = Rig.Cam.pixelWidth, fh = Rig.Cam.pixelHeight;
            if (_frame == null || _frame.width != fw || _frame.height != fh)
            {
                if (_frame != null) _frame.Release();
                _frame = new RenderTexture(fw, fh, 0, RenderTextureFormat.ARGB32) { name = "Transition frame" };
            }
            if (Rig.Cam.targetTexture != null) Graphics.Blit(Rig.Cam.targetTexture, _frame);
            else ScreenCapture.CaptureScreenshotIntoRenderTexture(_frame);
            UI.HudLayer.style.visibility = UnityEngine.UIElements.Visibility.Visible;
        }

        public IEnumerator EnterSystemRoutine(string id)
        {
            if (Busy) yield break;
            var sys = G.GetSystem(id);
            if (sys == null) yield break;
            Busy = true; Targeting = false; UI.Strategy.Hint(null);
            Selection.SelectSystem(id);
            float dive = Settings.ReducedMotion ? 0.12f : 0.6f;
            if (Scale == ViewScale.Galaxy)
            {
                _galaxyBookmark = Rig.Current; _haveGalaxyBookmark = true;
                Galaxy.SetOverlay(false);
                var to = Rig.Current; to.Focus = GalaxyScale.ToWorld(sys); to.Distance = 9f; to.Pitch = Mathf.Clamp(to.Pitch - 10, 30, 60); to.Fov = 34;
                Rig.TransitionTo(to, dive);
                while (Rig.Transitioning) yield return null;
                Audio.Play("transition_in", 0.7f);
            }
            yield return CaptureClean();
            var frame = _frame;
            UI.FadeFrom(frame, 60f);   // hold the captured frame while the system is prepared
            var yaw = Rig.Current.Yaw; var pitch = Rig.Current.Pitch;
            Galaxy.gameObject.SetActive(false);
            if (LocalSystem != null && LocalSystem.Data.Id != id) { Destroy(LocalSystem.gameObject); LocalSystem = null; }
            if (LocalSystem == null)
            {
                LocalSystem = SystemPresentation.Build(G, sys, WorldRoot);
                LoadingText = "Charting " + sys.Name;
                yield return LocalSystem.Load((p, t) => { LoadingProgress = p; LoadingText = t; });
            }
            LocalSystem.gameObject.SetActive(true);
            LocalSystem.SetOverlay(Settings.Overlays);
            LocalSystem.RefreshFleets();
            LoadingText = null; UI.HideLoading();
            float R = LocalSystem.OuterRadius;
            Rig.Limits = new CamLimits { MinDistance = 1.2f, MaxDistance = R * 3.2f, MinPitch = -20, MaxPitch = 82, FocusBounds = new Bounds(Vector3.zero, Vector3.one * R * 2.4f) };
            Rig.Near = 0.05f; Rig.Far = 8000;
            // The local sky carries the galaxy this system sits in: a band tinted by its own and its neighbours' sectors.
            var secA = sys.SectorId != null ? G.GetSector(sys.SectorId) : null;
            var tintA = secA != null && secA.OwnerId != null ? Look.PolityColor(G, secA.OwnerId) : new Color(0.35f, 0.32f, 0.6f);
            Sky.SetColor("_BandTintA", tintA * 0.8f + new Color(0.1f, 0.1f, 0.12f));
            Sky.SetColor("_BandTintB", (secA != null && secA.OwnerId == "silex" ? Look.PolityColor(G, "craigon") : new Color(0.45f, 0.5f, 0.75f)) * 0.7f);
            Sky.SetFloat("_BandStrength", 0.9f); Sky.SetFloat("_StarStrength", 1.15f);
            Sky.SetVector("_GalNormal", new Vector4(0.12f, 0.95f, 0.28f, 0));
            Scale = ViewScale.System;
            // Start far out along the same view direction (the star sits where it was in the galaxy frame), then dolly in.
            Rig.Snap(new CamPose { Focus = Vector3.zero, Yaw = yaw, Pitch = pitch, Distance = R * 5.5f, Fov = 34 });
            yield return null;
            UI.FadeFrom(frame, Settings.ReducedMotion ? 0.15f : 0.5f);
            Rig.TransitionTo(new CamPose { Focus = LocalSystem.FramingFocus, Yaw = yaw, Pitch = 22, Distance = R * 0.7f, Fov = 42 }, Settings.ReducedMotion ? 0.15f : 1.05f);
            BuildSystemLabels();
            Audio.Bed("amb_system");
            UI.Strategy.RefreshAll();
            while (Rig.Transitioning) yield return null;
            _systemBookmark = Rig.Current;
            Busy = false;
            
        }

        public IEnumerator ExitToGalaxyRoutine()
        {
            if (Busy || LocalSystem == null) yield break;
            Busy = true; Targeting = false;
            var sys = LocalSystem.Data;
            float R = LocalSystem.OuterRadius;
            var yaw = Rig.Current.Yaw;
            var pitch = Mathf.Clamp(Rig.Current.Pitch, 30, 70);
            Scale = ViewScale.System;
            Rig.TransitionTo(new CamPose { Focus = Vector3.zero, Yaw = yaw, Pitch = pitch, Distance = R * 5.5f, Fov = 34 }, Settings.ReducedMotion ? 0.12f : 0.6f);
            Audio.Play("transition_out", 0.6f);
            while (Rig.Transitioning) yield return null;
            yield return CaptureClean();
            var frame = _frame;
            UI.FadeFrom(frame, Settings.ReducedMotion ? 0.15f : 0.45f);
            LocalSystem.gameObject.SetActive(false);
            Galaxy.gameObject.SetActive(true);
            SetGalaxyLimits();
            Scale = ViewScale.Galaxy;
            Rig.Snap(new CamPose { Focus = GalaxyScale.ToWorld(sys), Yaw = yaw, Pitch = pitch, Distance = 9f, Fov = 34 });
            var back = _haveGalaxyBookmark ? _galaxyBookmark : GalaxyHome;
            back.Focus = Vector3.Lerp(back.Focus, GalaxyScale.ToWorld(sys), 0.5f);
            Rig.TransitionTo(back, Settings.ReducedMotion ? 0.15f : 1.0f);
            Sky.SetFloat("_BandStrength", 0.05f); Sky.SetFloat("_StarStrength", 0.7f);
            Galaxy.SetOverlay(Settings.Overlays);
            Galaxy.RefreshFleets(Clock.Fraction);
            BuildGalaxyLabels();
            Audio.Bed("amb_galaxy");
            UI.Strategy.RefreshAll();
            while (Rig.Transitioning) yield return null;
            Busy = false;
            
        }

        public void InspectPlanet(string planetId)
        {
            var p = G.GetPlanet(planetId);
            if (p == null || Busy) return;
            var sys = G.SystemOf(p);
            if (Scale == ViewScale.Galaxy || LocalSystem == null || LocalSystem.Data.Id != sys.Id) { StartCoroutine(EnterThenInspect(sys.Id, planetId)); return; }
            var pv = LocalSystem.Planet(planetId);
            if (pv == null) return;
            if (Scale == ViewScale.System) _systemBookmark = Rig.Current;
            Selection.SelectPlanet(G, planetId);
            Scale = ViewScale.Planet;
            var toStar = (LocalSystem.Star.position - pv.Root.position).normalized;
            float dayYaw = Mathf.Atan2(toStar.x, toStar.z) * Mathf.Rad2Deg + 180f;
            Rig.Limits = new CamLimits { MinDistance = pv.Radius * 1.25f, MaxDistance = pv.Radius * 9f, MinPitch = -45, MaxPitch = 75, FocusBounds = new Bounds(pv.Root.position, Vector3.one * 1e4f) };
            Rig.TransitionTo(new CamPose { Focus = pv.Root.position, Yaw = dayYaw + 32, Pitch = 14, Distance = pv.Radius * 3.1f, Fov = 38 }, Settings.ReducedMotion ? 0.12f : 0.95f);
            Audio.Play("transition_in", 0.4f);
            BuildSystemLabels();
            UI.Strategy.RefreshAll();
        }

        IEnumerator EnterThenInspect(string sysId, string planetId)
        {
            yield return EnterSystemRoutine(sysId);
            InspectPlanet(planetId);
        }

        public void ExitPlanet()
        {
            if (Scale != ViewScale.Planet || LocalSystem == null) return;
            Scale = ViewScale.System;
            float R = LocalSystem.OuterRadius;
            Rig.Limits = new CamLimits { MinDistance = 1.2f, MaxDistance = R * 3.2f, MinPitch = -20, MaxPitch = 82, FocusBounds = new Bounds(Vector3.zero, Vector3.one * R * 2.4f) };
            Rig.TransitionTo(_systemBookmark.Distance > 0 ? _systemBookmark : new CamPose { Focus = Vector3.zero, Yaw = 205, Pitch = 30, Distance = R * 0.95f, Fov = 42 }, Settings.ReducedMotion ? 0.12f : 0.8f);
            var planet = Selection.PlanetId;
            Selection.SelectSystem(LocalSystem.Data.Id);
            BuildSystemLabels();
            UI.Strategy.RefreshAll();
        }

        public void Recenter()
        {
            if (Scale == ViewScale.Galaxy)
            {
                var focus = Selection.FleetId != null && Galaxy.FleetMarkers.ContainsKey(Selection.FleetId) ? Galaxy.FleetMarkers[Selection.FleetId].position
                          : Selection.SystemId != null ? GalaxyScale.ToWorld(G.GetSystem(Selection.SystemId)) : GalaxyHome.Focus;
                var to = Rig.Current; to.Focus = focus; to.Yaw = 0; to.Pitch = 58;
                Rig.TransitionTo(to, 0.35f);
            }
            else if (Scale == ViewScale.System) Rig.TransitionTo(new CamPose { Focus = LocalSystem.FramingFocus, Yaw = Rig.Current.Yaw, Pitch = 24, Distance = LocalSystem.OuterRadius * 0.62f, Fov = 42 }, 0.35f);
            else if (Scale == ViewScale.Planet) { var pv = LocalSystem.Planet(Selection.PlanetId); if (pv != null) { var to = Rig.Current; to.Distance = pv.Radius * 3.1f; to.Pitch = 14; Rig.TransitionTo(to, 0.35f); } }
            else if (Scale == ViewScale.Battle) Rig.TransitionTo(BattlePose(), 0.35f);
        }

        public void Back()
        {
            if (UI.ModalOpen && G.PendingEngagement == null) { UI.CloseModal(); return; }
            if (Targeting) { Targeting = false; UI.Strategy.Hint(null); UI.Strategy.RefreshInspector(); return; }
            if (Rig.Transitioning) { Rig.FinishTransition(); return; }
            if (Scale == ViewScale.Planet) ExitPlanet();
            else if (Scale == ViewScale.System) ExitToGalaxy();
        }

        // ------------------------------------------------------------------ engagements and battle

        public void OpenEngagement()
        {
            var e = G.PendingEngagement;
            if (e == null) return;
            if (Busy) { StartCoroutine(WaitThenOpen()); return; }
            if (Scale == ViewScale.Galaxy || LocalSystem == null || LocalSystem.Data.Id != e.SystemId || Scale == ViewScale.Planet)
            {
                StartCoroutine(EnterThenOpen(e.SystemId));
                return;
            }
            var mine = G.GetFleet(e.AttackerFleetId).OwnerId == G.Player.Id ? G.GetFleet(e.AttackerFleetId) : G.GetFleet(e.DefenderFleetId);
            var sel = BattleBridge.AutoSelect(G, mine);
            UI.ShowBattlePrep(e, sel, chosen => StartBattle(chosen), () =>
            {
                var r = StrategyRules.Retreat(G, e.Id);
                UI.CloseModal();
                Feedback(r, e.ResultSummary ?? "Withdrawn.");
                OnDay(new DayReport());
            });
            Audio.Play("battle_prep", 0.6f);
        }

        IEnumerator WaitThenOpen() { while (Busy) yield return null; OpenEngagement(); }
        IEnumerator EnterThenOpen(string sysId)
        {
            if (Scale == ViewScale.Planet) ExitPlanet();
            if (LocalSystem == null || LocalSystem.Data.Id != sysId || Scale == ViewScale.Galaxy)
            {
                if (Scale == ViewScale.System) yield return ExitToGalaxyRoutine();
                yield return EnterSystemRoutine(sysId);
            }
            OpenEngagement();
        }

        public CamPose BattlePose() => new CamPose { Focus = Battle.transform.position + new Vector3(0, 0, 1.6f), Yaw = 0, Pitch = 34, Distance = 26.5f, Fov = 40 };

        public void StartBattle(List<int> selection) { if (!Busy) StartCoroutine(StartBattleRoutine(selection)); }

        IEnumerator StartBattleRoutine(List<int> selection)
        {
            var e = G.PendingEngagement;
            string err;
            _setup = BattleBridge.Prepare(G, e, selection, out err);
            if (_setup == null) { UI.Strategy.Toast(err, "bad"); Audio.Play("ui_invalid"); yield break; }
            var session = BattleSession.Begin(_setup, out err);
            if (session == null) { UI.Strategy.Toast(err, "bad"); yield break; }
            UI.CloseModal();
            Audio.Play("battle_commit");
            yield return EnterBattleRoutine(session);
        }

        /// <summary>Shows a battle session in the current system: a new battle or one restored from a save.</summary>
        IEnumerator EnterBattleRoutine(BattleSession session)
        {
            _setup = session.Setup;
            Busy = true;
            yield return CaptureClean();
            var frame = _frame;
            UI.FadeFrom(frame, 60f);
            LocalSystem.gameObject.SetActive(false);
            Battle = BattlePresentation.Build(this, session, G.GetSystem(BattleSystemId), WorldRoot);
            Battle.transform.position = new Vector3(0, 0, 0);
            Scale = ViewScale.Battle;
            _resultShown = false;
            Rig.Limits = new CamLimits { MinDistance = 9, MaxDistance = 52, MinPitch = 24, MaxPitch = 82, FocusBounds = new Bounds(Vector3.zero, new Vector3(26, 2, 18)) };
            Rig.Near = 0.1f; Rig.Far = 9000;
            var pose = BattlePose();
            var start = pose; start.Distance = 46; start.Pitch = 34;
            Rig.Snap(start);
            UI.Strategy.Root.style.display = UnityEngine.UIElements.DisplayStyle.None;
            UI.Strategy.ClearLabels();
            _battleHud = new BattleHud(this, Battle, UI.HudLayer);
            _slicePanel = new SliceBattlePanel(Battle, UI.HudLayer);
            yield return null;
            UI.FadeFrom(frame, Settings.ReducedMotion ? 0.15f : 0.55f);
            Rig.TransitionTo(pose, Settings.ReducedMotion ? 0.15f : 1.1f);
            Audio.Bed("amb_battle", 0.6f);
            while (Rig.Transitioning) yield return null;
            Busy = false;
            
        }

        void FinishBattle()
        {
            if (_setup.EngagementId == null) { FinishSkirmish(); return; }
            _resultShown = true;
            var o = BattleBridge.Extract(_setup, Battle.S);
            var e = G.GetEngagement(_setup.EngagementId);
            var sys = G.GetSystem(e.SystemId);
            var r = BattleBridge.Apply(G, _setup, o);     // applied once, keyed by battle id
            if (!r.Ok) Automation.Note("battle result rejected: " + r.Reason);
            string consequence;
            if (sys.Blockaded) consequence = G.GetPolity(sys.OrbitalControlId).Name + " now holds the orbit of " + sys.Name + ". Its worlds are blockaded but remain " + G.GetPolity(sys.OwnerId).Name + "; a conquest would still need landing and ground campaigns.";
            else if (sys.OwnerId == G.Player.Id) consequence = "The orbit of " + sys.Name + " remains in Silex hands. The surviving raiders are falling back.";
            else consequence = "The surviving ships of the defeated fleet are withdrawing from " + sys.Name + ".";
            _resultDone = () => { _resultDone = null; StartCoroutine(EndBattleRoutine()); };
            UI.ShowResult(o, _setup, e.ResultSummary, consequence, () => { if (_resultDone != null) _resultDone(); });
        }

        Action _resultDone;
        public bool ResultPending { get { return _resultDone != null; } }
        public BattleSetup CurrentSetup { get { return _setup; } }
        public void CompleteResult() { UI.CloseModal(); if (_resultDone != null) _resultDone(); }

        IEnumerator EndBattleRoutine()
        {
            Busy = true;
            yield return CaptureClean();
            var frame = _frame;
            UI.FadeFrom(frame, 60f);
            if (_battleHud != null) { _battleHud.Dispose(); _battleHud = null; }
            if (_slicePanel != null) { _slicePanel.Dispose(); _slicePanel = null; }
            Destroy(Battle.gameObject); Battle = null;
            UI.Strategy.Root.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
            var sysId = BattleSystemId;
            if (LocalSystem == null || LocalSystem.Data.Id != sysId) { if (LocalSystem != null) Destroy(LocalSystem.gameObject); LocalSystem = null; }
            Scale = ViewScale.Galaxy;
            Busy = false;
            yield return EnterSystemRoutine(sysId);
            UI.FadeFrom(frame, 0.4f);
            Galaxy.RefreshFleets(0); Galaxy.RefreshOwnership();
            Selection.Validate(G);
            UI.Strategy.RefreshAll();
            
        }

        // ------------------------------------------------------------------ save and load

        public void SaveGame()
        {
            bool inBattle = Scale == ViewScale.Battle && Battle != null;
            if (inBattle && (Busy || Battle.Animating || _resultShown)) { Battle_Notice("Wait for the current action to finish before saving.", "bad"); return; }
            try
            {
                // A battle in progress is saved as its setup, accepted commands and hashes, never as presentation state.
                if (inBattle) G.ActiveBattle = Battle.Session.Snapshot();
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath, SaveGame_Json());
                if (inBattle) Battle_Notice("Saved during the battle, after " + Battle.History.Count + " orders.", "ok");
                else UI.Strategy.Toast("Saved. " + GalaxyState.DateLabel(G.Day) + ".", "ok");
                Audio.Play("ui_select", 0.6f);
            }
            catch (Exception ex) { Battle_Notice("The game could not be saved: " + ex.Message, "bad"); }
            finally { G.ActiveBattle = null; }
        }

        void Battle_Notice(string text, string kind)
        {
            if (Scale == ViewScale.Battle && Battle != null) Battle.Announce(text, kind); else UI.Strategy.Toast(text, kind);
        }

        string SaveGame_Json() => RR.Strategy.SaveGame.ToJson(G);

        public void LoadGame()
        {
            if (Busy || (Scale == ViewScale.Battle && (Battle == null || Battle.Animating || _resultShown)))
            { Battle_Notice("Finish the current action before loading.", "bad"); return; }
            if (!File.Exists(SavePath)) { Battle_Notice("There is no saved game yet.", "bad"); return; }
            string err;
            var loaded = RR.Strategy.SaveGame.FromJson(File.ReadAllText(SavePath), out err);
            if (loaded == null) { Battle_Notice(err, "bad"); return; }
            BattleSession resume = null;
            if (loaded.ActiveBattle != null)
            {
                // Rebuilt by replay under the current rules; a record that does not replay exactly is refused, never rerolled.
                resume = BattleSession.Restore(loaded.ActiveBattle, out err);
                if (resume == null) { Battle_Notice(err, "bad"); return; }
                loaded.ActiveBattle = null;
            }
            StartCoroutine(ReplaceState(loaded, resume));
        }

        /// <summary>Removes the battle view without resolving the battle (used when a save replaces it).</summary>
        void DiscardBattleView()
        {
            if (_battleHud != null) { _battleHud.Dispose(); _battleHud = null; }
            if (_slicePanel != null) { _slicePanel.Dispose(); _slicePanel = null; }
            if (Battle != null) { Destroy(Battle.gameObject); Battle = null; }
            _resultShown = false; _resultDone = null;
            UI.CloseModal();
            UI.Strategy.Root.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
        }

        IEnumerator ReplaceState(GalaxyState loaded, BattleSession resume = null)
        {
            Busy = true;
            yield return CaptureClean();
            var frame = _frame;
            UI.FadeFrom(frame, 60f);
            DiscardBattleView();
            G = loaded;
            Clock.Paused = true; Clock.Fraction = 0;
            string sysId = LocalSystem != null && Scale != ViewScale.Galaxy ? LocalSystem.Data.Id : null;
            if (LocalSystem != null) { Destroy(LocalSystem.gameObject); LocalSystem = null; }
            Destroy(Galaxy.gameObject);
            Galaxy = GalaxyPresentation.Build(G, WorldRoot);
            var old = UI.Strategy.Root;
            Selection.SelectFleet(G, G.GetFleet(SliceScenario.PlayerFleet) != null ? SliceScenario.PlayerFleet : null);
            Scale = ViewScale.Galaxy;
            EnterGalaxyImmediate();
            Busy = false;
            if (resume != null) sysId = resume.Setup.SystemId ?? G.GetEngagement(resume.Setup.EngagementId).SystemId;
            if (sysId != null) yield return EnterSystemRoutine(sysId);
            if (resume != null)
            {
                yield return EnterBattleRoutine(resume);
                Battle_Notice("Loaded the battle in progress, after " + resume.Log.Count + " orders.", "ok");
                yield break;
            }
            UI.FadeFrom(frame, 0.4f);
            UI.Strategy.Toast("Loaded the save from " + GalaxyState.DateLabel(G.Day) + ".", "ok");
            UI.Strategy.RefreshAll();
            
        }

        public void ShowSettings() { UI.ShowSettings(); }

        // ------------------------------------------------------------------ labels


        void BuildGalaxyLabels()
        {
            var hud = UI.Strategy;
            hud.ClearLabels();
            foreach (var s in G.Sectors)
            {
                // The faction map sets each sector as emblem, title and subtitle. Emblems are provisional crops of the map
                // legend until the creator's artwork arrives (Resources/RR/Emblems/<polity id>.png).
                var c = GalaxyScale.ToWorld(s.X, s.Y, 0) + Vector3.up * 6;
                var emblem = s.OwnerId != null ? UnityEngine.Resources.Load<Texture2D>("RR/Emblems/" + s.OwnerId) : null;
                if (emblem != null)
                {
                    var ce = c + new Vector3(0, 0, 33);
                    hud.AddEmblem(emblem, () => ce, 190, 330, 5000, 46);
                }
                hud.AddLabel(s.Name.ToUpperInvariant(), () => c, 200, 330, 5000, "label-sector");
                var c2 = c + new Vector3(0, 0, -16);
                hud.AddLabel(s.Eclipse ? "(Unexplored Space)" : s.Subtitle, () => c2, 150, 420, 5000, "label-sector-sub");
            }
            foreach (var s in G.Systems)
            {
                var sys = s;
                var p = GalaxyScale.ToWorld(s) + new Vector3(0, 0, -4.2f);
                int pri = s.Detail == SystemDetail.Hero ? 120 : s.Detail == SystemDetail.Local ? 110 : s.OwnerId == G.Player.Id ? 90 : 60;
                float max = s.Detail != SystemDetail.Distant ? 700 : 420;
                string cls = s.OwnerId == G.Player.Id ? "label-owned" : s.OwnerId == "craigon" ? "label-hostile" : "label-system";
                hud.AddLabel(s.Name, () => p, pri, 0, max, "label-system", cls);
            }
            foreach (var f in G.Fleets.Where(f => f.Ships.Count > 0))
            {
                var id = f.Id;
                hud.AddLabel(f.Name, () => { Transform t; return Galaxy != null && Galaxy.FleetMarkers.TryGetValue(id, out t) && t != null ? t.position + Vector3.up * 2.6f : (Vector3?)null; }, 130, 0, 360, "label-fleet");
            }
        }

        void BuildSystemLabels()
        {
            var hud = UI.Strategy;
            hud.ClearLabels();
            if (LocalSystem == null) return;
            foreach (var pv in LocalSystem.Planets)
            {
                var v = pv;
                hud.AddLabel(pv.Data.Name, () => v.Root.position + Vector3.up * v.Radius * 1.35f, 80, 0, 4000, "label-planet");
            }
            hud.AddLabel(LocalSystem.Data.Name.ToUpperInvariant(), () => LocalSystem != null ? LocalSystem.Star.position + Vector3.up * LocalSystem.StarRadius * 1.6f : (Vector3?)null, 70, 60, 4000, "label-sector");
            foreach (var fv in LocalSystem.Fleets.Values)
            {
                var f = G.GetFleet(fv.FleetId);
                if (f == null) continue;
                var t = fv.Root;
                hud.AddLabel(f.Name, () => t != null ? t.position + Vector3.up * 2.4f : (Vector3?)null, 100, 0, 600, "label-fleet");
            }
        }
    }
}