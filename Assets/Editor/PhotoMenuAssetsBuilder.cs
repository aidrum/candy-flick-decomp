using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class PhotoMenuAssetsBuilder
    {
        [MenuItem("Candy Flick/Import Photo mode menu artwork")]
        public static void Build()
        {
            const string folder="Assets/Resources/PhotoModeMenu/";
            Directory.CreateDirectory(folder);
            foreach(string name in new[]{"photo_mode","photo_mode_pressed","add","add_pressed"})
            {
                string path=folder+name+".png";
                string source=Path.Combine(@"D:\Downloads\candyflick\photomode",name+".png");
                if(File.Exists(source))File.Copy(source,path,true);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
                importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
                importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
        }
    }
}
