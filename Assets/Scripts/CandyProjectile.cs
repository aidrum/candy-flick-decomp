using UnityEngine;

namespace CandyFlick
{
    // BulletBehaviour 0x218b4–0x220cc; the original Rigidbody does the integration.
    public sealed class CandyProjectile : MonoBehaviour
    {
        public bool IsShot { get; private set; }
        public int RingBonus { get; private set; }
        public Rigidbody Body { get; private set; }
        CandyFlickGame game;
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
            restorePosition = transform.position;
            childCandy = transform.Find("zCandy").GetComponent<Renderer>();
            normalShader = childCandy.sharedMaterial.shader;
            shadow = transform.Find("Shadow"); superEffect = transform.Find("aSuperEffect").gameObject;
            trail = GetComponent<TrailRenderer>();
            if (trail) normalTrail = trail.sharedMaterial;
            Restore();
        }

        public void LaunchImpulse(Vector3 force, Vector3 torque, bool super)
        {
            RingBonus = 0; saved = collisioned = false; alpha = 1;
            Body.isKinematic = false;
            Body.linearVelocity = Body.angularVelocity = Vector3.zero;
            // ObjectShooter.Shoot, 0x26264: ForceMode.Impulse, including relative torque.
            Body.AddForce(force, ForceMode.Impulse);
            Body.AddRelativeTorque(torque, ForceMode.Impulse);
            childCandy.material.shader = normalShader;
            childCandy.material.color = Color.white;
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
                    childCandy.material.color = color; alpha -= Time.deltaTime * 2;
                }
                if (elapsed >= 4f) Restore();
            }
            if (shadow) shadow.position = new Vector3(transform.position.x, 0, transform.position.z);
        }

        public void Restore()
        {
            if (!Body) return;
            superEffect.SetActive(false);
            if (trail) { trail.enabled = false; trail.sharedMaterial = normalTrail; trail.time = .3f; trail.startWidth = 70; trail.endWidth = 30; }
            if (shadow) shadow.GetComponent<Renderer>().enabled = true;
            Body.isKinematic = true;
            saved = collisioned = IsShot = false;
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
            // Native BulletBehaviour plays a floor sound once; FrogBehaviour owns hit/miss decisions.
            if (collision.gameObject.name != "Floor" || collisioned) return;
            collisioned = true;
            game.Play(game.assets.misses[new System.Random().Next(0, game.assets.misses.Length - 1)]);
        }
    }
}
