using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class EarlyMissValidation
    {
        const string Key="CandyFlickEarlyMissTest";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference,TimeTravelSkins.Preference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static int stage;static double next;
        static EarlyMissValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory("Documentation/EarlyMiss");if(File.Exists("Documentation/EarlyMiss/failure.txt"))File.Delete("Documentation/EarlyMiss/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.InGameMusicPreference,0);PlayerPrefs.SetInt(TimeTravelSkins.Preference,0);PlayerPrefs.Save();
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.3;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;AudioListener.pause=true;Application.runInBackground=true;return;}
                if(stage++==0)
                {
                    foreach(bool nonAR in new[]{true,false})CheckRenderTiming(nonAR);
                    File.WriteAllText("Documentation/early-miss-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void CheckRenderTiming(bool nonAR)
        {
            if(game.NonARModeEnabled!=nonAR)game.ToggleNonARMode();
            game.StartGame();game.enabled=false;game.Actor.enabled=false;
            var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var candy=game.World.GetComponentsInChildren<CandyProjectile>().First();
            try
            {
                candy.Body.position=game.Actor.transform.position+new Vector3(0,500,500);
                candy.LaunchImpulse(Vector3.back*100,Vector3.zero,false);game.Actor.OnBulletShoot(candy);
                typeof(CandyFlickGame).GetField("projectile",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(game,candy);
                Physics.SyncTransforms();string name=nonAR?"Non AR":"shared actor / AR-compatible path";
                for(int i=0;i<12;i++)game.Actor.SendMessage("Update");
                Check(game.Session.shots==0&&candy.IsShot&&!candy.HasLanded,name+": 12 renders before the first physics step do not resolve a miss");
                Physics.Simulate(.01f);
                game.Actor.SendMessage("UpdateFrogStatus"); // Explicit status sample for manual physics, whose fixed clock does not advance.
                for(int i=0;i<12;i++)game.Actor.SendMessage("Update");
                Check(game.Session.shots==0&&candy.IsShot&&!candy.HasLanded,name+": duplicate rendered frames after a moving physics sample do not resolve a miss");
                candy.Restore();game.Actor.SendMessage("UpdateFrogStatus");game.Actor.SendMessage("UpdateFrogStatus");
                Check(game.Session.shots==1&&game.Session.hits==0,name+": restored/stopped candy still resolves through the native miss path");
            }
            finally{candy.Restore();Physics.simulationMode=mode;game.ReturnToMenu();}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            if(error!=null)File.WriteAllText("Documentation/EarlyMiss/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
