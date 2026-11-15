using UnityEngine;

namespace CandyFlick
{
    public sealed class LanCandyView : MonoBehaviour
    {
        public int Id,Owner,RingBonus;public float ScoreDistance;public bool Flying,Landed;public double LandedAt=double.PositiveInfinity;public float Born;
        public Rigidbody Body {get;private set;}
        LanMultiplayer multiplayer;Transform shadow;CandySkinVisual visual;
        public int Skin=>visual?visual.Skin:0;
        public void SetLanded(bool value,float floorHeight){Landed=value;visual.SetGrounded(value,floorHeight);}
        public void SetSkin(int skin)
        {
            visual.SetSkin(skin);
            if(!Flying||!multiplayer.IsHost)foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        }
        public void Initialize(LanMultiplayer owner,int id,int player,bool flying,bool authority)
        {
            multiplayer=owner;Id=id;Owner=player;Flying=flying;Body=GetComponent<Rigidbody>();if(!Body)Body=gameObject.AddComponent<Rigidbody>();
            visual=CandySkinSettings.Visual(transform);
            var original=GetComponent<CandyProjectile>();if(original){original.enabled=false;Destroy(original);}
            // Remote held candies belong to the shared world, not the local
            // first-person candy-camera layer used by the source ready prefab.
            if(!flying)foreach(Transform child in GetComponentsInChildren<Transform>(true))child.gameObject.layer=0;
            Body.isKinematic=true;Body.useGravity=true;Body.mass=.2f;Body.linearDamping=.5f;Body.angularDamping=.05f;Body.maxAngularVelocity=7;
            Body.collisionDetectionMode=CollisionDetectionMode.Discrete; // Original convex-mesh body at the native 100 Hz step.
            // Preserve the original root collider and disabled visual-mesh collider.
            // Client/ready replicas have no collision authority.
            if(!flying||!authority)foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
            foreach(var trail in GetComponentsInChildren<TrailRenderer>()){trail.Clear();trail.enabled=flying;}
            Transform super=transform.Find("aSuperEffect");if(super)super.gameObject.SetActive(false);
            shadow=transform.Find("Shadow");
            Body.isKinematic=!(flying&&authority);
        }
        void LateUpdate(){if(shadow)shadow.position=new Vector3(transform.position.x,0,transform.position.z);}
        void OnCollisionEnter(Collision collision)
        {
            if(!Flying||!multiplayer||!multiplayer.IsHost)return;
            if(collision.gameObject.name=="Floor"&&!Landed){SetLanded(true,collision.collider.bounds.max.y);LandedAt=Time.fixedTimeAsDouble;}
            if(collision.gameObject.GetComponent<OmNomActor>())multiplayer.QueueLanContact(this,Time.fixedTimeAsDouble);
        }
    }
}
