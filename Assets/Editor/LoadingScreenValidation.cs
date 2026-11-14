using System;
using System.Collections.Generic;
using System.IO;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class LoadingScreenValidation
    {
        const string Key="CandyFlickLoadingTest",Folder="Documentation/Loading";
        static int stage;static double deadline;static float origin,next;
        static ZeptoLabLoadingScreen loader;static CandyFlickGame game;
        static readonly List<string> checks=new List<string>();
        static LoadingScreenValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate original loading screen")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            ProjectBuilder.ConfigureStartupScenes();
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.LoadingScenePath);
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,new object[]{640,1136});
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            if(deadline==0)deadline=EditorApplication.timeSinceStartup+25;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Loading validation timeout at stage "+stage);
                switch(stage)
                {
                    case 0:
                        loader=UnityEngine.Object.FindAnyObjectByType<ZeptoLabLoadingScreen>();if(!loader)return;
                        Application.runInBackground=true;AudioListener.pause=true;
                        origin=Time.realtimeSinceStartup-loader.Elapsed;
                        Check(SceneManager.GetActiveScene().name=="Loading","Application enters the lightweight loading scene first");
                        Check(!UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>(),"Gameplay has not initialized behind the loading screen");
                        Check(loader.ArtworkFamily=="iphone5","Portrait phone loads the original iPhone5 loader artwork");
                        Check(Mathf.Abs(loader.Duration-3)<.001f,"Indicator uses the recovered three-second animation span");
                        LayoutChecks();stage++;break;
                    case 1:
                        if(loader.Elapsed<.5f)return;
                        Check(loader.IndicatorX>=0&&loader.IndicatorX<.5f,"Original animated indicator advances before completion");
                        Check(loader.SceneLoad!=null&&!loader.SceneLoad.allowSceneActivation,"Main scene loads asynchronously while activation waits");
                        Capture("zeptolab-early");stage++;break;
                    case 2:
                        if(loader.Elapsed<2)return;
                        Check(loader.IndicatorX>.39f&&loader.IndicatorX<.5f,"Recovered indicator reaches its authored late-loading pose");
                        Check(!UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>(),"Fast loading does not bypass the logo animation");
                        Capture("zeptolab-late");stage++;break;
                    case 3:
                        game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
                        Check(Time.realtimeSinceStartup-origin>=3,"Main scene activation follows the completed indicator");
                        Check(SceneManager.GetActiveScene().name==ZeptoLabLoadingScreen.MainSceneName&&game.ScreenState==GameScreen.Menu,"Loading transitions to the original main menu");
                        Check(!UnityEngine.Object.FindAnyObjectByType<ZeptoLabLoadingScreen>(),"Loading scene and camera are removed on activation");
                        next=Time.realtimeSinceStartup+.5f;stage++;break;
                    case 4:
                        if(Time.realtimeSinceStartup<next)return;
                        Capture("main-menu-after-loading");stage++;break;
                    case 5:
                        game.StartGame();game.ReturnToMenu();
                        Check(!UnityEngine.Object.FindAnyObjectByType<ZeptoLabLoadingScreen>()&&game.ScreenState==GameScreen.Menu,"Returning from gameplay does not replay startup loading");
                        File.WriteAllText("Documentation/loading-screen-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void LayoutChecks()
        {
            var scenes=EditorBuildSettings.scenes;
            Check(scenes.Length>=2&&scenes[0].path==ProjectBuilder.LoadingScenePath&&scenes[1].path==ProjectBuilder.ScenePath,"Build order places loading before gameplay");
            Check(ZeptoLabLoadingScreen.SelectArtwork(640,960)=="iphone4"&&ZeptoLabLoadingScreen.SelectArtwork(768,1024)=="ipad2","Legacy phone and tablet artwork families remain available");
            foreach(var safe in new[]{new Rect(0,0,640,1136),new Rect(0,80,1080,2240),new Rect(20,0,1200,900)})
            {
                float screenHeight=safe.yMax+30;Rect frame=ZeptoLabLoadingScreen.ArtworkRect(safe,screenHeight,640,1136);
                Check(frame.xMin>=safe.xMin-.01f&&frame.xMax<=safe.xMax+.01f&&frame.yMin>=screenHeight-safe.yMax-.01f&&frame.yMax<=screenHeight-safe.yMin+.01f
                    &&Mathf.Abs(frame.width/frame.height-640f/1136)<.0001f,"Artwork preserves aspect and stays inside safe area "+safe.size);
            }
            foreach(string family in new[]{"iphone5","iphone4","ipad2"})
                foreach(string name in new[]{"zeptolab_bgr","zeptolab_indicator","zeptolab_indicator_bgr"})
                    Check(Resources.Load<Texture2D>("images/"+family+"/loader_screen/"+name),"Original loading texture resolves: "+family+"/"+name);
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            string previous=SessionState.GetString(Key+"StartScene","");
            EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
