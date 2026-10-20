using System.Collections.Generic;
using Commandfall.Core;
using UnityEngine;

namespace RR.Game
{
 public sealed partial class BattlePresentation
 {
  void BuildGround()
  {
   _starDir=new Vector3(-.6f,.8f,.4f).normalized;
   var vertices=new List<Vector3>();var tris=new List<int>();
   foreach(var h in S.Map.AllHexes)
   {
    int start=vertices.Count;var c=World(h);c.y=-.06f;vertices.Add(c);
    for(int i=0;i<6;i++)vertices.Add(c+new Vector3(Mathf.Cos((i*60+30)*Mathf.Deg2Rad),0,Mathf.Sin((i*60+30)*Mathf.Deg2Rad))*.96f);
    for(int i=0;i<6;i++){tris.Add(start);tris.Add(start+1+(i+1)%6);tris.Add(start+1+i);}
   }
   var mesh=new UnityEngine.Mesh{name="Proving ground tiles"};mesh.SetVertices(vertices);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
   var mat=new Material(Shader.Find("RR/UnitSurface"));mat.SetColor("_BaseTint",new Color(.11f,.16f,.22f));mat.SetColor("_Energy",Color.black);
   var ground=Mesh("Aurelith basalt terraces",mesh,mat);var owner=ground.AddComponent<MotionResourceOwner>();owner.Mesh=mesh;owner.Material=mat;
  }
  void BuildGroundTerrain()
  {
   foreach(var h in S.Map.AllHexes)
   {
    var type=S.Map.Terrain(h);var pos=World(h);
    if(type==TerrainType.Obstacle || type==TerrainType.Cover)
    {
     var mat=new Material(Shader.Find("RR/UnitSurface"));mat.SetColor("_BaseTint",new Color(.14f,.22f,.3f));mat.SetColor("_Energy",new Color(.25f,.55f,.8f));
     var mesh=MeshFactory.Sphere(2);var stone=Mesh(type==TerrainType.Cover?"Low crystal cover":"Crystal obstruction",mesh,mat);
     stone.transform.localPosition=pos+Vector3.up*.15f;stone.transform.localScale=type==TerrainType.Cover?new Vector3(.65f,.28f,.45f):new Vector3(.48f,.90f,.48f);
     stone.transform.localRotation=Quaternion.Euler(12,(h.Q*37+h.R*19)%360,8);stone.AddComponent<MotionResourceOwner>().Material=mat;
    }
    if(type==TerrainType.Hazard)Puff(pos,new Color(.6f,.2f,.85f),.75f,.35f,.4f);
   }
   foreach(var o in S.Objectives)
   {
    var ring=Mesh("Ground objective",MeshFactory.Circle(.8f,.04f,48),Look.Unlit(ArtLibrary.I.fx,Look.Gold,"Ground objective"));
    ring.transform.localPosition=World(o.Hex)+Vector3.up*.015f;_objectiveRings.Add(ring.transform);
    var owner=ring.AddComponent<MotionResourceOwner>();owner.Material=ring.GetComponent<Renderer>().sharedMaterial;owner.Mesh=ring.GetComponent<MeshFilter>().sharedMesh;
   }
  }
 }
}
