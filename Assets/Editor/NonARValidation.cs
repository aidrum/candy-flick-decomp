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
    public static class NonARValidation
    {
        const string Key="CandyFlickNonARTest", Folder="Documentation/NonAR";
        static readonly string[] prefs={CandyFlickGame.NonARPreference,CandyFlickGame.InGameMusicPreference,CandyFlickGame.ControlModePreference,TimeTravelSkins.Preference};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static int stage,roomId;static double next,deadline;
        static Vector3 cameraPosition;static Quaternion cameraRotation;
        static Dictionary<Light,bool> originalLights;
        static UnityEngine.Rendering.AmbientMode ambientMode;static Color ambientColor;static float reflections;
        static NonARValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            SessionState.SetBool(Key+"HadStart",PlayerPrefs.HasKey("ProgramStartKey"));SessionState.SetString(Key+"Start",PlayerPrefs.GetString("ProgramStartKey",""));
            PlayerPrefs.DeleteKey(CandyFlickGame.NonARPreference);PlayerPrefs.DeleteKey(CandyFlickGame.InGameMusicPreference);
            PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.SetInt(TimeTravelSkins.Preference,0);PlayerPrefs.Save();
            typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{640,1136});
            SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static void Click(Vector2 point)=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void MoreOptions(){game.Show(GameScreen.Settings);typeof(CandyFlickUI).GetField("settingsPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ui,"MoreOptions");}
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.8;
            try
            {
                if(!game)
                {
                    game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;
                    ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;return;
                }
                switch(stage++)
                {
                    case 0:
                        Check(!game.NonARModeEnabled&&!game.InGameMusicEnabled,"Non AR mode and in-game music both default off without saved values");
                        originalLights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).ToDictionary(l=>l,l=>l.enabled);
                        ambientMode=RenderSettings.ambientMode;ambientColor=RenderSettings.ambientLight;reflections=RenderSettings.reflectionIntensity;
                        MoreOptions();break;
                    case 1: Capture("more-options-default-off");break;
                    case 2: Click(ui.MoreNonARRect.center);break;
                    case 3:
                        Check(game.NonARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.NonARPreference,-1)==1,"More Options Non AR switch persists on");
                        PlayerPrefs.DeleteKey("ProgramStartKey");ui.MenuPlay();break;
                    case 4:
                        Check(game.ScreenState==GameScreen.Playing&&game.IsNonAR&&!game.IsAR&&game.CanSimulate,"First play in Non AR starts gameplay without the AR tutorial or tracking");
                        Check(!PlayerPrefs.HasKey("ProgramStartKey")&&game.ar.TargetCount==0,"Non AR does not initialize image targets or consume the AR first-run tutorial flag");
                        var room=game.NonARBackground.Room;roomId=room.GetInstanceID();
                        Check(game.NonARBackground.Active,"Core room is instantiated and visible during Non AR gameplay");
                        Check(originalLights.Keys.All(l=>!l.enabled),"Non AR overrides every existing scene light");
                        Check(RenderSettings.ambientMode==UnityEngine.Rendering.AmbientMode.Flat&&RenderSettings.ambientLight.maxColorComponent>0&&RenderSettings.ambientLight.maxColorComponent<1&&RenderSettings.reflectionIntensity==0,"Non AR overrides sky ambient and reflections with controlled warm ambient lighting");
                        File.WriteAllText(Folder+"/lighting-state.json",JsonUtility.ToJson(new LightingState{
                            colorSpace=QualitySettings.activeColorSpace.ToString(),quality=QualitySettings.names[QualitySettings.GetQualityLevel()],
                            ambient=RenderSettings.ambientLight,reflectionIntensity=RenderSettings.reflectionIntensity,
                            lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include).Select(l=>new LightState{name=l.name,enabled=l.enabled,intensity=l.intensity,color=l.color,cullingMask=l.cullingMask}).ToArray()
                        },true));
                        Check(Mathf.Abs(game.Actor.transform.position.y-NonARStage.GroundClearance)<.0001f&&Mathf.Abs(game.World.Find("Floor").GetComponent<BoxCollider>().bounds.max.y-NonARStage.GroundClearance)<.0001f,"Om Nom and the floor sit slightly above the physical marker card");
                        Check(game.NonARBackground.Marker.position.sqrMagnitude<.0001f,"Physical scene marker is aligned with the native table origin");
                        Check(Mathf.Abs(game.NonARBackground.Marker.GetComponent<MeshRenderer>().bounds.size.x-200)<.01f,"Physical marker has the recovered 200-unit card width");
                        var meshes=room.GetComponentsInChildren<MeshFilter>();
                        Check(meshes.Length>=10&&meshes.All(m=>m.sharedMesh&&m.sharedMesh.vertexCount>0),"All supplied Core geometry meshes exist after runtime initialization");
                        Check(room.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m&&m.shader&&m.shader.isSupported)),"Every Core material resolves to a supported shader");
                        Check(room.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Backdrop colliders leave native floor and mouth collisions in control");
                        Check(game.Actor.transform.localScale==Vector3.one*416.27103f&&game.Actor.GetComponent<BoxCollider>().size==new Vector3(.4f,.5f,.26f),"Non AR retains native character scale and mouth collider");
                        Check(Vector3.Dot(game.Actor.transform.forward,(game.Player.position-game.Actor.transform.position).normalized)>.8f,"Om Nom faces the fixed camera");
                        cameraPosition=game.Player.position;cameraRotation=game.Player.rotation;Capture("nonar-ready");break;
                    case 5:
                        Check(!game.GameplayMusic.isPlaying,"Gameplay stays silent with the new default-off music setting");
                        game.UpdateTableDistance(3);Check(game.Player.position==cameraPosition,"Fixed Non AR view ignores the distance inspection helper");
                        Check(Quaternion.Angle(game.Player.rotation,cameraRotation)<.001f,"Non AR camera pose stays static across frames");
                        game.PhotoVisibility.Begin(true);break;
                    case 6: Capture("nonar-clear-view");break;
                    case 7: game.PhotoVisibility.End();game.Pause();break;
                    case 8: Check(game.NonARBackground.Active&&!game.CanSimulate&&Time.timeScale==0,"Pause keeps the room visible and freezes gameplay");game.Resume();break;
                    case 9:
                        Check(game.CanSimulate&&game.NonARBackground.Room.GetInstanceID()==roomId,"Resume reuses the static room");
                        Physics.SyncTransforms();Vector2 start=game.GameCamera.WorldToScreenPoint(game.CandyGhost.position);
                        game.SampleInput(TouchPhase.Began,start,Vector2.zero);
                        Check(game.IsDragging,"Fixed camera still supports the real native slingshot touch region");
                        Vector2 end=start-Vector2.up*Screen.height*.15625f;
                        game.SampleInput(TouchPhase.Moved,end,end-start);game.SampleInput(TouchPhase.Ended,end,Vector2.zero);
                        Check(game.World.GetComponentsInChildren<CandyProjectile>().Count(c=>c.IsShot)==1,"Non AR gesture launches a real candy Rigidbody");
                        deadline=EditorApplication.timeSinceStartup+8;break;
                    case 10:
                        if(game.Session.shots==0)
                        {if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Non AR shot did not resolve");stage--;return;}
                        Check(game.Session.shots==1&&game.Ready,"Non AR airborne hit or landed miss resolves and enables the next candy");
                        game.StartGame(true);break;
                    case 11:
                        Check(game.IsNonAR&&!game.IsAR&&game.ar.TargetCount==0,"Non AR setting also overrides the device AR launch path without starting a camera");
                        Check(game.NonARBackground.Room.GetInstanceID()==roomId&&game.Player.position==cameraPosition,"Restart reuses room and fixed pose");
                        Check(Mathf.Abs(game.Actor.transform.position.y-NonARStage.GroundClearance)<.0001f&&Mathf.Abs(game.World.Find("Floor").localPosition.y-NonARStage.GroundClearance)<.0001f,"Restart retains the tiny ground clearance without accumulating height");
                        game.Skins.Select(NommieSkin.Disco);game.PhotoVisibility.Begin(true);break;
                    case 12: Capture("nonar-disco");break;
                    case 13: game.Skins.Select(NommieSkin.Prehistoric);break;
                    case 14: Capture("nonar-prehistoric");break;
                    case 15:
                        game.PhotoVisibility.End();game.ReturnToMenu();
                        Check(!game.NonARBackground.Active&&!game.IsNonAR&&game.ScreenState==GameScreen.Menu,"Returning to menu hides the room and leaves active Non AR mode");
                        Check(originalLights.All(pair=>pair.Key.enabled==pair.Value),"Leaving Non AR restores prior enabled states of scene lights");
                        Check(RenderSettings.ambientMode==ambientMode&&RenderSettings.ambientLight==ambientColor&&RenderSettings.reflectionIntensity==reflections,"Leaving Non AR restores the original ambient and reflection settings");
                        Check(Vector3.Distance(game.Player.position,new Vector3(0,900,-848))<.01f,"Leaving Non AR restores the recovered player camera pose");MoreOptions();break;
                    case 16: Click(ui.MoreNonARRect.center);break;
                    case 17:
                        Check(!game.NonARModeEnabled&&PlayerPrefs.GetInt(CandyFlickGame.NonARPreference,-1)==0,"More Options Non AR switch persists off");
                        game.StartGame();break;
                    case 18:
                        Check(!game.IsNonAR&&!game.IsAR&&!game.NonARBackground.Active,"Disabled setting retains the existing editor gameplay path without the room");
                        Check(Mathf.Abs(game.Actor.transform.position.y)<.0001f&&Mathf.Abs(game.World.Find("Floor").localPosition.y)<.0001f,"Leaving Non AR restores the normal actor and floor height for regular gameplay");
                        game.ReturnToMenu();break;
                    default:
                        File.WriteAllText("Documentation/nonar-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),unity=Application.unityVersion,checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}
            if(SessionState.GetBool(Key+"HadStart",false))PlayerPrefs.SetString("ProgramStartKey",SessionState.GetString(Key+"Start",""));else PlayerPrefs.DeleteKey("ProgramStartKey");PlayerPrefs.Save();
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,unity;public string[] checks;}
        [Serializable]class LightingState{public string colorSpace,quality;public Color ambient;public float reflectionIntensity;public LightState[] lights;}
        [Serializable]class LightState{public string name;public bool enabled;public float intensity;public Color color;public int cullingMask;}
    }
}
