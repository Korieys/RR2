using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    public sealed class PlanetTextures
    {
        public Cubemap Albedo, Normal, Detail, Clouds;
        public Texture2D RingBands;
        public int Size;
    }

    // Bakes planet surfaces into cubemaps (no UV seams or polar pinching) from deterministic 3D noise.
    // Generation runs on worker threads; the upload happens on the main thread.
    public static class PlanetBaker
    {
        public sealed class Job
        {
            public Planet Planet;
            public int Size, CloudSize;
            public Color32[][] Albedo, Normal, Detail, Clouds;
            public Color32[] Ring;
            public Task Work;
            public float Seconds;
        }

        static readonly Dictionary<string, PlanetTextures> _cache = new Dictionary<string, PlanetTextures>();

        public static bool TryGet(string planetId, out PlanetTextures t) => _cache.TryGetValue(planetId, out t);

        public static Job Start(Planet p, int size)
        {
            var job = new Job { Planet = p, Size = size, CloudSize = size >= 1024 ? size : Mathf.Max(128, size / 2) };
            job.Work = Task.Run(() =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                Generate(job);
                job.Seconds = (float)sw.Elapsed.TotalSeconds;
            });
            return job;
        }

        public static PlanetTextures Finish(Job job)
        {
            if (job.Work.Exception != null) throw job.Work.Exception;
            var t = new PlanetTextures { Size = job.Size };
            t.Albedo = Upload(job.Albedo, job.Size, "albedo", true);
            t.Normal = Upload(job.Normal, job.Size, "normal", false);
            t.Detail = Upload(job.Detail, job.CloudSize, "detail", false);
            t.Clouds = Upload(job.Clouds, job.CloudSize, "clouds", false);
            if (job.Ring != null)
            {
                t.RingBands = new Texture2D(job.Ring.Length, 1, TextureFormat.RGBA32, false, true) { wrapMode = TextureWrapMode.Clamp, name = job.Planet.Id + "_ring" };
                t.RingBands.SetPixels32(job.Ring);
                t.RingBands.Apply(false, true);
            }
            _cache[job.Planet.Id] = t;
            job.Albedo = job.Normal = job.Detail = job.Clouds = null;   // release the bake buffers
            return t;
        }

        static Cubemap Upload(Color32[][] faces, int size, string name, bool srgb)
        {
            var fmt = srgb ? UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB : UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            var c = new Cubemap(size, fmt, UnityEngine.Experimental.Rendering.TextureCreationFlags.MipChain) { name = name, filterMode = FilterMode.Trilinear, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };
            for (int f = 0; f < 6; f++) c.SetPixelData(faces[f], 0, (CubemapFace)f);
            c.Apply(true, true);
            return c;
        }

        // Unity/D3D cube face basis; row 0 is the top of each face.
        public static Vector3 FaceDir(int face, float u, float v)
        {
            switch (face)
            {
                case 0: return new Vector3(1, -v, -u).normalized;
                case 1: return new Vector3(-1, -v, u).normalized;
                case 2: return new Vector3(u, 1, v).normalized;
                case 3: return new Vector3(u, -1, -v).normalized;
                case 4: return new Vector3(u, -v, 1).normalized;
                default: return new Vector3(-u, -v, -1).normalized;
            }
        }

        // ----------------------------------------------------------------------------------------- generation

        struct Surface { public float Height; public Color Albedo; public float Rough; public float City; public float Emis; public float Water; }

        static void Generate(Job job)
        {
            var p = job.Planet;
            uint seed = (uint)p.Seed;
            Func<Vector3, Surface> fn = Pick(p.Kind, seed);
            float bump = p.Kind == PlanetKind.GasGiant ? 0 : p.Kind == PlanetKind.Barren ? 0.9f : p.Kind == PlanetKind.Ice ? 0.5f : 0.7f;
            int n = job.Size, nc = job.CloudSize;
            job.Albedo = new Color32[6][]; job.Normal = new Color32[6][]; job.Detail = new Color32[6][]; job.Clouds = new Color32[6][];
            for (int f = 0; f < 6; f++) { job.Albedo[f] = new Color32[n * n]; job.Normal[f] = new Color32[n * n]; job.Detail[f] = new Color32[nc * nc]; job.Clouds[f] = new Color32[nc * nc]; }
            float eps = 1.5f / n;
            Parallel.For(0, 6 * n, row =>
            {
                int f = row / n, y = row % n;
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1;
                    var d = FaceDir(f, u, v);
                    var s = fn(d);
                    Vector3 nrm = d;
                    if (bump > 0)
                    {
                        var t1 = Vector3.Cross(d, Mathf.Abs(d.y) < 0.99f ? Vector3.up : Vector3.right).normalized;
                        var t2 = Vector3.Cross(d, t1);
                        float h1 = fn((d + t1 * eps).normalized).Height, h2 = fn((d + t2 * eps).normalized).Height;
                        var g = (t1 * (h1 - s.Height) + t2 * (h2 - s.Height)) / eps;
                        nrm = (d - g * bump * 0.014f).normalized;
                    }
                    int i = y * n + x;
                    job.Albedo[f][i] = new Color(s.Albedo.r, s.Albedo.g, s.Albedo.b, s.Rough);
                    job.Normal[f][i] = new Color(nrm.x * 0.5f + 0.5f, nrm.y * 0.5f + 0.5f, nrm.z * 0.5f + 0.5f, s.Height * 0.5f + 0.5f);
                }
            });
            var cloudFn = Clouds(p.Kind, seed);
            Parallel.For(0, 6 * nc, row =>
            {
                int f = row / nc, y = row % nc;
                for (int x = 0; x < nc; x++)
                {
                    float u = (x + 0.5f) / nc * 2 - 1, v = (y + 0.5f) / nc * 2 - 1;
                    var d = FaceDir(f, u, v);
                    var s = fn(d);
                    int i = y * nc + x;
                    job.Detail[f][i] = new Color(s.City, s.Emis, s.Water, 1);
                    float c = cloudFn(d);
                    job.Clouds[f][i] = new Color(c, c, c, 1);
                }
            });
            if (p.Rings) job.Ring = RingBands(seed, 512);
        }

        static Color Lerp3(Color a, Color b, Color c, float t) => t < 0.5f ? Color.Lerp(a, b, t * 2) : Color.Lerp(b, c, t * 2 - 1);
        static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }

        static Func<Vector3, Surface> Pick(PlanetKind kind, uint seed)
        {
            switch (kind)
            {
                case PlanetKind.Crystal: return d => Habitable(d, seed, crystal: true, oceanBias: 0.0f);
                case PlanetKind.Terran: return d => Habitable(d, seed, crystal: false, oceanBias: 0.0f);
                case PlanetKind.Ocean: return d => Habitable(d, seed, crystal: false, oceanBias: 0.28f);
                case PlanetKind.Barren: return d => Barren(d, seed);
                case PlanetKind.Volcanic: return d => Volcanic(d, seed);
                case PlanetKind.Ice: return d => Ice(d, seed);
                case PlanetKind.Toxic: return d => Toxic(d, seed);
                default: return d => GasGiant(d, seed);
            }
        }

        static Surface Habitable(Vector3 d, uint seed, bool crystal, float oceanBias)
        {
            var w = Noise.Warp(d * 1.5f, 0.45f, seed);
            float cont = Noise.Fbm(w, 7, seed + 11) + 0.12f * Noise.Fbm(d * 0.7f, 2, seed + 5) - oceanBias;
            float ridge = Noise.Ridged(Noise.Warp(d * 3.2f, 0.25f, seed + 3), 6, seed + 21);
            float land = cont - 0.02f;
            float mount = ridge * S(0.04f, 0.25f, land);
            float h = land > 0 ? land * 0.6f + mount * 0.7f : land;
            float lat = Mathf.Abs(d.y);
            float moist = Noise.Fbm(d * 2.4f + new Vector3(4, 4, 4), 4, seed + 31) * 0.5f + 0.5f;
            var sf = new Surface { Height = Mathf.Clamp(h, -1, 1) };
            // Ocean and land are both evaluated and blended across a narrow band, so coastlines stay smooth when magnified.
            float depth = Mathf.Clamp01(-land * 5f);
            Color ocean = crystal ? Color.Lerp(new Color(0.07f, 0.25f, 0.34f), new Color(0.012f, 0.05f, 0.13f), depth)
                                  : Color.Lerp(new Color(0.06f, 0.2f, 0.28f), new Color(0.012f, 0.045f, 0.11f), depth);
            float oceanIce = S(0.78f, 0.86f, lat + Noise.Fbm(d * 6, 3, seed + 41) * 0.05f);
            ocean = Color.Lerp(ocean, new Color(0.8f, 0.85f, 0.9f), oceanIce);
            float wLand = S(-0.022f, 0.018f, land);
            if (wLand <= 0f)
            {
                sf.Albedo = ocean; sf.Rough = Mathf.Lerp(0.12f, 0.6f, oceanIce); sf.Water = 1 - oceanIce;
                return sf;
            }
            land = Mathf.Max(land, 0.0005f);
            float coast = 1 - S(0.0f, 0.03f, land);
            Color low = crystal ? Color.Lerp(new Color(0.40f, 0.44f, 0.47f), new Color(0.23f, 0.32f, 0.35f), moist)
                                : Color.Lerp(new Color(0.42f, 0.36f, 0.24f), new Color(0.13f, 0.24f, 0.1f), moist);
            Color high = crystal ? new Color(0.73f, 0.79f, 0.82f) : new Color(0.38f, 0.33f, 0.28f);
            Color c = Color.Lerp(low, high, S(0.1f, 0.6f, mount + land * 0.4f));
            c = Color.Lerp(c, new Color(0.62f, 0.58f, 0.5f), coast * 0.7f);
            float crystalField = 0;
            if (crystal)
            {
                var cell = Noise.Cell(d * 22f, seed + 51);
                float facets = S(0.55f, 0.75f, cell.y - cell.x + 0.4f);
                crystalField = S(0.25f, 0.5f, mount + Noise.Fbm(d * 5, 3, seed + 61) * 0.3f) * facets;
                c = Color.Lerp(c, new Color(0.62f, 0.78f, 0.92f), crystalField * 0.75f);
            }
            float snow = S(0.72f, 0.82f, lat + mount * 0.25f + Noise.Fbm(d * 7, 3, seed + 71) * 0.06f);
            c = Color.Lerp(c, new Color(0.9f, 0.92f, 0.95f), snow);
            sf.Albedo = Color.Lerp(ocean, c, wLand);
            sf.Rough = Mathf.Lerp(Mathf.Lerp(0.12f, 0.6f, oceanIce), Mathf.Lerp(0.85f, 0.3f, crystalField), wLand);
            sf.Water = (1 - wLand) * (1 - oceanIce);
            // Settlements: lowlands near coasts, clustered by cells.
            // Settlements: clustered conurbations of fine sparkle joined by thin transit lines, densest near coasts.
            var cc = Noise.Cell(d * 30f, seed + 81);
            float region = S(0.4f, 0.85f, Noise.Fbm(d * 5f, 3, seed + 91) * 0.5f + 0.5f);
            float clusters = S(0.5f, 0.05f, cc.x) * region;
            float sparkle = S(0.58f, 0.78f, Noise.Fbm(d * 260f, 2, seed + 95) * 0.5f + 0.5f);
            float roads = S(0.94f, 1.0f, 1f - Mathf.Abs(Noise.Gradient(d.x * 70f, d.y * 70f, d.z * 70f, seed + 97))) * region;
            float habitable = (1 - S(0.15f, 0.35f, mount)) * (1 - snow) * (0.4f + 0.6f * S(0.12f, 0.0f, land));
            sf.City = Mathf.Clamp01((clusters * (0.25f + sparkle) + roads * 0.35f) * habitable * 1.4f) * wLand;
            return sf;
        }

        static Surface Barren(Vector3 d, uint seed)
        {
            float basins = Noise.Fbm(d * 1.2f, 4, seed + 1);
            float h = basins * 0.3f;
            float crater = 0;
            for (int k = 0; k < 3; k++)
            {
                float sc = 4f * Mathf.Pow(2.3f, k);
                var cell = Noise.Cell(d * sc, seed + 100 + (uint)k);
                float r = cell.x * (1.3f + 0.3f * k);
                float bowl = -S(0.55f, 0.0f, r) * 0.6f;
                float rim = Mathf.Exp(-Mathf.Pow((r - 0.55f) / 0.08f, 2)) * 0.35f;
                crater += (bowl + rim) / (k + 1.5f);
            }
            h += crater + Noise.Fbm(d * 18, 3, seed + 7) * 0.05f;
            float t = S(-0.2f, 0.25f, basins);
            var c = Color.Lerp(new Color(0.16f, 0.15f, 0.14f), new Color(0.42f, 0.39f, 0.35f), t);
            c *= 0.85f + 0.3f * (Noise.Fbm(d * 30, 2, seed + 9) * 0.5f + 0.5f);
            c = Color.Lerp(c, c * 1.35f, Mathf.Clamp01(crater * 2));
            return new Surface { Height = h, Albedo = c, Rough = 0.92f };
        }

        static Surface Volcanic(Vector3 d, uint seed)
        {
            float h = Noise.Fbm(Noise.Warp(d * 2f, 0.4f, seed), 6, seed + 3) * 0.6f;
            float rivers = 1 - Mathf.Abs(Noise.Fbm(Noise.Warp(d * 3.5f, 0.6f, seed + 9), 5, seed + 5));
            float lava = S(0.9f, 0.97f, rivers) * S(0.3f, -0.1f, h);
            float glow = S(0.82f, 0.98f, rivers) * 0.5f + lava;
            var basalt = Color.Lerp(new Color(0.05f, 0.045f, 0.045f), new Color(0.2f, 0.16f, 0.13f), S(-0.3f, 0.5f, h));
            var c = Color.Lerp(basalt, new Color(0.9f, 0.32f, 0.06f), lava * 0.8f);
            return new Surface { Height = h - lava * 0.1f, Albedo = c, Rough = Mathf.Lerp(0.8f, 0.4f, lava), Emis = Mathf.Clamp01(glow) };
        }

        static Surface Ice(Vector3 d, uint seed)
        {
            float h = Noise.Fbm(d * 2.2f, 5, seed) * 0.4f;
            float crev = Noise.Ridged(Noise.Warp(d * 6f, 0.3f, seed + 2), 4, seed + 3);
            float rock = S(0.25f, 0.4f, h + Noise.Fbm(d * 9, 3, seed + 4) * 0.1f);
            var c = Color.Lerp(new Color(0.78f, 0.85f, 0.92f), new Color(0.5f, 0.65f, 0.8f), S(0.6f, 0.95f, crev));
            c = Color.Lerp(c, new Color(0.3f, 0.29f, 0.3f), rock * 0.8f);
            return new Surface { Height = h - crev * 0.15f, Albedo = c, Rough = 0.45f + rock * 0.4f };
        }

        static Surface Toxic(Vector3 d, uint seed)
        {
            var w = Noise.Warp(d * 1.8f, 0.5f, seed);
            float land = Noise.Fbm(w, 6, seed + 1);
            float veins = 1 - Mathf.Abs(Noise.Fbm(Noise.Warp(d * 5f, 0.5f, seed + 8), 4, seed + 9));
            var sf = new Surface { Height = land * 0.5f };
            if (land < -0.05f)
            {
                sf.Albedo = Color.Lerp(new Color(0.1f, 0.16f, 0.06f), new Color(0.03f, 0.06f, 0.03f), Mathf.Clamp01(-land * 4));
                sf.Water = 0.7f; sf.Rough = 0.2f;
            }
            else
            {
                sf.Albedo = Color.Lerp(new Color(0.25f, 0.1f, 0.22f), new Color(0.38f, 0.3f, 0.2f), S(0.0f, 0.4f, land));
                sf.Rough = 0.75f;
                sf.Emis = S(0.9f, 0.98f, veins);
                sf.City = S(0.93f, 0.99f, veins) * S(-0.02f, 0.1f, land);
            }
            return sf;
        }

        static Surface GasGiant(Vector3 d, uint seed)
        {
            // Organised zonal bands with turbulent edges and a few long-lived storms.
            var p = d;
            float stormSum = 0;
            for (int k = 0; k < 4; k++)
            {
                float lat = Mathf.Lerp(-0.55f, 0.55f, Noise.Rand01((int)seed, k)), lon = Noise.Rand01((int)seed + 9, k) * Mathf.PI * 2;
                var c = new Vector3(Mathf.Cos(lon) * Mathf.Sqrt(1 - lat * lat), lat, Mathf.Sin(lon) * Mathf.Sqrt(1 - lat * lat));
                float rad = k == 0 ? 0.16f : 0.07f;
                float dist = (d - c).magnitude;
                float sw = Mathf.Exp(-(dist * dist) / (rad * rad));
                if (sw > 0.001f)
                {
                    float ang = sw * (k == 0 ? 4.5f : 3f) * (k % 2 == 0 ? 1 : -1);
                    var axis = c;
                    p = Quaternion.AngleAxis(ang * Mathf.Rad2Deg, axis) * p;
                    stormSum += sw * (k == 0 ? 1f : 0.6f);
                }
            }
            float turb = Noise.Fbm(new Vector3(p.x * 3f, p.y * 14f, p.z * 3f), 5, seed + 3);
            float lat2 = p.y + turb * 0.035f + Noise.Fbm(p * 7f, 3, seed + 7) * 0.012f;
            float band = Mathf.Sin(lat2 * 19f + Mathf.Sin(lat2 * 7f) * 1.4f) * 0.5f + 0.5f;
            float band2 = Mathf.Sin(lat2 * 47f) * 0.5f + 0.5f;
            Color a = new Color(0.86f, 0.77f, 0.6f), b = new Color(0.62f, 0.42f, 0.27f), cc = new Color(0.55f, 0.58f, 0.6f);
            var col = Lerp3(b, a, cc, Mathf.Clamp01(band * 0.85f + band2 * 0.15f + turb * 0.2f));
            col = Color.Lerp(col, new Color(0.78f, 0.45f, 0.32f), Mathf.Clamp01(stormSum) * 0.75f);
            float polar = S(0.75f, 0.95f, Mathf.Abs(d.y));
            col = Color.Lerp(col, new Color(0.45f, 0.5f, 0.55f), polar * 0.6f);
            return new Surface { Height = 0, Albedo = col, Rough = 0.7f };
        }

        static Func<Vector3, float> Clouds(PlanetKind kind, uint seed)
        {
            switch (kind)
            {
                case PlanetKind.Crystal:
                case PlanetKind.Terran:
                case PlanetKind.Ocean:
                    return d =>
                    {
                        // Latitude-organised cloud belts with a few cyclones (sampling rotated around storm centres).
                        for (int k = 0; k < 3; k++)
                        {
                            float lat = (Noise.Rand01((int)seed + 50, k) * 0.65f + 0.15f) * (k % 2 == 0 ? 1 : -1);
                            float lon = Noise.Rand01((int)seed + 60, k) * Mathf.PI * 2;
                            var sc = new Vector3(Mathf.Cos(lon) * Mathf.Sqrt(1 - lat * lat), lat, Mathf.Sin(lon) * Mathf.Sqrt(1 - lat * lat));
                            float dist = (d - sc).magnitude;
                            float sw = Mathf.Exp(-dist * dist / 0.012f);
                            if (sw > 0.002f) d = Quaternion.AngleAxis(sw * 220f * Mathf.Sign(lat), sc) * d;
                        }
                        var w = Noise.Warp(d * 2.6f, 0.55f, seed + 200);
                        float c = Noise.Fbm(new Vector3(w.x, w.y * 1.6f, w.z), 6, seed + 201) * 0.5f + 0.5f;
                        float belts = 0.5f + 0.5f * Mathf.Cos(d.y * 9.5f);
                        float streak = Noise.Ridged(new Vector3(w.x * 3f, w.y * 9f, w.z * 3f), 4, seed + 205);
                        float v = S(0.5f, 0.78f, c * (0.75f + 0.35f * belts) + streak * 0.12f);
                        v *= 0.75f + 0.25f * (Noise.Fbm(d * 40f, 2, seed + 207) * 0.5f + 0.5f);
                        return Mathf.Clamp01(v);
                    };
                case PlanetKind.Toxic:
                    return d => S(0.45f, 0.8f, Noise.Fbm(Noise.Warp(d * 2f, 0.6f, seed + 300), 5, seed + 301) * 0.5f + 0.5f) * 0.8f;
                case PlanetKind.Volcanic:
                    return d => S(0.62f, 0.9f, Noise.Fbm(d * 3f, 4, seed + 400) * 0.5f + 0.5f) * 0.5f;
                default:
                    return d => 0f;
            }
        }

        static Color32[] RingBands(uint seed, int n)
        {
            var px = new Color32[n];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1);
                float dens = 0.55f + 0.25f * Mathf.Sin(u * 61f + Noise.Rand01((int)seed, 1) * 6) + 0.2f * Mathf.Sin(u * 173f);
                dens += Noise.Gradient(u * 40f, 0.5f, 0.5f, seed) * 0.35f + Noise.Gradient(u * 160f, 1.5f, 0.5f, seed + 1) * 0.15f;
                if (u > 0.58f && u < 0.64f) dens *= 0.08f;               // a broad gap
                if (u > 0.82f && u < 0.84f) dens *= 0.3f;                // a narrow gap
                dens *= S(0.0f, 0.06f, u) * S(1.0f, 0.9f, u);
                var c = Color.Lerp(new Color(0.75f, 0.68f, 0.55f), new Color(0.5f, 0.42f, 0.34f), Mathf.Clamp01(Mathf.Sin(u * 23f) * 0.5f + 0.5f));
                px[i] = new Color(c.r, c.g, c.b, Mathf.Clamp01(dens) * 0.9f);
            }
            return px;
        }
    }
}