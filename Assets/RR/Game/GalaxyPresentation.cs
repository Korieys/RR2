using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Galaxy scale: 1 unit = 10 light years. Strategic data coordinates (ly) map to render coordinates here and only here.
    public static class GalaxyScale
    {
        public const float LyPerUnit = 10f;
        public static Vector3 ToWorld(double x, double y, double z) { return new Vector3((float)(x / LyPerUnit), (float)(z / LyPerUnit), (float)(y / LyPerUnit)); }
        public static Vector3 ToWorld(StarSystem s) { return ToWorld(s.X, s.Y, s.Z); }
        public static readonly Vector3 VolumeSize = new Vector3(900, 52, 680);
    }

    public sealed class SystemMarker
    {
        public StarSystem Data;
        public Transform T;
        public Material Mat;
        public Transform Ring;
        public Material RingMat;
    }

    // The strategic galaxy as a finite-depth space scene: ambient stars, ray-marched nebular structure, actionable
    // system markers, lanes, fleets, and a political overlay that can be switched off without emptying the scene.
    public sealed class GalaxyPresentation : MonoBehaviour
    {
        GalaxyState _g;
        public readonly Dictionary<string, SystemMarker> Markers = new Dictionary<string, SystemMarker>();
        public readonly Dictionary<string, Transform> FleetMarkers = new Dictionary<string, Transform>();
        Material _starMat, _volMat, _terrMat, _laneMat, _routeMat;
        GameObject _overlayRoot, _territory, _lanes, _route;
        Texture3D _density;
        static Texture3D s_density;
        static string s_densityKey;
        public Vector3 EclipseCenter { get; private set; }

        public static GalaxyPresentation Build(GalaxyState g, Transform parent)
        {
            var go = new GameObject("Galaxy");
            go.transform.SetParent(parent, false);
            var gp = go.AddComponent<GalaxyPresentation>();
            gp._g = g;
            gp.Construct();
            return gp;
        }

        void Construct()
        {
            var lib = ArtLibrary.I;
            _overlayRoot = new GameObject("Strategic overlay");
            _overlayRoot.transform.SetParent(transform, false);
            var ecl = _g.Sectors.FirstOrDefault(s => s.Eclipse);
            EclipseCenter = ecl != null ? GalaxyScale.ToWorld(ecl.X, ecl.Y, 0) : Vector3.zero;
            // The galactic heart (faction map v2) sits inside the Eclipse; saves without it fall back to the sector centre.
            var heart = _g.GetFeature(MapFeatureKind.GalacticHeart);
            if (heart != null && heart.Points.Count >= 2)
            {
                EclipseCenter = GalaxyScale.ToWorld(heart.Points[0], heart.Points[1], 0);
                _heartRadius = (float)(heart.Radius / GalaxyScale.LyPerUnit);
            }
            _core = new Vector2(EclipseCenter.x, EclipseCenter.z);
            BuildStars();
            BuildEclipseStars();
            BuildVolume();
            BuildTerritory();
            BuildLanes();
            BuildMarkers();
            RefreshFleets();
        }

        // ------------------------------------------------------------------ density field shared by volume and territory

        // Sector cloud value at a galactic plane point (world units): >0 inside the sector's cloud.
        float SectorField(Sector s, Vector2 p, uint seed)
        {
            var c = new Vector2((float)(s.X / GalaxyScale.LyPerUnit), (float)(s.Y / GalaxyScale.LyPerUnit));
            float r = (float)(s.Radius / GalaxyScale.LyPerUnit) * 1.08f;
            var q = new Vector3(p.x, 0, p.y) * 0.012f;
            float warp = Noise.Fbm(q + new Vector3(seed * 0.37f, 0, 0), 4, seed) * 0.42f + Noise.Fbm(q * 3.1f, 3, seed + 7) * 0.12f;
            var dp = p - c;
            dp.y *= (float)s.Aspect;                                          // oval sectors: the north-south radius is r / aspect
            float d = dp.magnitude / r + warp;
            return 1f - d;
        }

        int OwnerAt(Vector2 p, out float best, out float second)
        {
            best = -9; second = -9; int bi = -1;
            for (int i = 0; i < _g.Sectors.Count; i++)
            {
                float v = SectorField(_g.Sectors[i], p, (uint)(i * 131 + 7));
                if (v > best) { second = best; best = v; bi = i; } else if (v > second) second = v;
            }
            return bi;
        }

        // ------------------------------------------------------------------ galactic structure under the political map

        // The anatomy of the galaxy itself, independent of ownership: the galactic heart inside the Eclipse Sector, an
        // exponential disk around it, two loose spiral arms, and dust rifts on the inner edges of the arms. Sectors sit on
        // top of it, so their brightness follows real structure instead of being uniform.
        Vector2 _core;
        float _heartRadius;                                                   // world units; 0 when the save has no heart

        static float Smooth(float a, float b, float v) { float k = Mathf.Clamp01((v - a) / (b - a)); return k * k * (3 - 2 * k); }

        void GalacticStructure(Vector2 p, out float disk, out float bulge, out float arm, out float rift)
        {
            var d = p - _core;
            d.y *= 1.18f;                                                     // the disk is a little wider east-west than north-south
            float r = d.magnitude;
            bulge = Mathf.Exp(-(r * r) / (46f * 46f));
            disk = Mathf.Exp(-r / 175f) * (1f - Smooth(400f, 500f, r));
            float wobble = Noise.Fbm(new Vector3(p.x, 0, p.y) * 0.006f, 3, 401) * 1.6f;
            float phase = 2f * Mathf.Atan2(d.y, d.x) - 3.3f * Mathf.Log(Mathf.Max(r, 20f) / 50f) + wobble;
            float reach = Smooth(55f, 125f, r) * (1f - Smooth(360f, 460f, r));
            arm = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase), 2.5f) * reach;
            rift = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase + 0.95f), 9f) * reach;
        }

        // Per-sector character: overall brightness, number of bright star-forming knots, filament strength, dust.
        struct SectorLook
        {
            public float Bright, Fil, Dust; public int Knots;
            public SectorLook(float b, int k, float f, float d) { Bright = b; Knots = k; Fil = f; Dust = d; }
        }

        // Nebula colours sampled from faction map v2. They are more saturated than the faction accents, which stay the
        // interface colours (territory overlay, rings, labels).
        static Color NebulaColor(Sector s, Color fallback)
        {
            switch (s.Id)
            {
                case "silex_expanse": return new Color(0.18f, 0.62f, 1f);
                case "pygmaei_sector": return new Color(0.62f, 0.22f, 1f);
                case "pirate_reach": return new Color(1f, 0.66f, 0.16f);
                case "inhumanus_dominion": return new Color(1f, 0.16f, 0.1f);
                case "elven_front": return new Color(1f, 0.38f, 0.16f);
                case "meeples": return new Color(0.16f, 0.4f, 1f);
                case "craigon": return new Color(0.8f, 0.25f, 0.95f);
                default: return fallback;
            }
        }

        static SectorLook LookFor(Sector s)
        {
            switch (s.Id)
            {
                case "silex_expanse": return new SectorLook(0.95f, 3, 0.9f, 0.55f);      // ordered, filamentary
                case "pygmaei_sector": return new SectorLook(0.68f, 2, 0.6f, 0.6f);
                case "pirate_reach": return new SectorLook(0.48f, 2, 0.45f, 1.0f);      // ragged and dusty
                case "inhumanus_dominion": return new SectorLook(0.9f, 4, 0.7f, 0.75f); // dense, hungry emission knots
                case "elven_front": return new SectorLook(0.72f, 2, 1.0f, 0.45f);
                case "meeples": return new SectorLook(0.95f, 3, 0.6f, 0.5f);            // the core worlds, vivid on the map
                case "craigon": return new SectorLook(0.68f, 3, 0.8f, 0.85f);
                default: return new SectorLook(0.7f, 2, 0.7f, 0.6f);
            }
        }

        // Emission is stored square-root encoded over [0, VolumeEmissionMax] so faint gas keeps precision and bright
        // knots do not clip. RR_GalaxyVolume decodes it with the same constant (_EmissionMax).
        const float VolumeEmissionMax = 2.5f;
        static float Enc(float e) { return Mathf.Sqrt(Mathf.Clamp01(e / VolumeEmissionMax)); }

        void BuildVolume()
        {
            var lib = ArtLibrary.I;
            if (lib.galaxyVolume == null) return;
            // The bake is deterministic for a given sector layout, so reloading a save reuses it instead of baking again.
            string key = string.Join(";", _g.Sectors.Select(s => s.Id + "," + s.X + "," + s.Y + "," + s.Radius + "," + s.Aspect + "," + s.OwnerId + "," + s.Eclipse))
                + ";heart," + _core.x + "," + _core.y + "," + _heartRadius;
            if (s_density == null || s_densityKey != key)
            {
                if (s_density != null) Destroy(s_density);
                s_density = BakeDensity();
                s_densityKey = key;
            }
            _density = s_density;
            var size = GalaxyScale.VolumeSize;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "Nebular volume";
            box.transform.SetParent(transform, false);
            box.transform.localScale = size;
            _volMat = new Material(lib.galaxyVolume) { name = "Galaxy volume" };
            _volMat.SetTexture("_Density", _density);
            _volMat.SetFloat("_Steps", 48);
            _volMat.SetFloat("_Emission", 1.9f);
            _volMat.SetFloat("_Extinction", 2.6f);
            _volMat.SetFloat("_EmissionMax", VolumeEmissionMax);
            _volMat.SetTexture("_Detail", DetailNoise());
            _volMat.SetFloat("_DetailScale", 1f / 44f);                         // one detail tile per 44 world units
            _volMat.SetFloat("_DetailStrength", 0.85f);
            var e = new Vector3(EclipseCenter.x / size.x + 0.5f, 0.5f, EclipseCenter.z / size.z + 0.5f);
            var ecl = _g.Sectors.FirstOrDefault(s => s.Eclipse);
            _volMat.SetVector("_EclipseCenter", new Vector4(e.x, e.y, e.z, ecl != null ? (float)(ecl.Radius / GalaxyScale.LyPerUnit / size.x) : 0.1f));
            var mr = box.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _volMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // Tiling 64-cubed detail noise for the ray march (fidelity plan, phase 3): r = billowy fbm, g = ridged strands,
        // b = a slower fbm that varies the other two. It erodes the baked clouds below their voxel size.
        static Texture3D s_detail;

        static Texture3D DetailNoise()
        {
            if (s_detail != null) return s_detail;
            const int N = 64;
            var px = new Color32[N * N * N];
            Parallel.For(0, N, z =>
            {
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float u = (x + 0.5f) / N, v = (y + 0.5f) / N, w = (z + 0.5f) / N;
                        float bill = 0, slow = 0, ridge = 0, a = 0.5f, n = 0;
                        for (int o = 0; o < 4; o++)
                        {
                            int per = 4 << o;
                            bill += Noise.GradientPeriodic(u * per, v * per, w * per, per, (uint)(901 + o)) * a;
                            int sp = 2 << o;
                            slow += Noise.GradientPeriodic(u * sp, v * sp, w * sp, sp, (uint)(951 + o)) * a;
                            n += a; a *= 0.5f;
                        }
                        a = 0.5f; float rn = 0;
                        for (int o = 0; o < 3; o++)
                        {
                            int per = 6 << o;
                            float k = 1f - Mathf.Abs(Noise.GradientPeriodic(u * per, v * per, w * per, per, (uint)(977 + o)));
                            ridge += k * k * a; rn += a; a *= 0.5f;
                        }
                        px[(z * N + y) * N + x] = new Color(Mathf.Clamp01(bill / n * 0.6f + 0.5f), Mathf.Clamp01(ridge / rn), Mathf.Clamp01(slow / n * 0.6f + 0.5f), 1f);
                    }
            });
            s_detail = new Texture3D(N, N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Galaxy detail" };
            s_detail.SetPixels32(px);
            s_detail.Apply(true, true);
            return s_detail;
        }

        Texture3D BakeDensity()
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int nx = 384, ny = 24, nz = 288;
            var size = GalaxyScale.VolumeSize;
            var data = new Color32[nx * ny * nz];
            int ns = _g.Sectors.Count;
            var sectorCols = _g.Sectors.Select(s => NebulaColor(s, s.OwnerId != null ? Look.PolityColor(_g, s.OwnerId) : new Color(0.32f, 0.3f, 0.55f))).ToArray();
            var looks = _g.Sectors.Select(LookFor).ToArray();
            // The Craigon origin vortex (faction map v2 draws the homeworlds as a small spiral).
            var vortexF = _g.GetFeature(MapFeatureKind.OriginVortex);
            bool hasVortex = vortexF != null && vortexF.Points.Count >= 2;
            var vortexAt = hasVortex ? new Vector2((float)(vortexF.Points[0] / GalaxyScale.LyPerUnit), (float)(vortexF.Points[1] / GalaxyScale.LyPerUnit)) : Vector2.zero;
            float vortexR = hasVortex ? (float)(vortexF.Radius / GalaxyScale.LyPerUnit) : 1f;
            var vortexCol = new Color(0.86f, 0.5f, 1f);
            // Bright knots (star-forming regions) scattered inside each sector, deterministic per sector.
            var knots = new List<(int sector, Vector2 at, float size, float gain)>();
            for (int i = 0; i < ns; i++)
            {
                var s = _g.Sectors[i];
                if (s.Eclipse) continue;
                var c = new Vector2((float)(s.X / GalaxyScale.LyPerUnit), (float)(s.Y / GalaxyScale.LyPerUnit));
                float rad = (float)(s.Radius / GalaxyScale.LyPerUnit);
                for (int k = 0; k < looks[i].Knots; k++)
                {
                    float a = Noise.Rand01(i * 17 + 3, k) * Mathf.PI * 2, dd = Mathf.Lerp(0.12f, 0.62f, Noise.Rand01(i * 17 + 5, k)) * rad;
                    knots.Add((i, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * dd, Mathf.Lerp(8f, 20f, Noise.Rand01(i * 17 + 7, k)), Mathf.Lerp(0.55f, 1.15f, Noise.Rand01(i * 17 + 9, k))));
                }
            }
            // Craigon influence spreads as a band across the southern galaxy (approved map), modelled as a second cloud.
            var craigon = Look.PolityColor(_g, "craigon");
            var starlight = new Color(0.78f, 0.8f, 0.9f);
            var knotWhite = new Color(1f, 0.9f, 0.92f);
            Parallel.For(0, nz, z =>
            {
                var f = new float[ns];
                for (int x = 0; x < nx; x++)
                {
                    var p = new Vector2((x + 0.5f) / nx * size.x - size.x / 2, (z + 0.5f) / nz * size.z - size.z / 2);
                    float best = -9; int si = -1;
                    for (int i = 0; i < ns; i++)
                    {
                        float v = f[i] = SectorField(_g.Sectors[i], p, (uint)(i * 131 + 7));
                        if (v > best) { best = v; si = i; }
                    }
                    var sec = si >= 0 ? _g.Sectors[si] : null;
                    // Soft regional tint: the colours of neighbouring sectors blend across borders instead of meeting at a seam.
                    Color soft = Color.black; float wsum = 0;
                    for (int i = 0; i < ns; i++) { float w = Mathf.Exp(Mathf.Clamp(f[i] - best, -3f, 0f) * 9f); soft += sectorCols[i] * w; wsum += w; }
                    soft /= Mathf.Max(wsum, 1e-4f);
                    float disk, bulge, arm, rift;
                    GalacticStructure(p, out disk, out bulge, out arm, out rift);
                    var q = new Vector3(p.x, 0, p.y) * 0.02f;
                    float fil = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise.Fbm(q * 1.7f, 4, 91))), 6);
                    float fine = Noise.Ridged(new Vector3(p.x, 0, p.y) * 0.075f, 4, 19);           // thin strands inside the clouds
                    float dustN = Noise.Fbm(q * 2.3f + new Vector3(5, 0, 9), 5, 33) * 0.5f + 0.5f;
                    // Southern Craigon influence band (south-west to south-east).
                    // Faction map v2: the band runs from the Craigon south along the southern rim to below the Eclipse.
                    float band = Mathf.Exp(-Mathf.Pow((p.y + 181f + Mathf.Sin(p.x * 0.012f) * 30f) / 40f, 2)) * Mathf.Clamp01((230f - Mathf.Abs(p.x + 91f)) / 90f);
                    bool eclipse = sec != null && sec.Eclipse && best > -0.12f;
                    float rim = Mathf.Exp(-(best * best) / (0.07f * 0.07f));
                    var q2 = new Vector3(p.x, 0, p.y) * 0.03f;
                    float lanes = Smooth(0.52f, 0.72f, Noise.Fbm(q2 * 2.2f + new Vector3(3, 0, 1), 4, 77) * 0.5f + 0.5f);
                    // Large dark clouds: a few coherent rifts that cut across sector borders.
                    float darkCloud = Smooth(0.6f, 0.8f, Noise.Fbm(new Vector3(p.x, 0, p.y) * 0.009f + new Vector3(11, 0, 4), 4, 515) * 0.5f + 0.5f);
                    float riftN = Mathf.Clamp01(rift * (0.35f + 0.9f * dustN) + darkCloud * 0.85f);
                    // Sector body: thins out towards the edge, so the cloud has no plateau and no outline.
                    float body = Smooth(-0.1f, 0.55f, best);
                    float kn = 0;
                    if (!eclipse)
                        foreach (var k in knots)
                        {
                            float dd = (p - k.at).sqrMagnitude;
                            if (dd < k.size * k.size * 9f) kn += Mathf.Exp(-dd / (k.size * k.size)) * k.gain * Smooth(-0.15f, 0.2f, f[k.sector]);
                        }
                    var lk = si >= 0 ? looks[si] : new SectorLook(0.6f, 0, 0.6f, 0.6f);
                    float vort = 0;
                    if (hasVortex)
                    {
                        var dv = p - vortexAt;
                        float rv = dv.magnitude;
                        if (rv < vortexR * 3f)
                        {
                            // Two arms winding inward to a bright core.
                            float ph = 2f * Mathf.Atan2(dv.y, dv.x) + 3.2f * Mathf.Log(Mathf.Max(rv, 0.5f) / (vortexR * 0.3f)) + fine * 1.4f;
                            float arms = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(ph), 3f);
                            vort = (arms * 1.5f + 0.08f) * Mathf.Exp(-rv / (vortexR * 0.75f)) + Mathf.Exp(-(rv * rv) / (vortexR * vortexR * 0.02f)) * 0.7f;
                            vort *= 1f - Smooth(vortexR * 2f, vortexR * 3f, rv);
                        }
                    }
                    float gal = 0.75f + disk * (0.45f + 0.8f * arm) + bulge * 0.3f;             // structural light level
                    for (int y = 0; y < ny; y++)
                    {
                        float hy = ((y + 0.5f) / ny - 0.5f) * size.y;
                        float thick = 3.5f + 3f * dustN;                                       // a thinner sheet keeps detail crisp at an angle
                        float vprof = Mathf.Exp(-(hy * hy) / (thick * thick));
                        float n3 = Noise.Fbm(new Vector3(p.x * 0.045f, hy * 0.11f, p.y * 0.045f), 4, 55) * 0.5f + 0.5f;
                        float clump = Mathf.Pow(n3, 2.4f);
                        Color emis;
                        float ext;
                        if (eclipse)
                        {
                            // The Eclipse Sector: patchy, deeply absorbing dust; faint indigo glow inside, a cold rim at its edge.
                            float inner = Smooth(-0.02f, 0.4f, best);
                            // Knots and veils: high-contrast dust with thinner gaps, never a uniform black disc.
                            float eknots = Smooth(0.35f, 0.75f, n3) * (0.6f + 0.4f * lanes);
                            emis = new Color(0.12f, 0.09f, 0.26f) * (0.35f * (1f - eknots) * inner + 0.08f) + new Color(0.5f, 0.62f, 0.95f) * (rim * fil * 0.38f + rim * 0.075f);
                            ext = (0.08f + inner * (0.12f + 0.75f * eknots)) * vprof;
                            emis *= vprof;
                        }
                        else
                        {
                            var sc = sec != null ? Color.Lerp(sectorCols[si], soft, 0.35f) : soft;
                            float strands = fine * fine * (0.35f + 0.65f * clump);
                            float cloud = body * lk.Bright * gal * (0.18f + 0.85f * clump + 0.45f * strands * lk.Fil);
                            emis = sc * cloud;
                            emis += sc * fil * lk.Fil * (0.08f + 0.3f * body) * gal * 0.6f;                // filaments
                            emis += sc * rim * (0.03f + 0.18f * fil);                                    // only a broken trace of an edge
                            emis += Color.Lerp(sc, knotWhite, 0.4f) * kn * (0.35f + 1.1f * clump) * 1.15f; // bright cores
                            emis += Color.Lerp(soft, starlight, 0.6f) * disk * (0.35f + 0.8f * arm) * 0.09f; // continuous disk glow
                            emis += craigon * band * (0.1f + 0.35f * clump) * 0.45f;
                            emis *= vprof;
                            emis *= (1f - lanes * 0.55f * lk.Dust) * (1f - riftN * 0.85f);
                            ext = (lanes * 0.9f * lk.Dust + riftN * 2.6f + body * clump * 0.12f + 0.03f) * vprof;
                            emis += vortexCol * vort * Mathf.Exp(-(hy * hy) / (4f * 4f));
                        }
                        data[(z * ny + y) * nx + x] = new Color(Enc(emis.r), Enc(emis.g), Enc(emis.b), Mathf.Clamp01(ext / 4f));
                    }
                }
            });
            Debug.Log("galaxy volume baked in " + watch.ElapsedMilliseconds + " ms");
            var tex = new Texture3D(nx, ny, nz, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Galaxy density" };
            tex.SetPixels32(data);
            tex.Apply(false, true);
            return tex;
        }

        void BuildStars()
        {
            var lib = ArtLibrary.I;
            if (lib.starField == null) return;
            const int N = 70000;
            var rnd = new System.Random(7);
            var verts = new Vector3[N * 4];
            var uv = new Vector2[N * 4];
            var uv2 = new Vector2[N * 4];
            var cols = new Color32[N * 4];
            var tris = new int[N * 6];
            var size = GalaxyScale.VolumeSize;
            int n = 0, guard = 0;
            while (n < N && guard++ < N * 30)
            {
                var p = new Vector2((float)(rnd.NextDouble() - 0.5) * size.x * 1.05f, (float)(rnd.NextDouble() - 0.5) * size.z * 1.05f);
                // Star counts follow the galaxy (bulge, disk, arms), with only a mild lift inside settled sectors, so the
                // stars do not trace the political blobs. Dark clouds thin them further.
                float disk, bulge, arm, rift;
                GalacticStructure(p, out disk, out bulge, out arm, out rift);
                float darkCloud = Smooth(0.6f, 0.8f, Noise.Fbm(new Vector3(p.x, 0, p.y) * 0.009f + new Vector3(11, 0, 4), 4, 515) * 0.5f + 0.5f);
                float thin = 1f - 0.55f * Mathf.Max(rift, darkCloud);
                float galDensity = 0.07f + disk * (0.32f + 0.6f * arm) + bulge * 0.6f;
                double u = rnd.NextDouble();
                if (u > Mathf.Max((galDensity + 0.12f) * thin, 0.1f)) continue;            // cheap rejection before the sector lookup
                float best, second;
                int si = OwnerAt(p, out best, out second);
                bool eclipse = si >= 0 && _g.Sectors[si].Eclipse && best > -0.05f;
                float density = (galDensity + Mathf.Clamp01(best * 2.5f) * 0.12f) * thin;
                if (eclipse) density = 0.1f;
                if (u > density) continue;
                double g1 = rnd.NextDouble(), g2 = rnd.NextDouble();
                float gauss = (float)(System.Math.Sqrt(-2 * System.Math.Log(System.Math.Max(1e-9, g1))) * System.Math.Cos(2 * System.Math.PI * g2));
                float h = gauss * (eclipse ? 8f : Mathf.Lerp(7f, 15f, bulge)) + (float)(rnd.NextDouble() - 0.5) * 4f;
                var pos = new Vector3(p.x, h, p.y);
                float t = (float)rnd.NextDouble();
                // Old warm population in the bulge, young blue-white stars along the arms.
                float tb = Mathf.Clamp01(t - 0.2f * arm + 0.35f * bulge);
                Color c = tb < 0.12f ? new Color(0.65f, 0.75f, 1f) : tb < 0.55f ? new Color(1f, 0.96f, 0.9f) : tb < 0.85f ? new Color(1f, 0.85f, 0.62f) : new Color(1f, 0.62f, 0.45f);
                if (eclipse && rnd.NextDouble() < 0.5) c = new Color(0.62f, 0.55f, 1f);
                float b = Mathf.Pow((float)rnd.NextDouble(), 3.5f) * (1.8f + 0.8f * arm) + 0.12f;
                if (eclipse) b *= 0.55f;
                float wsize = 0.16f + Mathf.Pow((float)rnd.NextDouble(), 4) * 0.55f;
                for (int k = 0; k < 4; k++)
                {
                    verts[n * 4 + k] = pos;
                    uv2[n * 4 + k] = new Vector2(wsize, 0);
                    cols[n * 4 + k] = new Color(c.r, c.g, c.b, Mathf.Clamp01(b / 2f));
                }
                uv[n * 4 + 0] = new Vector2(-1, -1); uv[n * 4 + 1] = new Vector2(1, -1); uv[n * 4 + 2] = new Vector2(1, 1); uv[n * 4 + 3] = new Vector2(-1, 1);
                int v0 = n * 4, ti = n * 6;
                tris[ti] = v0; tris[ti + 1] = v0 + 2; tris[ti + 2] = v0 + 1; tris[ti + 3] = v0; tris[ti + 4] = v0 + 3; tris[ti + 5] = v0 + 2;
                n++;
            }
            var mesh = new Mesh { name = "Ambient stars", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            System.Array.Resize(ref verts, n * 4); System.Array.Resize(ref uv, n * 4); System.Array.Resize(ref uv2, n * 4);
            System.Array.Resize(ref cols, n * 4); System.Array.Resize(ref tris, n * 6);
            mesh.vertices = verts; mesh.uv = uv; mesh.uv2 = uv2; mesh.colors32 = cols; mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, size * 1.2f);
            var go = new GameObject("Ambient stars");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            _starMat = new Material(lib.starField) { name = "Ambient stars" };
            _starMat.SetFloat("_Brightness", 2.8f);
            _starMat.SetFloat("_RefDistance", 260f);
            _starMat.SetFloat("_MaxPixels", 4.5f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _starMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // The Eclipse Sector's unusual stellar population: dim deep-red and violet stars and a few pale cold giants,
        // drawn after the dust so the region stays legible without being lit up.
        void BuildEclipseStars()
        {
            var lib = ArtLibrary.I;
            var ecl = _g.Sectors.FirstOrDefault(x => x.Eclipse);
            if (lib.starField == null || ecl == null) return;
            var rnd = new System.Random(1313);
            var c0 = GalaxyScale.ToWorld(ecl.X, ecl.Y, 0);
            float r = (float)(ecl.Radius / GalaxyScale.LyPerUnit);
            var verts = new List<Vector3>(); var uv = new List<Vector2>(); var uv2 = new List<Vector2>(); var cols = new List<Color32>(); var tris = new List<int>();
            for (int n = 0; n < 800; n++)                                    // the v2 Eclipse is about four times the area
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = Mathf.Sqrt((float)rnd.NextDouble()) * r * 1.05f;
                var p = c0 + new Vector3(Mathf.Cos(a) * d, ((float)rnd.NextDouble() - 0.5f) * 10f, Mathf.Sin(a) * d * 0.85f);
                float best, second;
                if (OwnerAt(new Vector2(p.x, p.z), out best, out second) < 0 || !_g.Sectors[OwnerAt(new Vector2(p.x, p.z), out best, out second)].Eclipse) continue;
                float t = (float)rnd.NextDouble();
                Color c = t < 0.45f ? new Color(0.85f, 0.25f, 0.2f) : t < 0.85f ? new Color(0.62f, 0.45f, 1f) : new Color(0.7f, 0.82f, 1f);
                float b = t < 0.85f ? 0.25f + (float)rnd.NextDouble() * 0.35f : 0.8f + (float)rnd.NextDouble() * 0.6f;
                float wsize = t < 0.85f ? 0.18f : 0.5f;
                int v0 = verts.Count;
                for (int k = 0; k < 4; k++) { verts.Add(p); uv2.Add(new Vector2(wsize, 0)); cols.Add(new Color(c.r, c.g, c.b, Mathf.Clamp01(b / 2f))); }
                uv.Add(new Vector2(-1, -1)); uv.Add(new Vector2(1, -1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(-1, 1));
                tris.AddRange(new[] { v0, v0 + 2, v0 + 1, v0, v0 + 3, v0 + 2 });
            }
            var mesh = new Mesh { name = "Eclipse stars" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uv); mesh.SetUVs(1, uv2); mesh.SetColors(cols); mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(c0, Vector3.one * r * 3);
            var go = new GameObject("Eclipse stars");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var m = new Material(lib.starField) { name = "Eclipse stars", renderQueue = 2960 };
            m.SetFloat("_Brightness", 2.2f); m.SetFloat("_RefDistance", 220f); m.SetFloat("_MaxPixels", 4f);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ------------------------------------------------------------------ chart layer: territory (fidelity plan, phase 4)

        // Territory as a signed distance field: _Field holds the distance in world units to the territory edge (positive inside),
        // so the shader draws anti-aliased glowing border lines of constant pixel width at any zoom. Where two sectors meet, each
        // side draws its own line just inside, leaving a dark gap between them as on the faction map. _Territory holds the owner
        // colour (rgb) and the disputed-zone mask (a).
        static Texture2D s_terrCol, s_terrField;
        static string s_terrKey;

        string LayoutKey()
        {
            return string.Join(";", _g.Sectors.Select(s => s.Id + "," + s.X + "," + s.Y + "," + s.Radius + "," + s.Aspect + "," + s.OwnerId + "," + s.Eclipse))
                + ";" + string.Join(";", _g.Features.Select(f => f.Id + "," + f.Radius + "," + string.Join(",", f.Points)));
        }

        void BuildTerritory()
        {
            var lib = ArtLibrary.I;
            if (lib.territory == null) return;
            var size = GalaxyScale.VolumeSize;
            string key = LayoutKey();
            if (s_terrCol == null || s_terrKey != key)
            {
                if (s_terrCol != null) { Destroy(s_terrCol); Destroy(s_terrField); }
                BakeTerritory(out s_terrCol, out s_terrField);
                s_terrKey = key;
            }
            _territory = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(_territory.GetComponent<Collider>());
            _territory.name = "Territory";
            _territory.transform.SetParent(_overlayRoot.transform, false);
            _territory.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _territory.transform.localScale = new Vector3(size.x, size.z, 1);
            _terrMat = new Material(lib.territory) { name = "Territory" };
            _terrMat.SetTexture("_Territory", s_terrCol);
            _terrMat.SetTexture("_Field", s_terrField);
            _terrMat.SetTexture("_Detail", DetailNoise());
            _terrMat.SetFloat("_DetailScale", 1f / 22f);
            _territory.GetComponent<MeshRenderer>().sharedMaterial = _terrMat;
        }

        // Squared distance (in texels) from every texel to the nearest edge texel, where an edge texel has a 4-neighbour
        // with a different label. Felzenszwalb and Huttenlocher's separable transform, one pass per axis.
        static float[] EdgeDistanceSquared(int[] labels, int w, int h)
        {
            const float Inf = 1e7f;                                           // above any squared distance on the grid
            var f = new float[w * h];
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, a = labels[i];
                    bool edge = (x > 0 && labels[i - 1] != a) || (x < w - 1 && labels[i + 1] != a) || (y > 0 && labels[i - w] != a) || (y < h - 1 && labels[i + w] != a);
                    f[i] = edge ? 0 : Inf;
                }
            });
            Parallel.For(0, h, y => { var row = new float[w]; for (int x = 0; x < w; x++) row[x] = f[y * w + x]; var o = Dt1(row); for (int x = 0; x < w; x++) f[y * w + x] = o[x]; });
            Parallel.For(0, w, x => { var col = new float[h]; for (int y = 0; y < h; y++) col[y] = f[y * w + x]; var o = Dt1(col); for (int y = 0; y < h; y++) f[y * w + x] = o[y]; });
            return f;
        }

        static float[] Dt1(float[] f)
        {
            int n = f.Length;
            var d = new float[n]; var v = new int[n]; var z = new double[n + 1];
            int k = 0; v[0] = 0; z[0] = double.NegativeInfinity; z[1] = double.PositiveInfinity;
            for (int q = 1; q < n; q++)
            {
                double s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                while (s <= z[k])
                {
                    k--;
                    s = ((f[q] + (double)q * q) - (f[v[k]] + (double)v[k] * v[k])) / (2.0 * q - 2.0 * v[k]);
                }
                k++; v[k] = q; z[k] = s; z[k + 1] = double.PositiveInfinity;
            }
            k = 0;
            for (int q = 0; q < n; q++) { while (z[k + 1] < q) k++; d[q] = (float)((double)(q - v[k]) * (q - v[k]) + f[v[k]]); }
            return d;
        }

        void BakeTerritory(out Texture2D colTex, out Texture2D fieldTex)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int w = 768, h = 576;
            var size = GalaxyScale.VolumeSize;
            float tx = size.x / w, tz = size.z / h;
            var polities = _g.Polities.Select(p => p.Id).ToList();
            var ownerCols = _g.Sectors.Select(s => s.OwnerId != null ? Look.PolityColor(_g, s.OwnerId) : new Color(0.62f, 0.55f, 0.92f)).ToArray();
            var zones = _g.Features.Where(f => f.Kind == MapFeatureKind.Disputed && f.Points.Count >= 2)
                .Select(f => (at: new Vector2((float)(f.Points[0] / GalaxyScale.LyPerUnit), (float)(f.Points[1] / GalaxyScale.LyPerUnit)), r: (float)(f.Radius / GalaxyScale.LyPerUnit))).ToArray();
            var raw = new float[w * h];
            var labels = new int[w * h];
            var col = new Color32[w * h];
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2((x + 0.5f) * tx - size.x / 2, (y + 0.5f) * tz - size.z / 2);
                    float best, second;
                    int si = OwnerAt(p, out best, out second);
                    // Inside a sector the edge is either its own outline (best = 0) or the line shared with a neighbour
                    // (best = second); the smaller of the two decides which edge is nearer.
                    raw[y * w + x] = second > -1f ? Mathf.Min(best, (best - second) * 0.5f) : best;
                    labels[y * w + x] = raw[y * w + x] > 0 ? si : -1;
                    var c = si >= 0 ? ownerCols[si] : Color.black;
                    float zone = 0;
                    foreach (var z in zones)
                    {
                        float wob = Noise.Fbm(new Vector3(p.x, 0, p.y) * 0.08f, 2, 61) * z.r * 0.35f;
                        zone = Mathf.Max(zone, 1f - Smooth(z.r * 0.6f, z.r, (p - z.at).magnitude + wob));
                    }
                    col[y * w + x] = new Color(c.r, c.g, c.b, zone);
                }
            });
            // Distance in world units: an exact Euclidean distance transform from the edge texels gives a clean field
            // everywhere; right at the edge the raw field divided by its local gradient refines it to sub-texel precision
            // (clamped to within a texel of the transform, so noise in the gradient cannot create stray lines).
            var halves = new ushort[w * h];
            float texel = (tx + tz) * 0.5f;
            var dsq = EdgeDistanceSquared(labels, w, h);
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    float edt = (Mathf.Sqrt(dsq[i]) + 0.5f) * texel * (labels[i] >= 0 ? 1f : -1f);
                    float e = raw[i];
                    float l = x > 0 ? raw[i - 1] : e, r = x < w - 1 ? raw[i + 1] : e;
                    float d = y > 0 ? raw[i - w] : e, u = y < h - 1 ? raw[i + w] : e;
                    float gx = Mathf.Max(Mathf.Abs(r - e), Mathf.Abs(e - l)) / tx, gz = Mathf.Max(Mathf.Abs(u - e), Mathf.Abs(e - d)) / tz;
                    float lin = e / Mathf.Max(Mathf.Sqrt(gx * gx + gz * gz), 1e-4f);
                    halves[i] = Mathf.FloatToHalf(Mathf.Clamp(Mathf.Clamp(lin, edt - texel, edt + texel), -60f, 60f));
                }
            });
            colTex = new Texture2D(w, h, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Territory colour" };
            colTex.SetPixels32(col); colTex.Apply(false, true);
            fieldTex = new Texture2D(w, h, TextureFormat.RHalf, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Territory field" };
            fieldTex.SetPixelData(halves, 0); fieldTex.Apply(false, true);
            Debug.Log("territory field baked in " + watch.ElapsedMilliseconds + " ms");
            var dump = Args.Value("-rrDumpTerritory");
            if (dump != null)
            {
                var img = new Texture2D(w, h, TextureFormat.RGBA32, false);
                var dp = new Color32[w * h];
                for (int i = 0; i < w * h; i++)
                {
                    float d = Mathf.HalfToFloat(halves[i]);
                    dp[i] = d >= 0 ? new Color(Mathf.Clamp01(d / 40f), Mathf.Clamp01(d / 40f), Mathf.Clamp01(d / 40f)) : new Color(Mathf.Clamp01(-d / 20f), 0, 0);
                }
                img.SetPixels32(dp); img.Apply();
                System.IO.File.WriteAllBytes(dump, img.EncodeToPNG());
            }
        }
        void BuildLanes()
        {
            var lib = ArtLibrary.I;
            if (lib.lane == null) return;
            // The faction map legend: major jump routes as long dashes, minor routes as short ones.
            _laneMat = new Material(lib.lane) { name = "Lanes (minor)" };
            _laneMat.SetFloat("_Width", 1.2f);
            _laneMat.SetFloat("_DashLength", 1.7f);
            _laneMat.SetFloat("_DashFill", 0.45f);
            _lanes = LaneMesh("Lanes", _g.Lanes.Where(l => !l.Major).Select(l => (l.A, l.B, new Color(0.62f, 0.7f, 0.84f, 0.42f))).ToList(), _laneMat);
            var majorMat = new Material(lib.lane) { name = "Lanes (major)" };
            majorMat.SetFloat("_Width", 1.8f);
            majorMat.SetFloat("_DashLength", 4.2f);
            majorMat.SetFloat("_DashFill", 0.62f);
            LaneMesh("Major lanes", _g.Lanes.Where(l => l.Major).Select(l => (l.A, l.B, new Color(0.9f, 0.92f, 0.98f, 0.7f))).ToList(), majorMat);
            BuildIncursions();
            _routeMat = new Material(lib.lane) { name = "Route" };
            _routeMat.SetFloat("_Width", 3f);
            _routeMat.SetFloat("_DashLength", 3f);
            _routeMat.SetFloat("_DashFill", 0.55f);
            _routeMat.SetFloat("_Flow", 0.8f);
        }

        // Craigon incursion routes (faction map v2): red dashes flowing outward, with glowing nodes at their bends.
        void BuildIncursions()
        {
            var lib = ArtLibrary.I;
            var segs = new List<(Vector3, Vector3, Color)>();
            var nodes = new List<Vector3>();
            var red = new Color(1f, 0.26f, 0.3f, 0.95f);
            foreach (var f in _g.Features.Where(f => f.Kind == MapFeatureKind.Incursion && f.Points.Count >= 4))
                for (int i = 0; i + 1 < f.Points.Count; i += 2)
                {
                    var p = GalaxyScale.ToWorld(f.Points[i], f.Points[i + 1], 0) + Vector3.up * 0.6f;
                    if (i + 3 < f.Points.Count) segs.Add((p, GalaxyScale.ToWorld(f.Points[i + 2], f.Points[i + 3], 0) + Vector3.up * 0.6f, red));
                    if (!nodes.Any(o => (o - p).sqrMagnitude < 1f)) nodes.Add(p);
                }
            if (segs.Count == 0) return;
            var mat = new Material(lib.lane) { name = "Incursions" };
            mat.SetFloat("_Width", 1.5f);
            mat.SetFloat("_DashLength", 2.2f);
            mat.SetFloat("_DashFill", 0.55f);
            mat.SetFloat("_Flow", 0.25f);
            LaneMesh("Incursions", segs, mat, 0.012f);
            var nodeMat = new Material(lib.marker) { name = "Incursion node" };
            nodeMat.SetColor("_Color", new Color(1f, 0.22f, 0.24f));
            nodeMat.SetFloat("_WorldRadius", 1.4f);
            nodeMat.SetFloat("_MinPx", 3.5f);
            nodeMat.SetFloat("_MaxPx", 12f);
            nodeMat.SetFloat("_Intensity", 1.8f);
            nodeMat.SetFloat("_Spikes", 0f);
            foreach (var p in nodes)
            {
                var q = new GameObject("Incursion node");
                q.transform.SetParent(_overlayRoot.transform, false);
                q.transform.localPosition = p;
                q.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad();
                var mr = q.AddComponent<MeshRenderer>();
                mr.sharedMaterial = nodeMat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        GameObject LaneMesh(string name, List<(string a, string b, Color c)> segs, Material mat)
        {
            return LaneMesh(name, segs.Select(s => (GalaxyScale.ToWorld(_g.GetSystem(s.a)), GalaxyScale.ToWorld(_g.GetSystem(s.b)), s.c)).ToList(), mat);
        }

        GameObject LaneMesh(string name, List<(Vector3 a, Vector3 b, Color c)> segs, Material mat, float arc = 0.035f)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tris = new List<int>();
            foreach (var (pa, pb, c) in segs)
            {
                const int steps = 16;
                float len = Vector3.Distance(pa, pb);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    var p = Vector3.Lerp(pa, pb, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * len * arc;
                    var p2 = Vector3.Lerp(pa, pb, t + 0.01f) + Vector3.up * Mathf.Sin((t + 0.01f) * Mathf.PI) * len * arc;
                    var tan = (p2 - p).normalized;
                    // Fade near the endpoints so lanes do not stab into the markers.
                    float endFade = Mathf.Clamp01(Mathf.Min(t, 1 - t) * len / 3f);
                    var cc = new Color(c.r, c.g, c.b, c.a * endFade);
                    int k = v.Count;
                    v.Add(p); v.Add(p); n.Add(tan); n.Add(tan);
                    uv.Add(new Vector2(t * len, -1)); uv.Add(new Vector2(t * len, 1));
                    col.Add(cc); col.Add(cc);
                    if (i < steps) tris.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 });
                }
            }
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(Vector3.zero, GalaxyScale.VolumeSize * 1.2f);
            var go = new GameObject(name);
            go.transform.SetParent(_overlayRoot.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        public void ShowRoute(Fleet f, List<string> path)
        {
            if (_route != null) Destroy(_route);
            _route = null;
            if (f == null || path == null || path.Count == 0 || _routeMat == null) return;
            var segs = new List<(string, string, Color)>();
            string at = f.SystemId;
            foreach (var p in path) { segs.Add((at, p, new Color(Look.GoldBright.r, Look.GoldBright.g, Look.GoldBright.b, 1.2f))); at = p; }
            _route = LaneMesh("Route", segs, _routeMat);
        }

        // One capital world per faction, shown with a ring glyph (presentation only; the map marks capitals in its legend).
        static readonly HashSet<string> Capitals = new HashSet<string> { "lithar", "origin_brood", "meeple_core", "forgefall", "blackwake", "ledgerhold", "aethel_spire" };

        void BuildMarkers()
        {
            var lib = ArtLibrary.I;
            foreach (var s in _g.Systems)
            {
                var go = new GameObject("System " + s.Name);
                go.transform.SetParent(transform, false);
                go.transform.localPosition = GalaxyScale.ToWorld(s);
                var q = new GameObject("Glow");
                q.transform.SetParent(go.transform, false);
                q.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Quad();
                var mr = q.AddComponent<MeshRenderer>();
                var m = new Material(lib.marker) { name = "Marker " + s.Name };
                m.SetColor("_Color", Look.StarColor(s.Star));
                m.SetFloat("_WorldRadius", s.Detail == SystemDetail.Hero ? 2.6f : 2.0f);
                m.SetFloat("_MinPx", s.Detail == SystemDetail.Distant ? 4.5f : 6f);
                m.SetFloat("_MaxPx", 22f);
                m.SetFloat("_Intensity", s.Star == StarClass.Dim ? 1.2f : 2.2f);
                // Capitals and the hero system carry long diffraction spikes; every system gets a soft halo.
                bool capital = Capitals.Contains(s.Id);
                m.SetFloat("_Extent", capital ? 3.4f : s.Detail == SystemDetail.Hero ? 2.8f : 1.8f);
                m.SetFloat("_Spikes", capital ? 1.25f : s.Detail == SystemDetail.Hero ? 0.9f : 0.12f);
                m.SetFloat("_Breathe", capital ? 1f : 0f);
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                // Owner ring on the galactic plane (overlay).
                var ring = new GameObject("Owner ring");
                ring.transform.SetParent(_overlayRoot.transform, false);
                ring.transform.localPosition = go.transform.localPosition;
                ring.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Circle(3.2f, 0.35f, 64);
                var rmr = ring.AddComponent<MeshRenderer>();
                var col = s.OwnerId != null ? Look.PolityColor(_g, s.OwnerId) : new Color(0.5f, 0.5f, 0.6f);
                var ringMat = Look.Unlit(lib.fx, col * (s.OwnerId != null ? 1.2f : 0.5f), "Owner ring");
                rmr.sharedMaterial = ringMat;
                rmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (Capitals.Contains(s.Id))
                {
                    // Capital worlds: the faction map's double-ring glyph.
                    var cap = new GameObject("Capital ring");
                    cap.transform.SetParent(ring.transform, false);
                    cap.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Circle(4.6f, 0.22f, 96);
                    var cmr = cap.AddComponent<MeshRenderer>();
                    cmr.sharedMaterial = Look.Unlit(lib.fx, Color.Lerp(col, Color.white, 0.35f) * 1.3f, "Capital ring");
                    cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Markers[s.Id] = new SystemMarker { Data = s, T = go.transform, Mat = m, Ring = ring.transform, RingMat = ringMat };
            }
        }

        public void RefreshOwnership()
        {
            foreach (var m in Markers.Values)
            {
                var s = m.Data;
                var col = s.OwnerId != null ? Look.PolityColor(_g, s.OwnerId) : new Color(0.5f, 0.5f, 0.6f);
                if (s.Blockaded) col = Color.Lerp(col, Look.PolityColor(_g, s.OrbitalControlId), 0.5f);
                m.RingMat.SetColor("_Color", col * (s.OwnerId != null ? 1.2f : 0.5f));
                m.RingMat.SetFloat("_Dash", s.Blockaded ? 24 : 0);
            }
        }

        // ------------------------------------------------------------------ fleets

        public void RefreshFleets(float dayFraction = 0)
        {
            foreach (var id in FleetMarkers.Keys.ToList())
                if (_g.GetFleet(id) == null || _g.GetFleet(id).Ships.Count == 0) { Destroy(FleetMarkers[id].gameObject); FleetMarkers.Remove(id); }
            foreach (var f in _g.Fleets.Where(f => f.Ships.Count > 0))
            {
                Transform t;
                if (!FleetMarkers.TryGetValue(f.Id, out t))
                {
                    var lead = f.Ships.OrderByDescending(s => ShipCatalog.IsLeader(s.DefId)).ThenByDescending(s => ShipCatalog.Tier(s.DefId)).First();
                    var model = ArtLibrary.I.Model(lead.DefId);
                    var go = model != null ? Instantiate(model, transform) : new GameObject();
                    go.name = "Fleet " + f.Name;
                    if (model != null) SystemPresentation.ApplyShipMaterial(go, lead.DefId);
                    var ring = new GameObject("Fleet ring");
                    ring.transform.SetParent(go.transform, false);
                    ring.transform.localPosition = new Vector3(0, -0.4f, 0);
                    ring.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Circle(1.3f, 0.12f, 48);
                    var rr = ring.AddComponent<MeshRenderer>();
                    rr.sharedMaterial = Look.Unlit(ArtLibrary.I.fx, Look.PolityColor(_g, f.OwnerId) * 1.6f, "Fleet ring");
                    t = go.transform;
                    FleetMarkers[f.Id] = t;
                }
                PlaceFleet(f, t, dayFraction);
            }
        }

        public Vector3 FleetPosition(Fleet f, float dayFraction)
        {
            var at = GalaxyScale.ToWorld(_g.GetSystem(f.SystemId));
            if (f.Moving)
            {
                var next = GalaxyScale.ToWorld(_g.GetSystem(f.Order.Path[0]));
                var lane = _g.LaneBetween(f.SystemId, f.Order.Path[0]);
                float t = Mathf.Clamp01((f.Order.DaysIntoLane + dayFraction) / Mathf.Max(1, lane.Days));
                float len = Vector3.Distance(at, next);
                return Vector3.Lerp(at, next, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * len * 0.035f + 1.6f);
            }
            int idx = _g.Fleets.Where(o => o.SystemId == f.SystemId && !o.Moving).ToList().IndexOf(f);
            float ang = 2.2f + idx * 1.4f;
            return at + new Vector3(Mathf.Cos(ang) * 4.2f, 1.6f, Mathf.Sin(ang) * 4.2f);
        }

        void PlaceFleet(Fleet f, Transform t, float dayFraction)
        {
            t.localPosition = FleetPosition(f, dayFraction);
            Vector3 dir;
            if (f.Moving) dir = GalaxyScale.ToWorld(_g.GetSystem(f.Order.Path[0])) - GalaxyScale.ToWorld(_g.GetSystem(f.SystemId));
            else dir = new Vector3(0, 0, 1);
            dir.y = 0;
            if (dir.sqrMagnitude > 1e-4f) t.localRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        void LateUpdate()
        {
            // Keep fleet models a readable size on screen (they stand for whole fleets at this scale).
            var cam = GameRoot.I != null ? GameRoot.I.Rig.Cam : null;
            if (cam == null) return;
            foreach (var t in FleetMarkers.Values)
            {
                float d = Vector3.Distance(cam.transform.position, t.position);
                t.localScale = Vector3.one * Mathf.Clamp(d * 0.016f, 0.9f, 7f);
            }
            var pose = GameRoot.I.Rig.Current;
            float far = Mathf.InverseLerp(80, 600, pose.Distance);
            if (_terrMat != null) _terrMat.SetFloat("_Fade", Mathf.Lerp(0.55f, 1f, far));
            if (_volMat != null)
            {
                // Clear the mist around what the player zoomed in on: none above 220 units, most of it by 60 units. A
                // residue stays so the region keeps its colour.
                float zoom = 1f - Mathf.InverseLerp(60f, 220f, pose.Distance);
                _volMat.SetFloat("_ClearStrength", 0.82f * zoom * zoom * (3f - 2f * zoom));
                _volMat.SetVector("_Clear", new Vector4(pose.Focus.x, pose.Focus.y, pose.Focus.z, 10f + pose.Distance * 0.45f));
            }
        }

        public void SetOverlay(bool on) { if (_overlayRoot != null) _overlayRoot.SetActive(on); if (_route != null) _route.SetActive(on); }

        public void SetSelected(string systemId)
        {
            foreach (var m in Markers.Values) m.Mat.SetFloat("_Pulse", m.Data.Id == systemId ? 1 : 0);
        }
    }
}