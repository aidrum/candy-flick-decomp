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

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class SuperCandyValidation
    {
        const string Key="CandyFlickSuperCandyTest",Folder="Documentation/SuperCandy";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.ControlModePreference,TimeTravelSkins.Preference,CandyFlickGame.InGameMusicPreference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static int mode,phase;static double next;
        static LegacyParticles26 ghostParticles,flightParticles;static ParticleSystemRenderer ghostRenderer;
        static string Name=>(mode<2?"nonar-":"shared-")+(mode%2==0?"slingshot":"flick");
        static SuperCandyValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate last-candy particles")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));PlayerPrefs.SetInt(pref,0);}PlayerPrefs.Save();
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{640,1136});
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(Name+": "+label);}
        public static void AdvanceToFinalCandy(CandyFlickGame game)
        {
            while(game.Session.shots<GameRules.CandiesPerRound-1)
            {
                int before=game.Session.shots;game.PrepareAndShootBullet(80,0,0);
                var shot=game.World.GetComponentsInChildren<CandyProjectile>().Single(c=>c.IsShot);
                shot.Restore(); // Resolve a stopped projectile through the actor's native miss path.
                game.Actor.SendMessage("UpdateFrogStatus");game.Actor.SendMessage("UpdateFrogStatus");
                if(game.Session.shots!=before+1)throw new Exception("Fixture shot did not resolve through normal score/readiness callbacks");
            }
        }
        static void Capture(string suffix)=>ScreenCapture.CaptureScreenshot(Folder+"/"+Name+suffix+".png");
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.4;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;AudioListener.pause=true;Application.runInBackground=true;game.enabled=false;game.Actor.enabled=false;return;}
                if(mode==4){File.WriteAllText("Documentation/super-candy-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);return;}
                switch(phase++)
                {
                    case 0:
                        if(game.NonARModeEnabled!=(mode<2))game.ToggleNonARMode();
                        game.SetMode(mode%2==0?LaunchMode.Slingshot:LaunchMode.Flick);game.StartGame();
                        Check(!game.CandyGhost.Find("CandyGhost/SuperEffect").gameObject.activeSelf,"ordinary ready candy has no super effect");
                        AdvanceToFinalCandy(game);
                        Check(game.Session.Remaining==1&&game.Session.IsSuperCandy&&game.Ready,"nine real launch/miss callbacks prepare the final candy");
                        Check(game.CandyGhost.Find("CandyGhost/SuperEffect").gameObject.activeInHierarchy,"final ready candy enables its original effect without manually activating it");break;
                    case 1:
                        ghostParticles=game.CandyGhost.GetComponentInChildren<LegacyParticles26>();
                        Check(ghostParticles&&ghostParticles.System&&ghostParticles.System.particleCount>0,"ready candy continuously emits its yellow particles");
                        ghostRenderer=ghostParticles.GetComponent<ParticleSystemRenderer>();
                        Check(ghostRenderer.enabled&&!ghostRenderer.forceRenderingOff&&ghostRenderer.sharedMaterial.shader.isSupported&&(game.CandyCamera.cullingMask&(1<<ghostParticles.gameObject.layer))!=0,"particles are visible to the candy camera and use a supported original material");
                        Time.timeScale=0;Capture("-ready");break;
                    case 2: ghostRenderer.enabled=false;Capture("-ready-without-particles");break;
                    case 3:
                        CompareScreenshots();ghostRenderer.enabled=true;Time.timeScale=1;
                        game.Pause();game.SendMessage("Update");Check(!game.CandyGhost.gameObject.activeSelf,"pausing hides the ready candy");game.Resume();game.SendMessage("Update");
                        Check(game.Session.IsSuperCandy&&game.Ready,"resume keeps the final candy state");
                        game.PrepareAndShootBullet(80,0,0);
                        Check(!game.CandyGhost.gameObject.activeSelf,"throwing hides the ready-candy effect with its parent");
                        var shot=game.World.GetComponentsInChildren<CandyProjectile>().Single(c=>c.IsShot);
                        flightParticles=shot.GetComponentInChildren<LegacyParticles26>();
                        Check(flightParticles&&flightParticles.gameObject.activeInHierarchy,"thrown super candy enables its serialized inactive particle child");break;
                    case 4:
                        Check(flightParticles.System.particleCount>0,"thrown super candy emits live particles");
                        game.StartGame();Check(!game.CandyGhost.Find("CandyGhost/SuperEffect").gameObject.activeSelf,"restart clears the last-candy effect for the new normal candy");
                        Check(game.World.Find("Candies").GetComponentsInChildren<LegacyParticles26>(true).All(p=>!p.gameObject.activeInHierarchy),"pooled projectile effects are hidden after restart");
                        mode++;phase=0;break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void CompareScreenshots()
        {
            var a=new Texture2D(2,2);var b=new Texture2D(2,2);
            try
            {
                a.LoadImage(File.ReadAllBytes(Folder+"/"+Name+"-ready.png"));b.LoadImage(File.ReadAllBytes(Folder+"/"+Name+"-ready-without-particles.png"));
                var x=a.GetPixels32();var y=b.GetPixels32();int changed=0;
                for(int i=0;i<x.Length;i++)if(Mathf.Abs(x[i].r-y[i].r)+Mathf.Abs(x[i].g-y[i].g)+Mathf.Abs(x[i].b-y[i].b)>30)changed++;
                Check(changed>20,"ready-candy screenshot includes rendered particle pixels ("+changed+")");
            }
            finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Time.timeScale=1;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            string previous=SessionState.GetString(Key+"StartScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
