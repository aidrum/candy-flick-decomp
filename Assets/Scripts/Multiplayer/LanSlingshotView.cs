using System.Linq;
using UnityEngine;
namespace CandyFlick
{
    // The complete original bone relationship is cloned so the native elastic
    // deforms with the remote candy, without copying any cameras or input logic.
    public sealed class LanSlingshotView : MonoBehaviour
    {
        public int Owner;public Transform Frame {get;private set;}public Transform CandyBoneFrame {get;private set;}
        public SlingshotVisual Support {get;private set;}
        bool posed;Transform released;Animation recoil;
        public void Initialize(int owner)
        {
            Owner=owner;
            var source=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Player");
            Frame=Instantiate(source.gameObject,transform,false).transform;Frame.name="Remote launcher frame";Frame.localPosition=Vector3.zero;Frame.localRotation=Quaternion.identity;
            CandyBoneFrame=Frame.Find("CandyGhost");Transform elastic=Frame.Find("elastic");released=Frame.Find("test");recoil=released.GetComponent<Animation>();
            foreach(Transform child in Frame)if(child!=elastic&&child!=CandyBoneFrame&&child!=released){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(Transform child in CandyBoneFrame)if(!child.name.StartsWith("CBone")){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            foreach(var script in Frame.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
            foreach(var camera in Frame.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var collider in Frame.GetComponentsInChildren<Collider>(true))collider.enabled=false;
            foreach(var renderer in CandyBoneFrame.GetComponentsInChildren<Renderer>(true))renderer.enabled=false;
            foreach(Transform child in Frame.GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
            foreach(var renderer in elastic.GetComponentsInChildren<SkinnedMeshRenderer>(true)){renderer.enabled=true;renderer.forceRenderingOff=false;renderer.updateWhenOffscreen=true;}
            Support=elastic.gameObject.AddComponent<SlingshotVisual>();Support.Initialize(null,Frame,true);
        }
        public void SetPose(Vector3 position,Quaternion rotation,Transform candy,float releaseAge)
        {
            float blend=posed?Mathf.Clamp01(Time.unscaledDeltaTime*25):1;posed=true;
            Frame.SetPositionAndRotation(Vector3.Lerp(Frame.position,position,blend),Quaternion.Slerp(Frame.rotation,rotation,blend));
            bool loaded=candy;Support.SetRemoteLoaded(loaded);released.gameObject.SetActive(!loaded);
            if(loaded)CandyBoneFrame.SetPositionAndRotation(candy.position,candy.rotation);
            else recoil.clip.SampleAnimation(released.gameObject,Mathf.Clamp(releaseAge,0,recoil.clip.length));
        }
    }
}
