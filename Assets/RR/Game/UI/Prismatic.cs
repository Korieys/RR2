using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // TextElement paints its own text before generateVisualContent. A child caption
    // paints after the custom face, while base.text retains native sizing/accessibility.
    public sealed class PrismaticButton : Button
    {
        Label _caption;
        public PrismaticButton(Action click) : base(click) { }
        public override string text
        {
            get => base.text;
            set { base.text = value; if (_caption != null) _caption.text = value; }
        }
        public void UseCaption()
        {
            if (_caption != null) return;
            _caption = new Label(base.text) { pickingMode = PickingMode.Ignore };
            _caption.AddToClassList("prism-caption");
            style.color = Color.clear; // Keep native semantic text, without drawing a second caption beneath translucent states.
            if (ArtLibrary.I.bodySemiBold != null) _caption.style.unityFontDefinition = FontDefinition.FromFont(ArtLibrary.I.bodySemiBold);
            Add(_caption);
        }
    }

    // Presentation only. All timing is unscaled and no animation owns a game command.
    // Each element owns its tracks; detach cancels them, replacement reuses the same channel.
    public static class UiMotion
    {
        sealed class Tracks
        {
            public readonly Dictionary<string, Track> Items = new Dictionary<string, Track>();
            public Tracks(VisualElement e) { e.RegisterCallback<DetachFromPanelEvent>(_ => { foreach (var t in Items.Values) t.Stop(true); }); }
        }
        sealed class Track
        {
            public IVisualElementScheduledItem Timer;
            public Action<float> Paint;
            public bool Running;
            public void Stop(bool finish)
            {
                Timer?.Pause();
                if (!Running) return;
                Running = false; ActiveTracks--;
                if (finish) Paint?.Invoke(1);
            }
        }
        static readonly ConditionalWeakTable<VisualElement, Tracks> Elements = new ConditionalWeakTable<VisualElement, Tracks>();
        public static int ActiveTracks { get; private set; }

        public static void Run(VisualElement e, string channel, float seconds, Action<float> paint)
        {
            var tracks = Elements.GetValue(e, x => new Tracks(x));
            if (!tracks.Items.TryGetValue(channel, out var t)) { t = new Track(); tracks.Items[channel] = t; }
            t.Stop(false); t.Paint = paint;
            if (Settings.ReducedMotion || e.panel == null) { paint(1); return; }
            float start = Time.unscaledTime;
            t.Running = true; ActiveTracks++; paint(0);
            t.Timer = e.schedule.Execute(() =>
            {
                float k = Settings.ReducedMotion ? 1 : Mathf.Clamp01((Time.unscaledTime - start) / seconds);
                paint(k);
                if (k >= 1) t.Stop(false);
            }).Every(16);
        }

        public static void Enter(VisualElement e, float distance = 8, float seconds = .26f)
        {
            Run(e, "reveal", seconds, k =>
            {
                float q = 1 - Mathf.Pow(1 - k, 3);
                e.style.opacity = .45f + .55f * q;
                e.style.translate = new Translate(0, distance * (1 - q));
            });
        }

        public static void Cascade(VisualElement parent)
        {
            int index=0;
            foreach(var child in parent.Children())
            {
                if(!child.ClassListContains("roster-item") && !child.ClassListContains("project-card"))continue;
                float delay=index++*.035f;
                Run(child,"reveal",.22f+delay,k=>
                {
                    float elapsed=k*(.22f+delay);
                    float q=MotionCurve.Out((elapsed-delay)/.22f);
                    child.style.opacity=.6f+.4f*q;child.style.translate=new Translate(5*(1-q),0);
                });
                if(index>=4)break;
            }
        }

        public static void Leave(VisualElement e, Action done)
        {
            Run(e, "reveal", .16f, k =>
            {
                e.style.opacity = 1 - k; e.style.translate = new Translate(0, -5 * k);
                if (k >= 1) done();
            });
        }

        // Numbers remain authoritative on the first frame. Only a short emphasis animates.
        public static void Number(Label label, int value)
        {
            string next = value.ToString(); if (label.text == next) return;
            bool first = label.text == "0"; label.text = next;
            if (!first) Run(label, "value", .32f, k => label.style.opacity = .62f + .38f * k);
        }
    }

    public static class Prismatic
    {
        enum Kind { Paper, Rail, Card, Control, Tab }
        static readonly ConditionalWeakTable<VisualElement, Surface> Surfaces = new ConditionalWeakTable<VisualElement, Surface>();
        public static void Decorate(VisualElement e)
        {
            bool Has(string c) => e.ClassListContains(c);
            if (Has("inspect-portrait") || Surfaces.TryGetValue(e, out _)) return;
            // Labels paint native text before generateVisualContent; keep notification
            // text on its native CSS surface so critical feedback cannot be overdrawn.
            if (e is Label && Has("toast"))
            {
                e.RegisterCallback<AttachToPanelEvent>(_ => UiMotion.Enter(e, 7));
                return;
            }
            Kind? kind = Has("inspector") || Has("command-card") || Has("modal") || Has("battle-left") || Has("battle-right") ? Kind.Paper
                : Has("topbar") || Has("battle-top") || Has("command-dock") ? Kind.Rail
                : Has("fleet-card") || Has("ticker") || Has("toast") || Has("dossier-summary") ? Kind.Card
                : Has("kit-tab") ? Kind.Tab
                : e is Button && !Has("empire-identity") && !Has("item") ? Kind.Control : (Kind?)null;
            if (!kind.HasValue) return;
            var surface = new Surface(e, kind.Value); Surfaces.Add(e, surface);
            if (e is PrismaticButton button && kind != Kind.Tab) button.UseCaption();
            if (Has("modal") || Has("toast")) e.RegisterCallback<AttachToPanelEvent>(_ => UiMotion.Enter(e, Has("modal") ? 12 : 7));
        }

        public static void Pulse(VisualElement e)
        {
            if (e != null && Surfaces.TryGetValue(e, out var surface)) surface.Pulse();
        }

        sealed class Surface
        {
            readonly VisualElement _e;
            readonly Kind _kind;
            Color _navy = new Color32(5, 17, 30, 247), _raised = new Color32(12, 33, 53, 247);
            Color _paper = new Color32(239, 247, 254, 255), _white = new Color32(251, 253, 255, 255);
            Color _accent = new Color32(122, 207, 255, 255), _line = new Color32(99, 145, 176, 255);
            Color _control, _hoverColor, _ink;
            float _hover, _pulse = 1;
            bool _focus;
            readonly Vector2[] _points = new Vector2[32];
            static Color Token(ICustomStyle style, string key, Color fallback) => style.TryGetValue(new CustomStyleProperty<Color>(key), out var c) ? c : fallback;

            public Surface(VisualElement e, Kind kind)
            {
                _e = e; _kind = kind; _control = _raised; _hoverColor = _raised; _ink = _navy;
                e.AddToClassList("prism-surface");
                e.generateVisualContent += Draw;
                e.RegisterCallback<CustomStyleResolvedEvent>(ev =>
                {
                    var s = ev.customStyle;
                    _navy = Token(s, "--surface-navy", _navy); _raised = Token(s, "--surface-raised", _raised);
                    _paper = Token(s, "--surface-paper", _paper); _white = Token(s, "--surface-white", _white);
                    _accent = Token(s, "--surface-accent", _accent); _line = Token(s, "--surface-line", _line);
                    _control = Token(s, "--surface-control", _raised); _hoverColor = Token(s, "--surface-hover", _raised); _ink = Token(s, "--surface-ink", _navy);
                    e.MarkDirtyRepaint();
                });
                if (e is Button)
                {
                    e.RegisterCallback<PointerEnterEvent>(_ => Hover(true));
                    e.RegisterCallback<PointerLeaveEvent>(_ => Hover(false));
                    e.RegisterCallback<PointerDownEvent>(_ => { if (e.enabledInHierarchy) Pulse(); });
                    e.RegisterCallback<NavigationSubmitEvent>(_ => { if (e.enabledInHierarchy) Pulse(); }, TrickleDown.TrickleDown);
                    e.RegisterCallback<FocusInEvent>(_ => { _focus = true; Pulse(); e.MarkDirtyRepaint(); });
                    e.RegisterCallback<FocusOutEvent>(_ => { _focus = false; e.MarkDirtyRepaint(); });
                }
                e.RegisterCallback<AttachToPanelEvent>(_ => { if(Has("modal")) Pulse(); });
                e.RegisterCallback<DetachFromPanelEvent>(_ => { _hover = 0; _pulse = 1; _focus = false; });
            }

            void Hover(bool on)
            {
                // Hover uses a quiet surface fade; catchlights follow explicit commands.
                float from = _hover, to = on && _e.enabledInHierarchy ? 1 : 0;
                UiMotion.Run(_e, "hover", .14f, k => { _hover = Mathf.Lerp(from, to, 1 - (1-k)*(1-k)); _e.MarkDirtyRepaint(); });
            }
            public void Pulse() { if(Has("fleet-card")){_pulse=1;_e.MarkDirtyRepaint();return;} UiMotion.Run(_e, "edge", .56f, k => { _pulse = k; _e.MarkDirtyRepaint(); }); }
            bool Has(string c) => _e.ClassListContains(c);

            // The shared surface keeps the existing input/motion API, with small rounded glass edges.
            void Shape(Rect r, float radius)
            {
                float c = Mathf.Min(radius, Mathf.Min(r.width,r.height)*.25f);
                for(int corner=0;corner<4;corner++)
                {
                    var center = new Vector2(corner<2 ? r.xMax-c : r.xMin+c, corner==0 || corner==3 ? r.yMin+c : r.yMax-c);
                    for(int j=0;j<8;j++)
                    {
                        float angle=(-90+corner*90+j*90f/7)*Mathf.Deg2Rad;
                        _points[corner*8+j]=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*c;
                    }
                }
            }
            void Mesh(MeshGenerationContext ctx, Rect r, float radius, Color top, Color bottom)
            {
                Shape(r,radius); var mesh=ctx.Allocate(33,96);
                void VertexAt(Vector2 v)
                {
                    float y=Mathf.InverseLerp(r.yMin,r.yMax,v.y);
                    mesh.SetNextVertex(new Vertex { position=new Vector3(v.x,v.y,Vertex.nearZ), tint=Color.Lerp(top,bottom,y), uv=Vector2.zero });
                }
                VertexAt(r.center); for(int i=0;i<32;i++) VertexAt(_points[i]);
                for(ushort i=0;i<32;i++) { mesh.SetNextIndex(0); mesh.SetNextIndex((ushort)(i+1)); mesh.SetNextIndex((ushort)((i+1)%32+1)); }
            }
            void Outline(Painter2D pen,Rect r,float radius,Color color,float width)
            {
                Shape(r,radius);pen.strokeColor=color;pen.lineWidth=width;pen.lineJoin=LineJoin.Round;
                pen.BeginPath();pen.MoveTo(_points[0]);for(int i=1;i<32;i++)pen.LineTo(_points[i]);pen.ClosePath();pen.Stroke();
            }
            void Draw(MeshGenerationContext ctx)
            {
                float w=_e.layout.width,h=_e.layout.height;
                if(w<4 || h<4 || float.IsNaN(w))return;
                bool panel=_kind==Kind.Paper,rail=_kind==Kind.Rail,tab=_kind==Kind.Tab;
                bool primary=Has("btn-primary"),selected=Has("fleet-card-selected") || Has("btn-toggle-on") || Has("kit-tab-selected");
                bool light=false;
                for(var ancestor=_e;ancestor!=null;ancestor=ancestor.parent)
                    if(ancestor.ClassListContains("surface-light")){light=true;break;}
                var r=new Rect(1,1,w-2,h-2);
                float radius=rail ? (Has("command-dock") ? 5 : 0) : panel ? 6 : _kind==Kind.Card ? 5 : 3;
                Color top,bottom;
                if(panel && light){top=_white;bottom=_paper;}
                else if(panel || rail || _kind==Kind.Card){top=Color.Lerp(_raised,_accent,_hover*.06f+(selected?.035f:0));bottom=_navy;}
                else if(primary){top=Color.Lerp(_accent,Color.white,.08f+_hover*.18f);bottom=Color.Lerp(_accent,new Color(.2f,.62f,.94f),.15f);}
                else{top=Color.Lerp(_control,_hoverColor,_hover);bottom=Color.Lerp(top,light?_paper:_navy,.28f);}
                var pen=ctx.painter2D;
                if(tab)
                {
                    if(selected || _hover>.01f || _focus)
                    {
                        pen.strokeColor=new Color(_accent.r,_accent.g,_accent.b,selected || _focus ? 1 : _hover*.5f);pen.lineWidth=selected?3:1;
                        pen.BeginPath();pen.MoveTo(new Vector2(1,h-2));pen.LineTo(new Vector2(w-1,h-2));pen.Stroke();
                        if(selected){pen.fillColor=_accent;pen.BeginPath();pen.MoveTo(new Vector2(w*.5f-5,h-2));pen.LineTo(new Vector2(w*.5f,h+3));pen.LineTo(new Vector2(w*.5f+5,h-2));pen.ClosePath();pen.Fill();}
                    }
                    return;
                }
                Mesh(ctx,r,radius,top,bottom);
                CinematicUi.Lighting(ctx,r,_accent,light,panel || rail,primary,selected,_hover,_pulse);
                Color edge=primary?_accent:Color.Lerp(_line,_accent,selected || _focus?1:_hover*.65f);
                if(panel && !light)edge=new Color(_accent.r,_accent.g,_accent.b,.62f);
                if(Has("btn-danger"))edge=light?new Color(.68f,.18f,.22f):new Color(.96f,.57f,.55f);
                Outline(pen,r,radius,edge,selected || _focus?1.8f:.8f);
                if(Has("ticker"))
                {
                    pen.strokeColor=_accent;pen.lineWidth=3;pen.BeginPath();pen.MoveTo(new Vector2(2,4));pen.LineTo(new Vector2(2,h-4));pen.Stroke();
                }
                if(primary || _e.parent!=null && _e.parent.ClassListContains("dossier-footer"))
                {
                    bool back=!primary;float x=back?25:w-21;
                    pen.strokeColor=back?_accent:UiKit.Ink;pen.lineWidth=1.5f;pen.BeginPath();
                    pen.MoveTo(new Vector2(x,h*.5f-5));pen.LineTo(new Vector2(x+(back?-5:5),h*.5f));pen.LineTo(new Vector2(x,h*.5f+5));pen.Stroke();
                }
                CinematicUi.Finish(pen,r,_accent,light,panel || rail,primary,selected,_pulse);
            }
        }
    }
}
