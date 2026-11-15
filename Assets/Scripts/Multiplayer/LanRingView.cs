using CandyFlick.Multiplayer;
using CandyFlick.Original26;
using UnityEngine;

namespace CandyFlick
{
    // Native ring art, curves and particles; only the host owns trigger contacts.
    public sealed class LanRingView : MonoBehaviour
    {
        public LanRingState State {get;private set;}
        LanMultiplayer multiplayer;GameObject wrapper;Transform ring;
        Animation animationPlayer;LegacyParticles26 particles;float particlesUntil;
        public void Initialize(LanMultiplayer owner,GameObject container,LanRingState state,bool authority)
        {
            multiplayer=owner;wrapper=container;State=state;
            transform.SetPositionAndRotation(state.position,state.rotation);
            ring=transform.Find("ring");animationPlayer=GetComponent<Animation>();particles=GetComponentInChildren<LegacyParticles26>(true);
            GetComponent<Collider>().enabled=authority&&!state.hit;
            Transform reflex=transform.Find("reflex");Vector3 p=reflex.position;p.y=0;reflex.position=p;
            animationPlayer.Play("RingFadeInAnim");if(state.hit)ApplyHit();
        }
        public void ApplyHit()
        {
            State.hit=true;GetComponent<Collider>().enabled=false;
            animationPlayer.Play("RingHitAnim");particles.SetEmitting(true);
            particlesUntil=Time.time+particles.Settings.maxEnergy;multiplayer.PlayLanRingSound();
        }
        void Update()
        {
            if(!multiplayer.Playing)return;
            ring.Rotate(Vector3.forward,-.1f*Time.deltaTime*Mathf.Rad2Deg,Space.Self);
            if(particlesUntil>0&&Time.time>=particlesUntil){particles.SetEmitting(false);particlesUntil=0;}
        }
        void OnTriggerEnter(Collider other)
        {
            var candy=other.attachedRigidbody?other.attachedRigidbody.GetComponent<LanCandyView>():null;
            if(candy)multiplayer.TryPassLanRing(this,candy);
        }
        public void Dispose(){if(wrapper){wrapper.SetActive(false);Destroy(wrapper);}}
    }
}
