using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class MainMenuArtworkValidation
    {
        const string Key="CandyFlickMainMenuArtworkTest",Folder="Documentation/MainMenuArtwork";
        static CandyFlickGame game;static CandyFlickUI ui;static int stage;static double next;
        static readonly List<string> checks=new List<string>();
        static MainMenuArtworkValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate main menu artwork")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void Click(Vector2 point)=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.8;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();AudioListener.pause=true;Application.runInBackground=true;return;}
                if(stage==1&&(float)typeof(CandyFlickUI).GetField("menuEntryAlpha",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui)>0)return;
                switch(stage++)
                {
                    case 0: game.ReturnToMenu();ValidateImports();break;
                    case 1: Capture("menu-640x1136");break;
                    case 2: Click(new Vector2(455,825));break;
                    case 3: Check(game.ScreenState==GameScreen.Settings,"Original Settings button opens settings above the new artwork");Capture("settings-640x1136");break;
                    case 4: ui.Back();break;
                    case 5: Check(game.ScreenState==GameScreen.Menu,"Settings Back returns to the layered main menu");Resolution(640,1422);break;
                    case 6: Capture("menu-640x1422");break;
                    case 7: Click(ui.MenuSkinsRect.center);break;
                    case 8: Check(game.ScreenState==GameScreen.Skins,"Skins button remains interactive on a tall display");ui.Back();break;
                    case 9: Check(game.ScreenState==GameScreen.Menu,"Skins Back returns to main menu");Resolution(640,960);break;
                    case 10: Capture("menu-640x960");break;
                    case 11: Resolution(768,1024);break;
                    case 12: Capture("menu-768x1024");break;
                    default:
                        File.WriteAllText("Documentation/main-menu-artwork-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void ValidateImports()
        {
            string[] paths={"iphone5/main_menu","iphone4/main_menu/main_bgr","ipad2/main_screen/main_background","all/logo"};
            foreach(string path in paths)
            {
                var texture=Resources.Load<Texture2D>("images/"+path);
                Check(texture&&GraphicsFormatUtility.HasAlphaChannel(texture.graphicsFormat),"Layer imports with transparency: "+path);
            }
            var logo=Resources.Load<Texture2D>("images/all/logo");
            Check(logo.width==603&&logo.height==374,"Separate logo retains original non-power-of-two dimensions");
            // Simulate physical display coordinates with both top and bottom cutouts.
            var display=new Rect(0,-40,640,1422);
            var foreground=Resources.Load<Texture2D>("images/iphone5/main_menu");
            Rect bottom=CandyFlickUI.MainMenuForegroundRect(display,foreground),top=CandyFlickUI.MainMenuLogoRect(display,0,logo);
            Check(Mathf.Abs(bottom.yMax-1382)<.01f&&bottom.height==1136,"Foreground reaches physical screen bottom beyond the usable-area boundary without vertical stretching");
            Check(top.y==20&&top.width==603&&top.height==374,"Logo remains below the top cutout at its native size and original margin");
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            string previous=SessionState.GetString(Key+"StartScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
