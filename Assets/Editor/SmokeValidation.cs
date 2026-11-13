using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CandyFlick;
using CandyFlick.Original26;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class SmokeValidation
    {
        static CandyFlickGame game;
        static CandyProjectile bullet;
        static int stage;
        static double deadline, pauseUntil;
        static Vector3 pausedPosition;
        static readonly List<string> checks = new List<string>();
        static SmokeValidation()
        {
            if (!SessionState.GetBool("CandyFlickNative26Smoke", false)) return;
            EditorApplication.update += Tick; Application.logMessageReceived += OnLog;
        }
        public static void Run()
        {
            Original26Validation.Run();
            SessionState.SetBool("SmokeHadControlMode",PlayerPrefs.HasKey(CandyFlickGame.ControlModePreference));
            SessionState.SetInt("SmokeControlMode",PlayerPrefs.GetInt(CandyFlickGame.ControlModePreference,2));
            PlayerPrefs.DeleteKey(CandyFlickGame.ControlModePreference);
            SessionState.SetBool("SmokeHadCameraFpsLock",PlayerPrefs.HasKey(CandyFlickGame.CameraFpsLockPreference));
            SessionState.SetInt("SmokeCameraFpsLock",PlayerPrefs.GetInt(CandyFlickGame.CameraFpsLockPreference,0));
            PlayerPrefs.DeleteKey(CandyFlickGame.CameraFpsLockPreference);
            if (File.Exists("Documentation/validation-failure.txt")) File.Delete("Documentation/validation-failure.txt");
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Show();
            Application.runInBackground = true;
            SessionState.SetBool("CandyFlickNative26Smoke", true); EditorApplication.EnterPlaymode();
        }
        static void OnLog(string message, string stack, LogType type)
        {
            if (stack.Contains("UnityEditor.Search.SearchDatabase")) return;
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) Fail(message + "\n" + stack);
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks.Add(message); Debug.Log("NATIVE26_SMOKE_PASS: " + message);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                if (stage == 0)
                {
                    game = UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>(); if (!game) return;
                    deadline = EditorApplication.timeSinceStartup + 50;
                    Check(game.ScreenState == GameScreen.Menu, "Boots into main menu");
                    int originalVSync = QualitySettings.vSyncCount;
                    Check(!game.LockFpsToCamera && Application.targetFrameRate==60, "Camera FPS lock defaults off with the existing 60 FPS target");
                    game.ToggleCameraFpsLock();
                    Check(game.LockFpsToCamera && PlayerPrefs.GetInt(CandyFlickGame.CameraFpsLockPreference)==1
                        && Application.targetFrameRate==60 && QualitySettings.vSyncCount==originalVSync,
                        "Camera FPS lock persists on without changing timing when no camera is running");
                    game.ToggleCameraFpsLock();
                    Check(!game.LockFpsToCamera && PlayerPrefs.GetInt(CandyFlickGame.CameraFpsLockPreference,-1)==0
                        && Application.targetFrameRate==60 && QualitySettings.vSyncCount==originalVSync,
                        "Camera FPS lock persists off and preserves original timing");
                    Check(game.ControlMode == LaunchMode.Slingshot, "Default mode is native Tweaker (2)");
                    game.ToggleControlMode();
                    Check(game.ControlMode==LaunchMode.Flick && PlayerPrefs.GetInt(CandyFlickGame.ControlModePreference,-1)==0,
                        "Port settings switch selects and persists native flick mode");
                    game.ToggleControlMode();
                    Check(game.ControlMode==LaunchMode.Slingshot && PlayerPrefs.GetInt(CandyFlickGame.ControlModePreference,-1)==2,
                        "Port settings switch returns to and persists native slingshot mode");
                    Pose native=ARMarkerMode.NativeCameraPose(new Pose(new Vector3(2,3,4),Quaternion.Euler(0,90,0)),
                        new Pose(new Vector3(3,4,4),Quaternion.Euler(0,90,0)));
                    Check(Vector3.Distance(native.position,new Vector3(0,1000,1000))<.01f && Quaternion.Angle(native.rotation,Quaternion.identity)<.01f,
                        "Vuforia camera pose converts into original target coordinates at 1000 units/metre");
                    Check(ARMarkerMode.IsImageVisible(Vuforia.Status.TRACKED) && !ARMarkerMode.IsImageVisible(Vuforia.Status.EXTENDED_TRACKED)
                        && !ARMarkerMode.IsImageVisible(Vuforia.Status.NO_POSE) && !ARMarkerMode.IsImageVisible(Vuforia.Status.LIMITED),
                        "Only a visually tracked image displays Om Nom; retained and lost poses do not");
                    var transition = new ScreenTransition26(); int switched = 0;
                    transition.Begin(() => switched++);
                    for(int i=0;i<19;i++) { transition.BeforeGUI();transition.AfterGUI(); }
                    Check(switched==0, "Native screen switches wait for 20 GUI invocations");
                    transition.BeforeGUI();transition.AfterGUI();
                    Check(switched==1 && Mathf.Abs(transition.Alpha-1.2f)<.00001f, "Original black fade advances .06 per GUI invocation");
                    Check(File.Exists("Assets/StreamingAssets/Vuforia/CandyFlick.dat") && ARMarkerMode.TargetNames.Length==2, "Original 2.6 Vuforia database and both targets are configured");
                    Check(Resources.Load<GUISkin>("skins/iPhone5SkinGame").customStyles.Length == 44, "Original gameplay skin has all 44 recovered styles");
                    Check(game.Actor.transform.localScale == Vector3.one * 416.27103f, "Om Nom uses original serialized scale");
                    Check(game.Actor.GetComponent<BoxCollider>().size == new Vector3(.4f, .5f, .26f), "Original Om Nom collision volume is retained");
                    game.StartGame();
                    Check(Physics.gravity.y == -2000 && Mathf.Abs(Time.fixedDeltaTime - .01f) < .0000001f,
                        "Native gravity and 100 Hz physics step are active (" + Physics.gravity.y + ", " + Time.fixedDeltaTime.ToString("R") + ")");
                    Check(Physics.autoSyncTransforms && Physics.defaultMaxAngularSpeed == 7,
                        "Pre-2017 physics transform synchronization and native angular limit are restored");
                    Check(game.Boosts.Current == null, "No bonus ring is shown in stage one");
                    Physics.SyncTransforms();
                    Check(Physics.Raycast(new Vector3(3000, 500, 3000), Vector3.down, out RaycastHit ground, 1000) && ground.collider.name == "Floor"
                        && Mathf.Abs(ground.point.y) < .01f, "Candy floor collider is the marker plane at Om Nom's height (y=0)");
                    stage = 1; return;
                }
                if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Native smoke timeout at stage " + stage);
                if (stage == 1 && game.Ready)
                {
                    Check(game.GameCamera.fieldOfView == 51, "Original camera FOV is retained");
                    Check(game.Actor.GetComponent<Animation>()["idle"] != null, "Original clips bind to the original hierarchy");
                    Physics.SyncTransforms();
                    Vector2 start = game.GameCamera.WorldToScreenPoint(game.CandyGhost.position);
                    Check(TouchFlickListener26.CheckTouchPosSlingshot(start, Screen.width, Screen.height), "Native candy slot projects into the slingshot input region");
                    game.SampleInput(TouchPhase.Began, start, Vector2.zero);
                    Check(game.IsDragging, "Touching the original ghost collider starts a slingshot drag");
                    Vector2 end = start - Vector2.up * Screen.height * .15625f;
                    game.SampleInput(TouchPhase.Moved, end, end - start);
                    game.SampleInput(TouchPhase.Ended, end, Vector2.zero);
                    bullet = UnityEngine.Object.FindObjectsByType<CandyProjectile>(FindObjectsSortMode.None).Single(c => c.IsShot);
                    Check(!bullet.Body.isKinematic && bullet.Body.mass == .2f && bullet.Body.linearDamping == .5f, "Native input launches the recovered dynamic Rigidbody");
                    Check(game.GetComponentsInChildren<LineRenderer>().Length == 0 && game.World.GetComponentsInChildren<LineRenderer>().Length == 0, "Invented aiming arc and line rings are removed");
                    game.Pause(); pausedPosition = bullet.transform.position;
                    pauseUntil = EditorApplication.timeSinceStartup + .25; stage = 2; return;
                }
                if (stage == 2 && EditorApplication.timeSinceStartup > pauseUntil)
                {
                    Check(Vector3.Distance(bullet.transform.position, pausedPosition) < .0001f, "Pause freezes the actual Rigidbody");
                    game.Resume(); stage = 3; return;
                }
                if (stage == 3 && game.Session.shots == 1)
                {
                    Check(game.Ready, "Native hit/miss notification enables the next candy");
                    Check(float.IsFinite(bullet.transform.position.x), "Original impulse flight resolves without invalid coordinates");
                    game.Boosts.OnNewRound(3);
                    Check(game.Boosts.Current != null, "Stage three creates one original vertical ring");
                    var ring = game.Boosts.Current;
                    Check(ring.GetComponent<SphereCollider>().radius == .011f, "Ring retains its original trigger radius");
                    Check(ring.transform.Find("ring").GetComponent<MeshFilter>().sharedMesh != null, "Original ring plane mesh resolves in Unity 6");
                    Check(ring.GetComponent<Animation>()["RingHitAnim"] != null, "Original ring hit animation remains bound");
                    var sparkles = ring.GetComponentInChildren<LegacyParticles26>();
                    Check(sparkles && sparkles.Settings.oneShot && sparkles.Settings.minEmission==50 && sparkles.Settings.maxEnergy==1,
                        "Ring sparkles retain their original 50-particle burst and one-second lifetime");
                    sparkles.SetEmitting(true);
                    Check(sparkles.System.particleCount==50 && sparkles.particleMaterial.HasProperty("_TintColor"), "Recovered ring sparkle material emits a real Unity 6 particle burst");
                    game.ReturnToMenu();
                    Check(Time.timeScale == 1, "Menu restores time after gameplay");
                    game.ToggleControlMode();game.StartGame();
                    Vector2 flickStart=game.GameCamera.WorldToScreenPoint(game.CandyGhost.position);
                    Check(CandyFlickGame.CheckTouchPosition(flickStart) && !game.Player.Find("CandyTouchPlane").gameObject.activeSelf,
                        "Native flick ghost (startAnimPos) projects into the flick touch radius; touch plane is disabled");
                    Vector2 flickEnd=flickStart+Vector2.up*Screen.height*.16f;
                    game.SampleInput(TouchPhase.Began,flickStart,Vector2.zero);
                    game.SampleInput(TouchPhase.Moved,flickEnd,flickEnd-flickStart);
                    game.SampleInput(TouchPhase.Ended,flickEnd,Vector2.zero);
                    Check(UnityEngine.Object.FindObjectsByType<CandyProjectile>(FindObjectsSortMode.None).Count(c=>c.IsShot)==1,
                        "Settings-selected flick mode launches the recovered Rigidbody through real gesture input");
                    game.ReturnToMenu();
                    RestoreControlPreference();
                    File.WriteAllText("Documentation/validation.json", JsonUtility.ToJson(new Evidence { checks = checks.ToArray(), originalBinaryFixtures = 96, unity = Application.unityVersion }, true));
                    SessionState.SetBool("CandyFlickNative26Smoke", false);
                    stage = 99;
                    EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
                    EditorApplication.ExitPlaymode();
                    if (Application.isBatchMode) EditorApplication.Exit(0);
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }
        static void Fail(string message)
        {
            if (stage == 99) return; stage = 99;
            File.WriteAllText("Documentation/validation-failure.txt", message);
            RestoreControlPreference();
            SessionState.SetBool("CandyFlickNative26Smoke", false);
            Debug.LogWarning("NATIVE26_SMOKE_FAILED: " + message);
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
        static void RestoreControlPreference()
        {
            if(SessionState.GetBool("SmokeHadCameraFpsLock",false))PlayerPrefs.SetInt(CandyFlickGame.CameraFpsLockPreference,SessionState.GetInt("SmokeCameraFpsLock",0));
            else PlayerPrefs.DeleteKey(CandyFlickGame.CameraFpsLockPreference);
            if(SessionState.GetBool("SmokeHadControlMode",false))PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,SessionState.GetInt("SmokeControlMode",2));
            else PlayerPrefs.DeleteKey(CandyFlickGame.ControlModePreference);
            PlayerPrefs.Save();
        }
        [Serializable] sealed class Evidence { public string unity; public int originalBinaryFixtures; public string[] checks; }
    }
}
