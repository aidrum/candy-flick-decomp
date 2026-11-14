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
    public static class RestartAnimationValidation
    {
        const string Key="CandyFlickRestartAnimationTest",Folder="Documentation/RestartAnimation";
        static readonly string[] prefs={TimeTravelSkins.Preference,CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static Animation player;static AnimationClip originalIdle;static object previousSession;
        static int stage;static double next;static bool finishing;
        static RestartAnimationValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        [MenuItem("Candy Flick/Validate queued animations on restart")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));PlayerPrefs.SetInt(pref,0);}PlayerPrefs.Save();
            SessionState.SetString(Key+"StartScene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{640,1136});
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static void Queue(bool petting)
        {
            player.Stop();player.Play(petting?"touch_left_in":"jump");
            AnimationState queued=petting?player.PlayQueued("touch_left"):player.CrossFadeQueued("idle");
            Check(queued!=null&&queued.name!=(petting?"touch_left":"idle"),"Unity creates a temporary queued animation state: "+queued?.name);
        }
        static void ValidateBindings()
        {
            var skin=game.Skins.Selected;
            var expected=skin==NommieSkin.Original?originalIdle:game.Skins.Visual.GetComponent<TimeTravelVisual>().clips.Single(c=>c.action=="idle").clip;
            Check(player["idle"]!=null&&MatchesClip(player["idle"].clip,expected)&&player.IsPlaying("idle"),skin+": correct idle curves are bound and playing after rebinding");
            Check(new[]{"idle2","idle3","idle4","open","close_eat","close_sad","jump","turn_left","turn_right","tap","touch_left_in","touch_left","touch_left_out","touch_right_in","touch_right","touch_right_out"}.All(name=>player[name]!=null),skin+": all original action bindings remain available");
            if(skin!=NommieSkin.Original)Check(player["idle"].wrapMode==WrapMode.Loop&&player["open"].wrapMode==WrapMode.ClampForever,skin+": imported idle and mouth-open wrap modes remain intact");
        }
        // Animation.AddClip copies the clip and names that copy after the action.
        // Compare its actual animation data, rather than the asset identity/name.
        static bool MatchesClip(AnimationClip actual,AnimationClip expected)
        {
            if(Mathf.Abs(actual.length-expected.length)>.0001f)return false;
            var bindings=AnimationUtility.GetCurveBindings(expected);
            if(bindings.Length!=AnimationUtility.GetCurveBindings(actual).Length)return false;
            foreach(var binding in bindings)
            {
                var a=AnimationUtility.GetEditorCurve(actual,binding);var b=AnimationUtility.GetEditorCurve(expected,binding);
                if(a==null||b==null||!a.keys.SequenceEqual(b.keys))return false;
            }
            return true;
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
                    Application.runInBackground=true;AudioListener.pause=true;
                    game.StartGame();game.enabled=false;game.Actor.enabled=false;
                    player=game.Actor.GetComponent<Animation>();player.cullingType=AnimationCullingType.AlwaysAnimate;originalIdle=player["idle"].clip;return;
                }
                if(stage<TimeTravelSkins.Names.Length)
                {
                    game.Skins.Select((NommieSkin)stage);Queue(stage%2==1);
                    game.Skins.Select((NommieSkin)((stage+1)%TimeTravelSkins.Names.Length));
                    if(finishing)return;ValidateBindings();stage++;return;
                }
                if(stage==8){game.Skins.SelectRandom();stage++;return;}
                if(stage<21)
                {
                    Queue(stage%2==1);game.StartGame();if(finishing)return;ValidateBindings();
                    Check(game.Skins.RandomSelected&&PlayerPrefs.GetInt(TimeTravelSkins.Preference)==TimeTravelSkins.RandomChoice,"Random restart retains the saved Random choice");stage++;return;
                }
                if(stage<27)
                {
                    if(stage%2==1)
                    {
                        Queue(false);previousSession=game.Session;
                        typeof(CandyFlickUI).GetMethod("RestartTapped",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.GetComponent<CandyFlickUI>(),null);
                    }
                    else
                    {
                        Check(!ReferenceEquals(previousSession,game.Session),"Camera-flash restart coroutine begins a new game with a queued idle");ValidateBindings();
                    }
                    stage++;return;
                }
                File.WriteAllText("Documentation/restart-animation-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void OnLog(string message,string stack,LogType type)
        {
            if(message.Contains("Unable to remove Animation Clip")||((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase")))Finish(message+"\n"+stack);
        }
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            string previous=SessionState.GetString(Key+"StartScene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
