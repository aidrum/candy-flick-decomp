using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class CameraProjectionValidation
    {
        const string Key="CandyFlickLensTest",Report="Documentation/camera-projection-validation.json";
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;
        static int stage;
        static int expectedWidth,expectedHeight;
        static double next,viewportReady;
        static bool finishing;
        static Matrix4x4 portrait;
        static CameraProjectionValidation(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        [MenuItem("Candy Flick/Validate calibrated Non AR projection")]
        public static void Run()
        {
            Directory.CreateDirectory("Documentation");File.Delete("Documentation/camera-projection-failure.txt");
            foreach(string pref in new[]{NonARStage.LensPreference,CandyFlickGame.NonARPreference})
            {
                SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));
                if(pref==NonARStage.LensPreference)SessionState.SetString(Key+pref,PlayerPrefs.GetString(pref));
                else SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));
            }
            PlayerPrefs.DeleteKey(NonARStage.LensPreference);PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int width,int height)
        {
            expectedWidth=width;expectedHeight=height;viewportReady=EditorApplication.timeSinceStartup+1;
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{width,height});
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void SameProjection(Matrix4x4 expected,Camera actual,string label)
        {
            expected.m22=-(actual.farClipPlane+actual.nearClipPlane)/(actual.farClipPlane-actual.nearClipPlane);
            expected.m23=-2*actual.farClipPlane*actual.nearClipPlane/(actual.farClipPlane-actual.nearClipPlane);
            for(int i=0;i<16;i++)if(Mathf.Abs(actual.projectionMatrix[i]-expected[i])>.000001f)throw new Exception(label+" (element "+i+")");
            checks.Add(label);
        }
        static Matrix4x4 Lens(float fov)
        {
            var projection=Matrix4x4.Perspective(fov,game.GameCamera.aspect,.01f,20);
            projection.m02=.067f;projection.m12=-.034f;
            return projection;
        }
        static void Refresh()=>game.NonARBackground.SendMessage("LateUpdate");
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            if(expectedWidth==0){expectedWidth=640;expectedHeight=1136;viewportReady=EditorApplication.timeSinceStartup+1;}
            if(EditorApplication.timeSinceStartup<viewportReady)return;
            if(Screen.width!=expectedWidth || Screen.height!=expectedHeight)
            {
                if(EditorApplication.timeSinceStartup>viewportReady+10)Finish("Game View did not resize to the requested viewport");
                return;
            }
            next=EditorApplication.timeSinceStartup+.4;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;game.StartGame();game.Pause();return;}
                var room=game.NonARBackground;
                switch(stage++)
                {
                    case 0:
                        Check(!room.HasCalibratedLens,"First Non AR launch has no fabricated device calibration");
                        Check(!game.ar.CalibratingForNonAR && !game.ar.Camera && game.ar.TargetCount==0,"Camera-less editor never opens capture or loads tracking targets for calibration");
                        SameProjection(Matrix4x4.Perspective(51,game.GameCamera.aspect,1,100),game.GameCamera,"Unavailable physical camera retains original vertical FOV without the screen-aware estimate");
                        var invalid=Matrix4x4.identity;
                        Check(!room.RememberARProjection(invalid,game.GameCamera.aspect),"Orthographic or uninitialized matrices are not cached");
                        invalid=Lens(64);invalid.m00=float.NaN;
                        Check(!room.RememberARProjection(invalid,game.GameCamera.aspect),"Nonfinite matrices are rejected");
                        Check(!room.RememberARProjection(Lens(64),float.NaN),"Nonfinite viewport aspects are rejected");
                        portrait=Lens(64);Check(room.RememberARProjection(portrait,game.GameCamera.aspect),"A calibrated SDK projection is accepted");
                        Refresh();
                        SameProjection(portrait,game.GameCamera,"Main camera exactly matches calibrated portrait optics and lens offsets");
                        SameProjection(portrait,game.CandyCamera,"Candy camera exactly matches calibrated portrait optics with its own clipping planes");
                        Check(game.ScreenState==GameScreen.Paused && Time.timeScale==0,"Projection updates work while gameplay is paused");
                        room.SaveARLens();
                        var probe=new GameObject("Persisted lens probe").AddComponent<NonARStage>();probe.Initialize(game);
                        Check(probe.HasCalibratedLens,"A new session loads persisted calibration without starting AR first");
                        var reloaded=probe.ProjectionFor(game.GameCamera.aspect,game.GameCamera.nearClipPlane,game.GameCamera.farClipPlane);
                        Check(Mathf.Abs(reloaded.m00-portrait.m00)<.000001f && Mathf.Abs(reloaded.m12-portrait.m12)<.000001f,"Persistence retains focal length and optical center");
                        UnityEngine.Object.Destroy(probe.gameObject);
                        Resolution(768,1024);break;
                    case 1:
                        Check(!room.HasCalibratedLens,"A tablet viewport cannot reuse a stretched phone calibration");
                        var tablet=Lens(71);room.RememberARProjection(tablet,game.GameCamera.aspect);Refresh();
                        SameProjection(tablet,game.GameCamera,"Tablet calibration is applied exactly");
                        SameProjection(tablet,game.CandyCamera,"Tablet candy view uses the same calibrated optics");
                        Resolution(1920,1080);break;
                    case 2:
                        Check(!room.HasCalibratedLens,"Landscape requests a distinct calibration instead of rotating a portrait guess");
                        var landscape=Lens(58);room.RememberARProjection(landscape,game.GameCamera.aspect);Refresh();
                        SameProjection(landscape,game.GameCamera,"Landscape calibration and principal point are preserved exactly");
                        Resolution(640,1136);break;
                    case 3:
                        Check(room.HasCalibratedLens,"Returning to the previous viewport reuses its original calibration");Refresh();
                        SameProjection(portrait,game.GameCamera,"Returning to portrait restores its original optics");
                        game.ReturnToMenu();Check(!game.ar.CalibratingForNonAR && !room.Active,"Leaving Non AR cancels calibration and hides the room");
                        File.WriteAllText(Report,JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Projection/cache/lifecycle validated in Editor; acquiring physical intrinsics requires a phone."},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            if(game)game.ReturnToMenu();
            if(SessionState.GetBool(Key+NonARStage.LensPreference+"Had",false))PlayerPrefs.SetString(NonARStage.LensPreference,SessionState.GetString(Key+NonARStage.LensPreference,""));else PlayerPrefs.DeleteKey(NonARStage.LensPreference);
            if(SessionState.GetBool(Key+CandyFlickGame.NonARPreference+"Had",false))PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,SessionState.GetInt(Key+CandyFlickGame.NonARPreference,0));else PlayerPrefs.DeleteKey(CandyFlickGame.NonARPreference);
            PlayerPrefs.Save();
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText("Documentation/camera-projection-failure.txt",error);
            EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
