using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RR.Game
{
 // Explicit offscreen rendering makes evidence capture independent of window occlusion.
 [DefaultExecutionOrder(1000)]
 public sealed class UnitReviewCapture:MonoBehaviour
 {
  void LateUpdate()
  {
   var r=GameRoot.I;if(r==null||r.OffscreenScene==null)return;
   RenderPipeline.SubmitRenderRequest(r.Rig.Cam,new UniversalRenderPipeline.SingleCameraRequest{destination=r.OffscreenScene});
  }
  static Color32[] Read(RenderTexture rt)
  {
   var previous=RenderTexture.active;RenderTexture.active=rt;
   var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);t.Apply();
   var result=t.GetPixels32();Object.Destroy(t);RenderTexture.active=previous;return result;
  }
  public static Texture2D Frame()
  {
   var root=GameRoot.I;if(root==null||root.OffscreenScene==null)return ScreenCapture.CaptureScreenshotAsTexture();
   var scene=Read(root.OffscreenScene);var ui=Read(root.OffscreenUI);
   for(int i=0;i<scene.Length;i++){var c=scene[i];var u=ui[i];float a=u.a/255f;scene[i]=new Color32((byte)(u.r*a+c.r*(1-a)),(byte)(u.g*a+c.g*(1-a)),(byte)(u.b*a+c.b*(1-a)),255);}
   var frame=new Texture2D(root.OffscreenScene.width,root.OffscreenScene.height,TextureFormat.RGBA32,false);frame.SetPixels32(scene);frame.Apply();return frame;
  }
 }
}
