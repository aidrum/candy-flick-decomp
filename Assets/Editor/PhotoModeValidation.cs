using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Vuforia;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class PhotoModeValidation
    {
        const string Key="CandyFlickPhotoTest",Folder="Documentation/PhotoMode";
        static readonly string[] ints={CandyFlickGame.NonARPreference,CandyFlickGame.GroundARPreference,CandyFlickGame.InGameMusicPreference,TimeTravelSkins.Preference,"IsMusic","IsSound","Language","BestScore","TimesPlayed"};
        static readonly string[] strings={LocalAchievements.SaveKey,NonARStage.LensPreference,"ProgramStartKey"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static PhotoOmNom first,second,third;
        static int stage;static double next,deadline;static bool finishing;static string before;
        static PhotoModeValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in ints){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            foreach(string pref in strings){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetString(Key+pref,PlayerPrefs.GetString(pref));}
            PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.GroundARPreference,0);PlayerPrefs.SetInt(CandyFlickGame.InGameMusicPreference,0);
            PlayerPrefs.SetInt(TimeTravelSkins.Preference,(int)NommieSkin.Pirate);PlayerPrefs.SetInt("Language",0);PlayerPrefs.SetInt("IsMusic",1);PlayerPrefs.SetInt("IsSound",1);PlayerPrefs.Save();
            SessionState.SetBool(Key+"Audio",AudioListener.pause);SessionState.SetFloat(Key+"Volume",AudioListener.volume);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            SessionState.SetInt(Key+"Simulator",(int)VuforiaConfiguration.Instance.PlayMode.PlayModeType);
            VuforiaConfiguration.Instance.PlayMode.PlayModeType=PlayModeType.SIMULATOR;
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);
        static void Set(object value,string field,object data)=>value.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(value,data);
        static void Property(object value,string name,object data)=>Set(value,"<"+name+">k__BackingField",data);
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static string Snapshot()=>string.Join("|",ints.Select(p=>p+":"+PlayerPrefs.HasKey(p)+":"+PlayerPrefs.GetInt(p)).Concat(strings.Select(p=>p+":"+PlayerPrefs.HasKey(p)+":"+PlayerPrefs.GetString(p))));
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.5;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=false;AudioListener.volume=0;return;}
                switch(stage++)
                {
                    case 0:
                        game.ReturnToMenu();before=Snapshot();MenuLayout();Capture("menu-phone");
                        game.GroundSupport.EditorSupportOverride=false;game.GroundSupport.Refresh();ui.OpenPhotoMode();
                        Check(CandyFlickUI.PopupActive&&!game.IsPhotoMode&&game.ScreenState==GameScreen.Menu,"Unsupported device stays in menu with the existing warning popup");
                        Call(ui,"ClosePopup");Check(Snapshot()==before,"Unsupported launch writes no preferences");
                        game.GroundSupport.EditorSupportOverride=true;game.GroundSupport.Refresh();ui.OpenPhotoMode();deadline=EditorApplication.timeSinceStartup+60;break;
                    case 1:
                        if(!game.IsPhotoMode||!VuforiaApplication.Instance.IsRunning){if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Photo startup timed out");stage--;return;}
                        Check(game.IsAR&&!game.IsNonAR&&game.ar.GroundMode&&!game.GroundARModeEnabled&&game.NonARModeEnabled,"Photo launch forces Ground AR without changing saved Image/Non AR preferences");
                        Check(game.ar.TargetCount==0&&VuforiaBehaviour.Instance.WorldCenterMode==WorldCenterMode.DEVICE,"Photo AR uses gravity-aligned device coordinates without image targets");
                        AnchorNameChecks();
                        Check(game.PhotoPlacing&&game.PhotoActors.Count==0&&!game.Actor.gameObject.activeSelf,"Launch prompts for the preferred skin and hides the normal actor");
                        Check(game.GameplayMusic.isPlaying&&game.GameplayMusic.clip==Resources.Load<AudioClip>(CandyFlickGame.PhotoTrackResource),"Winter Fun plays during scanning despite normal gameplay music being off");
                        Check(!game.ar.PlaceGround(),"Simulator cannot invent a ground hit");Capture("scanning");game.Pause();break;
                    case 2:
                        Check(game.GameplayMusic.isPlaying,"Photo music continues while paused with no actors");game.Resume();
                        Check(ui.PhotoAddRect.x==ui.PhotoCameraRect.x&&ui.PhotoAddRect.center.y<ui.PhotoCameraRect.center.y&&ui.PhotoAddRect.size==ui.PhotoCameraRect.size,"HUD Add button occupies the normal camera position above the Photo camera");
                        Check(Resources.Load<Texture2D>("PhotoModeMenu/add")&&Resources.Load<Texture2D>("PhotoModeMenu/add_pressed"),"HUD Add uses the supplied normal and pressed artwork");
                        Check(!ui.CanTapGroundPlacement(ui.PhotoAddRect.center),"Tapping Add while placing cannot also place an Om Nom");
                        Capture("add-button");typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{ui.PhotoAddRect.center});break;
                    case 3:
                        if(game.ScreenState!=GameScreen.Skins){stage--;return;}
                        Check(ui.PhotoSkinPicker&&Time.timeScale==0,"HUD Add Om Nom opens the dedicated skin picker and pauses gameplay");Capture("select-om-nom");ui.Back();break;
                    case 4:
                        if(game.ScreenState!=GameScreen.Playing){stage--;return;}
                        Check(game.PhotoPlacing&&game.PhotoActors.Count==0,"Picker Back returns to Photo gameplay without adding an actor");
                        game.Resume();game.ar.End();game.enabled=false;Call(game.ar,"SetTracking",true);
                        first=Place(new Vector3(0,0,0),null);second=Place(new Vector3(750,0,0),NommieSkin.Prehistoric);third=Place(new Vector3(-750,200,100),NommieSkin.Disco);
                        Check(first.Appearance.Selected==NommieSkin.Pirate&&second.Appearance.Selected==NommieSkin.Prehistoric&&third.Appearance.Selected==NommieSkin.Disco,"Initial actor uses preferred Pirate; added actors keep independent skins");
                        third.Appearance.ApplyForSession(NommieSkin.OmNelle);
                        Check(third.Appearance.Selected==NommieSkin.OmNelle&&PlayerPrefs.GetInt(TimeTravelSkins.Preference)==(int)NommieSkin.Pirate,"Om Nelle can join Photo mode without changing the preferred skin");
                        Check(game.PhotoActors.Count==3&&Snapshot()==before,"Adding multiple actors leaves skin preference and all game progress unchanged");
                        GroundChecks();
                        LaunchRangeChecks();
                        View();Capture("three-actors");Aim(first);Call(game,"TickPhotoMode");
                        typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{new Vector2(ui.CanvasWidth/2,Screen.height/CandyFlickUI.CanvasScale(Screen.safeArea)/2)});break;
                    case 5:
                        Check(game.PhotoTarget==first,"Center ray finds the nearest aimed Om Nom");
                        Check(game.PhotoSelection==first&&!game.Ready,"Actual screen tap selects the crosshaired actor and blocks throwing");
                        Call(ui,"UpdatePhotoHighlight");Check(OutlineWhite(first),"Selection changes the actor outline to white");Capture("selected");break;
                    case 6:
                        Quaternion rotation=first.transform.rotation;first.Actor.OnPreShoot();Check(first.transform.rotation==rotation,"Dragging candy cannot turn a photo actor");
                        game.Player.position+=Vector3.right*500;game.FacePhotoSelection();Check(Quaternion.Angle(rotation,first.transform.rotation)>5,"Face me turns only the selected actor");
                        Property(game,"PhotoTarget",first);game.SelectPhotoTarget();game.MovePhotoSelection();var oldAnchor=first.Anchor;
                        Check(game.PhotoPlacing&&game.PhotoActors.Count==3&&first.Anchor==oldAnchor,"Move retains all actors and the old anchor until a replacement is placed");
                        Check(first.IsMoving&&!first.gameObject.activeInHierarchy&&!first.Actor.GetComponent<Collider>().enabled&&!first.GroundCollider.gameObject.activeInHierarchy,"Move hides the actor, its hit collider and its local ground while choosing a position");
                        Check(second.gameObject.activeInHierarchy&&third.gameObject.activeInHierarchy,"Moving an actor leaves the other Om Noms visible");
                        game.CancelPhotoPlacement();Check(!first.IsMoving&&first.gameObject.activeInHierarchy&&first.Anchor==oldAnchor&&first.GroundCollider.gameObject.activeInHierarchy,"Cancelling Move restores the actor and local ground at the retained anchor");
                        Property(game,"PhotoTarget",first);game.SelectPhotoTarget();game.MovePhotoSelection();
                        var moved=game.CompletePhotoPlacement(Anchor(new Vector3(.15f,0,.1f)));MarkPlaced(moved);
                        Check(!moved.IsMoving&&moved.gameObject.activeInHierarchy,"Completing Move reveals the same actor at the replacement anchor");
                        Check(moved==first&&first.Anchor!=oldAnchor&&game.PhotoActors.Count==3&&second.transform.position==new Vector3(750,0,0),"Move replaces one anchor without deleting or moving other actors");
                        FeedChecks();SadnessChecks();PetChecks();View();game.DeselectPhotoActor();Property(game,"PhotoTarget",first);Call(ui,"UpdatePhotoHighlight");
                        Set(ui,"hideGui",true);game.PhotoCapturing=true;game.PhotoVisibility.Begin(true);Call(ui,"UpdatePhotoHighlight");Check(!OutlineWhite(first),"Photo capture restores the normal outline before rendering");Capture("clean-photo");break;
                    case 7:
                        Set(ui,"hideGui",false);game.PhotoCapturing=false;game.PhotoVisibility.End();Property(game,"PhotoTarget",second);game.SelectPhotoTarget();game.DeletePhotoSelection();
                        Check(game.PhotoActors.Count==2&&game.PhotoActors.Contains(first)&&!game.PhotoActors.Contains(second),"Delete removes only the selected Om Nom");
                        game.DeleteAllPhotoActors();Check(game.PhotoActors.Count==0&&!game.PhotoPlacing&&!game.Ready,"Deleting all leaves an empty scene without forcing placement");
                        Check(!game.World.Find("Floor").gameObject.activeSelf,"Deleting every actor also removes the photo ground");
                        Check(game.GameplayMusic.isPlaying,"Music keeps playing with every Om Nom deleted");
                        game.BeginPhotoPlacement(NommieSkin.Artist);game.CancelPhotoPlacement();Check(!game.PhotoPlacing&&game.PhotoActors.Count==0,"New placement can be cancelled without forcing a replacement");
                        Check(Snapshot()==before,"Photo placement, feeding, edits and deletion save no game data");
                        Property(game.ar,"PhotoSupportFailure","Photo mode requires Ground AR support.");game.SendMessage("Update");
                        Check(CandyFlickUI.PopupActive&&game.ScreenState==GameScreen.Menu,"Runtime rejection of ground support returns to menu with a warning popup");Call(ui,"ClosePopup");
                        Check(!game.IsPhotoMode&&game.Actor.gameObject.activeSelf&&game.PhotoActors.Count==0,"Leaving Photo mode clears its actors and restores the normal actor");
                        Check(game.World.Find("Floor").gameObject.activeSelf&&game.World.Find("Floor").position.y==0,"Leaving Photo mode restores the normal floor position");
                        Check(!game.GameplayMusic.clip,"Leaving Photo mode clears Winter Fun so it cannot leak into normal gameplay");
                        Resolution(768,1024);break;
                    case 8:MenuLayout();Capture("menu-tablet");Resolution(640,960);break;
                    case 9:MenuLayout();Capture("menu-short-phone");Resolution(1920,1080);break;
                    case 10:
                        MenuLayout();Capture("menu-wide");
                        var edge=ui.MenuSkinsRect.center+new Vector2(-ui.MenuSkinsRect.width*.35f,-ui.MenuSkinsRect.height*.08f);
                        typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{edge});deadline=EditorApplication.timeSinceStartup+10;break;
                    case 11:
                        if(game.ScreenState!=GameScreen.Skins){if(EditorApplication.timeSinceStartup<deadline){stage--;break;}throw new Exception("Enlarged skins icon edge did not open Skins");}
                        Check(game.ScreenState==GameScreen.Skins,"The enlarged central skins button receives edge clicks without opening a neighboring menu");
                        game.ReturnToMenu();game.StartGame();NormalFeedCheck();break;
                    default:
                        File.WriteAllText("Documentation/photo-mode-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Editor and Vuforia simulator, inactive anchor fixtures for scene editing, real PhysX catches and rendered UI captures. Physical surface anchoring, camera permissions and gallery saving require a supported phone. No player build made."},true));Finish(null);break;
                }
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static AnchorBehaviour Anchor(Vector3 position)
        {
            var fixture=new GameObject("Photo anchor UI fixture");fixture.SetActive(false);fixture.transform.position=position;return fixture.AddComponent<AnchorBehaviour>();
        }
        static PhotoOmNom Place(Vector3 position,NommieSkin? skin)
        {
            if(skin.HasValue)game.BeginPhotoPlacement(skin.Value);
            var nom=game.CompletePhotoPlacement(Anchor(position/1000));MarkPlaced(nom);return nom;
        }
        static void MarkPlaced(PhotoOmNom nom)
        {
            // Inactive anchor fixtures supply editor-only poses. Native tracking remains
            // the only production path that marks a placement established.
            Property(nom,"Tracked",true);Property(nom,"HasPlacedPose",true);nom.SetMoving(false);Call(game,"RefreshPhotoGround");
        }
        static void AnchorNameChecks()
        {
            // Exercise the installed SDK's actual uniqueness gate without fabricating
            // a native surface hit or depending on a phone camera in the editor.
            var assembly=typeof(AnchorBehaviour).Assembly;
            Type observerType=assembly.GetType("Vuforia.Internal.Observers.AnchorObserver",true),instanceType=assembly.GetType("Vuforia.Internal.Observers.AnchorInstance",true);
            object observer=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(observerType);
            var field=observerType.GetField("mAnchorInstances",BindingFlags.Instance|BindingFlags.NonPublic);
            var anchors=(System.Collections.IDictionary)Activator.CreateInstance(field.FieldType);field.SetValue(observer,anchors);
            var gate=observerType.GetMethod("EnsureAnchorNameValidAndUnique",BindingFlags.Instance|BindingFlags.NonPublic);
            string firstName=null;
            for(int i=0;i<12;i++)
            {
                string name=(string)Call(game.ar,"NextPhotoAnchorName");gate.Invoke(observer,new object[]{name});if(i==0)firstName=name;
                object instance=System.Runtime.Serialization.FormatterServices.GetUninitializedObject(instanceType);Property(instance,"Name",name);anchors.Add(i,instance);
            }
            Check(anchors.Count==12,"Twelve successive Photo anchor names pass Vuforia's real duplicate-name gate");
            bool rejected=false;try{gate.Invoke(observer,new object[]{firstName});}catch(TargetInvocationException error){rejected=error.InnerException.Message.Contains("already exists");}
            Check(rejected,"The SDK rejects reuse of the first anchor name, reproducing the former second-placement failure");
        }
        static void GroundChecks()
        {
            var floor=game.World.Find("Floor").GetComponent<Collider>();
            Check(floor.gameObject.activeSelf&&Mathf.Abs(floor.bounds.max.y)<.01f,"The shared normal floor is enabled at the lowest placed Om Nom");
            foreach(var nom in game.PhotoActors)
                Check(nom.GroundCollider.gameObject.activeInHierarchy&&Mathf.Abs(nom.GroundCollider.bounds.max.y-nom.transform.position.y)<.01f&&Mathf.Abs(nom.GroundCollider.bounds.size.y-200)<.1f,"A small solid patch sits at the placed surface for "+nom.Appearance.Selected);
            Drop(third.transform.position+new Vector3(180,450,0),third.transform.position.y,"A candy lands on a higher Om Nom's local ground patch");
            var lower=Place(new Vector3(1800,-300,700),NommieSkin.Original);
            Check(Mathf.Abs(floor.bounds.max.y+300)<.01f,"Placing a lower Om Nom lowers the shared ground");
            Vector3 outside=lower.transform.position+new Vector3(1600,450,1500);
            Drop(outside,-300,"A candy outside every local patch lands on the shared lowest ground");
            Property(game,"PhotoTarget",lower);game.SelectPhotoTarget();game.MovePhotoSelection();
            Check(Mathf.Abs(floor.bounds.max.y)<.01f,"Moving the lowest Om Nom sets the floor from the remaining placed actors");
            game.CancelPhotoPlacement();Check(Mathf.Abs(floor.bounds.max.y+300)<.01f,"Cancelling the lowest actor's move restores its ground height");
            Property(game,"PhotoTarget",lower);game.SelectPhotoTarget();game.DeletePhotoSelection();
            Check(Mathf.Abs(floor.bounds.max.y)<.01f,"Deleting the lowest Om Nom raises the floor to the next lowest placement");
            game.BeginPhotoPlacement(NommieSkin.Artist);
            Check(ui.CanTapGroundPlacement(new Vector2(20,20)),"Placement taps remain accepted with multiple Om Noms already present");
            game.CancelPhotoPlacement();
        }
        static void Drop(Vector3 point,float expectedHeight,string label)
        {
            var candy=game.World.GetComponentsInChildren<CandyProjectile>().First();var mode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                candy.transform.position=point;candy.LaunchImpulse(Vector3.zero,Vector3.zero,false);Physics.SyncTransforms();
                for(int i=0;i<180;i++)Physics.Simulate(.01f);
                Check(candy.HasLanded&&Mathf.Abs(candy.GetComponent<Collider>().bounds.min.y-expectedHeight)<3,label);
            }
            finally{candy.Restore();Physics.simulationMode=mode;}
        }
        static void View()
        {
            game.Player.SetPositionAndRotation(new Vector3(0,500,-1300),Quaternion.LookRotation(new Vector3(0,130,0)-new Vector3(0,500,-1300)));
            game.GameCamera.clearFlags=CameraClearFlags.SolidColor;game.GameCamera.ResetProjectionMatrix();game.CandyCamera.projectionMatrix=game.GameCamera.projectionMatrix;Call(game.ar,"SetTracking",true);Time.timeScale=1;
        }
        static void Aim(PhotoOmNom nom)
        {
            var center=nom.Actor.GetComponent<Collider>().bounds.center;game.Player.rotation=Quaternion.LookRotation(center-game.Player.position);Physics.SyncTransforms();
        }
        static bool OutlineWhite(PhotoOmNom nom)=>nom.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Any(m=>m.HasProperty("_OutlineColor")&&m.GetColor("_OutlineColor")==Color.white);
        static void MenuLayout()
        {
            var four=new[]{ui.MenuDownloadRect,ui.MenuSettingsRect,ui.MenuPhotoRect,ui.MenuMultiplayerRect};
            Check(four.All(r=>r.y>=0&&r.yMax<=Screen.height/CandyFlickUI.CanvasScale(Screen.safeArea))&&four.All(r=>Vector2.Distance(r.center,ui.MenuSkinsRect.center)>.82f*(r.width+ui.MenuSkinsRect.width)/2),"Round menu icons fit around the central skins button at "+Screen.width+"x"+Screen.height);
            Check(ui.MenuPhotoRect.size==ui.MenuSkinsRect.size&&ui.MenuSkinsRect.center==new Vector2((ui.MenuPhotoRect.xMax+ui.MenuMultiplayerRect.x)/2,(ui.MenuDownloadRect.y+ui.MenuPhotoRect.yMax)/2),"Skins matches the other small menu buttons while retaining its center position");
        }
        static void LaunchRangeChecks()
        {
            Vector3 player=game.Player.position;var poses=game.PhotoActors.Select(n=>n.transform.position).ToArray();
            try
            {
                game.Player.position=first.transform.position+new Vector3(0,1200,-500);Property(game,"PhotoTarget",second);
                foreach(float raw in new[]{480f,160f})
                {
                    float speed=(float)Call(game,"CalculateLaunchSpeed",raw);
                    float expected=CandyFlick.Original26.LaunchMath26.CalculateSpeed(raw,1200,500);
                    Check(Mathf.Abs(speed-expected)<.001f,"Photo launch uses the nearest Om Nom even when aiming at another (raw strength "+raw+")");
                    game.Player.position+=Vector3.up*2300;foreach(var nom in game.PhotoActors)nom.transform.position+=Vector3.up*2300;
                    Check(Mathf.Abs((float)Call(game,"CalculateLaunchSpeed",raw)-speed)<.001f,"Photo launch range is unchanged by absolute AR ground height (raw strength "+raw+")");
                    game.Player.position-=Vector3.up*2300;for(int i=0;i<poses.Length;i++)game.PhotoActors[i].transform.position=poses[i];
                }
                first.SetMoving(true);
                Check((float)Call(game,"CalculateLaunchSpeed",480f)>CandyFlick.Original26.LaunchMath26.CalculateSpeed(480,1200,500),"Moving an Om Nom removes it from nearest-target launch scaling");first.SetMoving(false);
            }
            finally{game.Player.position=player;for(int i=0;i<poses.Length;i++)game.PhotoActors[i].transform.position=poses[i];Property(game,"PhotoTarget",null);Call(game,"RefreshPhotoGround");}
        }
        static void FeedChecks()
        {
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                foreach(var nom in game.PhotoActors)nom.Actor.enabled=false;
                for(int i=0;i<12;i++)
                {
                    var nom=game.PhotoActors[i%game.PhotoActors.Count];Catch(nom.Actor);
                    Check(game.Session.shots==0&&game.Session.total==0&&game.Session.hits==0,"Photo catch "+(i+1)+" consumes candy without score or a round limit");
                    Check((int)typeof(OmNomActor).GetField("hitAudioIndex",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(nom.Actor)==0,"Photo feeding does not advance score sound sequence");
                }
                foreach(var nom in game.PhotoActors)nom.Actor.enabled=true;
            }
            finally{Physics.simulationMode=previous;}
        }
        static void Catch(OmNomActor actor)
        {
            var candy=game.World.GetComponentsInChildren<CandyProjectile>().First();var bounds=actor.GetComponent<Collider>().bounds;
            Set(game,"projectile",candy);Set(game,"timeForNextShoot",false);
            candy.transform.position=new Vector3(bounds.center.x,bounds.center.y,bounds.min.z-130);
            candy.LaunchImpulse(Vector3.zero,Vector3.zero,false);candy.Body.useGravity=false;candy.Body.linearVelocity=Vector3.forward*700;
            actor.OnBulletShoot(candy);Physics.SyncTransforms();
            for(int n=0;n<65&&candy.IsShot;n++){Physics.Simulate(.01f);actor.SendMessage("Update");}
            Check(!candy.IsShot&&actor.GetComponent<Animation>().IsPlaying("close_eat"),"Actual PhysX contact feeds "+actor.GetComponent<TimeTravelSkins>().Selected);
            candy.Body.useGravity=true;
        }
        static void SadnessChecks()
        {
            var candy=game.World.GetComponentsInChildren<CandyProjectile>().First();var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                foreach(var nom in game.PhotoActors)nom.Actor.enabled=false;
                void Idle(){foreach(var nom in game.PhotoActors){nom.GetComponent<Animation>().Stop();nom.Actor.Play("idle");}}
                void Begin(Vector3 point)
                {
                    Idle();candy.transform.position=point;Set(game,"projectile",candy);
                    candy.LaunchImpulse(Vector3.zero,Vector3.zero,false);foreach(var nom in game.PhotoActors)nom.Actor.OnBulletShoot(candy);Physics.SyncTransforms();
                }
                void Tick(){foreach(var nom in game.PhotoActors)nom.Actor.SendMessage("Update");Call(game,"ResolvePhotoCandyOutcome");}
                int Sad()=>game.PhotoActors.Count(n=>n.GetComponent<Animation>().IsPlaying("close_sad"));
                Begin(first.transform.position+new Vector3(180,350,0));
                for(int i=0;i<180&&!candy.HasLanded;i++){Physics.Simulate(.01f);Tick();}
                Check(candy.HasLanded&&Sad()==1&&first.GetComponent<Animation>().IsPlaying("close_sad"),"Only the Om Nom nearest a landed candy becomes sad");
                for(int i=0;i<10;i++)Tick();Check(Sad()==1,"A resolved miss does not make other Om Noms sad in later frames");
                candy.Restore();Call(game,"ResolvePhotoCandyOutcome");
                Begin(first.transform.position+new Vector3(0,300,-300));Tick();
                Bounds bounds=second.Actor.GetComponent<Collider>().bounds;candy.transform.position=new Vector3(bounds.center.x,bounds.center.y,bounds.min.z-130);
                candy.Body.useGravity=false;candy.Body.linearVelocity=Vector3.forward*700;Physics.SyncTransforms();
                for(int i=0;i<65&&candy.IsShot;i++){Physics.Simulate(.01f);Tick();}
                Check(!candy.IsShot&&second.GetComponent<Animation>().IsPlaying("close_eat")&&Sad()==0,"A candy eaten by another Om Nom produces no sadness anywhere");
                for(int i=0;i<10;i++)Tick();Check(Sad()==0,"Restoring an eaten candy to the pool never creates a delayed sad reaction");
                Begin(third.transform.position+new Vector3(180,250,0));candy.Restore();Call(game,"ResolvePhotoCandyOutcome");
                Check(Sad()==1&&third.GetComponent<Animation>().IsPlaying("close_sad"),"An expired candy chooses the closest Om Nom at its last real position, not its pool position");
                Check(game.Session.total==0&&game.Session.shots==0,"Photo miss reactions leave scores and round data untouched");Idle();
            }
            finally{candy.Restore();candy.Body.useGravity=true;foreach(var nom in game.PhotoActors)nom.Actor.enabled=true;Physics.simulationMode=previous;}
        }
        static void PetChecks()
        {
            foreach(var nom in game.PhotoActors)
                Check(nom.Actor.GetComponent<Animation>()["touch_left"]!=null&&nom.Actor.GetComponent<Animation>()["touch_right_out"]!=null,"Petting clips remain bound for "+nom.Appearance.Selected);
            Call(third.Actor,"InstantiateHeart");var heart=game.World.GetComponentsInChildren<Transform>().First(t=>t.name=="HeartGlobalPos");
            Check(Mathf.Abs(heart.position.y-third.transform.position.y-100)<.001f,"Heart particles use the petted actor's tracked surface height");
        }
        static void NormalFeedCheck()
        {
            Check(!game.IsPhotoMode&&game.IsNonAR&&game.Ready,"Normal mode restores saved Non AR behavior");
            var previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;game.Actor.enabled=false;
            try{Catch(game.Actor);Check(game.Session.shots==1&&game.Session.hits==1&&game.Session.total>0,"Normal catches still award score and advance the round");}
            finally{Physics.simulationMode=previous;game.Actor.enabled=true;}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            if(game){game.enabled=true;game.ReturnToMenu();}
            foreach(string pref in ints){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}
            foreach(string pref in strings){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetString(pref,SessionState.GetString(Key+pref,""));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            AudioListener.pause=SessionState.GetBool(Key+"Audio",false);AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);
            VuforiaConfiguration.Instance.PlayMode.PlayModeType=(PlayModeType)SessionState.GetInt(Key+"Simulator",0);
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
