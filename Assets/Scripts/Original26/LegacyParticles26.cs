using System;
using UnityEngine;

namespace CandyFlick.Original26
{
    // Unity 6 adapter for removed EllipsoidParticleEmitter/ParticleAnimator/ParticleRenderer.
    // All five profiles are extracted from 2.6, with their original materials and colors.
    // Random sampling/integration uses Unity 6; this does not assert identical Unity 4 RNG.
    public sealed class LegacyParticles26 : MonoBehaviour
    {
        [Serializable] public sealed class Profile
        {
            public string name;
            public float minSize,maxSize,minEnergy,maxEnergy,minEmission,maxEmission,emitterVelocityScale;
            public float angularVelocity,rndAngularVelocity,rndRotation,sizeGrow,damping,minEmitterRange;
            public float stretchParticles,lengthScale,velocityScale,maxParticleSize;
            public bool emit,oneShot,worldSpace,animateColor;
            public Vector3 worldVelocity,localVelocity,rndVelocity,tangentVelocity,ellipsoid,force,rndForce,worldRotationAxis,localRotationAxis;
            public uint[] colors;
        }
        [Serializable] sealed class Profiles { public Profile[] profiles; }
        public string profileName;
        public Material particleMaterial;
        public Profile Settings { get; private set; }
        public ParticleSystem System { get; private set; }
        static Profiles source;
        ParticleSystem.Particle[] particles;
        Vector3 previousPosition;
        Quaternion previousRotation;
        float emissionRemainder;
        bool emitting;
        void Awake()
        {
            if (source == null) source=JsonUtility.FromJson<Profiles>(Resources.Load<TextAsset>("Original26/particles").text);
            Settings=Array.Find(source.profiles,p=>p.name==profileName);
            if (Settings == null) throw new InvalidOperationException("Missing original particle profile "+profileName);
            System=gameObject.AddComponent<ParticleSystem>();
            System.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=System.main;
            main.playOnAwake=false;main.loop=true;main.simulationSpace=ParticleSystemSimulationSpace.World;
            main.scalingMode=ParticleSystemScalingMode.Shape;
            main.maxParticles=Mathf.CeilToInt(Settings.maxEmission*(Settings.maxEnergy+1));
            main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=System.emission;emission.enabled=false;
            var shape=System.shape;shape.enabled=false;
            var renderer=GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=particleMaterial;
            renderer.maxParticleSize=Settings.maxParticleSize;
            renderer.lengthScale=Settings.lengthScale;renderer.velocityScale=Settings.velocityScale;
            renderer.renderMode=Settings.stretchParticles==4?ParticleSystemRenderMode.VerticalBillboard:
                Settings.stretchParticles==3?ParticleSystemRenderMode.HorizontalBillboard:
                Settings.stretchParticles==1?ParticleSystemRenderMode.Stretch:ParticleSystemRenderMode.Billboard;
            if(Settings.stretchParticles==2)renderer.sortMode=ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            particles=new ParticleSystem.Particle[main.maxParticles];
        }
        void OnEnable()
        {
            previousPosition=transform.position;previousRotation=transform.rotation;emissionRemainder=0;
            if(System) { System.Clear();System.Play();SetEmitting(Settings.emit); }
        }
        void OnDisable() { if(System)System.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);emitting=false; }
        public void SetEmitting(bool value)
        {
            if(value && !emitting && Settings.oneShot) EmitNative(Mathf.RoundToInt(UnityEngine.Random.Range(Settings.minEmission,Settings.maxEmission)));
            emitting=value;
        }
        public void EmitNative(int count)
        {
            for(int i=0;i<count;i++)
            {
                Vector3 point=UnityEngine.Random.insideUnitSphere;
                if(point.magnitude<Settings.minEmitterRange)point=point.normalized*Settings.minEmitterRange;
                Vector3 offset=Vector3.Scale(point,Settings.ellipsoid);
                Vector3 localVelocity=Settings.localVelocity+Vector3.Scale(RandomSigned(),Settings.rndVelocity)
                    +Vector3.Cross(Settings.tangentVelocity,point.normalized);
                Vector3 inherit=Time.deltaTime>0?(transform.position-previousPosition)/Time.deltaTime*Settings.emitterVelocityScale:Vector3.zero;
                var parameters=new ParticleSystem.EmitParams {
                    position=transform.position+transform.rotation*offset,
                    velocity=Settings.worldVelocity+transform.rotation*localVelocity+inherit,
                    startLifetime=UnityEngine.Random.Range(Settings.minEnergy,Settings.maxEnergy),
                    startSize=UnityEngine.Random.Range(Settings.minSize,Settings.maxSize),
                    rotation=Settings.rndRotation!=0?UnityEngine.Random.Range(0,360):0,
                    randomSeed=(uint)UnityEngine.Random.Range(1,int.MaxValue),
                    startColor=Settings.animateColor?Unpack(Settings.colors[0]):Color.white
                };
                System.Emit(parameters,1);
            }
        }
        void Update()
        {
            float dt=Time.deltaTime;if(dt<=0)return;
            int count=System.GetParticles(particles);
            Quaternion rotation=transform.rotation*Quaternion.Inverse(previousRotation);
            for(int i=0;i<count;i++)
            {
                if(!Settings.worldSpace)
                {
                    particles[i].position=transform.position+rotation*(particles[i].position-previousPosition);
                    particles[i].velocity=rotation*particles[i].velocity;
                }
                particles[i].velocity=(particles[i].velocity+(Settings.force+Vector3.Scale(RandomSigned(),Settings.rndForce))*dt)*Mathf.Pow(Settings.damping,dt);
                particles[i].startSize=Mathf.Max(0,particles[i].startSize+Settings.sizeGrow*dt);
                float angularVelocity=Settings.angularVelocity+(particles[i].randomSeed/(float)int.MaxValue*2-1)*Settings.rndAngularVelocity;
                particles[i].rotation+=angularVelocity*dt;
                if(Settings.animateColor)
                {
                    float t=Mathf.Clamp01(1-particles[i].remainingLifetime/particles[i].startLifetime)*4;
                    int k=Mathf.Min(3,Mathf.FloorToInt(t));
                    particles[i].startColor=Color.Lerp(Unpack(Settings.colors[k]),Unpack(Settings.colors[k+1]),t-k);
                }
            }
            System.SetParticles(particles,count);
            if(emitting && !Settings.oneShot)
            {
                emissionRemainder+=UnityEngine.Random.Range(Settings.minEmission,Settings.maxEmission)*dt;
                int births=Mathf.FloorToInt(emissionRemainder);emissionRemainder-=births;EmitNative(births);
            }
            previousPosition=transform.position;previousRotation=transform.rotation;
        }
        static Vector3 RandomSigned()=>new Vector3(UnityEngine.Random.Range(-1f,1f),UnityEngine.Random.Range(-1f,1f),UnityEngine.Random.Range(-1f,1f));
        static Color32 Unpack(uint rgba)=>new Color32((byte)rgba,(byte)(rgba>>8),(byte)(rgba>>16),(byte)(rgba>>24));
    }
}
