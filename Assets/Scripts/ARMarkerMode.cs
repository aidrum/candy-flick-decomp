using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;

namespace CandyFlick
{
    // Modern Android bridge for the same Vuforia/QCAR tracker family used in 2.6.
    // Vuforia owns capture, calibration, tracking and live video rendering. No CPU
    // pixel copies, custom detector, buffered preview, world anchors or SLAM fallback.
    public sealed class ARMarkerMode : MonoBehaviour
    {
        public bool Tracking { get; private set; }
        public string Status { get; private set; } = "Starting camera…";
        public Camera Camera { get; private set; }
        public int TargetCount => targets.Count;
        public const string DatabasePath = "Vuforia/CandyFlick.xml";
        public static readonly string[] TargetNames = { "NewTarget", "CandyFlickImageTarget" };
        public const int BackgroundLayer = 30;
        readonly List<ImageTargetBehaviour> targets = new List<ImageTargetBehaviour>();
        CandyFlickGame game;
        GameObject rig;
        VuforiaBehaviour vuforia;
        Coroutine startup;
        bool running, subscribed;
        bool applicationPaused, capturePaused, frameRateLocked;
        int previousFrameRate, previousVSyncCount;

        public void Initialize(CandyFlickGame owner) { game = owner; }
        public void Begin()
        {
            running = true; SetTracking(false);
            if(startup != null)StopCoroutine(startup);
            startup = StartCoroutine(StartCamera());
        }
        IEnumerator StartCamera()
        {
            if(string.IsNullOrWhiteSpace(VuforiaConfiguration.Instance.Vuforia.LicenseKey))
            { Status = "The Vuforia license key is missing from this build."; yield break; }
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
            {
                Status = "Allow camera access to find the card.";
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Camera);
                float deadline = Time.realtimeSinceStartup + 30;
                while(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera) && Time.realtimeSinceStartup < deadline)yield return null;
                if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Camera))
                { Status = "Camera access is required. Allow it in Android settings."; yield break; }
            }
#endif
            if(!rig)
            {
                rig = new GameObject("Vuforia image camera");
                rig.SetActive(false);
                Camera = rig.AddComponent<Camera>();
                Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = Color.black;
                Camera.nearClipPlane = .01f; Camera.farClipPlane = 20;
                Camera.cullingMask = 1 << BackgroundLayer; Camera.depth = 0;
                game.GameCamera.cullingMask &= ~(1 << BackgroundLayer);
                game.CandyCamera.cullingMask &= ~(1 << BackgroundLayer);
                vuforia = rig.AddComponent<VuforiaBehaviour>();
                // Keep the camera as world origin: every game pose is expressed
                // relative to the currently observed image, as in the original.
                rig.SetActive(true);
                vuforia.SetWorldCenter(WorldCenterMode.DEVICE, null);
                vuforia.World.OnStateUpdated += OnCameraStateUpdated;
                VuforiaApplication.Instance.OnVuforiaInitialized += OnInitialized;
                VuforiaApplication.Instance.OnVuforiaStarted += OnStarted;
                VuforiaApplication.Instance.OnVuforiaStopped += OnStopped;
                VuforiaApplication.Instance.OnVuforiaPaused += OnPaused;
                subscribed = true;
            }
            else rig.SetActive(true);
            if(VuforiaApplication.Instance.IsInitialized)
            {
                if(targets.Count == 0)LoadTargets();
                foreach(var target in targets)target.enabled = true;
            }
            else VuforiaApplication.Instance.Initialize();
            RefreshFrameRate();
            Status = "Point your camera at the card.";
        }
        void OnInitialized(VuforiaInitError error)
        {
            if(error != VuforiaInitError.NONE)
            { Status = "The camera tracker could not start (" + error + ")."; SetTracking(false); return; }
            vuforia.VideoBackground.OnVideoBackgroundChanged += RouteVideoBackground;
            RouteVideoBackground();
            LoadTargets();
            if(!running) { foreach(var target in targets)target.enabled = false; if(rig)rig.SetActive(false); }
        }
        void RouteVideoBackground()
        {
            // Vuforia creates a MeshRenderer under its camera when capture starts
            // (and recreates it on restart). It must be visible to that camera,
            // while the two native-unit game cameras must never draw it again.
            foreach(var renderer in rig.GetComponentsInChildren<MeshRenderer>(true))
                renderer.gameObject.layer = BackgroundLayer;
        }
        void LoadTargets()
        {
            if(targets.Count != 0)return;
            foreach(string name in TargetNames)
            {
                // The database is copied byte-for-byte from the main 2.6 IPA,
                // retaining its original feature descriptors and image geometry.
                ImageTargetBehaviour target = vuforia.ObserverFactory.CreateImageTarget(DatabasePath, name);
                if(!target)throw new InvalidOperationException("Vuforia could not load the original target: " + name);
                target.SetWidth(.2f); // Original 200-unit scene target = physical 20 cm.
                targets.Add(target);
            }
        }
        void OnCameraStateUpdated()
        {
            if(!running)return;
            RefreshFrameRate();
            ImageTargetBehaviour observed = null;
            foreach(var target in targets)
                if(target && target.enabled && IsImageVisible(target.TargetStatus.Status)) { observed = target; break; }
            SetTracking(observed != null);
            if(!observed) { Status = "Point your camera at the card to continue."; return; }
            Pose native = NativeCameraPose(new Pose(observed.transform.position, observed.transform.rotation),
                new Pose(Camera.transform.position, Camera.transform.rotation));
            game.SetTrackedView(native.position, native.rotation, Camera.projectionMatrix);
        }
        public static bool IsImageVisible(Vuforia.Status status) => status == Vuforia.Status.TRACKED;
        // Vuforia reports the capture FPS after startup. Keep the render cap in
        // step with it, including after capture restarts or changes its rate.
        public void RefreshFrameRate()
        {
            if(!game || !game.LockFpsToCamera || !running || applicationPaused || capturePaused || !vuforia || !VuforiaApplication.Instance.IsRunning)
            { RestoreFrameRate(); return; }
            int fps = vuforia.CameraDevice.GetRecommendedFPS();
            if(fps <= 0) { RestoreFrameRate(); return; }
            if(!frameRateLocked)
            {
                previousFrameRate = Application.targetFrameRate;
                previousVSyncCount = QualitySettings.vSyncCount;
                frameRateLocked = true;
            }
            // Desktop VSync otherwise overrides targetFrameRate; Android uses the cap directly.
            QualitySettings.vSyncCount = 0;
            if(Application.targetFrameRate != fps)Application.targetFrameRate = fps;
        }
        void RestoreFrameRate()
        {
            if(!frameRateLocked)return;
            Application.targetFrameRate = previousFrameRate;
            QualitySettings.vSyncCount = previousVSyncCount;
            frameRateLocked = false;
        }
        void OnStarted() { capturePaused = false; RefreshFrameRate(); }
        void OnStopped() { RestoreFrameRate(); if(running)SetTracking(false); }
        void OnPaused(bool paused)
        {
            capturePaused = paused;
            if(paused) { RestoreFrameRate(); if(running)SetTracking(false); }
            else RefreshFrameRate();
        }
        public static Pose NativeCameraPose(Pose target, Pose camera)
        {
            Quaternion inverse = Quaternion.Inverse(target.rotation);
            return new Pose(inverse * (camera.position - target.position) * 1000f, inverse * camera.rotation);
        }
        void SetTracking(bool found)
        {
            if(Tracking != found) { Tracking = found; if(game)game.OnTrackingChanged(found); }
            if(game && game.World)foreach(var renderer in game.World.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff = !found;
        }
        void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if(paused) { RestoreFrameRate(); if(running)SetTracking(false); }
            else RefreshFrameRate();
        }
        public void End()
        {
            running = false;
            RestoreFrameRate();
            if(startup != null) { StopCoroutine(startup); startup = null; }
            SetTracking(false);
            foreach(var target in targets)if(target)target.enabled = false;
            // Stop capture when leaving AR, but retain the initialized SDK/database
            // for the next game. Avoid repeated native shutdown/reinitialization.
            if(rig)rig.SetActive(false);
            if(game && game.World)foreach(var renderer in game.World.GetComponentsInChildren<Renderer>())renderer.forceRenderingOff = false;
        }
        void OnDestroy()
        {
            End();
            if(subscribed)
            {
                VuforiaApplication.Instance.OnVuforiaInitialized -= OnInitialized;
                VuforiaApplication.Instance.OnVuforiaStarted -= OnStarted;
                VuforiaApplication.Instance.OnVuforiaStopped -= OnStopped;
                VuforiaApplication.Instance.OnVuforiaPaused -= OnPaused;
                if(vuforia != null && vuforia.VideoBackground != null)
                    vuforia.VideoBackground.OnVideoBackgroundChanged -= RouteVideoBackground;
                if(vuforia != null)vuforia.World.OnStateUpdated -= OnCameraStateUpdated;
            }
            foreach(var target in targets)if(target)Destroy(target.gameObject);
            targets.Clear();if(rig)Destroy(rig);
        }
    }
}
