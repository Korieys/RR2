using UnityEditor;
using UnityEngine;

namespace RR.EditorTools
{
    public static class PrismaticBuild
    {
        public static void PrepareAndBuild()
        {
            const string path = "Assets/Resources/RR/UI/Prismatic/crystal-relief.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var texture = (TextureImporter)AssetImporter.GetAtPath(path);
            texture.textureType = TextureImporterType.Default;
            texture.alphaIsTransparency = true;
            texture.mipmapEnabled = false;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.maxTextureSize = 1024;
            texture.textureCompression = TextureImporterCompression.Uncompressed;
            texture.SaveAndReimport();
            AssetDatabase.SaveAssets();
            BuildTools.BuildWindows();
        }
    }
}
