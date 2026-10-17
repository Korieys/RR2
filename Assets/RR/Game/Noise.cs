using System;
using UnityEngine;

namespace RR.Game
{
    // CPU twin of RR_Common.hlsl noise, used for deterministic bakes (planet cubemaps, galaxy density volume).
    public static class Noise
    {
        static uint Hash(uint x) { x ^= x >> 16; x *= 0x7feb352dU; x ^= x >> 15; x *= 0x846ca68bU; x ^= x >> 16; return x; }
        static uint Hash3(int x, int y, int z, uint seed) => Hash((uint)x * 73856093U ^ Hash((uint)y * 19349663U ^ Hash((uint)z * 83492791U ^ seed)));
        static float Rand(uint h) => (h & 0x00FFFFFFU) / 16777216f;

        static void Grad(int x, int y, int z, uint seed, out float gx, out float gy, out float gz)
        {
            uint h = Hash3(x, y, z, seed);
            float u = Rand(h) * 2f - 1f;
            float a = Rand(Hash(h)) * 6.2831853f;
            float r = Mathf.Sqrt(Mathf.Max(0, 1f - u * u));
            gx = r * Mathf.Cos(a); gy = r * Mathf.Sin(a); gz = u;
        }

        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        public static float Gradient(float px, float py, float pz, uint seed = 0)
        {
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py), iz = Mathf.FloorToInt(pz);
            float fx = px - ix, fy = py - iy, fz = pz - iz;
            float wx = Fade(fx), wy = Fade(fy), wz = Fade(fz);
            float Corner(int ox, int oy, int oz)
            {
                Grad(ix + ox, iy + oy, iz + oz, seed, out var gx, out var gy, out var gz);
                return gx * (fx - ox) + gy * (fy - oy) + gz * (fz - oz);
            }
            float x00 = Mathf.Lerp(Corner(0, 0, 0), Corner(1, 0, 0), wx), x10 = Mathf.Lerp(Corner(0, 1, 0), Corner(1, 1, 0), wx);
            float x01 = Mathf.Lerp(Corner(0, 0, 1), Corner(1, 0, 1), wx), x11 = Mathf.Lerp(Corner(0, 1, 1), Corner(1, 1, 1), wx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, wy), Mathf.Lerp(x01, x11, wy), wz) * 1.6f;
        }

        /// <summary>Gradient noise that repeats every <paramref name="period"/> lattice cells on all axes (for tiling textures).</summary>
        public static float GradientPeriodic(float px, float py, float pz, int period, uint seed = 0)
        {
            int ix = Mathf.FloorToInt(px), iy = Mathf.FloorToInt(py), iz = Mathf.FloorToInt(pz);
            float fx = px - ix, fy = py - iy, fz = pz - iz;
            float wx = Fade(fx), wy = Fade(fy), wz = Fade(fz);
            float Corner(int ox, int oy, int oz)
            {
                Grad(Wrap(ix + ox, period), Wrap(iy + oy, period), Wrap(iz + oz, period), seed, out var gx, out var gy, out var gz);
                return gx * (fx - ox) + gy * (fy - oy) + gz * (fz - oz);
            }
            float x00 = Mathf.Lerp(Corner(0, 0, 0), Corner(1, 0, 0), wx), x10 = Mathf.Lerp(Corner(0, 1, 0), Corner(1, 1, 0), wx);
            float x01 = Mathf.Lerp(Corner(0, 0, 1), Corner(1, 0, 1), wx), x11 = Mathf.Lerp(Corner(0, 1, 1), Corner(1, 1, 1), wx);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, wy), Mathf.Lerp(x01, x11, wy), wz) * 1.6f;
        }

        static int Wrap(int i, int period) { int m = i % period; return m < 0 ? m + period : m; }

        public static float Fbm(Vector3 p, int octaves, uint seed = 0, float lacunarity = 2.03f, float gain = 0.5f)
        {
            float s = 0, a = 0.5f, n = 0;
            for (int o = 0; o < octaves; o++)
            {
                s += Gradient(p.x, p.y, p.z, seed + (uint)o * 1013U) * a;
                n += a; p = p * lacunarity + new Vector3(17.1f, 9.7f, 3.3f); a *= gain;
            }
            return s / n;
        }

        /// <summary>Ridged multifractal in [0, 1]: sharp crests for mountains and canyons.</summary>
        public static float Ridged(Vector3 p, int octaves, uint seed = 0)
        {
            float s = 0, a = 0.5f, n = 0, w = 1;
            for (int o = 0; o < octaves; o++)
            {
                float v = 1f - Mathf.Abs(Gradient(p.x, p.y, p.z, seed + (uint)o * 7919U));
                v *= v * w;
                w = Mathf.Clamp01(v * 2f);
                s += v * a; n += a; p = p * 2.07f + new Vector3(5.3f, 1.9f, 8.7f); a *= 0.5f;
            }
            return s / n;
        }

        /// <summary>Worley distances (nearest, second nearest) for craters and crystal cells.</summary>
        public static Vector2 Cell(Vector3 p, uint seed = 0)
        {
            int ix = Mathf.FloorToInt(p.x), iy = Mathf.FloorToInt(p.y), iz = Mathf.FloorToInt(p.z);
            float d1 = 8, d2 = 8;
            for (int z = -1; z <= 1; z++)
            for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                uint h = Hash3(ix + x, iy + y, iz + z, seed);
                float ox = Rand(h), oy = Rand(Hash(h)), oz = Rand(Hash(h + 7U));
                float dx = ix + x + ox - p.x, dy = iy + y + oy - p.y, dz = iz + z + oz - p.z;
                float d = Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
            }
            return new Vector2(d1, d2);
        }

        public static Vector3 Warp(Vector3 p, float amount, uint seed)
        {
            return p + new Vector3(Fbm(p + new Vector3(3.1f, 0, 0), 3, seed), Fbm(p + new Vector3(0, 5.7f, 0), 3, seed + 1), Fbm(p + new Vector3(0, 0, 8.3f), 3, seed + 2)) * amount;
        }

        public static float Rand01(int a, int b = 0) => Rand(Hash((uint)a * 2654435761U ^ Hash((uint)b + 0x9E3779B9U)));
    }
}