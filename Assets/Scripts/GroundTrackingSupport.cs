using System;
using UnityEngine;

namespace CandyFlick
{
    public enum GroundSupportState { Checking, Supported, Unsupported, ServiceRequired, Unknown }
    // Gyroscope presence alone is insufficient: ARCore also needs a supported
    // camera/device and an installed, current Google Play Services for AR.
    public sealed class GroundTrackingSupport : MonoBehaviour
    {
        public GroundSupportState State {get;private set;}=GroundSupportState.Checking;
        public string Reason {get;private set;}="Checking ground tracking support...";
        public bool Supported=>State==GroundSupportState.Supported;
        float nextCheck;bool unavailableCached;
#if UNITY_EDITOR
        public bool? EditorSupportOverride {get;set;}
#endif
        void OnEnable(){Refresh();}
        void Update(){if(!unavailableCached&&Time.unscaledTime>=nextCheck)Refresh();}
        public void Refresh()
        {
#if UNITY_EDITOR
            if(EditorSupportOverride.HasValue){nextCheck=Time.unscaledTime+2;unavailableCached=false;Set(EditorSupportOverride.Value?GroundSupportState.Supported:GroundSupportState.Unsupported,"This device does not support ground tracking.");return;}
#endif
            if(unavailableCached)return;
            nextCheck=Time.unscaledTime+2;
#if UNITY_EDITOR
            Set(GroundSupportState.Unsupported,"Ground tracking needs a supported phone, it is unavailable in the editor.");
#elif UNITY_ANDROID
            if(!SystemInfo.supportsGyroscope){Set(GroundSupportState.Unsupported,"Ground tracking requires a gyroscope. Use AR Mode: Image.");return;}
            try
            {
                using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                using(var api=new AndroidJavaClass("com.google.ar.core.ArCoreApk"))
                using(var instance=api.CallStatic<AndroidJavaObject>("getInstance"))
                using(var availability=instance.Call<AndroidJavaObject>("checkAvailability",activity))
                {
                    string value=availability.Call<string>("name");
                    if(value=="SUPPORTED_INSTALLED")Set(GroundSupportState.Supported,"");
                    else if(availability.Call<bool>("isSupported"))Set(GroundSupportState.ServiceRequired,"Install or update Google Play Services for AR to use Ground mode.");
                    else if(availability.Call<bool>("isTransient"))Set(GroundSupportState.Checking,"Checking ground tracking support...");
                    else if(availability.Call<bool>("isUnsupported"))Set(GroundSupportState.Unsupported,"This device does not support ARCore ground tracking. Use Image mode.");
                    else Set(GroundSupportState.Unknown,"Ground tracking support could not be checked. Use Image mode or try again.");
                }
            }
            catch(Exception){Set(GroundSupportState.Unknown,"Ground tracking is unavailable. Check Google Play Services for AR.");}
#else
            Set(GroundSupportState.Unsupported,"ARCore ground tracking is unavailable on this platform.");
#endif
        }
        void Set(GroundSupportState state,string reason){State=state;Reason=state==GroundSupportState.Supported?"":reason;unavailableCached=state==GroundSupportState.Unsupported||state==GroundSupportState.ServiceRequired||state==GroundSupportState.Unknown;}
    }
}
