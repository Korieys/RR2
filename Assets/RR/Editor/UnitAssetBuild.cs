using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using RR.Game;

namespace RR.EditorTools
{
 public sealed class UnitImportRules:AssetPostprocessor
 {
  void OnPreprocessModel()
  {
   if(!assetPath.StartsWith("Assets/RR/Art/Units/"))return;
   var m=(ModelImporter)assetImporter;
   m.materialImportMode=ModelImporterMaterialImportMode.None;m.animationType=ModelImporterAnimationType.Legacy;
   m.importAnimation=true;m.bakeAxisConversion=true;m.globalScale=1;m.useFileScale=true;
   m.importNormals=ModelImporterNormals.Import;m.importTangents=ModelImporterTangents.CalculateMikk;
   m.isReadable=false;m.importCameras=false;m.importLights=false;m.addCollider=false;m.meshCompression=ModelImporterMeshCompression.Low;
   m.animationCompression=ModelImporterAnimationCompression.KeyframeReduction;
   m.animationRotationError=.02f;m.animationPositionError=.02f;m.animationScaleError=.02f;
  }
  void OnPreprocessTexture()
  {
   if(!assetPath.StartsWith("Assets/RR/Art/Units/"))return;
   var t=(TextureImporter)assetImporter;t.maxTextureSize=2048;t.mipmapEnabled=true;t.streamingMipmaps=true;t.isReadable=false;
   t.textureCompression=TextureImporterCompression.CompressedHQ;t.anisoLevel=4;
   if(assetPath.EndsWith("normal.png")){t.textureType=TextureImporterType.NormalMap;t.sRGBTexture=false;}
   else if(assetPath.EndsWith("orm.png"))t.sRGBTexture=false;
  }
 }
 public static class UnitAssetBuild
 {
  const string Root="Assets/RR/Art/Units";
  [Serializable]class Stats {public string id,category;public int source_triangles;public int[] lod_triangles;public bool provisional;}
  public static void Inspect()
  {
   var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/craigon_assimilator/craigon_assimilator.fbx"));
   foreach(var r in go.GetComponentsInChildren<SkinnedMeshRenderer>())
   {
    var m=new Mesh();r.BakeMesh(m);
    Debug.Log("[UnitInspect] "+r.name+" scale="+r.transform.lossyScale+" bounds="+r.bounds+" local="+r.localBounds+" baked="+m.bounds+" source="+r.sharedMesh.bounds+" root scale="+r.rootBone.lossyScale+" root pos="+r.rootBone.position);
    var b=new Bounds(r.transform.TransformPoint(m.vertices[0]),Vector3.zero);foreach(var p in m.vertices)b.Encapsulate(r.transform.TransformPoint(p));
    Debug.Log("[UnitInspect] baked world="+b);UnityEngine.Object.DestroyImmediate(m);
   }
   foreach(var t in go.GetComponentsInChildren<Transform>())Debug.Log("[UnitInspect] path "+AnimationUtility.CalculateTransformPath(t,go.transform));
   foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(Root+"/craigon_assimilator/craigon_assimilator.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))
   {
    var bindings=AnimationUtility.GetCurveBindings(clip);Debug.Log("[UnitInspect] clip "+clip.name+" length="+clip.length+" legacy="+clip.legacy+" bindings="+bindings.Length);
    foreach(var binding in bindings.Where(b=>b.path.Contains("Core")||b.path.EndsWith("Root")).Take(18))
    {var curve=AnimationUtility.GetEditorCurve(clip,binding);Debug.Log("[UnitInspect] "+binding.path+" "+binding.propertyName+" keys="+curve.length+" at08="+curve.Evaluate(.08f)+" at22="+curve.Evaluate(.22f));}
    var core=go.transform.Find("Rig/Root/Core");clip.SampleAnimation(go,.08f);var q=core.localRotation;clip.SampleAnimation(go,.22f);Debug.Log("[UnitInspect] sampled core "+q+" -> "+core.localRotation+" angle "+Quaternion.Angle(q,core.localRotation));
   }
   UnityEngine.Object.DestroyImmediate(go);
   go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/craigon_ascendant_ark/craigon_ascendant_ark.fbx"));
   foreach(var clip in AssetDatabase.LoadAllAssetsAtPath(Root+"/craigon_ascendant_ark/craigon_ascendant_ark.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")))
   {var core=go.transform.Find("Rig/Root/Core");clip.SampleAnimation(go,.08f);var q=core.localRotation;clip.SampleAnimation(go,.22f);Debug.Log("[UnitInspect] ascendant "+clip.name+" core "+q.ToString("F8")+" -> "+core.localRotation.ToString("F8")+" angle "+Quaternion.Angle(q,core.localRotation));}
   UnityEngine.Object.DestroyImmediate(go);
  }
  public static void Prepare()
  {
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var lib=AssetDatabase.LoadAssetAtPath<ArtLibrary>(ProjectSetup.LibraryPath);
   var report=new List<string>();int count=0;
   foreach(var dir in Directory.GetDirectories(Root).OrderBy(x=>x))
   {
    string id=Path.GetFileName(dir);string path=dir.Replace('\\','/')+"/"+id+".fbx";
    var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(source==null)continue;
    var stats=JsonUtility.FromJson<Stats>(File.ReadAllText(dir+"/stats.json"));
    var geometry=UnityEngine.Object.Instantiate(source);geometry.name="Geometry";
    var go=new GameObject(id);geometry.transform.SetParent(go.transform,false);geometry.transform.localRotation=Quaternion.Euler(0,180,0);
    foreach(var oldLod in geometry.GetComponentsInChildren<LODGroup>())UnityEngine.Object.DestroyImmediate(oldLod);
    var visual=go.AddComponent<UnitVisual>();visual.UnitId=id;visual.Ground=stats.category!="Ships";visual.Vehicle=stats.category=="GroundVehicles";
    visual.SourceTriangles=stats.source_triangles;visual.HeroTriangles=stats.lod_triangles[0];visual.ProvisionalName=stats.provisional;
    bool bio=id.StartsWith("craigon");visual.Energy=bio?new Color(.70f,.32f,1):new Color(.38f,.80f,1);
    string matPath=dir.Replace('\\','/')+"/Surface.mat";
    var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if(mat==null){mat=new Material(Shader.Find("RR/UnitSurface"));AssetDatabase.CreateAsset(mat,matPath);}
    mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir.Replace('\\','/')+"/albedo.png"));mat.SetColor("_Energy",visual.Energy);mat.SetFloat("_Bio",bio?1:0);EditorUtility.SetDirty(mat);
    mat.SetTexture("_NormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir.Replace('\\','/')+"/normal.png"));
    mat.SetTexture("_MaskMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir.Replace('\\','/')+"/orm.png"));
    var rs=go.GetComponentsInChildren<SkinnedMeshRenderer>();
    var lods=new LOD[3];
    for(int i=0;i<3;i++)
    {
     var matching=rs.Where(r=>r.name.EndsWith("LOD"+i)).ToArray();
     if(matching.Length!=1)throw new Exception(id+": missing LOD "+i);
     foreach(var r in matching){r.sharedMaterial=mat;r.updateWhenOffscreen=false;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;var b=r.localBounds;b.Expand(.6f);r.localBounds=b;}
     lods[i]=new LOD(new[]{.20f,.065f,.012f}[i],matching);
    }
    var lg=go.AddComponent<LODGroup>();lg.fadeMode=LODFadeMode.None;lg.SetLODs(lods);lg.RecalculateBounds();
    var anim=geometry.GetComponent<Animation>();if(anim==null)anim=geometry.AddComponent<Animation>();anim.playAutomatically=false;
    foreach(var old in anim.Cast<AnimationState>().Select(s=>s.clip).ToArray())anim.RemoveClip(old);
    foreach(var a in go.GetComponentsInChildren<Animation>())if(a!=anim)UnityEngine.Object.DestroyImmediate(a);
    var clips=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
    var expected=new[]{"Idle","Move","Attack","Ability","Hit","Death"};
    var ordered=new List<AnimationClip>();
    foreach(var name in expected)
    {
     var clip=clips.FirstOrDefault(c=>c.name==name || c.name.EndsWith("|"+name));
     if(clip==null)throw new Exception(id+": missing clip "+name+"; found "+string.Join(",",clips.Select(c=>c.name)));
     ordered.Add(clip);if(name=="Idle")anim.clip=clip;
    }
    visual.Clips=ordered.ToArray();AnimationUtility.SetAnimationClips(anim,visual.Clips);
    var bones=go.GetComponentsInChildren<Transform>().Where(t=>t.name=="Root"||t.name=="Core"||t.name.StartsWith("Arm")||t.name.StartsWith("Leg")).ToArray();
    foreach(var clip in visual.Clips)
    {
     clip.SampleAnimation(geometry,.08f);var before=bones.Select(b=>b.localRotation).ToArray();clip.SampleAnimation(geometry,.22f);
     if(!bones.Where((b,n)=>UnitVisual.RotationMoved(b.localRotation,before[n])).Any())throw new Exception(id+": no skeletal motion in "+clip.name);
     if(clip.name.EndsWith("|Idle")||clip.name.EndsWith("|Move"))
     {
      clip.SampleAnimation(geometry,0);var start=bones.Select(b=>b.localRotation).ToArray();var positions=bones.Select(b=>b.localPosition).ToArray();clip.SampleAnimation(geometry,clip.length);
      if(bones.Where((b,n)=>UnitVisual.RotationMoved(b.localRotation,start[n])||(b.localPosition-positions[n]).sqrMagnitude>1e-8f).Any())throw new Exception(id+": open loop in "+clip.name);
     }
    }
    visual.Clips[0].SampleAnimation(geometry,0);
    foreach(var t in go.GetComponentsInChildren<Transform>())switch(t.name){case "Muzzle":visual.Muzzle=t;break;case "CoreFX":visual.CoreFX=t;break;case "DriveL":visual.DriveL=t;break;case "DriveR":visual.DriveR=t;break;}
    if(visual.Muzzle==null || go.transform.InverseTransformPoint(visual.Muzzle.position).z<=0)throw new Exception(id+": invalid forward socket");
    foreach(var drive in new[]{visual.DriveL,visual.DriveR})if(drive!=null)drive.rotation=go.transform.rotation;
    var prefab=PrefabUtility.SaveAsPrefabAsset(go,dir.Replace('\\','/')+"/"+id+".prefab");UnityEngine.Object.DestroyImmediate(go);
    if(!id.Contains("__variant")){lib.models.RemoveAll(m=>m!=null&&m.name==id);lib.models.Add(prefab);}
    report.Add(id+": "+string.Join("/",stats.lod_triangles)+" triangles; 6 moving clips; closed Idle/Move loops; forward +Z; "+rs.Length+" skinned LODs");count++;
   }
   EditorUtility.SetDirty(lib);AssetDatabase.SaveAssets();
   Directory.CreateDirectory("Captures/UnitProduction");File.WriteAllLines("Captures/UnitProduction/import-validation.txt",report);
   Debug.Log("[UnitProduction] Prepared "+count+" assets");
  }
  public static void PrepareAndBuild(){Prepare();UiKitBuild.RenderShipPortraits();BuildTools.BuildWindows();}
 }
}
