using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class TimeTravelMotionValidation
    {
        static readonly List<string> checks=new List<string>();
        static readonly string[] ActionsForClearance={"idle","idle2","idle3","idle4","open","close_eat","close_sad","tap","touch_left","touch_right"};
        static void Check(bool value,string text) { if(!value)throw new Exception(text);checks.Add(text); }
        static void Pose(CandyFlickGame game,string action,float time)
        {
            var player=game.Actor.GetComponent<Animation>();player.Stop();player.Play(action);player[action].time=time;player.Sample();
            game.Skins.ConstrainAntenna();
            if(game.Skins.Visual)game.Skins.Visual.SendMessage("LateUpdate");
        }
        public static void Validate(CandyFlickGame game,Action<string> capture)
        {
            var skin=game.Skins.Selected;var player=game.Actor.GetComponent<Animation>();
            var antenna=game.Actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Jt_PonyTail");
            Pose(game,"idle",.3f);
            if(skin==NommieSkin.Original||skin==NommieSkin.Greek||skin==NommieSkin.OmNelle)
                Check(antenna.localScale.sqrMagnitude>.1f,skin+" retains its antenna");
            else if(skin!=NommieSkin.Prehistoric)
                Check(antenna.localScale==Vector3.zero,skin+" hides the original antenna under the hat");
            if(!game.Skins.Visual)return;
            foreach(var renderer in game.Skins.Visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                Check(renderer.quality==SkinQuality.Bone4,skin+" explicitly uses all four skin influences");
            if(skin==NommieSkin.Prehistoric)
            {
                Check(player["open"].length<=.201f,"Prehistoric mouth opens fully within 0.2 seconds");
                Check(Mathf.Abs(player["close_eat"].length-game.assets.chew.length)<.001f,"Prehistoric eating motion matches the cropped eating sound");
                var face=game.Skins.Visual.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Bip")&&!t.name.Contains("hair")).ToArray();
                Pose(game,"open",player["open"].length);var open=face.Select(t=>t.localRotation).ToArray();
                Pose(game,"close_eat",0);
                Check(face.Select((t,i)=>Quaternion.Angle(t.localRotation,open[i])).Max()<.1f,"Prehistoric eating begins at the open-mouth pose without a close/reopen seam");
                foreach(string action in new[]{"close_eat","close_sad","jump"})
                {
                    Pose(game,action,0);var previous=face.Select(t=>t.localRotation).ToArray();float maximum=0;
                    for(int f=1;f<=Mathf.CeilToInt(player[action].length*60);f++)
                    {
                        Pose(game,action,Mathf.Min(f/60f,player[action].length));
                        for(int i=0;i<face.Length;i++) { maximum=Mathf.Max(maximum,Quaternion.Angle(previous[i],face[i].localRotation));previous[i]=face[i].localRotation; }
                    }
                    Check(maximum<40,"Prehistoric "+action+" facial motion is continuous at 60 Hz; max step "+maximum);
                    var end=face.Select(t=>t.localRotation).ToArray();Pose(game,"idle",0);
                    Check(face.Select((t,i)=>Quaternion.Angle(t.localRotation,end[i])).Max()<.1f,"Prehistoric "+action+" settles to idle without a pose jump");
                }
                foreach(float time in new[]{0f,.05f,.1f,.15f,.2f}) { Pose(game,"open",time);capture("Prehistoric-open-"+Mathf.RoundToInt(time*1000)); }
                foreach(float time in new[]{0f,.1f,.2f,.3f,.6f,.96f}) { Pose(game,"close_eat",time);capture("Prehistoric-eat-"+Mathf.RoundToInt(time*1000)); }
                ValidatePettingWave(game,capture);
            }
            else if(skin==NommieSkin.OmNelle)
            {
                OmNelleValidation.Validate(game,capture);
            }
            else
            {
                Check(AnimationUtility.GetCurveBindings(player["open"].clip).Any(b=>b.path.StartsWith("TimeTravelSkin/")),skin+" has authored accessory animation curves");
                if(skin==NommieSkin.Greek)ValidateBracelet(game,capture);
                if(skin==NommieSkin.Disco)
                {
                    var glasses=game.Skins.Visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Bip_glasses");
                    Pose(game,"idle",0);Vector3 start=glasses.localPosition;Quaternion rotation=glasses.localRotation;
                    Pose(game,"open",player["open"].length);
                    Check(Vector3.Distance(start,glasses.localPosition)>.001f||Quaternion.Angle(rotation,glasses.localRotation)>1,"Disco glasses animate relative to the head");
                    var renderer=game.Skins.Visual.GetComponentInChildren<SkinnedMeshRenderer>();
                    Check(renderer.sharedMaterials.Length==3&&renderer.sharedMesh.GetTriangles(0).SequenceEqual(renderer.sharedMesh.GetTriangles(1)),"Disco opaque/lens passes retain identical geometry with a separate hair-only outline");
                    Check(renderer.sharedMaterials[0].shader.name=="Unlit/Transparent Cutout"&&renderer.sharedMaterials[1].shader.name=="Unlit/Transparent","Disco uses standard cutout frame and transparent lens materials with separate red-derived textures");
                    ValidateLensMask(renderer.sharedMaterials);
                    int[] outline=renderer.sharedMesh.GetTriangles(2);var weights=renderer.sharedMesh.boneWeights;var bones=renderer.bones;
                    bool IsGlasses(int vertex)
                    {
                        var w=weights[vertex];
                        return w.weight0>.00001f&&bones[w.boneIndex0].name.Contains("glasses")||w.weight1>.00001f&&bones[w.boneIndex1].name.Contains("glasses")
                            ||w.weight2>.00001f&&bones[w.boneIndex2].name.Contains("glasses")||w.weight3>.00001f&&bones[w.boneIndex3].name.Contains("glasses");
                    }
                    Check(outline.Length>0&&!outline.Any(IsGlasses),"Disco outline contains hair triangles only, with no glasses/lens vertices");
                    Check(renderer.sharedMaterials[2].shader.name=="CandyFlick/Disco Hair Outline"&&renderer.sharedMaterials[2].GetFloat("_Outline")>0,"Disco hair has the native-style outline material");
                    Pose(game,"idle",.3f);capture("Disco-hair-outline-idle");
                    Pose(game,"idle",.3f);capture("Disco-redmask-idle");
                    foreach(string action in ActionsForClearance)
                    {
                        for(int f=0;f<=10;f++) { Pose(game,action,player[action].length*f/10);Check(game.Skins.Visual.GetComponent<TimeTravelVisual>().LensClearance>=.0079f,"Disco lens surface stays in front of the eyes: "+action+" frame "+f); }
                    }
                    foreach(string action in new[]{"idle","open","close_eat","touch_left"})
                    { Pose(game,action,player[action].length*.5f);capture("Disco-motion-"+action); }
                }
            }
            Pose(game,"idle",.3f);
        }
        static void ValidatePettingWave(CandyFlickGame game,Action<string> capture)
        {
            var model=JsonUtility.FromJson<WaveModel>(File.ReadAllText("Assets/Editor/TimeTravelSource/Prehistoric.json"));
            var wave=model.animations.Single(a=>a.name=="greeting");
            foreach(string action in new[]{"touch_left","touch_right"})
            {
                Pose(game,action,.8f);
                foreach(string name in new[]{"BipL_hand1","BipR_hand1"})
                {
                    int index=Array.FindIndex(model.bones,b=>b.name==name);
                    var key=wave.tracks.Single(t=>t.bone==index).keys.OrderBy(k=>Mathf.Abs(k.time-.8f)).First();
                    var expected=new Quaternion(-key.rotation[0],key.rotation[1],key.rotation[2],key.rotation[3]).normalized;
                    var hand=game.Skins.Visual.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
                    Check(Quaternion.Angle(hand.localRotation,expected)<1,"Prehistoric "+action+" uses the authored wave pose for "+name);
                }
                Check(game.Actor.GetComponent<Animation>()[action].wrapMode==WrapMode.Loop,"Prehistoric "+action+" repeats the wave while petting is held");
            }
            foreach(float time in new[]{.4f,.8f,1.2f}) { Pose(game,"touch_left",time);capture("Prehistoric-held-wave-"+Mathf.RoundToInt(time*1000)); }
        }
        static void ValidateBracelet(CandyFlickGame game,Action<string> capture)
        {
            var foot=game.Actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="Bn_RFr_Leg01");
            var controls=game.Skins.Visual.GetComponent<TimeTravelVisual>().attachments.Where(a=>a.bone.name.StartsWith("brace")).ToArray();
            Check(controls.Length==4&&controls.All(a=>a.originalBone==foot.name),"Greek bracelet controls attach to the actual forefoot bone");
            Pose(game,"idle",0);var local=controls.Select(a=>foot.InverseTransformPoint(a.bone.position)).ToArray();
            foreach(string action in new[]{"idle","idle2","idle3","idle4","open","close_eat","close_sad","jump","tap","touch_left","touch_right"})
            {
                float maximum=0;float duration=game.Actor.GetComponent<Animation>()[action].length;
                for(int f=0;f<=10;f++)
                {
                    Pose(game,action,duration*f/10);
                    for(int i=0;i<controls.Length;i++)maximum=Mathf.Max(maximum,Vector3.Distance(local[i],foot.InverseTransformPoint(controls[i].bone.position)));
                }
                Check(maximum<.0002f,"Greek bracelet stays locked to the foot during "+action);
            }
            Pose(game,"idle",.3f);capture("Greek-foot-bracelet-idle");
            Pose(game,"jump",game.Actor.GetComponent<Animation>()["jump"].length*.5f);capture("Greek-foot-bracelet-jump");
        }
        [Serializable] sealed class WaveModel { public WaveBone[] bones;public WaveClip[] animations; }
        [Serializable] sealed class WaveBone { public string name; }
        [Serializable] sealed class WaveClip { public string name;public WaveTrack[] tracks; }
        [Serializable] sealed class WaveTrack { public int bone;public WaveKey[] keys; }
        [Serializable] sealed class WaveKey { public float time;public float[] rotation; }
        static void ValidateLensMask(Material[] materials)
        {
            var quad=GameObject.CreatePrimitive(PrimitiveType.Quad);quad.layer=29;
            var mesh=UnityEngine.Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);int[] indices=mesh.triangles;
            mesh.subMeshCount=2;mesh.SetTriangles(indices,0);mesh.SetTriangles(indices,1);
            quad.GetComponent<MeshFilter>().sharedMesh=mesh;quad.GetComponent<MeshRenderer>().sharedMaterials=materials.Take(2).ToArray();
            var camera=new GameObject("Lens mask validation").AddComponent<Camera>();camera.cullingMask=1<<29;
            camera.transform.position=new Vector3(0,0,-2);camera.orthographic=true;camera.orthographicSize=.5f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.1f;camera.farClipPlane=4;
            var rt=new RenderTexture(512,512,24);camera.targetTexture=rt;var previous=RenderTexture.active;
            Texture2D Render(Color background)
            {
                camera.backgroundColor=background;camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(512,512,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();return image;
            }
            Texture2D black=null,white=null,atlas=null;
            try
            {
                black=Render(Color.black);white=Render(Color.white);
                atlas=new Texture2D(2,2);atlas.LoadImage(File.ReadAllBytes("Assets/TimeTravel/Textures/Disco.png"));
                var colors=atlas.GetPixels();int lens=Array.FindIndex(colors,c=>c.r>.5f&&c.r>c.g*2&&c.r>c.b*2);
                int frame=Array.FindIndex(colors,c=>c.r>.65f&&c.g>.65f&&c.b>.65f);
                float Difference(int index) { Color a=black.GetPixel(index%512,index/512),b=white.GetPixel(index%512,index/512);return Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b); }
                Check(lens>=0&&frame>=0&&Difference(frame)<.02f,"Disco frame pixels remain opaque against both black and white backgrounds");
                Check(Difference(lens)>.1f,"Only Disco lens pixels transmit the background");
                File.WriteAllBytes("Documentation/TimeTravel/Disco-mask-black.png",black.EncodeToPNG());
                File.WriteAllBytes("Documentation/TimeTravel/Disco-mask-white.png",white.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;rt.Release();
                foreach(var obj in new UnityEngine.Object[]{black,white,atlas,rt,camera.gameObject,quad,mesh})if(obj)UnityEngine.Object.DestroyImmediate(obj);
            }
        }
        public static void Save()=>File.WriteAllText("Documentation/timetravel-motion-validation.json",JsonUtility.ToJson(new Evidence {unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));
        [Serializable] sealed class Evidence { public string unity,utc;public string[] checks; }
    }
}
