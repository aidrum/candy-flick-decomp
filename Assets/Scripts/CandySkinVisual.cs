using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    // The original bodies are scaled (50,4.5,50), with a flattened candy mesh.
    // Cancel that inherited scale on the new visual, never on the rigidbody.
    public sealed class CandySkinVisual : MonoBehaviour
    {
        [SerializeField] Renderer[] nativeRenderers;
        [SerializeField] bool[] nativeEnabled;
        [SerializeField] float diameter;
        GameObject model;Renderer[] modelRenderers;Material[] materials;Shader[] shaders;
        bool initialized,grounded;float floorHeight;int skin=-1;
        public int Skin=>Mathf.Max(0,skin);
        public Transform Model=>model?model.transform:null;
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            // Instantiated LAN bodies retain the serialized original enable flags,
            // even when their source currently displays a different candy model.
            Transform orphan=transform.Find("Candy skin visual");if(orphan){orphan.gameObject.SetActive(false);Destroy(orphan.gameObject);}
            if(nativeRenderers==null||nativeRenderers.Length==0)
            {
                nativeRenderers=GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.transform==transform||r.name=="zCandy"||r.name=="CandyGhost").Cast<Renderer>().ToArray();
                nativeEnabled=nativeRenderers.Select(r=>r.enabled).ToArray();
                Renderer main=nativeRenderers.Last();diameter=Mathf.Max(main.bounds.size.x,main.bounds.size.y,main.bounds.size.z);
            }
            SetSkin(0);
        }
        public void SetSkin(int value)
        {
            value=CandySkinSettings.CleanSkin(value);if(skin==value)return;
            grounded=false;ClearModel();skin=value;
            for(int i=0;i<nativeRenderers.Length;i++)if(nativeRenderers[i])nativeRenderers[i].enabled=value==0&&nativeEnabled[i];
            if(value==0)return;
            model=Instantiate(Resources.Load<GameObject>("CandySkins/"+(value==1?"Donut":"Cupcake")),transform,false);model.name="Candy skin visual";
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;ScaleModel();
            foreach(Transform child in model.GetComponentsInChildren<Transform>(true))child.gameObject.layer=gameObject.layer;
            modelRenderers=model.GetComponentsInChildren<Renderer>();materials=new Material[modelRenderers.Length];shaders=new Shader[modelRenderers.Length];
            for(int i=0;i<materials.Length;i++)
            {
                modelRenderers[i].forceRenderingOff=nativeRenderers.Last().forceRenderingOff;
                materials[i]=new Material(modelRenderers[i].sharedMaterial);shaders[i]=materials[i].shader;modelRenderers[i].sharedMaterial=materials[i];
            }
        }
        void ScaleModel()
        {
            if(!model)return;Vector3 scale=transform.lossyScale;
            model.transform.localScale=new Vector3(diameter/Mathf.Abs(scale.x),diameter/Mathf.Abs(scale.y),diameter/Mathf.Abs(scale.z));
        }
        void LateUpdate(){ScaleModel();AlignToFloor();}
        public void SetGrounded(bool landed,float height)
        {grounded=landed&&skin==2;floorHeight=height;AlignToFloor();}
        void AlignToFloor()
        {
            if(!model)return;model.transform.localPosition=Vector3.zero;
            // Preserve the full cupcake above the surface while the recovered
            // flat candy collider and its stopping behaviour remain unchanged.
            if(grounded&&modelRenderers!=null)
            {
                float bottom=modelRenderers.Min(r=>r.bounds.min.y);
                model.transform.position+=Vector3.up*Mathf.Max(0,floorHeight-bottom);
            }
        }
        public void SetFade(float alpha)
        {
            if(materials==null)return;
            for(int i=0;i<materials.Length;i++)
            {materials[i].shader=alpha>=1?shaders[i]:Shader.Find("Transparent/VertexLit");materials[i].color=new Color(1,1,1,alpha);}
        }
        void ClearModel()
        {
            if(model){model.SetActive(false);Destroy(model);model=null;}
            if(materials!=null)foreach(var material in materials)if(material)Destroy(material);materials=null;modelRenderers=null;
        }
        void OnDestroy()=>ClearModel();
    }
}
