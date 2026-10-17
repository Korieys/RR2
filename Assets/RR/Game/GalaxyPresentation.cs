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
            float d = (p - c).magnitude / r + warp;
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

        void BuildVolume()
        {
            var lib = ArtLibrary.I;
            if (lib.galaxyVolume == null) return;
            int nx = 192, ny = 20, nz = 144;
            var size = GalaxyScale.VolumeSize;
            var data = new Color32[nx * ny * nz];
            var sectorCols = _g.Sectors.Select(s => s.OwnerId != null ? Look.PolityColor(_g, s.OwnerId) : new Color(0.32f, 0.3f, 0.55f)).ToArray();
            // Craigon influence spreads as a band across the southern galaxy (approved map), modelled as a second cloud.
            var craigon = Look.PolityColor(_g, "craigon");
            System.Func<float, float, float, float> Smooth = (a, b, v) => { float k = Mathf.Clamp01((v - a) / (b - a)); return k * k * (3 - 2 * k); };
            Parallel.For(0, nz, z =>
            {
                for (int x = 0; x < nx; x++)
                {
                    var p = new Vector2((x + 0.5f) / nx * size.x - size.x / 2, (z + 0.5f) / nz * size.z - size.z / 2);
                    float best, second;
                    int si = OwnerAt(p, out best, out second);
                    var sec = si >= 0 ? _g.Sectors[si] : null;
                    var q = new Vector3(p.x, 0, p.y) * 0.02f;
                    float fil = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(Noise.Fbm(q * 1.7f, 4, 91))), 6);
                    float dustN = Noise.Fbm(q * 2.3f + new Vector3(5, 0, 9), 5, 33) * 0.5f + 0.5f;
                    // Southern Craigon influence band (south-west to south-east).
                    float band = Mathf.Exp(-Mathf.Pow((p.y + 205f + Mathf.Sin(p.x * 0.012f) * 30f) / 40f, 2)) * Mathf.Clamp01((260f - Mathf.Abs(p.x + 20f)) / 120f);
                    bool eclipse = sec != null && sec.Eclipse && best > -0.12f;
                    float inside = Smooth(-0.04f, 0.32f, best);
                    float rim = Mathf.Exp(-(best * best) / (0.07f * 0.07f));
                    var q2 = new Vector3(p.x, 0, p.y) * 0.03f;
                    float lanes = Smooth(0.52f, 0.72f, Noise.Fbm(q2 * 2.2f + new Vector3(3, 0, 1), 4, 77) * 0.5f + 0.5f);
                    for (int y = 0; y < ny; y++)
                    {
                        float hy = ((y + 0.5f) / ny - 0.5f) * size.y;
                        float thick = 4.5f + 4f * dustN;
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
                            float knots = Smooth(0.35f, 0.75f, n3) * (0.6f + 0.4f * lanes);
                            emis = new Color(0.12f, 0.09f, 0.26f) * (0.35f * (1f - knots) * inner + 0.08f) + new Color(0.5f, 0.62f, 0.95f) * (rim * fil * 0.5f + rim * 0.1f);
                            ext = (0.08f + inner * (0.12f + 0.75f * knots)) * vprof;
                            emis *= vprof;
                        }
                        else
                        {
                            var sc = sec != null ? sectorCols[si] : new Color(0.4f, 0.42f, 0.5f);
                            var core = Color.Lerp(sc, Color.white, 0.18f * clump);
                            emis = core * (inside * clump * 0.75f + rim * (0.25f + fil * 0.6f) + fil * inside * 0.3f) * vprof;
                            emis += new Color(0.22f, 0.25f, 0.33f) * fil * 0.12f * vprof;                 // faint inter-sector filaments
                            emis += craigon * band * (0.15f + 0.45f * clump) * vprof * 0.55f;
                            emis *= 1f - lanes * 0.6f;
                            ext = (lanes * 0.9f + inside * clump * 0.12f + 0.03f) * vprof;
                        }
                        data[(z * ny + y) * nx + x] = new Color(Mathf.Clamp01(emis.r), Mathf.Clamp01(emis.g), Mathf.Clamp01(emis.b), Mathf.Clamp01(ext / 4f));
                    }
                }
            });
            _density = new Texture3D(nx, ny, nz, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Galaxy density" };
            _density.SetPixels32(data);
            _density.Apply(false, true);
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(box.GetComponent<Collider>());
            box.name = "Nebular volume";
            box.transform.SetParent(transform, false);
            box.transform.localScale = size;
            _volMat = new Material(lib.galaxyVolume) { name = "Galaxy volume" };
            _volMat.SetTexture("_Density", _density);
            _volMat.SetFloat("_Steps", 48);
            _volMat.SetFloat("_Emission", 1.5f);
            _volMat.SetFloat("_Extinction", 2.6f);
            var e = new Vector3(EclipseCenter.x / size.x + 0.5f, 0.5f, EclipseCenter.z / size.z + 0.5f);
            var ecl = _g.Sectors.FirstOrDefault(s => s.Eclipse);
            _volMat.SetVector("_EclipseCenter", new Vector4(e.x, e.y, e.z, ecl != null ? (float)(ecl.Radius / GalaxyScale.LyPerUnit / size.x) : 0.1f));
            var mr = box.GetComponent<MeshRenderer>();
            mr.sharedMaterial = _volMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
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
                float best, second;
                int si = OwnerAt(p, out best, out second);
                bool eclipse = si >= 0 && _g.Sectors[si].Eclipse && best > -0.05f;
                float density = 0.18f + Mathf.Clamp01(best * 2.5f) * 0.82f;
                if (eclipse) density = 0.12f;
                if (rnd.NextDouble() > density) continue;
                double g1 = rnd.NextDouble(), g2 = rnd.NextDouble();
                float gauss = (float)(System.Math.Sqrt(-2 * System.Math.Log(System.Math.Max(1e-9, g1))) * System.Math.Cos(2 * System.Math.PI * g2));
                float h = gauss * (eclipse ? 8f : 7f) + (float)(rnd.NextDouble() - 0.5) * 4f;
                var pos = new Vector3(p.x, h, p.y);
                float t = (float)rnd.NextDouble();
                Color c = t < 0.12f ? new Color(0.65f, 0.75f, 1f) : t < 0.55f ? new Color(1f, 0.96f, 0.9f) : t < 0.85f ? new Color(1f, 0.85f, 0.62f) : new Color(1f, 0.62f, 0.45f);
                if (eclipse && rnd.NextDouble() < 0.5) c = new Color(0.62f, 0.55f, 1f);
                float b = Mathf.Pow((float)rnd.NextDouble(), 3.5f) * 1.8f + 0.12f;
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
            for (int n = 0; n < 340; n++)
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

        void BuildTerritory()
        {
            var lib = ArtLibrary.I;
            if (lib.territory == null) return;
            int w = 512, h = 384;
            var size = GalaxyScale.VolumeSize;
            var owners = new int[w * h];
            var strength = new float[w * h];
            var disputed = new float[w * h];
            var polities = _g.Polities.Select(p => p.Id).ToList();
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2((x + 0.5f) / w * size.x - size.x / 2, (y + 0.5f) / h * size.z - size.z / 2);
                    float best, second;
                    int si = OwnerAt(p, out best, out second);
                    int o = -1;
                    if (si >= 0 && best > 0)
                    {
                        var sec = _g.Sectors[si];
                        o = sec.OwnerId != null ? polities.IndexOf(sec.OwnerId) : 15;    // 15 = unclaimed sector (Eclipse)
                    }
                    owners[y * w + x] = o;
                    strength[y * w + x] = Mathf.Clamp01(best * 3f);
                    disputed[y * w + x] = best > 0 && second > 0 && best - second < 0.06f ? 1 : 0;
                }
            });
            var px = new Color32[w * h];
            const int R = 4;
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int o = owners[y * w + x];
                    if (o < 0) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float nearest = R + 1;
                    for (int dy = -R; dy <= R; dy++)
                        for (int dx = -R; dx <= R; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            int other = (xx < 0 || yy < 0 || xx >= w || yy >= h) ? -1 : owners[yy * w + xx];
                            if (other != o) { float d = Mathf.Sqrt(dx * dx + dy * dy); if (d < nearest) nearest = d; }
                        }
                    float border = Mathf.Clamp01(1f - nearest / (R + 1));
                    px[y * w + x] = new Color(o / 15f, border, Mathf.Max(0.05f, strength[y * w + x]), disputed[y * w + x]);
                }
            });
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Territory" };
            tex.SetPixels32(px); tex.Apply(false, true);
            var pal = new Texture2D(16, 1, TextureFormat.RGBA32, false, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "Polity palette" };
            for (int i = 0; i < 16; i++) pal.SetPixel(i, 0, i < polities.Count ? Look.PolityColor(_g, polities[i]) : new Color(0.55f, 0.5f, 0.85f));
            pal.Apply();
            _territory = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(_territory.GetComponent<Collider>());
            _territory.name = "Territory";
            _territory.transform.SetParent(_overlayRoot.transform, false);
            _territory.transform.localRotation = Quaternion.Euler(90, 0, 0);
            _territory.transform.localScale = new Vector3(size.x, size.z, 1);
            _terrMat = new Material(lib.territory) { name = "Territory" };
            _terrMat.SetTexture("_Territory", tex);
            _terrMat.SetTexture("_Palette", pal);
            _territory.GetComponent<MeshRenderer>().sharedMaterial = _terrMat;
        }

        void BuildLanes()
        {
            var lib = ArtLibrary.I;
            if (lib.lane == null) return;
            _laneMat = new Material(lib.lane) { name = "Lanes" };
            _laneMat.SetFloat("_Width", 1.5f);
            _laneMat.SetFloat("_DashLength", 2.6f);
            _laneMat.SetFloat("_DashFill", 0.62f);
            _lanes = LaneMesh("Lanes", _g.Lanes.Select(l => (l.A, l.B, l.Major ? new Color(0.85f, 0.88f, 0.95f, 0.55f) : new Color(0.6f, 0.68f, 0.82f, 0.32f))).ToList(), _laneMat);
            _routeMat = new Material(lib.lane) { name = "Route" };
            _routeMat.SetFloat("_Width", 3f);
            _routeMat.SetFloat("_DashLength", 3f);
            _routeMat.SetFloat("_DashFill", 0.55f);
            _routeMat.SetFloat("_Flow", 0.8f);
        }

        GameObject LaneMesh(string name, List<(string a, string b, Color c)> segs, Material mat)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var col = new List<Color>(); var tris = new List<int>();
            foreach (var (a, b, c) in segs)
            {
                var pa = GalaxyScale.ToWorld(_g.GetSystem(a)); var pb = GalaxyScale.ToWorld(_g.GetSystem(b));
                const int steps = 16;
                float len = Vector3.Distance(pa, pb);
                for (int i = 0; i <= steps; i++)
                {
                    float t = i / (float)steps;
                    var p = Vector3.Lerp(pa, pb, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * len * 0.035f;
                    var p2 = Vector3.Lerp(pa, pb, t + 0.01f) + Vector3.up * Mathf.Sin((t + 0.01f) * Mathf.PI) * len * 0.035f;
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
            float far = Mathf.InverseLerp(80, 600, GameRoot.I.Rig.Current.Distance);
            if (_terrMat != null) _terrMat.SetFloat("_Fade", Mathf.Lerp(0.55f, 1f, far));
        }

        public void SetOverlay(bool on) { if (_overlayRoot != null) _overlayRoot.SetActive(on); if (_route != null) _route.SetActive(on); }

        public void SetSelected(string systemId)
        {
            foreach (var m in Markers.Values) m.Mat.SetFloat("_Pulse", m.Data.Id == systemId ? 1 : 0);
        }
    }
}