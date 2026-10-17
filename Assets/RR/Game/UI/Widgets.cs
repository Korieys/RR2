using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    public enum IconKind { Energy, Materials, Credits, Influence, Fleet, Crown, Planet, Star, Pause, Play, Fast, Faster, Layers, Save, Load, Gear, Warning, Shield, Target, Move, Wait }

    // Vector icons drawn with Painter2D (crisp at every UI scale, no texture dependencies).
    public sealed class Icon : VisualElement
    {
        public IconKind Kind;
        public Color Tint;

        public Icon(IconKind kind, Color tint, float size = 16)
        {
            Kind = kind; Tint = tint;
            style.width = size; style.height = size;
            style.flexShrink = 0;
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        public void Set(IconKind kind, Color tint) { Kind = kind; Tint = tint; MarkDirtyRepaint(); }

        void Draw(MeshGenerationContext mgc)
        {
            var r = contentRect;
            float w = r.width, h = r.height, cx = w / 2, cy = h / 2, s = Mathf.Min(w, h);
            var p = mgc.painter2D;
            p.fillColor = Tint; p.strokeColor = Tint; p.lineWidth = Mathf.Max(1.2f, s * 0.09f);
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            Vector2 P(float x, float y) => new Vector2(cx + x * s * 0.5f, cy + y * s * 0.5f);
            switch (Kind)
            {
                case IconKind.Energy:
                    p.BeginPath(); p.MoveTo(P(0.15f, -0.95f)); p.LineTo(P(-0.55f, 0.12f)); p.LineTo(P(-0.02f, 0.12f)); p.LineTo(P(-0.18f, 0.95f));
                    p.LineTo(P(0.55f, -0.2f)); p.LineTo(P(0.02f, -0.2f)); p.ClosePath(); p.Fill(); break;
                case IconKind.Materials:
                    p.BeginPath();
                    for (int i = 0; i < 6; i++) { float a = Mathf.PI / 3 * i + Mathf.PI / 6; var v = P(Mathf.Cos(a) * 0.9f, Mathf.Sin(a) * 0.9f); if (i == 0) p.MoveTo(v); else p.LineTo(v); }
                    p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(0, 0)); p.LineTo(P(0, -0.85f)); p.MoveTo(P(0, 0)); p.LineTo(P(0.74f, 0.42f)); p.MoveTo(P(0, 0)); p.LineTo(P(-0.74f, 0.42f)); p.Stroke(); break;
                case IconKind.Credits:
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.43f, 0, 360); p.Stroke();
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.2f, 0, 360); p.Fill(); break;
                case IconKind.Influence:
                    p.BeginPath();
                    for (int i = 0; i < 10; i++) { float a = -Mathf.PI / 2 + Mathf.PI / 5 * i; float rr = i % 2 == 0 ? 0.95f : 0.42f; var v = P(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr); if (i == 0) p.MoveTo(v); else p.LineTo(v); }
                    p.ClosePath(); p.Fill(); break;
                case IconKind.Fleet:
                    p.BeginPath(); p.MoveTo(P(0, -0.9f)); p.LineTo(P(0.7f, 0.75f)); p.LineTo(P(0, 0.35f)); p.LineTo(P(-0.7f, 0.75f)); p.ClosePath(); p.Fill(); break;
                case IconKind.Crown:
                    p.BeginPath(); p.MoveTo(P(-0.85f, 0.6f)); p.LineTo(P(-0.85f, -0.4f)); p.LineTo(P(-0.4f, 0.05f)); p.LineTo(P(0, -0.7f)); p.LineTo(P(0.4f, 0.05f));
                    p.LineTo(P(0.85f, -0.4f)); p.LineTo(P(0.85f, 0.6f)); p.ClosePath(); p.Fill(); break;
                case IconKind.Planet:
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.32f, 0, 360); p.Fill();
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.47f, 200, 340); p.Stroke(); break;
                case IconKind.Star:
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.22f, 0, 360); p.Fill();
                    for (int i = 0; i < 4; i++) { float a = Mathf.PI / 2 * i; p.BeginPath(); p.MoveTo(P(Mathf.Cos(a) * 0.35f, Mathf.Sin(a) * 0.35f)); p.LineTo(P(Mathf.Cos(a) * 0.95f, Mathf.Sin(a) * 0.95f)); p.Stroke(); }
                    break;
                case IconKind.Pause:
                    p.BeginPath(); p.MoveTo(P(-0.4f, -0.7f)); p.LineTo(P(-0.4f, 0.7f)); p.MoveTo(P(0.4f, -0.7f)); p.LineTo(P(0.4f, 0.7f)); p.lineWidth = s * 0.2f; p.Stroke(); break;
                case IconKind.Play:
                    p.BeginPath(); p.MoveTo(P(-0.45f, -0.75f)); p.LineTo(P(0.7f, 0)); p.LineTo(P(-0.45f, 0.75f)); p.ClosePath(); p.Fill(); break;
                case IconKind.Fast:
                case IconKind.Faster:
                    int n = Kind == IconKind.Fast ? 2 : 3;
                    for (int i = 0; i < n; i++) { float o = (i - (n - 1) / 2f) * 0.55f; p.BeginPath(); p.MoveTo(P(o - 0.3f, -0.6f)); p.LineTo(P(o + 0.3f, 0)); p.LineTo(P(o - 0.3f, 0.6f)); p.ClosePath(); p.Fill(); }
                    break;
                case IconKind.Layers:
                    for (int i = 0; i < 2; i++) { float o = i * 0.38f - 0.2f; p.BeginPath(); p.MoveTo(P(0, o - 0.35f)); p.LineTo(P(0.85f, o)); p.LineTo(P(0, o + 0.35f)); p.LineTo(P(-0.85f, o)); p.ClosePath(); p.Stroke(); }
                    break;
                case IconKind.Save:
                    p.BeginPath(); p.MoveTo(P(0, -0.8f)); p.LineTo(P(0, 0.35f)); p.MoveTo(P(-0.45f, -0.05f)); p.LineTo(P(0, 0.4f)); p.LineTo(P(0.45f, -0.05f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(-0.8f, 0.45f)); p.LineTo(P(-0.8f, 0.8f)); p.LineTo(P(0.8f, 0.8f)); p.LineTo(P(0.8f, 0.45f)); p.Stroke(); break;
                case IconKind.Load:
                    p.BeginPath(); p.MoveTo(P(0, 0.4f)); p.LineTo(P(0, -0.75f)); p.MoveTo(P(-0.45f, -0.3f)); p.LineTo(P(0, -0.75f)); p.LineTo(P(0.45f, -0.3f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(-0.8f, 0.45f)); p.LineTo(P(-0.8f, 0.8f)); p.LineTo(P(0.8f, 0.8f)); p.LineTo(P(0.8f, 0.45f)); p.Stroke(); break;
                case IconKind.Gear:
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.25f, 0, 360); p.Stroke();
                    for (int i = 0; i < 8; i++) { float a = Mathf.PI / 4 * i; p.BeginPath(); p.MoveTo(P(Mathf.Cos(a) * 0.55f, Mathf.Sin(a) * 0.55f)); p.LineTo(P(Mathf.Cos(a) * 0.9f, Mathf.Sin(a) * 0.9f)); p.Stroke(); }
                    break;
                case IconKind.Warning:
                    p.BeginPath(); p.MoveTo(P(0, -0.85f)); p.LineTo(P(0.9f, 0.75f)); p.LineTo(P(-0.9f, 0.75f)); p.ClosePath(); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(0, -0.3f)); p.LineTo(P(0, 0.2f)); p.Stroke();
                    p.BeginPath(); p.Arc(P(0, 0.48f), s * 0.05f, 0, 360); p.Fill(); break;
                case IconKind.Shield:
                    p.BeginPath(); p.MoveTo(P(0, -0.9f)); p.LineTo(P(0.75f, -0.55f)); p.LineTo(P(0.6f, 0.35f)); p.LineTo(P(0, 0.9f)); p.LineTo(P(-0.6f, 0.35f)); p.LineTo(P(-0.75f, -0.55f)); p.ClosePath(); p.Stroke(); break;
                case IconKind.Target:
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.38f, 0, 360); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(0, -0.95f)); p.LineTo(P(0, -0.45f)); p.MoveTo(P(0, 0.45f)); p.LineTo(P(0, 0.95f)); p.MoveTo(P(-0.95f, 0)); p.LineTo(P(-0.45f, 0)); p.MoveTo(P(0.45f, 0)); p.LineTo(P(0.95f, 0)); p.Stroke(); break;
                case IconKind.Move:
                    p.BeginPath(); p.MoveTo(P(-0.8f, 0)); p.LineTo(P(0.6f, 0)); p.MoveTo(P(0.15f, -0.5f)); p.LineTo(P(0.7f, 0)); p.LineTo(P(0.15f, 0.5f)); p.Stroke(); break;
                case IconKind.Wait:
                    p.BeginPath(); p.Arc(P(0, 0), s * 0.42f, 0, 360); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(0, -0.5f)); p.LineTo(P(0, 0)); p.LineTo(P(0.4f, 0.25f)); p.Stroke(); break;
            }
        }
    }

    public static class UIX
    {
        public static VisualElement Div(params string[] classes) { var v = new VisualElement(); foreach (var c in classes) v.AddToClassList(c); return v; }
        public static Label Text(string text, params string[] classes) { var l = new Label(text); foreach (var c in classes) l.AddToClassList(c); return l; }
        public static Button Btn(string text, Action click, params string[] classes)
        {
            var b = new Button(click) { text = text };
            b.AddToClassList("btn");
            foreach (var c in classes) b.AddToClassList(c);
            return b;
        }
        public static Button IconBtn(IconKind icon, Action click, string tooltip)
        {
            var b = new Button(click) { tooltip = tooltip };
            b.AddToClassList("btn"); b.AddToClassList("btn-icon");
            var ic = new Icon(icon, new Color32(236, 230, 214, 255), 15);
            ic.style.alignSelf = Align.Center;
            b.Add(ic);
            return b;
        }
        public static VisualElement Bar(float fill, string fillClass = null)
        {
            var bar = Div("bar");
            var f = Div("bar-fill");
            if (fillClass != null) f.AddToClassList(fillClass);
            f.style.width = Length.Percent(Mathf.Clamp01(fill) * 100);
            bar.Add(f);
            return bar;
        }
        public static VisualElement Tag(string text, string extra = null)
        {
            var l = Text(text, "tag");
            if (extra != null) l.AddToClassList(extra);
            return l;
        }
        public static string Thousands(long n)
        {
            if (n >= 1_000_000_000) return (n / 1e9).ToString("0.0") + " billion";
            if (n >= 1_000_000) return (n / 1e6).ToString("0") + " million";
            return n.ToString("N0");
        }
    }
}