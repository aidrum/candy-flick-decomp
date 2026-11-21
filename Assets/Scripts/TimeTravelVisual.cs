using System;
using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    // Bind-space corrections accommodate the different FBX and Cal3D bone axes.
    // All matching face/body bones follow the recovered Candy Flick animation.
    [DefaultExecutionOrder(100)]
    public sealed class TimeTravelVisual : MonoBehaviour
    {
        [Serializable] public struct Attachment
        {
            public Transform bone;
            public string originalBone;
            public Matrix4x4 correction;
        }
        [Serializable] public struct ClipBinding
        {
            public string action;
            public AnimationClip clip;
            public bool loop;
        }
        public Attachment[] attachments = Array.Empty<Attachment>();
        public ClipBinding[] clips = Array.Empty<ClipBinding>();
        public bool directSkinning;
        public Transform[] glasses = Array.Empty<Transform>();
        public int[] lensVertices = Array.Empty<int>();
        public float LensClearance { get; private set; }
        Transform[] drivers;
        TimeTravelSkins owner;
        SkinnedMeshRenderer body, costume;
        Transform[] bodyBones,costumeBones;
        int[] eyeVertices;
        Vector3[] bodyPoints,costumePoints;
        BoneWeight[] bodyWeights,costumeWeights;
        Matrix4x4[] bodyBind,costumeBind,bodyPose,costumePose;

        public void Bind(Transform actor)
        {
            owner=actor.GetComponent<TimeTravelSkins>();
            var bones = actor.GetComponentsInChildren<Transform>(true)
                .Where(t => !t.IsChildOf(transform)).ToDictionary(t => t.name);
            drivers = attachments.Select(a => bones[a.originalBone]).ToArray();
            if(directSkinning)
            {
                var mapped=attachments.Select((a,i)=>(a.bone,driver:drivers[i])).ToDictionary(p=>p.bone,p=>p.driver);
                foreach(var renderer in GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    renderer.bones=renderer.bones.Select(b=>mapped.TryGetValue(b,out var driver)?driver:actor).ToArray();
                    renderer.rootBone=actor;
                }
                GetComponent<OmNelleEyeSurface>()?.Initialize(actor);
                return;
            }
            if(glasses.Length>0)
            {
                body=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r=>r.name=="OmNom_Mesh");
                costume=GetComponentInChildren<SkinnedMeshRenderer>();
                bodyBones=body.bones;costumeBones=costume.bones;
                bodyPoints=body.sharedMesh.vertices;bodyWeights=body.sharedMesh.boneWeights;bodyBind=body.sharedMesh.bindposes;
                costumePoints=costume.sharedMesh.vertices;costumeWeights=costume.sharedMesh.boneWeights;costumeBind=costume.sharedMesh.bindposes;
                eyeVertices=body.sharedMesh.uv.Select((uv,i)=>(uv,i)).Where(v=>v.uv.x>.5f&&v.uv.x<.82f&&v.uv.y>.73f).Select(v=>v.i).ToArray();
                bodyPose=new Matrix4x4[bodyBind.Length];costumePose=new Matrix4x4[costumeBind.Length];
            }
            Follow();
        }
        void LateUpdate() => Follow();
        void Follow()
        {
            if(owner)owner.ConstrainAntenna();
            if (drivers == null || directSkinning) return;
            // Attachments are stored parent first, so intermediate mapped bones are stable.
            for (int i = 0; i < attachments.Length; i++)
            {
                var a = attachments[i];
                Matrix4x4 pose = a.bone.parent.worldToLocalMatrix * drivers[i].localToWorldMatrix * a.correction;
                a.bone.localPosition = pose.GetColumn(3);
                a.bone.localRotation = pose.rotation;
                a.bone.localScale = pose.lossyScale;
            }
            KeepGlassesInFrontOfEyes();
        }
        void KeepGlassesInFrontOfEyes()
        {
            if(!body||lensVertices.Length==0||eyeVertices.Length==0)return;
            Matrix4x4 worldToActor=transform.parent.worldToLocalMatrix;
            for(int i=0;i<bodyPose.Length;i++)bodyPose[i]=worldToActor*bodyBones[i].localToWorldMatrix*bodyBind[i];
            for(int i=0;i<costumePose.Length;i++)costumePose[i]=worldToActor*costumeBones[i].localToWorldMatrix*costumeBind[i];
            int influences=body.quality==SkinQuality.Bone1?1:body.quality==SkinQuality.Bone2?2:
                body.quality==SkinQuality.Auto&&(QualitySettings.skinWeights==SkinWeights.OneBone||QualitySettings.skinWeights==SkinWeights.TwoBones)?(int)QualitySettings.skinWeights:4;
            float eye=float.NegativeInfinity,lens=float.PositiveInfinity;
            foreach(int i in eyeVertices)eye=Mathf.Max(eye,Skin(bodyPoints[i],bodyWeights[i],bodyPose,influences).z);
            foreach(int i in lensVertices)lens=Mathf.Min(lens,Skin(costumePoints[i],costumeWeights[i],costumePose,4).z);
            float shift=Mathf.Max(0,eye+.008f-lens);
            Vector3 offset=transform.parent.TransformVector(Vector3.forward*shift);
            foreach(var bone in glasses)bone.position+=offset;
            LensClearance=lens+shift-eye;
        }
        static Vector3 Skin(Vector3 vertex,BoneWeight w,Matrix4x4[] pose,int influences)
        {
            Vector3 result=pose[w.boneIndex0].MultiplyPoint3x4(vertex)*w.weight0;float total=w.weight0;
            if(influences>1){result+=pose[w.boneIndex1].MultiplyPoint3x4(vertex)*w.weight1;total+=w.weight1;}
            if(influences>2){result+=pose[w.boneIndex2].MultiplyPoint3x4(vertex)*w.weight2+pose[w.boneIndex3].MultiplyPoint3x4(vertex)*w.weight3;total+=w.weight2+w.weight3;}
            return result/Mathf.Max(total,.0001f);
        }
    }
}
