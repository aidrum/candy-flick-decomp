using System;
using System.IO;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace CandyFlickEditor
{
    // Uses Vuforia's own simulator and native database loader. This verifies SDK
    // integration and legacy dataset compatibility, not physical camera quality.
    [InitializeOnLoad]
    public static class VuforiaValidation
    {
        const string Key = "CandyFlickVuforiaValidation";
        static CandyFlickGame game;
        static double deadline;
        static int stage;
        static string pendingFailure;
        static double failedAt;
        static int cameraFps, originalVSync;
        static VuforiaValidation()
        {
            if(!SessionState.GetBool(Key,false))return;
            EditorApplication.update += Tick;Application.logMessageReceived += OnLog;
        }
        public static void Run()
        {
            ProjectBuilder.ConfigureVuforia();
            SessionState.SetBool(Key+"HadCameraFpsLock",PlayerPrefs.HasKey(CandyFlickGame.CameraFpsLockPreference));
            SessionState.SetInt(Key+"CameraFpsLock",PlayerPrefs.GetInt(CandyFlickGame.CameraFpsLockPreference,0));
            // Start with a saved enabled preference to cover the next app launch.
            PlayerPrefs.SetInt(CandyFlickGame.CameraFpsLockPreference,1);
            SessionState.SetInt(Key+"PlayMode",(int)VuforiaConfiguration.Instance.PlayMode.PlayModeType);
            SetPlayMode(PlayModeType.SIMULATOR);
            if(File.Exists("Documentation/vuforia-validation-failure.txt"))File.Delete("Documentation/vuforia-validation-failure.txt");
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if((type==LogType.Error || type==LogType.Exception || type==LogType.Assert) && pendingFailure==null)
            { pendingFailure=message+"\n"+stack;failedAt=EditorApplication.timeSinceStartup; }
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            if(pendingFailure!=null && EditorApplication.timeSinceStartup-failedAt>2) { Fail(pendingFailure);return; }
            try {
                if(stage==0) {
                    game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
                    if(!game.LockFpsToCamera || Application.targetFrameRate!=60)throw new Exception("Saved FPS lock not loaded or affected timing before capture");
                    originalVSync=QualitySettings.vSyncCount;QualitySettings.vSyncCount=1;
                    deadline=EditorApplication.timeSinceStartup+60;stage=1;game.StartGame(true);return;
                }
                if(stage==1 && game.ar.TargetCount==2 && VuforiaApplication.Instance.IsRunning) {
                    if(VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled)throw new Exception("Device/world tracking unexpectedly enabled");
                    cameraFps=VuforiaBehaviour.Instance.CameraDevice.GetRecommendedFPS();
                    CheckCameraCap();
                    game.ToggleCameraFpsLock();
                    CheckRestored();
                    game.ToggleCameraFpsLock();CheckCameraCap();
                    game.ar.SendMessage("OnApplicationPause",true);CheckRestored();
                    game.ar.SendMessage("OnApplicationPause",false);CheckCameraCap();
                    game.ar.SendMessage("OnPaused",true);CheckRestored();
                    game.ar.SendMessage("OnPaused",false);CheckCameraCap();
                    game.ReturnToMenu();CheckRestored();
                    game.StartGame(true);stage=2;return;
                }
                if(stage==2 && VuforiaApplication.Instance.IsRunning) {
                    CheckCameraCap();
                    game.ToggleCameraFpsLock();CheckRestored();
                    // Capture continues with the switch off; state updates must preserve 60 FPS/VSync.
                    stage=3;return;
                }
                if(stage==3) {
                    CheckRestored();
                    File.WriteAllText("Documentation/vuforia-validation.json",JsonUtility.ToJson(new Evidence {
                        result="passed",sdk=VuforiaApplication.GetVuforiaLibraryVersion(),targets=game.ar.TargetCount,
                        originalDatabase="2.6 CandyFlick.xml/.dat loaded by Vuforia",deviceTracking=false,
                        limit="Vuforia simulator/native SDK validation; physical Android range and performance not tested"
                    },true));
                    File.WriteAllText("Documentation/camera-fps-validation.json",JsonUtility.ToJson(new FrameRateEvidence {
                        result="passed",cameraFps=cameraFps,
                        checks=new[]{"Saved enabled preference loads without changing timing before capture", "Camera startup and restart apply the SDK camera FPS with VSync disabled",
                            "Toggle off restores 60 FPS and the previous VSync setting", "Application and Vuforia pause/resume restore and reapply the cap",
                            "Leaving AR restores prior timing", "Camera updates with the toggle off preserve original timing"},
                        limit="Vuforia simulator/native SDK; physical Android capture/display synchronization not tested"
                    },true));
                    Finish(0);return;
                }
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Vuforia initialization/legacy database timeout: "+game.ar.Status);
            } catch(Exception exception) { Fail(exception.ToString()); }
        }
        static void Fail(string message)
        {
            if(stage==99)return;
            File.WriteAllText("Documentation/vuforia-validation-failure.txt",message);Finish(1);
        }
        static void CheckCameraCap()
        {
            game.ar.RefreshFrameRate();
            int fps=VuforiaBehaviour.Instance.CameraDevice.GetRecommendedFPS();
            if(fps<=0 || Application.targetFrameRate!=fps || QualitySettings.vSyncCount!=0)
                throw new Exception("Camera FPS cap not applied: camera="+fps+", render="+Application.targetFrameRate+", VSync="+QualitySettings.vSyncCount);
            if(Mathf.Abs(Time.fixedDeltaTime-.01f)>.0000001f)throw new Exception("FPS lock changed native physics timing");
        }
        static void CheckRestored()
        {
            game.ar.RefreshFrameRate();
            if(Application.targetFrameRate!=60 || QualitySettings.vSyncCount!=1)throw new Exception("Original render timing not restored");
        }
        static void Finish(int code)
        {
            stage=99;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            SetPlayMode((PlayModeType)SessionState.GetInt(Key+"PlayMode",0));
            if(game)game.ReturnToMenu();EditorApplication.ExitPlaymode();
            QualitySettings.vSyncCount=originalVSync;
            if(SessionState.GetBool(Key+"HadCameraFpsLock",false))PlayerPrefs.SetInt(CandyFlickGame.CameraFpsLockPreference,SessionState.GetInt(Key+"CameraFpsLock",0));
            else PlayerPrefs.DeleteKey(CandyFlickGame.CameraFpsLockPreference);
            PlayerPrefs.Save();
            if(Application.isBatchMode)EditorApplication.Exit(code);
        }
        static void SetPlayMode(PlayModeType mode)
        {
            var asset=AssetDatabase.LoadAssetAtPath<VuforiaConfiguration>("Assets/Resources/VuforiaConfiguration.asset");
            foreach(var config in new[]{asset,VuforiaConfiguration.Instance}) {
                config.PlayMode.PlayModeType=mode;EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
        }
        [Serializable]sealed class Evidence { public string result,sdk,originalDatabase,limit;public int targets;public bool deviceTracking; }
        [Serializable]sealed class FrameRateEvidence { public string result,limit;public int cameraFps;public string[] checks; }
    }
}
