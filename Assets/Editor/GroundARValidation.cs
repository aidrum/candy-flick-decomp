using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using CandyFlick.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class GroundARValidation
    {
        const string Key="CandyFlickGroundARTest", Folder="Documentation/GroundAR";
        static readonly string[] prefs={CandyFlickGame.GroundARPreference,CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference,"IsMusic"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game; static CandyFlickUI ui;
        static int stage; static double next,deadline; static bool finishing; static Action afterCapture;
        static GroundARValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate Ground AR mode")]
        public static void Run()=>Start(true);
        public static void RunWithoutCaptures()=>Start(false);
        static void Start(bool capture)
        {
            SessionState.SetBool(Key+"Capture",capture);
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            ProjectBuilder.ConfigureVuforia();
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));PlayerPrefs.DeleteKey(pref);}
            SessionState.SetBool(Key+"HadStart",PlayerPrefs.HasKey("ProgramStartKey"));SessionState.SetString(Key+"Start",PlayerPrefs.GetString("ProgramStartKey",""));
            SessionState.SetInt(Key+"PlayMode",(int)VuforiaConfiguration.Instance.PlayMode.PlayModeType);
            SessionState.SetBool(Key+"AudioPause",AudioListener.pause);
            var config=AssetDatabase.LoadAssetAtPath<VuforiaConfiguration>("Assets/Resources/VuforiaConfiguration.asset");
            config.PlayMode.PlayModeType=VuforiaConfiguration.Instance.PlayMode.PlayModeType=PlayModeType.SIMULATOR;
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            PlayerPrefs.Save();
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void CaptureThen(string name, Action nextAction)
        {
            if(SessionState.GetBool(Key+"Capture",true))ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png"); afterCapture=nextAction;
        }
        static void More(){game.Show(GameScreen.Settings);typeof(CandyFlickUI).GetField("settingsPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ui,"MoreOptions");}
        static void Layout()
        {
            var rows=new[]{ui.MoreControlsRect,ui.MoreFpsRect,ui.MoreHideCandyRect,ui.MoreMusicRect,ui.MoreARModeRect};
            Rect back=(Rect)Call(ui,"R","SettingScreen$$GetBackBtnRect",true);
            Check(rows.All(r=>r.x>=0&&r.xMax<=640&&r.height>=60)&&rows.Last().yMax<back.y,"All five options fit above Back at "+Screen.width+"x"+Screen.height);
            Check(Enumerable.Range(1,4).All(i=>rows[i-1].yMax<rows[i].y),"More Options rows do not overlap");
            Check(ui.MoreARModeRect.yMax+70<back.y,"Device support explanation fits above Back");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.7;
            try
            {
                if(afterCapture!=null){var action=afterCapture;afterCapture=null;action();return;}
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;VuforiaConfiguration.Instance.PlayMode.PlayModeType=PlayModeType.SIMULATOR;return;}
                switch(stage++)
                {
                    case 0:
                        game.GroundSupport.EditorSupportOverride=false;game.GroundSupport.Refresh();
                        game.ToggleGroundARMode();Check(!game.GroundARModeEnabled&&!PlayerPrefs.HasKey(CandyFlickGame.GroundARPreference),"Unsupported devices cannot enable Ground mode or save it accidentally");
                        game.GroundSupport.EditorSupportOverride=true;game.GroundSupport.Refresh();game.ToggleGroundARMode();
                        game.GroundSupport.EditorSupportOverride=false;game.GroundSupport.Refresh();game.ToggleGroundARMode();
                        Check(!game.GroundARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.GroundARPreference)==0,"An unsupported device can always switch an existing Ground selection back to Image");
                        Check(typeof(CandyFlickUI).GetProperty("MoreNonARRect")==null,"More Options no longer exposes the redundant Non AR toggle");
                        game.GroundSupport.EditorSupportOverride=true;game.GroundSupport.Refresh();
                        Check(game.InGameMusicEnabled&&!game.GroundARModeEnabled&&!game.NonARModeEnabled,"Fresh settings enable gameplay music and default to Image AR with Non AR off");
                        Check(ui.ARModeLabel=="AR Mode: Image","Default label is exactly AR Mode: Image");
                        Check(VuforiaConfiguration.Instance.DeviceTracker.ARCoreRequirementSetting==VuforiaConfiguration.DeviceTrackerConfiguration.ARCoreRequirement.OPTIONAL&&VuforiaConfiguration.Instance.DeviceTracker.AutoImportArcoreSetting,"Build configuration includes ARCore as optional");
                        More();break;
                    case 1: Layout();CaptureThen("phone-image-default",game.ToggleGroundARMode);break;
                    case 2:
                        Check(game.GroundARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.GroundARPreference,-1)==1&&ui.ARModeLabel=="AR Mode: Ground","AR Mode selection saves Ground and updates its label");
                        CaptureThen("phone-ground",()=>Resolution(768,1024));break;
                    case 3: Layout();CaptureThen("tablet-ground",()=>Resolution(1920,1080));break;
                    case 4: Layout();CaptureThen("wide-ground",()=>Resolution(640,1136));break;
                    case 5: game.ToggleGroundARMode();break;
                    case 6:
                        Check(!game.GroundARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.GroundARPreference,-1)==0&&ui.ARModeLabel=="AR Mode: Image","AR Mode selection switches back to Image and updates its label");
                        ui.Back();deadline=EditorApplication.timeSinceStartup+8;break;
                    case 7:
                        if(game.ScreenState!=GameScreen.Settings||ui.SettingsPage!=null)
                        {if(EditorApplication.timeSinceStartup>deadline)throw new Exception("More Options Back transition timed out");stage--;return;}
                        Check(game.ScreenState==GameScreen.Settings&&ui.SettingsPage==null,"More Options Back returns to main Settings");
                        game.ToggleGroundARMode();game.ReturnToMenu();Resolution(1920,1080);PlayerPrefs.DeleteKey("ProgramStartKey");ui.MenuPlay();deadline=EditorApplication.timeSinceStartup+60;break;
                    case 8:
                        if(!VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Ground camera startup timed out: "+game.ar.Status);stage--;return;}
                        game.ar.SendMessage("OnCameraStateUpdated");
                        Check(game.IsAR&&game.ar.GroundMode&&game.ScreenState==GameScreen.Playing&&!PlayerPrefs.HasKey("ProgramStartKey"),"Ground Play starts AR directly without image tutorial or consuming its first-run flag");
                        Check(game.ar.TargetCount==0,"Ground startup does not load or require the image database");
                        Check(VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled&&VuforiaBehaviour.Instance.WorldCenterMode==WorldCenterMode.FIRST_TARGET,"Singleplayer Ground retains positional tracking and its original first-target world frame");
                        Check(!game.ar.Tracking&&!game.CanSimulate&&!game.Ready&&Time.timeScale==0,"Gameplay and physics wait for a tracked ground anchor");
                        Check(!game.ar.PlaceGround()&&!game.ar.GroundPlaced,"Unsupported simulator cannot place content at an invented camera-relative point");
                        Check(game.ar.Status.Contains("supported phone"),"Ground scan explains the simulator limitation ("+game.ar.Status+")");
                        var focus=(CandyFlick.Original26.LegacyGUITexture26)typeof(CandyFlickUI).GetField("searchFocus",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                        var sticker=(CandyFlick.Original26.LegacyGUITexture26)typeof(CandyFlickUI).GetField("searchSticker",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                        Check(sticker.gameObject.activeSelf&&!focus.gameObject.activeSelf,"Ground scanning reuses the yellow note without image-target focusing art");
                        Check(sticker.texture==Resources.Load<Texture2D>("images/ipad2/search_target_screen/sticker"),"Ground note uses the same active-device texture as Image mode");
                        Check(game.World.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Unplaced world stays hidden while live camera can render");
                        GroundTapChecks();CaptureThen("wide-ground-scanning",()=>Resolution(640,1136));break;
                    case 9:
                        CaptureThen("phone-ground-scanning",()=>{game.Pause();GroundButtonFixture();});break;
                    case 10:
                        GroundButtonChecks();CaptureThen("phone-ground-pause",()=>{GroundLossChecks();game.ToggleARMode();});break;
                    case 11:
                        Check(game.IsNonAR&&!game.IsAR&&game.NonARBackground.Active&&!game.ar.Camera.gameObject.activeInHierarchy,"Pause AR switch can leave Ground for the Non AR room and stop capture");
                        Check(game.GroundARModeEnabled,"Non AR preserves the chosen AR backend");game.ToggleARMode();deadline=EditorApplication.timeSinceStartup+30;break;
                    case 12:
                        if(!VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Ground restart timeout");stage--;return;}
                        Check(game.IsAR&&game.ar.GroundMode&&!game.IsNonAR&&game.ScreenState==GameScreen.Paused,"Pause switch restores Ground while keeping the same paused game");
                        game.ReturnToMenu();game.ToggleGroundARMode();game.StartGame(true);deadline=EditorApplication.timeSinceStartup+30;break;
                    case 13:
                        if(game.ar.TargetCount!=2||!VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Image regression startup timeout: "+game.ar.Status);stage--;return;}
                        Check(!game.ar.GroundMode&&!VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled,"Returning to Image disables positional tracking and loads both original targets");
                        Check(!ARMarkerMode.IsImageVisible(Vuforia.Status.EXTENDED_TRACKED)&&ARMarkerMode.IsImageVisible(Vuforia.Status.TRACKED),"Image retains its original visible-card tracking policy");
                        Geometry();NativeLanCalibrationTargets();
                        game.Show(GameScreen.Marker);Call(game.ar,"RefreshImageMenuCapture");deadline=EditorApplication.timeSinceStartup+30;break;
                    case 14:
                        if(VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Show Image did not stop capture");stage--;return;}
                        Check(!VuforiaBehaviour.Instance.enabled&&game.ar.Camera.gameObject.activeInHierarchy&&game.ar.TargetCount==2,"Normal Show Image stops the native capture engine while retaining the initialized rig and targets");
                        game.Resume();Call(game.ar,"RefreshImageMenuCapture");deadline=EditorApplication.timeSinceStartup+30;break;
                    case 15:
                        if(!VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Closing Show Image did not restart capture");stage--;return;}
                        Check(VuforiaBehaviour.Instance.enabled&&game.ar.TargetCount==2,"Closing Show Image restarts native capture with both existing image observers");
                        game.Show(GameScreen.Marker);Call(game.ar,"RefreshImageMenuCapture");game.ReturnToMenu();
                        Check(VuforiaBehaviour.Instance.enabled&&!game.ar.Camera.gameObject.activeInHierarchy&&!VuforiaApplication.Instance.IsRunning,"Leaving through the covered menu resets capture state without restarting the inactive camera");
                        game.StartGame(true);deadline=EditorApplication.timeSinceStartup+30;break;
                    case 16:
                        if(!VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("New game after Show Image did not restart capture");stage--;return;}
                        Check(VuforiaBehaviour.Instance.enabled&&game.ar.TargetCount==2,"A new normal game starts capture after leaving Show Image directly");
                        game.ReturnToMenu();
                        game.ToggleInGameMusic();Check(!game.InGameMusicEnabled&&PlayerPrefs.GetInt(CandyFlickGame.InGameMusicPreference,-1)==0,"Explicit gameplay music off remains saved");
                        // Restore test-owned preferences afterward; Reset has its own existing full-pref suite.
                        File.WriteAllText("Documentation/ground-ar-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Editor UI, Vuforia simulator, lifecycle and pose conversion. Real surface hits, ARCore installation and physical anchoring require an Android phone; no player build made."},true));Finish(null);break;
                }
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static void NativeLanCalibrationTargets()
        {
            game.GroundSupport.EditorSupportOverride=null;
            Call(game.GroundSupport,"Set",GroundSupportState.ServiceRequired,"Missing AR service at startup");game.GroundSupport.Refresh();
            Check(game.GroundSupport.State==GroundSupportState.ServiceRequired&&game.GroundSupport.Reason=="Missing AR service at startup","Missing ARCore service result remains cached instead of rechecking");
            Call(game.GroundSupport,"Set",GroundSupportState.Unsupported,"Unsupported startup device");game.GroundSupport.Refresh();
            Check(game.GroundSupport.State==GroundSupportState.Unsupported&&game.GroundSupport.Reason=="Unsupported startup device","Unsupported device result remains cached for this app session");game.GroundSupport.EditorSupportOverride=true;game.GroundSupport.Refresh();
            // Use the installed SDK's real database observers, not the LAN test's
            // EditorCalibrationOverride. Simulator cannot recognize a phone image,
            // but it does exercise observer activation and the readiness gates.
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var active=typeof(LanMultiplayer).GetField("activeMatch",flags);
            var state=typeof(LanMultiplayer).GetField("remoteState",flags);
            var reference=typeof(ARMarkerMode).GetField("lanReference",flags);
            var seen=typeof(ARMarkerMode).GetField("lanReferenceSeen",flags);
            bool previousActive=(bool)active.GetValue(game.LAN);object previousState=state.GetValue(game.LAN);
            var network=typeof(LanMultiplayer).GetField("network",flags);var roomGround=typeof(LanMultiplayer).GetField("roomGround",flags);
            object previousNetwork=network.GetValue(game.LAN),previousRoomGround=roomGround.GetValue(game.LAN);LanTransport host=null;
            var placement=typeof(ARMarkerMode).GetField("lanPlacement",flags);GameObject pending=null,placed=null;
            try
            {
                var snapshot=new LanSnapshot{groundMode=true,ground=new LanGroundState{generation=73,markerWidth=.06f,originReady=false}};
                active.SetValue(game.LAN,true);state.SetValue(game.LAN,snapshot);game.ar.Begin();
                var targets=(List<ImageTargetBehaviour>)typeof(ARMarkerMode).GetField("targets",flags).GetValue(game.ar);
                ImageTargetBehaviour newer=targets.Find(t=>t.TargetName==LanMultiplayer.CalibrationTarget),older=targets.Find(t=>t.TargetName!=LanMultiplayer.CalibrationTarget);
                Check(VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled&&VuforiaBehaviour.Instance.WorldCenterMode==WorldCenterMode.DEVICE,"Native LAN client keeps positional tracking in the gravity-aligned device frame while scanning an upright image");
                VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled=false;
                Call(game.ar,"UpdateLanGroundCalibration");
                Check(newer.enabled&&!older.enabled,"Native client enables the newer image observer before device tracking or host origin readiness");
                Check(Mathf.Abs(newer.GetSize().x-.06f)<.00001f,"Native calibration observer uses the host screen's physical image width");
                newer.enabled=false;snapshot.ground.originReady=true;Call(game.ar,"UpdateLanGroundCalibration");
                Check(newer.enabled,"Host readiness updates re-enable the native client observer without a generation change");
                pending=new GameObject("Pending LAN anchor gate fixture");pending.SetActive(false);reference.SetValue(game.ar,pending.AddComponent<AnchorBehaviour>());seen.SetValue(game.ar,false);
                Call(game.ar,"ConfigureLanCalibrationTargets");
                Check(newer.enabled,"Allocating an untracked reference anchor does not disable native image detection");
                seen.SetValue(game.ar,true);Call(game.ar,"ConfigureLanCalibrationTargets");
                Check(!newer.enabled,"Native image observer detaches only after the reference has reported tracked");
                host=new LanTransport();Check(host.Host(),"Native host world-frame fixture opens a local room");network.SetValue(game.LAN,host);roomGround.SetValue(game.LAN,true);
                Call(game.ar,"ConfigureTrackingMode");
                Check(game.LAN.IsHost&&game.ar.GroundMode&&VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled&&VuforiaBehaviour.Instance.WorldCenterMode==WorldCenterMode.DEVICE,"Native LAN host retains a gravity-aligned world for surface hits after screen calibration");
                Check(targets.All(t=>!t.enabled),"Host surface finding keeps calibration image observers disabled");
                placed=new GameObject("Retained placement lifecycle fixture");placed.SetActive(false);var placedAnchor=placed.AddComponent<AnchorBehaviour>();placement.SetValue(game.ar,placedAnchor);
                active.SetValue(game.LAN,false);Call(game,"ReturnToMenu",true);
                Check(game.ar.RetainingLanGround&&ReferenceEquals(reference.GetValue(game.ar),pending.GetComponent<AnchorBehaviour>())&&ReferenceEquals(placement.GetValue(game.ar),placedAnchor)&&VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled&&game.ar.Camera.gameObject.activeInHierarchy,"Returning to a retained Ground room keeps its reference, placement, capture and device tracking alive");
                active.SetValue(game.LAN,true);game.StartGame(true);
                Check(!game.ar.RetainingLanGround&&ReferenceEquals(reference.GetValue(game.ar),pending.GetComponent<AnchorBehaviour>())&&ReferenceEquals(placement.GetValue(game.ar),placedAnchor),"Starting a Ground rematch preserves existing native anchor objects");
                Call(game.ar,"SyncLanPlacement",new LanGroundState{placed=false,placement=2});
                Check(placement.GetValue(game.ar)==null&&ReferenceEquals(reference.GetValue(game.ar),pending.GetComponent<AnchorBehaviour>()),"Changing only Ground placement removes the placement anchor while preserving the calibration reference");
            }
            finally
            {
                reference.SetValue(game.ar,null);placement.SetValue(game.ar,null);seen.SetValue(game.ar,false);if(placed)UnityEngine.Object.Destroy(placed);if(pending)UnityEngine.Object.Destroy(pending);
                game.ar.End();network.SetValue(game.LAN,previousNetwork);roomGround.SetValue(game.LAN,previousRoomGround);host?.Dispose();active.SetValue(game.LAN,previousActive);state.SetValue(game.LAN,previousState);game.ar.Begin();
            }
            Check(!game.ar.GroundMode&&!VuforiaBehaviour.Instance.DevicePoseBehaviour.enabled&&VuforiaBehaviour.Instance.WorldCenterMode==WorldCenterMode.DEVICE,"Native observer gate test restores normal Image tracking");
        }
        static void GroundTapChecks()
        {
            Rect pause=(Rect)Call(ui,"R","GamePlayScreen$$GetPauseRect",true);
            float height=Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea);
            foreach(float x in new[]{2f,ui.CanvasWidth/2,ui.CanvasWidth-2})
            foreach(float y in new[]{2f,height/2,height-2})
            {
                Vector2 point=new Vector2(x,y);
                Check(ui.CanTapGroundPlacement(point)==!pause.Contains(point),"Placement accepts screen point outside buttons: "+point);
            }
            Check(!ui.CanTapGroundPlacement(pause.center),"Pause button is excluded from placement taps");
            ui.ShowPopupWithMessageKey("PHOTO_SAVED");
            Check(!ui.CanTapGroundPlacement(new Vector2(20,20)),"Popup blocks placement input");Call(ui,"ClosePopup");
        }
        static void GroundButtonFixture()
        {
            // Unconfigured inactive component for UI only, never a simulated surface
            // or native anchor. Real hit testing remains a physical-phone limitation.
            var obj=new GameObject("Ground pause UI fixture");obj.SetActive(false);
            typeof(ARMarkerMode).GetField("groundAnchor",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game.ar,obj.AddComponent<AnchorBehaviour>());
        }
        static void GroundButtonChecks()
        {
            Rect rect=ui.GroundRepositionRect;var style=(GUIStyle)Call(ui,"Main",1);
            Texture2D image=style.normal.background;
            Check(Mathf.Abs(rect.width/rect.height-(float)image.width/image.height)<.001f,"Place Again matches the settings button texture aspect without stretching");
            Check(rect.center.x==ui.CanvasWidth/2&&!rect.Overlaps(ui.ARToggleRect),"Place Again fits in pause without overlapping the AR switch");
            Check(!ui.CanTapGroundPlacement(new Vector2(20,20)),"Paused game blocks placement taps");
        }
        static void GroundLossChecks()
        {
            var session=game.Session;int shots=game.Session.shots;
            typeof(ARMarkerMode).GetField("groundAnchorWasTracked",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game.ar,true);
            Call(game.ar,"SetTracking",true);Call(game.ar,"LoseTracking");
            Check(!game.ar.GroundPlaced&&!game.ar.Tracking&&!game.ar.GroundPointAvailable,"Tracking loss clears the old ground anchor and returns to placement");
            Check(game.ar.Status=="Move your phone slowly to find a flat surface.","Tracking loss uses the same new-place prompt as Place Again");
            Check(ReferenceEquals(session,game.Session)&&game.Session.shots==shots&&game.ScreenState==GameScreen.Paused,"Losing placement keeps the current game and pause state");
            Check(!(bool)typeof(ARMarkerMode).GetField("groundAnchorWasTracked",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game.ar),"Lost anchor history is cleared so old placement cannot resume automatically");
        }
        static void Geometry()
        {
            var utilities=Type.GetType("Vuforia.EditorClasses.BuildGradleUtilities, Vuforia.Unity.Editor",true);
            string gradle=(string)utilities.GetMethod("AddARCoreDependency",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"plugins {}\ndependencies {\n}\n"});
            Check(gradle.Contains("com.google.ar:core:1.45.0"),"Installed SDK Gradle integration includes its ARCore 1.45.0 library in player builds");
            Check(ARMarkerMode.IsHorizontalSurface(new Pose(new Vector3(2,0,3),Quaternion.Euler(0,120,0)))&&!ARMarkerMode.IsHorizontalSurface(new Pose(Vector3.zero,Quaternion.Euler(90,0,0))),"Surface validation accepts horizontal floors and rejects walls");
            Check(!ARMarkerMode.IsHorizontalSurface(new Pose(new Vector3(float.NaN,0,0),Quaternion.identity)),"Invalid surface poses are rejected");
            Check(ARMarkerMode.IsGroundTracked(Vuforia.Status.EXTENDED_TRACKED)&&!ARMarkerMode.IsGroundTracked(Vuforia.Status.LIMITED)&&!ARMarkerMode.IsGroundTracked(Vuforia.Status.NO_POSE),"Ground accepts stable positional tracking and suspends limited/lost poses");
            Pose anchor=new Pose(new Vector3(2,.7f,-3),Quaternion.Euler(0,73,0));
            Vector3 expected=new Vector3(300,500,800);Quaternion rotation=Quaternion.Euler(23,210,0);
            Pose camera=new Pose(anchor.position+anchor.rotation*(expected/1000),anchor.rotation*rotation);
            Pose native=ARMarkerMode.NativeCameraPose(anchor,camera);
            Check(Vector3.Distance(native.position,expected)<.002f&&Quaternion.Angle(native.rotation,rotation)<.01f,"Ground anchor converts meters and world yaw to the original 1000-unit gameplay frame");
            game.SetTrackedView(native.position,native.rotation,game.GameCamera.projectionMatrix,true);
            Check(Vector3.Distance(game.Player.position,expected)<.002f,"First placed/recovered view snaps directly to the anchored pose");
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            if(game)game.ReturnToMenu();
            var config=AssetDatabase.LoadAssetAtPath<VuforiaConfiguration>("Assets/Resources/VuforiaConfiguration.asset");
            config.PlayMode.PlayModeType=VuforiaConfiguration.Instance.PlayMode.PlayModeType=(PlayModeType)SessionState.GetInt(Key+"PlayMode",0);
            EditorUtility.SetDirty(config);AssetDatabase.SaveAssets();
            AudioListener.pause=SessionState.GetBool(Key+"AudioPause",false);
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}
            if(SessionState.GetBool(Key+"HadStart",false))PlayerPrefs.SetString("ProgramStartKey",SessionState.GetString(Key+"Start",""));else PlayerPrefs.DeleteKey("ProgramStartKey");PlayerPrefs.Save();
            string previous=SessionState.GetString(Key+"StartScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
