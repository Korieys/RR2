using System.Collections;
namespace RR.Game {
 public sealed partial class GameRoot {
  string _inspectRequested;
  public void InspectUnit(string id) {
   if(string.IsNullOrEmpty(id))return;
   if(Battle!=null && Battle.Animating){if(_inspectRequested==null)StartCoroutine(InspectWhenSettled());_inspectRequested=id;return;}
   UnitGallery.Open(this,id);
  }
  IEnumerator InspectWhenSettled(){
   // Wait for the current visual action to finish; never stop a combat coroutine mid-action.
   yield return null;
   while(Battle!=null&&Battle.Animating)yield return null;
   var id=_inspectRequested;_inspectRequested=null;UnitGallery.Open(this,id);
  }
 }
}

