using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace CandyFlick
{
    // One independently anchored actor. Appearance, heading and edits live only in memory.
    public sealed class PhotoOmNom : MonoBehaviour
    {
        public OmNomActor Actor {get;private set;}
        public TimeTravelSkins Appearance {get;private set;}
        public AnchorBehaviour Anchor {get;private set;}
        public bool Tracked {get;private set;}
        public bool HasPlacedPose {get;private set;}
        public bool IsMoving {get;private set;}
        public bool Placed=>HasPlacedPose&&!IsMoving;
        public Collider GroundCollider {get;private set;}
        Quaternion heading=Quaternion.identity;
        readonly Dictionary<Material,Color> outlineColors=new Dictionary<Material,Color>();
        readonly List<Material> ownedMaterials=new List<Material>();
        bool highlighted;
        GameObject surface;
        public void Initialize(OmNomActor actor,TimeTravelSkins appearance)
        {
            Actor=actor;Appearance=appearance;
            foreach(var renderer in GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.materials)
                {
                    ownedMaterials.Add(material);
                    if(material.HasProperty("_OutlineColor"))outlineColors[material]=material.GetColor("_OutlineColor");
                }
            surface=new GameObject("Photo ground collider");surface.transform.SetParent(transform,false);
            Vector3 scale=transform.lossyScale;surface.transform.localScale=new Vector3(1/scale.x,1/scale.y,1/scale.z);
            var floor=surface.AddComponent<BoxCollider>();floor.size=new Vector3(500,200,500);floor.center=new Vector3(0,-100,0);
            GroundCollider=floor;surface.SetActive(false);
        }
        public void Attach(AnchorBehaviour anchor)
        {
            Release();Anchor=anchor;Tracked=false;
            transform.position=anchor.transform.position*1000;
            SetMoving(false);
        }
        public void RememberHeading(){if(Anchor)heading=Quaternion.Inverse(Anchor.transform.rotation)*transform.rotation;}
        public void SyncAnchor(bool deviceTracked)
        {
            Tracked=deviceTracked&&Anchor&&Anchor.enabled&&ARMarkerMode.IsGroundTracked(Anchor.TargetStatus.Status);
            if(Tracked)
            {
                transform.SetPositionAndRotation(Anchor.transform.position*1000,Anchor.transform.rotation*heading);
                HasPlacedPose=true;
            }
            RefreshColliders();
        }
        public void SetMoving(bool moving)
        {
            IsMoving=moving;if(moving)Highlight(false);
            gameObject.SetActive(!moving);RefreshColliders();
        }
        void RefreshColliders()
        {
            if(surface)surface.SetActive(Tracked&&!IsMoving);
            var body=GetComponent<Collider>();if(body)body.enabled=Tracked&&!IsMoving;
        }
        public void Highlight(bool show)
        {
            if(highlighted==show)return;highlighted=show;
            foreach(var pair in outlineColors)if(pair.Key)pair.Key.SetColor("_OutlineColor",show?Color.white:pair.Value);
        }
        public void Release()
        {
            Highlight(false);Tracked=HasPlacedPose=false;RefreshColliders();
            if(Anchor){Anchor.UnconfigureAnchor();Destroy(Anchor.gameObject);Anchor=null;}
        }
        void OnDestroy(){Release();foreach(var material in ownedMaterials)if(material)Destroy(material);}
    }
}
