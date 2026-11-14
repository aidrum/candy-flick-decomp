using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class ARToggleValidation
    {
        const string Key="CandyFlickARToggleTest",Folder="Documentation/ARToggle";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference,TimeTravelSkins.Preference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static object session;static int stage;static double next,deadline;
        static CandyProjectile shot;static Vector3 shotPosition,shotVelocity;static int total,shots;static bool finishing;
        static ARToggleValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate pause AR toggle")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));PlayerPrefs.SetInt(pref,0);}PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.Save();
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void Click()=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{ui.ARToggleRect.center});
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void Preserve()
        {
            Check(ReferenceEquals(session,game.Session)&&game.Session.total==total&&game.Session.shots==shots,"Mode switch preserves current session, score and remaining candies");
            Check(game.ScreenState==GameScreen.Paused&&Time.timeScale==0&&!game.CanSimulate,"Mode switch keeps gameplay paused");
        }
        static void Layout()
        {
            Check(ui.ARLabelRect.yMax<=ui.ARToggleRect.yMin&&ui.ARLabelRect.center.x==ui.ARToggleRect.center.x,"AR label sits centered above the switch");
            Check(ui.ARToggleRect.xMin>ui.CanvasWidth*.7f&&ui.ARToggleRect.xMax<ui.CanvasWidth,"Pause switch follows the usable top-right corner");
            var style=(GUIStyle)typeof(CandyFlickUI).GetField("arToggle",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ui);
            Check(style.normal.background.name==(game.IsNonAR?"ar_off":"ar_on"),"Pause switch uses the supplied image for the active mode");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.6;
            try
            {
                if(!game)
                {
                    game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;
                    SessionState.SetInt(Key+"PlayMode",(int)VuforiaConfiguration.Instance.PlayMode.PlayModeType);
                    VuforiaConfiguration.Instance.PlayMode.PlayModeType=PlayModeType.SIMULATOR;
                    game.StartGame();game.Session.RecordUnits(true,750,0);game.Session.RecordUnits(false,750,0);game.Session.RecordUnits(true,750,0);
                    ui.SendMessage("Update");game.Pause();session=game.Session;total=game.Session.total;shots=game.Session.shots;return;
                }
                switch(stage++)
                {
                    case 0:
                        foreach(string name in new[]{"ar_on","ar_off"})
                        {var texture=Resources.Load<Texture2D>("ARToggle/"+name);Check(texture&&texture.width==172&&texture.height==172,"Original toggle image imports at 172x172: "+name);}
                        Layout();Capture("phone-ar-off");break;
                    case 1: Click();deadline=EditorApplication.timeSinceStartup+60;break;
                    case 2:
                        if(game.ar.TargetCount!=2||!VuforiaApplication.Instance.IsRunning)
                        {if(EditorApplication.timeSinceStartup>deadline)throw new Exception("AR simulator startup timed out: "+game.ar.Status);stage--;return;}
                        Check(game.IsAR&&!game.IsNonAR&&!game.NonARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.NonARPreference)==0,"Pause switch enables AR and updates the existing Non AR preference");
                        Check(!game.NonARBackground.Active&&game.GameCamera.clearFlags==CameraClearFlags.Depth,"AR enables the camera path and hides the Non AR room");
                        Preserve();Layout();Capture("phone-ar-on");break;
                    case 3: Click();break;
                    case 4:
                        Check(!game.IsAR&&game.IsNonAR&&game.NonARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.NonARPreference)==1,"Pause switch disables AR and updates the existing Non AR preference");
                        Check(game.NonARBackground.Active&&game.GameCamera.clearFlags==CameraClearFlags.SolidColor&&!game.ar.Camera.gameObject.activeInHierarchy,"Non AR restores its room and stops the capture rig");
                        Check(Mathf.Abs(game.Actor.transform.position.y-NonARStage.GroundClearance)<.0001f&&Mathf.Abs(game.World.Find("Floor").localPosition.y-NonARStage.GroundClearance)<.0001f,"Live switching preserves matching actor/floor clearance");
                        Preserve();Resolution(768,1024);break;
                    case 5: Layout();Capture("tablet-ar-off");break;
                    case 6: Click();break;
                    case 7: Preserve();Layout();Capture("tablet-ar-on");Resolution(1920,1080);break;
                    case 8: Layout();Capture("desktop-ar-on");break;
                    case 9: Click();break;
                    case 10: Preserve();Layout();Capture("desktop-ar-off");break;
                    case 11:
                        game.Resume();game.PrepareAndShootBullet(80,0,0);shot=game.World.GetComponentsInChildren<CandyProjectile>().Single(c=>c.IsShot);
                        shot.Body.useGravity=false;shot.Body.position=game.Actor.transform.position+new Vector3(0,400,400);shot.Body.linearVelocity=Vector3.up*100;break;
                    case 12: game.Pause();shotPosition=shot.Body.position;shotVelocity=shot.Body.linearVelocity;Click();break;
                    case 13:
                        Preserve();Check(shot.IsShot&&shot.Body.position==shotPosition&&shot.Body.linearVelocity==shotVelocity,"Paused in-flight candy keeps its position and velocity when enabling AR");Click();break;
                    case 14:
                        Preserve();Check(shot.IsShot&&shot.Body.position==shotPosition&&shot.Body.linearVelocity==shotVelocity,"Paused in-flight candy keeps its position and velocity when returning to Non AR");
                        game.Resume();Check(game.CanSimulate&&ReferenceEquals(session,game.Session),"Resume continues the same Non AR game after switching");game.ReturnToMenu();break;
                    case 15:
                        game.StartGame();Check(game.IsNonAR&&game.NonARBackground.Active,"Next game respects the pause toggle's saved Non AR choice");
                        game.Pause();Click();break;
                    case 16: game.ReturnToMenu();game.StartGame(true);Check(game.IsAR&&!game.IsNonAR,"AR launch respects the pause toggle's saved AR choice");game.ReturnToMenu();break;
                    default: File.WriteAllText("Documentation/ar-toggle-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Vuforia simulator and native database startup; physical camera quality is not exercised"},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            if(game)game.ReturnToMenu();VuforiaConfiguration.Instance.PlayMode.PlayModeType=(PlayModeType)SessionState.GetInt(Key+"PlayMode",0);
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            string previous=SessionState.GetString(Key+"StartScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
