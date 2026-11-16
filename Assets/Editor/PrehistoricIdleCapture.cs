using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CandyFlick;

namespace CandyFlickEditor
{
    // Isolated Unity scene renders; never captures the desktop or other apps.
    [InitializeOnLoad]
    public static class PrehistoricIdleCapture
    {
        const string Key="CandyFlickPrehistoricIdleCapture",Folder="Documentation/TimeTravel/PrehistoricIdles";
        static readonly string[] Clips={"idle","idle2","idle3","idle4"};
        static readonly float[] Samples={.1f,.3f,.5f,.7f,.9f};
        static CandyFlickGame game;
        static int frame;static bool pending;static double next;
        static PrehistoricIdleCapture(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            SessionState.SetBool(Key+"NonARHad",PlayerPrefs.HasKey(CandyFlickGame.NonARPreference));
            SessionState.SetInt(Key+"NonAR",PlayerPrefs.GetInt(CandyFlickGame.NonARPreference));
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,0);
            SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.25;
            try
            {
                if(!game)
                {
                    game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
                    Application.runInBackground=true;AudioListener.pause=true;game.StartGame();Time.timeScale=0;
                    game.enabled=false;game.Actor.enabled=false;game.GetComponent<CandyFlickUI>().enabled=false;
                    game.Skins.ApplyForSession(NommieSkin.Prehistoric);
                    typeof(TimeTravelSkinValidation).GetField("game",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,game);
                    foreach(string clip in Clips)if(game.Actor.GetComponent<Animation>()[clip]==null)throw new Exception("Missing "+clip);
                }
                if(pending)
                {
                    int clip=frame/Samples.Length,sample=frame%Samples.Length;
                    typeof(TimeTravelSkinValidation).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{"PrehistoricIdles/"+(clip+1)+"-"+Clips[clip]+"-"+(sample+1)});
                    frame++;pending=false;
                    if(frame==Clips.Length*Samples.Length)
                    {
                        File.WriteAllText(Folder+"/captured.txt",DateTime.UtcNow.ToString("o")+"\n1 = idle (idle_loop)\n2 = idle2 (hungry)\n3 = idle3 (puzzled)\n4 = idle4 (greeting)\nFive samples per animation at 10%, 30%, 50%, 70%, 90%. All variations restored.\n");
                        Finish(null);return;
                    }
                }
                var animation=game.Actor.GetComponent<Animation>();string name=Clips[frame/Samples.Length];
                animation.Stop();animation.Play(name);animation[name].speed=0;
                animation[name].time=animation[name].length*Samples[frame%Samples.Length];animation.Sample();
                if(game.Skins.Visual)game.Skins.Visual.GetComponent<TimeTravelVisual>().SendMessage("LateUpdate");
                pending=true;
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            if(SessionState.GetBool(Key+"NonARHad",false))PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,SessionState.GetInt(Key+"NonAR",0));else PlayerPrefs.DeleteKey(CandyFlickGame.NonARPreference);
            PlayerPrefs.Save();AudioListener.pause=SessionState.GetBool(Key+"Audio",false);
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);
            EditorApplication.ExitPlaymode();
        }
    }
}
