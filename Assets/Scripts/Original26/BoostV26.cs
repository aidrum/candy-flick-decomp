using UnityEngine;

namespace CandyFlick.Original26
{
    // BoostVBehaviour, 0x215d4–0x21844. Original textured planes and animation curves.
    public sealed class BoostV26 : MonoBehaviour
    {
        CandyFlickGame game;
        GameObject wrapper;
        Transform ring;
        Animation animationPlayer;
        LegacyParticles26 hitParticles;
        public void Initialize(CandyFlickGame owner, GameObject container)
        {
            game = owner; wrapper = container; ring = transform.Find("ring");
            animationPlayer = GetComponent<Animation>();
            hitParticles = GetComponentInChildren<LegacyParticles26>(true);
            animationPlayer.Play("RingFadeInAnim");
            Transform reflex = transform.Find("reflex");
            Vector3 position = reflex.position; position.y = 0; reflex.position = position;
        }
        void Update()
        {
            if (game.CanSimulate)
                ring.Rotate(Vector3.forward, -.1f * Time.deltaTime * Mathf.Rad2Deg, Space.Self);
        }
        void OnTriggerEnter(Collider other)
        {
            var bullet = other.GetComponent<CandyProjectile>();
            if (!bullet || !bullet.IsShot) return;
            animationPlayer.Play("RingHitAnim");
            hitParticles.SetEmitting(true);
            StartCoroutine(WaitAndHideParticle());
            bullet.AddRingBonus(15);
            game.RingHit(0);
        }
        System.Collections.IEnumerator WaitAndHideParticle()
        {
            yield return new WaitForSeconds(hitParticles.Settings.maxEnergy);
            hitParticles.SetEmitting(false);
        }
        public void DeleteWithAlphaTimer(float seconds)
        {
            GetComponent<Collider>().enabled = false;
            animationPlayer.CrossFadeQueued("RingFadeOutAnim");
            Destroy(wrapper, seconds);
        }
    }
}
