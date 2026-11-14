using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class DeviceUIValidation
    {
        const string Key="CandyFlickDeviceUITest",Folder="Documentation/DeviceUI";
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly Vector2Int[] sizes={new Vector2Int(640,1422),new Vector2Int(640,960),new Vector2Int(768,1024),new Vector2Int(1920,1080)};
        static readonly string[] pages={"menu","settings","more-options","skins","help-welcome","help-start","help-create","help-download","help-print","gameplay","pause","results","marker-options","marker","popup","credits","model-credits","search"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static int resolution,page,phase;static double next;
        static DeviceUIValidation(){resolution=SessionState.GetInt(Key+"StartRes",0);if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void WidePointer(){SessionState.SetInt(Key+"StartRes",3);Run();}
        [MenuItem("Candy Flick/Validate device UI layouts")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            SaveInt(CandyFlickGame.NonARPreference);PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);
            SessionState.SetBool(Key+"HadTutorial",PlayerPrefs.HasKey("ProgramStartKey"));SessionState.SetString(Key+"Tutorial",PlayerPrefs.GetString("ProgramStartKey",""));
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            SetResolution(sizes[0]);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void SaveInt(string pref){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
        static void SetResolution(Vector2Int size)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{size.x,size.y});
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static Rect R(string name,bool bottom=false)=>(Rect)typeof(CandyFlickUI).GetMethod("R",Private).Invoke(ui,new object[]{name,bottom});
        static void Click(Vector2 point)=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        static double transitionDeadline;static object activeSession;
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.45;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;return;}
                if(phase==0)
                {
                    if(resolution==sizes.Length){game.StartGame();activeSession=game.Session;SetResolution(sizes[0]);phase=20;return;}
                    SetResolution(sizes[resolution]);game.ReturnToMenu();phase=1;return;
                }
                if(phase==1){Prepare(pages[page]);phase=2;return;}
                if(phase==2)
                {
                    if(page==0)
                    {
                        float alpha=(float)typeof(CandyFlickUI).GetField("menuEntryAlpha",Private).GetValue(ui);if(alpha>0)return;
                        ValidateProfile();
                    }
                    if(pages[page]=="gameplay"||pages[page]=="pause")ValidateGameplayWidth();
                    ScreenCapture.CaptureScreenshot(Folder+"/"+sizes[resolution].x+"x"+sizes[resolution].y+"-"+pages[page]+".png");
                    checks.Add(sizes[resolution]+": "+pages[page]+" rendered without runtime errors");
                    page++;phase=page==pages.Length?3:1;return;
                }
                var transition=(CandyFlick.Original26.ScreenTransition26)typeof(CandyFlickUI).GetField("transition",Private).GetValue(ui);
                if(transition.Waiting)
                {
                    if(transitionDeadline==0)transitionDeadline=EditorApplication.timeSinceStartup+10;
                    if(EditorApplication.timeSinceStartup>transitionDeadline)throw new Exception("Native GUI transition timed out at "+sizes[resolution]+" phase "+phase);
                    return;
                }
                transitionDeadline=0;
                switch(phase++)
                {
                    case 3: game.ReturnToMenu();typeof(CandyFlickGame).GetProperty("IsAR").SetValue(game,false);break;
                    case 4: Click(R("MainScreen$$GetSettingRect").center);break;
                    case 5: Check(game.ScreenState==GameScreen.Settings,"Settings pointer maps correctly at "+sizes[resolution]);Click(R("SettingScreen$$GetBackBtnRect",true).center);break;
                    case 6: Check(game.ScreenState==GameScreen.Menu,"Back pointer maps correctly at "+sizes[resolution]);game.StartGame();break;
                    case 7: Click(R("GamePlayScreen$$GetPauseRect",true).center);break;
                    case 8: Check(game.ScreenState==GameScreen.Paused,"Edge-anchored Pause button works at "+sizes[resolution]);Click(R("GamePlayScreen$$GetPauseRect",true).center);break;
                    case 9: Check(game.ScreenState==GameScreen.Playing,"Edge-anchored Resume button works at "+sizes[resolution]);Click(R("GamePlayScreen$$GetPauseRect",true).center);break;
                    case 10: Check(game.ScreenState==GameScreen.Paused,"Pause can be opened again at "+sizes[resolution]);Click(R("GamePlayScreen$$GetHelpRect",true).center);break;
                    case 11: Check(game.ScreenState==GameScreen.Tutorial&&ui.CanvasWidth==640,"In-game Help opens a bounded tutorial menu at "+sizes[resolution]);ui.Back();break;
                    case 12: Check(game.ScreenState==GameScreen.Playing,"Closing Help restores the full-width gameplay layout at "+sizes[resolution]);Click(R("GamePlayScreen$$GetPauseRect",true).center);break;
                    case 13: Check(game.ScreenState==GameScreen.Paused,"Pause layout returns after Help at "+sizes[resolution]);Click(R("GamePlayScreen$$GetMenuRect",true).center);break;
                    case 14: Check(game.ScreenState==GameScreen.Menu&&ui.CanvasWidth==640,"Pause Menu button restores the bounded main menu at "+sizes[resolution]);resolution++;page=phase=0;break;
                    case 20: Check(ui.ArtworkFamily=="iphone5"&&ReferenceEquals(activeSession,game.Session),"Live gameplay keeps its session when resizing to a tall phone");SetResolution(sizes[2]);break;
                    case 21: Check(ui.ArtworkFamily=="ipad2"&&ReferenceEquals(activeSession,game.Session),"Live gameplay swaps to tablet assets and layout without restarting the session");SetResolution(sizes[3]);break;
                    case 22: Check(ui.ArtworkFamily=="ipad2"&&ReferenceEquals(activeSession,game.Session)&&game.ScreenState==GameScreen.Playing,"Live gameplay retains the tablet profile and session after widening to PC");
                        File.WriteAllText("Documentation/device-ui-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Prepare(string page)
        {
            typeof(CandyFlickGame).GetProperty("IsAR").SetValue(game,false);
            typeof(CandyFlickUI).GetField("settingsPage",Private).SetValue(ui,null);
            typeof(CandyFlickUI).GetMethod("ClosePopup",Private).Invoke(ui,null);
            if(page.StartsWith("help-"))
            {
                string[] names={"help-welcome","help-start","help-create","help-download","help-print"};
                game.Show(GameScreen.Tutorial);typeof(CandyFlickUI).GetProperty("CurrentTutorialPage").SetValue(ui,(CandyFlickUI.TutorialPage)Array.IndexOf(names,page));return;
            }
            switch(page)
            {
                case "menu":game.ReturnToMenu();break;
                case "settings":game.Show(GameScreen.Settings);break;
                case "more-options":case "credits":case "model-credits":game.Show(GameScreen.Settings);typeof(CandyFlickUI).GetField("settingsPage",Private).SetValue(ui,page=="more-options"?"MoreOptions":page=="credits"?"About":"ModelCredits");break;
                case "skins":game.Show(GameScreen.Skins);break;
                case "gameplay":game.StartGame();break;
                case "pause":game.Pause();break;
                case "results":game.Show(GameScreen.Results);break;
                case "marker-options":game.Show(GameScreen.TargetOptions);break;
                case "marker":game.Show(GameScreen.Marker);break;
                case "popup":game.Show(GameScreen.Menu);ui.ShowPopupWithMessageKey("PHOTO_SAVED");break;
                case "search":game.StartGame();typeof(CandyFlickGame).GetProperty("IsAR").SetValue(game,true);break;
            }
        }
        static void ValidateGameplayWidth()
        {
            Rect safe=Screen.safeArea;float scale=CandyFlickUI.CanvasScale(safe);
            Vector2 origin=CandyFlickUI.CanvasOrigin(safe,Screen.height,game.ScreenState);
            Check(Mathf.Abs(ui.CanvasWidth*scale-safe.width)<.01f&&Mathf.Abs(origin.x-safe.x)<.01f,"Gameplay and pause canvas spans the usable width at "+sizes[resolution]);
            Rect candy=R("ScoresGPView$$GetCandiesRect"),total=R("ScoresGPView$$GetTotalScoresRect"),possible=R("ScoresGPView$$GetPossibleScoresRect");
            Check(candy.xMin*scale<safe.width*.1f&&(ui.CanvasWidth-total.xMax)*scale<safe.width*.1f,"Candy counter and total score stay at opposite screen edges at "+sizes[resolution]);
            Check(Mathf.Abs(possible.center.x-ui.CanvasWidth/2)<1,"Possible score stays at the screen center at "+sizes[resolution]);
            Check(R("GamePlayScreen$$GetPauseRect",true).center.x>ui.CanvasWidth*.9f,"Pause/resume control follows the right edge at "+sizes[resolution]);
        }
        static void ValidateProfile()
        {
            string expected=resolution==0?"iphone5":resolution==1?"iphone4":"ipad2";
            Check(ui.ArtworkFamily==expected,"Correct device profile at "+sizes[resolution]);
            var skin=(GUISkin)typeof(CandyFlickUI).GetField("gameSkin",Private).GetValue(ui);
            Check(skin.name.StartsWith(expected=="ipad2"?"iPad2":expected=="iphone4"?"iPhone4":"iPhone5"),"Device-specific gameplay GUI skin at "+sizes[resolution]);
            if(expected=="ipad2")
            {
                var actual=R("TutorDownloadScreen$$GetAppStoreRect");
                Check(Vector2.Distance(actual.position,new Vector2(135,202f*640/768))<.01f,"Tablet download layout matches recovered 768x1024 ARM coordinates");
                Check(AssetDatabase.GetAssetPath(skin.GetStyle("pause").normal.background).Contains("/ipad2/"),"Tablet pause style uses recovered tablet artwork");
            }
            var safe=Screen.safeArea;float scale=CandyFlickUI.CanvasScale(safe);
            Check(640*scale<=safe.height*.75f+.1f,"Menu UI width stays within 3:4 at "+sizes[resolution]);
            Check(Mathf.Abs(CandyFlickUI.CanvasScale(new Rect(0,0,1920,1080))-CandyFlickUI.CanvasScale(new Rect(0,0,810,1080)))<.0001f,"Wider displays keep tablet UI size instead of growing with width");
            Check(Mathf.Abs(CandyFlickUI.CanvasScale(new Rect(0,0,640,1422))-1)<.0001f,"Taller phone displays retain width scaling and extra vertical space");
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            string pref=CandyFlickGame.NonARPreference;if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);
            if(SessionState.GetBool(Key+"HadTutorial",false))PlayerPrefs.SetString("ProgramStartKey",SessionState.GetString(Key+"Tutorial",""));else PlayerPrefs.DeleteKey("ProgramStartKey");PlayerPrefs.Save();
            string previous=SessionState.GetString(Key+"StartScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);SessionState.SetInt(Key+"StartRes",0);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
