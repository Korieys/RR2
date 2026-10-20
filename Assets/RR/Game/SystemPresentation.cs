using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Local system scale. All presentation distances are local units; the galaxy uses its own scale (GalaxyPresentation).
    public static class SystemScale
    {
        public const float Orbit = 34f;          // units per strategic orbit unit
        public const float PlanetRadius = 5.0f;  // units per strategic planet size (strategy readability exaggeration)
        public const float Ship = 0.85f;         // ship model scale (models are about 0.5 to 2.9 long)
        public const float Station = 1.1f;
        public static float PeriodDays(double orbit) { return 160f * Mathf.Pow((float)orbit, 1.5f); }
    }

    public sealed class PlanetView
    {
        public Planet Data;
        public Transform Root, Body;
        public float Radius;
        public Material Surface;
        public Material Atmosphere;
    }

    public sealed class FleetView
    {
        public string FleetId;
        public Transform Root;
        public readonly Dictionary<int, Transform> Ships = new Dictionary<int, Transform>();
        public Vector3 Anchor;
    }

    // Builds and animates one star system from strategic data. Owns nothing authoritative: it reads GalaxyState.
    public sealed class SystemPresentation : MonoBehaviour
    {
        public StarSystem Data { get; private set; }
        public readonly List<PlanetView> Planets = new List<PlanetView>();
        public readonly Dictionary<string, FleetView> Fleets = new Dictionary<string, FleetView>();
        public Transform Star { get; private set; }
        public Transform StationT { get; private set; }
        public float StarRadius { get; private set; }
        public float OuterRadius { get; private set; }
        public bool Ready { get; private set; }
        readonly List<GameObject> _overlay = new List<GameObject>();
        GalaxyState _g;
        float _spin;

        public static SystemPresentation Build(GalaxyState g, StarSystem sys, Transform parent)
        {
            var go = new GameObject("System " + sys.Name);
            go.transform.SetParent(parent, false);
            var sp = go.AddComponent<SystemPresentation>();
            sp._g = g;
            sp.Data = sys;
            return sp;
        }

        public IEnumerator Load(System.Action<float, string> progress)
        {
            var lib = ArtLibrary.I;
            StarRadius = Look.StarRadius(Data.Star) * 0.82f;
            var starGo = new GameObject("Star");
            starGo.transform.SetParent(transform, false);
            Star = starGo.transform;
            var body = Child(Star, "Photosphere", MeshFactory.Sphere(48), new Material(lib.starSurface));
            body.transform.localScale = Vector3.one * StarRadius;
            var sc = Look.StarColor(Data.Star);
            var starMat = body.GetComponent<MeshRenderer>().sharedMaterial;
            starMat.SetColor("_Color", sc);
            starMat.SetFloat("_Intensity", Data.Star == StarClass.Dim ? 1.5f : 2.8f);
            var corona = Child(Star, "Corona", MeshFactory.Quad(), new Material(lib.corona));
            corona.transform.localScale = Vector3.one * StarRadius * 4.5f;
            var coronaMat = corona.GetComponent<MeshRenderer>().sharedMaterial;
            coronaMat.SetColor("_Color", sc);
            coronaMat.SetFloat("_StarRadius", 1f / 4.5f);
            coronaMat.SetFloat("_Intensity", Data.Star == StarClass.Dim ? 0.3f : 0.72f);

            // Planets: bake (or reuse) their surfaces off the main thread.
            var jobs = new List<(Planet p, PlanetBaker.Job job)>();
            foreach (var p in Data.Planets)
                if (!PlanetBaker.TryGet(p.Id, out _))
                    jobs.Add((p, PlanetBaker.Start(p, BakeSize(p))));
            while (jobs.Any(j => !j.job.Work.IsCompleted))
            {
                int now = jobs.Count(j => j.job.Work.IsCompleted);
                if (progress != null) progress(jobs.Count == 0 ? 1 : now / (float)jobs.Count, "Charting " + Data.Name);
                yield return null;
            }
            foreach (var j in jobs) PlanetBaker.Finish(j.job);
            foreach (var p in Data.Planets) Planets.Add(BuildPlanet(p));
            OuterRadius = Planets.Count > 0 ? Planets.Max(p => (float)p.Data.OrbitRadius) * SystemScale.Orbit + 30 : 120;

            if (Data.HasStation)
            {
                var host = Planets.FirstOrDefault(p => p.Data.HasShipyard) ?? Planets.FirstOrDefault();
                var model = lib.Model("station_silex_citadel");
                if (model != null && host != null)
                {
                    var st = Instantiate(model, host.Root);
                    st.name = "Station";
                    ApplyShipMaterial(st, "station_silex_citadel");
                    st.transform.localPosition = new Vector3(host.Radius * 2.6f, host.Radius * 0.35f, host.Radius * 0.6f);
                    st.transform.localScale = Vector3.one * SystemScale.Station;
                    StationT = st.transform;
                }
            }
            if (Data.Id == "spore_drift" || Data.Id == "cinder_verge") BuildBelt(Data.Id == "spore_drift" ? 2.0f : 2.2f);
            RefreshFleets();
            SetOverlay(true);
            Ready = true;
            if (progress != null) progress(1, Data.Name);
        }

        static int BakeSize(Planet p)
        {
            return p.Id == SliceScenario.HeroPlanet ? 1024 : p.Kind == PlanetKind.GasGiant && p.Size > 2 ? 768 : 512;
        }

        static GameObject Child(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }

        public static void ApplyShipMaterial(GameObject model, string defId)
        {
            if (model.GetComponent<UnitVisual>() != null) return;
            var m = Look.ShipMaterial(defId);
            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        PlanetView BuildPlanet(Planet p)
        {
            var lib = ArtLibrary.I;
            PlanetTextures tex;
            PlanetBaker.TryGet(p.Id, out tex);
            var root = new GameObject(p.Name).transform;
            root.SetParent(transform, false);
            float radius = (float)p.Size * SystemScale.PlanetRadius;
            var pv = new PlanetView { Data = p, Root = root, Radius = radius };
            var surf = new Material(lib.planet) { name = "Planet " + p.Name };
            surf.SetTexture("_AlbedoCube", tex.Albedo);
            surf.SetTexture("_NormalCube", tex.Normal);
            surf.SetTexture("_DetailCube", tex.Detail);
            surf.SetTexture("_CloudCube", tex.Clouds);
            var look = KindLook(p.Kind);
            surf.SetColor("_AtmoColor", look.atmo);
            surf.SetFloat("_AtmoStrength", look.haze);
            surf.SetColor("_CloudColor", look.cloud);
            surf.SetFloat("_CloudAmount", look.clouds);
            surf.SetColor("_CityColor", look.city);
            surf.SetColor("_EmisColor", look.emis);
            surf.SetFloat("_Glint", p.Kind == PlanetKind.Crystal ? 1.2f : 0f);
            var bodyGo = Child(root, "Body", MeshFactory.Sphere(p.Id == SliceScenario.HeroPlanet ? 96 : 64), surf);
            bodyGo.transform.localScale = Vector3.one * radius;
            bodyGo.transform.localRotation = Quaternion.Euler(Noise.Rand01(p.Seed, 3) * 20 - 10, 0, Noise.Rand01(p.Seed, 4) * 24 - 12);
            pv.Body = bodyGo.transform;
            pv.Surface = surf;
            if (look.atmoShell > 0)
            {
                var atmoMat = new Material(lib.atmosphere) { name = "Atmosphere " + p.Name };
                atmoMat.SetColor("_AtmoColor", look.atmo);
                atmoMat.SetColor("_SunsetColor", look.sunset);
                atmoMat.SetFloat("_Intensity", look.atmoShell);
                float shell = 1f + look.thickness;
                atmoMat.SetFloat("_PlanetRadius", 1f / shell);
                var atmoGo = Child(root, "Atmosphere", MeshFactory.Sphere(48), atmoMat);
                atmoGo.transform.localScale = Vector3.one * radius * shell;
                pv.Atmosphere = atmoMat;
            }
            if (p.Rings && tex.RingBands != null)
            {
                float inner = 1.45f, outer = 2.5f;
                var ringMat = new Material(lib.ring) { name = "Rings " + p.Name };
                ringMat.SetTexture("_RingTex", tex.RingBands);
                ringMat.SetFloat("_Inner", inner); ringMat.SetFloat("_Outer", outer); ringMat.SetFloat("_PlanetRadius", 1f);
                Child(pv.Body, "Rings", MeshFactory.Ring(inner, outer), ringMat);
                surf.SetTexture("_RingTex", tex.RingBands);
                surf.SetFloat("_RingInner", inner); surf.SetFloat("_RingOuter", outer);
            }
            for (int m = 0; m < p.Moons; m++) BuildMoon(pv, m);
            var orbit = Child(transform, "Orbit " + p.Name, MeshFactory.Circle((float)p.OrbitRadius * SystemScale.Orbit, 0.35f), Look.Unlit(lib.fx, new Color(0.35f, 0.42f, 0.55f) * 0.35f, "Orbit"));
            _overlay.Add(orbit);
            PlaceOrbit(pv, _g.Day);
            return pv;
        }

        void BuildMoon(PlanetView host, int index)
        {
            var lib = ArtLibrary.I;
            string id = host.Data.Id + "_moon" + index;
            var moonData = new Planet { Id = id, Kind = PlanetKind.Barren, Seed = SliceScenario.Hash(id), Size = 0.2 };
            PlanetTextures tex;
            if (!PlanetBaker.TryGet(id, out tex))
            {
                var job = PlanetBaker.Start(moonData, 128);
                job.Work.Wait();
                tex = PlanetBaker.Finish(job);
            }
            var mat = new Material(lib.planet) { name = "Moon" };
            mat.SetTexture("_AlbedoCube", tex.Albedo); mat.SetTexture("_NormalCube", tex.Normal); mat.SetTexture("_DetailCube", tex.Detail);
            mat.SetTexture("_CloudCube", tex.Clouds); mat.SetFloat("_CloudAmount", 0); mat.SetFloat("_AtmoStrength", 0);
            var go = Child(host.Root, "Moon " + index, MeshFactory.Sphere(32), mat);
            float radius = host.Radius * (0.18f + 0.06f * index);
            go.transform.localScale = Vector3.one * radius;
            float dist = host.Radius * (3.4f + 1.6f * index) + (host.Data.Rings ? host.Radius * 1.6f : 0);
            float a = Noise.Rand01(moonData.Seed, 1) * Mathf.PI * 2;
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, host.Radius * 0.15f * (index % 2 == 0 ? 1 : -1), Mathf.Sin(a) * dist);
        }

        void BuildBelt(float orbit)
        {
            var lib = ArtLibrary.I;
            var belt = new GameObject("Belt").transform;
            belt.SetParent(transform, false);
            var rnd = new System.Random(Data.Seed);
            var meshes = new[] { "asteroid_0", "asteroid_1", "asteroid_2", "asteroid_3" }.Select(lib.Model).Where(x => x != null).ToArray();
            if (meshes.Length == 0) return;
            for (int i = 0; i < 260; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2;
                float dist = orbit * SystemScale.Orbit + (float)(rnd.NextDouble() - 0.5) * 16f;
                var go = Instantiate(meshes[rnd.Next(meshes.Length)], belt);
                ApplyShipMaterial(go, "asteroid");
                go.transform.localPosition = new Vector3(Mathf.Cos(a) * dist, (float)(rnd.NextDouble() - 0.5) * 3f, Mathf.Sin(a) * dist);
                go.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 360, (float)rnd.NextDouble() * 360);
                float sz = Mathf.Lerp(0.08f, 0.55f, Mathf.Pow((float)rnd.NextDouble(), 3));
                go.transform.localScale = Vector3.one * sz;
            }
        }

        struct KindStyle { public Color atmo, sunset, cloud, city, emis; public float haze, clouds, atmoShell, thickness; }

        static KindStyle KindLook(PlanetKind k)
        {
            switch (k)
            {
                case PlanetKind.Crystal: return new KindStyle { atmo = new Color(0.36f, 0.62f, 1f), sunset = new Color(1f, 0.6f, 0.4f), cloud = new Color(0.96f, 0.97f, 1f),
                    city = new Color(0.55f, 0.85f, 1f), haze = 0.72f, clouds = 0.52f, atmoShell = 1.25f, thickness = 0.032f };
                case PlanetKind.Terran: return new KindStyle { atmo = new Color(0.35f, 0.58f, 1f), sunset = new Color(1f, 0.55f, 0.32f), cloud = Color.white,
                    city = new Color(1f, 0.78f, 0.45f), haze = 1.0f, clouds = 0.85f, atmoShell = 1.6f, thickness = 0.045f };
                case PlanetKind.Ocean: return new KindStyle { atmo = new Color(0.32f, 0.6f, 1f), sunset = new Color(1f, 0.55f, 0.32f), cloud = Color.white,
                    city = new Color(1f, 0.8f, 0.5f), haze = 1.1f, clouds = 0.9f, atmoShell = 1.7f, thickness = 0.05f };
                case PlanetKind.Toxic: return new KindStyle { atmo = new Color(0.55f, 0.75f, 0.3f), sunset = new Color(0.8f, 0.4f, 0.9f), cloud = new Color(0.72f, 0.78f, 0.45f),
                    city = new Color(0.8f, 0.4f, 1f), emis = new Color(0.65f, 0.25f, 1f) * 0.8f, haze = 1.2f, clouds = 0.7f, atmoShell = 1.5f, thickness = 0.06f };
                case PlanetKind.Volcanic: return new KindStyle { atmo = new Color(0.7f, 0.35f, 0.18f), sunset = new Color(1f, 0.4f, 0.15f), cloud = new Color(0.32f, 0.28f, 0.26f),
                    emis = new Color(1f, 0.35f, 0.06f) * 2.2f, haze = 0.5f, clouds = 0.6f, atmoShell = 0.6f, thickness = 0.03f };
                case PlanetKind.Ice: return new KindStyle { atmo = new Color(0.6f, 0.75f, 1f), sunset = new Color(0.8f, 0.85f, 1f), cloud = Color.white,
                    haze = 0.4f, clouds = 0f, atmoShell = 0.45f, thickness = 0.025f };
                case PlanetKind.GasGiant: return new KindStyle { atmo = new Color(0.9f, 0.75f, 0.55f), sunset = new Color(1f, 0.6f, 0.4f), cloud = Color.white,
                    haze = 0.7f, clouds = 0f, atmoShell = 1.1f, thickness = 0.035f };
                default: return new KindStyle { atmo = new Color(0.5f, 0.5f, 0.55f), haze = 0f, clouds = 0f, atmoShell = 0f };
            }
        }

        public void PlaceOrbit(PlanetView pv, float day)
        {
            float a = (float)pv.Data.OrbitPhase + day / SystemScale.PeriodDays(pv.Data.OrbitRadius) * Mathf.PI * 2;
            float dist = (float)pv.Data.OrbitRadius * SystemScale.Orbit;
            pv.Root.localPosition = new Vector3(Mathf.Cos(a) * dist, 0, Mathf.Sin(a) * dist);
        }

        public PlanetView Planet(string id) { return Planets.FirstOrDefault(p => p.Data.Id == id); }

        // Arrival framing: between the star and the system's principal world, so both read on entry.
        public Vector3 FramingFocus
        {
            get
            {
                var main = Planets.FirstOrDefault(p => p.Data.HasShipyard) ?? Planets.FirstOrDefault(p => p.Data.OwnerId != null) ?? Planets.FirstOrDefault();
                return main != null ? Vector3.Lerp(Vector3.zero, main.Root.localPosition, 0.45f) : Vector3.zero;
            }
        }

        // Synchronises fleet formations with the strategic state (call after any strategic change).
        public void RefreshFleets()
        {
            var present = _g.Fleets.Where(f => f.SystemId == Data.Id && f.Ships.Count > 0).ToList();
            foreach (var id in Fleets.Keys.ToList())
                if (present.All(f => f.Id != id)) { Destroy(Fleets[id].Root.gameObject); Fleets.Remove(id); }
            foreach (var f in present)
            {
                FleetView fv;
                if (!Fleets.TryGetValue(f.Id, out fv))
                {
                    fv = new FleetView { FleetId = f.Id, Root = new GameObject("Fleet " + f.Name).transform };
                    fv.Root.SetParent(transform, false);
                    Fleets[f.Id] = fv;
                }
                fv.Anchor = FleetAnchor(f);
                fv.Root.localPosition = fv.Anchor;
                fv.Root.localRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Facing(f, fv.Anchor), Vector3.up).normalized, Vector3.up);
                foreach (var uid in fv.Ships.Keys.ToList())
                    if (f.Ships.All(s => s.Uid != uid)) { Destroy(fv.Ships[uid].gameObject); fv.Ships.Remove(uid); }
                var order = f.Ships.OrderByDescending(s => ShipCatalog.IsLeader(s.DefId)).ThenByDescending(s => ShipCatalog.Tier(s.DefId)).ThenBy(s => s.Uid).ToList();
                for (int i = 0; i < order.Count; i++)
                {
                    var s = order[i];
                    Transform t;
                    if (!fv.Ships.TryGetValue(s.Uid, out t))
                    {
                        var model = ArtLibrary.I.Model(s.DefId);
                        if (model == null) continue;
                        var go = Instantiate(model, fv.Root);
                        go.name = ShipCatalog.Name(s.DefId) + " #" + s.Uid;
                        ApplyShipMaterial(go, s.DefId);
                        go.transform.localScale = Vector3.one * SystemScale.Ship;
                        t = go.transform;
                        fv.Ships[s.Uid] = t;
                    }
                    t.localPosition = FormationSlot(i);
                    t.localRotation = Quaternion.identity;
                    ShipSuspension.Attach(t,s.Uid);
                    float dmg = 1f - s.Hp / (float)Mathf.Max(1, ShipCatalog.MaxHp(s.DefId));
                    var block = new MaterialPropertyBlock();
                    if (dmg > 0.05f) block.SetFloat("_Damage", dmg);
                    foreach (var mr in t.GetComponentsInChildren<Renderer>()) mr.SetPropertyBlock(block);
                }
            }
        }

        static Vector3 FormationSlot(int i)
        {
            if (i == 0) return Vector3.zero;
            int row = (i + 1) / 2, side = i % 2 == 0 ? 1 : -1;
            return new Vector3(side * row * 1.9f, (row % 2) * 0.45f - 0.15f, -row * 1.6f);
        }

        Vector3 FleetAnchor(Fleet f)
        {
            if (Data.OwnerId == f.OwnerId)
            {
                var home = Planets.FirstOrDefault(p => p.Data.HasShipyard) ?? Planets.FirstOrDefault(p => p.Data.OwnerId == f.OwnerId) ?? Planets.FirstOrDefault();
                if (home != null) return home.Root.localPosition + new Vector3(-home.Radius * 2.2f, home.Radius * 0.6f, -home.Radius * 2.6f);
            }
            // Visiting fleets hold at the jump point on the side of the system they came from (continuity with the galaxy map).
            var dir = GalaxyDirection(f.LastSystemId ?? f.HomeSystemId);
            return dir * (OuterRadius * 0.9f) + Vector3.up * 4f;
        }

        Vector3 Facing(Fleet f, Vector3 anchor)
        {
            var target = Planets.FirstOrDefault(p => p.Data.HasShipyard) ?? Planets.FirstOrDefault();
            if (Data.OwnerId != f.OwnerId && target != null) return target.Root.localPosition - anchor;
            var dir = GalaxyDirection(f.Order != null && f.Order.Path.Count > 0 ? f.Order.Path[0] : null);
            return dir.sqrMagnitude > 0 ? dir : -anchor.normalized + Vector3.right * 0.3f;
        }

        // Direction (local XZ) toward another system as seen on the galaxy map.
        public Vector3 GalaxyDirection(string otherId)
        {
            var o = otherId != null ? _g.GetSystem(otherId) : null;
            if (o == null || o == Data) return new Vector3(0.6f, 0, -0.8f);
            var d = new Vector3((float)(o.X - Data.X), 0, (float)(o.Y - Data.Y));
            return d.sqrMagnitude < 1e-3f ? new Vector3(0.6f, 0, -0.8f) : d.normalized;
        }

        public void SetOverlay(bool on) { foreach (var o in _overlay) if (o != null) o.SetActive(on); }

        void Update()
        {
            if (!Ready) return;
            float t = Time.unscaledDeltaTime * (Settings.ReducedMotion ? 0f : 1f);
            _spin += t;
            foreach (var p in Planets)
            {
                p.Body.Rotate(0, t * (p.Data.Kind == PlanetKind.GasGiant ? 2.2f : 1.2f), 0, Space.Self);
                if (p.Surface != null) p.Surface.SetFloat("_CloudRot", _spin * 0.012f);
            }
            if (StationT != null) StationT.Rotate(0, t * 3f, 0, Space.Self);
        }

        public void ApplyLighting()
        {
            var sc = Look.StarColor(Data.Star);
            Shader.SetGlobalVector("_RR_StarPosWS", new Vector4(Star.position.x, Star.position.y, Star.position.z, StarRadius));
            Shader.SetGlobalVector("_RR_StarColor", sc * Look.StarLight(Data.Star));
            Shader.SetGlobalVector("_RR_Ambient", new Vector4(0.016f, 0.022f, 0.034f, 0.25f));
        }
    }
}