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
    public static class UpdateCheckerValidation
    {
        const string Key="CandyFlickUpdateCheckTest",Folder="Documentation/UpdateChecker";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly string[] preferences={GameUpdateChecker.Preference,CandyFlickGame.NonARPreference,"Language"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static GameUpdateChecker checker;
        static int stage,downloads;static double next,deadline;static bool finishing;
        static UpdateCheckerValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate update checker and Photo menu artwork")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            PhotoMenuAssetsBuilder.Build();
            foreach(string pref in preferences){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            PlayerPrefs.DeleteKey(GameUpdateChecker.Preference);PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt("Language",0);PlayerPrefs.Save();
            SessionState.SetFloat(Key+"Volume",AudioListener.volume);SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetFloat(Key+"Started",(float)EditorApplication.timeSinceStartup);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);checks.Add(message);}
        static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Private).Invoke(target,args);
        static void Set(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
        static T Get<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
        static Rect Rect(string name)=>(Rect)ui.GetType().GetProperty(name,Private).GetValue(ui);
        static void Resolution(int width,int height)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{width,height});
        static void Pointer(EventType type,Vector2 canvas)
        {
            Vector2 point=CandyFlickUI.CanvasOrigin(Screen.safeArea,Screen.height,game.ScreenState)+canvas*CandyFlickUI.CanvasScale(Screen.safeArea);
            typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{new Event{type=type,mousePosition=point,button=0,clickCount=1}});
        }
        static void Click(Vector2 point){Pointer(EventType.MouseDown,point);Pointer(EventType.MouseUp,point);}
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static string Release(string version,bool draft=false,bool prerelease=false,string url=null)=>JsonUtility.ToJson(new ReleaseFixture{
            tag_name=version,draft=draft,prerelease=prerelease,html_url=url??"https://github.com/aidrum/candy-flick-decomp/releases/tag/"+version});
        static void Rules()
        {
            Check(!string.IsNullOrWhiteSpace(PlayerSettings.bundleVersion)&&Application.version==PlayerSettings.bundleVersion,"Runtime version follows the current Player Settings version");
            Check(GameUpdateChecker.LatestReleaseUrl.EndsWith("/releases/latest"),"The checker requests GitHub's latest stable release");
            Check(GameUpdateChecker.TryFindUpdate(Release("v1.1"),"1.0",out var update)&&update.Version=="1.1"&&update.Url.EndsWith("/tag/v1.1"),"Newer stable tag with v prefix retains the exact release link");
            Check(!GameUpdateChecker.TryFindUpdate(Release("1.0.0.0"),"1.0",out _),"Equal normalized versions do not prompt");
            Check(!GameUpdateChecker.TryFindUpdate(Release("0.9"),"1.0",out _),"Older releases do not prompt");
            Check(GameUpdateChecker.TryFindUpdate(Release("1.10"),"1.9",out _),"Version components compare numerically");
            Check(!GameUpdateChecker.TryFindUpdate(Release("9.0",draft:true),"1.0",out _),"Draft releases are ignored");
            Check(!GameUpdateChecker.TryFindUpdate(Release("9.0",prerelease:true),"1.0",out _),"Prerelease API records are ignored");
            Check(!GameUpdateChecker.TryFindUpdate(Release("9.0-prerelease+2"),"1.0",out _),"Prerelease tags are ignored even if incorrectly marked stable");
            foreach(string json in new[]{"", "not JSON", "{}", "[]", "null", Release("garbage"),Release("1.2.3.4.5")})
                Check(!GameUpdateChecker.TryFindUpdate(json,"1.0",out _),"Malformed release is ignored: "+json);
            Check(!GameUpdateChecker.TryFindUpdate(Release("2.0"),"unknown",out _),"An invalid installed version cannot produce a misleading notification");
            Check(GameUpdateChecker.TryFindUpdate(Release("1.1",url:"javascript:alert(1)"),"1.0",out update)&&update.Url==GameUpdateChecker.ReleasesPageUrl,"Unexpected release links fall back to the project's HTTPS releases page");
            var normal=Resources.Load<Texture2D>("PhotoModeMenu/photo_mode");var pressed=Resources.Load<Texture2D>("PhotoModeMenu/photo_mode_pressed");
            Check(normal&&pressed&&normal.width==264&&normal.height==263&&pressed.width==264&&pressed.height==263,"Both supplied Photo button images import at their native dimensions");
            var style=(GUIStyle)ui.GetType().GetProperty("PhotoMenuButtonStyle",Private).GetValue(ui);
            Check(style.normal.background==normal&&style.hover.background==normal&&style.active.background==pressed,"Photo mode uses its own normal and pressed artwork");
            Check(ui.MenuPhotoRect.size==ui.MenuSkinsRect.size,"Photo mode button retains its original menu size");
            var catalog=PortLocalization.CatalogData();
            foreach(string key in new[]{"Download","Cancel","A new version of Candy Flick is available.\nVersion: {0}"})
                Check(catalog.entries.Single(e=>e.key==key).text.Length==9,"All nine game languages cover "+key);
            var temporary=new GameObject("Update request lifecycle fixture");
            try
            {
                var owner=temporary.AddComponent<GameUpdateChecker>();
                var pending=(System.Collections.IEnumerator)Call(owner,"CheckLatestRelease");
                pending.MoveNext();((IDisposable)pending).Dispose();
                Check(Get<object>(owner,"request")==null,"Disposing an in-flight check clears its request before the component is destroyed");
            }
            finally{UnityEngine.Object.DestroyImmediate(temporary);}
        }
        static bool Await(bool ready,string message)
        {
            if(ready)return true;if(EditorApplication.timeSinceStartup>deadline)throw new Exception(message);stage--;return false;
        }
        static void MoreOptions(){game.Show(GameScreen.Settings);Set(ui,"settingsPage","MoreOptions");}
        static void PopupBounds(string name)
        {
            Rect note=Rect("PopupRect"),message=Rect("PopupMessageRect"),primary=Rect("PopupButtonRect"),cancel=Rect("PopupCancelRect");
            float height=Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea);
            Check(note.x>=0&&note.xMax<=640&&note.y>=0&&note.yMax<=height,"Native update note fits "+name);
            Check(note.Contains(primary.min)&&note.Contains(primary.max)&&note.Contains(cancel.min)&&note.Contains(cancel.max)&&message.yMax<primary.y&&primary.yMax<cancel.y,"Message, Download and Cancel have separate readable areas at "+name);
            Check(ui.MenuPhotoRect.size==ui.MenuSkinsRect.size,"Photo icon size stays unchanged at "+name);
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.4;
            try
            {
                if(EditorApplication.timeSinceStartup-SessionState.GetFloat(Key+"Started",0)>180)throw new TimeoutException("Update checker validation timed out");
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();checker=game.Updates;AudioListener.pause=true;AudioListener.volume=0;Application.runInBackground=true;deadline=EditorApplication.timeSinceStartup+20;return;}
                switch(stage++)
                {
                    case 0:
                        if(!Await(ui.CanShowUpdatePrompt,"Initial menu did not finish entering"))break;
                        Rules();Check(checker.ChecksEnabled&&!checker.CheckedThisLaunch,"Checks default on while editor previews avoid automatic network prompts");
                        game.GroundSupport.EditorSupportOverride=false;game.GroundSupport.Refresh();Capture("main-menu");break;
                    case 1: Pointer(EventType.MouseDown,ui.MenuPhotoRect.center);break;
                    case 2: Capture("photo-button-pressed");break;
                    case 3: Pointer(EventType.MouseUp,ui.MenuPhotoRect.center);deadline=EditorApplication.timeSinceStartup+10;break;
                    case 4:
                        if(!Await(CandyFlickUI.PopupActive,"Photo button did not open the unsupported-device note"))break;
                        Check(!game.IsPhotoMode&&Get<string>(ui,"popupSecondaryButton")==null,"New Photo artwork retains the original mode action and one-button support warning");Capture("photo-support-warning");break;
                    case 5: Click(Rect("PopupButtonRect").center);break;
                    case 6:
                        Check(!CandyFlickUI.PopupActive,"Original one-button warning still closes normally");MoreOptions();
                        checker.CheckForUpdates();var request=Get<object>(checker,"request");checker.CheckForUpdates();
                        Check(checker.CheckedThisLaunch&&ReferenceEquals(request,Get<object>(checker,"request")),"Repeated checks do not start a second request this launch");deadline=EditorApplication.timeSinceStartup+23;break;
                    case 7:
                        if(!Await(Get<object>(checker,"request")==null,"Background release check exceeded its timeout"))break;
                        Check(game.ScreenState==GameScreen.Settings&&!CandyFlickUI.PopupActive,"A real background request completes or fails quietly without interrupting the screen");
                        Set(checker,"<Pending>k__BackingField",null);game.StartGame(false);Call(checker,"AcceptRelease",Release("1.1"));break;
                    case 8: Check(checker.Pending!=null&&!CandyFlickUI.PopupActive,"New releases wait while playing");game.Pause();break;
                    case 9: Check(checker.Pending!=null&&!CandyFlickUI.PopupActive,"New releases wait while paused");game.ReturnToMenu();deadline=EditorApplication.timeSinceStartup+10;break;
                    case 10:
                        if(!Await(CandyFlickUI.PopupActive,"Pending update was not shown on returning to the menu"))break;
                        Check(checker.Pending==null&&Get<string>(ui,"popupMessage").Contains("1.1")&&Get<string>(ui,"popupButton")=="Download"&&Get<string>(ui,"popupSecondaryButton")=="Cancel","Main menu consumes the update once and shows Download / Cancel");
                        Set(ui,"popupConfirm",(Action)(()=>downloads++));PopupBounds("640x1136");Capture("update-phone");break;
                    case 11: Pointer(EventType.MouseDown,new Vector2(1,1));Pointer(EventType.MouseUp,Rect("PopupButtonRect").center);break;
                    case 12:
                        Check(CandyFlickUI.PopupActive&&downloads==0,"Releasing over Download after pressing outside never activates it");
                        Pointer(EventType.MouseDown,Rect("PopupButtonRect").center);Pointer(EventType.MouseDrag,Rect("PopupCancelRect").center);Pointer(EventType.MouseUp,Rect("PopupCancelRect").center);break;
                    case 13: Check(CandyFlickUI.PopupActive&&downloads==0,"Dragging between the two buttons does not accept or cancel");Click(Rect("PopupCancelRect").center);break;
                    case 14:
                        Check(!CandyFlickUI.PopupActive&&downloads==0&&checker.Pending==null,"Cancel closes the prompt without opening a link or requeuing the update");
                        ui.ShowUpdateAvailable("1.1",GameUpdateChecker.ReleasesPageUrl);Set(ui,"popupConfirm",(Action)(()=>downloads++));Pointer(EventType.MouseDown,Rect("PopupButtonRect").center);break;
                    case 15: Capture("download-pressed");break;
                    case 16: Pointer(EventType.MouseUp,Rect("PopupButtonRect").center);break;
                    case 17:
                        Check(!CandyFlickUI.PopupActive&&downloads==1,"Download invokes its link action exactly once on release and closes the popup");
                        ui.ShowPopupWithMessageKey("PHOTO_SAVED");Check(Get<string>(ui,"popupSecondaryButton")==null&&Rect("PopupButtonRect").Equals((Rect)Call(ui,"CenteredPopupRect","GetBtnRect")),"Saved-image notes keep their native one-button layout");Capture("photo-saved");break;
                    case 18: Click(Rect("PopupButtonRect").center);break;
                    case 19:
                        Check(!CandyFlickUI.PopupActive&&downloads==1,"Opening a normal notification clears the previous Download action");
                        ui.ShowUpdateAvailable("1.1",GameUpdateChecker.ReleasesPageUrl);
                        typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{new Event{type=EventType.KeyDown,keyCode=KeyCode.Escape}});break;
                    case 20:
                        Check(!CandyFlickUI.PopupActive&&game.ScreenState==GameScreen.Menu,"Escape cancels the update without exiting the menu");MoreOptions();
                        var rows=new[]{ui.MoreControlsRect,ui.MoreFpsRect,ui.MoreHideCandyRect,ui.MoreMusicRect,ui.MoreUpdatesRect,ui.MoreARModeRect};
                        Check(rows.Zip(rows.Skip(1),(a,b)=>a.yMax<b.y).All(v=>v)&&rows.Last().yMax<Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea)-120,"All six More Options buttons fit without overlapping");Capture("more-options");break;
                    case 21: Click(ui.MoreUpdatesRect.center);break;
                    case 22:
                        Check(!checker.ChecksEnabled&&PlayerPrefs.GetInt(GameUpdateChecker.Preference)==0,"The update-check option turns off and persists");Call(checker,"AcceptRelease",Release("1.2"));game.ReturnToMenu();break;
                    case 23: Check(!CandyFlickUI.PopupActive&&checker.Pending!=null,"Disabled checks suppress even an already downloaded update");MoreOptions();Click(ui.MoreUpdatesRect.center);break;
                    case 24:
                        Check(checker.ChecksEnabled&&PlayerPrefs.GetInt(GameUpdateChecker.Preference)==1&&checker.CheckedThisLaunch&&Get<object>(checker,"request")==null,"Re-enabling persists the preference without duplicating a completed launch check");game.ReturnToMenu();deadline=EditorApplication.timeSinceStartup+10;break;
                    case 25: if(!Await(CandyFlickUI.PopupActive,"Re-enabled check did not offer its pending result"))break;Check(Get<string>(ui,"popupMessage").Contains("1.2"),"A pending release can be offered after checks are re-enabled");break;
                    case 26: Click(Rect("PopupCancelRect").center);break;
                    case 27: Resolution(768,1024);break;
                    case 28: ui.ShowUpdateAvailable("1.1",GameUpdateChecker.ReleasesPageUrl);break;
                    case 29: PopupBounds("768x1024");Capture("update-tablet");break;
                    case 30: Click(Rect("PopupCancelRect").center);break;
                    case 31: Resolution(1920,1080);break;
                    case 32: ui.ShowUpdateAvailable("1.1",GameUpdateChecker.ReleasesPageUrl);break;
                    case 33: PopupBounds("1920x1080");Capture("update-wide");break;
                    case 34: Click(Rect("PopupCancelRect").center);break;
                    case 35: Resolution(640,1422);break;
                    case 36: ui.ShowUpdateAvailable("1.1",GameUpdateChecker.ReleasesPageUrl);break;
                    case 37: PopupBounds("640x1422");Capture("update-tall");break;
                    case 38: Click(Rect("PopupCancelRect").center);break;
                    default:
                        File.WriteAllText("Documentation/update-checker-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),version=Application.version,checks=checks.ToArray(),limit="Unity editor rendering and real input events; real stable-endpoint request (currently private), deterministic release fixtures for update prompts; browser launch action intercepted during UI checks."},true));Finish(null);break;
                }
            }
            catch(Exception exception){Finish(exception.ToString());}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in preferences){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);AudioListener.pause=SessionState.GetBool(Key+"Audio",false);
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);
            EditorApplication.ExitPlaymode();if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);
        }
        [Serializable]sealed class ReleaseFixture{public string tag_name,html_url;public bool draft,prerelease;}
        [Serializable]sealed class Evidence{public string utc,version,limit;public string[] checks;}
    }
}
