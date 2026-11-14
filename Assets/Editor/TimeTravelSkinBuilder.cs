using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class TimeTravelSkinBuilder
    {
        const string Source = "Assets/Editor/TimeTravelSource/";
        const string Output = "Assets/TimeTravel/";
        [Serializable] sealed class Model { public string name; public Bone[] bones; public Submesh[] meshes; public Clip[] animations; }
        [Serializable] sealed class Bone { public string name; public int parent; public float[] position, rotation, inversePosition, inverseRotation; }
        [Serializable] sealed class Submesh { public Vertex[] vertices; public int[] triangles; }
        [Serializable] sealed class Vertex { public float[] position, normal, uv; public Influence[] weights; }
        [Serializable] sealed class Influence { public int bone; public float weight; }
        [Serializable] sealed class Clip { public string name; public float duration; public Track[] tracks; }
        [Serializable] sealed class Track { public int bone; public Key[] keys; }
        [Serializable] sealed class Key { public float time; public float[] position, rotation; }
        static Vector3 P(float[] p, float scale) => new Vector3(-p[0], p[1], p[2]) * scale;
        // Cal3D rotates vectors by the conjugate quaternion; Unity also needs X reflection.
        static Quaternion Q(float[] q) => new Quaternion(-q[0], q[1], q[2], q[3]).normalized;
        static T Save<T>(T value, string path) where T : UnityEngine.Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing) { EditorUtility.CopySerialized(value, existing); UnityEngine.Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(value, path); return value;
        }

        [MenuItem("Candy Flick/Import Time Travel skins")]
        public static void Build()
        {
            Directory.CreateDirectory(Output + "Meshes"); Directory.CreateDirectory(Output + "Materials");
            Directory.CreateDirectory(Output + "Textures"); Directory.CreateDirectory(Output + "Animations");
            Directory.CreateDirectory("Assets/Resources/TimeTravel"); AssetDatabase.Refresh();
            var geometry = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));
            try
            {
                Transform actor = geometry.transform.Find("OmNom@All-1");
                var body = actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "OmNom_Mesh");
                var originalBones = actor.GetComponentsInChildren<Transform>(true);
                // Match source bones by their global bind positions, since several exporter names differ.
                foreach (NommieSkin skin in Enum.GetValues(typeof(NommieSkin)))
                {
                    if (skin == NommieSkin.Original) continue;
                    Model model = JsonUtility.FromJson<Model>(File.ReadAllText(Source + skin + ".json"));
                    bool prehistoric = skin == NommieSkin.Prehistoric;
                    float scale = prehistoric ? .53776795f / .71954298f : .01f;
                    var root = new GameObject("TimeTravelSkin");
                    try
                    {
                        root.transform.SetParent(actor, false);
                        root.transform.localRotation = body.transform.localRotation;
                        if (prehistoric) root.transform.localRotation = Quaternion.AngleAxis(180, Vector3.up) * root.transform.localRotation;
                        root.transform.localPosition = body.transform.localPosition;
                        var visual = root.AddComponent<TimeTravelVisual>();
                        Transform[] bones = model.bones.Select(b => new GameObject(b.name).transform).ToArray();
                        for (int i = 0; i < bones.Length; i++)
                        {
                            Bone b = model.bones[i];
                            bones[i].SetParent(b.parent < 0 ? root.transform : bones[b.parent], false);
                            bones[i].localPosition = P(b.position, scale); bones[i].localRotation = Q(b.rotation);
                        }
                        if (!prehistoric)
                        {
                            var attachments = new List<TimeTravelVisual.Attachment>();
                            for (int i = 0; i < bones.Length; i++)
                            {
                                // Position match only existing body bones; accessory-only bones remain in their authored hierarchy.
                                var candidates = originalBones.Where(t => Vector3.Distance(actor.InverseTransformPoint(t.position), actor.InverseTransformPoint(bones[i].position)) < .00002f).ToArray();
                                Transform driver = candidates.FirstOrDefault(t => t.name == model.bones[i].name)
                                    ?? candidates.FirstOrDefault(t => model.bones[i].name == "OmNomoot" && t.name == "OmNom_Root")
                                    ?? candidates.FirstOrDefault();
                                // The bracelet surrounds the forefoot skin joint. Its Time Travel
                                // animation is not the Candy Flick foot's motion, so lock its
                                // control bones to the actual foot rather than replaying that track.
                                if(skin==NommieSkin.Greek && IsBraceBone(model.bones[i].name))
                                    driver=originalBones.Single(t=>t.name=="Bn_RFr_Leg01");
                                if (!driver) continue;
                                attachments.Add(new TimeTravelVisual.Attachment { bone = bones[i], originalBone = driver.name,
                                    correction = driver.worldToLocalMatrix * bones[i].localToWorldMatrix });
                            }
                            if (attachments.Count < 4) throw new Exception("Unmatched accessory rig: " + skin);
                            visual.attachments = attachments.ToArray();
                        }
                        string png = Output + "Textures/" + skin + ".png";
                        File.Copy(Source + skin + ".png", png, true); AssetDatabase.ImportAsset(png);
                        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
                        importer.wrapMode = TextureWrapMode.Clamp; importer.mipmapEnabled = false;
                        importer.alphaIsTransparency = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
                        importer.SaveAndReimport();
                        var material = new Material(body.sharedMaterial) { name = skin.ToString() };
                        material.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
                        Material lenses=null,hairOutline=null;Texture2D lensAtlas=null;
                        if(skin==NommieSkin.Disco)
                        {
                            lensAtlas=new Texture2D(2,2);lensAtlas.LoadImage(File.ReadAllBytes(png));
                            Color32[] pixels=lensAtlas.GetPixels32();
                            Color32[] framePixels=(Color32[])pixels.Clone(),lensPixels=(Color32[])pixels.Clone();
                            for(int i=0;i<pixels.Length;i++)
                            {
                                Color32 c=pixels[i];bool red=c.r>80 && c.r>c.g*1.8f && c.r>c.b*1.8f;
                                framePixels[i].a=(byte)(red?0:255);
                                lensPixels[i].a=(byte)(red?160:0);
                            }
                            Texture2D SaveMask(string name,Color32[] colors)
                            {
                                var texture=new Texture2D(lensAtlas.width,lensAtlas.height,TextureFormat.RGBA32,false);
                                texture.SetPixels32(colors);texture.Apply();string path=Output+"Textures/"+name+".png";
                                File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
                                AssetDatabase.ImportAsset(path);var settings=(TextureImporter)AssetImporter.GetAtPath(path);
                                settings.mipmapEnabled=false;settings.alphaIsTransparency=false;
                                settings.textureCompression=TextureImporterCompression.Uncompressed;settings.wrapMode=TextureWrapMode.Clamp;
                                settings.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                            }
                            material.shader=Shader.Find("Unlit/Transparent Cutout");
                            material.mainTexture=SaveMask("Disco_Frame",framePixels);material.SetFloat("_Cutoff",.5f);
                            lenses=new Material(Shader.Find("Unlit/Transparent")) {name="Disco_Lenses",mainTexture=SaveMask("Disco_Lenses",lensPixels)};
                            lenses=Save(lenses,Output+"Materials/Disco_Lenses.mat");
                            hairOutline=new Material(Shader.Find("CandyFlick/Disco Hair Outline")) { name="Disco_HairOutline" };
                            hairOutline.SetFloat("_Outline",body.sharedMaterial.GetFloat("_Outline"));
                            hairOutline.SetColor("_OutlineColor",body.sharedMaterial.GetColor("_OutlineColor"));
                            hairOutline=Save(hairOutline,Output+"Materials/Disco_HairOutline.mat");
                        }
                        material = Save(material, Output + "Materials/" + skin + ".mat");
                        for (int s = 0; s < model.meshes.Length; s++)
                        {
                            Submesh source = model.meshes[s];
                            var mesh = new Mesh { name = skin + "_" + s };
                            mesh.vertices = source.vertices.Select(v => P(v.position, scale)).ToArray();
                            mesh.normals = source.vertices.Select(v => P(v.normal, 1)).ToArray();
                            mesh.uv = source.vertices.Select(v => new Vector2(v.uv[0], 1-v.uv[1])).ToArray();
                            int[] triangles = (int[])source.triangles.Clone();
                            for (int t = 0; t < triangles.Length; t += 3) (triangles[t+1], triangles[t+2]) = (triangles[t+2], triangles[t+1]);
                            mesh.triangles = triangles;
                            if(lenses)
                            {
                                visual.lensVertices=Enumerable.Range(0,source.vertices.Length).Where(i=>{
                                    Vector2 uv=mesh.uv[i];Color c=lensAtlas.GetPixelBilinear(uv.x,uv.y);
                                    return c.r>.3f&&c.r>c.g*1.8f&&c.r>c.b*1.8f;
                                }).ToArray();
                                if(visual.lensVertices.Length==0)throw new Exception("Missing lens surface vertices");
                                // Both passes use the identical per-pixel atlas mask. No lens/frame
                                // boundary is approximated by choosing whole triangles.
                                var hair=new List<int>();
                                bool IsHairBone(int bone)=>model.bones[bone].name.Contains("hair")||model.bones[bone].name.Contains("_side");
                                for(int t=0;t<triangles.Length;t+=3)
                                {
                                    var influences=triangles.Skip(t).Take(3).SelectMany(v=>source.vertices[v].weights).Where(w=>w.weight>.00001f).ToArray();
                                    if(!influences.Any(w=>IsHairBone(w.bone)))continue;
                                    if(influences.Any(w=>model.bones[w.bone].name.Contains("glasses")))throw new Exception("Hair outline overlaps glasses geometry");
                                    hair.AddRange(new[]{triangles[t],triangles[t+1],triangles[t+2]});
                                }
                                if(hair.Count==0)throw new Exception("Missing Disco hair outline geometry");
                                mesh.subMeshCount=3;mesh.SetTriangles(triangles,0);mesh.SetTriangles(triangles,1);mesh.SetTriangles(hair,2);
                            }
                            mesh.boneWeights = source.vertices.Select(v =>
                            {
                                var w = v.weights.OrderByDescending(a => a.weight).ToArray();
                                if (w.Length > 4) throw new Exception("More than four influences in " + skin);
                                var bw = new BoneWeight();
                                for (int n = 0; n < w.Length; n++)
                                {
                                    switch (n) {
                                        case 0: bw.boneIndex0=w[n].bone; bw.weight0=w[n].weight; break;
                                        case 1: bw.boneIndex1=w[n].bone; bw.weight1=w[n].weight; break;
                                        case 2: bw.boneIndex2=w[n].bone; bw.weight2=w[n].weight; break;
                                        case 3: bw.boneIndex3=w[n].bone; bw.weight3=w[n].weight; break;
                                    }
                                }
                                return bw;
                            }).ToArray();
                            mesh.bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
                            mesh.RecalculateBounds(); mesh = Save(mesh, Output + "Meshes/" + skin + "_" + s + ".asset");
                            var renderer = new GameObject(skin + "_Mesh_" + s).AddComponent<SkinnedMeshRenderer>();
                            renderer.transform.SetParent(root.transform, false); renderer.sharedMesh = mesh;
                            renderer.sharedMaterials = lenses?new[]{material,lenses,hairOutline}:new[]{material}; renderer.bones = bones; renderer.rootBone = bones[0];
                            renderer.localBounds = new Bounds(Vector3.zero, Vector3.one * 4);
                            renderer.updateWhenOffscreen = true; renderer.quality = SkinQuality.Bone4;
                        }
                        if(lensAtlas)UnityEngine.Object.DestroyImmediate(lensAtlas);
                        BuildClips(model, bones, root.transform, scale, visual, actor.GetComponent<Animation>(), prehistoric);
                        PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/TimeTravel/" + skin + ".prefab");
                        Debug.Log("Imported Time Travel " + skin);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(root); }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(geometry); }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        static readonly string[] Actions = {
            "idle","idle2","idle3","idle4","open","close_eat","close_sad","jump","turn_left","turn_right","tap",
            "touch_left_in","touch_right_in","touch_left","touch_right","touch_left_out","touch_right_out"
        };
        static bool IsBraceBone(string name)=>name.StartsWith("brace",StringComparison.Ordinal);
        static Key Sample(Clip clip, int bone, float time)
        {
            var keys=clip.tracks.First(t=>t.bone==bone).keys;
            time=Mathf.Clamp(time,0,clip.duration);
            int lo=0,hi=keys.Length-1;
            while(hi-lo>1) { int mid=(lo+hi)/2; if(keys[mid].time<=time)lo=mid; else hi=mid; }
            float t=Mathf.InverseLerp(keys[lo].time,keys[hi].time,time);
            return Mix(keys[lo],keys[hi],t);
        }
        static Key Mix(Key a,Key b,float t)
        {
            var p=Vector3.Lerp(new Vector3(a.position[0],a.position[1],a.position[2]),new Vector3(b.position[0],b.position[1],b.position[2]),t);
            var q=Quaternion.Slerp(new Quaternion(a.rotation[0],a.rotation[1],a.rotation[2],a.rotation[3]).normalized,
                new Quaternion(b.rotation[0],b.rotation[1],b.rotation[2],b.rotation[3]).normalized,t);
            return new Key { position=new[]{p.x,p.y,p.z},rotation=new[]{q.x,q.y,q.z,q.w} };
        }
        static void BuildClips(Model model, Transform[] bones, Transform root, float scale, TimeTravelVisual visual, Animation original, bool prehistoric)
        {
            Clip SourceClip(string name)=>model.animations.First(a=>a.name==name);
            var bindings=new List<TimeTravelVisual.ClipBinding>();
            var shared=new HashSet<Transform>(visual.attachments.Select(a=>a.bone));
            visual.glasses=model.name=="Disco"?bones.Where(t=>t.name.Contains("glasses")).ToArray():Array.Empty<Transform>();
            foreach(string action in Actions)
            {
                bool loop=action=="idle"||action=="touch_left"||action=="touch_right";
                float duration=original[action].length;
                Func<int,float,Key> pose;
                if(prehistoric)
                {
                    Clip idle=SourceClip("idle_loop"),petWave=SourceClip("greeting");
                    switch(action)
                    {
                        case "idle": duration=idle.duration; pose=(i,t)=>Sample(idle,i,t);break;
                        case "idle2": case "idle3": case "idle4":
                            var variation=SourceClip(action=="idle2"?"hungry":action=="idle3"?"puzzled":"greeting");
                            duration=variation.duration;pose=(i,t)=>Sample(variation,i,t);break;
                        case "open":
                            duration=.2f;var open=SourceClip("mouth_open");pose=(i,t)=>Sample(open,i,t/duration*open.duration);break;
                        case "close_eat":
                            duration=Resources.Load<GameAssets>("GameAssets").chew.length;
                            var chew=SourceClip("chew");
                            // Chew already starts with an open mouth. Preserve its initial close and
                            // first chew cycle at authored speed, then settle smoothly to idle.
                            pose=(i,t)=>t<duration-.15f?Sample(chew,i,t):Mix(Sample(chew,i,duration-.15f),Sample(idle,i,0),Mathf.SmoothStep(0,1,(t-duration+.15f)/.15f));break;
                        case "close_sad":
                            duration=Resources.Load<TimeTravelVoice>("TimeTravelAudio/Prehistoric").sad.length;
                            var close=SourceClip("mouth_close");var fail=SourceClip("fail");
                            pose=(i,t)=>t<.2f?Sample(close,i,t/.2f*close.duration):t<.3f?Mix(Sample(close,i,close.duration),Sample(fail,i,t-.2f),Mathf.SmoothStep(0,1,(t-.2f)/.1f)):t<.7f?Sample(fail,i,t-.2f):Mix(Sample(fail,i,fail.duration),Sample(idle,i,0),Mathf.SmoothStep(0,1,(t-.7f)/(duration-.7f)));break;
                        case "jump":
                            var jump=SourceClip("excited");pose=(i,t)=>t<duration-.1f?Sample(jump,i,t/duration*jump.duration):Mix(Sample(jump,i,(duration-.1f)/duration*jump.duration),Sample(idle,i,0),Mathf.SmoothStep(0,1,(t-duration+.1f)/.1f));break;
                        case "tap":
                            var tap=SourceClip("excited");duration=tap.duration;pose=(i,t)=>t<duration-.1f?Sample(tap,i,t):Mix(Sample(tap,i,duration-.1f),Sample(idle,i,0),Mathf.SmoothStep(0,1,(t-duration+.1f)/.1f));break;
                        case "turn_left": case "turn_right": pose=(i,t)=>Sample(idle,i,t);break;
                        case "touch_left": case "touch_right": duration=petWave.duration;pose=(i,t)=>Sample(petWave,i,t);break;
                        case "touch_left_in": case "touch_right_in":
                            duration=.2f;pose=(i,t)=>Mix(Sample(idle,i,0),Sample(petWave,i,0),Mathf.SmoothStep(0,1,t/duration));break;
                        default:
                            duration=.2f;pose=(i,t)=>Mix(Sample(petWave,i,0),Sample(idle,i,0),Mathf.SmoothStep(0,1,t/duration));break;
                    }
                }
                else
                {
                    string name=action=="idle"?"idle":action=="idle2"?"idle3":action=="idle3"?"idle2":action=="idle4"?"lick":
                        action=="open"?"mouth_open":action=="close_eat"?"mouth_close":action=="close_sad"?"fail":action=="jump"?"doublejump":
                        action=="tap"?"win":action.Contains("_in")||action.Contains("_out")?"idle_to_sleepR":action.StartsWith("touch")?"sleepR":"idle";
                    var source=SourceClip(name);
                    bool reverse=action.EndsWith("_out");
                    pose=(i,t)=>Sample(source,i,(reverse?1-t/duration:t/duration)*source.duration);
                }
                var clip=prehistoric?new AnimationClip {legacy=true}:UnityEngine.Object.Instantiate(original[action].clip);
                clip.name=model.name+"_"+action;clip.frameRate=60;
                for(int i=0;i<bones.Length;i++)
                {
                    if(!prehistoric&&shared.Contains(bones[i]))continue;
                    int count=Mathf.CeilToInt(duration*60);
                    var keys=new Key[count+1];
                    for(int n=0;n<=count;n++) { float time=n*duration/count;keys[n]=pose(i,time);keys[n].time=time; }
                    // Canonicalize every clip to the bind-pose quaternion hemisphere before
                    // continuity filtering. This keeps cross-fades from crossing q versus -q.
                    Quaternion rest=Q(model.bones[i].rotation);
                    Vector3 clearance=visual.glasses.Contains(bones[i])?bones[i].parent.InverseTransformVector(root.parent.forward*(.025f*root.parent.lossyScale.x)):Vector3.zero;
                    AddTrack(clip,"TimeTravelSkin/"+AnimationUtility.CalculateTransformPath(bones[i],root),keys,scale,0,rest,clearance);
                }
                clip.EnsureQuaternionContinuity();
                var saved=Save(clip,Output+"Animations/"+model.name+"_"+action+".anim");
                bindings.Add(new TimeTravelVisual.ClipBinding {action=action,clip=saved,loop=loop});
            }
            visual.clips=bindings.ToArray();
        }

        static void AddTrack(AnimationClip clip, string path, Key[] keys, float scale, float offset, Quaternion rest, Vector3 localOffset)
        {
            float[][] values = keys.Select(k => { Vector3 p=P(k.position,scale)+localOffset; Quaternion q=Q(k.rotation); return new[]{p.x,p.y,p.z,q.x,q.y,q.z,q.w}; }).ToArray();
            if(Quaternion.Dot(new Quaternion(values[0][3],values[0][4],values[0][5],values[0][6]),rest)<0)
                for(int c=3;c<7;c++) values[0][c]=-values[0][c];
            // Keep quaternion signs continuous before generating individual component curves.
            for (int i = 1; i < values.Length; i++)
                if (Enumerable.Range(3,4).Sum(c => values[i-1][c]*values[i][c]) < 0)
                    for (int c = 3; c < 7; c++) values[i][c] = -values[i][c];
            string[] properties = { "localPosition.x", "localPosition.y", "localPosition.z", "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
            for (int c = 0; c < 7; c++)
            {
                bool constant = values.All(v => Mathf.Abs(v[c]-values[0][c]) < .000001f);
                var frames = Enumerable.Range(0, keys.Length).Where(i => !constant || i == 0 || i == keys.Length-1)
                    .Select(i => new Keyframe(keys[i].time+offset, values[i][c])).ToArray();
                var curve = new AnimationCurve(frames);
                for (int i = 0; i < curve.length; i++)
                { AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear); }
                clip.SetCurve(path, typeof(Transform), properties[c], curve);
            }
        }
    }
}
