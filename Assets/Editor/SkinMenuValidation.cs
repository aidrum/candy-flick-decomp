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
    public static class SkinMenuValidation
    {
        const string Key="CandyFlickSkinMenuTest";
        static CandyFlickGame game;static CandyFlickUI ui;
        static int stage;static double next;
        static LaunchMode controls;static bool fps;
        static readonly List<string> checks=new List<string>();
        static readonly string[] prefs={TimeTravelSkins.Preference,CandyFlickGame.ControlModePreference,CandyFlickGame.CameraFpsLockPreference};
        static SkinMenuValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory("Documentation/SkinMenu");
            if(File.Exists("Documentation/SkinMenu/failure.txt"))File.Delete("Documentation/SkinMenu/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SetResolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void SetResolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot("Documentation/SkinMenu/"+name+".png");
        static void Pointer(EventType type,Vector2 native,Vector2 delta=default)
        {
            Rect safe=Screen.safeArea;float scale=CandyFlickUI.CanvasScale(safe);
            var activeGame=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();
            Vector2 point=CandyFlickUI.CanvasOrigin(safe,Screen.height,activeGame?activeGame.ScreenState:GameScreen.Menu)+native*scale;
            typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)
                .Invoke(null,new object[]{new Event{type=type,mousePosition=point,delta=delta,button=0,clickCount=1}});
        }
        static void Click(Vector2 point){Pointer(EventType.MouseDown,point);Pointer(EventType.MouseUp,point);}
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.85;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;}
                switch(stage++)
                {
                    case 0: game.ReturnToMenu();break;
                    case 1: Capture("main-menu");Click(ui.MenuSkinsRect.center);break;
                    case 2: Check(game.ScreenState==GameScreen.Skins,"Main menu skin icon opens separate Skins screen");Capture("skins-top");
                        Check(Resources.Load<Texture2D>("SkinMenu/nommies")?.width==2048,"Eight rendered idle portraits load as a 2048x1024 atlas");
                        Click(ui.SkinViewport.position+CandyFlickUI.SkinCardRect(1).center);break;
                    case 3: Check(game.Skins.Selected==NommieSkin.Medieval&&PlayerPrefs.GetInt(TimeTravelSkins.Preference)==1,"Grid click selects and persists Medieval");
                        Pointer(EventType.ScrollWheel,ui.SkinViewport.center,new Vector2(0,24));break;
                    case 4: Check(ui.SkinScroll.y==0&&CandyFlickUI.SkinGridHeight<=ui.SkinViewport.height,"Three-column grid fits all eight skins and clamps excess wheel scrolling");
                        Pointer(EventType.MouseDown,new Vector2(220,700));break;
                    case 5: Pointer(EventType.MouseDrag,new Vector2(220,300));break;
                    case 6: Pointer(EventType.MouseUp,new Vector2(220,300));break;
                    case 7: Check(CandyFlickUI.SkinCardRect(7).yMax<=ui.SkinViewport.height,"Final row fits within the viewport");Check(game.Skins.Selected==NommieSkin.Medieval,"Drag does not select a card");
                        Click(ui.SkinViewport.position+CandyFlickUI.SkinCardRect(7).center-ui.SkinScroll);break;
                    case 8: Check(game.Skins.Selected==NommieSkin.Disco,"Final row is reachable and Disco can be selected");Capture("skins-bottom-selected");Click(new Vector2(79,1057));break;
                    case 9: Check(game.ScreenState==GameScreen.Menu,"Skins Back returns to main menu");Click(new Vector2(450,832));break;
                    case 10: Check(game.ScreenState==GameScreen.Settings&&ui.SettingsPage==null,"Settings opens main settings");Capture("settings-main");Click(new Vector2(320,510));break;
                    case 11: Check(ui.SettingsPage=="MoreOptions","More Options opens from main settings");controls=game.ControlMode;fps=game.LockFpsToCamera;Capture("more-options");Click(ui.MoreControlsRect.center);break;
                    case 12: Check(game.ControlMode!=controls,"Controls toggles inside More Options");Click(ui.MoreFpsRect.center);break;
                    case 13: Check(game.LockFpsToCamera!=fps,"Camera FPS lock toggles inside More Options");Click(new Vector2(79,1057));break;
                    case 14: Check(game.ScreenState==GameScreen.Settings&&ui.SettingsPage==null,"More Options Back returns to main settings");ui.Back();break;
                    case 15: Check(game.ScreenState==GameScreen.Menu,"Main settings Back returns to menu");SetResolution(540,960);break;
                    case 16: Click(ui.MenuSkinsRect.center);break;
                    case 17: Check(game.ScreenState==GameScreen.Skins,"Skins navigation works with scaled portrait canvas");Pointer(EventType.MouseDown,new Vector2(210,700));break;
                    case 18: Pointer(EventType.MouseDrag,new Vector2(210,200));break;
                    case 19: Pointer(EventType.MouseUp,new Vector2(210,200));break;
                    case 20: Check(ui.SkinScroll.y==0&&game.Skins.Selected==NommieSkin.Disco,"Touch-style drag clamps correctly without selection at 540x960");Capture("skins-scaled");break;
                    case 21: GroundCandyChecks();break;
                    default:
                        File.WriteAllText("Documentation/skin-menu-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion,checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void GroundCandyChecks()
        {
            game.StartGame();game.enabled=false;game.Actor.enabled=false;ui.enabled=false;Time.timeScale=0;
            var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var candy=game.World.GetComponentsInChildren<CandyProjectile>().First();
            bool hit=false;Action<bool> resolved=value=>hit=value;game.CandyResolved+=resolved;
            var projectile=typeof(CandyFlickGame).GetField("projectile",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                var bounds=game.Actor.GetComponent<Collider>().bounds;
                // Shoot directly through the original collider while well above the floor.
                candy.transform.position=new Vector3(bounds.center.x,bounds.center.y,bounds.min.z-100);
                candy.LaunchImpulse(Vector3.zero,Vector3.zero,false);candy.Body.useGravity=false;candy.Body.linearVelocity=Vector3.forward*700;
                game.Actor.OnBulletShoot(candy);projectile.SetValue(game,candy);Physics.SyncTransforms();
                for(int i=0;i<50&&!hit;i++){Physics.Simulate(.01f);game.Actor.SendMessage("Update");}
                Check(hit&&game.Session.hits==1,"Airborne candy still enters native eat/score path through a real physics contact");
                Check(!candy.CanBeEaten,"Restored pool candy cannot be eaten");
                game.StartGame();Time.timeScale=0;hit=false;
                // Land away from Om Nom, then roll the same launched candy into him.
                candy.transform.position=new Vector3(bounds.center.x,200,bounds.min.z-300);
                candy.LaunchImpulse(Vector3.zero,Vector3.zero,false);candy.Body.useGravity=true;Physics.SyncTransforms();
                for(int i=0;i<150&&!candy.HasLanded;i++)Physics.Simulate(.01f);
                Check(candy.HasLanded&&!candy.CanBeEaten,"A real Floor contact makes a launched candy ineligible to eat");
                Check(!candy.CanBeEatenAt(Time.fixedTimeAsDouble),"Same-physics-step floor contact rejects a pending catch regardless of callback order");
                // Remove the landing bounce and place it on the surface just in front
                // of the collider, so this fixture necessarily exercises ground contact.
                bounds=game.Actor.GetComponent<Collider>().bounds;
                float radius=candy.GetComponent<Collider>().bounds.extents.y;
                candy.Body.position=new Vector3(bounds.center.x,Mathf.Max(radius+.2f,bounds.min.y+radius),bounds.min.z-radius-25);
                candy.Body.useGravity=false;candy.Body.linearDamping=0;
                game.Actor.OnBulletShoot(candy);projectile.SetValue(game,candy);
                candy.Body.linearVelocity=Vector3.forward*300;Physics.SyncTransforms();
                bool touchedActor=false;
                var pending=typeof(OmNomActor).GetField("pendingCandy",BindingFlags.Instance|BindingFlags.NonPublic);
                for(int i=0;i<100;i++)
                {
                    Physics.Simulate(.01f);
                    if((CandyProjectile)pending.GetValue(game.Actor)==candy)touchedActor=true;
                    game.Actor.SendMessage("Update");
                }
                Check(touchedActor,"Ground-roll fixture physically contacts Om Nom's original collider (radius "+radius+", actor "+bounds+")");
                Check(!hit&&game.Session.hits==0&&game.Session.total==0,"Ground candy rolling into Om Nom never eats or awards score");
                candy.Body.linearVelocity=Vector3.zero;candy.Body.angularVelocity=Vector3.zero;
                // Manual Physics.Simulate does not advance Unity's fixed clock;
                // explicitly sample the stopped-distance rule for this fixture.
                game.Actor.SendMessage("UpdateFrogStatus");game.Actor.SendMessage("UpdateFrogStatus");
                Check(game.Session.shots==1,"Grounded miss retains original stopped-distance shot resolution");
                candy.Restore();candy.LaunchImpulse(Vector3.zero,Vector3.zero,false);
                Check(candy.CanBeEaten&&!candy.HasLanded&&double.IsPositiveInfinity(candy.LandingPhysicsTime),"Relaunch clears landed state for pool reuse");candy.Restore();
            }
            finally{candy.Body.useGravity=true;candy.Body.linearDamping=.5f;game.CandyResolved-=resolved;Physics.simulationMode=mode;}
        }
        static void OnLog(string message,string stack,LogType type)
        {if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            if(error!=null)File.WriteAllText("Documentation/SkinMenu/failure.txt",error);
            EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,unity;public string[] checks;}
    }
}
