using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class SlingshotEdgeValidation
    {
        const string Key="CandyFlickSlingshotEdgeTest",Folder="Documentation/SlingshotEdge";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.GroundARPreference,CandyFlickGame.InGameMusicPreference,CandyFlickGame.ControlModePreference,TimeTravelSkins.Preference};
        static readonly Vector2Int[] sizes={new Vector2Int(640,1422),new Vector2Int(1080,2400),new Vector2Int(1440,3200)};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static int phase,index;static double next;static bool finishing;static Vector2 end;
        static SlingshotEdgeValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));PlayerPrefs.SetInt(pref,0);}
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.Save();
            SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string message){if(!valid)throw new Exception(message);checks.Add(message);}
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.7;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;Application.runInBackground=true;AudioListener.pause=true;return;}
                if(index==sizes.Length){File.WriteAllText("Documentation/slingshot-edge-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);return;}
                switch(phase++)
                {
                    case 0: typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{sizes[index].x,sizes[index].y});break;
                    case 1: game.enabled=true;game.Actor.enabled=true;game.StartGame();break;
                    case 2:
                        game.enabled=false;game.Actor.enabled=false;Time.timeScale=0;PullToBottom();
                        ScreenCapture.CaptureScreenshot(Folder+"/"+sizes[index].x+"x"+sizes[index].y+"-bottom-pull.png");break;
                    case 3:
                        game.SampleInput(TouchPhase.Ended,end,Vector2.zero);
                        Check(game.World.GetComponentsInChildren<CandyProjectile>(true).Count(c=>c.IsShot)==1,"Releasing at the bottom launches exactly one candy at "+sizes[index]);
                        game.ReturnToMenu();index++;phase=0;break;
                }
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static void PullToBottom()
        {
            // Exercise a wide portrait AR projection as well as a tall display;
            // the fixed room camera's narrower FOV does not reach this collider edge.
            Matrix4x4 projection=Matrix4x4.Perspective(100f,(float)Screen.width/Screen.height,game.GameCamera.nearClipPlane,game.GameCamera.farClipPlane);
            game.GameCamera.projectionMatrix=projection;game.CandyCamera.projectionMatrix=projection;
            Physics.SyncTransforms();Vector2 start=game.GameCamera.WorldToScreenPoint(game.CandyGhost.position);
            Vector3 initial=game.CandyGhost.position;
            game.SampleInput(TouchPhase.Began,start,Vector2.zero);
            Check(game.IsDragging,"Candy owns the slingshot gesture at "+sizes[index]);
            Collider original=game.Player.Find("CandyTouchPlane").GetComponent<Collider>();
            int outside=0;Vector2 previous=start;
            for(float y=start.y-10;y>=1;y-=Mathf.Max(1,start.y/80))
            {
                Vector2 point=new Vector2(start.x,y);Ray ray=game.GameCamera.ScreenPointToRay(point);
                bool inside=original.Raycast(ray,out _,Mathf.Infinity);
                game.SampleInput(TouchPhase.Moved,point,point-previous);previous=point;
                if(!inside)
                {
                    outside++;
                    Check(game.IsDragging&&Vector3.Distance(initial,game.CandyGhost.position)>1,"Pull remains stretched beyond finite touch plane at y="+y.ToString("F1")+" on "+sizes[index]);
                }
            }
            end=new Vector2(start.x,.5f);game.SampleInput(TouchPhase.Moved,end,end-previous);
            Check(outside>0,"Tall-phone drag reproduces leaving the original finite touch collider at "+sizes[index]);
            Check(game.IsDragging&&Vector3.Distance(initial,game.CandyGhost.position)>1,"Pull reaches the physical bottom edge without snapping back at "+sizes[index]);
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            if(game){game.enabled=true;game.Actor.enabled=true;game.ReturnToMenu();}Time.timeScale=1;AudioListener.pause=SessionState.GetBool(Key+"Audio",false);
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
