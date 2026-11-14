using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyFlick
{
    // GameLoader 0x4ce84/0x4cec8: original artwork over an animated indicator,
    // loading the main scene asynchronously. Camera permission stays at AR Play.
    public sealed class ZeptoLabLoadingScreen : MonoBehaviour
    {
        public const string MainSceneName = "CandyFlick";
        [System.Serializable] sealed class Key { public float time,value,inTangent,outTangent; }
        [System.Serializable] sealed class Source { public Key[] indicatorKeys; }
        Texture2D background,indicator,indicatorBackground;
        AnimationCurve curve;
        float started;
        AudioListener listener;
        public AsyncOperation SceneLoad { get; private set; }
        public string ArtworkFamily { get; private set; }
        public float Duration { get; private set; }
        public float Elapsed => Time.realtimeSinceStartup-started;
        public float IndicatorX => curve.Evaluate(Mathf.Min(Elapsed,Duration));
        public bool ActivationRequested { get; private set; }
        void Awake()
        {
            ArtworkFamily=SelectArtwork(Screen.width,Screen.height);
            string prefix="images/"+ArtworkFamily+"/loader_screen/";
            background=Resources.Load<Texture2D>(prefix+"zeptolab_bgr");
            indicator=Resources.Load<Texture2D>(prefix+"zeptolab_indicator");
            indicatorBackground=Resources.Load<Texture2D>(prefix+"zeptolab_indicator_bgr");
            var source=JsonUtility.FromJson<Source>(Resources.Load<TextAsset>("Original26/loader").text);
            var keys=new Keyframe[source.indicatorKeys.Length];
            for(int i=0;i<keys.Length;i++)
            {
                Key key=source.indicatorKeys[i];keys[i]=new Keyframe(key.time,key.value,key.inTangent,key.outTangent);
            }
            curve=new AnimationCurve(keys);Duration=keys[keys.Length-1].time;
            var camera=new GameObject("Loading screen camera").AddComponent<Camera>();
            camera.transform.SetParent(transform,false);camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.white;camera.cullingMask=0;
            listener=camera.gameObject.AddComponent<AudioListener>();
            started=Time.realtimeSinceStartup;
        }
        IEnumerator Start()
        {
            // Present the logo before loading or activating the heavier game scene.
            yield return null;
            SceneLoad=SceneManager.LoadSceneAsync(MainSceneName,LoadSceneMode.Single);
            SceneLoad.allowSceneActivation=false;
            // Preserve the full recovered three-second indicator on fast devices;
            // on slower loads it holds at its final pose until the scene is ready.
            while(SceneLoad.progress<.9f||Elapsed<Duration)yield return null;
            ActivationRequested=true;listener.enabled=false;SceneLoad.allowSceneActivation=true;
        }
        public static string SelectArtwork(float width,float height)
        {
            float aspect=width/height;
            return aspect>=.72f?"ipad2":aspect>=.62f?"iphone4":"iphone5";
        }
        public static Rect ArtworkRect(Rect safe,float screenHeight,float width,float height)
        {
            float scale=Mathf.Min(safe.width/width,safe.height/height);
            return new Rect(safe.x+(safe.width-width*scale)/2,screenHeight-safe.yMax+(safe.height-height*scale)/2,width*scale,height*scale);
        }
        void OnGUI()
        {
            if(!background||!indicator||!indicatorBackground)return;
            GUI.depth=-10000;GUI.color=Color.white;
            Rect screen=new Rect(0,0,Screen.width,Screen.height);
            // Extend only the artwork's edge gradient into device gutters.
            GUI.DrawTextureWithTexCoords(screen,background,new Rect(0,0,1f/background.width,1),true);
            Rect frame=ArtworkRect(Screen.safeArea,Screen.height,background.width,background.height);
            GUI.BeginGroup(frame);
            Rect whole=new Rect(0,0,frame.width,frame.height);
            GUI.DrawTexture(whole,indicatorBackground);
            Rect moving=whole;moving.x+=(IndicatorX-.5f)*frame.width;
            GUI.DrawTexture(moving,indicator);
            // Its transparent slot masks the sliding green strip, as in the native GUITextures.
            GUI.DrawTexture(whole,background);
            GUI.EndGroup();
            if(Event.current.isMouse||Event.current.isKey||Event.current.type==EventType.ScrollWheel)Event.current.Use();
        }
    }
}
