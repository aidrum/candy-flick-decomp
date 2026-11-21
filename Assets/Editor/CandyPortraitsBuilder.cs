using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class CandyPortraitsBuilder
    {
        const int Cell=512;
        const string Folder="Assets/Resources/CandySkins",EvidenceFolder="Documentation/CandyPortraits";
        static readonly string[] Names={"Candy","Donut","Cupcake"};

        [MenuItem("Candy Flick/Generate candy skin portraits")]
        public static void Build()
        {
            Directory.CreateDirectory(EvidenceFolder);
            var atlas=new Texture2D(Cell*4,Cell,TextureFormat.RGBA32,false);
            atlas.SetPixels32(new Color32[atlas.width*atlas.height]);
            try
            {
                for(int i=0;i<Names.Length;i++)
                {
                    var portrait=Render(i);
                    try
                    {
                        atlas.SetPixels32(i*Cell,0,Cell,Cell,portrait.GetPixels32());
                        File.WriteAllBytes(EvidenceFolder+"/"+Names[i]+".png",portrait.EncodeToPNG());
                    }
                    finally{UnityEngine.Object.DestroyImmediate(portrait);}
                }
                atlas.Apply();string path=Folder+"/portraits.png";
                File.WriteAllBytes(path,atlas.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
                importer.npotScale=TextureImporterNPOTScale.None;importer.wrapMode=TextureWrapMode.Clamp;
                importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
                // The illustrations are no longer used by the menu or by asset regeneration.
                if(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/thumbnails.png"))AssetDatabase.DeleteAsset(Folder+"/thumbnails.png");
                AssetDatabase.SaveAssets();
                File.WriteAllText("Documentation/candy-portraits.json",JsonUtility.ToJson(new Evidence{
                    utc=DateTime.UtcNow.ToString("o"),width=atlas.width,height=atlas.height,cell=Cell,
                    skins=Names,views=new[]{"Top (+Y)","Top (+Y)","Front (+Z)"},
                    method="Orthographic Unity camera renders with preview shader outlines disabled; transparent backgrounds; models fill about two thirds of each image; silhouette outlines are added to the finished images."},true));
                Debug.Log("CANDY_PORTRAITS_READY: Candy and Donut from above; Cupcake from the front. Outlines are baked into the images.");
            }
            finally{UnityEngine.Object.DestroyImmediate(atlas);}
        }

        static GameObject Model(int skin)
        {
            GameObject model;
            if(skin==0)
            {
                var source=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Candies/Candy1/zCandy");
                model=UnityEngine.Object.Instantiate(source.gameObject);
                // Reproduce the original candy's inherited flattening and orientation.
                model.transform.SetPositionAndRotation(Vector3.zero,source.rotation);model.transform.localScale=source.lossyScale;
            }
            else model=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CandySkins/"+Names[skin]));
            model.SetActive(true);
            foreach(var collider in model.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            var renderers=model.GetComponentsInChildren<Renderer>();
            foreach(var renderer in renderers){renderer.enabled=true;renderer.forceRenderingOff=false;}
            Bounds bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.localScale/=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            model.transform.position-=bounds.center;
            return model;
        }
        static Texture2D Render(int skin)
        {
            var preview=new PreviewRenderUtility();var readback=new Texture2D(Cell,Cell,TextureFormat.RGB24,false);
            var previewMaterials=new List<Material>();
            RenderTexture previous=RenderTexture.active;bool drawing=false;
            try
            {
                var model=Model(skin);preview.AddSingleGO(model);
                foreach(var renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials=renderer.sharedMaterials.Select(material=>
                    {
                        var copy=new Material(material){hideFlags=HideFlags.HideAndDontSave};
                        copy.SetShaderPassEnabled("OUTLINE",false);previewMaterials.Add(copy);return copy;
                    }).ToArray();
                var camera=preview.camera;camera.orthographic=true;camera.nearClipPlane=.01f;camera.farClipPlane=10;
                Vector3 direction=skin==2?Vector3.forward:Vector3.up;
                Vector3 up=skin==2?Vector3.up:Vector3.forward;
                camera.transform.SetPositionAndRotation(direction*3,Quaternion.LookRotation(-direction,up));
                var renderers=model.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;
                foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                Vector3 cameraUp=camera.transform.up;
                float vertical=Mathf.Abs(cameraUp.x)*bounds.size.x+Mathf.Abs(cameraUp.y)*bounds.size.y+Mathf.Abs(cameraUp.z)*bounds.size.z;
                camera.orthographicSize=Mathf.Max(bounds.size.x,vertical)*.75f;
                camera.clearFlags=CameraClearFlags.SolidColor;
                preview.BeginPreview(new Rect(0,0,Cell,Cell),GUIStyle.none);drawing=true;
                Color[] Read(Color background)
                {
                    camera.backgroundColor=background;preview.Render();RenderTexture.active=camera.targetTexture;
                    readback.ReadPixels(new Rect(0,0,Cell,Cell),0,0);readback.Apply();return readback.GetPixels();
                }
                Color[] black=Read(Color.black),white=Read(Color.white),pixels=new Color[Cell*Cell];
                for(int i=0;i<pixels.Length;i++)
                {
                    Color b=black[i],w=white[i];float alpha=Mathf.Clamp01(1-Mathf.Max(w.r-b.r,Mathf.Max(w.g-b.g,w.b-b.b)));
                    pixels[i]=alpha<.01f?Color.clear:new Color(Mathf.Clamp01(b.r/alpha),Mathf.Clamp01(b.g/alpha),Mathf.Clamp01(b.b/alpha),alpha);
                }
                preview.EndPreview();drawing=false;
                if(!pixels.Any(p=>p.a>.5f))throw new InvalidOperationException("Empty candy portrait: "+Names[skin]);
                OutlineSilhouette(pixels);
                var result=new Texture2D(Cell,Cell,TextureFormat.RGBA32,false);result.SetPixels(pixels);result.Apply();return result;
            }
            finally
            {
                if(drawing)preview.EndPreview();RenderTexture.active=previous;
                preview.Cleanup();foreach(var material in previewMaterials)UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(readback);
            }
        }
        static void OutlineSilhouette(Color[] pixels)
        {
            // Expand the rendered alpha mask, leaving all opaque model pixels intact.
            // Consistent image-space width avoids model-normal artifacts on open/two-sided faces.
            var source=(Color[])pixels.Clone();const int radius=5;var ink=new Color(.23f,.09f,.02f);
            for(int y=0;y<Cell;y++)for(int x=0;x<Cell;x++)
            {
                int i=y*Cell+x;Color fill=source[i];if(fill.a>=.999f)continue;
                float expanded=fill.a;
                for(int dy=-radius;dy<=radius;dy++)for(int dx=-radius;dx<=radius;dx++)
                {
                    int px=x+dx,py=y+dy;
                    if(dx*dx+dy*dy<=radius*radius&&px>=0&&px<Cell&&py>=0&&py<Cell)expanded=Mathf.Max(expanded,source[py*Cell+px].a);
                }
                float edge=expanded*(1-fill.a),alpha=fill.a+edge;if(alpha<.01f)continue;
                pixels[i]=new Color((fill.r*fill.a+ink.r*edge)/alpha,(fill.g*fill.a+ink.g*edge)/alpha,(fill.b*fill.a+ink.b*edge)/alpha,alpha);
            }
        }
        [Serializable]class Evidence{public string utc,method;public int width,height,cell;public string[] skins,views;}
    }
}
