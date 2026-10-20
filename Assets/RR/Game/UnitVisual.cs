using System;
using System.Linq;
using UnityEngine;

namespace RR.Game
{
    // Authored clips are visual-only. Commandfall remains the sole owner of position, damage and timing rules.
    public sealed class UnitVisual : MonoBehaviour
    {
        public string UnitId;
        public bool Ground, Vehicle;
        public int SourceTriangles, HeroTriangles;
        public bool ProvisionalName;
        public AnimationClip[] Clips;
        public Transform Muzzle, CoreFX, DriveL, DriveR;
        public Color Energy = new Color(.45f,.8f,1f);
        Animation _animation;
        float _remaining;
        float _deathTime;
        string _playing;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        Transform[] _plumes;
        Material _driveMaterial;
        public string Playing => _playing;
        // Quaternion.Angle snaps small rotations to zero; breathing uses smaller, intentional motion.
        public static bool RotationMoved(Quaternion a,Quaternion b) => new Vector4(a.x-b.x,a.y-b.y,a.z-b.z,a.w-b.w).sqrMagnitude>1e-10f;
        public bool PreviewPaused;
        public float PreviewRate=1;
        float BaseSpeed => 1f;
        public float Progress => _animation!=null && _playing!=null && _animation[_playing]!=null ? ((_playing=="Idle"||_playing=="Move")?Mathf.Repeat(_animation[_playing].normalizedTime,1):Mathf.Clamp01(_animation[_playing].normalizedTime)) : 0;
        public void Scrub(float value) {
            if(_animation==null || _playing==null)return;PreviewPaused=true;
            _animation.Stop();_animation.Play(_playing);var state=_animation[_playing];state.normalizedTime=Mathf.Clamp01(value);state.speed=0;_animation.Sample();if(_remaining>=0)_remaining=(1-value)*state.length;
            Damage(0,_playing=="Death"?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.56f,1,value)):0);
        }
        public static Vector3[] PresentationVertices(GameObject go) {
            var points=new System.Collections.Generic.List<Vector3>();
            foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                if(!renderer.name.EndsWith("LOD0"))continue;var mesh=new Mesh();renderer.BakeMesh(mesh);
                points.AddRange(mesh.vertices.Select(v=>renderer.transform.TransformPoint(v)));
                if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
            }
            return points.ToArray();
        }
        // Use the actual posed surface, not expanded animation-culling bounds, for framing.
        public static Bounds PresentationBounds(GameObject go) {
            bool found=false;var result=new Bounds(go.transform.position,Vector3.zero);
            foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                if(!renderer.name.EndsWith("LOD0"))continue;
                var mesh=new Mesh();renderer.BakeMesh(mesh);
                foreach(var vertex in mesh.vertices){var point=renderer.transform.TransformPoint(vertex);if(!found){result=new Bounds(point,Vector3.zero);found=true;}else result.Encapsulate(point);}
                if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);
            }
            if(!found) return BoundsOf(go);return result;
        }
        public static Bounds BoundsOf(GameObject go)
        {
            var rs=go.GetComponentsInChildren<Renderer>();
            if(rs.Length==0)return new Bounds(go.transform.position,Vector3.one);
            var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;
        }
        void Awake()
        {
            _animation=GetComponentInChildren<Animation>();_renderers=GetComponentsInChildren<Renderer>();_block=new MaterialPropertyBlock();
            if(_animation!=null)
            {
                _animation.playAutomatically=false;_animation.cullingType=AnimationCullingType.BasedOnRenderers;
                // FBX action names include the rig prefix; Animation aliases are not retained by prefab serialization.
                var clips=(Clips??new AnimationClip[0]).Concat(_animation.Cast<AnimationState>().Select(s=>s.clip)).Where(c=>c!=null).Distinct().ToArray();
                foreach(var name in new[]{"Idle","Move","Attack","Ability","Hit","Death"})
                {
                    var clip=clips.FirstOrDefault(c=>c.name==name||c.name.EndsWith("|"+name));
                    if(clip!=null)_animation.AddClip(clip,name);
                }
            }
            if(!Ground && ArtLibrary.I!=null)
            {
                _driveMaterial=Look.Unlit(ArtLibrary.I.fx,Energy*1.4f,"Unit drive emission");_driveMaterial.SetFloat("_Soft",1);
                _plumes=new Transform[2];var sockets=new[]{DriveL,DriveR};
                for(int i=0;i<2;i++)if(sockets[i]!=null)
                {
                    var p=new GameObject("Drive glow");p.transform.SetParent(sockets[i],false);_plumes[i]=p.transform;
                    p.AddComponent<MeshFilter>().sharedMesh=UnitEffects.ParticleMesh;p.AddComponent<MeshRenderer>().sharedMaterial=_driveMaterial;
                }
            }
            Play("Idle");
        }
        public bool HasClip(string clip) => _animation!=null && _animation[clip]!=null;
        public void Play(string clip,float seconds=0)
        {
            if(_animation==null)_animation=GetComponentInChildren<Animation>();
            if(_animation==null || _animation[clip]==null)return;
            if(!UnitGallery.Active && _playing==clip && (clip=="Idle" || clip=="Move"))return;
            _playing=clip;var state=_animation[clip];
            _deathTime=0;PreviewPaused=false;
            state.wrapMode=clip=="Idle"||clip=="Move"?WrapMode.Loop:clip=="Death"||UnitGallery.Active?WrapMode.ClampForever:WrapMode.Once;
            state.speed=seconds>0?state.length/seconds:BaseSpeed*PreviewRate;
            state.time=0;
            _remaining=clip=="Idle"||clip=="Move"||clip=="Death"&&!UnitGallery.Active?-1:(seconds>0?seconds:state.length/BaseSpeed);
            _animation.CrossFade(clip,.08f);
        }
        public Vector3 Socket(bool muzzle) => (muzzle?Muzzle:CoreFX)!=null?(muzzle?Muzzle:CoreFX).position:transform.position+Vector3.up*.5f;
        void Update()
        {
            if(_remaining>0 && !PreviewPaused){_remaining-=Time.unscaledDeltaTime*PreviewRate;if(_remaining<=0){if(UnitGallery.Active){_remaining=0;PreviewPaused=true;_animation[_playing].normalizedTime=1;_animation.Sample();}else Play("Idle");}}
            if(_playing=="Death" && UnitGallery.Active)Damage(0,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.56f,1,Progress)));
            if(_animation!=null && _playing!=null && (UnitGallery.Active || _playing=="Idle" || _playing=="Move"))
            {
                var state=_animation[_playing];if(state!=null)state.speed=PreviewPaused || Settings.ReducedMotion && (_playing=="Idle" || _playing=="Move")?0:BaseSpeed*PreviewRate;
            }
            if(_plumes!=null)foreach(var p in _plumes)if(p!=null)
            {
                float thrust=_playing=="Move"?1:.2f;
                p.localScale=new Vector3(.035f,.035f,.06f+thrust*.22f);
                p.localPosition=new Vector3(0,0,-.02f-thrust*.08f);
                p.gameObject.SetActive(_playing!="Death");
            }
        }
        public void Damage(float damage,float dissolve=0)
        {
            if(_renderers==null)_renderers=GetComponentsInChildren<Renderer>();
            if(_block==null)_block=new MaterialPropertyBlock();
            foreach(var r in _renderers){r.GetPropertyBlock(_block);_block.SetFloat("_Damage",damage);_block.SetFloat("_Dissolve",dissolve);r.SetPropertyBlock(_block);}
        }
        void OnDestroy(){if(_driveMaterial!=null)Destroy(_driveMaterial);}
    }
}
