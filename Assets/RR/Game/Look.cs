using System.Collections.Generic;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
    // Central palette and material factory (colour roles in one place).
    public static class Look
    {
        // Interface roles (Rule and Ruin direction: charcoal, near-black blue, fine metal, warm gold, ivory).
        public static readonly Color Charcoal = new Color32(14, 17, 22, 255);
        public static readonly Color NightBlue = new Color32(9, 13, 24, 255);
        public static readonly Color Metal = new Color32(140, 150, 163, 255);
        public static readonly Color Gold = new Color32(201, 164, 92, 255);
        public static readonly Color GoldBright = new Color32(232, 198, 120, 255);
        public static readonly Color Ivory = new Color32(236, 230, 214, 255);
        public static readonly Color Muted = new Color32(150, 156, 166, 255);
        public static readonly Color Danger = new Color32(226, 86, 72, 255);
        public static readonly Color Positive = new Color32(120, 200, 150, 255);

        public static Color PolityColor(GalaxyState g, string polityId)
        {
            var p = polityId != null ? g.GetPolity(polityId) : null;
            if (p == null) return new Color(0.55f, 0.55f, 0.62f);
            return new Color(((p.Color >> 16) & 0xFF) / 255f, ((p.Color >> 8) & 0xFF) / 255f, (p.Color & 0xFF) / 255f);
        }

        /// <summary>Scatters the faint distant galaxies drawn by RR_SpaceSky (deterministic, set once as shader globals).</summary>
        public static void SeedDistantGalaxies()
        {
            var dirs = new Vector4[40]; var shape = new Vector4[40]; var tint = new Vector4[40];
            for (int i = 0; i < 40; i++)
            {
                float z = Noise.Rand01(i, 1) * 2 - 1, a = Noise.Rand01(i, 2) * Mathf.PI * 2, r = Mathf.Sqrt(1 - z * z);
                dirs[i] = new Vector4(r * Mathf.Cos(a), z, r * Mathf.Sin(a), Mathf.Lerp(0.008f, 0.03f, Mathf.Pow(Noise.Rand01(i, 3), 2)));
                shape[i] = new Vector4(Noise.Rand01(i, 4) * Mathf.PI * 2, Mathf.Lerp(0.25f, 1f, Noise.Rand01(i, 5)), Noise.Rand01(i, 6), Mathf.Lerp(0.07f, 0.22f, Noise.Rand01(i, 7)));
                var c = Color.Lerp(new Color(0.62f, 0.72f, 1f), new Color(0.9f, 0.78f, 1f), Noise.Rand01(i, 8));
                tint[i] = new Vector4(c.r, c.g, c.b, 1);
            }
            Shader.SetGlobalVectorArray("_RR_Galaxies", dirs);
            Shader.SetGlobalVectorArray("_RR_GalaxyShape", shape);
            Shader.SetGlobalVectorArray("_RR_GalaxyTint", tint);
        }

        public static Color StarColor(StarClass c)
        {
            switch (c)
            {
                case StarClass.BlueGiant: return new Color(0.72f, 0.82f, 1.0f);
                case StarClass.White: return new Color(0.95f, 0.96f, 1.0f);
                case StarClass.Yellow: return new Color(1.0f, 0.93f, 0.78f);
                case StarClass.Orange: return new Color(1.0f, 0.74f, 0.48f);
                case StarClass.RedDwarf: return new Color(1.0f, 0.5f, 0.32f);
                case StarClass.Violet: return new Color(0.82f, 0.62f, 1.0f);
                default: return new Color(0.55f, 0.52f, 0.7f);
            }
        }

        public static float StarRadius(StarClass c) => c == StarClass.BlueGiant ? 13f : c == StarClass.White ? 9f : c == StarClass.Yellow ? 8.5f
            : c == StarClass.Orange ? 7.5f : c == StarClass.RedDwarf ? 5.5f : c == StarClass.Violet ? 7f : 6f;

        public static float StarLight(StarClass c) => c == StarClass.BlueGiant ? 2.6f : c == StarClass.White ? 2.3f : c == StarClass.Yellow ? 2.2f
            : c == StarClass.Orange ? 2.0f : c == StarClass.RedDwarf ? 1.7f : c == StarClass.Violet ? 1.8f : 1.2f;

        static readonly Dictionary<string, Material> _shipMats = new Dictionary<string, Material>();

        public static Material ShipMaterial(string defId)
        {
            string key = defId == "silex_throne_ark" ? "silex_royal" : defId.StartsWith("silex") || defId.StartsWith("station_silex") ? "silex"
                       : defId.StartsWith("craigon") || defId.StartsWith("token_craigon") ? "craigon" : defId.StartsWith("asteroid") ? "rock" : "wreck";
            if (_shipMats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(ArtLibrary.I.ship) { name = "RR_Ship_" + key };
            switch (key)
            {
                case "silex":
                case "silex_royal":
                    m.SetColor("_HullColor", new Color(0.70f, 0.69f, 0.67f)); m.SetColor("_HullColor2", new Color(0.48f, 0.48f, 0.5f));
                    m.SetColor("_TrimColor", key == "silex_royal" ? new Color(0.32f, 0.25f, 0.13f) : new Color(0.11f, 0.15f, 0.20f)); m.SetFloat("_Metal", 0.72f);
                    m.SetColor("_GlassColor", key == "silex_royal" ? new Color(1.0f, 0.74f, 0.38f) : new Color(0.32f, 0.64f, 1.0f));
                    m.SetFloat("_GlowIntensity", 2.2f); m.SetFloat("_Stone", 0.52f); m.SetFloat("_HullRough", 0.46f); m.SetFloat("_DetailScale", 8f);
                    break;
                case "craigon":
                    m.SetColor("_HullColor", new Color(0.3f, 0.16f, 0.36f)); m.SetColor("_HullColor2", new Color(0.46f, 0.22f, 0.5f));
                    m.SetColor("_TrimColor", new Color(0.5f, 0.45f, 0.4f)); m.SetFloat("_Metal", 0.0f); m.SetFloat("_TrimRough", 0.55f);
                    m.SetColor("_GlassColor", new Color(0.72f, 0.32f, 1.0f)); m.SetFloat("_GlowIntensity", 1.6f);
                    m.SetFloat("_Chitin", 1f); m.SetColor("_SheenColor", new Color(0.85f, 0.45f, 1.0f)); m.SetFloat("_HullRough", 0.3f); m.SetFloat("_DetailScale", 10f);
                    break;
                case "rock":
                    m.SetColor("_HullColor", new Color(0.3f, 0.28f, 0.26f)); m.SetColor("_HullColor2", new Color(0.18f, 0.17f, 0.16f));
                    m.SetColor("_TrimColor", new Color(0.32f, 0.3f, 0.28f)); m.SetFloat("_TrimRough", 0.95f); m.SetFloat("_Metal", 0);
                    m.SetFloat("_Stone", 0.6f); m.SetFloat("_GlowIntensity", 0); m.SetFloat("_DetailScale", 2.5f);
                    break;
                default:
                    m.SetColor("_HullColor", new Color(0.32f, 0.32f, 0.33f)); m.SetColor("_HullColor2", new Color(0.18f, 0.17f, 0.17f));
                    m.SetColor("_TrimColor", new Color(0.12f, 0.11f, 0.11f)); m.SetColor("_GlassColor", new Color(1.0f, 0.45f, 0.15f));
                    m.SetFloat("_GlowIntensity", 1.2f); m.SetFloat("_Stone", 0.4f); m.SetFloat("_Damage", 0.6f);
                    break;
            }
            _shipMats[key] = m;
            return m;
        }

        public static Material Unlit(Shader s, Color c, string name)
        {
            var m = new Material(s) { name = name };
            m.SetColor("_Color", c);
            return m;
        }
    }
}