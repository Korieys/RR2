using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Commandfall.AI;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Automated evidence capture driven through the real game (the same GameRoot methods the interface calls).
    public static class Automation
    {
        public static readonly StringBuilder Log = new StringBuilder();
        static string _dir;

        public static void Note(string line)
        {
            Log.AppendLine(line);
            UnityEngine.Debug.Log("[RR] " + line);
            if (_dir != null) try { File.WriteAllText(Path.Combine(_dir, "tour_log.txt"), Log.ToString()); } catch { }
        }

        public static IEnumerator Shot(string dir, string name)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForEndOfFrame();
            var root = GameRoot.I;
            if (root != null && root.OffscreenScene != null)
            {
                int w = root.OffscreenScene.width, h = root.OffscreenScene.height;
                var scene = Read(root.OffscreenScene, w, h);
                var ui = Read(root.OffscreenUI, w, h);
                for (int i = 0; i < scene.Length; i++)
                {
                    var u = ui[i]; float a = u.a / 255f;
                    var c = scene[i];
                    scene[i] = new Color32((byte)(u.r * a + c.r * (1 - a)), (byte)(u.g * a + c.g * (1 - a)), (byte)(u.b * a + c.b * (1 - a)), 255);
                }
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels32(scene); tex.Apply();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            else ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null; yield return null;
            Note("shot " + name);
        }

        static Color32[] Read(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            var px = t.GetPixels32();
            Object.Destroy(t);
            return px;
        }

        static bool Rig_Pixel(GameRoot r) { return r.Rig != null && r.Rig.Cam != null; }

        static Vector2 PanelToScreen(UnityEngine.UIElements.IPanel panel, Vector2 p, Camera cam)
        {
            var a = RuntimePanelUtils_Screen(panel, Vector2.zero, cam);
            var b = RuntimePanelUtils_Screen(panel, new Vector2(cam.pixelWidth, cam.pixelHeight), cam);
            var k = new Vector2((b.x - a.x) / cam.pixelWidth, (b.y - a.y) / cam.pixelHeight);
            float sx = (p.x - a.x) / k.x, sy = (p.y - a.y) / k.y;
            return new Vector2(sx, cam.pixelHeight - sy);
        }

        static Vector2 RuntimePanelUtils_Screen(UnityEngine.UIElements.IPanel panel, Vector2 screenTopLeftOrigin, Camera cam)
        {
            return UnityEngine.UIElements.RuntimePanelUtils.ScreenToPanel(panel, screenTopLeftOrigin);
        }

        static IEnumerator Wait(float s) { float t = 0; while (t < s) { t += Time.unscaledDeltaTime; yield return null; } }

        static IEnumerator Until(System.Func<bool> cond, float timeout)
        {
            float t = 0;
            while (!cond() && t < timeout) { t += Time.unscaledDeltaTime; yield return null; }
            if (!cond()) Note("timeout waiting (" + timeout + " s)");
        }

        static IEnumerator Settle(CameraRig rig, CamPose pose, float seconds = 0.7f) { rig.Snap(pose); yield return Wait(seconds); }

        public static IEnumerator Measure(string label, int frames)
        {
            var ms = new List<double>();
            var sw = Stopwatch.StartNew();
            double last = 0;
            long gc0 = System.GC.CollectionCount(0);
            long mem0 = System.GC.GetTotalMemory(false);
            for (int i = 0; i < frames; i++) { yield return null; double now = sw.Elapsed.TotalMilliseconds; ms.Add(now - last); last = now; }
            ms.RemoveAt(0);
            ms.Sort();
            Note(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "perf {0}: median {1:0.00} ms ({2:0} fps), p95 {3:0.00} ms, p99 {4:0.00} ms, max {5:0.00} ms over {6} frames; gen0 GCs {7}; managed heap {8:0.0} MB",
                label, ms[ms.Count / 2], 1000.0 / ms[ms.Count / 2], ms[(int)(ms.Count * 0.95)], ms[(int)(ms.Count * 0.99)], ms[ms.Count - 1], ms.Count,
                System.GC.CollectionCount(0) - gc0, System.GC.GetTotalMemory(false) / 1048576.0));
        }

        static void Environment()
        {
            Note("device " + SystemInfo.graphicsDeviceName + ", " + SystemInfo.graphicsDeviceType + ", " + SystemInfo.processorType + ", " + SystemInfo.systemMemorySize + " MB RAM, " + SystemInfo.operatingSystem);
            var root = GameRoot.I;
            string res = root != null && root.OffscreenScene != null ? root.OffscreenScene.width + "x" + root.OffscreenScene.height + " offscreen (window " + Screen.width + "x" + Screen.height + ")" : Screen.width + "x" + Screen.height;
            Note("screen " + res + ", render scale 1.0 (native), quality " + QualitySettings.names[QualitySettings.GetQualityLevel()] +
                 ", vsync " + QualitySettings.vSyncCount + ", target fps " + Application.targetFrameRate + ", UI scale " + Settings.UiScale);
        }

        // The full playable sequence, end to end.
        public static IEnumerator FullTour(GameRoot root, string dir)
        {
            _dir = dir;
            Directory.CreateDirectory(dir);
            var rig = root.Rig;
            var G = root.G;
            Environment();
            string plan = Args.Value("-rrTourPlan") ?? "hold";
            Note("tour plan " + plan);
            yield return Wait(1.5f);

            // 1. Survey the galaxy.
            yield return Settle(rig, root.GalaxyHome, 1.2f);
            yield return Shot(dir, "g01_galaxy_overview");
            yield return Measure("galaxy overview", 300);
            root.ToggleOverlays(); yield return Wait(0.3f);
            yield return Shot(dir, "g02_galaxy_overview_no_overlay");
            root.ToggleOverlays();
            // Map registration: straight down over the whole galaxy, so the faction map can be laid over the frame.
            var galaxyLimits = rig.Limits;
            rig.Limits = new CamLimits { MinDistance = galaxyLimits.MinDistance, MaxDistance = 1200, MinPitch = galaxyLimits.MinPitch, MaxPitch = 89.5f, FocusBounds = galaxyLimits.FocusBounds };
            yield return Settle(rig, new CamPose { Focus = Vector3.zero, Yaw = 0, Pitch = 89.5f, Distance = 860, Fov = 40 }, 1.0f);
            yield return Shot(dir, "g00_map_frame");
            root.ToggleOverlays(); yield return Wait(0.3f);
            yield return Shot(dir, "g00_map_frame_no_overlay");
            root.ToggleOverlays();
            rig.Limits = galaxyLimits;
            var ecl = root.Galaxy.EclipseCenter;
            yield return Settle(rig, new CamPose { Focus = ecl + new Vector3(0, 0, -20), Yaw = 0, Pitch = 55, Distance = 380, Fov = 40 }, 0.8f);
            yield return Shot(dir, "g03_eclipse_sector");
            root.ToggleOverlays(); yield return Wait(0.3f);
            yield return Shot(dir, "g04_eclipse_sector_no_overlay");
            root.ToggleOverlays();
            var aur = GalaxyScale.ToWorld(G.GetSystem(SliceScenario.HeroSystem));
            var spore = GalaxyScale.ToWorld(G.GetSystem("spore_drift"));
            yield return Settle(rig, new CamPose { Focus = (aur + spore) * 0.5f, Yaw = 0, Pitch = 52, Distance = 240, Fov = 40 }, 0.8f);
            yield return Shot(dir, "g05_frontier_approach");
            root.SelectAndFocusFleet(SliceScenario.PlayerFleet);
            yield return Wait(0.9f);
            yield return Shot(dir, "g06_fleet_selected_galaxy");
            // Zoomed in on the player's fleet: the territory mist clears around it.
            var crownPos = root.Galaxy.FleetPosition(G.GetFleet(SliceScenario.PlayerFleet), 0);
            yield return Settle(rig, new CamPose { Focus = crownPos, Yaw = 0, Pitch = 55, Distance = 70, Fov = 40 }, 0.6f);
            yield return Shot(dir, "g06b_fleet_zoomed");
            // Chart-versus-space test at galaxy scale: overlays off, lateral pans and an orbit.
            root.ToggleOverlays();
            var basePose = new CamPose { Focus = aur + new Vector3(30, 0, -20), Yaw = 10, Pitch = 40, Distance = 150, Fov = 40 };
            for (int i = 0; i < 3; i++) { var p = basePose; p.Focus += Quaternion.Euler(0, 10, 0) * new Vector3(-60 + i * 60, 0, 0); yield return Settle(rig, p, 0.35f); yield return Shot(dir, "cv_galaxy_pan_" + i); }
            for (int i = 0; i < 3; i++) { var p = basePose; p.Yaw = 10 + i * 35; yield return Settle(rig, p, 0.35f); yield return Shot(dir, "cv_galaxy_orbit_" + i); }
            root.ToggleOverlays();

            // Camera feel checks: zoom-linked pitch and pan glide, driven through the same calls as player input.
            {
                yield return Settle(rig, root.GalaxyHome, 0.3f);
                float p0 = rig.Target.Pitch;
                rig.Zoom(0.2f);
                yield return Wait(0.6f);
                Note("camera feel: zooming from " + root.GalaxyHome.Distance.ToString("0") + " to " + rig.Current.Distance.ToString("0") + " moved pitch from " +
                     p0.ToString("0.0") + " to " + rig.Current.Pitch.ToString("0.0") + " degrees");
                // A brisk mouse drag: 700 pixels a second for 0.3 s, independent of frame rate.
                for (float tt = 0; tt < 0.3f; tt += Time.unscaledDeltaTime) { rig.Pan(new Vector2(-700f * Time.unscaledDeltaTime, 0)); yield return null; }
                var released = rig.Target.Focus;
                yield return Wait(0.6f);
                Note("camera feel: after a drag ends the focus glides on " + (rig.Target.Focus - released).magnitude.ToString("0.0") + " units" +
                     (Settings.ReducedMotion ? " (reduced motion: expected 0)" : ""));
            }

            // Interaction checks: UI input capture and camera cancellation.
            {
                var panel = root.UI.Root.panel;
                var insp = root.UI.Root.Q(className: "inspector");
                if (panel != null && insp != null && Rig_Pixel(root) )
                {
                    var wb = insp.worldBound;
                    var pp = new Vector2(wb.center.x, wb.center.y);
                    // Panel to screen: the panel scales with screen size; invert ScreenToPanel numerically.
                    var scr = PanelToScreen(panel, pp, root.Rig.Cam);
                    bool over = root.UI.PointerOverUI(scr);
                    bool empty = root.UI.PointerOverUI(new Vector2(root.Rig.Cam.pixelWidth * 0.5f, root.Rig.Cam.pixelHeight * 0.45f));
                    Note("ui capture: pointer over inspector blocks world input = " + over + "; open space passes through = " + !empty);
                }
                var target = new CamPose { Focus = aur, Yaw = 40, Pitch = 45, Distance = 160, Fov = 40 };
                rig.TransitionTo(target, 3f);
                yield return Wait(0.2f);
                rig.FinishTransition();
                yield return null;
                Note("camera cancellation: interrupted transition lands on its target = " + (Mathf.Abs(rig.Current.Distance - 160) < 0.5f && (rig.Current.Focus - aur).magnitude < 0.5f));
            }

            // 2. Enter the hero system (frame sequence through the transition).
            yield return Settle(rig, new CamPose { Focus = aur, Yaw = 10, Pitch = 50, Distance = 120, Fov = 40 }, 0.5f);
            root.Selection.SelectSystem(SliceScenario.HeroSystem);
            root.EnterSystem(SliceScenario.HeroSystem);
            float t0 = Time.unscaledTime; int k = 0;
            foreach (var at in new[] { 0.15f, 0.35f, 0.55f, 0.8f, 1.1f, 1.45f, 1.9f })
            {
                while (Time.unscaledTime - t0 < at) yield return null;
                yield return Shot(dir, "t_enter_" + (k++).ToString("00"));
            }
            yield return Until(() => !root.Busy, 20);
            yield return Wait(0.5f);
            yield return Shot(dir, "s01_hero_system");
            yield return Measure("hero system", 300);
            root.ToggleOverlays(); yield return Wait(0.3f);
            yield return Shot(dir, "s02_hero_system_no_overlay");
            var sp = rig.Current;
            for (int i = 0; i < 3; i++) { var p = sp; p.Yaw += i * 40; p.Pitch = 22; yield return Settle(rig, p, 0.4f); yield return Shot(dir, "cv_system_orbit_" + i); }
            for (int i = 0; i < 2; i++) { var p = sp; p.Focus += Quaternion.Euler(0, sp.Yaw, 0) * new Vector3(-40 + i * 80, 0, 0); yield return Settle(rig, p, 0.4f); yield return Shot(dir, "cv_system_pan_" + i); }
            root.ToggleOverlays();
            rig.Snap(sp);

            // 3. Inspect the hero world and perform a management action.
            root.InspectPlanet(SliceScenario.HeroPlanet);
            yield return Until(() => !rig.Transitioning, 5);
            yield return Wait(0.6f);
            yield return Shot(dir, "p01_planet_inspection");
            yield return Measure("planet inspection", 240);
            var before = G.Player.Stock;
            root.StartProject(SliceScenario.HeroPlanet, ProjectKind.CommissionShip, "silex_lattice_corvette");
            Note("commissioned silex_lattice_corvette: stock " + before + " -> " + G.Player.Stock);
            yield return Wait(0.4f);
            yield return Shot(dir, "p02_project_started");
            var pr = rig.Current;
            for (int i = 1; i <= 2; i++) { var p = pr; p.Yaw += i * 55; yield return Settle(rig, p, 0.4f); yield return Shot(dir, "p03_planet_orbit_" + i); }
            root.ExitPlanet();
            yield return Until(() => !rig.Transitioning, 5);

            // 4. Fleet inspection in the system, then the order.
            root.SelectAndFocusFleet(SliceScenario.PlayerFleet);
            yield return Until(() => !rig.Transitioning, 5);
            yield return Wait(0.4f);
            yield return Shot(dir, "f01_fleet_inspection");
            yield return Measure("fleet close", 200);
            root.ExitToGalaxy();
            t0 = Time.unscaledTime; k = 0;
            foreach (var at in new[] { 0.2f, 0.5f, 0.8f, 1.2f, 1.7f })
            {
                while (Time.unscaledTime - t0 < at) yield return null;
                yield return Shot(dir, "t_exit_" + (k++).ToString("00"));
            }
            yield return Until(() => !root.Busy, 10);
            root.Selection.SelectFleet(G, SliceScenario.PlayerFleet);
            if (plan == "intercept")
            {
                root.IssueMove("cinder_verge");
                yield return Wait(0.4f);
                yield return Shot(dir, "g07_intercept_order");
            }
            else Note("player holds the First Crown Fleet at Aurelith");
            yield return Settle(rig, new CamPose { Focus = (aur + spore) * 0.5f, Yaw = 0, Pitch = 55, Distance = 200, Fov = 40 }, 0.2f);
            root.SetSpeed(3);
            bool shotInbound = false;
            yield return Until(() =>
            {
                var raid = G.GetFleet(SliceScenario.RaidFleet);
                if (!shotInbound && raid != null && raid.Moving && raid.Order.DaysIntoLane >= 2) { shotInbound = true; root.StartCoroutine(Shot(dir, "g08_raid_inbound")); }
                return G.PendingEngagement != null;
            }, 90);
            Note("engagement on day " + G.Day + " at " + (G.PendingEngagement != null ? G.PendingEngagement.SystemId : "none"));

            // 5. Battle preparation.
            yield return Until(() => root.UI.ModalOpen && !root.Busy, 25);
            yield return Wait(0.6f);
            yield return Shot(dir, "b01_battle_preparation");
            var e = G.PendingEngagement;
            var mine = G.GetFleet(e.AttackerFleetId).OwnerId == G.Player.Id ? G.GetFleet(e.AttackerFleetId) : G.GetFleet(e.DefenderFleetId);
            var line = BattleBridge.AutoSelect(G, mine);
            Note("battle line: " + string.Join(", ", line.Select(u => mine.Ships.First(s => s.Uid == u).DefId)) + " (cost " + BattleBridge.Validate(G, mine, line).Cost + ")");
            root.StartBattle(line);
            yield return Until(() => root.Scale == ViewScale.Battle && !root.Busy, 20);
            yield return Wait(0.6f);
            yield return Shot(dir, "b02_battle_start");
            yield return Measure("orbital battle", 240);

            // 6. Play the battle. The tour pilot chooses the player's commands with the same AI the opponent uses and
            //    submits them through BattlePresentation.Submit, the path the interface uses.
            var b = root.Battle;
            var pilot = new RosterAI(AIDifficulty.Tactical, 4242);
            int shots = 0, acts = 0;
            while (!b.S.IsOver && acts < 600)
            {
                if (b.PlayersTurn)
                {
                    var c = pilot.ChooseAction(b.S);
                    if (shots == 0 && c.UnitId != 0) { b.Select(c.UnitId); yield return Wait(0.3f); yield return Shot(dir, "b03_ship_selected"); shots++; }
                    b.Submit(c);
                    acts++;
                    if (acts == 6 || acts == 16) { yield return Until(() => !b.Animating, 10); yield return Shot(dir, "b0" + (3 + shots) + "_battle_" + acts); shots++; }
                    if (acts == 10 && !b.S.IsOver)
                    {
                        // Save in the middle of the battle and load it again through the normal F5 and F9 paths.
                        yield return Until(() => !b.Animating && b.PlayersTurn, 20);
                        if (!b.S.IsOver)
                        {
                            var probe = b.S.ActiveUnitId != 0 ? b.S.ActiveUnitId : b.S.Units.Where(x => x.Owner == b.PlayerSeat && Commandfall.Core.RosterRules.Eligible(b.S, x)).Select(x => x.Id).FirstOrDefault();
                            b.Select(probe);
                            var hexes = b.S.Units.Where(x => x.OnBoard).Select(x => x.Position).ToList();
                            var previewsBefore = hexes.Select(h => b.PreviewAt(h)).ToList();
                            ulong hb = b.Session.Hash; int nb = b.History.Count;
                            root.SaveGame();
                            root.LoadGame();
                            yield return Until(() => root.Battle != null && root.Battle != b && root.Scale == ViewScale.Battle && !root.Busy, 40);
                            b = root.Battle;
                            G = root.G;   // the load replaced the galaxy state; keep checking the live one
                            yield return Until(() => !b.Animating, 10);
                            b.Select(probe);
                            var previewsAfter = hexes.Select(h => b.PreviewAt(h)).ToList();
                            Note("mid-battle save and load after " + nb + " orders: " + (b.Session.Hash == hb && b.History.Count == nb ? "restored the same battle" : "DIFFERENT BATTLE") +
                                 " (hash " + hb.ToString("x16") + "); previews " + (previewsBefore.SequenceEqual(previewsAfter) ? "identical" : "DIFFERENT") + " for " + hexes.Count + " hexes");
                            yield return Wait(0.4f);
                            yield return Shot(dir, "b06_after_midbattle_load");
                        }
                    }
                }
                yield return null;
            }
            Note("battle over: winner seat " + b.S.Winner + " (" + b.S.VictoryReason + ") after round " + b.S.Round + ", " + b.History.Count + " commands");
            var divergence = BattleSession.FindDivergence(b.Session.Snapshot(), out var replayed);
            Note("replay of the battle from its setup and command log: " + (divergence == null && replayed.Hash == Serialization.StateHash(b.S) ? "identical final state" : "DIVERGED: " + divergence) +
                 " (rules " + RulesVersion.Id + ")");
            yield return Until(() => root.ResultPending, 20);
            yield return Wait(0.5f);
            yield return Shot(dir, "b07_result");
            int applied = G.AppliedBattles.Count;
            root.CompleteResult();
            yield return Until(() => root.Scale == ViewScale.System && !root.Busy, 25);
            yield return Wait(0.8f);
            yield return Shot(dir, "r01_after_battle_system");
            var sys = G.GetSystem(e.SystemId);
            Note("after battle: applied battles " + applied + ", system " + sys.Name + " orbit " + sys.OrbitalControlId + ", owner " + sys.OwnerId + ", conquest " + sys.Conquest);
            foreach (var f in G.Fleets) Note("  fleet " + f.Name + " at " + f.SystemId + (f.Moving ? " moving to " + f.Order.Path.Last() : "") + ": " + string.Join(", ", f.Ships.Select(s => s.DefId + " " + s.Hp)));
            root.ExitToGalaxy();
            yield return Until(() => !root.Busy, 10);
            yield return Wait(0.5f);
            yield return Shot(dir, "r02_after_battle_galaxy");

            // 7. Save, change, load: the consequence persists and the loaded state matches the saved one.
            root.SaveGame();
            ulong d1 = SaveGame.Digest(G);
            root.SetSpeed(3);
            yield return Wait(1.5f);
            root.TogglePause();
            ulong d2 = SaveGame.Digest(root.G);
            root.LoadGame();
            yield return Until(() => !root.Busy, 20);
            yield return Wait(0.5f);
            ulong d3 = SaveGame.Digest(root.G);
            Note("save digest " + d1.ToString("x") + ", after more days " + d2.ToString("x") + ", after load " + d3.ToString("x") + ": " + (d1 == d3 ? "load restored the saved state" : "MISMATCH"));
            Note("applied battles after load: " + root.G.AppliedBattles.Count);
            yield return Shot(dir, "r03_after_load");
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Log.ToString());
            yield return Wait(0.5f);
            Application.Quit();
        }


        // Asset review: every ship and station model in-engine with the real shader, one fixed three-quarter view each.
        public static IEnumerator ShipSheet(GameRoot root, string dir)
        {
            _dir = dir;
            Directory.CreateDirectory(dir);
            yield return Wait(1.5f);
            root.Galaxy.gameObject.SetActive(false);
            var stage = new GameObject("Ship stage").transform;
            stage.position = new Vector3(0, 500, 0);
            var lib = ArtLibrary.I;
            foreach (var m in lib.models.Where(x => !x.name.StartsWith("asteroid")))
            {
                var go = Object.Instantiate(m, stage);
                SystemPresentation.ApplyShipMaterial(go, m.name);
                go.transform.localPosition = Vector3.zero;
                var mf = go.GetComponentInChildren<MeshFilter>();
                var b = mf.sharedMesh.bounds;
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                var focus = stage.position + b.center;
                Shader.SetGlobalVector("_RR_StarPosWS", new Vector4(focus.x - 3000, focus.y + 2200, focus.z + 1400, 1));
                Shader.SetGlobalVector("_RR_StarColor", new Color(1f, 0.97f, 0.92f) * 2.2f);
                Shader.SetGlobalVector("_RR_Ambient", new Vector4(0.03f, 0.035f, 0.05f, 0.35f));
                root.Rig.Limits = new CamLimits { MinDistance = 0.1f, MaxDistance = 100, MinPitch = -80, MaxPitch = 80, FocusBounds = new Bounds(focus, Vector3.one * 1000) };
                root.Rig.Snap(new CamPose { Focus = focus, Yaw = 215, Pitch = 22, Distance = size * 1.9f, Fov = 35 });
                root.ShipSheetMode = true;
                yield return Wait(0.25f);
                yield return Shot(dir, "ship_" + m.name);
                root.Rig.Snap(new CamPose { Focus = focus, Yaw = 300, Pitch = 12, Distance = size * 1.9f, Fov = 35 });
                yield return Wait(0.15f);
                yield return Shot(dir, "ship_" + m.name + "_b");
                Object.Destroy(go);
            }
            Application.Quit();
        }


        // Soak: repeated scale transitions; logs memory and live object counts per cycle to expose leaks or duplicates.
        public static IEnumerator Soak(GameRoot root, string dir, int cycles)
        {
            _dir = dir;
            Directory.CreateDirectory(dir);
            Environment();
            yield return Wait(1.5f);
            for (int c = 0; c < cycles; c++)
            {
                root.EnterSystem(SliceScenario.HeroSystem);
                yield return Until(() => !root.Busy, 20);
                root.InspectPlanet(SliceScenario.HeroPlanet);
                yield return Until(() => !root.Rig.Transitioning, 5);
                root.ExitPlanet();
                yield return Until(() => !root.Rig.Transitioning, 5);
                int fleetViews = root.LocalSystem != null ? root.LocalSystem.Fleets.Count : -1;
                int shipViews = root.LocalSystem != null ? root.LocalSystem.Fleets.Values.Sum(f => f.Ships.Count) : -1;
                root.ExitToGalaxy();
                yield return Until(() => !root.Busy, 20);
                System.GC.Collect();
                yield return Wait(0.3f);
                int objects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
                int mats = UnityEngine.Resources.FindObjectsOfTypeAll<Material>().Length;
                long native = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024 * 1024);
                Note("cycle " + (c + 1) + ": transforms " + objects + ", materials " + mats + ", native " + native + " MB, managed " +
                     (System.GC.GetTotalMemory(true) / (1024 * 1024)) + " MB, fleet views in system " + fleetViews + ", ship views " + shipViews +
                     ", galaxy fleet markers " + root.Galaxy.FleetMarkers.Count);
            }
            Application.Quit();
        }

        // Stage B fixed bookmarks for the hero system (kept for comparison runs).
        public static IEnumerator SystemTourFromGalaxy(GameRoot root, string dir)
        {
            _dir = dir;
            Environment();
            yield return Wait(1f);
            root.EnterSystem(SliceScenario.HeroSystem);
            yield return Until(() => !root.Busy, 20);
            var rig = root.Rig;
            var sys = root.LocalSystem;
            var hero = sys.Planet(SliceScenario.HeroPlanet);
            var giant = sys.Planets.FirstOrDefault(p => p.Data.Kind == PlanetKind.GasGiant);
            var fleet = sys.Fleets.Values.FirstOrDefault();
            float R = sys.OuterRadius;
            yield return Settle(rig, new CamPose { Focus = Vector3.zero, Yaw = 205, Pitch = 30, Distance = R * 0.95f, Fov = 42 }, 1.0f);
            yield return Shot(dir, "b01_system_overview");
            System.Func<PlanetView, float> dayYaw = pv => { var d = (sys.Star.position - pv.Root.position).normalized; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 180; };
            if (hero != null)
            {
                yield return Settle(rig, new CamPose { Focus = hero.Root.position, Yaw = dayYaw(hero) + 35, Pitch = 12, Distance = hero.Radius * 3.4f, Fov = 40 });
                yield return Shot(dir, "b03_hero_planet_day");
                yield return Settle(rig, new CamPose { Focus = hero.Root.position, Yaw = dayYaw(hero) + 95, Pitch = 8, Distance = hero.Radius * 2.6f, Fov = 40 });
                yield return Shot(dir, "b04_hero_planet_terminator");
                yield return Settle(rig, new CamPose { Focus = hero.Root.position, Yaw = dayYaw(hero) + 180, Pitch = 18, Distance = hero.Radius * 3.0f, Fov = 40 });
                yield return Shot(dir, "b05_hero_planet_night");
                yield return Settle(rig, new CamPose { Focus = hero.Root.position, Yaw = dayYaw(hero) + 60, Pitch = 40, Distance = hero.Radius * 1.6f, Fov = 35 });
                yield return Shot(dir, "b06_hero_planet_close");
            }
            if (giant != null)
            {
                yield return Settle(rig, new CamPose { Focus = giant.Root.position, Yaw = dayYaw(giant) + 50, Pitch = 16, Distance = giant.Radius * 5.5f, Fov = 40 });
                yield return Shot(dir, "b07_gas_giant_rings");
                yield return Settle(rig, new CamPose { Focus = giant.Root.position, Yaw = dayYaw(giant) + 150, Pitch = 4, Distance = giant.Radius * 5f, Fov = 40 });
                yield return Shot(dir, "b08_gas_giant_backlit");
            }
            if (fleet != null)
            {
                yield return Settle(rig, new CamPose { Focus = fleet.Root.position, Yaw = fleet.Root.eulerAngles.y + 140, Pitch = 14, Distance = 11f, Fov = 40 });
                yield return Shot(dir, "b09_fleet_close");
            }
            if (sys.StationT != null)
            {
                yield return Settle(rig, new CamPose { Focus = sys.StationT.position, Yaw = 200, Pitch = 10, Distance = 14f, Fov = 40 });
                yield return Shot(dir, "b10_station");
            }
            yield return Settle(rig, new CamPose { Focus = sys.Star.position, Yaw = 120, Pitch = 10, Distance = sys.StarRadius * 4f, Fov = 45 });
            yield return Shot(dir, "b11_star");
            File.WriteAllText(Path.Combine(dir, "tour_log.txt"), Log.ToString());
            Application.Quit();
        }
    }
}