using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using CandyFlick.Original26;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class TutorialModeValidation
    {
        const string Key="CandyFlickTutorialModeTest",Folder="Documentation/TutorialModes";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.GroundARPreference,"Language"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static int stage;static double next;
        static object session;
        static TutorialModeValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate tutorial play options")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetBool(Key+"HadTutorial",PlayerPrefs.HasKey("ProgramStartKey"));SessionState.SetString(Key+"Tutorial",PlayerPrefs.GetString("ProgramStartKey"));
            SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetInt(Key+"Vuforia",(int)VuforiaConfiguration.Instance.PlayMode.PlayModeType);VuforiaConfiguration.Instance.PlayMode.PlayModeType=PlayModeType.SIMULATOR;
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void Call(string name)=>typeof(CandyFlickUI).GetMethod(name,Private).Invoke(ui,null);
        static void Language(int language)
        {
            ((TextManager26)typeof(CandyFlickUI).GetField("text",Private).GetValue(ui)).ChangeLanguage(language);Call("ApplyLanguageFont");
        }
        static void OpenStart(bool fromGame=false)
        {
            if(!fromGame)game.ReturnToMenu();
            ui.OpenTutorial(false,!fromGame);ui.SetTutorialPage(CandyFlickUI.TutorialPage.Start);
        }
        static void Click(Rect rect)=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{rect.center});
        static Rect Native(string key)=>(Rect)typeof(CandyFlickUI).GetMethod("R",Private).Invoke(ui,new object[]{key,true});
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void Layout()
        {
            var rects=ui.TutorialStartRects;
            Check(rects[1].yMax<rects[2].yMin&&rects[3].yMax<rects[5].yMin&&rects[5].yMax<rects[7].yMin,"Marker pictures, image choices and dotted OR separator do not overlap at "+Screen.width+"x"+Screen.height);
            Check(rects[8].yMax<Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea),"All four arrows fit the short/tablet canvas");
            foreach(int i in new[]{2,3,7,8})Check(rects[i].xMax<=640&&rects[i].width>=80,"Arrow is inside the menu and remains tappable: "+i);
            Check(Mathf.Abs(rects[2].center.x-Native("TutorStartScreen$$GetHaveMarkerBtnRect").center.x)<.001f,"Arrow column retains its original horizontal center");
            for(int language=0;language<9;language++)
            {
                Language(language);var style=ui.TutorialStartRowStyle(rects);
                foreach(string label in ui.TutorialStartLabels)
                    Check(!string.IsNullOrEmpty(label)&&label.Split(' ').All(word=>style.CalcSize(new GUIContent(word)).x<=CandyFlickUI.TutorialStartLabelRect(rects[2]).width+.1f)&&style.CalcHeight(new GUIContent(label),CandyFlickUI.TutorialStartLabelRect(rects[2]).width)<=rects[2].height,"Complete row label fits "+PortLocalization.CatalogData().languages[language]+": "+label);
            }
            foreach(string key in new[]{"Ground detection mode","Play with AR off"})
            {
                var entry=PortLocalization.CatalogData().entries.Single(e=>e.key==key);
                Check(entry.text.Length==9&&entry.text.All(t=>!string.IsNullOrWhiteSpace(t)&&!t.Contains("?")),"New string has all nine translations: "+key);
            }
            Language(0);
        }
        static void ImageLaunch(string trigger)
        {
            Check(game.ScreenState==GameScreen.Playing&&game.IsAR&&!game.IsNonAR&&!game.NonARModeEnabled&&!game.GroundARModeEnabled&&!game.ar.GroundMode,trigger+" enables image AR from Ground + AR off");
            Check(PlayerPrefs.GetInt(CandyFlickGame.NonARPreference)==0&&PlayerPrefs.GetInt(CandyFlickGame.GroundARPreference)==0,trigger+" saves both settings");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.8;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();AudioListener.pause=true;Application.runInBackground=true;return;}
                switch(stage++)
                {
                    case 0: OpenStart();break;
                    case 1: Layout();break;
                    case 2: Capture("phone-en");break;
                    case 3: Language(8);break;
                    case 4: Capture("phone-es");break;
                    case 5: Resolution(640,960);break;
                    case 6: Layout();Language(1);break;
                    case 7: Capture("iphone4-ru");break;
                    case 8: Language(0);break;
                    case 9: Capture("iphone4-en");break;
                    case 10: Resolution(768,1024);break;
                    case 11: Layout();Language(8);break;
                    case 12: Capture("ipad-es");break;
                    case 13: Language(0);break;
                    case 14: Capture("ipad-en");break;
                    case 15: game.SetTutorialARMode(false,false,false);game.GroundSupport.EditorSupportOverride=false;game.GroundSupport.Refresh();Click(ui.TutorialStartRects[7]);break;
                    case 16: Check(CandyFlickUI.PopupActive&&game.ScreenState==GameScreen.Tutorial&&game.NonARModeEnabled&&!game.GroundARModeEnabled,"Unsupported Ground shows the native warning and leaves both settings unchanged");Capture("unsupported-ground");break;
                    case 17: Call("ClosePopup");game.GroundSupport.EditorSupportOverride=true;game.GroundSupport.Refresh();Click(ui.TutorialStartRects[7]);break;
                    case 18:
                        Check(game.ScreenState==GameScreen.Playing&&game.IsAR&&!game.NonARModeEnabled&&game.GroundARModeEnabled&&game.ar.GroundMode,"Ground arrow enables AR and selects Ground");
                        game.ReturnToMenu();game.SetTutorialARMode(true,true,false);OpenStart();break;
                    case 19: Click(ui.TutorialStartRects[8]);break;
                    case 20:
                        Check(game.IsNonAR&&game.NonARModeEnabled&&!game.IsAR&&game.GroundARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.GroundARPreference)==1,"AR off arrow disables AR while retaining Ground preference");
                        game.ReturnToMenu();game.SetTutorialARMode(false,false,false);OpenStart();break;
                    case 21: Click(ui.TutorialStartRects[8]);break;
                    case 22:
                        Check(game.IsNonAR&&!game.GroundARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.GroundARPreference)==0,"AR off arrow retains Image preference too");
                        game.ReturnToMenu();game.SetTutorialARMode(false,true,false);OpenStart();break;
                    case 23: Click(ui.TutorialStartRects[2]);break;
                    case 24: ImageLaunch("Already got one");game.ReturnToMenu();game.SetTutorialARMode(false,true,false);OpenStart();break;
                    case 25: Click(ui.TutorialStartRects[3]);break;
                    case 26:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.CreateMarker,"Get image opens the existing download/print choices");
                        ui.SetTutorialPage(CandyFlickUI.TutorialPage.Download);break;
                    case 27: Click(Native("TutorDownloadScreen$$GetOkRect"));break;
                    case 28: ImageLaunch("Get image download Play");game.ReturnToMenu();game.SetTutorialARMode(false,true,false);OpenStart();break;
                    case 29: ui.SetTutorialPage(CandyFlickUI.TutorialPage.Print);break;
                    case 30: Click(Native("TutorPrintScreen$$GetOkBtnRect"));break;
                    case 31: ImageLaunch("Get image print Play");session=game.Session;OpenStart(true);break;
                    case 32: Click(ui.TutorialStartRects[8]);break;
                    case 33: Check(game.IsNonAR&&!game.GroundARModeEnabled&&ReferenceEquals(session,game.Session),"In-game Help switches AR off without resetting the current session or changing Image preference");OpenStart(true);break;
                    case 34: Click(ui.TutorialStartRects[7]);break;
                    case 35: Check(game.IsAR&&game.GroundARModeEnabled&&ReferenceEquals(session,game.Session),"In-game Help switches to Ground AR without resetting the current session");break;
                    default: File.WriteAllText("Documentation/tutorial-mode-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Editor UI input and Vuforia simulator; physical ground tracking requires supported hardware."},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}
            if(SessionState.GetBool(Key+"HadTutorial",false))PlayerPrefs.SetString("ProgramStartKey",SessionState.GetString(Key+"Tutorial",""));else PlayerPrefs.DeleteKey("ProgramStartKey");PlayerPrefs.Save();
            AudioListener.pause=SessionState.GetBool(Key+"Audio",false);VuforiaConfiguration.Instance.PlayMode.PlayModeType=(PlayModeType)SessionState.GetInt(Key+"Vuforia",0);
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
