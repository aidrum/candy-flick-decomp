using System;
using System.Linq;
using UnityEngine;
namespace CandyFlick
{
    // The fork is fixed to the player frame. Only the recovered elastic's
    // candy-side bones follow the pull; this model never follows the candy.
    public sealed class SlingshotVisual : MonoBehaviour
    {
        [Serializable] public sealed class Layout {public Vector3 left,right,mountLeft,mountRight,releaseLeft,releaseRight,handle;}
        public const float Size=.55f,LanSize=.25f;
        static Layout layout;
        CandyFlickGame game;Transform frame;GameObject model;bool remote,reduced,fitted,fittedLan;Vector3 leftMount,rightMount;
        SkinnedMeshRenderer band;Mesh originalMesh;Transform[] fixedBones;Vector3[] originalLocal,originalFrame;
        Transform released;Vector3 releasePosition,releaseScale;Quaternion releaseRotation;bool remoteLoaded=true;
        public void SetRemoteLoaded(bool loaded){remoteLoaded=loaded;}
        
        public Transform Model=>model?model.transform:null;
        public static Layout Attachments=>layout??(layout=JsonUtility.FromJson<Layout>(Resources.Load<TextAsset>("Slingshot/layout").text));
        public void Initialize(CandyFlickGame owner,Transform player,bool isRemote)
        {
            game=owner;frame=player;remote=isRemote;
            band=GetComponentInChildren<SkinnedMeshRenderer>();originalMesh=band.sharedMesh;
            released=frame.Find("test");releasePosition=released.localPosition;releaseRotation=released.localRotation;releaseScale=released.localScale;
            var ghost=frame.Find("CandyGhost");fixedBones=band.bones.Where(b=>!b.IsChildOf(ghost)).Distinct().ToArray();
            originalLocal=fixedBones.Select(b=>b.localPosition).ToArray();originalFrame=fixedBones.Select(b=>frame.InverseTransformPoint(b.position)).ToArray();
            model=Instantiate(Resources.Load<GameObject>("Slingshot/Slingshot"),transform,false);model.name="Slingshot support";
            foreach(Transform child in model.GetComponentsInChildren<Transform>(true))child.gameObject.layer=remote?0:13;
            model.SetActive(false);Refresh();
        }
        void FitFrame(bool lan)
        {
            if(fitted&&fittedLan==lan)return;fitted=true;fittedLan=lan;
            Vector3 center=(Attachments.left+Attachments.right)/2;
            if(lan)
            {
                Vector3 rest=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Player/CandySlingshotSlot").localPosition;
                center=rest+new Vector3(0,18,30);
                float half=Vector3.Distance(Attachments.left,Attachments.right)*LanSize/2;
                leftMount=center-Vector3.right*half;rightMount=center+Vector3.right*half;
            }
            else
            {
                Vector3 rest=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Player/CandySlingshotSlot").localPosition;
                float forkWidth=Vector3.Distance(Attachments.left,Attachments.right)*Size;
                // Center the actual grip beneath the resting candy, rather than
                // inheriting the asymmetric ends of the original elastic mesh.
                center.x=rest.x-Attachments.handle.x*forkWidth;center.z+=2;
                leftMount=center-Vector3.right*forkWidth/2;rightMount=center+Vector3.right*forkWidth/2;
            }
            Vector3 a=frame.TransformPoint(leftMount),b=frame.TransformPoint(rightMount),right=(b-a).normalized;
            Vector3 up=Vector3.ProjectOnPlane(frame.up,right).normalized,forward=Vector3.Cross(right,up).normalized;
            model.transform.SetPositionAndRotation((a+b)/2,Quaternion.LookRotation(forward,up));
            float width=Vector3.Distance(a,b);Vector3 scale=transform.lossyScale;
            model.transform.localScale=new Vector3(width/Mathf.Abs(scale.x),width/Mathf.Abs(scale.y),width/Mathf.Abs(scale.z));
        }
        public bool EndsVisible(Camera camera)
        {
            foreach(Vector3 point in new[]{Attachments.left,Attachments.right})
            {
                Vector3 v=camera.WorldToViewportPoint(frame.TransformPoint(point));
                if(v.z>camera.nearClipPlane&&v.x>-.01f&&v.x<1.01f&&v.y>-.01f&&v.y<1.01f)return true;
            }
            return false;
        }
        void Refresh()
        {
            FitFrame(remote||(game&&game.IsLanMatch));
            bool show=remote||(game&&game.CanSimulate&&game.ControlMode==LaunchMode.Slingshot&&EndsVisible(game.CandyCamera));model.SetActive(show);
            band.enabled=remote?remoteLoaded:game&&game.Ready;
            // Fit the complete native recoil rig to the same fork mounts. Its
            // animation changes child bones only, leaving these endpoints fixed.
            if(show)
            {
                Vector3 source=Attachments.releaseRight-Attachments.releaseLeft;
                float ratio=Vector3.Distance(leftMount,rightMount)/source.magnitude;
                Quaternion rotation=Quaternion.FromToRotation(source,rightMount-leftMount);
                released.localPosition=(leftMount+rightMount)/2+rotation*(releasePosition-(Attachments.releaseLeft+Attachments.releaseRight)/2)*ratio;
                released.localRotation=rotation*releaseRotation;released.localScale=releaseScale*ratio;
            }
            else {released.localPosition=releasePosition;released.localRotation=releaseRotation;released.localScale=releaseScale;}
            if(show!=reduced)
            {
                reduced=show;band.sharedMesh=show?Resources.Load<Mesh>("Slingshot/ElasticMoving"):originalMesh;
                if(!show){for(int i=0;i<fixedBones.Length;i++)fixedBones[i].localPosition=originalLocal[i];}
            }
            if(!show)return;
            Vector3 center=(Attachments.left+Attachments.right)/2;
            Vector3 left=leftMount,right=rightMount;
            for(int i=0;i<fixedBones.Length;i++)fixedBones[i].position=frame.TransformPoint(originalFrame[i]+(originalFrame[i].x<center.x?left-Attachments.mountLeft:right-Attachments.mountRight));
        }
        void LateUpdate(){if(model)Refresh();}
        void OnDestroy()
        {
            if(band)band.sharedMesh=originalMesh;
            if(fixedBones!=null)for(int i=0;i<fixedBones.Length;i++)if(fixedBones[i])fixedBones[i].localPosition=originalLocal[i];
        }
    }
}
