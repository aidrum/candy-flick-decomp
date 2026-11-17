using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CandyFlick;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class AchievementValidation
    {
        const string Key="CandyFlickAchievementsTest",Folder="Documentation/Achievements";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference,CandyFlickGame.ControlModePreference,TimeTravelSkins.Preference,"IsMusic","IsSound","BestScore","TimesPlayed"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static int stage;static double next,deadline;static bool finishing;
        static AchievementValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            foreach(string path in Directory.GetFiles("Assets/Resources/Achievements","*.png"))
            {
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;
                importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
            }
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetBool(Key+"SaveHad",PlayerPrefs.HasKey(LocalAchievements.SaveKey));SessionState.SetString(Key+"Save",PlayerPrefs.GetString(LocalAchievements.SaveKey,""));PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.InGameMusicPreference,0);PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.SetInt(TimeTravelSkins.Preference,0);PlayerPrefs.SetInt("IsMusic",0);PlayerPrefs.SetInt("IsSound",0);PlayerPrefs.Save();
            SessionState.SetFloat(Key+"Volume",AudioListener.volume);SessionState.SetBool(Key+"Audio",AudioListener.pause);SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static object Call(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
        static void Check(bool valid,string message){if(!valid)throw new Exception(message);checks.Add(message);}
        static void Click(Vector2 point)
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        }
        static void Drag(Vector2 a,Vector2 b)
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            var pointer=typeof(SkinMenuValidation).GetMethod("Pointer",BindingFlags.Static|BindingFlags.NonPublic);
            pointer.Invoke(null,new object[]{EventType.MouseDown,a,Vector2.zero});pointer.Invoke(null,new object[]{EventType.MouseDrag,b,b-a});pointer.Invoke(null,new object[]{EventType.MouseUp,b,Vector2.zero});
        }
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static Rect Back=>(Rect)Call(ui,"R","SettingScreen$$GetBackBtnRect",true);
        static Vector2 Scroll=>(Vector2)typeof(CandyFlickUI).GetField("achievementScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        static void Wait(bool ready,string message){if(ready)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception(message);stage--;}
        static LocalAchievements Fresh(){PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);return new LocalAchievements();}
        static LocalAchievements.Entry Entry(LocalAchievements a,string id)=>a.Definitions.entries.Single(e=>e.id==id);
        static bool Has(LocalAchievements a,string id)=>a.Unlocked(Entry(a,id));
        static void Rules()
        {
            var a=Fresh();Check(a.Definitions.entries.Length==20&&a.Definitions.entries.Select(e=>e.id).Distinct().Count()==20,"All twenty Exophase achievements have unique original Game Center IDs");
            Check(a.UnlockedCount==0,"Fresh local progress leaves every achievement locked");
            var atlas=Resources.Load<Texture2D>("Achievements/icons");Check(atlas&&atlas.width==2048&&atlas.height==1024,"All achievement icons and locked art load from one atlas");
            Check(a.Definitions.entries.All(e=>e.uv.Length==4&&e.uv.All(float.IsFinite)&&e.uv[0]>=0&&e.uv[1]>=0&&e.uv[0]+e.uv[2]<=1&&e.uv[1]+e.uv[3]<=1),"Every icon has a valid atlas rectangle");
            foreach(string resource in new[]{"track","thumb","thumb_pressed"})Check(Resources.Load<Texture2D>("Achievements/"+resource),"Supplied scrollbar quad imports: "+resource);
            for(int i=1;i<=1000;i++)
            {
                a.ThrowCandy();if(i==99||i==100||i==499||i==500||i==999||i==1000)
                    foreach(var e in a.Definitions.entries.Where(e=>e.metric=="throws"))Check(a.Unlocked(e)==(i>=e.target),"Throw threshold "+e.target+" at "+i);
            }
            a=new LocalAchievements();Check(Has(a,"ninjatosser"),"Completed lifetime progress survives local reload");
            a=Fresh();
            for(int i=1;i<=900;i++)
            {
                if(i%10==1)a.BeginGame();a.Resolve(true,i%10==0,false,5,false);
                if(new[]{9,10,99,100,299,300}.Contains(i))foreach(var e in a.Definitions.entries.Where(e=>e.metric=="hits"))Check(a.Unlocked(e)==(i>=e.target),"Feed threshold "+e.target+" at "+i);
                if(new[]{40,50,290,300,890,900}.Contains(i))foreach(var e in a.Definitions.entries.Where(e=>e.metric=="super"))Check(a.Unlocked(e)==(i/10>=e.target),"Super-candy threshold "+e.target+" at "+i/10);
            }
            Check(Has(a,"marksman"),"Ten consecutive hits unlock Marksman");
            a=Fresh();for(int i=0;i<2;i++)a.Resolve(true,false,false,5,false);a.Resolve(false,false,false,0,false);a.Resolve(true,false,false,5,false);
            Check(!Has(a,"skilledsniper"),"A miss breaks the hit streak");a.BeginGame();a.Resolve(true,false,false,5,false);a.Resolve(true,false,false,5,false);Check(!Has(a,"skilledsniper"),"Starting a new game resets the hit streak");a.Resolve(true,false,false,5,false);Check(Has(a,"skilledsniper"),"Three consecutive hits unlock Sharp Shooter");
            a=Fresh();foreach(bool hit in new[]{true,false,true,false,true})a.Resolve(hit,false,false,hit?5:0,false);Check(Has(a,"feedingpattern"),"The first five Hit Miss Hit Miss Hit results unlock Feeding Pattern");
            a=Fresh();foreach(bool hit in new[]{false,true,false,true,false,true})a.Resolve(hit,false,false,hit?5:0,false);Check(!Has(a,"feedingpattern"),"Feeding Pattern follows the original first-five rule, not a sliding window");
            a=Fresh();for(int i=0;i<10;i++)a.Resolve(i%2==0,false,false,i%2==0?5:0,false);Check(Has(a,"tummyteaser"),"Five nonconsecutive misses in one game unlock Tummy Teaser");
            a=Fresh();for(int i=0;i<4;i++)a.Resolve(false,false,false,0,false);a.BeginGame();a.Resolve(false,false,false,0,false);Check(!Has(a,"tummyteaser"),"Miss totals do not carry over between games");
            a=Fresh();a.Resolve(false,false,true,0,true);Check(!Has(a,"fastfood")&&a.Progress(Entry(a,"ringmaster"))==0,"Misses award neither moving catches nor ring-feed progress");
            for(int i=0;i<49;i++)a.Resolve(true,false,true,5,false);Check(!Has(a,"ringmaster"),"Master of the Rings remains locked at 49 successful ring feeds");a.Resolve(true,false,true,5,true);Check(Has(a,"ringmaster")&&Has(a,"fastfood"),"The fiftieth ring feed and moving catch unlock their achievements");
            a=Fresh();for(int i=0;i<10;i++){a.Resolve(true,i==9,i<4,i<4?65:i==9?100:50,false);}
            Check(new[]{"quicksnack","goodlunch","sweetjackpot","610points"}.All(id=>Has(a,id)),"A native 610-point game unlocks all four score thresholds");
            a=Fresh();a.Resolve(true,false,false,50,false);a.BeginGame();a.Resolve(true,false,false,50,false);Check(!Has(a,"quicksnack"),"Score thresholds do not combine points across games");
            a=Fresh();Check(a.UnlockedCount==0,"Clearing saved progress and reloading resets local achievements");game.Achievements.Reload();
        }
        static void Integration()
        {
            game.StartGame();game.Actor.enabled=false;
            game.PrepareAndShootBullet(400,0,0);Check(game.Achievements.Progress(Entry(game.Achievements,"skilledtosser"))==1,"Actual candy launch increments the lifetime throw counter once");
            game.PrepareAndShootBullet(400,0,0);Check(game.Achievements.Progress(Entry(game.Achievements,"skilledtosser"))==1,"A rejected duplicate launch does not award progress");
            typeof(OmNomActor).GetField("moveProgress",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game.Actor,.5f);
            game.ResolveShot(true,15);Check(Has(game.Achievements,"fastfood")&&game.Achievements.Progress(Entry(game.Achievements,"ringmaster"))==1,"The actual normal-shot resolution forwards moving and ring-feed state");
            game.StartGame();game.Session.shots=9;game.PrepareAndShootBullet(400,0,0);game.ResolveShot(true,0);
            Check(game.Achievements.Progress(Entry(game.Achievements,"abettertaste"))==1,"The actual tenth-candy resolution records a super candy before advancing the session");
            game.StartGame();game.PrepareAndShootBullet(400,0,0);string before=PlayerPrefs.GetString(LocalAchievements.SaveKey);
            var active=typeof(LanMultiplayer).GetField("activeMatch",BindingFlags.Instance|BindingFlags.NonPublic);active.SetValue(game.LAN,true);
            try{game.ResolveShot(true,15);}finally{active.SetValue(game.LAN,false);}
            Check(PlayerPrefs.GetString(LocalAchievements.SaveKey)==before,"LAN shot resolution cannot alter normal-game achievement progress");
            game.ReturnToMenu();game.Actor.enabled=true;PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);game.Achievements.Reload();
        }
        static void LayoutChecks()
        {
            Check(ui.AchievementsViewportRect.y>120&&ui.AchievementsViewportRect.yMax<Back.y,"Scrollable achievement list stays between title and Back at "+Screen.width+"x"+Screen.height);
            Check(ui.AchievementsScrollbarRect.xMax<640&&!ui.AchievementsScrollbarRect.Overlaps(ui.AchievementsViewportRect),"Custom candy scrollbar stays beside the list without overlap");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.45;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;return;}
                switch(stage++)
                {
                    case 0:Rules();Integration();game.Show(GameScreen.Settings);break;
                    case 1:Check(ui.SettingsAchievementsRect.width>0,"Settings exposes the original achievement button");Click(ui.SettingsAchievementsRect.center);deadline=EditorApplication.timeSinceStartup+8;break;
                    case 2:if(ui.SettingsPage!="Achievements"){Wait(false,"Settings achievement button did not open its menu");return;}LayoutChecks();Capture("locked-phone");break;
                    case 3:Rect view=ui.AchievementsViewportRect;Drag(view.center,view.center-Vector2.up*220);break;
                    case 4:Check(Scroll.y>100,"Touch-style list dragging scrolls achievements");Capture("scrolled-phone");break;
                    case 5:Rect track=ui.AchievementsScrollbarRect;Drag(new Vector2(track.center.x,track.y+4),new Vector2(track.center.x,track.yMax));break;
                    case 6:Check(Scroll.y>=ui.AchievementContentHeight-ui.AchievementsViewportRect.height-2,"Dragging the supplied candy scrollbar reaches the final achievement");Capture("bottom-phone");break;
                    case 7:Click(Back.center);deadline=EditorApplication.timeSinceStartup+8;break;
                    case 8:if(ui.SettingsPage!=null){Wait(false,"Achievements Back did not return to main Settings");return;}Check(game.ScreenState==GameScreen.Settings,"Achievements opened from Settings return to Settings");game.Show(GameScreen.Results);break;
                    case 9:Click(ui.ResultsAchievementsRect.center);deadline=EditorApplication.timeSinceStartup+8;break;
                    case 10:if(ui.SettingsPage!="Achievements"){Wait(false,"Results achievement button did not open its menu");return;}Check(game.ScreenState==GameScreen.Settings,"Original results achievement button opens the local menu");ui.Back();deadline=EditorApplication.timeSinceStartup+8;break;
                    case 11:if(game.ScreenState!=GameScreen.Results){Wait(false,"Results achievement Back did not restore results");return;}Check(ui.SettingsPage==null,"Achievements opened from Results return to the same Results screen");game.ReturnToMenu();game.Achievements.BeginGame();for(int i=0;i<10;i++)game.Achievements.Resolve(true,i==9,i<4,i<4?65:i==9?100:50,false);game.Achievements.Reload();ui.OpenAchievements();deadline=EditorApplication.timeSinceStartup+8;break;
                    case 12:if(ui.SettingsPage!="Achievements"){Wait(false,"Mixed achievement menu did not open");return;}Check(game.Achievements.UnlockedCount>0&&game.Achievements.UnlockedCount<20,"Menu supports mixed locked and unlocked achievement art");Capture("mixed-phone");break;
                    case 13:Resolution(768,1024);break;
                    case 14:LayoutChecks();Capture("mixed-tablet");break;
                    case 15:Resolution(1920,1080);break;
                    case 16:LayoutChecks();Capture("mixed-wide");break;
                    case 17:Resolution(640,1440);break;
                    case 18:LayoutChecks();Capture("mixed-tall");break;
                    case 19:
                        game.ReturnToMenu();PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);game.Achievements.Reload();Resolution(640,1136);game.StartGame();game.Pause();AudioListener.pause=false;AudioListener.volume=0;if(!game.SoundEnabled)game.ToggleSound();
                        game.Achievements.Resolve(true,false,false,5,true);break;
                    case 20:
                        Check(ui.ActiveAchievementNotification?.id=="fastfood"&&ui.PendingAchievementNotifications==0,"A newly earned achievement starts its notification after saving progress");
                        Check(Time.timeScale==0&&ui.AchievementNotificationAlpha>.95f,"Achievement fade-in uses unscaled time while gameplay is paused");
                        Check(game.GetComponents<AudioSource>().Any(a=>a.isPlaying&&!a.loop)&&(AudioClip)typeof(CandyFlickUI).GetField("achievementNotificationSound",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui)==Resources.Load<AudioClip>("LanAudio/calibrated"),"Starting an achievement notification plays the shared calibration star sound");NotificationLayout();Capture("notification-phone");break;
                    case 21:
                        typeof(CandyFlickUI).GetField("achievementNotificationStarted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,Time.unscaledTime-2.34f);
                        Check(ui.AchievementNotificationAlpha>.35f&&ui.AchievementNotificationAlpha<.65f,"The notification fades out smoothly before disappearing");Capture("notification-fade");break;
                    case 22:
                        Check(ui.ActiveAchievementNotification==null,"The notification disappears when its short display interval finishes");
                        game.Achievements.Resolve(true,false,false,5,true);game.Achievements.Reload();
                        Check(ui.PendingAchievementNotifications==0&&ui.ActiveAchievementNotification==null,"Already unlocked achievements and reloading progress do not replay notifications");
                        game.Achievements.Resolve(true,false,false,610,false);break;
                    case 23:
                        Check(ui.ActiveAchievementNotification!=null&&ui.PendingAchievementNotifications==3,"Four achievements earned together queue without overlapping or dropping entries");
                        var seen=new HashSet<string>();
                        while(ui.ActiveAchievementNotification!=null)
                        {
                            Check(seen.Add(ui.ActiveAchievementNotification.id),"Each queued achievement is displayed once: "+ui.ActiveAchievementNotification.id);
                            typeof(CandyFlickUI).GetField("achievementNotificationStarted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,Time.unscaledTime-3);
                            Call(ui,"UpdateAchievementNotifications");
                        }
                        Check(seen.Count==4,"All four score achievements are shown from the same result");Resolution(768,1024);break;
                    case 24:NotificationFixture();NotificationLayout();Capture("notification-tablet");break;
                    case 25:Resolution(1920,1080);break;
                    case 26:NotificationFixture();NotificationLayout();Capture("notification-wide");break;
                    case 27:Resolution(640,1440);break;
                    case 28:NotificationFixture();NotificationLayout();Capture("notification-tall");break;
                    case 29:
                        game.Achievements.Reload();Check(ui.ActiveAchievementNotification==null&&ui.PendingAchievementNotifications==0,"Reload/reset clears any pending achievement notifications");
                        game.ToggleSound();for(int i=0;i<5;i++)game.Achievements.Resolve(false,false,false,0,false);break;
                    case 30:Check(!game.SoundEnabled&&ui.ActiveAchievementNotification?.id=="tummyteaser"&&!game.GetComponents<AudioSource>().Any(a=>a.isPlaying&&!a.loop),"Muted sound keeps the unlock notification visible without playing the star cue");break;
                    default:File.WriteAllText("Documentation/achievements-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static void NotificationFixture()
        {
            Call(ui,"ClearAchievementNotifications");
            typeof(CandyFlickUI).GetField("activeAchievementNotification",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,Entry(game.Achievements,"ringmaster"));
            typeof(CandyFlickUI).GetField("achievementNotificationStarted",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ui,Time.unscaledTime-.5f);
        }
        static void NotificationLayout()
        {
            Rect box=ui.AchievementNotificationRect,safe=Screen.safeArea;
            float ratio=(float)Screen.width/Screen.height;
            Check((ratio>.75f?Mathf.Abs(box.width/Screen.height-.705f)<.0001f:Mathf.Abs(box.width/Screen.width-.94f)<.0001f)&&Mathf.Abs(Screen.width-box.xMax)<.1f&&Mathf.Abs(Screen.height-box.yMax)<.1f,"Notification nearly spans portrait screens, caps at 3:4 and touches the actual bottom-right screen edges at "+Screen.width+"x"+Screen.height);
            float scale=CandyFlickUI.CanvasScale(safe),width=box.width/scale,h=box.height/scale;
            foreach(var entry in game.Achievements.Definitions.entries)
            {
                object[] args={entry,width,h,null,null,null,null};Call(ui,"AchievementNotificationTextLayout",args);
                var name=(Rect)args[3];var description=(Rect)args[4];var nameStyle=(GUIStyle)args[5];var descriptionStyle=(GUIStyle)args[6];
                if(name.yMax>description.y||description.yMax>h*.95f+.1f||nameStyle.normal.textColor!=Color.white||descriptionStyle.normal.textColor!=Color.black)throw new Exception("Notification text overflow/style: "+entry.id+" at "+Screen.width+"x"+Screen.height);
            }
            Check(true,"All twenty official titles/descriptions fit with the achievement menu's colors at "+Screen.width+"x"+Screen.height);
            Check(File.ReadAllBytes("Assets/Resources/Achievements/notification.png").SequenceEqual(File.ReadAllBytes("D:/Downloads/menu_notification.png")),"Notification background keeps the supplied pixels unchanged");
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            if(game)game.ReturnToMenu();
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}
            if(SessionState.GetBool(Key+"SaveHad",false))PlayerPrefs.SetString(LocalAchievements.SaveKey,SessionState.GetString(Key+"Save",""));else PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);PlayerPrefs.Save();
            AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);AudioListener.pause=SessionState.GetBool(Key+"Audio",false);string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.delayCall+=()=>EditorApplication.Exit(error==null?0:1);
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
