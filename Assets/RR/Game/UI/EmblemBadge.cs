using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // A faction emblem on a machined signet plate. Emblem PNGs are drop-in art (Resources/RR/Emblems/<faction>.png) with
    // uneven padding and their own colour, so each is measured once: trimmed to its visible artwork, so every faction reads
    // at the same optical size, and sampled for the tint that colours the plate's glow and rim. Presentation only.
    public sealed class EmblemBadge : VisualElement
    {
        sealed class Art { public Texture2D Texture; public Rect Uv; public Color Tint; }
        static readonly Dictionary<string, Art> Cache = new Dictionary<string, Art>();
        static readonly Color Fallback = new Color32(139, 207, 252, 255);
        static Color Alpha(Color c, float a) { c.a = a; return c; }

        readonly Art _art;
        public Color Tint => _art != null ? _art.Tint : Fallback;
        public bool HasArt => _art != null;
        /// <summary>The trimmed artwork as a fraction of the source image (automation checks the trim).</summary>
        public Rect ArtUv => _art != null ? _art.Uv : new Rect(0, 0, 1, 1);

        public EmblemBadge(string faction)
        {
            AddToClassList("emblem-badge"); pickingMode = PickingMode.Ignore;
            _art = Load(faction);
            generateVisualContent += Draw;
            if (_art == null) { var icon = UiKit.AccentIcon(IconKind.Crown, 24); icon.AddToClassList("emblem-fallback"); icon.pickingMode = PickingMode.Ignore; Add(icon); }
            // The brighter rim is its own layer, so styles can fade it in on hover.
            var hot = new VisualElement { pickingMode = PickingMode.Ignore }; hot.AddToClassList("emblem-badge-hot");
            hot.generateVisualContent += ctx => Rim(ctx, hot.contentRect, true);
            Add(hot);
        }

        static Art Load(string faction)
        {
            if (string.IsNullOrEmpty(faction)) return null;
            if (Cache.TryGetValue(faction, out var art)) return art;
            var tex = Resources.Load<Texture2D>("RR/Emblems/" + faction);
            art = tex == null ? null : Measure(tex);
            Cache[faction] = art;
            return art;
        }

        static Art Measure(Texture2D tex)
        {
            int w = tex.width, h = tex.height;
            // Imported textures are not CPU-readable; a one-off blit reads them without changing import settings.
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            Graphics.Blit(tex, rt); RenderTexture.active = rt;
            var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
            copy.ReadPixels(new Rect(0, 0, w, h), 0, 0); copy.Apply(false);
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            var px = copy.GetPixels32(); Object.Destroy(copy);
            int x0 = w, y0 = h, x1 = -1, y1 = -1; float r = 0, g = 0, b = 0, weight = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var c = px[y * w + x]; if (c.a < 32) continue;
                    if (x < x0) x0 = x; if (x > x1) x1 = x; if (y < y0) y0 = y; if (y > y1) y1 = y;
                    Color.RGBToHSV(c, out _, out float s, out float v);
                    float k = c.a / 255f * (.15f + s) * v;   // saturated, bright pixels carry the faction colour
                    r += c.r * k; g += c.g * k; b += c.b * k; weight += k;
                }
            if (x1 < 0) return new Art { Texture = tex, Uv = new Rect(0, 0, 1, 1), Tint = Fallback };
            var tint = weight > 0 ? new Color(r / weight / 255f, g / weight / 255f, b / weight / 255f) : Fallback;
            Color.RGBToHSV(tint, out float hue, out float sat, out _);
            tint = Color.HSVToRGB(hue, Mathf.Min(sat, .72f), 1f);   // one brightness for every faction; keep the hue
            return new Art { Texture = tex, Uv = Rect.MinMaxRect(x0 / (float)w, y0 / (float)h, (x1 + 1) / (float)w, (y1 + 1) / (float)h), Tint = tint };
        }

        static void Plate(Painter2D pen, Rect r, float inset)
        {
            var q = new Rect(r.x + inset, r.y + inset, r.width - inset * 2, r.height - inset * 2);
            float c = Mathf.Min(q.width, q.height) * .2f;
            pen.BeginPath();
            pen.MoveTo(new Vector2(q.xMin + c, q.yMin)); pen.LineTo(new Vector2(q.xMax - c, q.yMin));
            pen.LineTo(new Vector2(q.xMax, q.yMin + c)); pen.LineTo(new Vector2(q.xMax, q.yMax - c));
            pen.LineTo(new Vector2(q.xMax - c, q.yMax)); pen.LineTo(new Vector2(q.xMin + c, q.yMax));
            pen.LineTo(new Vector2(q.xMin, q.yMax - c)); pen.LineTo(new Vector2(q.xMin, q.yMin + c));
            pen.ClosePath();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect; if (r.width < 8 || r.height < 8) return;
            var tint = Tint; var pen = ctx.painter2D;
            Plate(pen, r, 0); pen.fillColor = new Color32(8, 24, 40, 246); pen.Fill();
            // A tinted glow behind the emblem and a faint sheen from above.
            CinematicUi.Field(ctx, r, new Vector2(r.center.x, r.center.y + r.height * .06f), r.size * .62f, Alpha(tint, .2f));
            CinematicUi.Field(ctx, r, new Vector2(r.center.x, r.yMin + r.height * .14f), new Vector2(r.width * .4f, r.height * .36f), new Color(1, 1, 1, .05f));   // falls to zero before the chamfers
            if (_art != null)
            {
                float inset = r.width * .15f;
                var box = new Rect(r.x + inset, r.y + inset, r.width - inset * 2, r.height - inset * 2);
                float aspect = _art.Uv.width * _art.Texture.width / (_art.Uv.height * _art.Texture.height);
                var fit = aspect > box.width / box.height
                    ? new Rect(box.x, box.center.y - box.width / aspect * .5f, box.width, box.width / aspect)
                    : new Rect(box.center.x - box.height * aspect * .5f, box.y, box.height * aspect, box.height);
                var mesh = ctx.Allocate(4, 6, _art.Texture);
                var region = mesh.uvRegion;   // the texture may sit in the dynamic atlas
                void Corner(float x, float y, float u, float v) => mesh.SetNextVertex(new Vertex
                {
                    position = new Vector3(x, y, Vertex.nearZ), tint = Color.white,
                    uv = new Vector2(region.x + u * region.width, region.y + v * region.height)
                });
                // Texture v runs up while panel y runs down.
                Corner(fit.xMin, fit.yMin, _art.Uv.xMin, _art.Uv.yMax);
                Corner(fit.xMax, fit.yMin, _art.Uv.xMax, _art.Uv.yMax);
                Corner(fit.xMax, fit.yMax, _art.Uv.xMax, _art.Uv.yMin);
                Corner(fit.xMin, fit.yMax, _art.Uv.xMin, _art.Uv.yMin);
                mesh.SetNextIndex(0); mesh.SetNextIndex(1); mesh.SetNextIndex(2);
                mesh.SetNextIndex(0); mesh.SetNextIndex(2); mesh.SetNextIndex(3);
            }
            Rim(ctx, r, false);
        }

        void Rim(MeshGenerationContext ctx, Rect r, bool hot)
        {
            if (r.width < 8 || r.height < 8) return;
            var pen = ctx.painter2D; var tint = Tint;
            if (hot) CinematicUi.Field(ctx, r, r.center, r.size * .6f, Alpha(tint, .12f));
            pen.lineJoin = LineJoin.Miter; pen.lineCap = LineCap.Butt;
            Plate(pen, r, .75f); pen.strokeColor = Alpha(tint, hot ? .95f : .42f); pen.lineWidth = hot ? 1.4f : 1f; pen.Stroke();
            if (!hot) { Plate(pen, r, 3.5f); pen.strokeColor = new Color(1, 1, 1, .07f); pen.lineWidth = 1; pen.Stroke(); }
            // The two chamfers on the lit diagonal carry a brighter edge, as the glass panels' rims do.
            float c = Mathf.Min(r.width, r.height) * .2f;
            pen.strokeColor = Alpha(Color.Lerp(tint, Color.white, .35f), hot ? 1 : .85f); pen.lineWidth = 1.6f; pen.lineCap = LineCap.Round;
            pen.BeginPath(); pen.MoveTo(new Vector2(r.xMin + .75f, r.yMin + c + 1)); pen.LineTo(new Vector2(r.xMin + c + 1, r.yMin + .75f)); pen.Stroke();
            pen.BeginPath(); pen.MoveTo(new Vector2(r.xMax - .75f, r.yMax - c - 1)); pen.LineTo(new Vector2(r.xMax - c - 1, r.yMax - .75f)); pen.Stroke();
        }
    }
}
