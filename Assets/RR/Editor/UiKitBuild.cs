using System.IO;
using System.Linq;
using RR.Game;
using UnityEditor;
using UnityEngine;

namespace RR.EditorTools
{
    public static class UiKitBuild
    {
        public static void PrepareAndBuild()
        {
            RenderShipPortraits();
            BuildTools.BuildWindows();
        }

        static Texture2D CropPortrait(Texture2D image)
        {
            var pixels=image.GetPixels32();int x0=image.width,y0=image.height,x1=0,y1=0;
            for(int y=0;y<image.height;y++)for(int x=0;x<image.width;x++)if(pixels[y*image.width+x].a>8){x0=Mathf.Min(x0,x);y0=Mathf.Min(y0,y);x1=Mathf.Max(x1,x);y1=Mathf.Max(y1,y);}
            int side=Mathf.CeilToInt(Mathf.Max(x1-x0+1,y1-y0+1)*1.12f);side=Mathf.Max(16,side);
            int left=(x0+x1-side)/2,bottom=(y0+y1-side)/2;var cropped=new Color32[side*side];
            for(int y=0;y<side;y++)for(int x=0;x<side;x++){int sx=left+x,sy=bottom+y;if(sx>=0&&sx<image.width&&sy>=0&&sy<image.height)cropped[y*side+x]=pixels[sy*image.width+sx];}
            var result=new Texture2D(side,side,TextureFormat.RGBA32,false);result.SetPixels32(cropped);result.Apply();return result;
        }

        [MenuItem("RR/UI Kit/Render ship portraits")]
        public static void RenderShipPortraits()
        {
            const string output = "Assets/Resources/RR/UI/Ships";
            Directory.CreateDirectory(output);
            var preview = new PreviewRenderUtility();
            var oldPos = Shader.GetGlobalVector("_RR_StarPosWS");
            var oldColor = Shader.GetGlobalVector("_RR_StarColor");
            var oldAmbient = Shader.GetGlobalVector("_RR_Ambient");
            int oldAA = QualitySettings.antiAliasing;
            try
            {
                Shader.SetGlobalVector("_RR_StarPosWS", new Vector4(-100, 180, -100, 10));
                Shader.SetGlobalVector("_RR_StarColor", new Vector4(2.6f, 2.8f, 3f, 1));
                Shader.SetGlobalVector("_RR_Ambient", new Vector4(.22f, .28f, .38f, .4f));
                foreach (var model in ArtLibrary.I.models.Where(m => m != null && (m.name.StartsWith("silex_") || m.name.StartsWith("craigon_"))))
                {
                    var go = Object.Instantiate(model); preview.AddSingleGO(go);
                    try
                    {
                        SystemPresentation.ApplyShipMaterial(go, model.name);
                        Shader.SetGlobalVector("_RR_StarPosWS",new Vector4(-100,180,go.GetComponent<UnitVisual>()!=null?100:-100,10));
                        var renders = go.GetComponentsInChildren<Renderer>();
                        if (renders.Length == 0) continue;
                        var bounds = UnitVisual.PresentationBounds(go);
                        var lod=go.GetComponent<LODGroup>();if(lod!=null)lod.ForceLOD(0);
                        var cam = preview.camera;
                        cam.orthographic = true; cam.orthographicSize = bounds.extents.magnitude * .72f;
                        cam.nearClipPlane = .01f; cam.farClipPlane = bounds.size.magnitude * 12 + 10;
                        cam.transform.position = bounds.center + new Vector3(-1.1f, .95f, go.GetComponent<UnitVisual>()!=null?1.45f:-1.45f).normalized * bounds.size.magnitude * 3;
                        cam.transform.LookAt(bounds.center);
                        float halfWidth=0,halfHeight=0;
                        for(int corner=0;corner<8;corner++){
                            var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                            var v=cam.transform.InverseTransformPoint(p);halfWidth=Mathf.Max(halfWidth,Mathf.Abs(v.x));halfHeight=Mathf.Max(halfHeight,Mathf.Abs(v.y));
                        }
                        cam.orthographicSize=Mathf.Max(halfHeight,halfWidth/(384f/224f))*1.08f;
                        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0,0,0,0);
                        preview.BeginPreview(new Rect(0,0,768,448), GUIStyle.none);
                        preview.Render(true);
                        var rendered = preview.EndPreview() as RenderTexture;
                        var previous = RenderTexture.active;
                        var image = new Texture2D(768, 448, TextureFormat.RGBA32, false);
                        try
                        {
                            RenderTexture.active = rendered;
                            image.ReadPixels(new Rect(0,0,768,448), 0,0); image.Apply();
                            var cropped=CropPortrait(image);
                            try {File.WriteAllBytes(output + "/" + model.name + ".png", cropped.EncodeToPNG());}
                            finally {Object.DestroyImmediate(cropped);}
                        }
                        finally { RenderTexture.active = previous; Object.DestroyImmediate(image); }
                    }
                    finally { Object.DestroyImmediate(go); }
                }
            }
            finally
            {
                preview.Cleanup();
                QualitySettings.antiAliasing = oldAA;
                Shader.SetGlobalVector("_RR_StarPosWS", oldPos); Shader.SetGlobalVector("_RR_StarColor", oldColor); Shader.SetGlobalVector("_RR_Ambient", oldAmbient);
            }
            AssetDatabase.Refresh();
            foreach (var path in Directory.GetFiles(output, "*.png"))
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\','/')) as TextureImporter;
                if (importer == null) continue;
                importer.textureType = TextureImporterType.Default; importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            Debug.Log("[RR UI Kit] Ship portraits rendered from source models.");
        }
    }
}
