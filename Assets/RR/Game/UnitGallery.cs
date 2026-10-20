using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using Commandfall.Core;

namespace RR.Game
{
 public sealed class UnitGallery:MonoBehaviour
 {
  public static bool Active {get;private set;}
  public static UnitGallery Instance {get;private set;}
  GameRoot _root;GameObject[] _models;GameObject _model;int _index;Transform _stage;
  VisualElement _panel;Label _title,_info;Slider _timeline;Button _pause;
  readonly Dictionary<string,Button> _clipButtons=new Dictionary<string,Button>();
  readonly List<(VisualElement element,StyleEnum<DisplayStyle> display)> _hidden=new List<(VisualElement,StyleEnum<DisplayStyle>)>();
  CamPose _oldPose,_fit;CamLimits _oldLimits;bool _galaxy,_local,_paused,_battle,_oldSheet,_drag;
  float _oldNear,_oldBand,_oldStars;Vector2 _lastMouse;Vector4 _starPos,_starColor,_ambient;
  int _playGeneration;bool _closed;
  public UnitVisual Current => _model!=null?_model.GetComponent<UnitVisual>():null;
  public static UnitGallery Open(GameRoot root,string id=null)
  {
   if(Active){if(id!=null)Instance.ShowUnit(id);return Instance;}
   if(root.Busy || root.Battle!=null && root.Battle.Animating)return null;
   var models=ArtLibrary.I.models.Where(m=>m!=null&&m.GetComponent<UnitVisual>()!=null).OrderBy(m=>m.name).ToArray();
   if(models.Length==0 || id!=null&&!models.Any(m=>m.name==id))return null;
   var g=new GameObject("Unit inspector").AddComponent<UnitGallery>();g._root=root;g._models=models;g.Setup(id);return g;
  }
  void Setup(string id)
  {
   Active=true;Instance=this;_oldPose=_root.Rig.Current;_oldLimits=_root.Rig.Limits;_oldNear=_root.Rig.Near;
   _paused=_root.Clock.Paused;_root.Clock.Paused=true;_oldSheet=_root.ShipSheetMode;_root.ShipSheetMode=true;
   _galaxy=_root.Galaxy.gameObject.activeSelf;_local=_root.LocalSystem!=null&&_root.LocalSystem.gameObject.activeSelf;
   _battle=_root.Battle!=null&&_root.Battle.gameObject.activeSelf;
   _root.Galaxy.gameObject.SetActive(false);if(_root.LocalSystem!=null)_root.LocalSystem.gameObject.SetActive(false);
   if(_battle)_root.Battle.gameObject.SetActive(false);
   foreach(var element in _root.UI.HudLayer.Children().ToArray()){_hidden.Add((element,element.style.display));element.style.display=DisplayStyle.None;}
   _starPos=Shader.GetGlobalVector("_RR_StarPosWS");_starColor=Shader.GetGlobalVector("_RR_StarColor");_ambient=Shader.GetGlobalVector("_RR_Ambient");
   _oldBand=_root.Sky.GetFloat("_BandStrength");_oldStars=_root.Sky.GetFloat("_StarStrength");_root.Sky.SetFloat("_BandStrength",0);_root.Sky.SetFloat("_StarStrength",.15f);
   _stage=new GameObject("Inspection stage").transform;_stage.SetParent(transform);_stage.position=new Vector3(0,500,0);
   _panel=UIX.Div("panel","unit-inspector");_panel.name="unit-inspector";_root.UI.HudLayer.Add(_panel);
   var scroll=new ScrollView();scroll.style.flexShrink=1;UIRoot.StyleScroll(scroll);_panel.Add(scroll);
   scroll.Add(UIX.Text("UNIT INSPECTION","eyebrow"));
   _title=UIX.Text("","h-display");scroll.Add(_title);_info=UIX.Text("","body");scroll.Add(_info);
   var nav=UIX.Div("row");nav.Add(Named("Previous",()=>Show(_index-1),"inspect-previous"));nav.Add(Named("Next",()=>Show(_index+1),"inspect-next"));scroll.Add(nav);
   scroll.Add(UiKit.Section("01","Animation"));var clips=UIX.Div("row");
   foreach(var clip in new[]{"Idle","Move","Attack","Ability","Hit","Death"}){string c=clip;var b=Named(c,()=>Play(c),"clip-"+c.ToLowerInvariant());_clipButtons[c]=b;clips.Add(b);}scroll.Add(clips);
   var playback=UIX.Div("row");_pause=Named("Pause",TogglePause,"inspect-pause");playback.Add(_pause);
   playback.Add(Named("Slow",()=>{Current.PreviewRate=.5f;},"inspect-slow"));playback.Add(Named("Normal",()=>{Current.PreviewRate=1;},"inspect-normal"));scroll.Add(playback);
   _timeline=new Slider("Pose",0,1){name="inspect-timeline"};_timeline.RegisterValueChangedCallback(e=>{Current.Scrub(e.newValue);_pause.text="Resume";});scroll.Add(_timeline);
   scroll.Add(UiKit.Section("02","View"));var view=UIX.Div("row");
   view.Add(Named("Rotate",()=>_root.Rig.Orbit(45,0),"inspect-rotate"));
   view.Add(Named("Closer",()=>_root.Rig.Zoom(.8f),"inspect-closer"));view.Add(Named("Reset view",ResetView,"inspect-reset"));scroll.Add(view);
   scroll.Add(UIX.Text("Drag to rotate / Scroll to zoom\nSpace to pause / Escape to return","body"));
   var close=Named("Return to game",Close,"inspect-close");close.style.marginTop=18;scroll.Add(close);
   Show(id==null?0:Array.FindIndex(_models,m=>m.name==id));
  }
  static Button Named(string title,Action action,string name){var b=UIX.Btn(title,action);b.name=name;return b;}
  public void ShowUnit(string id){int i=Array.FindIndex(_models,m=>m.name==id);if(i>=0)Show(i);}
  public void Show(int index)
  {
   if(_model!=null)Destroy(_model);_index=(index+_models.Length)%_models.Length;var src=_models[_index];
   _model=Instantiate(src,_stage);_model.transform.localPosition=Vector3.zero;_model.GetComponent<LODGroup>().ForceLOD(0);
   var b=UnitVisual.PresentationBounds(_model);var center=b.center;_title.text=src.name.Replace('_',' ').ToUpperInvariant();
   _info.text=(_index+1)+" / "+_models.Length+"  |  "+(Current.Ground?Current.Vehicle?"Ground vehicle":"Infantry":"Spacecraft")+(Current.ProvisionalName?"\nProvisional identity":"");
   float yaw=150,pitch=Current.Ground&&!Current.Vehicle?8:22,fov=32,aspect=_root.Rig.Cam.aspect;
   var rot=Quaternion.Euler(pitch,yaw,0);var projected=new Bounds();bool first=true;
   foreach(var point in UnitVisual.PresentationVertices(_model)){var v=Quaternion.Inverse(rot)*(point-center);if(first){projected=new Bounds(v,Vector3.zero);first=false;}else projected.Encapsulate(v);}
   float halfX=projected.extents.x,halfY=projected.extents.y,depth=projected.extents.z;
   center+=rot*new Vector3(projected.center.x,projected.center.y,0);
   float tan=Mathf.Tan(fov*.5f*Mathf.Deg2Rad);
   float distance=Mathf.Max(halfY/(tan*.80f),halfX/(tan*aspect*.60f))+depth;
   var focus=center-(rot*Vector3.right)*(distance*tan*aspect*.24f);
   _fit=new CamPose{Focus=focus,Yaw=yaw,Pitch=pitch,Distance=distance,Fov=fov};
   _root.Rig.Limits=new CamLimits{MinDistance=distance*.46f,MaxDistance=distance*2.2f,MinPitch=-30,MaxPitch=75,FocusBounds=new Bounds(center,Vector3.one*30)};
   _root.Rig.PitchForDistance=null;_root.Rig.Near=.03f;ResetView();
   Shader.SetGlobalVector("_RR_StarPosWS",new Vector4(center.x-30,center.y+50,center.z+40,1));
   Shader.SetGlobalVector("_RR_StarColor",new Vector4(2.1f,2.05f,1.98f,1));Shader.SetGlobalVector("_RR_Ambient",new Vector4(.13f,.16f,.23f,.35f));
   Play("Idle");
  }
  public void ResetView(){_root.Rig.Snap(_fit);}
  public void TogglePause(){if(Current==null)return;if(Current.PreviewPaused&&Current.Progress>=.999f)Play(Current.Playing);else Current.PreviewPaused=!Current.PreviewPaused;_pause.text=Current.PreviewPaused?"Resume":"Pause";}
  public void Play(string clip)
  {
   if(Current==null)return;_playGeneration++;Current.Damage(0);Current.Play(clip);_pause.text="Pause";
   foreach(var kv in _clipButtons)kv.Value.EnableInClassList("clip-active",kv.Key==clip);
   if(clip=="Attack" || clip=="Ability" || clip=="Death")StartCoroutine(PreviewEffect(clip,_playGeneration));
  }
  IEnumerator PreviewEffect(string clip,int generation)
  {
   float at=clip=="Death"?.62f:clip=="Ability"?.5f:.38f;
   while(generation==_playGeneration&&Current!=null&&Current.Playing==clip&&Current.Progress<at)yield return null;
   if(generation!=_playGeneration||Current==null||Current.Playing!=clip||Current.PreviewPaused)yield break;
   var fx=clip=="Attack"?UnitEffect.Discharge:clip=="Death"?UnitEffect.Shatter:UnitEffect.Rally;
   UnitEffects.Spawn(_stage,Current.Socket(clip=="Attack"),Current.Energy,clip=="Death"?.8f:clip=="Attack"?.32f:.55f,fx,_model.transform.forward);
  }
  void Update()
  {
   if(Current==null)return;
   _timeline.SetValueWithoutNotify(Current.Progress);_pause.text=Current.PreviewPaused?"Resume":"Pause";
   var kb=Keyboard.current;var m=Mouse.current;
   if(kb!=null){if(kb.escapeKey.wasPressedThisFrame){Close();return;}if(kb.spaceKey.wasPressedThisFrame)TogglePause();}
   if(m==null)return;var pos=m.position.ReadValue();bool over=_root.UI.PointerOverUI(pos);
   if(m.leftButton.wasPressedThisFrame&&!over)_drag=true;
   if(_drag&&m.leftButton.isPressed){var d=pos-_lastMouse;_root.Rig.Orbit(d.x*.25f,-d.y*.2f);}
   if(m.leftButton.wasReleasedThisFrame)_drag=false;
   if(!over){float wheel=m.scroll.ReadValue().y;if(Mathf.Abs(wheel)>.01f)_root.Rig.Zoom(Mathf.Pow(.86f,wheel/120f));}
   _lastMouse=pos;
  }
  public void Close(){if(_closed)return;_closed=true;Restore();Destroy(gameObject);}
  void Restore()
  {
   if(Instance!=this)return;_playGeneration++;_panel?.RemoveFromHierarchy();Active=false;Instance=null;
   if(_root==null)return;_root.ShipSheetMode=_oldSheet;_root.Clock.Paused=_paused;
   _root.Galaxy.gameObject.SetActive(_galaxy);if(_root.LocalSystem!=null)_root.LocalSystem.gameObject.SetActive(_local);
   if(_root.Battle!=null)_root.Battle.gameObject.SetActive(_battle);
   foreach(var e in _hidden)e.element.style.display=e.display;
   _root.Rig.Near=_oldNear;_root.Rig.Limits=_oldLimits;_root.Rig.Snap(_oldPose);
   Shader.SetGlobalVector("_RR_StarPosWS",_starPos);Shader.SetGlobalVector("_RR_StarColor",_starColor);Shader.SetGlobalVector("_RR_Ambient",_ambient);
   _root.Sky.SetFloat("_BandStrength",_oldBand);_root.Sky.SetFloat("_StarStrength",_oldStars);
  }
  void OnDestroy(){Restore();}


  public static IEnumerator Review(GameRoot root,string dir)
  {
   Directory.CreateDirectory(dir);bool userReduced=Settings.ReducedMotion;Settings.ReducedMotion=false;var g=Open(root);yield return null;
   var report=new List<string>();int failures=0;
   var digest=RR.Strategy.SaveGame.Digest(root.G);
   for(int i=0;i<g._models.Length;i++)
   {
    g.Show(i);yield return new WaitForSecondsRealtime(.12f);var v=g.Current;
    bool ok=v!=null&&v.GetComponent<LODGroup>().GetLODs().Length==3;
    var bones=v.GetComponentsInChildren<Transform>().Where(t=>t.name=="Root"||t.name=="Core"||t.name.StartsWith("Arm")||t.name.StartsWith("Leg")).ToArray();
    foreach(var c in new[]{"Idle","Move","Attack","Ability","Hit","Death"})
    {
     ok&=v.HasClip(c);g.Play(c);yield return new WaitForSecondsRealtime(.08f);
     var before=bones.Select(t=>t.localRotation).ToArray();yield return new WaitForSecondsRealtime(.14f);
     bool moved=bones.Where((b,n)=>UnitVisual.RotationMoved(b.localRotation,before[n])).Any();ok&=moved;
     report.Add((moved?"PASS ":"FAIL ")+v.UnitId+" clip "+c+" bone motion");
    }
    g.Play("Idle");yield return new WaitForSecondsRealtime(1.3f);yield return Automation.Shot(dir,"unit_"+v.UnitId);
    report.Add((ok?"PASS ":"FAIL ")+v.UnitId+" LODs/clips "+v.HeroTriangles);if(!ok)failures++;
   }
   if(Args.Has("-rrMotionRecord"))foreach(var id in new[]{"silex_crown_warden","craigon_carapace_drone","silex_throne_ark","craigon_ascension_engine"})
   {
    int index=Array.FindIndex(g._models,m=>m.name==id);if(index<0)continue;
    g.Show(index);yield return new WaitForSecondsRealtime(.2f);MotionCapture.Begin(root,dir,id,9);
    foreach(var c in new[]{"Idle","Move","Attack","Ability","Hit","Death"}){g.Play(c);yield return new WaitForSecondsRealtime(1.5f);}
    while(MotionCapture.Active>0)yield return null;
   }
   bool reduced=Settings.ReducedMotion;Settings.ReducedMotion=true;g.Play("Idle");yield return new WaitForSecondsRealtime(.2f);
   foreach(UnitEffect fx in Enum.GetValues(typeof(UnitEffect)))UnitEffects.Spawn(g._stage,g.Current.Socket(false),g.Current.Energy,.5f,fx);
   yield return new WaitForSecondsRealtime(1.5f);if(UnitEffects.Active!=0)failures++;
   Settings.ReducedMotion=reduced;g.Close();yield return null;
   if(RR.Strategy.SaveGame.Digest(root.G)!=digest)failures++;
   report.Add("Effects remaining: "+UnitEffects.Active+"; failures: "+failures);File.WriteAllLines(Path.Combine(dir,"unit-validation.txt"),report);
   // Real planetary rules, legal commands, and animated views in each roster group.
   for(int group=0;group<3;group++)
   {
    root.StartGroundExercise(group);yield return new WaitForSecondsRealtime(.5f);
    if(root.Battle==null){failures++;continue;}
    yield return Automation.Shot(dir,"ground_"+group);
    var ai=new Commandfall.AI.RosterAI(AIDifficulty.Tactical,1729);int n=0;float deadline=Time.realtimeSinceStartup+90;
    MotionCapture.Begin(root,dir,"ground_"+group,8);
    while(root.Battle!=null&&!root.Battle.S.IsOver&&n<14&&Time.realtimeSinceStartup<deadline)
    {
     if(!root.Battle.Animating && root.Battle.S.ActiveSeat==root.Battle.PlayerSeat){root.Battle.Submit(ai.ChooseAction(root.Battle.S));n++;}
     yield return null;
    }
    while(root.Battle!=null&&root.Battle.Animating&&Time.realtimeSinceStartup<deadline)yield return null;
    if(Time.realtimeSinceStartup>=deadline){failures++;report.Add("FAIL ground exercise "+group+" timed out");}
    report.Add("Ground exercise "+group+": "+n+" legal player commands");
    yield return Automation.Shot(dir,"ground_"+group+"_actions");root.LeaveGroundExercise();yield return null;
   }
   if(RR.Strategy.SaveGame.Digest(root.G)!=digest)failures++;
   while(MotionCapture.Active>0)yield return null;
   Settings.ReducedMotion=userReduced;
   report.Add("Final failures: "+failures);File.WriteAllLines(Path.Combine(dir,"unit-validation.txt"),report);Application.Quit(failures==0?0:1);
  }
 }
}
