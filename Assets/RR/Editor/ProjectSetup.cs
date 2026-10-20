using System;
using System.IO;
using System.Linq;
using RR.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace RR.EditorTools
{
    // Idempotent project configuration. Menu: RR > Setup Project, or batchmode
    // -executeMethod RR.EditorTools.ProjectSetup.RunBatch.
    public static class ProjectSetup
    {
        public const string Root = "Assets/RR";
        public const string Settings = Root + "/Settings";
        public const string ScenePath = Root + "/Scenes/RuleAndRuin.unity";
        public const string LibraryPath = "Assets/Resources/RR/ArtLibrary.asset";

        [MenuItem("RR/Setup Project")]
        public static void Run()
        {
            Ensure(Settings); Ensure(Root + "/Scenes"); Ensure("Assets/Resources/RR");
            ConfigureRendering();
            ConfigurePlayer();
            AssetDatabase.Refresh();
            BuildLibrary();
            BuildScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[RR Setup] complete");
        }

        public static void RunBatch()
        {
            try { Run(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Ensure(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int cut = path.LastIndexOf('/');
            var parent = path.Substring(0, cut);
            Ensure(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(cut + 1));
        }

        static void ConfigureRendering()
        {
            string rendererPath = Settings + "/RR_Renderer.asset", urpPath = Settings + "/RR_URP.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null) { rendererData = ScriptableObject.CreateInstance<UniversalRendererData>(); AssetDatabase.CreateAsset(rendererData, rendererPath); }
            if (rendererData.postProcessData == null)
                rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            EditorUtility.SetDirty(rendererData);
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpPath);
            if (urp == null) { urp = UniversalRenderPipelineAsset.Create(rendererData); AssetDatabase.CreateAsset(urp, urpPath); }
            var so = new SerializedObject(urp);
            void B(string n, bool v) { var p = so.FindProperty(n); if (p != null) p.boolValue = v; else Debug.LogWarning("[RR Setup] missing " + n); }
            void I(string n, int v) { var p = so.FindProperty(n); if (p != null) p.intValue = v; else Debug.LogWarning("[RR Setup] missing " + n); }
            void F(string n, float v) { var p = so.FindProperty(n); if (p != null) p.floatValue = v; else Debug.LogWarning("[RR Setup] missing " + n); }
            B("m_SupportsHDR", true);
            I("m_MSAA", 4);
            F("m_RenderScale", 1f);
            B("m_RequireDepthTexture", true);          // the galaxy volume clamps its rays against scene depth
            B("m_RequireOpaqueTexture", false);
            B("m_MainLightShadowsSupported", false);   // star lighting is analytic; ships carry baked AO
            B("m_AdditionalLightShadowsSupported", false);
            B("m_UseSRPBatcher", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);
            GraphicsSettings.defaultRenderPipeline = urp;
            int cur = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++) { QualitySettings.SetQualityLevel(i, false); QualitySettings.renderPipeline = urp; }
            QualitySettings.SetQualityLevel(cur, false);

            string fxPath = Settings + "/RR_PostFX.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(fxPath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, fxPath);
                var bloom = profile.Add<Bloom>(true);
                bloom.threshold.Override(1.1f); bloom.intensity.Override(0.5f); bloom.scatter.Override(0.7f);
                var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                var ca = profile.Add<ColorAdjustments>(true); ca.postExposure.Override(0.2f); ca.contrast.Override(8f); ca.saturation.Override(4f);
                var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.24f); vig.smoothness.Override(0.5f);
                foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
                EditorUtility.SetDirty(profile);
            }
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Rule and Ruin";
            PlayerSettings.productName = "Rule and Ruin Slice";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, "com.ruleandruin.slice");
            var ps = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset").FirstOrDefault();
            if (ps != null)
            {
                var so = new SerializedObject(ps);
                var input = so.FindProperty("activeInputHandler");
                if (input != null) input.intValue = 1;   // Input System only
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static T Load<T>(string path) where T : UnityEngine.Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogWarning("[RR Setup] missing asset " + path);
            return a;
        }

        static Shader Sh(string n) => AssetDatabase.LoadAssetAtPath<Shader>(Root + "/Shaders/RR_" + n + ".shader");

        static System.Collections.Generic.List<T> All<T>(string filter, string folder) where T : UnityEngine.Object =>
            !AssetDatabase.IsValidFolder(folder) ? new System.Collections.Generic.List<T>() :
            AssetDatabase.FindAssets(filter, new[] { folder }).Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(g => g != null).OrderBy(g => g.name).ToList();

        static void BuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<ArtLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
            lib.planet = Sh("Planet"); lib.atmosphere = Sh("Atmosphere"); lib.ring = Sh("Ring"); lib.starSurface = Sh("StarSurface");
            lib.corona = Sh("Corona"); lib.ship = Sh("Ship"); lib.sky = Sh("SpaceSky"); lib.starField = Sh("StarField");
            lib.galaxyVolume = Sh("GalaxyVolume"); lib.territory = Sh("Territory"); lib.lane = Sh("Lane"); lib.marker = Sh("Marker");
            lib.hexGrid = Sh("HexGrid"); lib.fx = Sh("Fx"); lib.haze = Sh("Haze");
            lib.postFx = Load<VolumeProfile>(Settings + "/RR_PostFX.asset");
            lib.displayFont = Load<Font>(Root + "/UI/Fonts/Cinzel-Variable.ttf");
            lib.bodyFont = Load<Font>(Root + "/UI/Fonts/Barlow-Regular.ttf");
            lib.bodySemiBold = Load<Font>(Root + "/UI/Fonts/Barlow-SemiBold.ttf");
            lib.bodyBold = Load<Font>(Root + "/UI/Fonts/Barlow-Bold.ttf");
            lib.condensed = Load<Font>(Root + "/UI/Fonts/BarlowCondensed-SemiBold.ttf");
            lib.panelSettings = BuildPanelSettings();
            lib.models = All<GameObject>("t:GameObject", Root + "/Art/Models");
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Art/Units"})) {var assetPath=AssetDatabase.GUIDToAssetPath(guid);if(!assetPath.EndsWith(".prefab"))continue;var asset=Load<GameObject>(assetPath);if(asset!=null&&!asset.name.Contains("__variant")){lib.models.RemoveAll(m=>m!=null&&m.name==asset.name);lib.models.Add(asset);}}
            lib.audio = All<AudioClip>("t:AudioClip", Root + "/Audio");
            lib.icons = All<Texture2D>("t:Texture2D", Root + "/UI/Icons");
            EditorUtility.SetDirty(lib);
            Debug.Log("[RR Setup] library: " + lib.models.Count + " models, " + lib.audio.Count + " clips, " + lib.icons.Count + " icons");
        }

        static PanelSettings BuildPanelSettings()
        {
            string path = Root + "/UI/RR_Panel.asset";
            var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
            if (ps == null) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, path); }
            ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/RR_Theme.tss");
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            ps.sortingOrder = 10;
            EditorUtility.SetDirty(ps);
            return ps;
        }

        static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Rule and Ruin").AddComponent<GameRoot>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }

    // Import rules for the Blender-generated models: axis conversion baked, no imported materials.
    public sealed class RRModelPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/RR/Art/Models")) return;
            var mi = (ModelImporter)assetImporter;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.importAnimation = false;
            mi.animationType = ModelImporterAnimationType.None;
            mi.bakeAxisConversion = true;
            mi.isReadable = assetPath.Contains("asteroid");
            mi.importNormals = ModelImporterNormals.Import;
            mi.importTangents = ModelImporterTangents.None;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.useFileScale = true;
            mi.globalScale = 1f;
            mi.importCameras = false;
            mi.importLights = false;
        }
    }

    public static class BuildTools
    {
        [MenuItem("RR/Build Windows")]
        public static void BuildMenu() { Build("Builds/Windows/RuleAndRuin.exe"); }

        public static void BuildWindows()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-rrBuildOut");
            string outPath = i >= 0 && i + 1 < args.Length ? args[i + 1] : "Builds/Windows/RuleAndRuin.exe";
            if (args.Contains("-rrSetup")) ProjectSetup.Run();
            EditorApplication.Exit(Build(outPath) ? 0 : 1);
        }

        static bool Build(string outPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath }, locationPathName = outPath, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log("[RR Build] " + s.result + ": " + (s.totalSize / (1024 * 1024)) + " MB in " + s.totalTime.TotalSeconds.ToString("0") + "s, " + s.totalErrors + " errors -> " + outPath);
            return s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        }
    }
}