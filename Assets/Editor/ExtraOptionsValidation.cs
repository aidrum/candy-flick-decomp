using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using CandyFlick.Original26;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class ExtraOptionsValidation
    {
        const string Key="CandyFlickExtraOptionsTest", Folder="Documentation/ExtraOptions";
        static readonly string[] prefs={CandyFlickGame.HideCandyPreference,CandyFlickGame.InGameMusicPreference,TimeTravelSkins.Preference,"IsMusic",CandyFlickGame.ControlModePreference,CandyFlickGame.NonARPreference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static int stage,hiddenPasses;
        static double next;static bool incorrectRender;
        static Renderer[] photoRenderers;static bool[] savedFlags;
        static ExtraOptionsValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetFloat(Key+"Volume",AudioListener.volume);SessionState.SetBool(Key+"Pause",AudioListener.pause);
            PlayerPrefs.DeleteKey(CandyFlickGame.HideCandyPreference);PlayerPrefs.DeleteKey(CandyFlickGame.InGameMusicPreference);
            PlayerPrefs.DeleteKey(CandyFlickGame.NonARPreference);
            PlayerPrefs.SetInt("IsMusic",1);PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.Save();
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{640,1136});
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static object Call(object obj,string method,params object[] args)=>obj.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);
        static void Click(Vector2 point)=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static Vector2 PhotoPoint()
        {
            Rect rect=(Rect)Call(ui,"R","GamePlayScreen$$GetRestartRectGP",true);
            rect.y=1136-(int)(rect.height*1.5f);return rect.center;
        }
        static void MoreOptions()
        {
            game.Show(GameScreen.Settings);
            typeof(CandyFlickUI).GetField("settingsPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ui,"MoreOptions");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.8;
            try
            {
                if(!game)
                {
                    game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
                    ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=false;AudioListener.volume=0;
                    return; // Let the native first-entry menu animation finish before the preview.
                }
                switch(stage++)
                {
                    case 0:
                        Check(game.HideCandyInPictures&&game.InGameMusicEnabled,"Photo hiding and in-game music default on with no saved preferences");
                        ValidateMusic();game.ReturnToMenu();Capture("main-menu-lowered-skins");
                        Check(ui.MenuMultiplayerRect.yMax<=1136&&Mathf.Approximately(ui.MenuMultiplayerRect.y,ui.MenuSkinsRect.y)&&!ui.MenuMultiplayerRect.Overlaps(ui.MenuSkinsRect),"Skins and LAN menu row fits above the bottom edge");Click(ui.MenuSkinsRect.center);break;
                    case 1: Check(game.ScreenState==GameScreen.Skins,"Lowered main menu button opens skins");Click(ui.SkinViewport.position+CandyFlickUI.SkinCardRect(TimeTravelSkins.RandomChoice).center);break;
                    case 2:
                        Check(game.Skins.RandomSelected&&PlayerPrefs.GetInt(TimeTravelSkins.Preference)==TimeTravelSkins.RandomChoice,"Text-only Random card selects and saves random mode");Capture("skins-random");
                        break;
                    case 3:
                        var skins=new HashSet<NommieSkin>();var tracks=new HashSet<string>();
                        for(int i=0;i<20;i++){game.StartGame();skins.Add(game.Skins.Selected);tracks.Add(game.GameplayMusic.clip.name);}
                        Check(skins.Count>1&&skins.All(s=>Enum.IsDefined(typeof(NommieSkin),s)),"New games choose varying valid appearances while Random remains selected");
                        Check(tracks.Count>1,"New games choose varying songs from the six supplied tracks");
                        Check(game.Skins.RandomSelected&&PlayerPrefs.GetInt(TimeTravelSkins.Preference)==TimeTravelSkins.RandomChoice,"Rerolling keeps the saved Random choice");break;
                    case 4: Check(game.GameplayMusic.isPlaying&&game.GameplayMusic.loop,"Enabled gameplay music plays and loops");game.Pause();break;
                    case 5: Check(!game.GameplayMusic.isPlaying,"Pause pauses gameplay music");game.Resume();break;
                    case 6:
                        Check(game.GameplayMusic.isPlaying,"Resume resumes gameplay music");game.ToggleMusic();
                        Check(!game.GameplayMusic.isPlaying,"Existing master music toggle also mutes gameplay music");game.ToggleMusic();
                        Check(game.GameplayMusic.isPlaying&&!game.GetComponents<AudioSource>().Single(s=>s.clip==game.assets.music).isPlaying,"Re-enabling music in gameplay resumes the song without starting the menu theme");
                        game.ToggleInGameMusic();Check(!game.GameplayMusic.isPlaying,"In-game music switch stops the song immediately");game.ReturnToMenu();break;
                    case 7:
                        Check(game.GetComponents<AudioSource>().Single(s=>s.clip==game.assets.music).isPlaying,"Disabling in-game music leaves the main menu theme available");
                        game.ToggleInGameMusic();MoreOptions();break;
                    case 8: Capture("more-options-default-on");break;
                    case 9: Click(ui.MoreHideCandyRect.center);break;
                    case 10: Check(!game.HideCandyInPictures&&PlayerPrefs.GetInt(CandyFlickGame.HideCandyPreference,-1)==0,"Hide candy toggle in More Options persists off");Click(ui.MoreMusicRect.center);break;
                    case 11: Check(!game.InGameMusicEnabled&&PlayerPrefs.GetInt(CandyFlickGame.InGameMusicPreference,-1)==0,"In-game music toggle in More Options persists off");Capture("more-options-off");Click(new Vector2(79,1057));break;
                    case 12: Check(game.ScreenState==GameScreen.Settings&&ui.SettingsPage==null,"Back from six-option submenu returns to settings");game.StartGame();break;
                    case 13:
                        Check(!game.GameplayMusic.isPlaying,"Disabled in-game music stays silent when starting a game");
                        game.Skins.Select(NommieSkin.Original);Check(!game.Skins.RandomSelected,"Selecting a fixed skin leaves Random mode");
                        game.enabled=false;game.Actor.enabled=false;Time.timeScale=0;
                        var animation=game.Actor.GetComponent<Animation>();animation.Stop();animation.Play("idle");animation["idle"].time=.3f;animation.Sample();
                        Check(game.CandyGhost.gameObject.activeSelf&&game.Player.Find("elastic").gameObject.activeSelf,"Photo fixture shows the ready candy and slingshot");
                        game.CandyGhost.Find("CandyGhost/SuperEffect").gameObject.SetActive(true);
                        photoRenderers=new[]{game.World.Find("Candies"),game.CandyGhost,game.Player.Find("elastic"),game.Player.Find("test")}.SelectMany(t=>t.GetComponentsInChildren<Renderer>(true)).ToArray();
                        savedFlags=photoRenderers.Select(r=>r.forceRenderingOff).ToArray();Camera.onPreCull+=Observe;
                        Click(PhotoPoint());break;
                    case 14:
                        Check(!game.PhotoVisibility.IsHiding&&hiddenPasses==0,"Disabled photo option uses the existing visible-candy capture behavior");SavePhoto("photo-candy-visible");
                        next=EditorApplication.timeSinceStartup+3.2;break;
                    case 15:
                        Call(ui,"ClosePopup");game.ToggleHideCandyInPictures();
                        Check(game.HideCandyInPictures&&PlayerPrefs.GetInt(CandyFlickGame.HideCandyPreference)==1,"Hide candy toggle persists on");
                        Check(!CaptureManager26.IsScreenShotSaving,"First photo finishes saving before the next camera press");Click(PhotoPoint());break;
                    case 16:
                        Check(hiddenPasses>0&&!incorrectRender,"Actual photo camera renders suppress every candy, slingshot and recoil renderer");
                        Check(!game.PhotoVisibility.IsHiding&&photoRenderers.Select(r=>r.forceRenderingOff).SequenceEqual(savedFlags),"Capture completion restores all prior renderer visibility flags");
                        SavePhoto("photo-candy-hidden");ComparePhotos();next=EditorApplication.timeSinceStartup+3.2;break;
                    case 17:
                        Call(ui,"ClosePopup");game.PhotoVisibility.Begin(true);game.StartGame();
                        Check(!game.PhotoVisibility.IsHiding,"Restart cancels photo hiding and restores visibility");
                        game.PhotoVisibility.Begin(true);game.ReturnToMenu();Check(!game.PhotoVisibility.IsHiding,"Leaving gameplay cancels photo hiding");
                        game.ToggleInGameMusic();Check(game.InGameMusicEnabled&&PlayerPrefs.GetInt(CandyFlickGame.InGameMusicPreference)==1,"In-game music toggle persists on");
                        break;
                    default:
                        File.WriteAllText("Documentation/extra-options-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion,checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void ValidateMusic()
        {
            var clips=Resources.LoadAll<AudioClip>("GameplayMusic");Check(clips.Length==6,"All six supplied songs are available as gameplay resources");
            var method=typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil").GetMethod("GetTargetPlatformSoundCompressionFormat");
            foreach(var clip in clips)
            {
                var importer=(AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));var settings=importer.GetOverrideSampleSettings("Android");
                Check(settings.compressionFormat==AudioCompressionFormat.MP3&&method.Invoke(null,new object[]{clip}).ToString()=="MP3",clip.name+": imported target-platform audio is MP3");
                Check(clip.loadType==AudioClipLoadType.Streaming&&!settings.preloadAudioData&&clip.length>30,clip.name+": music streams without preloading decoded PCM");
            }
        }
        static void Observe(Camera camera)
        {
            if(!game||!game.PhotoVisibility.IsHiding||(camera!=game.GameCamera&&camera!=game.CandyCamera))return;
            hiddenPasses++;if(photoRenderers.Any(r=>!r.forceRenderingOff))incorrectRender=true;
        }
        static void SavePhoto(string name)
        {
            string path=game.GetComponent<CaptureManager26>().LastCapturePath;
            Check(File.Exists(path)&&new FileInfo(path).Length>1000,"Camera button writes "+name+" PNG");File.Copy(path,Folder+"/"+name+".png",true);
        }
        static void ComparePhotos()
        {
            var a=new Texture2D(2,2);var b=new Texture2D(2,2);
            try
            {
                a.LoadImage(File.ReadAllBytes(Folder+"/photo-candy-visible.png"));b.LoadImage(File.ReadAllBytes(Folder+"/photo-candy-hidden.png"));
                var first=a.GetPixels32();var second=b.GetPixels32();int changed=0;
                for(int i=0;i<first.Length;i++)if(Mathf.Abs(first[i].r-second[i].r)+Mathf.Abs(first[i].g-second[i].g)+Mathf.Abs(first[i].b-second[i].b)>30)changed++;
                Check(changed>100,"Saved visible/hidden photos differ in rendered candy and launcher pixels ("+changed+")");
            }
            finally{UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;Camera.onPreCull-=Observe;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);AudioListener.pause=SessionState.GetBool(Key+"Pause",false);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,unity;public string[] checks;}
    }
}
