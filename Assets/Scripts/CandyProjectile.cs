using UnityEngine;

namespace CandyFlick
{
    // BulletBehaviour 0x218b4–0x220cc; the original Rigidbody does the integration.
    public sealed class CandyProjectile : MonoBehaviour
    {
        public bool IsShot { get; private set; }
        public int RingBonus { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool HasLanded => collisioned;
        public Vector3 LastShotPosition {get;private set;}
        public double LandingPhysicsTime { get; private set; } = double.PositiveInfinity;
        public bool CanBeEaten => IsShot && !collisioned;
        // A catch from an earlier physics step still counts if a later step lands
        // before Update. Contacts in the landing step itself cannot count as a catch.
        public bool CanBeEatenAt(double contactTime) => IsShot && (!collisioned || LandingPhysicsTime > contactTime);
        CandyFlickGame game;
        CandySkinVisual skinVisual;
        Transform shadow;
        GameObject superEffect;
        Renderer childCandy;
        TrailRenderer trail;
        Material normalTrail;
        Shader normalShader;
        Vector3 restorePosition, savedForce;
        float shootTime, alpha = 1;
        bool saved, collisioned;

        public void Initialize(CandyFlickGame owner)
        {
            game = owner; Body = GetComponent<Rigidbody>();
            // Explicit deprecated-field migration from the serialized 2.6 Rigidbody.
            Body.mass = .2f; Body.linearDamping = .5f; Body.angularDamping = .05f;
            Body.useGravity = true; Body.isKinematic = true;
            Body.interpolation = RigidbodyInterpolation.None;
            Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            Body.maxAngularVelocity = 7; // Unity 4 DynamicsManager; Unity 6 defaults to 50.
            restorePosition = transform.position;skinVisual=CandySkinSettings.Visual(transform);
            childCandy = transform.Find("zCandy").GetComponent<Renderer>();
            normalShader = childCandy.sharedMaterial.shader;
            shadow = transform.Find("Shadow"); superEffect = transform.Find("aSuperEffect").gameObject;
            trail = GetComponent<TrailRenderer>();
            if (trail) normalTrail = trail.sharedMaterial;
            Restore();
        }

        public void LaunchImpulse(Vector3 force, Vector3 torque, bool super)
        {
            skinVisual.SetGrounded(false,0);skinVisual.SetFade(1);
            RingBonus = 0; saved = collisioned = false; alpha = 1; LandingPhysicsTime = double.PositiveInfinity;
            Body.isKinematic = false;
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            // ObjectShooter.Shoot, 0x26264: ForceMode.Impulse, including relative torque.
            Body.AddForce(force, ForceMode.Impulse);
            Body.AddRelativeTorque(torque, ForceMode.Impulse);
            childCandy.material.shader = normalShader;
            childCandy.material.color = Color.white;
            // Unity 4 enabled this hierarchy recursively; the serialized particle
            // child starts inactive and does not inherit SetActive(true) on its parent.
            if(super)foreach(Transform child in superEffect.GetComponentsInChildren<Transform>(true))child.gameObject.SetActive(true);
            superEffect.SetActive(super);
            if (trail)
            {
                trail.Clear(); trail.enabled = true;
                trail.sharedMaterial = super ? Resources.Load<Material>("images/all/yellowtrailmaterial") : normalTrail;
                trail.time = super ? .5f : .3f;
                trail.startWidth = super ? 120 : 70; trail.endWidth = super ? 90 : 30;
            }
            shootTime = Time.time; IsShot = true;
        }

        void Update()
        {
            if (!game || !game.CanSimulate) return;
            if (IsShot)
            {
                if (!saved) { savedForce = Body.linearVelocity; saved = true; }
                float elapsed = Time.time - shootTime;
                if (elapsed >= 3.5f)
                {
                    if (shadow) shadow.GetComponent<Renderer>().enabled = false;
                    childCandy.material.shader = Shader.Find("Transparent/VertexLit");
                    Color color = childCandy.material.color; color.a = alpha;
                    childCandy.material.color = color;skinVisual.SetFade(alpha); alpha -= Time.deltaTime * 2;
                }
                if (elapsed >= 4f) Restore();
            }
            if (shadow) shadow.position = new Vector3(transform.position.x, game.IsPhotoMode?game.PhotoGroundHeightAt(transform.position):0, transform.position.z);
        }

        public void Restore()
        {
            if (!Body) return;
            if(game.IsPhotoMode&&IsShot)LastShotPosition=transform.position;

            superEffect.SetActive(false);skinVisual.SetGrounded(false,0);skinVisual.SetFade(1);
            if (trail) { trail.enabled = false; trail.sharedMaterial = normalTrail; trail.time = .3f; trail.startWidth = 70; trail.endWidth = 30; }
            if (shadow) shadow.GetComponent<Renderer>().enabled = true;
            Body.isKinematic = true;
            saved = collisioned = IsShot = false; LandingPhysicsTime = double.PositiveInfinity;
            transform.position = restorePosition;
            childCandy.material.shader = normalShader; childCandy.material.color = Color.white; alpha = 1;
        }

        public void AddLandingForce()
        {
            float max = Mathf.Max(Mathf.Abs(savedForce.x), Mathf.Abs(savedForce.z));
            savedForce *= 1f / (max / 300f);
            Body.linearVelocity = savedForce;
        }
        public void AddRingBonus(int value) { RingBonus = value; } // ScoreBehaviour.temp_boost is assigned, not accumulated.
        void OnCollisionEnter(Collision collision)
        {
            // Keep the native one-shot landing sound. The modern floor sits at the
            // table surface, so also exclude landed candies from subsequent catches.
            if ((collision.gameObject.name != "Floor" && collision.gameObject.name != "Photo ground collider") || collisioned) return;
            collisioned = true; LandingPhysicsTime = Time.fixedTimeAsDouble;skinVisual.SetGrounded(true,collision.collider.bounds.max.y);

            if(!game.IsPhotoMode)game.Play(game.assets.misses[new System.Random().Next(0, game.assets.misses.Length - 1)]);
        }
    }
}
