using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using CandyFlick;
using CandyFlick.Original26;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class CandySkinValidation
    {
        const string Key="CandyFlickCandyTest",Folder="Documentation/CandySkins";
        static readonly string[] prefs={CandySkinSettings.Preference,CandyFlickGame.NonARPreference,CandyFlickGame.ControlModePreference,"IsMusic","IsSound"};
        static readonly List<string> checks=new List<string>();
        static CandyFlickGame game;static CandyFlickUI ui;static CandyProjectile shot;
        static int stage,particleCase;static LegacyParticles26 ghostParticles,flightParticles;static ParticleSystemRenderer particleRenderer;static double next,deadline;static bool finishing;static Action afterCapture;
        static CandySkinValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            PlayerPrefs.DeleteKey(CandySkinSettings.Preference);PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,2);PlayerPrefs.SetInt("IsMusic",0);PlayerPrefs.SetInt("IsSound",0);PlayerPrefs.Save();
            SessionState.SetBool(Key+"Audio",AudioListener.pause);SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Click(Vector2 point)
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void Capture(string name,Action action){ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");afterCapture=action;}
        static void Wait(bool ready,string error){if(ready)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception(error);stage--;}
        static CandySkinVisual Ghost=>game.CandyGhost.GetComponent<CandySkinVisual>();
        static void Shape(int expected)
        {
            var visual=Ghost;Check(visual.Skin==expected&&visual.Model,"Held candy displays the selected "+(expected==1?"donut":"cupcake")+" model");
            if(expected==2)Check(visual.Model.GetComponent<Renderer>().sharedMaterial.shader.name=="Toon/Basic","Cupcake uses the two-sided base shader without the candy outline pass");
            Vector3 size=visual.Model.lossyScale;
            Check(Mathf.Abs(size.x-size.y)<.01f&&Mathf.Abs(size.x-size.z)<.01f,"New model has uniform world scale despite the flattened candy parent");
            Check(Vector3.Dot(visual.Model.up,game.CandyGhost.up)>.999f,"Model's baked top points up in the held candy frame");
        }
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.4;
            try
            {
                if(afterCapture!=null){var action=afterCapture;afterCapture=null;action();return;}
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=true;}
                switch(stage++)
                {
                    case 0:
                        Check(game.CandySkins.Selected==0&&Ghost.Skin==0,"Candy 1 is the default without a saved candy preference");
                        var portraits=Resources.Load<Texture2D>("CandySkins/portraits");
                        Check(portraits&&portraits.width==2048&&portraits.height==512,"Three rendered 3D candy portraits load from one atlas");
                        Check(CandySkinSettings.CleanChoice(99)==0&&CandySkinSettings.CleanSkin(3)==0,"Invalid choice and unresolved Random IDs fall back to original candy");
                        ui.OpenSkins();deadline=EditorApplication.timeSinceStartup+8;break;
                    case 1:
                        if(game.ScreenState!=GameScreen.Skins){Wait(false,"Skins transition timed out");return;}
                        var style=(GUIStyle)typeof(CandyFlickUI).GetMethod("Main",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{1});Texture image=style.normal.background;
                        Check(ui.OmNomSkinTabRect.width==248&&ui.OmNomSkinTabRect.height<80&&Mathf.Abs(ui.OmNomSkinTabRect.width/ui.OmNomSkinTabRect.height-(float)image.width/image.height)<.001f,"Skin tabs use the longer button at its original aspect ratio and a compact height");
                        float center=CandyFlickUI.SkinCardRect(1).center.x;
                        Check(CandyFlickUI.SkinCardRect(3,4).center.x==center,"The Candy page's single Random entry centers below its full first row");
                        Check(Mathf.Abs((CandyFlickUI.SkinCardRect(3,5).center.x+CandyFlickUI.SkinCardRect(4,5).center.x)/2-center)<.001f,"A two-entry final row centers as a group on either skins page");
                        Check(Enumerable.Range(0,9).All(i=>CandyFlickUI.SkinCardRect(i).x==4+188*(i%3)),"All full Om Nom rows retain their existing positions");
                        Check(!ui.OmNomSkinTabRect.Overlaps(ui.CandySkinTabRect)&&ui.OmNomSkinTabRect.yMax<ui.SkinViewport.y,"Om Nom and Candy tabs fit above the grid");Click(ui.CandySkinTabRect.center);break;
                    case 2:
                        Check((bool)typeof(CandyFlickUI).GetField("candySkinTab",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui),"Candy tab switches to candy cards");
                        Capture("menu-phone",()=>Click(ui.SkinViewport.position+CandyFlickUI.SkinCardRect(1).center));break;
                    case 3:
                        Check(game.CandySkins.Selected==1&&PlayerPrefs.GetInt(CandySkinSettings.Preference)==1,"Donut card selects and saves candy 2");game.ReturnToMenu();game.StartGame();break;
                    case 4: PrehistoricIdleChecks();Shape(1);Capture("donut-sling",()=>game.SetMode(LaunchMode.Flick));break;
                    case 5: Shape(1);Capture("donut-flick",()=>game.SetMode(LaunchMode.Slingshot));break;
                    case 6:
                        game.PrepareAndShootBullet(400,0,0);shot=game.World.GetComponentsInChildren<CandyProjectile>().First(c=>c.IsShot);
                        Check(shot.GetComponent<CandySkinVisual>().Skin==1&&shot.GetComponent<MeshCollider>().enabled,"Donut launches with the original candy collider");
                        shot.Body.position=new Vector3(300,130,0);shot.Body.linearVelocity=Vector3.down*100;deadline=EditorApplication.timeSinceStartup+3;break;
                    case 7:
                        if(!shot.HasLanded){Wait(false,"Donut did not contact the real floor");return;}
                        Check(shot.GetComponent<MeshCollider>().enabled&&shot.GetComponent<SphereCollider>()==null,"Donut keeps its original collider after floor contact");
                        game.CandySkins.Select(2);game.StartGame();break;
                    case 8: Shape(2);Capture("cupcake-sling",()=>game.SetMode(LaunchMode.Flick));break;
                    case 9: Shape(2);Capture("cupcake-flick",()=>game.SetMode(LaunchMode.Slingshot));break;
                    case 10:
                        game.PrepareAndShootBullet(400,0,0);shot=game.World.GetComponentsInChildren<CandyProjectile>().First(c=>c.IsShot);
                        Check(shot.GetComponent<CandySkinVisual>().Skin==2&&shot.GetComponent<MeshCollider>().enabled&&shot.GetComponent<SphereCollider>()==null,"Cupcake retains the original collider while airborne");
                        shot.Body.position=new Vector3(300,130,0);shot.Body.linearVelocity=Vector3.down*100;deadline=EditorApplication.timeSinceStartup+3;break;
                    case 11:
                        if(!shot.HasLanded){Wait(false,"Cupcake did not contact the real floor");return;}
                        Check(!shot.CanBeEaten&&shot.GetComponent<MeshCollider>().enabled&&shot.GetComponent<SphereCollider>()==null,"Cupcake retains the native candy collider after floor contact and cannot be fed from the ground");
                        Check(shot.GetComponent<MeshCollider>().sharedMesh==Resources.Load<GameObject>("Original26/Geometry").transform.Find("Candies/Candy1").GetComponent<MeshCollider>().sharedMesh,"Landed cupcake uses the same collision mesh as the original candy");
                        Check(shot.GetComponent<CandySkinVisual>().Model.GetComponent<Renderer>().bounds.min.y>=game.World.Find("Floor").GetComponent<Collider>().bounds.max.y-1,"Cupcake visual stays above the table without moving its physics body");
                        shot.GetComponent<CandySkinVisual>().SetFade(.5f);Check(Mathf.Abs(shot.GetComponent<CandySkinVisual>().Model.GetComponent<Renderer>().sharedMaterial.color.a-.5f)<.001f,"New model participates in the original candy fade");
                        shot.Restore();Check(shot.GetComponent<MeshCollider>().enabled&&shot.GetComponent<SphereCollider>()==null,"Pool restore keeps cupcake on the native candy collider");
                        RandomShots();game.ReturnToMenu();ui.OpenSkins();deadline=EditorApplication.timeSinceStartup+8;break;
                    case 12:
                        if(game.ScreenState!=GameScreen.Skins){Wait(false,"Return to Skins timed out");return;}Click(ui.CandySkinTabRect.center);break;
                    case 13:
                        Check(game.CandySkins.Selected==3,"Random remains selected after play rather than replacing the preference with a rolled skin");Capture("menu-random",()=>Resolution(768,1024));break;
                    case 14: Capture("menu-tablet",()=>Click(ui.OmNomSkinTabRect.center));break;
                    case 15:
                        Check(!(bool)typeof(CandyFlickUI).GetField("candySkinTab",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui),"Om Nom tab restores the original skin grid");Capture("menu-omnom",()=>Resolution(1920,1080));break;
                    case 16: Click(ui.CandySkinTabRect.center);deadline=EditorApplication.timeSinceStartup+6;break;
                    case 17:
                        if(!(bool)typeof(CandyFlickUI).GetField("candySkinTab",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui))
                        {if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Wide Candy tab pointer timed out");Click(ui.CandySkinTabRect.center);stage--;return;}
                        Capture("menu-wide",()=>Click(ui.SkinViewport.position+CandyFlickUI.SkinCardRect(0).center));break;
                    case 18:
                        Check(game.CandySkins.Selected==0,"Original candy can be restored from the Candy tab");
                        Resolution(640,1136);stage=40;break;
                    case 40:
                        Time.timeScale=1;game.enabled=true;game.Actor.enabled=true;game.ReturnToMenu();game.CandySkins.Select(particleCase/2+1);game.SetMode(particleCase%2==0?LaunchMode.Slingshot:LaunchMode.Flick);game.StartGame();
                        game.enabled=false;game.Actor.enabled=false;SuperCandyValidation.AdvanceToFinalCandy(game);
                        Check(game.Session.IsSuperCandy&&Ghost.Skin==particleCase/2+1&&game.Ready,ParticleName+": normal shot callbacks prepare the final candy with its chosen skin");break;
                    case 41:
                        ghostParticles=game.CandyGhost.GetComponentInChildren<LegacyParticles26>();
                        Check(ghostParticles&&ghostParticles.System.particleCount>0,ParticleName+": ready super candy emits live yellow particles");
                        particleRenderer=ghostParticles.GetComponent<ParticleSystemRenderer>();
                        Check(particleRenderer.enabled&&!particleRenderer.forceRenderingOff&&particleRenderer.sharedMaterial.shader.isSupported&&(game.CandyCamera.cullingMask&(1<<ghostParticles.gameObject.layer))!=0,ParticleName+": original particle renderer remains visible to the candy camera");
                        Time.timeScale=0;Capture(ParticleName+"-particles",()=>particleRenderer.enabled=false);break;
                    case 42:
                        Capture(ParticleName+"-without-particles",()=>
                        {
                            CompareParticles();particleRenderer.enabled=true;Time.timeScale=1;
                            game.PrepareAndShootBullet(80,0,0);shot=game.World.GetComponentsInChildren<CandyProjectile>().Single(c=>c.IsShot);
                            flightParticles=shot.GetComponentInChildren<LegacyParticles26>();
                            Check(flightParticles&&flightParticles.gameObject.activeInHierarchy,ParticleName+": throwing activates the original projectile particle effect");
                            var trail=shot.GetComponent<TrailRenderer>();Check(trail.enabled&&trail.startWidth==120&&Mathf.Abs(trail.time-.5f)<.001f,ParticleName+": thrown super candy retains its yellow trail");
                        });break;
                    case 43:
                        Check(flightParticles.System.particleCount>0,ParticleName+": thrown super candy emits live particles");
                        game.StartGame();Check(!game.CandyGhost.GetComponentsInChildren<LegacyParticles26>(true).Any(p=>p.gameObject.activeInHierarchy),ParticleName+": restart hides the final-candy effect");
                        particleCase++;stage=particleCase<4?40:44;break;
                    case 44:
                        game.enabled=true;game.Actor.enabled=true;game.CandySkins.Select(0);game.ReturnToMenu();
                        File.WriteAllText("Documentation/candy-skin-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Unity Game view only. Solo UI, actual physics floor contacts, model proportions, pooled reset, per-shot random and visible final-candy particles/trails on both variants in both control modes; LAN checked separately. No player build."},true));Finish(null);break;
                }
            }
            catch(Exception error){Finish(error.ToString());}
        }
        static void PrehistoricIdleChecks()
        {
            NommieSkin saved=game.Skins.Selected;var animation=game.Actor.GetComponent<Animation>();
            var start=typeof(OmNomActor).GetField("startIdleTime",BindingFlags.Instance|BindingFlags.NonPublic);
            try
            {
                game.Skins.ApplyForSession(NommieSkin.Prehistoric);
                for(int i=0;i<24;i++)
                {
                    animation.Stop();game.Actor.ResetOriginal();start.SetValue(game.Actor,Time.time-6);
                    game.Actor.SendMessage("Update");
                    if(animation.IsPlaying("idle3")||(!animation.IsPlaying("idle2")&&!animation.IsPlaying("idle4")))throw new Exception("Prehistoric selected a missed-feed clip as an idle variation");
                }
                Check(true,"Repeated actual Prehistoric idle callbacks use only puzzled/greeting, excluding the missed-feed mouth reaction");
                Check(animation["close_sad"]!=null,"Prehistoric retains the close/sad animation for actual misses");
            }
            finally{game.Skins.ApplyForSession(saved);game.Actor.ResetOriginal();}
        }
        static string ParticleName=>"super-"+(particleCase/2==0?"donut":"cupcake")+(particleCase%2==0?"-sling":"-flick");
        static void CompareParticles()
        {
            var with=new Texture2D(2,2);var without=new Texture2D(2,2);
            try
            {
                with.LoadImage(File.ReadAllBytes(Folder+"/"+ParticleName+"-particles.png"));without.LoadImage(File.ReadAllBytes(Folder+"/"+ParticleName+"-without-particles.png"));
                var a=with.GetPixels32();var b=without.GetPixels32();int changed=0;
                // Bottom half contains the held candy. Freeze game time and toggle
                // only this renderer so the comparison measures rendered particles.
                for(int y=0;y<with.height/2;y++)for(int x=0;x<with.width;x++)
                {
                    int i=y*with.width+x;
                    if(Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b)>30)changed++;
                }
                Check(changed>20,ParticleName+": particles contribute visible candy-area pixels ("+changed+")");
            }
            finally{UnityEngine.Object.DestroyImmediate(with);UnityEngine.Object.DestroyImmediate(without);}
        }
        static void RandomShots()
        {
            var saved=UnityEngine.Random.state;UnityEngine.Random.InitState(72034);var kinds=new HashSet<int>();
            try
            {
                game.CandySkins.Select(3);game.StartGame();int held=game.CandySkins.HeldSkin;game.SetMode(LaunchMode.Flick);
                Check(game.CandySkins.HeldSkin==held,"Changing controls does not reroll the held Random candy");game.SetMode(LaunchMode.Slingshot);
                for(int i=0;i<10;i++)
                {
                    int expected=game.CandySkins.HeldSkin;game.PrepareAndShootBullet(0,0,0);var current=game.World.GetComponentsInChildren<CandyProjectile>().First(c=>c.IsShot);
                    Check(current.GetComponent<CandySkinVisual>().Skin==expected,"Random shot "+(i+1)+" matches its pre-throw held model");kinds.Add(expected);game.ResolveShot(false,0);current.Restore();
                }
                Check(kinds.Count>1&&game.Session.shots==10,"Random produces different candy models within a single normal ten-shot game");
            }
            finally{UnityEngine.Random.state=saved;}
        }
        static void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            Time.timeScale=1;if(game){game.enabled=true;game.Actor.enabled=true;game.ReturnToMenu();}
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false))PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();AudioListener.pause=SessionState.GetBool(Key+"Audio",false);
            string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
