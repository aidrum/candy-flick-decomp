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
    [InitializeOnLoad]public static class SlingshotValidation
    {
        const string Key="CandyFlickSlingshotTest",Folder="Documentation/Slingshot";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.ControlModePreference,CandyFlickGame.InGameMusicPreference,TimeTravelSkins.Preference,CandySkinSettings.Preference,"IsSound","IsMusic"};
        static readonly List<string> checks=new List<string>();static CandyFlickGame game;static SlingshotVisual support;static int stage;static double next;static Vector3[] idle;static Vector3 modelPosition;
        static SlingshotValidation(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetBool(Key+"LensHad",PlayerPrefs.HasKey(NonARStage.LensPreference));SessionState.SetString(Key+"Lens",PlayerPrefs.GetString(NonARStage.LensPreference,""));PlayerPrefs.DeleteKey(NonARStage.LensPreference);
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.SetInt(CandyFlickGame.InGameMusicPreference,0);PlayerPrefs.SetInt(TimeTravelSkins.Preference,0);PlayerPrefs.SetInt(CandySkinSettings.Preference,0);PlayerPrefs.SetInt("IsMusic",0);PlayerPrefs.SetInt("IsSound",0);
            SessionState.SetBool(Key+"AchievementsHad",PlayerPrefs.HasKey(LocalAchievements.SaveKey));SessionState.SetString(Key+"Achievements",PlayerPrefs.GetString(LocalAchievements.SaveKey,""));
            SessionState.SetBool(Key+"Audio",AudioListener.pause);SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Check(bool condition,string text){if(!condition)throw new Exception(text);checks.Add(text);}
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static Vector3[] Band()
        {
            var renderer=game.Player.Find("elastic").GetComponentInChildren<SkinnedMeshRenderer>();var mesh=new Mesh();renderer.BakeMesh(mesh);var points=mesh.vertices;UnityEngine.Object.DestroyImmediate(mesh);return points;
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.5;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;Application.runInBackground=true;AudioListener.pause=true;game.StartGame();support=game.Player.Find("elastic").GetComponent<SlingshotVisual>();return;}
                switch(stage++)
                {
                    case 0:
                        Check(support&&support.Model,"Supplied slingshot prefab instantiates as a visual-only support");
                        Check(!support.Model.GetComponentsInChildren<Collider>().Any(),"The visual slingshot adds no physics colliders");
                        Check(!support.Model.gameObject.activeInHierarchy,"Portrait view keeps the original elastic when both ends remain outside the screen");Capture("portrait-original");break;
                    case 1:Resolution(1280,720);break;
                    case 2:
                        Check(support.Model.gameObject.activeInHierarchy&&support.EndsVisible(game.CandyCamera),"Widescreen reveals the new support when the elastic ends enter the view");
                        var layout=SlingshotVisual.Attachments;
                        Vector3 leftWrap=game.Player.InverseTransformPoint(support.Model.TransformPoint(new Vector3(-.5f,0,0))),rightWrap=game.Player.InverseTransformPoint(support.Model.TransformPoint(new Vector3(.5f,0,0)));
                        Check(Mathf.Abs(Vector3.Distance(leftWrap,rightWrap)-Vector3.Distance(layout.left,layout.right)*SlingshotVisual.Size)<.1f&&Mathf.Abs(leftWrap.y-rightWrap.y)<.01f,"Normal-mode fork wraps keep the smaller span and sit level");
                        Check(Mathf.Abs(game.CandyCamera.WorldToScreenPoint(support.Model.TransformPoint(layout.handle)).x-game.CandyCamera.WorldToScreenPoint(game.CandyGhost.position).x)<1,"The normal-mode stick is horizontally centered beneath the resting candy");
                        Vector3 scale=support.Model.lossyScale;Check(Mathf.Abs(scale.x-scale.y)<.01f&&Mathf.Abs(scale.y-scale.z)<.01f,"The support retains the supplied model's proportions");
                        Check(game.Player.Find("elastic").GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.triangles.Length<Resources.Load<GameObject>("Original26/Geometry").transform.Find("Player/elastic").GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh.triangles.Length,"Supported elastic omits the stationary outer tip geometry");
                        idle=Band();modelPosition=support.Model.position;Capture("wide-idle");break;
                    case 3:
                        Physics.SyncTransforms();Vector2 start=game.CandyCamera.WorldToScreenPoint(game.CandyGhost.position);game.SampleInput(TouchPhase.Began,start,Vector2.zero);
                        Check(game.IsDragging,"Native slingshot input still accepts the visible candy");Vector2 end=start-new Vector2(40,Screen.height*.16f);game.SampleInput(TouchPhase.Moved,end,end-start);break;
                    case 4:
                        var stretched=Band();Check(stretched.Zip(idle,(a,b)=>Vector3.Distance(a,b)).Max()>.01f,"Pulling the candy deforms the original skinned elastic");
                        Check(Vector3.Distance(modelPosition,support.Model.position)<.01f,"The support remains fixed at the fork ends while the candy is pulled");Capture("wide-stretched");break;
                    case 5:game.SampleInput(TouchPhase.Canceled,Vector2.zero,Vector2.zero);game.SetMode(LaunchMode.Flick);break;
                    case 6:
                        Check(game.CandyGhost.gameObject.activeInHierarchy&&!game.Player.Find("elastic").gameObject.activeInHierarchy,"Flick mode shows only the candy, with no line or support");Capture("wide-flick");game.SetMode(LaunchMode.Slingshot);break;
                    case 7:
                        game.PhotoVisibility.Begin(true);typeof(PhotoCandyVisibility).GetMethod("BeforeRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.PhotoVisibility,new object[]{game.CandyCamera});
                        Check(support.Model.GetComponentsInChildren<Renderer>().All(r=>r.forceRenderingOff),"Hide candy in pictures also hides the support");
                        typeof(PhotoCandyVisibility).GetMethod("AfterRender",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game.PhotoVisibility,new object[]{game.CandyCamera});game.PhotoVisibility.End();
                        Check(support.Model.GetComponentsInChildren<Renderer>().All(r=>!r.forceRenderingOff),"Photo hiding restores the support's renderer state");game.Pause();break;
                    case 8:Check(!support.Model.gameObject.activeInHierarchy,"Pause hides the local launcher through the existing readiness rule");game.Resume();game.StartGame();break;
                    case 9:
                        Check(game.Player.Find("elastic").GetComponentsInChildren<SlingshotVisual>(true).Length==1,"Restart reuses the existing support without duplicating it");
                        foreach(string file in new[]{"CandyFlickSlingshot.fbx","SlingshotTex.png"})Check(File.ReadAllBytes("Assets/Resources/Slingshot/Models/"+file).SequenceEqual(File.ReadAllBytes("D:/Downloads/candyflick/slingshot/"+file)),"Source asset remains unchanged: "+file);
                        Capture("wide-restarted");break;
                    case 10:Resolution(640,1136);break;
                    case 11:
                        var band=game.Player.Find("elastic").GetComponentInChildren<SkinnedMeshRenderer>();var original=Resources.Load<GameObject>("Original26/Geometry").transform.Find("Player/elastic").GetComponentInChildren<SkinnedMeshRenderer>();
                        Check(!support.Model.gameObject.activeInHierarchy&&band.sharedMesh==original.sharedMesh&&band.sharedMaterial==original.sharedMaterial,"Returning to portrait restores the exact original full elastic mesh and material");break;
                    case 12:
                        game.StartGame();game.Actor.enabled=false;game.PrepareAndShootBullet(400,0,0);
                        var projectile=(CandyProjectile)typeof(CandyFlickGame).GetField("projectile",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(game);projectile.GetComponent<Rigidbody>().isKinematic=true;break;
                    case 13:
                        Check(!game.Ready&&!game.CandyGhost.gameObject.activeSelf&&game.Player.Find("test").gameObject.activeInHierarchy,"A launched candy leaves the original released elastic visible without another held candy");game.Pause();break;
                    case 14:Check(!game.Player.Find("test").gameObject.activeInHierarchy,"Pause hides the released elastic without discarding its pending state");game.Resume();break;
                    case 15:
                        Check(game.Player.Find("test").gameObject.activeInHierarchy&&!game.Ready,"Released elastic remains after its one-second recoil finishes and after resuming");Capture("portrait-released");Resolution(1280,720);break;
                    case 16:
                        Check(support.Model.gameObject.activeInHierarchy&&game.Player.Find("test").gameObject.activeInHierarchy&&!game.Player.Find("elastic").GetComponentInChildren<SkinnedMeshRenderer>().enabled,"The fixed widescreen support holds the released bar while the loaded band is hidden");
                        var release=game.Player.Find("test").GetComponentInChildren<SkinnedMeshRenderer>();var source=release.sharedMesh;var points=source.vertices.Select((p,i)=>
                        {
                            var w=source.boneWeights[i];Vector3 v=Vector3.zero;
                            foreach(var pair in new[]{(w.boneIndex0,w.weight0),(w.boneIndex1,w.weight1),(w.boneIndex2,w.weight2),(w.boneIndex3,w.weight3)})if(pair.Item2>0)v+=(release.bones[pair.Item1].localToWorldMatrix*source.bindposes[pair.Item1]).MultiplyPoint3x4(p)*pair.Item2;
                            return game.Player.InverseTransformPoint(v);
                        }).ToArray();
                        float left=game.Player.InverseTransformPoint(support.Model.TransformPoint(new Vector3(-.5f,0,0))).x,right=game.Player.InverseTransformPoint(support.Model.TransformPoint(new Vector3(.5f,0,0))).x;
                        Capture("wide-released");Check(Mathf.Abs(points.Min(p=>p.x)-left)<1&&Mathf.Abs(points.Max(p=>p.x)-right)<1,"The final released elastic spans exactly between both smaller fork wraps: actual "+points.Min(p=>p.x)+" / "+points.Max(p=>p.x)+", wraps "+left+" / "+right);break;
                    case 17:game.ResolveShot(true,0);break;
                    case 18:
                        Check(game.Ready&&game.CandyGhost.gameObject.activeInHierarchy&&!game.Player.Find("test").gameObject.activeSelf&&game.Player.Find("elastic").GetComponentInChildren<SkinnedMeshRenderer>().enabled,"The released bar hands over to the loaded elastic only when the next candy appears");game.PrepareAndShootBullet(400,0,0);var recoil=game.Player.Find("test").GetComponent<Animation>();Check(recoil[recoil.clip.name].time<.001f,"A subsequent slingshot launch restarts recoil from the beginning after the previous clamped pose");game.StartGame();game.SetMode(LaunchMode.Flick);game.PrepareAndShootBullet(400,0,0);break;
                    case 19:Check(!game.Player.Find("elastic").gameObject.activeSelf&&!game.Player.Find("test").gameObject.activeSelf,"A Flick launch still has neither support nor released elastic");break;
                    default:File.WriteAllText(Folder+"/validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;if(game)game.ReturnToMenu();
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}
            if(SessionState.GetBool(Key+"LensHad",false))PlayerPrefs.SetString(NonARStage.LensPreference,SessionState.GetString(Key+"Lens",""));else PlayerPrefs.DeleteKey(NonARStage.LensPreference);PlayerPrefs.Save();
            if(SessionState.GetBool(Key+"AchievementsHad",false))PlayerPrefs.SetString(LocalAchievements.SaveKey,SessionState.GetString(Key+"Achievements",""));else PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);PlayerPrefs.Save();
            AudioListener.pause=SessionState.GetBool(Key+"Audio",false);string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
