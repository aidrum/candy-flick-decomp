using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class NonARControlsValidation
    {
        const string Key="CandyFlickNonARControlsTest",Folder="Documentation/NonARControls";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference,CandyFlickGame.ControlModePreference,TimeTravelSkins.Preference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static NonARViewControls navigation;static int stage;static double next,deadline;
        static NonARControlsValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.InGameMusicPreference,0);
            PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.SetInt(TimeTravelSkins.Preference,0);PlayerPrefs.Save();
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{640,1136});
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static Touch T(int id,TouchPhase phase,Vector2 point)=>new Touch{fingerId=id,phase=phase,position=point};
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static int ActiveCandies=>game.World.GetComponentsInChildren<CandyProjectile>().Count(c=>c.IsShot);
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.8;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;Application.runInBackground=true;AudioListener.pause=true;game.StartGame();navigation=game.NonARBackground.Navigation;return;}
                switch(stage++)
                {
                    case 0: Check(game.IsNonAR&&game.Ready,"Non AR starts with a ready launcher");PinchChecks();navigation.ZoomBy(.0001f);break;
                    case 1: Check(game.PossibleScore==5,"Closest zoom produces native distance score 5");Capture("closest-score-5");break;
                    case 2: navigation.ZoomBy(10000);break;
                    case 3: Check(game.PossibleScore==50,"Farthest zoom produces native distance score 50");Capture("farthest-score-50");break;
                    case 4: OrbitChecks();navigation.OrbitBy(new Vector2(-50-navigation.Yaw,15-navigation.Pitch));break;
                    case 5: Capture("orbit-left");break;
                    case 6: navigation.OrbitBy(new Vector2(50-navigation.Yaw,55-navigation.Pitch));break;
                    case 7: Capture("orbit-high-right");break;
                    case 8: game.StartGame();break;
                    case 9: GestureChecks();game.Pause();break;
                    case 10:
                        Vector3 paused=game.Player.position;navigation.ZoomBy(.5f);navigation.OrbitBy(new Vector2(30,20));
                        Check(game.Player.position==paused,"Pause blocks camera zoom and orbit");game.Resume();game.StartGame();break;
                    case 11:
                        Physics.SyncTransforms();Vector2 start=game.CandyCamera.WorldToScreenPoint(game.CandyGhost.position);
                        Check(!navigation.HandleTouches(1,T(10,TouchPhase.Began,start),default),"A fresh launcher gesture still goes to native slingshot input");
                        game.SampleInput(TouchPhase.Began,start,Vector2.zero);
                        Vector2 end=start-Vector2.up*Screen.height*.15625f;
                        Check(!navigation.HandleTouches(1,T(10,TouchPhase.Moved,end),default),"Pulling the slingshot keeps its input ownership");
                        game.SampleInput(TouchPhase.Moved,end,end-start);
                        Check(!navigation.HandleTouches(1,T(10,TouchPhase.Ended,end),default),"Slingshot release reaches the native shooting path");
                        game.SampleInput(TouchPhase.Ended,end,Vector2.zero);Check(ActiveCandies==1,"Normal slingshot release launches exactly one candy");deadline=EditorApplication.timeSinceStartup+8;break;
                    case 12:
                        if(game.Session.shots==0){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Slingshot shot failed to resolve");stage--;return;}
                        Check(game.Session.shots==1&&game.Ready,"Native hit/miss resolution still readies the next shot");
                        game.ReturnToMenu();Vector3 menu=game.Player.position;navigation.ZoomBy(.5f);navigation.OrbitBy(new Vector2(20,20));
                        Check(game.Player.position==menu,"Leaving Non AR blocks camera controls and restores the original pose");
                        game.SetMode(LaunchMode.Flick);game.StartGame();break;
                    case 13:
                        navigation.ZoomBy(.0001f);Physics.SyncTransforms();
                        Vector2 flickStart=game.CandyCamera.WorldToScreenPoint(game.CandyGhost.position),flickEnd=flickStart+Vector2.up*Screen.height*.15f;
                        Check(!navigation.HandleTouches(1,T(11,TouchPhase.Began,flickStart),default),"Flick-mode candy is also excluded from camera dragging at closest zoom");
                        game.SampleInput(TouchPhase.Began,flickStart,Vector2.zero);
                        navigation.HandleTouches(1,T(11,TouchPhase.Moved,flickEnd),default);game.SampleInput(TouchPhase.Moved,flickEnd,flickEnd-flickStart);
                        navigation.HandleTouches(1,T(11,TouchPhase.Ended,flickEnd),default);game.SampleInput(TouchPhase.Ended,flickEnd,Vector2.zero);
                        Check(ActiveCandies==1,"Flick mode still launches exactly one candy after zooming");
                        deadline=EditorApplication.timeSinceStartup+8;break;
                    case 14:
                        if(game.Session.shots==0){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Flick shot failed to resolve");stage--;return;}
                        Check(game.Session.shots==1&&game.Ready,"Flick shot resolves normally at the near zoom limit");game.ReturnToMenu();break;
                    default:
                        File.WriteAllText("Documentation/nonar-controls-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void PinchChecks()
        {
            Vector2 a=new Vector2(Screen.width*.3f,Screen.height*.7f),b=new Vector2(Screen.width*.7f,Screen.height*.7f);
            float distance=navigation.Distance;
            Check(navigation.HandleTouches(2,T(1,TouchPhase.Began,a),T(2,TouchPhase.Began,b)),"Two fingers claim input for pinch zoom");
            navigation.HandleTouches(2,T(1,TouchPhase.Moved,a-Vector2.right*100),T(2,TouchPhase.Moved,b+Vector2.right*100));
            Check(navigation.Distance<distance,"Spreading two fingers moves closer");
            float closer=navigation.Distance;
            navigation.HandleTouches(2,T(2,TouchPhase.Moved,b),T(1,TouchPhase.Moved,a));
            Check(navigation.Distance>closer,"Closing a pinch moves farther, including reordered finger indices");
            float held=navigation.Distance;
            navigation.HandleTouches(2,T(1,TouchPhase.Moved,a),T(3,TouchPhase.Began,b+Vector2.right*50));
            Check(navigation.Distance==held,"Replacing a pinch finger establishes a new baseline without jumping");
            Check(navigation.HandleTouches(1,T(1,TouchPhase.Moved,a),default),"A remaining pinch finger stays consumed");
            navigation.HandleTouches(0,default,default);
            Check(ActiveCandies==0&&game.Session.shots==0,"Pinching never launches or scores a candy");
            navigation.ZoomBy(.0001f);Check(Mathf.Abs(game.ScoreDistance-NonARViewControls.MinDistance)<.02f,"Zoom clamps to the near native scoring distance");
            navigation.ZoomBy(10000);Check(Mathf.Abs(game.ScoreDistance-NonARViewControls.MaxDistance)<.02f,"Zoom clamps to the far native scoring distance");
        }
        static void OrbitChecks()
        {
            Bounds table=game.NonARBackground.Room.transform.Find("Cube").GetComponent<Renderer>().bounds;
            Vector3 center=game.World.Find("FrogPos").position;
            bool safe=true,front=false,back=false,left=false,right=false;float lowestCandy=float.PositiveInfinity;
            for(int d=0;d<=8;d++)for(int y=-4;y<=4;y++)for(int p=-2;p<=4;p++)
            {
                float desired=Mathf.Lerp(NonARViewControls.MinDistance,NonARViewControls.MaxDistance,d/8f);
                navigation.ZoomBy(desired/navigation.Distance);navigation.OrbitBy(new Vector2(y*45-navigation.Yaw,p*30-navigation.Pitch));
                Vector3 position=game.Player.position;
                lowestCandy=Mathf.Min(lowestCandy,game.CandyGhost.position.y);
                safe&=float.IsFinite(position.x)&&float.IsFinite(position.y)&&float.IsFinite(position.z)
                    &&position.y>=table.max.y+249.9f
                    &&game.ScoreDistance>=NonARViewControls.MinDistance-.02f&&game.ScoreDistance<=NonARViewControls.MaxDistance+.02f;
                front|=position.z>center.z+1;back|=position.z<center.z-1;
                left|=position.x<center.x-1;right|=position.x>center.x+1;
            }
            Check(safe,"567 sampled full-circle zoom/orbit poses retain table-height and scoring-distance limits");
            Check(front&&back&&left&&right,"Horizontal orbit reaches all four sides of Om Nom");
            Vector3 beforeTurn=game.Player.position;navigation.OrbitBy(new Vector2(360,0));
            Check(Vector3.Distance(game.Player.position,beforeTurn)<.01f,"A full 360-degree rotation wraps continuously to the same position");
            Check(lowestCandy>20,"Ready candy stays above the table across all sampled camera poses (minimum "+lowestCandy+")");
            navigation.ZoomBy(10000);Check(game.PossibleScore==50,"Extreme rotations retain the far score endpoint without float-rounding loss");
            navigation.ZoomBy(.0001f);Check(game.PossibleScore==5,"Extreme rotations retain the near score endpoint");
            navigation.ZoomBy(NonARViewControls.MaxDistance/navigation.Distance);
        }
        static void GestureChecks()
        {
            Vector2 high=new Vector2(Screen.width*.7f,Screen.height*.7f);Vector3 initial=game.Player.position;
            Check(navigation.HandleTouches(1,T(4,TouchPhase.Began,high),default),"One finger above the launcher claims a camera gesture");
            float originalYaw=navigation.Yaw,originalPitch=navigation.Pitch;
            navigation.HandleTouches(1,T(4,TouchPhase.Moved,high+new Vector2(80,-80)),default);
            Check(game.Player.position!=initial&&navigation.BlocksPetting,"Dragging above the launcher orbits and suppresses petting");
            Check(Mathf.DeltaAngle(originalYaw,navigation.Yaw)>0,"Dragging right uses the revised horizontal rotation direction");
            Check(navigation.Pitch>originalPitch,"Downward dragging retains its original vertical rotation direction");
            float rightYaw=navigation.Yaw;
            navigation.HandleTouches(1,T(4,TouchPhase.Moved,high+new Vector2(40,-80)),default);
            Check(Mathf.DeltaAngle(rightYaw,navigation.Yaw)<0,"Dragging left reverses horizontal rotation naturally");
            Check(navigation.HandleTouches(1,T(4,TouchPhase.Moved,new Vector2(Screen.width*.5f,Screen.height*.2f)),default),"Camera drag remains camera-owned when crossing into the launcher area");
            Check(navigation.HandleTouches(1,T(4,TouchPhase.Ended,high),default),"Camera release is consumed");
            Check(ActiveCandies==0&&game.Session.shots==0,"Camera dragging never launches or scores a candy");
            navigation.HandleTouches(0,default,default);game.StartGame();Physics.SyncTransforms();
            Vector2 launcher=game.CandyCamera.WorldToScreenPoint(game.CandyGhost.position);
            Check(!navigation.HandleTouches(1,T(5,TouchPhase.Began,launcher),default),"Launcher touch is excluded from camera orbit");
            game.SampleInput(TouchPhase.Began,launcher,Vector2.zero);Check(game.IsDragging,"Native slingshot starts dragging normally");
            navigation.HandleTouches(2,T(5,TouchPhase.Moved,launcher),T(6,TouchPhase.Began,high));
            Check(!game.IsDragging,"A second finger safely cancels an active slingshot pull");
            Check(navigation.HandleTouches(1,T(5,TouchPhase.Ended,launcher),default),"Releasing a pinch-canceled launcher touch cannot fire");
            navigation.HandleTouches(0,default,default);Check(ActiveCandies==0,"Pinch cancellation leaves no launched candy");
            game.StartGame();
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
