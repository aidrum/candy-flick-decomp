using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public static class SkinPerformanceValidation
    {
        const string Key="CandyFlickSkinPerformance";
        static SkinPerformanceValidation(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
        [MenuItem("Candy Flick/Profile skin animation CPU cost")]
        public static void Run()=>Run("latest");
        public static void Run(string label)
        {
            Directory.CreateDirectory("Documentation");
            SessionState.SetString(Key+"Label",label);SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            var game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
            try
            {
                AudioListener.pause=true;game.StartGame();Time.timeScale=0;
                game.enabled=false;game.Actor.enabled=false;game.GetComponent<CandyFlickUI>().enabled=false;
                var player=game.Actor.GetComponent<Animation>();var results=new List<Sample>();
                foreach(NommieSkin skin in Enum.GetValues(typeof(NommieSkin)))
                {
                    game.Skins.ApplyForSession(skin);
                    var visual=game.Skins.Visual?game.Skins.Visual.GetComponent<TimeTravelVisual>():null;
                    var eye=visual?visual.GetComponent<OmNelleEyeSurface>():null;
                    Action follow=visual?(Action)Delegate.CreateDelegate(typeof(Action),visual,typeof(TimeTravelVisual).GetMethod("Follow",BindingFlags.Instance|BindingFlags.NonPublic)):()=>{};
                    Action update=()=>{follow();if(eye)eye.Refresh();};
                    for(int i=0;i<8;i++)update();
                    var times=new List<double>();long allocated=0;
                    using(var reference=eye?new BinaryWriter(File.Create("Documentation/omnelle-eye-poses-"+SessionState.GetString(Key+"Label","latest")+".bin")):null)
                    {
                    var points=new List<Vector3>(1024);
                    foreach(string action in new[]{"idle","open","close_sad","jump","touch_left","touch_right"})
                    {
                        player.Stop();player.Play(action);
                        for(int frame=0;frame<60;frame++)
                        {
                            player[action].time=player[action].length*frame/59;player.Sample();
                            long bytes=GC.GetAllocatedBytesForCurrentThread(),start=Stopwatch.GetTimestamp();
                            update();
                            long elapsed=Stopwatch.GetTimestamp()-start;
                            allocated+=GC.GetAllocatedBytesForCurrentThread()-bytes;
                            times.Add(elapsed*1000.0/Stopwatch.Frequency);
                            if(eye)
                            {
                                foreach(var mesh in new[]{eye.lids.sharedMesh,eye.pupils.sharedMesh})
                                {
                                    mesh.GetVertices(points);
                                    foreach(var point in points){reference.Write(point.x);reference.Write(point.y);reference.Write(point.z);}
                                }
                            }
                        }
                    }
                    }
                    times.Sort();
                    results.Add(new Sample{skin=skin.ToString(),poses=times.Count,meanMs=times.Average(),medianMs=times[times.Count/2],p95Ms=times[(int)(times.Count*.95)],allocatedBytesPerPose=(double)allocated/times.Count,
                        eyeTriangles=eye?eye.whiteTriangles.Length/3:0,lidVertices=eye?eye.lids.sharedMesh.vertexCount:0,pupilVertices=eye?eye.pupils.sharedMesh.vertexCount:0});
                }
                string path="Documentation/skin-performance-"+SessionState.GetString(Key+"Label","latest")+".json";
                File.WriteAllText(path,JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion,samples=results.ToArray(),
                    scope="Editor CPU cost of each skin's extra animation work, excluding shared Animation.Sample, GPU rendering and AR. 360 changing poses per skin. Phone frame rates are not measured."},true));
                UnityEngine.Debug.Log("CANDY_FLICK_SKIN_PERFORMANCE: "+path);
            }
            catch(Exception e){File.WriteAllText("Documentation/skin-performance-failure.txt",e.ToString());UnityEngine.Debug.LogException(e);}
            finally
            {
                EditorApplication.update-=Tick;SessionState.SetBool(Key,false);
                AudioListener.pause=SessionState.GetBool(Key+"Audio",false);
                string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
                EditorApplication.ExitPlaymode();
            }
        }
        [Serializable]class Sample{public string skin;public int poses,eyeTriangles,lidVertices,pupilVertices;public double meanMs,medianMs,p95Ms,allocatedBytesPerPose;}
        [Serializable]class Evidence{public string utc,unity,scope;public Sample[] samples;}
    }
}
