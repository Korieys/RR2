using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using RR.Strategy;

namespace RR.Game {
 public static class UnitPolishReview {
  static readonly List<string> Lines=new List<string>();static int failures;
  static void Check(bool ok,string label){Lines.Add((ok?"PASS ":"FAIL ")+label);if(!ok)failures++;Debug.Log("[UnitPolish] "+Lines[Lines.Count-1]);}
  static bool Submit(GameRoot root,string name){
   var b=root.UI.Root.Q<Button>(name);if(b==null||!b.enabledInHierarchy)return false;
   using(var e=NavigationSubmitEvent.GetPooled()){e.target=b;b.SendEvent(e);}return true;
  }
  public static IEnumerator Run(GameRoot root,string dir) {
   Directory.CreateDirectory(dir);Lines.Clear();failures=0;
   bool oldMotion=Settings.ReducedMotion;float oldScale=Settings.UiScale;Settings.ReducedMotion=false;
   root.Clock.Paused=true;var digest=SaveGame.Digest(root.G);
   var fleet=root.G.Fleets.First(f=>f.OwnerId==root.G.Player.Id&&f.Ships.Count>0);
   var lead=fleet.Ships.OrderByDescending(x=>ShipCatalog.IsLeader(x.DefId)).First();
   foreach(float scale in new[]{1f,1.4f}){
    Settings.UiScale=scale;root.UI.ApplyScale();yield return new WaitForSecondsRealtime(.4f);
    Check(Submit(root,"fleet-card-"+fleet.Id),"fleet selection accepts UI submit at "+scale);
    yield return new WaitForSecondsRealtime(.5f);
    var card=root.UI.Root.Q("fleet-card-"+fleet.Id);
    using(var e=PointerEnterEvent.GetPooled()){e.target=card;card.SendEvent(e);}
    yield return new WaitForSecondsRealtime(.15f);yield return Automation.Shot(dir,"fleet-hover-"+scale);
    string selected=root.Selection.FleetId;var pose=root.Rig.Current;
    Check(Submit(root,"inspect-"+lead.DefId),"portrait accepts UI submit");
    yield return new WaitForSecondsRealtime(.3f);
    var gallery=UnitGallery.Instance;
    Check(gallery!=null&&gallery.Current.UnitId==lead.DefId,"clicked ship opens exact model");
    Check(root.Selection.FleetId==selected,"portrait click does not bubble into fleet selection");
    yield return Automation.Shot(dir,"ship-inspector-"+scale);
    if(gallery!=null){
     float before=root.Rig.Target.Yaw;Submit(root,"inspect-rotate");Check(root.Rig.Target.Yaw>before,"rotate control");
     float distance=root.Rig.Target.Distance;Submit(root,"inspect-closer");Check(root.Rig.Target.Distance<distance,"closer control");
     Submit(root,"inspect-reset");Check(Mathf.Abs(root.Rig.Current.Distance-distance)<.01f,"reset framing");
     Submit(root,"clip-attack");Submit(root,"inspect-slow");Check(gallery.Current.PreviewRate==.5f,"half-speed control");
     gallery.Current.Scrub(.38f);yield return null;Check(gallery.Current.PreviewPaused&&Mathf.Abs(gallery.Current.Progress-.38f)<.01f,"pose scrub pauses at requested contact");
     gallery.Current.PreviewRate=1;gallery.Play("Hit");yield return new WaitForSecondsRealtime(1);
     Check(gallery.Current.PreviewPaused&&gallery.Current.Playing=="Hit"&&gallery.Current.Progress>.99f,"completed preview holds its clip for scrubbing");
     Submit(root,"inspect-close");yield return null;
     Check(!UnitGallery.Active&&(root.Rig.Current.Focus-pose.Focus).sqrMagnitude<.0001f,"close restores exact game camera");
     Check(SaveGame.Digest(root.G)==digest,"inspection preserves campaign");
    }
   }
   if(Args.Has("-rrPolishUiOnly")){Settings.ReducedMotion=oldMotion;Settings.UiScale=oldScale;root.UI.ApplyScale();Lines.Add("Final failures: "+failures);File.WriteAllLines(Path.Combine(dir,"polish-validation.txt"),Lines);Application.Quit(failures==0?0:1);yield break;}
   Settings.UiScale=1;root.UI.ApplyScale();var g=UnitGallery.Open(root);yield return null;
   var ids=ArtLibrary.I.models.Where(m=>m!=null&&m.GetComponent<UnitVisual>()!=null).Select(m=>m.name).OrderBy(x=>x).ToArray();
   foreach(var id in ids){
    g.ShowUnit(id);yield return null;var v=g.Current;Check(v!=null&&v.UnitId==id,"inspect "+id);
    var bones=v.GetComponentsInChildren<Transform>().Where(t=>t.name=="Core"||t.name=="Head"||t.name.StartsWith("Arm")).ToArray();
    foreach(var clip in new[]{"Idle","Move","Attack","Ability","Hit","Death"}){
     g.Play(clip);yield return new WaitForSecondsRealtime(.08f);var q=bones.Select(b=>b.localRotation).ToArray();
     yield return new WaitForSecondsRealtime(.16f);Check(bones.Where((b,n)=>UnitVisual.RotationMoved(b.localRotation,q[n])).Any(),id+" "+clip+" live motion");
     v.Scrub(clip=="Death"?.48f:clip=="Hit"?.18f:clip=="Move"?.24f:clip=="Attack"?.38f:.5f);
     yield return null;
     if(v.Ground)yield return Automation.Shot(dir,id+"_"+clip);
    }
    g.Play("Idle");yield return new WaitForSecondsRealtime(.1f);
    if(!v.Ground)yield return Automation.Shot(dir,id+"_Idle");
   }
   foreach(var id in new[]{"craigon_carapace_drone","silex_crown_warden","craigon_chitin_walker","silex_geode_walker","silex_facet_ram","silex_throne_ark","craigon_sporewing_fighter"}){
    g.ShowUnit(id);yield return null;MotionCapture.Begin(root,dir,id,12);
    foreach(var c in new[]{"Idle","Move","Attack","Ability","Hit","Death"}){g.Play(c);yield return new WaitForSecondsRealtime(2);}
    while(MotionCapture.Active>0)yield return null;
   }
   Settings.ReducedMotion=true;g.Play("Idle");yield return new WaitForSecondsRealtime(.15f);
   var core=g.Current.GetComponentsInChildren<Transform>().First(t=>t.name=="Core");var fixedPose=core.localRotation;
   yield return new WaitForSecondsRealtime(.2f);Check(!UnitVisual.RotationMoved(core.localRotation,fixedPose),"reduced motion freezes ambient pose");
   g.Close();yield return null;
   root.StartGroundExercise(0);yield return new WaitForSecondsRealtime(.2f);
   if(root.Battle!=null){
    var battle=root.Battle;var unit=battle.S.Units.First(u=>u.Alive&&ArtLibrary.I.Model(u.Def.Id)?.GetComponent<UnitVisual>()!=null);
    int history=battle.History.Count;int turn=battle.S.ActiveSeat;
    root.InspectUnit(unit.Def.Id);yield return new WaitForSecondsRealtime(.4f);
    Check(UnitGallery.Active&&UnitGallery.Instance.Current.UnitId==unit.Def.Id,"battle inspection opens exact unit");
    Check(battle.History.Count==history&&battle.S.ActiveSeat==turn,"battle inspection suspends AI and commands");
    yield return Automation.Shot(dir,"battle-inspection");
    UnitGallery.Instance?.Close();yield return null;Check(battle.gameObject.activeSelf,"battle resumes after inspection");root.LeaveGroundExercise();yield return null;
   }else Check(false,"ground battle available");
   Settings.ReducedMotion=false;
   for(int group=0;group<3;group++){
    root.StartGroundExercise(group);yield return new WaitForSecondsRealtime(.3f);
    if(root.Battle==null){Check(false,"ground group "+group);continue;}
    var ai=new Commandfall.AI.RosterAI(Commandfall.Core.AIDifficulty.Tactical,1729);int commands=0;float deadline=Time.realtimeSinceStartup+90;
    while(root.Battle!=null&&!root.Battle.S.IsOver&&commands<14&&Time.realtimeSinceStartup<deadline){
     if(!root.Battle.Animating&&root.Battle.S.ActiveSeat==root.Battle.PlayerSeat){root.Battle.Submit(ai.ChooseAction(root.Battle.S));commands++;}yield return null;
    }
    while(root.Battle!=null&&root.Battle.Animating&&Time.realtimeSinceStartup<deadline)yield return null;
    Check(Time.realtimeSinceStartup<deadline&&commands>0,"ground group "+group+": "+commands+" legal player commands");
    yield return Automation.Shot(dir,"ground-combat-"+group);root.LeaveGroundExercise();yield return null;
   }
   yield return new WaitForSecondsRealtime(1.6f);
   Check(UnitEffects.Active==0,"inspection effects clean up");Check(SaveGame.Digest(root.G)==digest,"campaign unchanged after all inspection contexts");
   Settings.ReducedMotion=oldMotion;Settings.UiScale=oldScale;root.UI.ApplyScale();
   Lines.Add("Final failures: "+failures);File.WriteAllLines(Path.Combine(dir,"polish-validation.txt"),Lines);Application.Quit(failures==0?0:1);
  }
 }
}

