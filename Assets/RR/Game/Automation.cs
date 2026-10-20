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

        // Start the deadline now: the preceding capture frame must not count toward a new wait.
        static IEnumerator Wait(float s) { float end=Time.unscaledTime+Mathf.Max(0,s); while(Time.unscaledTime<end) yield return null; }

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

        // Reproducible presentation checks, separate from the full campaign tour.
        // Exercises real scale transitions and checks instrument bounds at enlarged UI scale.
        static int _reviewFailures;
        static void CheckReview(bool ok, string description)
        {
            Note((ok ? "PASS " : "FAIL ") + description);
            if (!ok) _reviewFailures++;
        }

        static void CheckLayout(GameRoot root, string label)
        {
            var view = root.UI.Root.worldBound;
            foreach (var cls in new[] { "topbar", "outliner", "inspector", "inspector-actions", "ticker", "command-dock", "dossier-footer", "kit-tabs" })
            {
                var e = root.UI.Root.Q(className: cls);
                if (e == null || e.resolvedStyle.display == DisplayStyle.None) continue;
                var b = e.worldBound;
                CheckReview(b.width > 0 && b.height > 0 && b.xMin >= view.xMin - 1 && b.xMax <= view.xMax + 1 && b.yMin >= view.yMin - 1 && b.yMax <= view.yMax + 1,
                    label + " / " + cls + " stays inside viewport: " + b);
            }
            var dossier = root.UI.Root.Q(className: "inspector");
            var dock = root.UI.Root.Q(className: "command-dock");
            if (dossier != null && dock != null) CheckReview(!dossier.worldBound.Overlaps(dock.worldBound), label + " / dossier and command dock do not overlap");
            foreach (var button in root.UI.Root.Query<Button>(className: "btn-primary").ToList())
            {
                if (button.resolvedStyle.display == DisplayStyle.None || button.worldBound.width <= 0) continue;
                var b = button.worldBound;
                CheckReview(b.xMin >= view.xMin && b.xMax <= view.xMax + 1 && b.yMin >= view.yMin && b.yMax <= view.yMax + 1, label + " / primary action stays visible");
            }
            // Bounds alone missed controls touching their frame. Check the actual negative space.
            var actions = root.UI.Root.Q(className: "inspector-actions");
            if (actions != null)
            {
                var card = actions.Q(className: "command-card");
                CheckReview(card != null && card.ClassListContains("surface-light"), label + " / primary command owns its light card");
                var frame = card != null ? card.worldBound : actions.worldBound;
                foreach (var button in actions.Query<Button>().ToList())
                {
                    var b = button.worldBound;
                    CheckReview(b.xMin >= frame.xMin + 16 && b.xMax <= frame.xMax - 16 && b.yMin >= frame.yMin + 16 && b.yMax <= frame.yMax - 16,
                        label + " / command button has safe frame inset: " + button.text + " bottom=" + (frame.yMax-b.yMax).ToString("0.0"));
                }
                var info = actions.Q(className: "command-info");
                if (info != null) CheckReview(info.worldBound.yMax <= frame.yMax - 16, label + " / command content preserves bottom breathing room");
                var buttons = actions.Q(className: "command-buttons");
                if (buttons != null && buttons.childCount > 1)
                    CheckReview(buttons[1].worldBound.xMin - buttons[0].worldBound.xMax >= 10, label + " / fleet actions have a distinct gap");
            }
            var tabs = root.UI.Root.Q(className: "kit-tabs");
            if (tabs != null)
                for (int i=1; i<tabs.childCount; i++) CheckReview(tabs[i].worldBound.xMin-tabs[i-1].worldBound.xMax >= 7, label + " / dossier tabs have separate hit areas");
            if (tabs != null)
                foreach (var caption in tabs.Query<Label>(className:"tab-label").ToList())
                {
                    var natural = caption.MeasureTextSize(caption.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
                    CheckReview(natural.x <= caption.contentRect.width + 1, label + " / tab caption fits on one line: " + caption.text);
                }
            if (dossier != null)
            {
                var scroll=dossier.Q<ScrollView>();
                CheckReview(scroll.horizontalScroller.resolvedStyle.display==DisplayStyle.None,label+" / vertical dossier has no horizontal scrollbar");
                foreach(var card in dossier.Query(className:"project-card").ToList())
                    CheckReview(card.worldBound.xMax<=scroll.contentViewport.worldBound.xMax+6,label+" / project cards fit the dossier viewport during reveal");
            }
            var top = root.UI.Root.Q(className: "topbar");
            foreach (var e in top.Children())
            {
                if (e.ClassListContains("spacer")) continue;
                var b = e.worldBound;
                CheckReview(b.xMin >= view.xMin - 1 && b.xMax <= view.xMax + 1, label + " / status control within width");
            }
        }

        static bool SubmitUi(GameRoot root, string name)
        {
            var button = root.UI.Root.Q<Button>(name);
            if (button == null || !button.enabledInHierarchy) return false;
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = button; button.SendEvent(evt); }
            return true;
        }

        public static IEnumerator PresentationReview(GameRoot root, string dir)
        {
            _dir = dir; _reviewFailures = 0; Directory.CreateDirectory(dir);
            root.Clock.Paused = true;
            if (!Settings.Overlays) root.ToggleOverlays();
            ulong initial = SaveGame.Digest(root.G);
            string initialJson = SaveGame.ToJson(root.G);
            float originalScale = Settings.UiScale;
            Environment();
            root.Rig.Snap(root.GalaxyHome);
            yield return Wait(0.5f);
            CheckLayout(root, "galaxy");
            yield return Shot(dir, "review_galaxy");
            if (Args.Has("-rrHudReview")) yield return HudRefinementReview(root,dir);
            root.EnterSystem("prismvale");
            yield return Until(() => !root.Busy, 120);
            yield return Wait(0.7f);
            CheckReview(root.LocalSystem != null && root.LocalSystem.Data.Id == "prismvale", "entered selected Prismvale system");
            // Capture the actual arrival pose.
            CheckLayout(root, "prismvale");
            yield return Shot(dir, "review_prismvale");
            root.ToggleOverlays();
            yield return Wait(0.2f);
            yield return Shot(dir, "review_prismvale_clean");
            root.ToggleOverlays();
            var world = root.G.GetSystem("prismvale").Planets[0];
            root.InspectPlanet(world.Id);
            yield return Until(() => !root.Rig.Transitioning, 8);
            yield return Wait(0.5f);
            CheckLayout(root, "prismvale world");
            yield return Shot(dir, "review_prismvale_world");
            root.ExitPlanet();
            yield return Until(() => !root.Rig.Transitioning, 8);
            root.ExitToGalaxy();
            yield return Until(() => !root.Busy, 15);
            CheckReview(root.Scale == ViewScale.Galaxy && root.Selection.SystemId == "prismvale", "return preserves selected system");
            root.EnterSystem(SliceScenario.HeroSystem);
            yield return Until(() => !root.Busy, 30);
            if (Args.Has("-rrSpacingReview"))
            {
                foreach (float scale in new[] { 1f, 1.25f, 1.4f })
                {
                    Settings.UiScale=scale; root.UI.ApplyScale(); yield return Wait(.5f);
                    CheckLayout(root, "system command spacing " + scale);
                    yield return Shot(dir, "spacing_system_" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                }
                Settings.UiScale=originalScale; root.UI.ApplyScale();
            }
            root.InspectPlanet(SliceScenario.HeroPlanet);
            yield return Until(() => !root.Rig.Transitioning, 8);
            yield return Wait(0.5f);
            yield return Shot(dir, "review_hero_world");
            if (Args.Has("-rrMotionReview")) yield return MotionReview(root,dir);
            if (Args.Has("-rrPrismaticReview")) yield return PrismaticReview(root, dir);
            foreach (float scale in new[] { 1f, 1.25f, 1.4f })
            {
                root.UI.Strategy.ShowTab("Overview");
                Settings.UiScale = scale; root.UI.ApplyScale();
                yield return Wait(0.4f);
                CheckLayout(root, "hero world scale " + scale);
                yield return Shot(dir, "review_ui_scale_" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                CheckReview(SubmitUi(root, "tab-shipyard"), "shipyard tab accepts UI submit");
                yield return Wait(.3f);
                CheckReview(root.UI.Root.Q<Button>("tab-shipyard").ClassListContains("kit-tab-selected"), "shipyard tab shows selected state");
                CheckLayout(root, "shipyard scale " + scale);
                yield return Shot(dir, "review_shipyard_" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                CheckReview(SubmitUi(root, "tab-development"), "development tab accepts UI submit");
                yield return Wait(.3f);
                CheckLayout(root, "development scale " + scale);
                yield return Shot(dir, "review_development_" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
            }
            Settings.UiScale = originalScale; root.UI.ApplyScale();
            root.ExitPlanet();
            yield return Until(() => !root.Rig.Transitioning, 8);
            root.SelectAndFocusFleet(SliceScenario.PlayerFleet);
            yield return Until(() => !root.Rig.Transitioning, 8);
            yield return Wait(0.5f);
            CheckLayout(root, "fleet");
            yield return Shot(dir, "review_fleet");
            CheckReview(SaveGame.Digest(root.G) == initial, "visual navigation and UI scaling leave authoritative state unchanged");
            foreach (var ship in ShipCatalog.Commissionable)
                CheckReview(UnityEngine.Resources.Load<Texture2D>("RR/UI/Ships/" + ship.defId) != null, "portrait available for " + ship.defId);
            // Order-button regression uses a disposable launch only; ordinary presentation review remains read-only.
            if (Args.Has("-rrUiKitOrders") && Args.Value("-rrSavePath") != null)
            {
                root.InspectPlanet(SliceScenario.HeroPlanet);
                yield return Until(() => !root.Rig.Transitioning, 8);
                SubmitUi(root, "tab-shipyard"); yield return Wait(.2f);
                var planet = root.G.GetPlanet(SliceScenario.HeroPlanet);
                var stock = root.G.Player.Stock;
                CheckReview(planet.Project == null, "order review starts with idle shipyard");
                CheckReview(SubmitUi(root, "project-commit"), "commission button accepts UI submit");
                yield return Wait(.3f);
                CheckReview(planet.Project != null && planet.Project.ShipDefId == "silex_prism_escort", "commission button starts selected hull");
                CheckReview(root.G.Player.Stock.Energy == stock.Energy - 40 && root.G.Player.Stock.Materials == stock.Materials - 90 && root.G.Player.Stock.Credits == stock.Credits - 30, "commission charges authoritative cost once");
                CheckReview(root.UI.Root.Q<Button>("project-commit") == null, "active project replaces commission action");
                CheckLayout(root, "active project"); yield return Shot(dir, "review_project_active");
                CheckReview(SubmitUi(root, "project-cancel"), "cancel button accepts UI submit"); yield return Wait(.3f);
                CheckReview(planet.Project == null && root.G.Player.Stock.Energy == stock.Energy - 20 && root.G.Player.Stock.Materials == stock.Materials - 45 && root.G.Player.Stock.Credits == stock.Credits - 15, "cancel clears project and returns half cost once");
                SubmitUi(root, "tab-development"); yield return Wait(.2f);
                CheckReview(SubmitUi(root, "project-commit"), "development button accepts UI submit"); yield return Wait(.3f);
                CheckReview(planet.Project != null && planet.Project.Kind == ProjectKind.CrystalFoundry, "development action starts foundry");
                SubmitUi(root, "project-cancel"); yield return Wait(.2f);
                if (Args.Has("-rrSpacingReview"))
                {
                    root.ExitPlanet(); yield return Until(() => !root.Rig.Transitioning, 8);
                    root.ExitToGalaxy(); yield return Until(() => !root.Busy, 15);
                    root.SelectAndFocusFleet(SliceScenario.PlayerFleet); yield return Wait(.6f);
                    root.IssueMove("cinder_verge"); yield return Wait(.5f);
                    foreach (float scale in new[] { 1f, 1.25f, 1.4f })
                    {
                        Settings.UiScale=scale; root.UI.ApplyScale(); yield return Wait(.5f);
                        CheckReview(root.UI.Root.Q<Button>("fleet-hold") != null, "moving fleet exposes separate Hold action");
                        CheckLayout(root, "moving fleet spacing " + scale);
                        yield return Shot(dir, "spacing_fleet_moving_" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    CheckReview(SubmitUi(root,"fleet-hold"), "spaced Hold button accepts input"); yield return Wait(.3f);
                    CheckReview(!root.G.GetFleet(SliceScenario.PlayerFleet).Moving, "Hold retains existing cancel-order behavior");
                    Settings.UiScale=originalScale; root.UI.ApplyScale();
                }
            }
            Note("presentation review failures: " + _reviewFailures);
            string after=Args.Value("-rrTourAfterReview"), disposable=Args.Value("-rrSavePath");
            if (_reviewFailures == 0 && after != null && disposable != null)
            {
                // Restore the exact initial scenario through the real save/load path before the campaign tour.
                File.WriteAllText(disposable,initialJson); root.LoadGame();
                yield return Until(() => !root.Busy,20); yield return Wait(.5f);
                CheckReview(SaveGame.Digest(root.G)==initial,"combined tour restores the initial authoritative scenario");
                // Load preserves local-system context; return through the real navigation flow before the galaxy tour.
                if(root.Scale == ViewScale.Planet) { root.ExitPlanet(); yield return Until(() => !root.Rig.Transitioning,8); }
                if(root.Scale == ViewScale.System) { root.ExitToGalaxy(); yield return Until(() => !root.Busy,20); }
                CheckReview(root.Scale == ViewScale.Galaxy,"combined tour starts at galaxy scale");
                if (_reviewFailures != 0) { Application.Quit(1); yield break; }
                if(float.TryParse(Args.Value("-rrTourUiScale"),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float tourScale)) { Settings.UiScale=tourScale; root.UI.ApplyScale(); }
                yield return FullTour(root,after); yield break;
            }
            if (!Args.Has("-rrReviewStay")) Application.Quit(_reviewFailures == 0 ? 0 : 1);
        }

        static IEnumerator HudRefinementReview(GameRoot root, string dir)
        {
            ulong before=SaveGame.Digest(root.G); float oldScale=Settings.UiScale;
            var fleets=root.G.Fleets.Where(f=>f.OwnerId==root.G.Player.Id && f.Ships.Count>0).ToList();
            foreach(float scale in new[]{1f,1.4f})
            {
                Settings.UiScale=scale; root.UI.ApplyScale(); yield return Wait(.3f);
                foreach(var fleet in fleets)
                {
                    CheckReview(SubmitUi(root,"fleet-card-"+fleet.Id), "fleet card selects through UI: "+fleet.Name);
                    yield return Wait(.5f);
                    CheckReview(root.Selection.FleetId==fleet.Id,"fleet selection reaches existing game handler");
                    var summary=root.UI.Root.Q(className:"dossier-summary");
                    var values=summary.Query<Label>(className:"summary-value").ToList();
                    CheckReview(values.Count==2 && values[0].text==fleet.Ships.Count.ToString("D2") && values[1].text==fleet.Ships.Sum(s=>ShipCatalog.Cost(s.DefId)).ToString(),"fleet summary matches authoritative ships and battle points");
                    CheckReview(root.UI.Root.Query(className:"fleet-card-selected").ToList().Count==1,"exactly one fleet card has selected state");
                    var roster=root.UI.Root.Q(className:"inspector").Query(className:"roster-item").ToList();
                    CheckReview(roster.Count==fleet.Ships.Count,"roster represents every ship");
                    foreach(var row in roster) CheckReview(row.worldBound.xMax<=root.UI.Root.Q(className:"inspector").worldBound.xMax-8,"roster remains inside dossier width");
                    CheckLayout(root,"fleet "+fleet.Name+" scale "+scale);
                    yield return Shot(dir,"hud_fleet_"+fleet.Id+"_"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
                }
            }
            Settings.UiScale=oldScale; root.UI.ApplyScale(); root.Rig.Snap(root.GalaxyHome);
            CheckReview(SaveGame.Digest(root.G)==before,"fleet inspection and UI scale preserve authoritative state");
        }

        // Focused UI regression run. Skip only the expensive planet warmup; the real galaxy and HUD render normally.
        public static IEnumerator UiSmoke(GameRoot root, string dir)
        {
            _dir=dir; _reviewFailures=0; Directory.CreateDirectory(dir);
            ulong before=SaveGame.Digest(root.G);
            float oldScale=Settings.UiScale, oldMaster=Settings.Master;
            bool oldMute=Settings.Muted, oldMotion=Settings.ReducedMotion;
            Settings.ReducedMotion=false; root.UI.Root.EnableInClassList("reduced-motion",false);
            yield return Wait(.5f);
            if(Args.Has("-rrCinematicReview")) yield return CinematicReview(root,dir);
            root.UI.Strategy.Toast("Fleet orders acknowledged.","ok");
            root.UI.Strategy.Toast("Route unavailable: select a connected system.","bad");
            yield return Wait(.4f);
            var toasts=root.UI.Root.Query<Label>(className:"toast").ToList();
            CheckReview(toasts.Count==2,"success and warning notifications are present");
            foreach(var toast in toasts)
            {
                CheckReview(!string.IsNullOrWhiteSpace(toast.text) && toast.resolvedStyle.color.a>.9f,"notification retains opaque native text: "+toast.text);
                CheckReview(toast.worldBound.width>100 && toast.worldBound.height>20,"notification has readable bounds");
            }
            yield return Shot(dir,"notifications");
            root.ShowSettings(); yield return Wait(.35f);
            // Exercise the exact result-copy class combination on both live surface roles.
            var darkStatus=UIX.Text("Destroyed ship", "item-sub", "text-danger");
            var lightStatus=UIX.Text("Unavailable order", "item-sub", "text-danger");
            root.UI.Root.Q(className:"modal").Add(darkStatus);
            root.UI.Root.Q(className:"command-card").Add(lightStatus);
            yield return null;
            var darkColor=darkStatus.resolvedStyle.color; var lightColor=lightStatus.resolvedStyle.color;
            CheckReview(darkColor.r>.9f && darkColor.g>.5f && darkColor.g<.65f && darkColor.b<.65f,
                "secondary danger copy retains readable coral on dark surfaces");
            CheckReview(lightColor.r>.6f && lightColor.r<.75f && lightColor.g<.25f && lightColor.b<.3f,
                "secondary danger copy switches to dark red on light surfaces");
            darkStatus.RemoveFromHierarchy(); lightStatus.RemoveFromHierarchy();
            yield return null;
            foreach(var button in root.UI.Root.Query<Button>(className:"btn-primary").ToList())
            {
                var caption=button.Q<Label>(className:"prism-caption");
                if(caption!=null) CheckReview(caption.text==button.text && caption.worldBound.width>20,"primary caption sizes with its control");
            }
            yield return Shot(dir,"settings");
            var master=root.UI.Root.Q<Slider>("settings-master"); master.value=.37f;
            CheckReview(Mathf.Abs(Settings.Master-.37f)<.001f && root.UI.Root.Q<Label>("settings-master-value").text=="37%", "volume control updates preference and numeric readout");
            Settings.Load();
            CheckReview(Mathf.Abs(Settings.Master-.37f)<.001f, "volume preference survives persisted settings reload");
            master.value=oldMaster;
            var mute=root.UI.Root.Q<Toggle>("settings-mute"); mute.value=!oldMute;
            CheckReview(Settings.Muted==!oldMute && root.UI.Root.Q<Label>("settings-mute-state").text==(!oldMute?"ON":"OFF"), "mute control retains explicit state");
            mute.value=oldMute;
            var motion=root.UI.Root.Q<Toggle>("settings-motion"); motion.value=true;
            CheckReview(Settings.ReducedMotion && root.UI.Root.ClassListContains("reduced-motion"), "reduced motion applies immediately");
            foreach(float scale in new[]{1f,1.4f})
            {
                root.UI.Root.Q<Slider>("settings-scale").value=scale; yield return Wait(.3f);
                var done=root.UI.Root.Q<Button>("settings-done"); var bounds=done.worldBound; var viewport=root.UI.Root.worldBound;
                CheckReview(bounds.yMax<=viewport.yMax && bounds.yMin>=0 && bounds.xMax<=viewport.xMax, "settings footer stays visible at scale "+scale);
                CheckReview(SubmitUi(root,"settings-tab-controls"),"controls tab accepts keyboard submit"); yield return Wait(.2f);
                CheckReview(root.UI.Root.Q("settings-controls").resolvedStyle.display==DisplayStyle.Flex && root.UI.Root.Q("settings-preferences").resolvedStyle.display==DisplayStyle.None,"controls reference replaces preferences");
                yield return Shot(dir,"settings_controls_"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
                SubmitUi(root,"settings-tab-preferences"); yield return Wait(.2f);
                yield return Shot(dir,"settings_preferences_"+scale.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture));
            }
            root.UI.Root.Q<Slider>("settings-scale").value=oldScale;
            motion.value=oldMotion; Settings.ReducedMotion=oldMotion; root.UI.Root.EnableInClassList("reduced-motion",oldMotion); Settings.Save();
            CheckReview(SubmitUi(root,"settings-done") && !root.UI.ModalOpen,"Done closes settings through actual UI event");
            yield return Wait(4.5f);
            CheckReview(root.UI.Root.Query<Label>(className:"toast").ToList().Count==0,"notifications expire and remove their elements");
            CheckReview(UiMotion.ActiveTracks==0,"notification exit and modal close leave no active animation tracks");
            CheckReview(SaveGame.Digest(root.G)==before,"focused UI review leaves authoritative state unchanged");
            Note("UI smoke failures: "+_reviewFailures);
            if(!Args.Has("-rrUiSmokeStay")) Application.Quit(_reviewFailures==0?0:1);
        }

        static IEnumerator CinematicReview(GameRoot root,string dir)
        {
            ulong digest=SaveGame.Digest(root.G);
            CheckReview(root.UI.Root.ClassListContains("cinematic-ui"),"cinematic surface system is installed");
            var portraits=root.UI.Root.Query<Image>(className:"ship-portrait-image").ToList();
            CheckReview(portraits.Count>=3,"real ship portraits paint above their optical backing");
            foreach(var portrait in portraits)
            {
                CheckReview(portrait.image!=null && portrait.pickingMode==PickingMode.Ignore && portrait.parent.pickingMode==PickingMode.Ignore,
                    "portrait retains its ship texture and never intercepts input");
                CheckReview(portrait.worldBound.width>20 && portrait.worldBound.xMin>=portrait.parent.worldBound.xMin-1 && portrait.worldBound.xMax<=portrait.parent.worldBound.xMax+1,
                    "portrait optics remain inside their original layout bounds");
            }
            var command=root.UI.Root.Q(className:"command-card");
            var button=command.Q<Button>(className:"btn-primary");
            var originalBounds=button.worldBound;
            using(var enter=PointerEnterEvent.GetPooled()){enter.target=button;button.SendEvent(enter);}
            yield return Wait(.12f);
            CheckReview(UiMotion.ActiveTracks>0,"pointer entry starts finite command lighting");
            CheckReview(button.worldBound==originalBounds,"command lighting does not shift the hit area");
            yield return Shot(dir,"cinematic_command_active");
            for(int i=0;i<20;i++)Prismatic.Pulse(button);
            CheckReview(UiMotion.ActiveTracks<=3,"repeated command emphasis replaces its track instead of accumulating");
            yield return Wait(.7f);
            using(var leave=PointerLeaveEvent.GetPooled()){leave.target=button;button.SendEvent(leave);}
            yield return Wait(.2f);
            CheckReview(UiMotion.ActiveTracks==0,"command sheen settles without an idle animation loop; remaining="+UiMotion.ActiveTracks);
            yield return Shot(dir,"cinematic_command_settled");
            root.ShowSettings();yield return Wait(.12f);
            CheckReview(root.UI.Root.Q(className:"cinematic-backdrop")!=null,"modal uses the cinematic focus backdrop");
            yield return Shot(dir,"cinematic_modal_entry");
            yield return Wait(.65f);yield return Shot(dir,"cinematic_modal_settled");
            root.UI.CloseModal();yield return Wait(.1f);
            Prismatic.Pulse(button);yield return null;Settings.ReducedMotion=true;
            yield return Wait(.08f);
            CheckReview(UiMotion.ActiveTracks==0,"Reduced Motion immediately resolves the new light pass");
            Settings.ReducedMotion=false;
            button.SetEnabled(false);
            using(var enter=PointerEnterEvent.GetPooled()){enter.target=button;button.SendEvent(enter);}
            yield return Wait(.2f);
            CheckReview(UiMotion.ActiveTracks==0,"disabled commands do not launch an emphasis effect");
            button.SetEnabled(true);
            // Recording stalls the render thread for PNG encoding. Verify timing before recording.
            MotionCapture.Begin(root,dir,"cinematic-command-and-modal",3.8f);
            Prismatic.Pulse(command);Prismatic.Pulse(button);yield return Wait(.9f);
            root.ShowSettings();yield return Wait(1.1f);root.UI.CloseModal();
            yield return Until(()=>MotionCapture.Active==0,8);yield return Wait(.6f);
            CheckReview(UiMotion.ActiveTracks==0,"recorded light and modal sequence releases every track");
            CheckReview(SaveGame.Digest(root.G)==digest,"cinematic optics preserve authoritative state");
            yield return Measure("cinematic UI settled",120);
        }

        static IEnumerator MotionReview(GameRoot root,string dir)
        {
            ulong digest=SaveGame.Digest(root.G);var pose=root.Rig.Current;
            bool reduced=Settings.ReducedMotion;Settings.ReducedMotion=false;
            CheckReview(UnityEngine.Resources.Load<GameObject>("RR/Motion/pulse-rail")!=null,"authored Blender pulse rail imported into player");
            CheckReview(UnityEngine.Resources.Load<GameObject>("RR/Motion/crystal-ejecta")!=null,"authored Blender ejecta imported into player");
            root.Rig.Pan(new Vector2(14,8));yield return null;root.Rig.Snap(pose);yield return Wait(.3f);
            CheckReview(Vector3.Distance(root.Rig.Current.Focus,pose.Focus)<.001f,"camera snap clears carried damping velocity");
            var to=pose;to.Yaw+=16;to.Distance*=1.08f;
            MotionCapture.Begin(root,dir,"camera-orbit-settle",3.5f);
            root.Rig.TransitionTo(to,.8f);yield return Until(()=>!root.Rig.Transitioning,3);
            CheckReview(Mathf.Abs(Mathf.DeltaAngle(root.Rig.Current.Yaw,to.Yaw))<.01f,"authored camera arrival reaches requested yaw");
            yield return Wait(.25f);root.Rig.TransitionTo(pose,.8f);yield return Until(()=>!root.Rig.Transitioning,3);yield return Wait(.5f);
            root.Rig.TransitionTo(to,1);yield return Wait(.1f);Settings.ReducedMotion=true;yield return Wait(.1f);
            CheckReview(!root.Rig.Transitioning,"Reduced Motion resolves an active camera transition");
            root.Rig.Snap(pose);Settings.ReducedMotion=false;
            int callbacks=0;root.Rig.TransitionTo(to,1,()=>callbacks++);yield return null;root.Rig.FinishTransition();root.Rig.FinishTransition();
            CheckReview(callbacks==1 && !root.Rig.Transitioning,"interrupted camera transition completes exactly once");root.Rig.Snap(pose);
            var probe=new GameObject("Arrival audit");var travel=probe.AddComponent<FleetMotion>();travel.SetCourse(Quaternion.identity,true,Color.cyan);
            yield return null;probe.transform.localPosition=Vector3.right*4;travel.SetCourse(Quaternion.identity,false,Color.cyan);
            yield return Wait(.1f);CheckReview(probe.transform.localPosition.x>0 && probe.transform.localPosition.x<4,"fleet arrival settles between previous and final marker positions");
            yield return Wait(.4f);CheckReview(travel.Settled && Vector3.Distance(probe.transform.localPosition,Vector3.right*4)<.001f,"fleet arrival reaches the exact canonical marker position");Object.Destroy(probe);yield return null;
            var sp=root.LocalSystem;var body=sp.Planets[0].Body;var rotation=body.localRotation;
            yield return Wait(.2f);CheckReview(Quaternion.Angle(rotation,body.localRotation)>.01f,"planetary ambient rotation remains active");
            Settings.ReducedMotion=true;rotation=body.localRotation;yield return Wait(.2f);
            CheckReview(Quaternion.Angle(rotation,body.localRotation)<.001f,"Reduced Motion freezes decorative planetary rotation");
            Settings.ReducedMotion=false;
            int materialCount=UnityEngine.Resources.FindObjectsOfTypeAll<Material>().Length;
            foreach(MotionBeat beat in System.Enum.GetValues(typeof(MotionBeat)))
                MotionBurst.Spawn(sp.transform,Vector3.zero,new Color(.4f,.7f,1),1,.2f,beat);
            yield return Wait(.5f);CheckReview(MotionBurst.Active==0,"all finite effect types release instances and owned materials");
            CheckReview(UnityEngine.Resources.FindObjectsOfTypeAll<Material>().Length<=materialCount,"finite effect burst returns native material count to baseline");
            Settings.ReducedMotion=true;
            var fx=MotionBurst.Spawn(sp.transform,Vector3.zero,Color.white,1,.4f,MotionBeat.Destruction);
            yield return null;var scale=fx.transform.GetChild(0).localScale;yield return Wait(.1f);
            CheckReview(Vector3.Distance(scale,fx.transform.GetChild(0).localScale)<.001f && fx.transform.childCount==1,"Reduced Motion effect uses static rail without flying ejecta");
            yield return Wait(.5f);CheckReview(MotionBurst.Active==0,"Reduced Motion effects also clean up");
            Settings.ReducedMotion=reduced;root.UI.Root.EnableInClassList("reduced-motion",reduced);
            CheckReview(SaveGame.Digest(root.G)==digest,"motion audit does not change authoritative state");
        }

        // Real UI lifecycle and input checks; no state is changed by the motion test.
        static IEnumerator PrismaticReview(GameRoot root, string dir)
        {
            ulong before = SaveGame.Digest(root.G);
            bool reduced = Settings.ReducedMotion;
            Settings.ReducedMotion = false; root.UI.Root.EnableInClassList("reduced-motion", false);
            CheckReview(UnityEngine.Resources.Load<Texture2D>("RR/UI/Prismatic/crystal-relief") != null, "legacy art source remains available for prior builds");
            CheckReview(root.UI.Root.Query(className:"prism-surface").ToList().Count >= 12, "shared glass frames decorate the live instruments");
            root.UI.Strategy.ShowTab("Overview"); yield return Wait(.6f);
            CheckReview(root.UI.Root.ClassListContains("command-glass"), "reference UI kit is installed");
            var dossier=root.UI.Root.Q(className:"inspector");
            CheckReview(!dossier.ClassListContains("surface-light") && dossier.Q<Label>(className:"h-display").resolvedStyle.color.grayscale>.65f,"dossier uses a dark surface with light heading");
            var worldRows=dossier.Query<VisualElement>(className:"data-row").ToList();
            CheckReview(worldRows.Count==5 && dossier.Q(className:"data-population")!=null,"reference world overview exposes all five intelligence rows");
            var command=root.UI.Root.Q(className:"command-card");
            CheckReview(command!=null && command.Q<Label>(className:"command-title").resolvedStyle.color.grayscale<.35f,"light action card has dark readable content");
            foreach(var tab in dossier.Query<Button>(className:"kit-tab").ToList())
                CheckReview(tab.Query<Icon>().ToList().Count==0,"reference tabs use text and an active underline");
            CheckReview(root.UI.Root.Query(className:"yield-cell").ToList().Count == 3, "daily production is grouped into three columns");
            CheckReview(root.UI.Root.Query(className:"development-segment").ToList().Count == 5, "world development has five explicit segments");
            var orbitCopy=root.UI.Root.Q(className:"orbit-copy");
            var overviewScroll=root.UI.Root.Q(className:"inspector").Q<ScrollView>();
            CheckReview(orbitCopy != null && orbitCopy.worldBound.yMax <= overviewScroll.contentViewport.worldBound.yMax + 1, "normal overview shows full orbital explanation without scrolling");
            var hero=root.LocalSystem.Planet(SliceScenario.HeroPlanet);
            var planetCenter=root.Rig.Cam.WorldToViewportPoint(hero.Root.position);
            CheckReview(planetCenter.x > .54f && planetCenter.x < .66f, "planet remains composed beside the dossier after camera tracking settles: " + planetCenter);
            var logButton=root.UI.Root.Q<Button>(className:"log-button");
            CheckReview(logButton.worldBound.width >= 30, "compact Log button retains a readable caption width");
            int peak=0;
            for (int i=0; i<12; i++)
            {
                CheckReview(SubmitUi(root, i%2==0 ? "tab-shipyard" : "tab-development"), "rapid tab change " + i + " accepts input during motion");
                peak=Mathf.Max(peak,UiMotion.ActiveTracks); yield return null;
            }
            CheckReview(peak <= 8, "rapid replacement keeps live motion tracks bounded: " + peak);
            yield return Wait(.7f);
            CheckReview(UiMotion.ActiveTracks == 0, "all motion tracks settle after tab changes");
            var commit=root.UI.Root.Q<Button>("project-commit");
            CheckReview(commit != null && root.UI.PointerOverUI(PanelToScreen(root.UI.Root.panel,commit.worldBound.center,root.Rig.Cam)), "decorated action still blocks world input");
            CheckReview(commit != null && commit.Q<Label>(className:"prism-caption")?.text == commit.text && !string.IsNullOrEmpty(commit.text), "custom command face retains its visible caption");
            var view=root.UI.Root.worldBound;
            CheckReview(!root.UI.PointerOverUI(PanelToScreen(root.UI.Root.panel,new Vector2(view.width*.65f,view.height*.46f),root.Rig.Cam)), "open space remains available to camera and world input");

            root.ShowSettings(); yield return null;
            CheckReview(UiMotion.ActiveTracks > 0, "modal entrance has an active presentation track");
            root.UI.CloseModal(); yield return Wait(.6f);
            CheckReview(UiMotion.ActiveTracks == 0, "closing a modal during entry releases its animation");

            // Toggle while an animation is running, rather than testing only a cold start.
            root.UI.Strategy.ShowTab("Shipyard"); yield return null;
            Settings.ReducedMotion=true; root.UI.Root.EnableInClassList("reduced-motion",true);
            yield return Wait(.08f);
            root.UI.Strategy.ShowTab("Overview"); yield return null;
            var actions=root.UI.Root.Q(className:"inspector-actions");
            CheckReview(UiMotion.ActiveTracks == 0, "Reduced Motion immediately settles active tracks");
            CheckReview(Mathf.Abs(actions.resolvedStyle.opacity-1)<.001f && Mathf.Abs(actions.resolvedStyle.translate.y)<.001f, "Reduced Motion shows final position and opacity");
            yield return Shot(dir,"prismatic_reduced_motion");
            Settings.ReducedMotion=false; root.UI.Root.EnableInClassList("reduced-motion",false);

            if (Args.Has("-rrMotionClip"))
            {
                string frames=Path.Combine(dir,"motion"); Directory.CreateDirectory(frames);
                var timings=new StringBuilder("frame,seconds,phase,active_tracks\n");
                float start=Time.unscaledTime; int index=0, phase=-1;
                while (Time.unscaledTime-start<6)
                {
                    float t=Time.unscaledTime-start;
                    int next=t<.7f?0:t<1.8f?1:t<2.9f?2:t<4?3:t<5.1f?4:5;
                    if(next!=phase)
                    {
                        phase=next;
                        if(phase==1) SubmitUi(root,"tab-shipyard");
                        if(phase==2) SubmitUi(root,"tab-development");
                        if(phase==3) SubmitUi(root,"tab-overview");
                        if(phase==4) root.ShowSettings();
                        if(phase==5) root.UI.CloseModal();
                    }
                    yield return new WaitForEndOfFrame();
                    string name="frame_"+index.ToString("D4")+".png";
                    timings.AppendLine(name+","+(Time.unscaledTime-start).ToString("0.000000",System.Globalization.CultureInfo.InvariantCulture)+","+phase+","+UiMotion.ActiveTracks);
                    var frame=ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(frames,name),frame.EncodeToPNG()); Object.Destroy(frame);
                    index++; yield return null;
                }
                File.WriteAllText(Path.Combine(frames,"timings.csv"),timings.ToString());
                Note("motion capture: "+index+" real player frames with measured timestamps; encoding is separate from frame-time measurement");
            }
            yield return Wait(.7f);
            CheckReview(UiMotion.ActiveTracks == 0, "motion showcase leaves no scheduled animation tracks");
            CheckReview(SaveGame.Digest(root.G)==before, "UI motion and modal transitions leave authoritative galaxy state unchanged");
            Settings.ReducedMotion=reduced; root.UI.Root.EnableInClassList("reduced-motion",reduced);
            yield return Measure("Prismatic UI settled",120);
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
            MotionCapture.Begin(root,dir,"galaxy-to-system",4);
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
            MotionCapture.Begin(root,dir,"world-inspection",4);
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
            MotionCapture.Begin(root,dir,"formation-dwell",3);
            yield return Measure("fleet close", 200);
            MotionCapture.Begin(root,dir,"system-to-galaxy",3);
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
            MotionCapture.Begin(root,dir,"fleet-travel",8);
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
            // First-time procedural world preparation can exceed a short animation timeout.
            yield return Until(() => root.UI.ModalOpen && !root.Busy, 180);
            bool preparationReady = root.UI.ModalOpen && !root.Busy;
            CheckReview(preparationReady, "battle preparation finishes loading before commitment");
            if (!preparationReady) { Application.Quit(1); yield break; }
            yield return Wait(0.6f);
            yield return Shot(dir, "b01_battle_preparation");
            var e = G.PendingEngagement;
            var mine = G.GetFleet(e.AttackerFleetId).OwnerId == G.Player.Id ? G.GetFleet(e.AttackerFleetId) : G.GetFleet(e.DefenderFleetId);
            var line = BattleBridge.AutoSelect(G, mine);
            Note("battle line: " + string.Join(", ", line.Select(u => mine.Ships.First(s => s.Uid == u).DefId)) + " (cost " + BattleBridge.Validate(G, mine, line).Cost + ")");
            root.StartBattle(line);
            yield return Until(() => root.Scale == ViewScale.Battle && !root.Busy, 60);
            bool battleReady = root.Scale == ViewScale.Battle && !root.Busy && root.Battle != null;
            CheckReview(battleReady, "battle presentation is ready before animation review");
            if (!battleReady) { Application.Quit(1); yield break; }
            yield return Wait(0.6f);
            yield return Shot(dir, "b02_battle_start");
            if (Args.Has("-rrSpacingReview"))
            {
                float rosterScale = Settings.UiScale;
                foreach (float scale in new[] { 1f, 1.25f, 1.4f })
                {
                    Settings.UiScale = scale; root.UI.ApplyScale(); yield return Wait(.5f);
                    var cards = root.UI.Root.Query<VisualElement>(className: "unit-card").ToList();
                    CheckReview(cards.Count > 0, "combat roster available for spacing review at " + scale);
                    foreach (var card in cards)
                    {
                        var name = card.Q<Label>(className: "unit-name");
                        var status = card.Q<Label>(className: "unit-status");
                        CheckReview(name != null && status != null, "combat roster separates name and status");
                        if (name == null || status == null) continue;
                        CheckReview(status.worldBound.xMin - name.worldBound.xMax >= 9,
                            "combat roster preserves name/status gap: " + name.text);
                        CheckReview(status.worldBound.xMax <= card.worldBound.xMax - 7,
                            "combat roster status stays inside card: " + name.text);
                        var wrapped = name.MeasureTextSize(name.text, name.contentRect.width, VisualElement.MeasureMode.Exactly, 0, VisualElement.MeasureMode.Undefined);
                        CheckReview(wrapped.y <= name.contentRect.height + 1,
                            "combat roster shows the full wrapped name: " + name.text);
                        var natural = status.MeasureTextSize(status.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
                        CheckReview(natural.x <= status.contentRect.width + 1,
                            "combat roster status fits without clipping: " + name.text);
                    }
                    yield return Shot(dir, "spacing_combat_roster_" + scale.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
                }
                Settings.UiScale = rosterScale; root.UI.ApplyScale(); yield return Wait(.3f);
            }
            foreach (var scroll in root.UI.Root.Query<ScrollView>().ToList().Where(s => s.mode == ScrollViewMode.Vertical && s.worldBound.width > 0))
                CheckReview(scroll.horizontalScroller.resolvedStyle.display == DisplayStyle.None, "vertical combat panels have no horizontal scrollbar");
            yield return Measure("orbital battle", 240);

            // 6. Play the battle. The tour pilot chooses the player's commands with the same AI the opponent uses and
            //    submits them through BattlePresentation.Submit, the path the interface uses.
            var b = root.Battle;
            var pilot = new RosterAI(AIDifficulty.Tactical, 4242);
            int shots = 0, acts = 0;
            bool initialReduced=Settings.ReducedMotion;
            MotionCapture.Begin(root,dir,"combat-opening",14);
            while (!b.S.IsOver && acts < 600)
            {
                if (b.PlayersTurn)
                {
                    var c = pilot.ChooseAction(b.S);
                    if (shots == 0 && c.UnitId != 0) { b.Select(c.UnitId); yield return Wait(0.3f); yield return Shot(dir, "b03_ship_selected"); shots++; }
                    if(Args.Has("-rrMotionReview"))
                    {
                        foreach(var u in b.S.Units.Where(u=>u.Alive && u.OnBoard))
                            CheckReview(b.Units.ContainsKey(u.Id) && Vector3.Distance(b.Units[u.Id].T.localPosition,BattlePresentation.World(u.Position))<.001f,"settled animated unit occupies legal hex: "+u.Id);
                        if(acts==12){b.AnimSpeed=2;MotionCapture.Begin(root,dir,"combat-double-speed",9);}
                        if(acts==24){b.AnimSpeed=1;MotionCapture.Begin(root,dir,"combat-midgame",12);}
                        if(acts==40){Settings.ReducedMotion=true;MotionCapture.Begin(root,dir,"combat-reduced-motion",8);}
                        if(acts==50)Settings.ReducedMotion=initialReduced;
                    }
                    b.Submit(c);
                    acts++;
                    if (acts == 6 || acts == 16) { yield return Until(() => !b.Animating, 10); yield return Shot(dir, "b0" + (3 + shots) + "_battle_" + acts); shots++; }
                }
                yield return null;
            }
            Note("battle over: winner seat " + b.S.Winner + " (" + b.S.VictoryReason + ") after round " + b.S.Round + ", " + b.History.Count + " commands");
            Settings.ReducedMotion=initialReduced;
            ulong h1 = Serialization.StateHash(b.S);
            var replay = BattleBridge.CreateState(root.CurrentSetup, out var rerr);
            foreach (var c in b.History) RulesEngine.Apply(replay, c.Clone());
            Note("replay of the battle from its setup and command log: " + (Serialization.StateHash(replay) == h1 ? "identical final state" : "DIVERGED"));
            CheckReview(Serialization.StateHash(replay)==h1,"animated combat replay is identical to authoritative final state");
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
            if(Args.Has("-rrMotionReview"))
            {
                yield return Wait(1);
                CheckReview(MotionBurst.Active==0,"battle teardown leaves no active motion effects");
                CheckReview(d1==d3,"animated campaign save/load preserves state");
                Note("motion campaign failures: "+_reviewFailures+"; peak effects "+MotionBurst.Peak+"; effects spawned "+MotionBurst.Spawned);
            }
            yield return Until(()=>MotionCapture.Active==0,20);
            if (!Args.Has("-rrTourStay")) Application.Quit(_reviewFailures==0?0:1);
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
                bool skinned=go.GetComponent<UnitVisual>()!=null;
                var b = skinned?UnitVisual.BoundsOf(go):mf.sharedMesh.bounds;
                if(skinned)b.center-=stage.position;
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