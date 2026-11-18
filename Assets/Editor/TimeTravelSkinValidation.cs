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
    public static class TimeTravelSkinValidation
    {
        const string Key = "CandyFlickTimeTravelValidation";
        static CandyFlickGame game;
        static int stage;
        static Vector3 originalActorScale,originalColliderSize;
        static int reaction = -1;
        static readonly string[] reactions = { "open", "close_eat", "touch_left", "jump" };
        static double next;
        static readonly List<string> checks = new List<string>();
        static readonly string[] actions = { "idle", "idle2", "idle3", "idle4", "open", "close_eat", "close_sad", "jump", "turn_left", "turn_right", "tap", "touch_left_in", "touch_left", "touch_left_out", "touch_right_in", "touch_right", "touch_right_out" };
        static TimeTravelSkinValidation()
        {
            if (SessionState.GetBool(Key, false)) { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
        }
        [MenuItem("Candy Flick/Validate Time Travel skins")]
        public static void Run()
        {
            Directory.CreateDirectory("Documentation/TimeTravel");
            if (File.Exists("Documentation/TimeTravel/failure.txt")) File.Delete("Documentation/TimeTravel/failure.txt");
            SessionState.SetBool("SkinsHadPreference", PlayerPrefs.HasKey(TimeTravelSkins.Preference));
            SessionState.SetInt("SkinsSavedPreference", PlayerPrefs.GetInt(TimeTravelSkins.Preference, 0));
            PlayerPrefs.DeleteKey(TimeTravelSkins.Preference); PlayerPrefs.Save();
            SessionState.SetBool(Key, true);
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            EditorApplication.EnterPlaymode();
        }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add(message); }
        static void OnLog(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) Finish(message + "\n" + stack); }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            next = EditorApplication.timeSinceStartup + .4;
            try
            {
                if (reaction >= 0)
                {
                    // GPU skinning is cached per frame. Capture the pose prepared on the previous tick.
                    Capture("Prehistoric-"+reactions[reaction]);
                    reaction++;
                    if (reaction == reactions.Length) reaction=-1;
                    else SampleReaction();
                    return;
                }
                if (!game)
                {
                    game = UnityEngine.Object.FindFirstObjectByType<CandyFlickGame>();
                    if (!game) return;
                    Check(game.Skins.Selected == NommieSkin.Original, "New preferences use Original");
                    Application.runInBackground=true;
                    game.StartGame(); Time.timeScale = 0;
                    // Keep live desktop input and Actor.Update from interrupting sampled poses.
                    game.enabled=false; game.Actor.enabled=false; game.GetComponent<CandyFlickUI>().enabled=false;
                    originalActorScale=game.Actor.transform.localScale;
                    originalColliderSize=game.Actor.GetComponent<BoxCollider>().bounds.size;
                }
                if (stage < TimeTravelSkins.Names.Length)
                {
                    var skin = (NommieSkin)stage;
                    game.Skins.Select(skin);
                    Physics.SyncTransforms();
                    Check(game.Actor.transform.localScale==originalActorScale,skin+" retains the original actor scale");
                    Check(Vector3.Distance(game.Actor.GetComponent<BoxCollider>().bounds.size,originalColliderSize)<.01f,skin+" retains the same gameplay hitbox as Original");
                    if(skin==NommieSkin.Prehistoric)Check(Vector3.Distance(game.Skins.Visual.transform.localScale,Vector3.one*TimeTravelSkins.PrehistoricSize)<.001f,"Prehistoric enlarges only the cosmetic model by 25 percent");
                    Check(game.Skins.Selected == skin && PlayerPrefs.GetInt(TimeTravelSkins.Preference) == stage, skin + " selected and saved");
                    TimeTravelAudioValidation.CheckSkin(game,skin);
                    TimeTravelMotionValidation.Validate(game,Capture);
                    var player = game.Actor.GetComponent<Animation>();
                    var collider = game.Actor.GetComponent<BoxCollider>();
                    Vector3 center = collider.center, size = collider.size, position = game.Actor.transform.position;
                    foreach (string action in actions)
                    {
                        Check(player[action] != null, skin + " action exists: " + action);
                        foreach (float sample in new[]{0f, .35f, .8f, 1f})
                        {
                            player.Stop(); player.Play(action);
                            player[action].time = sample * player[action].length; player.Sample();
                            game.Skins.ConstrainAntenna();
                            if (game.Skins.Visual) game.Skins.Visual.GetComponent<TimeTravelVisual>().SendMessage("LateUpdate");
                            foreach (var renderer in game.Actor.GetComponentsInChildren<SkinnedMeshRenderer>())
                            {
                                var baked = new Mesh(); renderer.BakeMesh(baked, false);
                                // BakeMesh includes inherited scene scale even with useScale=false.
                                bool valid = baked.vertices.All(v => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z)
                                    && v.magnitude < 4 * game.Actor.transform.lossyScale.x);
                                if (!valid) throw new Exception(skin + " invalid skinned vertices in " + action + ": " + baked.bounds + ", scale " + renderer.transform.lossyScale);
                                UnityEngine.Object.DestroyImmediate(baked);
                            }
                        }
                    }
                    Check(collider.center == center && collider.size == size && game.Actor.transform.position == position, skin + " retains actor physics and position");
                    player.Stop(); player.Play("idle"); player["idle"].time = .3f; player.Sample();
                    if (game.Skins.Visual) game.Skins.Visual.GetComponent<TimeTravelVisual>().SendMessage("LateUpdate");
                    Capture(skin.ToString());
                    if (skin == NommieSkin.Prehistoric)
                    {
                        CaptureSizeComparison();
                        Transform jaw = game.Skins.Visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Bip_jaw");
                        Quaternion idleJaw = jaw.localRotation;
                        reaction=0; SampleReaction();
                        Check(Quaternion.Angle(idleJaw,jaw.localRotation)>20, "Prehistoric mouth-open drives its own jaw through more than 20 degrees");
                    }
                    game.Show(GameScreen.Playing);
                    typeof(CandyFlickGame).GetProperty("IsAR").SetValue(game,true);
                    typeof(ARMarkerMode).GetProperty("Tracking").SetValue(game.ar,false);
                    game.Actor.SendMessage("LateUpdate");
                    Check(game.Actor.GetComponentsInChildren<Renderer>().All(r=>!r.enabled), skin+" hidden without a tracked image");
                    typeof(ARMarkerMode).GetProperty("Tracking").SetValue(game.ar,true);
                    game.Actor.SendMessage("LateUpdate");
                    Check(game.Actor.GetComponentsInChildren<Renderer>().All(r=>r.enabled==game.Skins.Includes(r)), skin+" shown when tracking returns: "+game.ScreenState+"; "+string.Join(",",game.Actor.GetComponentsInChildren<Renderer>().Select(r=>r.name+"="+r.enabled)));
                    typeof(CandyFlickGame).GetProperty("IsAR").SetValue(game,false);
                    typeof(ARMarkerMode).GetProperty("Tracking").SetValue(game.ar,false);
                    stage++; return;
                }
                if (stage == TimeTravelSkins.Names.Length)
                {
                    game.Skins.Select(NommieSkin.Original);
                    Check(game.Actor.transform.localScale==originalActorScale,"Switching from larger Prehistoric restores the original character scale");
                    Check(!game.Skins.Visual && game.Actor.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 1, "Switching back restores original body with no accessory");
                    Check(!game.Actor.GetComponent<Animation>()["idle"].clip.name.StartsWith("Prehistoric"), "Original animation bindings restored");
                    game.Skins.Select(NommieSkin.Pirate);
                    game.Pause(); game.Actor.SendMessage("LateUpdate");
                    Check(game.Actor.GetComponentsInChildren<Renderer>().All(r => !r.enabled), "Imported skin hidden on pause");
                    game.Resume(); game.Actor.SendMessage("LateUpdate");
                    Check(game.Actor.GetComponentsInChildren<Renderer>().All(r => r.enabled), "Imported skin visible on resume");
                    stage++;
                    var ui = game.GetComponent<CandyFlickUI>();
                    ui.enabled=true;
                    game.ReturnToMenu(); game.Show(GameScreen.Skins);
                    return;
                }
                if (stage == TimeTravelSkins.Names.Length+1)
                {
                    ScreenCapture.CaptureScreenshot("Documentation/TimeTravel/skins-grid.png");
                    game.Show(GameScreen.Settings);
                    typeof(CandyFlickUI).GetField("settingsPage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(game.GetComponent<CandyFlickUI>(), null);
                    stage++; return;
                }
                if (stage == TimeTravelSkins.Names.Length+2)
                {
                    ScreenCapture.CaptureScreenshot("Documentation/TimeTravel/settings-main.png");
                    foreach(int saved in new[]{(int)NommieSkin.Pirate,(int)NommieSkin.Prehistoric,(int)NommieSkin.OmNelle,TimeTravelSkins.SavedRandomChoice,999})
                    {
                        PlayerPrefs.SetInt(TimeTravelSkins.Preference,saved);
                        var clone=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/Geometry"));
                        try
                        {
                            var skins=clone.transform.Find("OmNom@All-1").gameObject.AddComponent<TimeTravelSkins>(); skins.Initialize();
                            Check(saved==TimeTravelSkins.SavedRandomChoice
                                ? skins.RandomSelected && Enum.IsDefined(typeof(NommieSkin),skins.Selected)
                                : !skins.RandomSelected && skins.Selected==(saved==999?NommieSkin.Original:(NommieSkin)saved), "Fresh actor loads saved skin "+saved+" with Random mode and invalid-value fallback");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(clone); }
                    }
                    stage++; return;
                }
                File.WriteAllText("Documentation/timetravel-validation.json", JsonUtility.ToJson(new Evidence { unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("o"), checks = checks.ToArray() }, true));
                TimeTravelAudioValidation.Save();
                TimeTravelMotionValidation.Save();
                Finish(null);
            }
            catch (Exception e) { Finish(e.ToString()); }
        }
        static void SampleReaction()
        {
            var player=game.Actor.GetComponent<Animation>(); string action=reactions[reaction];
            player.Stop(); player.Play(action); player[action].time=player[action].length*.6f; player.Sample();
            if(action=="jump")
            {
                var jaw=game.Skins.Visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="Bip_jaw");
                Check(Quaternion.Angle(jaw.localRotation,new Quaternion(0,0,-.213f,.977f))<5, "Prehistoric excited/jump pose closes its jaw after mouth-open");
            }
        }
        static void CaptureSizeComparison()
        {
            var actors=new List<GameObject>();var camera=new GameObject("Character size comparison camera").AddComponent<Camera>();
            var target=new RenderTexture(1000,600,24);RenderTexture previous=RenderTexture.active;
            try
            {
                foreach(var entry in new[]{(NommieSkin.Prehistoric,-190f),(NommieSkin.Original,190f)})
                {
                    var template=Resources.Load<GameObject>("Original26/Geometry").transform.Find("OmNom@All-1");
                    var actor=UnityEngine.Object.Instantiate(template.gameObject);actors.Add(actor);
                    var skins=actor.AddComponent<TimeTravelSkins>();skins.Initialize(game.assets);skins.ApplyForSession(entry.Item1);
                    actor.transform.SetPositionAndRotation(new Vector3(entry.Item2,0,0),Quaternion.Euler(0,180,0));
                    var animation=actor.GetComponent<Animation>();animation.Stop();animation.Play("idle");animation["idle"].time=.3f;animation.Sample();
                    foreach(var child in actor.GetComponentsInChildren<Transform>(true))child.gameObject.layer=31;
                    foreach(var renderer in actor.GetComponentsInChildren<Renderer>(true)){renderer.enabled=skins.Includes(renderer);renderer.forceRenderingOff=false;}
                    foreach(var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))renderer.forceMatrixRecalculationPerRender=true;
                }
                camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.23f);
                camera.nearClipPlane=1;camera.farClipPlane=2500;camera.fieldOfView=35;
                camera.transform.position=new Vector3(0,190,-850);camera.transform.LookAt(new Vector3(0,150,0));camera.targetTexture=target;
                camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1000,600,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,600),0,0);image.Apply();
                File.WriteAllBytes("Documentation/TimeTravel/Prehistoric-size-comparison.png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(camera.gameObject);
                foreach(var actor in actors)UnityEngine.Object.DestroyImmediate(actor);
            }
        }
        static void Capture(string name)=>CaptureAtAngle(name,0);
        public static void CaptureAtAngle(string name,float yaw)
        {
            var camera = new GameObject("Skin validation camera").AddComponent<Camera>();
            var actor = game.Actor.transform;
            var objects = actor.GetComponentsInChildren<Transform>(true);
            var layers = objects.Select(t => t.gameObject.layer).ToArray();
            var renderers = actor.GetComponentsInChildren<Renderer>(true);
            var forceOff = renderers.Select(r => r.forceRenderingOff).ToArray();
            var enabled = renderers.Select(r => r.enabled).ToArray();
            var skinned = renderers.OfType<SkinnedMeshRenderer>().ToArray();
            var recalculate = skinned.Select(r=>r.forceMatrixRecalculationPerRender).ToArray();
            var rt = new RenderTexture(640,640,24); var previous = RenderTexture.active;
            try
            {
                for (int i=0;i<objects.Length;i++) objects[i].gameObject.layer=31;
                foreach (var r in renderers) { r.enabled=game.Skins.Includes(r); r.forceRenderingOff=false; }
                foreach (var r in skinned) r.forceMatrixRecalculationPerRender=true;
                camera.cullingMask=1<<31; camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.16f,.19f,.23f); camera.nearClipPlane=1; camera.farClipPlane=2000;
                camera.transform.position=actor.TransformPoint(Quaternion.Euler(0,yaw,0)*new Vector3(0,.32f,1.5f));
                camera.transform.LookAt(actor.TransformPoint(new Vector3(0,.29f,0)));
                camera.fieldOfView=38; camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
                var image=new Texture2D(640,640,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,640,640),0,0); image.Apply();
                File.WriteAllBytes("Documentation/TimeTravel/"+name+".png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                for(int i=0;i<objects.Length;i++)objects[i].gameObject.layer=layers[i];
                for(int i=0;i<renderers.Length;i++){renderers[i].forceRenderingOff=forceOff[i];renderers[i].enabled=enabled[i];}
                for(int i=0;i<skinned.Length;i++)skinned[i].forceMatrixRecalculationPerRender=recalculate[i];
                RenderTexture.active=previous; camera.targetTexture=null; rt.Release();
                UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(camera.gameObject);
            }
        }
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false); EditorApplication.update-=Tick; Application.logMessageReceived-=OnLog;
            if (SessionState.GetBool("SkinsHadPreference",false)) PlayerPrefs.SetInt(TimeTravelSkins.Preference,SessionState.GetInt("SkinsSavedPreference",0));
            else PlayerPrefs.DeleteKey(TimeTravelSkins.Preference);
            PlayerPrefs.Save();
            if(error!=null)File.WriteAllText("Documentation/TimeTravel/failure.txt",error);
            EditorApplication.ExitPlaymode();
            if(Application.isBatchMode)EditorApplication.Exit(error==null?0:1);
        }
        [Serializable] sealed class Evidence { public string unity,utc; public string[] checks; }
    }
}
