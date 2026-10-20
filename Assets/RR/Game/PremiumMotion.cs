using UnityEngine;

namespace RR.Game
{
    // Shared presentation envelopes. These never advance a strategy day or mutate a combat unit.
    public static class MotionCurve
    {
        public static float Smooth(float t) { t=Mathf.Clamp01(t); return t*t*t*(t*(t*6-15)+10); }
        public static float Out(float t) { t=Mathf.Clamp01(t); return 1-(1-t)*(1-t)*(1-t); }
        public static float Damping(float rate, float dt) => 1-Mathf.Exp(-rate*Mathf.Max(0,dt));
    }

    public enum MotionBeat { Impact, Shield, Jump, Destruction, Objective, Charge }

    // Finite, owned effect instances. Imported Blender meshes are shared; only per-effect materials are owned.
    public sealed class MotionBurst : MonoBehaviour
    {
        static Mesh _rail, _shard;
        Material _material;
        Transform _ring;
        readonly Transform[] _pieces=new Transform[8];
        readonly Vector3[] _directions=new Vector3[8];
        float _age, _duration, _size;
        MotionBeat _beat;
        bool _counted;
        public static int Active { get; private set; }
        public static int Peak { get; private set; }
        public static int Spawned { get; private set; }

        static Mesh Asset(string path)
        {
            var prefab=Resources.Load<GameObject>("RR/Motion/"+path);
            return prefab != null ? prefab.GetComponentInChildren<MeshFilter>()?.sharedMesh : null;
        }
        public static MotionBurst Spawn(Transform parent, Vector3 position, Color color, float size, float duration, MotionBeat beat)
        {
            if (_rail==null) _rail=Asset("pulse-rail");
            if (_shard==null) _shard=Asset("crystal-ejecta");
            var go=new GameObject("Motion / "+beat);go.transform.SetParent(parent,false);go.transform.localPosition=position;
            var fx=go.AddComponent<MotionBurst>();fx._size=size;fx._duration=Mathf.Max(.08f,duration);fx._beat=beat;
            fx._material=Look.Unlit(ArtLibrary.I.fx,color*(Settings.ReducedMotion ? .55f : 1.15f),"Motion / "+beat);
            fx._material.SetFloat("_Soft",1);
            fx._ring=fx.Part("Pulse rail",_rail != null ? _rail : MeshFactory.Circle(1,.025f,48));
            if (beat==MotionBeat.Shield || beat==MotionBeat.Charge) fx._ring.localRotation=Quaternion.Euler(65,0,0);
            int count=beat==MotionBeat.Destruction?8:beat==MotionBeat.Impact?4:0;
            if(Settings.ReducedMotion)count=0;
            for(int i=0;i<count;i++)
            {
                float a=(i+.25f)*Mathf.PI*2/count;
                fx._pieces[i]=fx.Part("Ejecta "+i,_shard != null?_shard:MeshFactory.Sphere(4));
                fx._directions[i]=new Vector3(Mathf.Cos(a),.12f+(i%3)*.13f,Mathf.Sin(a)).normalized;
                fx._pieces[i].localRotation=Quaternion.LookRotation(fx._directions[i]);
            }
            fx._counted=true;Active++;Spawned++;Peak=Mathf.Max(Peak,Active);fx.Paint(0);return fx;
        }
        Transform Part(string name,Mesh mesh)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=_material;return go.transform;
        }
        void Update()
        {
            _age+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(_age/_duration);Paint(t);
            if(t>=1)Destroy(gameObject);
        }
        void Paint(float t)
        {
            bool reduced=Settings.ReducedMotion;
            float expand=_beat==MotionBeat.Charge?Mathf.Lerp(1.2f,.25f,MotionCurve.Smooth(t)):Mathf.Lerp(.18f,1.4f,MotionCurve.Out(t));
            _ring.localScale=Vector3.one*_size*(reduced ? .65f : expand);
            float fade=(1-t)*(1-t)*Mathf.Min(1,t*18+.3f);
            _material.SetFloat("_Fade",fade);
            for(int i=0;i<_pieces.Length;i++)if(_pieces[i]!=null)
            {
                _pieces[i].gameObject.SetActive(!reduced);
                _pieces[i].localPosition=_directions[i]*_size*MotionCurve.Out(t)*1.4f;
                _pieces[i].localScale=Vector3.one*_size*.27f*(1-t*.7f);
                _pieces[i].localRotation=Quaternion.LookRotation(_directions[i])*Quaternion.Euler(t*80,t*110,0);
            }
        }
        void OnDestroy() { if(_counted){Active--;_counted=false;} if(_material!=null)Destroy(_material); }
    }

    // Gentle formation suspension, evaluated around authored rest transforms; never modifies fleet coordinates.
    public sealed class ShipSuspension : MonoBehaviour
    {
        Vector3 _rest;
        Quaternion _rotation;
        float _phase;
        public static void Attach(Transform ship, int seed)
        {
            var motion=ship.GetComponent<ShipSuspension>() ?? ship.gameObject.AddComponent<ShipSuspension>();
            motion._rest=ship.localPosition;motion._rotation=ship.localRotation;motion._phase=seed*.731f;
        }
        void LateUpdate()
        {
            float t=Time.unscaledTime*.45f+_phase;
            transform.localPosition=_rest+(Settings.ReducedMotion?Vector3.zero:Vector3.up*Mathf.Sin(t)*.025f);
            transform.localRotation=_rotation*(Settings.ReducedMotion?Quaternion.identity:Quaternion.Euler(Mathf.Sin(t*.7f)*.3f,0,Mathf.Sin(t)*.55f));
        }
    }
    public sealed class FleetMotion : MonoBehaviour
    {
        Quaternion _heading;
        TrailRenderer _trail;
        Material _material;
        Vector3 _last, _target, _display, _arrivalFrom;
        float _arrivalAge=1;
        bool _moving, _initialized;
        public bool Settled => _arrivalAge>=.35f;
        public void SetCourse(Quaternion heading,bool moving,Color tint)
        {
            _heading=heading;_target=transform.localPosition;
            if(_initialized)
            {
                if(_moving && !moving && Vector3.Distance(_display,_target)<20 && !Settings.ReducedMotion)
                {
                    _arrivalFrom=_display;_arrivalAge=0;
                    MotionBurst.Spawn(transform,Vector3.zero,tint*.7f,1,.45f,MotionBeat.Jump);
                }
                _moving=moving;return;
            }
            _moving=moving;_initialized=true;_display=_target;transform.localRotation=heading;_last=transform.position;
            var go=new GameObject("Drive wake");go.transform.SetParent(transform,false);go.transform.localPosition=new Vector3(0,0,-.65f);
            _trail=go.AddComponent<TrailRenderer>();_trail.time=.42f;_trail.minVertexDistance=.12f;
            _trail.startWidth=.16f;_trail.endWidth=0;_trail.numCapVertices=2;
            _material=Look.Unlit(ArtLibrary.I.fx,tint*.65f,"Fleet drive wake");_trail.sharedMaterial=_material;
            _trail.emitting=false;
        }
        void LateUpdate()
        {
            if(!_initialized)return;
            _arrivalAge=Settings.ReducedMotion?1:_arrivalAge+Time.unscaledDeltaTime;
            _display=_moving || Settled?_target:Vector3.Lerp(_arrivalFrom,_target,MotionCurve.Smooth(_arrivalAge/.35f));
            transform.localPosition=_display;
            transform.localRotation=Settings.ReducedMotion?_heading:Quaternion.Slerp(transform.localRotation,_heading,MotionCurve.Damping(8,Time.unscaledDeltaTime));
            bool moved=(transform.position-_last).sqrMagnitude>.00001f;
            _trail.emitting=_moving && moved && !Settings.ReducedMotion;
            if(Settings.ReducedMotion)_trail.Clear();
            _last=transform.position;
        }
        void OnDestroy(){if(_material!=null)Destroy(_material);}
    }

    public sealed class MotionResourceOwner : MonoBehaviour
    {
        public Mesh Mesh;
        public Material Material;
        void OnDestroy(){if(Mesh!=null)Destroy(Mesh);if(Material!=null)Destroy(Material);}
    }

}
