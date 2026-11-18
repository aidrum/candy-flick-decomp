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
            Check(visual.clips.Length==0,"must retain the original game's animations");
            ValidateSavedChoices(game);
            var player=game.Actor.GetComponent<Animation>();
            Check(!AnimationUtility.GetCurveBindings(player["open"].clip).Any(b=>b.path.StartsWith("TimeTravelSkin/")),"must use native mouth animation");
            Check(visual.attachments.Length>=65,"body/face retarget is incomplete");
            var lashes=visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="Eyelashes.001");
            Check(lashes.bones.Length==2&&lashes.bones.All(t=>t.name.Contains("Bn_FullEye")),"both eyelashes must be skinned to their own eye");
            Check(lashes.sharedMesh.boneWeights.All(w=>w.weight0==1)&&lashes.sharedMesh.boneWeights.Select(w=>w.boneIndex0).Distinct().Count()==2,"eyelashes must follow both eyes");
            var lids=visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="eyelid_mesh_0");
            var whites=visual.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="omnom_mesh_2");
            Check(lids.bones.SequenceEqual(whites.bones),"lids must use the exact eye-white joints, without independent lid controls");
            var lidWeights=lids.sharedMesh.boneWeights;var lidPoints=lids.sharedMesh.vertices;
            var deformingBones=lidWeights.SelectMany(w=>new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)}).Where(p=>p.Item2>0).Select(p=>lids.bones[p.Item1]).Distinct().ToArray();
            Check(lidWeights.All(w=>Mathf.Abs(w.weight0+w.weight1+w.weight2+w.weight3-1)<.00001f),"lids need normalized eye weights and a continuous blend across the bridge");
            float minimumClearance=ValidateEyeClearance(lids,whites,game.Actor.transform);
            int[] whiteIndices=lids.bones.Select(t=>Array.IndexOf(whites.bones,t)).ToArray();
            Matrix4x4 restToWhites=whites.transform.worldToLocalMatrix*lids.transform.localToWorldMatrix;
            float maximumError=0,maximumSquashRatio=1;
            foreach(string action in new[]{"idle","idle2","idle3","idle4","open","close_eat","close_sad","jump","tap","touch_left","touch_right"})
            {
                for(int i=0;i<=10;i++)
                {
                    player.Stop();player.Play(action);player[action].time=player[action].length*i/10;player.Sample();visual.SendMessage("LateUpdate");
                    var localToActor=game.Actor.transform.worldToLocalMatrix;
                    var actual=lids.bones.Select((bone,b)=>localToActor*bone.localToWorldMatrix*lids.sharedMesh.bindposes[b]).ToArray();
                    var expected=whiteIndices.Select(b=>localToActor*whites.bones[b].localToWorldMatrix*whites.sharedMesh.bindposes[b]*restToWhites).ToArray();
                    for(int v=0;v<lidPoints.Length;v++)
                    {
                        var w=lidWeights[v];var point=lidPoints[v];
                        Vector3 deformed=Skin(point,w,actual);
                        Vector3 onEyes=Skin(point,w,expected);
                        maximumError=Mathf.Max(maximumError,Vector3.Distance(deformed,onEyes));
                    }
                    foreach(var eye in deformingBones){Vector3 scale=eye.localScale;maximumSquashRatio=Mathf.Max(maximumSquashRatio,Mathf.Max(scale.x,Mathf.Max(scale.y,scale.z))/Mathf.Max(.00001f,Mathf.Min(scale.x,Mathf.Min(scale.y,scale.z))));}
                    Check(maximumError<.00001f,"eyelids must stretch/squash with the whites: "+action+" sample "+i+", error "+maximumError);
                }
                player[action].time=player[action].length*.5f;player.Sample();visual.SendMessage("LateUpdate");capture("OmNelle-"+action);
                TimeTravelSkinValidation.CaptureAtAngle("OmNelle-"+action+"-side",55);
            }
            File.WriteAllText("Documentation/omnelle-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),bindings=visual.attachments.Length,
                maximumEyeDeformationError=maximumError,maximumEyeSquashRatio=maximumSquashRatio,
                minimumEyelidClearance=minimumClearance,
                result="Native animation clips; 121 sampled poses with identical eye/lid affine deformation; front and side captures; eye-skinned eyelashes; common gameplay hitbox checked by SkinsTest"},true));
        }
        static Vector3 Skin(Vector3 point,BoneWeight w,Matrix4x4[] pose)=>pose[w.boneIndex0].MultiplyPoint3x4(point)*w.weight0+pose[w.boneIndex1].MultiplyPoint3x4(point)*w.weight1+pose[w.boneIndex2].MultiplyPoint3x4(point)*w.weight2+pose[w.boneIndex3].MultiplyPoint3x4(point)*w.weight3;
        static float ValidateEyeClearance(SkinnedMeshRenderer lids,SkinnedMeshRenderer whites,Transform actor)
        {
            var surface=OmNelleAssetsBuilder.EyeSurface(whites,actor);
            var points=lids.sharedMesh.vertices;var weights=lids.sharedMesh.boneWeights;var triangles=lids.sharedMesh.triangles;
            Matrix4x4 toActor=actor.worldToLocalMatrix*lids.transform.localToWorldMatrix;
            float minimum=float.PositiveInfinity;int samples=0;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                Vector3 pa=toActor.MultiplyPoint3x4(points[a]),pb=toActor.MultiplyPoint3x4(points[b]),pc=toActor.MultiplyPoint3x4(points[c]);
                foreach(Vector3 point in new[]{pa,pb,pc,(pa+pb+pc)/3,(pa+pb)*.5f,(pb+pc)*.5f,(pc+pa)*.5f})
                {
                    Vector3 nearest=OmNelleAssetsBuilder.NearestEyeSurface(point,-1,surface,out Vector3 normal);
                    minimum=Mathf.Min(minimum,Vector3.Dot(point-nearest,normal));samples++;
                }
            }
            if(samples<100||minimum<-.0001f)throw new Exception("Om Nelle eyelid surface intersects the whites: minimum clearance "+minimum+", samples "+samples);
            return minimum;
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
        [Serializable]class Evidence{public string utc,result;public int bindings;public float maximumEyeDeformationError,maximumEyeSquashRatio,minimumEyelidClearance;}
    }
}
