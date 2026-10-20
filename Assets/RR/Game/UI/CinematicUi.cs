using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Inset, presentation-only optics. Two shared procedural light textures; no materials, polling or persistent animation.
    // Meshes paint behind native captions; decorative portraits never intercept input.
    public static class CinematicUi
    {
        static Color Alpha(Color c, float a) { c.a=a; return c; }

        static Texture2D _lightField,_vignetteField;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetFields()
        {
            ReleaseFields();Application.quitting-=ReleaseFields;Application.quitting+=ReleaseFields;
        }
        static void ReleaseFields()
        {
            if(_lightField!=null)Object.Destroy(_lightField);
            if(_vignetteField!=null)Object.Destroy(_vignetteField);
            _lightField=null;_vignetteField=null;
        }
        static Texture2D LightTexture(bool vignette)
        {
            var existing=vignette?_vignetteField:_lightField;if(existing!=null)return existing;
            const int size=512;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false,true)
                { name=vignette?"UI / focus field":"UI / soft light",filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,hideFlags=HideFlags.DontSave };
            var pixels=new Color32[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=x*2f/(size-1)-1,dy=y*2f/(size-1)-1;
                float weight=Mathf.Clamp01(1-dx*dx-dy*dy);weight*=weight;
                // Stable subpixel dither removes the visible steps of low-opacity vertex colors.
                uint hash=unchecked((uint)x*1664525u^(uint)y*1013904223u);
                float noise=(hash%256)/255f-.5f;
                float alpha=(vignette?1-weight:weight)*255+noise*32*Mathf.Clamp01(weight*16);
                pixels[y*size+x]=new Color32(255,255,255,(byte)Mathf.Clamp(Mathf.RoundToInt(alpha),0,255));
            }
            texture.SetPixels32(pixels);texture.Apply(false,true);
            if(vignette)_vignetteField=texture;else _lightField=texture;
            return texture;
        }

        static void Field(MeshGenerationContext ctx, Rect r, Vector2 center, Vector2 radius, Color color, bool vignette=false)
        {
            radius.x=Mathf.Max(1,radius.x);radius.y=Mathf.Max(1,radius.y);
            var extent=new Rect(center-radius,radius*2);
            var clipped=Rect.MinMaxRect(Mathf.Max(r.xMin,extent.xMin),Mathf.Max(r.yMin,extent.yMin),Mathf.Min(r.xMax,extent.xMax),Mathf.Min(r.yMax,extent.yMax));
            if(clipped.width<=0 || clipped.height<=0)return;
            var mesh=ctx.Allocate(4,6,LightTexture(vignette));
            void Add(float x,float y)
            {
                float u=(x-extent.xMin)/extent.width,v=(y-extent.yMin)/extent.height;
                mesh.SetNextVertex(new Vertex { position=new Vector3(x,y,Vertex.nearZ),tint=color,
                    uv=new Vector2(u,v) });
            }
            Add(clipped.xMin,clipped.yMin);Add(clipped.xMax,clipped.yMin);Add(clipped.xMax,clipped.yMax);Add(clipped.xMin,clipped.yMax);
            mesh.SetNextIndex(0);mesh.SetNextIndex(1);mesh.SetNextIndex(2);
            mesh.SetNextIndex(0);mesh.SetNextIndex(2);mesh.SetNextIndex(3);
        }

        static void Line(Painter2D pen,Vector2 a,Vector2 b,Color color,float width=1)
        {
            pen.strokeColor=color;pen.lineWidth=width;pen.BeginPath();pen.MoveTo(a);pen.LineTo(b);pen.Stroke();
        }

        // Soft illumination lies inside the glass. Edges remain sharp enough to read against space.
        public static void Lighting(MeshGenerationContext ctx,Rect r,Color accent,bool light,bool panel,bool primary,bool selected,float hover,float pulse)
        {
            var inner=new Rect(r.x+5,r.y+5,Mathf.Max(1,r.width-10),Mathf.Max(1,r.height-10));
            if(panel || selected)
            {
                Field(ctx,inner,new Vector2(r.x+r.width*.22f,r.y+8),new Vector2(r.width*.9f,Mathf.Min(r.height,300)),Alpha(accent,light?.045f:selected?.075f:.052f));
                if(!light && panel)
                    Field(ctx,inner,new Vector2(r.xMax-8,r.yMax-8),new Vector2(r.width*.65f,Mathf.Min(210,r.height)),Alpha(accent,.022f));
            }
            else if(hover>.01f)
                Field(ctx,inner,new Vector2(r.center.x,r.yMin),new Vector2(r.width*.7f,r.height*1.5f),Alpha(accent,hover*.07f));

            if(primary)
                Field(ctx,inner,new Vector2(r.x+r.width*.3f,r.yMin),new Vector2(r.width*.75f,r.height*1.4f),new Color(1,1,1,.09f));

            // A finite light pass follows a command, hover, or panel change, never an idle loop.
            if(pulse<1 && !Settings.ReducedMotion)
            {
                float envelope=Mathf.Sin(pulse*Mathf.PI);
                float x=Mathf.Lerp(r.xMin-20,r.xMax+20,1-Mathf.Pow(1-pulse,2));
                Field(ctx,inner,new Vector2(x,r.yMin+3),new Vector2(Mathf.Min(100,r.width*.32f),Mathf.Min(90,r.height)),
                    Alpha(light?Color.white:accent,envelope*(light?.27f:.2f)));
            }
        }

        public static void Finish(Painter2D pen,Rect r,Color accent,bool light,bool panel,bool primary,bool selected,float pulse)
        {
            Color shine=light?Color.white:Color.Lerp(accent,Color.white,.38f);
            // A paired catchlight and shadow give the face a shallow, machined lip.
            if(panel || primary || selected)
            {
                Line(pen,new Vector2(r.x+7,r.y+2),new Vector2(r.xMax-7,r.y+2),Alpha(shine,primary?.38f:panel?.18f:.1f));
                Line(pen,new Vector2(r.x+7,r.yMax-2),new Vector2(r.xMax-7,r.yMax-2),new Color(0,0,0,light?.08f:.3f));
            }
            if(panel || selected)
            {
                float span=Mathf.Min(38,r.width*.16f);
                Color rim=Alpha(light?new Color(.19f,.43f,.61f):accent,selected?.9f:.48f);
                Line(pen,new Vector2(r.x+8,r.y+1),new Vector2(r.x+8+span,r.y+1),rim,1.6f);
                Line(pen,new Vector2(r.x+1,r.y+8),new Vector2(r.x+1,r.y+8+span*.6f),rim,1.6f);
                Line(pen,new Vector2(r.xMax-8-span*.55f,r.yMax-1),new Vector2(r.xMax-8,r.yMax-1),Alpha(rim,.3f));
            }
            if(pulse<1 && !Settings.ReducedMotion)
            {
                float envelope=Mathf.Sin(pulse*Mathf.PI),x=Mathf.Lerp(r.x+8,r.xMax-8,1-Mathf.Pow(1-pulse,2));
                float span=Mathf.Min(46,r.width*.23f);
                for(int i=3;i>=1;i--)
                    Line(pen,new Vector2(Mathf.Max(r.x+7,x-span),r.y+1.5f),new Vector2(Mathf.Min(r.xMax-7,x+span*.35f),r.y+1.5f),
                        Alpha(light?new Color(.16f,.48f,.7f):shine,envelope*(i==1?.9f:.1f)),i==1?1.2f:i*2);
            }
        }

        public static void Portrait(VisualElement e)
        {
            e.AddToClassList("portrait-optics");
            Color accent=new Color32(139,207,252,255);
            e.RegisterCallback<CustomStyleResolvedEvent>(ev=>
            {
                if(ev.customStyle.TryGetValue(new CustomStyleProperty<Color>("--portrait-accent"),out var color))accent=color;
                e.MarkDirtyRepaint();
            });
            e.generateVisualContent+=ctx=>
            {
                var r=e.contentRect;if(r.width<8 || r.height<8)return;
                bool light=false;for(var p=e;p!=null;p=p.parent)if(p.ClassListContains("surface-light")){light=true;break;}
                Color ink=light?new Color(.19f,.43f,.61f):accent;
                Field(ctx,r,r.center,new Vector2(r.width*.52f,r.height*.6f),Alpha(ink,light?.09f:.1f));
                var pen=ctx.painter2D;
                float rx=r.width*.43f,ry=Mathf.Min(r.height*.31f,rx*.48f);
                var center=new Vector2(r.center.x,r.center.y+ry*.35f);
                // An interrupted orbital plinth grounds the portrait without drawing over the ship.
                for(int segment=0;segment<3;segment++)
                {
                    pen.strokeColor=Alpha(ink,light?.18f:.2f);pen.lineWidth=.8f;pen.BeginPath();
                    for(int i=0;i<=20;i++)
                    {
                        float a=(segment*120+12+i*4.8f)*Mathf.Deg2Rad;
                        var pt=center+new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry);
                        if(i==0)pen.MoveTo(pt);else pen.LineTo(pt);
                    }
                    pen.Stroke();
                }
                for(int i=-1;i<=1;i+=2)
                    Line(pen,new Vector2(center.x+i*(rx-3),center.y),new Vector2(center.x+i*(rx+3),center.y),Alpha(ink,.4f));
            };
        }

        public static void Backdrop(VisualElement e)
        {
            e.AddToClassList("cinematic-backdrop");
            e.generateVisualContent+=ctx=>
            {
                var r=e.contentRect;if(r.width<8 || r.height<8)return;
                Field(ctx,r,r.center,new Vector2(r.width*.76f,r.height*.8f),new Color(.003f,.012f,.024f,.35f),true);
            };
        }
    }
}
