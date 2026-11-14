using System;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class SkinMenuAssetsBuilder
    {
        const string Key="CandyFlickSkinMenuAssets", Folder="Assets/Resources/SkinMenu";
        static CandyFlickGame game;
        static int index;
        static double next;
        static Texture2D atlas;
        static SkinMenuAssetsBuilder(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        [MenuItem("Candy Flick/Generate skin menu assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);Directory.CreateDirectory("Documentation/SkinMenu");
            foreach(string name in new[]{"skins_bg","skins_btn","skins_btn_pressed","skin_btn_bg","skin_btn_bg_selected"})
            {
                string path=Folder+"/"+name+".png";
                File.Copy(Path.Combine(@"D:\Downloads\skins",name+".png"),path,true);
                Import(path);
            }
            if(File.Exists("Documentation/SkinMenu/assets-failure.txt"))File.Delete("Documentation/SkinMenu/assets-failure.txt");
            SessionState.SetBool(Key+"HadSkin",PlayerPrefs.HasKey(TimeTravelSkins.Preference));
            SessionState.SetInt(Key+"Skin",PlayerPrefs.GetInt(TimeTravelSkins.Preference));
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Import(string path)
        {
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Default;importer.mipmapEnabled=false;
            importer.alphaIsTransparency=true;importer.wrapMode=TextureWrapMode.Clamp;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;
            importer.SaveAndReimport();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.3;
            try
            {
                if(!game)
                {
                    game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
                    game.StartGame();game.enabled=false;game.Actor.enabled=false;
                    game.GetComponent<CandyFlickUI>().enabled=false;
                    Time.timeScale=0;AudioListener.pause=true;Application.runInBackground=true;
                    atlas=new Texture2D(2048,1024,TextureFormat.RGBA32,false);
                }
                if(index<TimeTravelSkins.Names.Length)
                {
                    game.Skins.Select((NommieSkin)index);
                    var animation=game.Actor.GetComponent<Animation>();animation.Stop();animation.Play("idle");
                    animation["idle"].time=.3f;animation.Sample();game.Skins.ConstrainAntenna();
                    if(game.Skins.Visual)game.Skins.Visual.SendMessage("LateUpdate",SendMessageOptions.DontRequireReceiver);
                    Texture2D portrait=Capture();
                    atlas.SetPixels32((index%4)*512,(1-index/4)*512,512,512,portrait.GetPixels32());
                    File.WriteAllBytes("Documentation/SkinMenu/"+TimeTravelSkins.Names[index]+".png",portrait.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(portrait);index++;return;
                }
                atlas.Apply();string output=Folder+"/nommies.png";File.WriteAllBytes(output,atlas.EncodeToPNG());Import(output);
                File.WriteAllText("Documentation/skin-menu-assets.json",JsonUtility.ToJson(new Evidence{
                    utc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion,width=2048,height=1024,cell=512,
                    skins=TimeTravelSkins.Names,method="Idle at 0.3 seconds; black/white dual render, straight-alpha matting; silhouette crop with 8% padding; four columns, two rows"
                },true));
                Finish(null);
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static Texture2D Capture()
        {
            var actor=game.Actor.transform;
            var camera=new GameObject("Skin portrait camera").AddComponent<Camera>();
            var objects=actor.GetComponentsInChildren<Transform>(true);var layers=objects.Select(t=>t.gameObject.layer).ToArray();
            var renderers=actor.GetComponentsInChildren<Renderer>(true);
            var forceOff=renderers.Select(r=>r.forceRenderingOff).ToArray();var enabled=renderers.Select(r=>r.enabled).ToArray();
            var skinned=renderers.OfType<SkinnedMeshRenderer>().ToArray();var recalculate=skinned.Select(r=>r.forceMatrixRecalculationPerRender).ToArray();
            var rt=new RenderTexture(512,512,24,RenderTextureFormat.ARGB32){antiAliasing=4};var previous=RenderTexture.active;
            var readback=new Texture2D(512,512,TextureFormat.RGB24,false);
            try
            {
                for(int i=0;i<objects.Length;i++)objects[i].gameObject.layer=31;
                foreach(var r in renderers){r.enabled=game.Skins.Includes(r);r.forceRenderingOff=false;}
                foreach(var r in skinned)r.forceMatrixRecalculationPerRender=true;
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;
                camera.nearClipPlane=1;camera.farClipPlane=2000;camera.fieldOfView=38;
                camera.transform.position=actor.TransformPoint(new Vector3(0,.32f,1.5f));
                camera.transform.LookAt(actor.TransformPoint(new Vector3(0,.29f,0)));camera.targetTexture=rt;
                Color[] Read(Color bg){camera.backgroundColor=bg;camera.Render();RenderTexture.active=rt;readback.ReadPixels(new Rect(0,0,512,512),0,0);readback.Apply();return readback.GetPixels();}
                var black=Read(Color.black);var white=Read(Color.white);var pixels=new Color[black.Length];
                for(int i=0;i<pixels.Length;i++)
                {
                    Color b=black[i],w=white[i];float a=Mathf.Clamp01(1-Mathf.Max(w.r-b.r,Mathf.Max(w.g-b.g,w.b-b.b)));
                    pixels[i]=a<.01f?Color.clear:new Color(Mathf.Clamp01(b.r/a),Mathf.Clamp01(b.g/a),Mathf.Clamp01(b.b/a),a);
                }
                var raw=new Texture2D(512,512,TextureFormat.RGBA32,false);raw.SetPixels(pixels);raw.Apply();
                // Fill each square with the complete silhouette rather than the camera's empty margins.
                int minX=512,minY=512,maxX=0,maxY=0;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)if(pixels[y*512+x].a>.03f)
                {minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);}
                float size=Mathf.Max(maxX-minX+1,maxY-minY+1)*1.08f;
                float left=(minX+maxX+1-size)/2, bottom=(minY+maxY+1-size)/2;
                for(int y=0;y<512;y++)for(int x=0;x<512;x++)
                    pixels[y*512+x]=raw.GetPixelBilinear((left+(x+.5f)*size/512)/512,(bottom+(y+.5f)*size/512)/512);
                UnityEngine.Object.DestroyImmediate(raw);
                var result=new Texture2D(512,512,TextureFormat.RGBA32,false);result.SetPixels(pixels);result.Apply();return result;
            }
            finally
            {
                for(int i=0;i<objects.Length;i++)objects[i].gameObject.layer=layers[i];
                for(int i=0;i<renderers.Length;i++){renderers[i].forceRenderingOff=forceOff[i];renderers[i].enabled=enabled[i];}
                for(int i=0;i<skinned.Length;i++)skinned[i].forceMatrixRecalculationPerRender=recalculate[i];
                RenderTexture.active=previous;camera.targetTexture=null;rt.Release();
                UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            if(SessionState.GetBool(Key+"HadSkin",false))PlayerPrefs.SetInt(TimeTravelSkins.Preference,SessionState.GetInt(Key+"Skin",0));
            else PlayerPrefs.DeleteKey(TimeTravelSkins.Preference);PlayerPrefs.Save();
            if(error!=null){File.WriteAllText("Documentation/SkinMenu/assets-failure.txt",error);Debug.LogError(error);}
            if(atlas)UnityEngine.Object.DestroyImmediate(atlas);
            EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,unity,method;public int width,height,cell;public string[] skins;}
    }
}
