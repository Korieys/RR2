using System.Collections;
using System.Linq;
using Commandfall.Core;
using RR.Strategy;
using UnityEngine;

namespace RR.Game
{
 public sealed partial class GameRoot
 {
  public bool GroundExercise {get;private set;}
  public void StartGroundExercise(int group=0)
  {
   if(Busy||Scale==ViewScale.Battle)return;
   StartCoroutine(GroundRoutine(group));
  }
  IEnumerator GroundRoutine(int group)
  {
   UI.CloseModal();Clock.Paused=true;
   var cfg=new MatchConfig().UseRoster(BattleDomain.Planetary);cfg.ClockSeconds=0;cfg.TechMode=TechMode.Advanced;
   cfg.PointBudget=40;cfg.MaxUnits=8;cfg.Label="Aurelith proving grounds";
   string[][] silex={
    new[]{"royal_castellan","shard_retainer","shard_sentinel","lithic_strider","obelisk_guard","bastion_keeper"},
    new[]{"royal_castellan","crown_warden","geode_colossus","shard_retainer"},
    new[]{"royal_castellan","bastion_sled","facet_ram","geode_walker","throne_engine"}};
   string[][] craigon={
    new[]{"evolved_praetor","carapace_drone","shellbound","tide_skimmer","synapse_stalker","pincer_guard"},
    new[]{"evolved_praetor","carapace_custodian","neural_spiker","harmonic_disruptor","assimilator","giftbearer","fleshweaver"},
    new[]{"evolved_praetor","specimen_crawler","chitin_walker","convergence_hauler","ascension_engine"}};
   group=Mathf.Clamp(group,0,2);
   for(int seat=0;seat<2;seat++)
   {
    string faction=seat==0?"silex":"craigon";
    cfg.Players[seat].DisplayName=seat==0?"Silex expedition":"Craigon incursion";
    cfg.Players[seat].Army=new ArmyRoster{Faction=seat==0?FactionId.Silex:FactionId.Craigon,Name=cfg.Players[seat].DisplayName};
    foreach(var id in (seat==0?silex[group]:craigon[group]))cfg.Players[seat].Army.Units.Add(new RosterEntry(faction+"_"+id));
    cfg.Players[seat].Caps=new TechCaps(1){Unlock=5};cfg.Players[seat].IsAI=seat==1;
   }
   var setup=new BattleSetup{BattleId="ground_exercise",Config=cfg,PlayerSeat=0,FleetIds=new[]{SliceScenario.PlayerFleet,"ground_enemy"}};
   string error;var session=BattleSession.Begin(setup,out error);   // the same authority as every other battle (VS-07)
   if(session==null){UI.Strategy.Toast(error,"bad");yield break;}
   Busy=true;_setup=setup;GroundExercise=true;
   Galaxy.gameObject.SetActive(false);if(LocalSystem!=null)LocalSystem.gameObject.SetActive(false);
   Battle=BattlePresentation.Build(this,session,G.GetSystem(SliceScenario.HeroSystem),WorldRoot);
   Scale=ViewScale.Battle;_resultShown=false;
   Rig.Limits=new CamLimits{MinDistance=7,MaxDistance=52,MinPitch=24,MaxPitch=82,FocusBounds=new Bounds(Vector3.zero,new Vector3(26,3,18))};
   Rig.Near=.1f;Rig.Far=9000;Rig.Snap(BattlePose());
   UI.Strategy.Root.style.display=UnityEngine.UIElements.DisplayStyle.None;UI.Strategy.ClearLabels();
   _battleHud=new BattleHud(this,Battle,UI.HudLayer);Audio.Bed("amb_battle",.5f);
   yield return null;Busy=false;
  }
  public void LeaveGroundExercise()
  {
   if(!GroundExercise)return;
   UI.CloseModal();_resultDone=null;_resultShown=false;
   if(_battleHud!=null){_battleHud.Dispose();_battleHud=null;}
   if(Battle!=null)Destroy(Battle.gameObject);Battle=null;GroundExercise=false;Busy=false;
   if(LocalSystem!=null)LocalSystem.gameObject.SetActive(false);
   Galaxy.gameObject.SetActive(true);UI.Strategy.Root.style.display=UnityEngine.UIElements.DisplayStyle.Flex;
   EnterGalaxyImmediate();UI.Strategy.RefreshAll();
  }
 }
}
