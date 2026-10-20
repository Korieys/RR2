using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RR.Strategy;
using UnityEngine;
using UnityEngine.Rendering;

namespace RR.Game
{
    public enum ViewScale { Galaxy, System, Planet, Battle }

    // Composition root and the single owner of scale, transitions and command submission for the slice.
    public sealed partial class GameRoot : MonoBehaviour
    {
        public static GameRoot I { get; private set; }
        public GalaxyState G { get; set; }
        public CameraRig Rig { get; private set; }
        public Volume Post { get; private set; }
        public Material Sky { get; private set; }
        public Transform WorldRoot { get; private set; }
        public readonly SelectionState Selection = new SelectionState();
        public readonly StrategyClock Clock = new StrategyClock();
        public AudioService Audio { get; private set; }
        public UIRoot UI { get; private set; }
        public GalaxyPresentation Galaxy { get; private set; }
        public SystemPresentation LocalSystem { get; private set; }
        public BattlePresentation Battle { get; private set; }
        public ViewScale Scale { get; private set; }
        public bool Targeting { get; private set; }
        public bool Busy { get; private set; }
        public bool ShipSheetMode;
        public RenderTexture OffscreenScene, OffscreenUI;
        BattleHud _battleHud;
        BattleSetup _setup;
        bool _resultShown;
        CamPose _galaxyBookmark, _systemBookmark;
        bool _haveGalaxyBookmark;
        bool _loadingShown;
        public string LoadingText;
        public float LoadingProgress;
        /// <summary>The save slot: -rrSavePath <file> overrides it (automation and playtests use a disposable file, so the
        /// player's own slot is never touched).</summary>
        public static string SavePath => Args.Value("-rrSavePath") ?? Path.Combine(Application.persistentDataPath, "saves", "slot1.json");

        void Awake()
        {
            I = this;
            Application.targetFrameRate = Args.Has("-rrUncapped") ? -1 : 60;
            QualitySettings.vSyncCount = Args.Has("-rrUncapped") ? 0 : 1;
            Settings.Load();
            var ui = Args.Value("-rrUiScale");
            if (ui != null) Settings.UiScale = float.Parse(ui, System.Globalization.CultureInfo.InvariantCulture);
            if (Args.Has("-rrReducedMotion")) Settings.ReducedMotion = true;
            var lib = ArtLibrary.I;
            Rig = CameraRig.Create();
            var pv = new GameObject("Post");
            Post = pv.AddComponent<Volume>();
            Post.isGlobal = true;
            Post.sharedProfile = lib.postFx;
            GalaxyGrade = BuildGalaxyGrade(pv.transform);
            Sky = new Material(lib.sky) { name = "Space Sky" };
            RenderSettings.skybox = Sky;
            Look.SeedDistantGalaxies();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            WorldRoot = new GameObject("World").transform;
            Audio = AudioService.Create(transform);
            var uiGo = new GameObject("UI");
            UI = uiGo.AddComponent<UIRoot>();
            Clock.DayAdvanced += OnDay;
            Selection.Changed += OnSelection;
        }

        IEnumerator Start()
        {
            var res = Args.Value("-rrRes");
            if (res != null)
            {
                var wh = res.Split('x');
                Screen.SetResolution(int.Parse(wh[0]), int.Parse(wh[1]), FullScreenMode.Windowed);
                yield return null; yield return null;
            }
            G = SliceScenario.Create();
            UI.Init(this);
            var off = Args.Value("-rrOffscreen");
            if (off != null)
            {
                // Offscreen evaluation at a resolution the attached displays cannot show natively (both are 1080p here):
                // the camera and the interface render into separate targets of that size and each shot composites them.
                var wh = off.Split('x');
                int w = int.Parse(wh[0]), h = int.Parse(wh[1]);
                OffscreenScene = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4, name = "Offscreen scene" };
                OffscreenUI = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "Offscreen UI" };
                Rig.Cam.targetTexture = OffscreenScene;
                UI.Doc.panelSettings.targetTexture = OffscreenUI;
                UI.Doc.panelSettings.clearColor = true;
                UI.Doc.panelSettings.colorClearValue = new Color(0, 0, 0, 0);
            }
            UI.Strategy.Root.style.display = DisplayStyle_None;
            // Chart the hero system up front (honest loading: shown with progress).
            var hero = G.GetSystem(SliceScenario.HeroSystem);
            var jobs = hero.Planets.Where(p => !Args.Has("-rrUiSmoke") && !Args.Has("-rrHoverReview")).Select(p => PlanetBaker.Start(p, p.Id == SliceScenario.HeroPlanet ? 1024 : p.Kind == PlanetKind.GasGiant ? 768 : 384)).ToList();
            while (jobs.Any(j => !j.Work.IsCompleted))
            {
                UI.ShowLoading("Charting the Aurelith system", jobs.Count(j => j.Work.IsCompleted) / (float)jobs.Count);
                yield return null;
            }
            foreach (var j in jobs) PlanetBaker.Finish(j);
            UI.ShowLoading("Mapping the galaxy", 1);
            yield return null;
            Galaxy = GalaxyPresentation.Build(G, WorldRoot);
            UI.HideLoading();
            UI.Strategy.Root.style.display = DisplayStyle_Flex;
            EnterGalaxyImmediate();
            Selection.SelectFleet(G, SliceScenario.PlayerFleet);
            Audio.Bed("amb_galaxy");
            PlaytestMetrics.Init(SavePath);
            var assetReview=Args.Value("-rrUnitReview");
            if(Args.Has("-rrPolishReview")){gameObject.AddComponent<UnitReviewCapture>();StartCoroutine(UnitPolishReview.Run(this,Args.Value("-rrPolishReview")));yield break;}
            if(OffscreenScene!=null && (assetReview!=null || Args.Has("-rrUnitCapture")))gameObject.AddComponent<UnitReviewCapture>();
            if(assetReview!=null){StartCoroutine(UnitGallery.Review(this,assetReview));yield break;}
            if(Args.Has("-rrGround")){StartGroundExercise();yield break;}
            var uiSmoke = Args.Value("-rrUiSmoke");
            if(uiSmoke != null) { StartCoroutine(Automation.UiSmoke(this,uiSmoke)); yield break; }
            var hoverReview = Args.Value("-rrHoverReview");
            if(hoverReview != null) { StartCoroutine(Automation.HoverReview(this,hoverReview)); yield break; }
            var tour = Args.Value("-rrTour");
            if (tour != null) StartCoroutine(Automation.FullTour(this, tour));
            var soak = Args.Value("-rrSoak");
            if (soak != null) StartCoroutine(Automation.Soak(this, soak, 10));
            var sheet = Args.Value("-rrShipSheet");
            if (sheet != null) { UI.Strategy.Root.style.display = DisplayStyle_None; StartCoroutine(Automation.ShipSheet(this, sheet)); }
            var review = Args.Value("-rrReview");
            if (review != null) StartCoroutine(Automation.PresentationReview(this, review));
            var cap = Args.Value("-rrCapture");
            if (cap != null) StartCoroutine(Automation.SystemTourFromGalaxy(this, cap));
            var passageTour = Args.Value("-rrPassageTour");
            if (passageTour != null) StartCoroutine(SliceAutomation.PassageTour(this, passageTour));
            var cameraCheck = Args.Value("-rrCameraCheck");
            if (cameraCheck != null) StartCoroutine(SliceAutomation.CameraCheck(this, cameraCheck));
            var campaignTour = Args.Value("-rrCampaignTour");
            if (campaignTour != null) StartCoroutine(CampaignAutomation.Tour(this, campaignTour));
            var problems = SliceRoster.Validate();
            if (problems.Count > 0) { foreach (var p in problems) Debug.LogError("Slice content: " + p); UI.Strategy.Toast("Slice content problem: " + problems[0], "bad"); }
            MaybeShowStartMenu();
        }

        static readonly UnityEngine.UIElements.StyleEnum<UnityEngine.UIElements.DisplayStyle> DisplayStyle_None = UnityEngine.UIElements.DisplayStyle.None;
        static readonly UnityEngine.UIElements.StyleEnum<UnityEngine.UIElements.DisplayStyle> DisplayStyle_Flex = UnityEngine.UIElements.DisplayStyle.Flex;

        public CamPose GalaxyHome => new CamPose { Focus = new Vector3(-10, 0, -20), Yaw = 0, Pitch = 62, Distance = 720, Fov = 40 };

        // Galaxy camera: steep from far out (62 degrees at the home view), flatter close in (40 degrees), so the disk shows depth.
        static float GalaxyPitch(float distance) => Mathf.Lerp(40f, 62f, Mathf.InverseLerp(60f, 720f, distance));

        public Volume GalaxyGrade { get; private set; }

        // A second global volume layered over the base profile for the galaxy view: a little more contrast and saturation
        // (closer to the faction map), a firmer vignette, and bloom that catches only the bright knots, capitals and borders.
        static Volume BuildGalaxyGrade(Transform parent)
        {
            var go = new GameObject("Galaxy grade");
            go.transform.SetParent(parent, false);
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1;
            vol.weight = 0;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Galaxy grade";
            var ca = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            // Gentle on purpose: the faint gas lives in the low values, and any lift below zero erases it.
            ca.postExposure.value = 0.25f;
            ca.contrast.value = 12f;
            ca.saturation.value = 12f;
            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.value = 0.95f;
            bloom.intensity.value = 0.7f;
            bloom.scatter.value = 0.72f;
            var vig = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vig.intensity.value = 0.3f;
            vig.smoothness.value = 0.45f;
            vol.sharedProfile = profile;
            return vol;
        }

        void SetGalaxyLimits()
        {
            Rig.Limits = new CamLimits { MinDistance = 22, MaxDistance = 900, MinPitch = 28, MaxPitch = 82, FocusBounds = new Bounds(Vector3.zero, new Vector3(860, 10, 640)) };
            Rig.Near = 0.3f; Rig.Far = 6000;
        }

        void EnterGalaxyImmediate()
        {
            Scale = ViewScale.Galaxy;
            Galaxy.gameObject.SetActive(true);
            SetGalaxyLimits();
            Rig.Snap(_haveGalaxyBookmark ? _galaxyBookmark : GalaxyHome);
            Sky.SetFloat("_BandStrength", 0.05f);
            Sky.SetFloat("_StarStrength", 0.7f);
            BuildGalaxyLabels();
            Galaxy.SetOverlay(Settings.Overlays);
            UI.Strategy.RefreshAll();
        }

        void Update()
        {
            Shader.SetGlobalFloat("_RR_Time", Time.unscaledTime);
            Shader.SetGlobalFloat("_RR_Motion", Settings.ReducedMotion ? 0.2f : 1f);   // scales purely ambient shader motion
            // The galaxy view has its own grade, faded in and out with the scale (about a third of a second).
            if (GalaxyGrade != null)
            {
                float want = Scale == ViewScale.Galaxy && !ShipSheetMode ? 1f : 0f;
                GalaxyGrade.weight = Mathf.MoveTowards(GalaxyGrade.weight, want, Time.unscaledDeltaTime / (Settings.ReducedMotion ? 0.1f : 0.35f));
            }
            Rig.PitchForDistance = !UnitGallery.Active && Scale == ViewScale.Galaxy ? GalaxyPitch : (Func<float, float>)null;
            if (G == null || Galaxy == null) return;
            Clock.Blocked = Busy || Scale == ViewScale.Battle || UI.ModalOpen;
            Clock.Tick(G, Time.unscaledDeltaTime);
            if (ShipSheetMode) { }
            else if (Scale == ViewScale.Galaxy) { Galaxy.RefreshFleets(Clock.Fraction); GalaxyLighting(); }
            else if (LocalSystem != null && LocalSystem.Ready && Scale != ViewScale.Battle) LocalSystem.ApplyLighting();
            else if (Scale == ViewScale.Battle && Battle != null) BattleLighting();
            if (!UnitGallery.Active && Scale == ViewScale.Planet && LocalSystem != null && Selection.PlanetId != null)
            {
                var pv = LocalSystem.Planet(Selection.PlanetId);
                if (pv != null && !Rig.Transitioning) Rig.Target.Focus = PlanetFramingFocus(pv.Root.position, pv.Radius, Rig.Target.Yaw, Rig.Target.Pitch);
            }
            if(!UnitGallery.Active)HandleInput();
            CampaignUpdate();
            UI.Strategy.RefreshTop();
            UI.Strategy.UpdateLabels(Rig.Cam, Settings.Overlays && !Busy && Scale != ViewScale.Battle);
            if (_battleHud != null) _battleHud.Update(Rig.Cam);
            if (_slicePanel != null) _slicePanel.Update(Rig.Cam);
            if (!UnitGallery.Active && Scale == ViewScale.Battle && Battle != null && Battle.Over && !_resultShown && ReviewDone()) FinishBattle();
            bool loading = Busy && !string.IsNullOrEmpty(LoadingText);
            if (loading) { UI.ShowLoading(LoadingText, LoadingProgress); _loadingShown = true; }
            else if (_loadingShown) { UI.HideLoading(); _loadingShown = false; }
        }

        void GalaxyLighting()
        {
            // Fleet models at galaxy scale are lit by a soft key from galactic north-west; markers are self-luminous.
            Shader.SetGlobalVector("_RR_StarPosWS", new Vector4(-3000, 4200, 2600, 1));
            Shader.SetGlobalVector("_RR_StarColor", new Color(1f, 0.96f, 0.9f) * 1.6f);
            Shader.SetGlobalVector("_RR_Ambient", new Vector4(0.05f, 0.055f, 0.07f, 0.4f));
        }

        void BattleLighting()
        {
            var d = Battle.StarDir;
            var p = Battle.transform.position + d * 2600;
            Shader.SetGlobalVector("_RR_StarPosWS", new Vector4(p.x, p.y, p.z, 34));
            Shader.SetGlobalVector("_RR_StarColor", Look.StarColor(G.GetSystem(GroundExercise ? SliceScenario.HeroSystem : BattleSystemId).Star) * 2.1f);
            Shader.SetGlobalVector("_RR_Ambient", new Vector4(0.02f, 0.024f, 0.034f, 0.35f));
        }

        // ------------------------------------------------------------------ strategic commands (UI and input call these)

        public void TogglePause()
        {
            if (G.PendingEngagement != null) { UI.Strategy.Toast("Time is held until the engagement at " + G.GetSystem(G.PendingEngagement.SystemId).Name + " is decided.", "bad"); OpenEngagement(); return; }
            Clock.Paused = !Clock.Paused;
            Audio.Play("ui_select", 0.5f);
        }

        public void SetSpeed(int s)
        {
            Clock.Speed = Mathf.Clamp(s, 1, 3);
            if (G.PendingEngagement == null) Clock.Paused = false;
            Audio.Play("ui_select", 0.5f);
        }

        public void ToggleOverlays()
        {
            Settings.Overlays = !Settings.Overlays;
            if (Galaxy != null) Galaxy.SetOverlay(Settings.Overlays);
            if (LocalSystem != null) LocalSystem.SetOverlay(Settings.Overlays);
            UI.Strategy.RefreshTop();
        }

        public void StartProject(string planetId, ProjectKind kind, string shipDefId)
        {
            var r = StrategyRules.StartProject(G, planetId, kind, shipDefId);
            Feedback(r, kind == ProjectKind.CommissionShip ? "Commissioning " + ShipCatalog.Name(shipDefId) + " at " + G.GetPlanet(planetId).Name + "." : "Work on a Crystal Foundry has begun.");
            UI.Strategy.RefreshAll();
        }

        public void CancelProject(string planetId)
        {
            Feedback(StrategyRules.CancelProject(G, planetId), "The project was cancelled.");
            UI.Strategy.RefreshAll();
        }

        public void CancelMove(string fleetId)
        {
            Feedback(StrategyRules.CancelMove(G, fleetId), G.GetFleet(fleetId).Name + " will hold its position.");
            Galaxy.ShowRoute(null, null);
            UI.Strategy.RefreshAll();
        }

        public void IssueMove(string targetSystemId)
        {
            var f = Selection.FleetId != null ? G.GetFleet(Selection.FleetId) : null;
            if (f == null || f.OwnerId != G.Player.Id) { UI.Strategy.Toast("Select one of your fleets first.", "bad"); Audio.Play("ui_invalid"); return; }
            var plan = StrategyRules.PlanRoute(G, f, targetSystemId);
            var r = StrategyRules.OrderMove(G, f.Id, targetSystemId);
            Feedback(r, f.Name + " is under way to " + G.GetSystem(targetSystemId).Name + ". Arrival in " + plan.Days + " days.");
            if (r.Ok) { Targeting = false; UI.Strategy.Hint(null); Galaxy.ShowRoute(f, f.Order != null ? f.Order.Path : null); }
            UI.Strategy.RefreshAll();
        }

        void Feedback(StratResult r, string okText)
        {
            if (r.Ok) { UI.Strategy.Toast(okText, "ok"); Audio.Play("order_accepted", 0.8f); }
            else { UI.Strategy.Toast(r.Reason, "bad"); Audio.Play("ui_invalid"); }
        }

        public void BeginMoveTargeting()
        {
            var f = Selection.FleetId != null ? G.GetFleet(Selection.FleetId) : null;
            if (f == null || f.OwnerId != G.Player.Id) return;
            if (Scale != ViewScale.Galaxy) { StartCoroutine(ExitThen(() => BeginMoveTargeting())); return; }
            Targeting = true;
            UI.Strategy.Hint("Choose a destination for " + f.Name + ". Click a system to order the move, or press Escape to cancel.");
            UI.Strategy.RefreshInspector();
        }

        IEnumerator ExitThen(Action then)
        {
            yield return ExitToGalaxyRoutine();
            then();
        }

        void OnSelection()
        {
            if (Galaxy != null) Galaxy.SetSelected(Selection.SystemId);
            if (Scale == ViewScale.Galaxy && Galaxy != null)
            {
                var f = Selection.FleetId != null ? G.GetFleet(Selection.FleetId) : null;
                Galaxy.ShowRoute(f, f != null && f.Moving ? f.Order.Path : null);
            }
            UI.Strategy.RefreshOutliner();
            UI.Strategy.RefreshInspector();
        }

        /// <summary>Galaxy view: select a fleet and fly the camera close to it (double-click it, C, or pick it in the fleet list).</summary>
        public void FlyToFleet(string id)
        {
            var f = G.GetFleet(id);
            if (f == null || Scale != ViewScale.Galaxy) return;
            Selection.SelectFleet(G, id);
            Audio.Play("ui_select", 0.6f);
            var p = Galaxy.FleetMarkers.ContainsKey(id) ? Galaxy.FleetMarkers[id].position : GalaxyScale.ToWorld(G.GetSystem(f.SystemId));
            var to = Rig.Target; to.Focus = p; to.Distance = Mathf.Min(Rig.Target.Distance, FleetCloseDistance);
            to.Pitch = GalaxyPitch(to.Distance) + (Rig.Target.Pitch - GalaxyPitch(Rig.Target.Distance));   // keep the player's tilt offset
            Rig.TransitionTo(to, 0.45f);
        }

        public void SelectAndFocusFleet(string id)
        {
            var f = G.GetFleet(id);
            if (f == null) return;
            if (Scale == ViewScale.Galaxy) { FlyToFleet(id); return; }
            Selection.SelectFleet(G, id);
            Audio.Play("ui_select", 0.6f);
            if (LocalSystem != null && LocalSystem.Data.Id == f.SystemId && LocalSystem.Fleets.ContainsKey(id))
            {
                if (Scale == ViewScale.Planet) Scale = ViewScale.System;
                var to = Rig.Current; to.Focus = LocalSystem.Fleets[id].Root.position; to.Distance = 16; to.Pitch = 18;
                Rig.TransitionTo(to, 0.6f);
            }
        }

        public void SelectAndFocusSystem(string id)
        {
            Selection.SelectSystem(id);
            Audio.Play("ui_select", 0.6f);
            if (Scale == ViewScale.Galaxy)
            {
                var to = Rig.Current; to.Focus = GalaxyScale.ToWorld(G.GetSystem(id)); to.Distance = Mathf.Min(Rig.Current.Distance, 220);
                Rig.TransitionTo(to, 0.45f);
            }
            else if (LocalSystem == null || LocalSystem.Data.Id != id) EnterSystem(id);
        }

        // ------------------------------------------------------------------ day loop

        void OnDay(DayReport rep)
        {
            Galaxy.RefreshFleets(0);
            Galaxy.RefreshOwnership();
            if (LocalSystem != null)
            {
                LocalSystem.RefreshFleets();
                foreach (var p in LocalSystem.Planets) LocalSystem.PlaceOrbit(p, G.Day);
            }
            foreach (var pid in rep.Completed)
            {
                Audio.Play("build_complete", 0.7f);
                var p = G.GetPlanet(pid);
                UI.Strategy.Toast(G.Log.LastOrDefault()?.Text ?? (p.Name + " completed its project."), "ok");
            }
            foreach (var fid in rep.Arrivals)
            {
                var f = G.GetFleet(fid);
                if (f != null && f.OwnerId == G.Player.Id) { Audio.Play("fleet_arrival", 0.6f); UI.Strategy.Toast(f.Name + " arrived at " + G.GetSystem(f.SystemId).Name + ".", "ok"); }
            }
            Selection.Validate(G);
            UI.Strategy.RefreshAll();
            if (rep.NewEngagement != null)
            {
                Audio.Play("contact");
                UI.Strategy.Toast("Contact at " + G.GetSystem(rep.NewEngagement.SystemId).Name + ". Time is held for your decision.", "danger");
                OpenEngagement();
            }
        }
    }

    public static class Args
    {
        static readonly string[] _a = Environment.GetCommandLineArgs();
        public static bool Has(string k) => Array.IndexOf(_a, k) >= 0;
        public static string Value(string k) { int i = Array.IndexOf(_a, k); return i >= 0 && i + 1 < _a.Length ? _a[i + 1] : null; }
    }
}