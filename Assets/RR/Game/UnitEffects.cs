using UnityEngine;
using Commandfall.Core;

namespace RR.Game
{
    public enum UnitEffect { Discharge, Impact, Shield, Mend, Synapse, Capture, Rally, Shatter, Step }
    // Finite effects use a capped count, shared geometry and one owned material per burst.
    public sealed class UnitEffects:MonoBehaviour
    {
        public static int Active {get;private set;}
        public static int Peak {get;private set;}
        Material _mat;
        static Mesh _ringA,_ringB,_facet,_particle;
        public static Mesh ParticleMesh
        {
            get
            {
                if(_particle==null){_particle=Object.Instantiate(MeshFactory.Sphere(4));_particle.name="Unit effect particle";var uv=new Vector2[_particle.vertexCount];for(int i=0;i<uv.Length;i++)uv[i]=new Vector2(.5f,.5f);_particle.uv=uv;}
                return _particle;
            }
        }
        readonly Transform[] _parts=new Transform[16];
        Vector3 _toward;
        UnitEffect _kind;float _age,_life,_size;bool _counted;
        public static UnitEffect For(EffectCode code)
        {
            switch(code)
            {
                case EffectCode.HEAL:case EffectCode.REPAIR:return UnitEffect.Mend;
                case EffectCode.GUARD:case EffectCode.SHIELD:case EffectCode.INTERCEPT:case EffectCode.BARRIER:return UnitEffect.Shield;
                case EffectCode.CAPTURE:case EffectCode.CONVERT:case EffectCode.BOARD:case EffectCode.PIN:case EffectCode.PULL:return UnitEffect.Capture;
                case EffectCode.DISABLE:case EffectCode.DEBUFF:case EffectCode.MARK:case EffectCode.REVEAL:return UnitEffect.Synapse;
                default:return UnitEffect.Rally;
            }
        }
        public static UnitEffects Spawn(Transform parent,Vector3 position,Color energy,float size,UnitEffect kind,Vector3 toward=default)
        {
            if(Active>=48)return null;
            if(_ringA==null)_ringA=MeshFactory.Circle(1,.018f,48);
            if(_ringB==null)_ringB=MeshFactory.Circle(1,.030f,48);
            if(_facet==null)_facet=MeshFactory.Circle(1,.012f,6);
            var go=new GameObject("Unit effect / "+kind);go.transform.SetParent(parent,true);go.transform.position=position;
            var f=go.AddComponent<UnitEffects>();f._kind=kind;f._size=size;f._toward=toward;
            f._life=kind==UnitEffect.Shatter?1.25f:kind==UnitEffect.Discharge?.32f:kind==UnitEffect.Step?.32f:.9f;
            f._mat=Look.Unlit(ArtLibrary.I.fx,energy*(Settings.ReducedMotion?.65f:1.5f),"Unit effect / "+kind);f._mat.SetFloat("_Soft",.7f);
            int count=Settings.ReducedMotion?2:kind==UnitEffect.Shatter?14:kind==UnitEffect.Shield?7:kind==UnitEffect.Discharge?5:9;
            for(int i=0;i<count;i++)
            {
                var p=new GameObject("Layer "+i);p.transform.SetParent(go.transform,false);f._parts[i]=p.transform;
                var mesh=i<2?(i==0?_ringA:_ringB):kind==UnitEffect.Shield?_facet:ParticleMesh;
                p.AddComponent<MeshFilter>().sharedMesh=mesh;p.AddComponent<MeshRenderer>().sharedMaterial=f._mat;
                if(i>=2 && kind!=UnitEffect.Shield)p.transform.localScale=Vector3.one*.025f;
            }
            f._counted=true;Active++;Peak=Mathf.Max(Peak,Active);f.Paint(0);return f;
        }
        void Update(){_age+=Time.unscaledDeltaTime;Paint(Mathf.Clamp01(_age/_life));if(_age>=_life)Destroy(gameObject);}
        void Paint(float t)
        {
            bool reduced=Settings.ReducedMotion;
            float expand=reduced?.65f:MotionCurve.Out(t);
            _mat.SetFloat("_Fade",Mathf.Min(1,t*15+.2f)*(1-t)*(1-t));
            for(int i=0;i<_parts.Length;i++)if(_parts[i]!=null)
            {
                var p=_parts[i];float angle=(i*2.39996f)+t*.8f;
                if(i<2)
                {
                    float radius=_kind==UnitEffect.Discharge?Mathf.Lerp(.25f,.04f,expand):Mathf.Lerp(.18f,1,expand);
                    p.localScale=Vector3.one*_size*radius*(i==0?1:.7f);
                    p.localPosition=Vector3.up*(i==0?0:.08f);
                    if(_kind==UnitEffect.Discharge && _toward.sqrMagnitude>0)p.rotation=Quaternion.LookRotation(_toward.normalized)*Quaternion.Euler(90,0,0);
                    if(_kind==UnitEffect.Synapse||_kind==UnitEffect.Capture)p.localRotation=Quaternion.Euler(55+i*65,t*140*(i==0?1:-1),0);
                }
                else if(_kind==UnitEffect.Shield)
                {
                    float a=(i-2)*Mathf.PI*2/5;p.localPosition=new Vector3(Mathf.Sin(a),.2f,Mathf.Cos(a))*_size*.65f;
                    p.localScale=Vector3.one*_size*.5f;p.localRotation=Quaternion.Euler(80,a*Mathf.Rad2Deg,30);
                }
                else
                {
                    var dir=new Vector3(Mathf.Cos(angle),.2f+(i%3)*.25f,Mathf.Sin(angle));
                    float radius=_kind==UnitEffect.Capture?1-expand:expand;
                    p.localPosition=dir*_size*radius;
                    if(_kind==UnitEffect.Mend||_kind==UnitEffect.Rally)p.localPosition=new Vector3(dir.x*.5f,expand*1.2f,dir.z*.5f)*_size;
                    if(_kind==UnitEffect.Shatter)p.localPosition+=Vector3.down*t*t*.9f;
                    p.localScale=new Vector3(.025f,.065f,.025f)*_size*(1-t*.6f);
                    p.localRotation=Quaternion.Euler(i*37+t*100,i*61,t*120);
                }
            }
        }
        void OnDestroy(){if(_mat!=null)Destroy(_mat);if(_counted){Active--;_counted=false;}}
    }
}
