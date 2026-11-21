using System;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class OmNelleValidation
    {
        public static void Validate(CandyFlickGame game,Action<string> capture)
        {
            var visual=game.Skins.Visual.GetComponent<TimeTravelVisual>();
            void Check(bool value,string message){if(!value)throw new Exception("Om Nelle: "+message);}
            Check(visual.clips.Length==0,"must retain the original game's animations");ValidateSavedChoices(game);
            var player=game.Actor.GetComponent<Animation>();
            Check(!AnimationUtility.GetCurveBindings(player["open"].clip).Any(b=>b.path.StartsWith("TimeTravelSkin/")),"must use native mouth animation");
            Check(visual.attachments.Length>=65,"body/face retarget is incomplete");
            Check(visual.directSkinning,"replacement must preserve the native joints' full affine deformation");
            var renderers=visual.GetComponentsInChildren<SkinnedMeshRenderer>();
            Check(renderers.Length==3,"must use the replacement model's three skinned meshes");
            var face=renderers.Single(r=>r.name=="eyelid_mesh_0.001");var pupils=renderers.Single(r=>r.name=="omnom_mesh_0");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/OmNelleSource/OmNelle.fbx");
            var originalFace=source.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name==face.name);
            var originalPupils=source.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name==pupils.name);
            Check(face.sharedMesh.boneWeights.SequenceEqual(originalFace.sharedMesh.boneWeights),"must preserve MlopsFunny's authored face/lid/lash skin weights");
            var faceVertices=face.sharedMesh.vertices;var originalFaceVertices=originalFace.sharedMesh.vertices;
            Matrix4x4 faceToActor=game.Actor.transform.worldToLocalMatrix*face.transform.localToWorldMatrix;int capVertices=0;
            for(int i=0;i<faceVertices.Length;i++)
            {
                float offset=faceToActor.MultiplyVector(faceVertices[i]-originalFaceVertices[i]).magnitude;
                if(offset<.00001f)continue;
                Check(Mathf.Abs(offset-.0015f)<.00001f,"only the tiny eyelid shell clearance may change authored face geometry");capVertices++;
            }
            Check(capVertices>150&&capVertices<300,"both eyelid caps have clearance while the rest of the replacement face remains unchanged");
            Check(face.bones.Any(t=>t.name.Contains("FullEye"))&&face.bones.Any(t=>t.name.Contains("Mouth"))&&face.bones.All(t=>!t.IsChildOf(visual.transform)),"authored face binds to this actor's native eye and mouth joints");
            Check(renderers.All(r=>r.sharedMaterial.mainTexture==Resources.Load<GameObject>("TimeTravel/OmNelle").GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial.mainTexture),"all parts use the replacement model's supplied atlas");
            var vertices=pupils.sharedMesh.vertices;var old=originalPupils.sharedMesh.vertices;var weights=pupils.sharedMesh.boneWeights;
            Matrix4x4 toActor=game.Actor.transform.worldToLocalMatrix*pupils.transform.localToWorldMatrix;int lowered=0;
            for(int i=0;i<vertices.Length;i++)
            {
                string bone=pupils.bones[weights[i].boneIndex0].name;
                if(bone!="Bn_L_Eye"&&bone!="Bn_R_Eye")continue;
                Check(Mathf.Abs(toActor.MultiplyVector(vertices[i]-old[i]).y+.035f)<.00001f,"pupil vertex keeps the lower gaze: "+i);lowered++;
            }
            Check(lowered==242,"both pupils retain the lower position");
            var surface=visual.GetComponent<OmNelleEyeSurface>();
            Check(surface&&surface.pupilSourceIndices.Length==242,"pupils and eyelids must follow the animated white-eye surface");
            Check(surface.authoredPupils.SequenceEqual(surface.pupilSourceIndices.Select(i=>old[i])),"animated gaze starts with the original model's pupil geometry and alignment");
            surface.Refresh();
            float alignmentError=AlignmentError(surface,game.Actor.transform,old);
            Check(alignmentError<.0001f,"idle pupils retain the original animated model's horizontal alignment and shape, offset down by 0.035: "+alignmentError);
            var probe=new EyeProbe(surface,game.Actor.transform);int poseSamples=0;float minimumPupil=float.PositiveInfinity,minimumLid=float.PositiveInfinity;
            foreach(string action in new[]{"idle","idle2","idle3","idle4","open","close_eat","close_sad","jump","tap","turn_left","turn_right","touch_left_in","touch_left","touch_left_out","touch_right_in","touch_right","touch_right_out"})
            {
                int samples=action=="jump"||action=="close_sad"?Mathf.Max(10,Mathf.CeilToInt(player[action].length*60)):10;
                for(int i=0;i<=samples;i++)
                {
                    player.Stop();player.Play(action);player[action].time=player[action].length*i/samples;player.Sample();visual.SendMessage("LateUpdate");surface.Refresh();
                    var baked=new Mesh();face.BakeMesh(baked,false);
                    Check(baked.vertices.All(p=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z)),"replacement face deforms correctly in "+action+" sample "+i);
                    UnityEngine.Object.DestroyImmediate(baked);
                    probe.Measure(out float pupilGap,out float lidGap);poseSamples++;
                    minimumPupil=Mathf.Min(minimumPupil,pupilGap);minimumLid=Mathf.Min(minimumLid,lidGap);
                    if(pupilGap<-.0002f||lidGap<-.0003f)
                    {
                        capture("OmNelle-penetration-"+action+"-"+i);TimeTravelSkinValidation.CaptureAtAngle("OmNelle-penetration-"+action+"-"+i+"-side",55);
                        throw new Exception("Eye surface penetration in "+action+" sample "+i+"/"+samples+": pupils="+pupilGap+", lids="+lidGap);
                    }
                }
                player[action].time=player[action].length*.5f;player.Sample();visual.SendMessage("LateUpdate");surface.Refresh();capture("OmNelle-"+action);
                TimeTravelSkinValidation.CaptureAtAngle("OmNelle-"+action+"-side",55);
            }
            File.WriteAllText("Documentation/omnelle-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),bindings=visual.attachments.Length,
                poseSamples=poseSamples,minimumPupilClearance=minimumPupil,minimumLidClearance=minimumLid,
                pupilAlignmentError=alignmentError,
                result="Native clips with dynamic eye-surface attachment; rendered pupils and eyelids measured against animated whites in all actions; jump and sadness sampled at 60 Hz; lower gaze and shared hitboxes preserved"},true));
        }
        static float AlignmentError(OmNelleEyeSurface surface,Transform actor,Vector3[] authored)
        {
            var mesh=surface.gaze.sharedMesh;var bind=mesh.bindposes;var bones=surface.gaze.bones;var weights=mesh.boneWeights;
            var pose=bones.Select((bone,i)=>actor.worldToLocalMatrix*bone.localToWorldMatrix*bind[i]).ToArray();
            var vertices=surface.pupils.sharedMesh.vertices;Matrix4x4 toActor=actor.worldToLocalMatrix*surface.pupils.transform.localToWorldMatrix;
            float error=0;
            for(int i=0;i<vertices.Length;i++)
            {
                int source=surface.pupilSourceIndices[i];var w=weights[source];Vector3 p=authored[source];
                Vector3 expected=pose[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+pose[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+pose[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+pose[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
                expected.y-=.035f;Vector3 actual=toActor.MultiplyPoint3x4(vertices[i]);
                error=Mathf.Max(error,Vector2.Distance(new Vector2(expected.x,expected.y),new Vector2(actual.x,actual.y)));
            }
            return error;
        }
        sealed class EyeProbe
        {
            readonly OmNelleEyeSurface surface;readonly Transform actor;readonly Vector3[] rest,normals,points;readonly BoneWeight[] weights;
            readonly Matrix4x4[] bind,pose;readonly Transform[] bones;readonly int[] indices,lidIndices;readonly float[] signs;
            public EyeProbe(OmNelleEyeSurface value,Transform owner)
            {
                surface=value;actor=owner;rest=value.whites.sharedMesh.vertices;normals=value.whites.sharedMesh.normals;weights=value.whites.sharedMesh.boneWeights;
                bind=value.whites.sharedMesh.bindposes;bones=value.whites.bones;pose=new Matrix4x4[bind.Length];points=new Vector3[rest.Length];indices=value.whiteTriangles;
                lidIndices=value.lids.sharedMesh.triangles;signs=new float[indices.Length/3];
                Matrix4x4 toActor=actor.worldToLocalMatrix*value.whites.transform.localToWorldMatrix,normalPose=toActor.inverse.transpose;
                for(int i=0;i<rest.Length;i++)points[i]=toActor.MultiplyPoint3x4(rest[i]);
                for(int t=0;t<signs.Length;t++){int a=indices[t*3],b=indices[t*3+1],c=indices[t*3+2];signs[t]=Vector3.Dot(Vector3.Cross(points[b]-points[a],points[c]-points[a]),normalPose.MultiplyVector(normals[a]+normals[b]+normals[c]))<0?-1:1;}
            }
            public void Measure(out float pupilGap,out float lidGap)
            {
                Matrix4x4 worldToActor=actor.worldToLocalMatrix;
                for(int i=0;i<pose.Length;i++)pose[i]=worldToActor*bones[i].localToWorldMatrix*bind[i];
                for(int i=0;i<rest.Length;i++){var w=weights[i];points[i]=pose[w.boneIndex0].MultiplyPoint3x4(rest[i])*w.weight0+pose[w.boneIndex1].MultiplyPoint3x4(rest[i])*w.weight1+pose[w.boneIndex2].MultiplyPoint3x4(rest[i])*w.weight2+pose[w.boneIndex3].MultiplyPoint3x4(rest[i])*w.weight3;}
                pupilGap=float.PositiveInfinity;lidGap=float.PositiveInfinity;
                var pupilVertices=surface.pupils.sharedMesh.vertices;Matrix4x4 pupilPose=worldToActor*surface.pupils.transform.localToWorldMatrix;
                foreach(var vertex in pupilVertices)
                {
                    Vector3 p=pupilPose.MultiplyPoint3x4(vertex);float front=float.NegativeInfinity;
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        Vector3 a=points[indices[t]],b=points[indices[t+1]],c=points[indices[t+2]];
                        float d=(b.y-c.y)*(a.x-c.x)+(c.x-b.x)*(a.y-c.y);if(Mathf.Abs(d)<1e-10f)continue;
                        float u=((b.y-c.y)*(p.x-c.x)+(c.x-b.x)*(p.y-c.y))/d,v=((c.y-a.y)*(p.x-c.x)+(a.x-c.x)*(p.y-c.y))/d;
                        if(u>=0&&v>=0&&u+v<=1)front=Mathf.Max(front,a.z*u+b.z*v+c.z*(1-u-v));
                    }
                    if(float.IsFinite(front))pupilGap=Mathf.Min(pupilGap,p.z-front);
                }
                var lids=surface.lids.sharedMesh.vertices;Matrix4x4 lidPose=worldToActor*surface.lids.transform.localToWorldMatrix;
                float MeasurePoint(Vector3 p)
                {
                    float closest=float.PositiveInfinity,gap=float.PositiveInfinity;
                    for(int t=0;t<indices.Length;t+=3)
                    {
                        Vector3 a=points[indices[t]],b=points[indices[t+1]],c=points[indices[t+2]],hit=OmNelleEyeSurface.ClosestPoint(p,a,b,c);float squared=(p-hit).sqrMagnitude;
                        if(squared>=closest)continue;closest=squared;gap=Vector3.Dot(p-hit,Vector3.Cross(b-a,c-a).normalized*signs[t/3]);
                    }
                    return gap;
                }
                for(int i=0;i<lids.Length;i+=8)lidGap=Mathf.Min(lidGap,MeasurePoint(lidPose.MultiplyPoint3x4(lids[i])));
                for(int i=0;i<lidIndices.Length;i+=51)lidGap=Mathf.Min(lidGap,MeasurePoint(lidPose.MultiplyPoint3x4((lids[lidIndices[i]]+lids[lidIndices[i+1]]+lids[lidIndices[i+2]])/3)));
            }
        }
        public static void CheckAudio(CandyFlickGame game)
        {
            var frames=new[]{9872,6729,0,55265,30075,24493};
            foreach(NommieSound cue in Enum.GetValues(typeof(NommieSound)))
            {
                var clip=game.Skins.Sound(cue);
                if(cue==NommieSound.Chew){if(clip!=game.assets.chew)throw new Exception("Om Nelle must share Om Nom chewing");continue;}
                string path="Assets/OmNelle/Audio/OmNelle_"+cue+".wav";
                if(!clip||AssetDatabase.GetAssetPath(clip)!=path)throw new Exception("Missing Om Nelle cue "+cue);
                if(clip.samples!=frames[(int)cue]||clip.frequency!=48000||clip.channels!=1)throw new Exception("Altered source audio timing "+cue);
                var settings=((AudioImporter)AssetImporter.GetAtPath(path)).GetOverrideSampleSettings("Android");
                if(settings.compressionFormat!=AudioCompressionFormat.PCM)throw new Exception("Om Nelle audio must preserve PCM timing");
                clip.LoadAudioData();var samples=new float[clip.samples*clip.channels];
                if(!clip.GetData(samples,0)||!samples.Any(v=>Mathf.Abs(v)>.001f))throw new Exception("Silent Om Nelle cue "+cue);
                if(cue==NommieSound.Sad&&!samples.Take(28985).All(v=>v==0))throw new Exception("Om Nelle sadness must wait for the mouth-closing animation");
            }
        }
        static void ValidateSavedChoices(CandyFlickGame game)
        {
            const string version="PortNommieSkinVersion";
            int saved=PlayerPrefs.GetInt(TimeTravelSkins.Preference),savedVersion=PlayerPrefs.GetInt(version);
            bool had=PlayerPrefs.HasKey(TimeTravelSkins.Preference),hadVersion=PlayerPrefs.HasKey(version);
            GameObject first=null,second=null;
            try
            {
                PlayerPrefs.DeleteKey(version);PlayerPrefs.SetInt(TimeTravelSkins.Preference,8);
                first=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));
                var skins=first.transform.Find("OmNom@All-1").gameObject.AddComponent<TimeTravelSkins>();skins.Initialize(game.assets);
                if(!skins.RandomSelected||PlayerPrefs.GetInt(TimeTravelSkins.Preference)!=TimeTravelSkins.SavedRandomChoice)throw new Exception("Existing Random selection must survive adding Om Nelle");
                skins.Select(NommieSkin.OmNelle);
                second=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));
                var reload=second.transform.Find("OmNom@All-1").gameObject.AddComponent<TimeTravelSkins>();reload.Initialize(game.assets);
                if(reload.RandomSelected||reload.Selected!=NommieSkin.OmNelle)throw new Exception("New Om Nelle selection must survive reload");
            }
            finally
            {
                if(first)UnityEngine.Object.DestroyImmediate(first);if(second)UnityEngine.Object.DestroyImmediate(second);
                if(had)PlayerPrefs.SetInt(TimeTravelSkins.Preference,saved);else PlayerPrefs.DeleteKey(TimeTravelSkins.Preference);
                if(hadVersion)PlayerPrefs.SetInt(version,savedVersion);else PlayerPrefs.DeleteKey(version);PlayerPrefs.Save();
            }
        }
        [Serializable]class Evidence{public string utc,result;public int bindings,poseSamples;public float minimumPupilClearance,minimumLidClearance,pupilAlignmentError;}
    }
}
