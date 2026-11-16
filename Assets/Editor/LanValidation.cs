using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using CandyFlick;
using CandyFlick.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class LanValidation
    {
        const string Key="CandyFlickLanTest",Folder="Documentation/LAN";
        static readonly string[] prefs={CandySkinSettings.Preference,LanMultiplayer.NamePreference,LanMultiplayer.ProfilePreference,CandyFlickGame.NonARPreference,CandyFlickGame.GroundARPreference,CandyFlickGame.InGameMusicPreference,"IsMusic","IsSound","Language",TimeTravelSkins.Preference};
        static readonly List<string> checks=new List<string>();
        static readonly List<Bot> bots=new List<Bot>();
        static CandyFlickGame game;static CandyFlickUI ui;static int stage;static double next,deadline;static bool finishing;
        static int languageIndex,calibrationCueBefore;static bool calibrationSoundWasEnabled;
        static int fakeHelloAvatar=-1,fakeProfileAvatar=-1;static int[] resultScores;static int cachedGeneration,cachedPlacement;static Vector3 cachedNom,cachedOffset;
        static readonly Dictionary<int,int> heldCandySkins=new Dictionary<int,int>();
        static LanTransport fakeHost;static LanSnapshot fakeState;static LanPeer fakePeer;static float clock;static int fedBefore;static int hitRingId,hitRingGeneration;static float scoredDistance;static int priorGroundGeneration;static int expiredRingId,secondRingId,unclaimedRingId;static Vector3 collectedPosition,unclaimedPosition;static LanCandyView expiryCandy;static LanCandyView floorCandy,feedCandy;static string oldSkin;static Action afterCapture;
        static LanValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()=>Start(true);
        public static void RunWithoutCaptures()=>Start(false);
        static bool CaptureEnabled=>SessionState.GetBool(Key+"Capture",true);
        static void Capture(string name){if(CaptureEnabled)ScreenCapture.CaptureScreenshot(name);}
        static void Start(bool capture)
        {
            SessionState.SetBool(Key+"SaveHad",PlayerPrefs.HasKey(LocalAchievements.SaveKey));SessionState.SetString(Key+"Save",PlayerPrefs.GetString(LocalAchievements.SaveKey,""));
            SessionState.SetBool(Key+"LensHad",PlayerPrefs.HasKey(NonARStage.LensPreference));SessionState.SetString(Key+"Lens",PlayerPrefs.GetString(NonARStage.LensPreference,""));
            SessionState.SetBool(Key+"Capture",capture);
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            foreach(string pref in prefs){SessionState.SetBool(Key+pref+"Had",PlayerPrefs.HasKey(pref));if(pref==LanMultiplayer.NamePreference)SessionState.SetString(Key+pref,PlayerPrefs.GetString(pref));else SessionState.SetInt(Key+pref,PlayerPrefs.GetInt(pref));}
            PlayerPrefs.SetInt("Language",0);PlayerPrefs.DeleteKey(CandySkinSettings.Preference);PlayerPrefs.DeleteKey(LanMultiplayer.ProfilePreference);PlayerPrefs.SetString(LanMultiplayer.NamePreference,"");PlayerPrefs.SetInt(CandyFlickGame.NonARPreference,1);PlayerPrefs.SetInt(CandyFlickGame.GroundARPreference,1);
            PlayerPrefs.SetInt(CandyFlickGame.InGameMusicPreference,0);PlayerPrefs.SetInt("IsMusic",1);PlayerPrefs.SetInt(TimeTravelSkins.Preference,(int)NommieSkin.Disco);PlayerPrefs.Save();
            SessionState.SetFloat(Key+"Volume",AudioListener.volume);SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Check(bool valid,string label){if(!valid)throw new Exception(label);checks.Add(label);}
        static object Call(object value,string name,params object[] args)=>value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(value,args);
        static void Click(Vector2 point)
        {
            EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        }
        static void ProfileDrag()
        {
            Rect viewport=(Rect)typeof(CandyFlickUI).GetProperty("LanProfileGridRect",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            var pointer=typeof(SkinMenuValidation).GetMethod("Pointer",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var step in new[]{EventType.MouseDown,EventType.MouseDrag,EventType.MouseUp})pointer.Invoke(null,new object[]{step,viewport.center+(step==EventType.MouseDown?Vector2.zero:new Vector2(0,-180)),Vector2.zero});
        }
        static bool PickerOpen=>(bool)typeof(CandyFlickUI).GetField("lanProfilePicker",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void WaitFor(bool ready,string label){if(ready)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception(label);stage--;}
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                PumpBots();PumpFakeHost();
                if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.3;
                if(afterCapture!=null){var action=afterCapture;afterCapture=null;action();return;}
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();Application.runInBackground=true;AudioListener.pause=false;AudioListener.volume=0;return;}
                switch(stage++)
                {
                    case 0: Rules();Check(game.LAN.PlayerName=="Player","Blank saved name falls back to Player");oldSkin=game.Skins.Selected.ToString();Capture(Folder+"/menu-phone.png");break;
                    case 1:
                        CheckMenuLayout();
                        var normal=Resources.Load<Texture2D>("LanMenu/pvp_button");var pressed=Resources.Load<Texture2D>("LanMenu/pvp_button_pressed");
                        Check(normal&&pressed&&normal.width==264&&normal.height==263&&pressed.width==264&&pressed.height==263,"LAN normal and pressed artwork imports retain their original dimensions");
                        var menuStyle=(GUIStyle)typeof(CandyFlickUI).GetProperty("LanMenuButtonStyle",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
                        Check(menuStyle.normal.background==normal&&menuStyle.active.background==pressed,"Main menu LAN control uses the supplied normal and pressed graphics");
                        game.LAN.OpenBrowser();break;
                    case 2:
                        Check(game.LAN.ProfilePicture==LanProfiles.Default,"New players start with the normal Om Nom profile picture");ProfileLayout();typeof(LanMultiplayer).GetProperty("Notice").SetValue(game.LAN,"Connecting...");Check(ui.LanFooterText=="Connecting...","Connection status is displayed in the browser footer");Capture(Folder+"/finder-phone.png");afterCapture=()=>Click(ui.LanProfileButtonRect.center);stage=300;break;
                    case 300:
                        Check(PickerOpen,"Clicking the profile picture opens its selector");Capture(Folder+"/profiles-phone.png");afterCapture=ProfileDrag;stage=304;deadline=EditorApplication.timeSinceStartup+5;break;
                    case 304:
                        if((Vector2)typeof(CandyFlickUI).GetField("lanProfileScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui)==Vector2.zero){WaitFor(false,"Profile drag did not reveal the final portrait row");return;}
                        Check(PickerOpen&&game.LAN.ProfilePicture==LanProfiles.Default,"Dragging the portrait grid reveals its final row without selecting a picture");Capture(Folder+"/profiles-phone-bottom.png");afterCapture=()=>Click(ProfileChoice(LanProfiles.Count-1));stage=301;break;
                    case 301:
                        Check(!PickerOpen&&game.LAN.ProfilePicture==LanProfiles.Count-1&&PlayerPrefs.GetInt(LanMultiplayer.ProfilePreference,-1)==LanProfiles.Count-1,"Choosing the last supplied picture by pointer closes the selector and saves its ID");
                        game.LAN.Initialize(game);Check(game.LAN.ProfilePicture==LanProfiles.Count-1,"Profile picture selection reloads from its saved preference");stage=3;break;
                    case 3: game.GroundSupport.EditorSupportOverride=true;game.GroundSupport.Refresh();game.LAN.EditorMarkerWidthOverride=.06f;game.CandySkins.Select(CandySkinSettings.RandomChoice);game.LAN.HostRoom();
                        Check(game.LAN.GroundMode,"New room inherits the selected Ground preference");game.LAN.ToggleRoomGround();Check(!game.LAN.GroundMode&&game.GroundARModeEnabled,"Host room mode changes preserve the solo preference");Check(game.LAN.IsHost&&!game.LAN.CanStart,"A host alone cannot start");
                        for(int i=0;i<3;i++){var bot=new Bot("Guest"+(i+1)){GroundSupported=i!=1,Avatar=i==1?99999:16+i,CandyChoice=i+1};bots.Add(bot);bot.net.Browse();bot.net.Join("127.0.0.1:"+LanTransport.GamePort,Time.realtimeSinceStartupAsDouble);}deadline=EditorApplication.timeSinceStartup+8;break;
                    case 4:
                        if(game.LAN.Players.Length<4){WaitFor(false,"Three real TCP clients did not join");return;}
                        Check(game.LAN.CanStart&&game.LAN.Players.Select(p=>p.id).Distinct().Count()==4,"Host plus three clients fill four unique slots");
                        Check(game.LAN.Players[0].avatar==LanProfiles.Count-1&&bots.All(b=>game.LAN.Players.First(p=>p.id==b.Id).avatar==LanProfiles.Clean(b.Avatar)),"Join messages preserve profile IDs and replace invalid remote IDs with normal Om Nom");
                        Check(bots.All(b=>game.LAN.Players.First(p=>p.id==b.Id).candyChoice==b.CandyChoice),"Each joining player retains their own donut, cupcake or Random candy preference");
                        Check(game.LAN.GroundWarning=="The device of one or more players in the room does not support Ground mode.","Host is warned about an unsupported Ground player even in Image mode");
                        game.LAN.ToggleRoomGround();Check(!game.LAN.CanStart,"Ground Start waits for every device's support");
                        Check(!game.LAN.StartMatch()&&game.LAN.Phase==LanPhase.Lobby&&!game.LAN.InMatch&&!string.IsNullOrEmpty(game.LAN.GroundWarning),"Unavailable ARCore blocks the actual Start command with a compact host-only warning");bots[1].SetGroundSupport(true);game.LAN.ToggleRoomGround();
                        game.LAN.SetProfilePicture(22);bots[1].ChangeProfile(20);deadline=EditorApplication.timeSinceStartup+5;
                        Capture(Folder+"/room-four-players.png");afterCapture=()=>{bots[1].SetGroundSupport(false);languageIndex=0;stage=6100;};break;
                    case 6100:
                        SetLanguage(languageIndex);stage=6101;break;
                    case 6101:
                        Check(ui.LanFooterText==""&&ui.LanRoomStatusRect.y==ui.LanBoardRect.yMax-100,"Room status uses its clipboard position without duplicate footer text in "+PortLocalization.CatalogData().languages[languageIndex]);
                        var randomStyle=(GUIStyle)typeof(CandyFlickUI).GetMethod("RandomSkinTextStyle",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{new Rect(0,0,176,150)});
                        Check(randomStyle.CalcSize(new GUIContent(PortLocalization.Translate("Random"))).x<=158,"Localized Random text fits the skin card in "+PortLocalization.CatalogData().languages[languageIndex]);
                        if(languageIndex==6)Check(((GUIStyle)typeof(CandyFlickUI).GetMethod("LanText",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{23,null,TextAnchor.MiddleCenter})).font.HasCharacter('\uc0ac'),"Korean LAN labels use the bundled Hangul font");
                        Capture(Folder+"/room-"+PortLocalization.CatalogData().languages[languageIndex]+".png");
                        afterCapture=()=>{if(++languageIndex<9)stage=6100;else{SetLanguage(0);bots[1].SetGroundSupport(true);Resolution(768,1024);stage=5;}};break;
                    case 5:
                        if(bots.Any(b=>b.State==null||b.State.players.First(p=>p.id==0).avatar!=22||b.State.players.First(p=>p.id==bots[1].Id).avatar!=20)){WaitFor(false,"Lobby profile changes did not reach all clients");return;}
                        Check(true,"Host and client profile changes replicate to every peer before the match");ProfileLayout();Capture(Folder+"/room-four-players-tablet.png");afterCapture=()=>Click(ui.LanProfileButtonRect.center);stage=302;break;
                    case 302:
                        Check(PickerOpen,"Tablet profile selector opens from the same name-row control");Capture(Folder+"/profiles-tablet.png");afterCapture=()=>Click(ProfileChoice(22));break;
                    case 303:
                        Check(!PickerOpen&&game.LAN.ProfilePicture==22,"Tablet picture selection closes without activating the underlying room controls");Click(ui.LanSkinsButtonRect.center);deadline=EditorApplication.timeSinceStartup+8;stage=305;break;
                    case 305:
                        if(game.ScreenState!=GameScreen.Skins){WaitFor(false,"LAN skins button did not open the selector");return;}
                        Check(game.LAN.IsHost&&game.LAN.Players.Length==4,"Opening skins keeps the existing LAN room and players connected");Click(ui.CandySkinTabRect.center);stage=306;break;
                    case 306: Click(ui.SkinViewport.position+CandyFlickUI.SkinCardRect(1,CandySkinSettings.Count+1).center);stage=307;break;
                    case 307:
                        if(game.CandySkins.Selected!=1||bots.Any(b=>b.State.players.First(p=>p.id==0).candyChoice!=1)){WaitFor(false,"Candy selection from the LAN skins screen did not reach the room");return;}
                        Check(true,"Candy selected from the room's skins screen updates every participant");game.CandySkins.Select(CandySkinSettings.RandomChoice);ui.Back();deadline=EditorApplication.timeSinceStartup+8;stage=308;break;
                    case 308:
                        if(game.ScreenState!=GameScreen.LanLobby){WaitFor(false,"Skins Back did not return to LAN");return;}
                        Check(game.LAN.IsHost&&game.LAN.Players.Length==4,"Skins Back returns to the same connected LAN room");
                        for(int i=0;i<6;i++){var extra=new Bot("Extra"+(i+5)){Avatar=30+i};bots.Add(extra);extra.net.Join("127.0.0.1:"+LanTransport.GamePort,Time.realtimeSinceStartupAsDouble);}deadline=EditorApplication.timeSinceStartup+8;stage=6;break;
                    case 6:
                        if(game.LAN.Players.Length<10){WaitFor(false,"Nine clients did not fill the ten-player room");return;}
                        Check(game.LAN.Players.Select(p=>p.id).Distinct().Count()==10,"Host plus nine real TCP clients occupy ten unique player slots");
                        Capture(Folder+"/room-ten-tablet.png");afterCapture=()=>Drag(RosterCenter());stage=400;break;
                    case 400:
                        Check(((Vector2)typeof(CandyFlickUI).GetField("lanPlayersScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui)).y>0,"Ten-player lobby roster supports touch-style scrolling");
                        Capture(Folder+"/room-ten-bottom.png");var overflow=new Bot("Eleventh");bots.Add(overflow);overflow.net.Join("127.0.0.1:"+LanTransport.GamePort,Time.realtimeSinceStartupAsDouble);deadline=EditorApplication.timeSinceStartup+6;stage=401;break;
                    case 401:
                        if(!bots[9].Rejected){WaitFor(false,"Eleventh connection was not rejected");return;}
                        Check(game.LAN.Players.Length==10,"An eleventh player is rejected without changing the ten-player room");
                        Check(bots.Take(3).All(b=>b.net.Rooms.Any(r=>r.name=="Player"&&r.count>=1)),"UDP discovery continues to advertise compatible ten-player rooms");
                        foreach(var bot in bots.Take(9))bot.Tracked=true;
                        Resolution(640,1136);game.LAN.EditorTrackingOverride=true;Check(game.LAN.StartMatch(),"Host starts a ten-player match");
                        game.GameCamera.clearFlags=CameraClearFlags.SolidColor;game.GameCamera.backgroundColor=Color.black;
                        Vector3 tenView=new Vector3(0,500,900);game.SetTrackedView(tenView,Quaternion.LookRotation(new Vector3(0,70,0)-tenView),Matrix4x4.Perspective(60,(float)Screen.width/Screen.height,50,5000),true);
                        deadline=EditorApplication.timeSinceStartup+6;stage=402;break;
                    case 402:
                        if(!game.CanSimulate){WaitFor(false,"Ten-player tracking gate did not release");return;}
                        Check(game.World.GetComponentsInChildren<LanRingView>().Length==10,"Ten players receive ten shared rings");
                        var cards=Enumerable.Range(0,10).Select(ui.LanPlayerScoreRect).ToArray();
                        Check(cards[0].y==cards[4].y&&cards[5].y==cards[9].y&&cards[5].y>cards[0].y&&cards.All(r=>!r.Overlaps(ui.LanDistanceScoreRect)),"Ten-player HUD uses two rows of five clear of the distance plate");
                        Capture(Folder+"/playing-ten.png");afterCapture=()=>game.LAN.Match.Step(60);deadline=EditorApplication.timeSinceStartup+5;stage=403;break;
                    case 403:
                        if(game.ScreenState!=GameScreen.Results){WaitFor(false,"Ten-player match did not finish");return;}
                        Capture(Folder+"/results-ten.png");afterCapture=()=>Drag(ResultsCenter());stage=404;break;
                    case 404:
                        Check(((Vector2)typeof(CandyFlickUI).GetField("lanResultsScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui)).y>0,"Ten-player results list supports touch-style scrolling");Capture(Folder+"/results-ten-bottom.png");
                        afterCapture=()=>{game.LAN.ReturnToRoom();foreach(var bot in bots.Skip(3).ToArray()){bot.net.Dispose();bots.Remove(bot);}foreach(var bot in bots)bot.Tracked=false;};deadline=EditorApplication.timeSinceStartup+6;stage=405;break;
                    case 405:
                        if(game.LAN.Players.Length!=4){WaitFor(false,"Extra capacity-test clients did not leave");return;}
                        Check(game.LAN.Players.All(p=>p.groundSupported),"Updated capability is shared with the host");
                        Check(game.LAN.StartMatch(),"Host starts the four-player regression match");stage=7;break;
                    case 7:
                        game.GameCamera.clearFlags=CameraClearFlags.SolidColor;game.GameCamera.backgroundColor=Color.black;
                        Vector3 preview=new Vector3(0,500,900);
                        game.SetTrackedView(preview,Quaternion.LookRotation(new Vector3(0,70,0)-preview),Matrix4x4.Perspective(60,(float)Screen.width/Screen.height,50,5000),true);
                        Check(game.IsLanMatch&&game.IsAR&&!game.IsNonAR&&game.NonARModeEnabled&&game.GroundARModeEnabled,"LAN forces image AR without changing saved Non AR or Ground options");
                        Check(!game.CanSimulate&&Mathf.Abs(game.LAN.Remaining-60)<.001f,"All players must find the card before the timer or physics starts");
                        Check(game.GameplayMusic.clip==Resources.Load<AudioClip>(LanMultiplayer.TrackResource),"LAN selects its dedicated supplied music even with in-game music off");
                        Capture(Folder+"/waiting-for-cards.png");clock=game.LAN.Remaining;break;
                    case 8:
                        Check(game.LAN.Remaining==clock,"Waiting for a card does not consume match time");foreach(var bot in bots)bot.Tracked=true;deadline=EditorApplication.timeSinceStartup+5;break;
                    case 9:
                        if(!game.CanSimulate||game.LAN.Remaining>59){WaitFor(false,"Readiness barrier did not release");return;}
                        Check(game.LAN.Phase==LanPhase.Playing&&game.LAN.Remaining<60,"All four tracked cards start the shared active-play clock");
                        Check(game.GameplayMusic.isPlaying&&!game.InGameMusicEnabled,"LAN music plays while the ordinary gameplay music option is off");
                        Check(game.World.GetComponentsInChildren<LanCandyView>(true).Count(v=>!v.Flying)==3,"Host displays all three other players' pre-throw candies");
                        var rings=game.World.GetComponentsInChildren<LanRingView>();
                        Check(rings.Length==game.LAN.Players.Length&&rings.All(r=>r.GetComponent<Collider>().enabled),"One shared host-authoritative ring per player is available from the start of LAN play");
                        var bearings=rings.Select(r=>Mathf.Repeat(Mathf.Atan2(r.transform.position.x,r.transform.position.z)*Mathf.Rad2Deg,360)).OrderBy(a=>a).ToArray();
                        Check(Enumerable.Range(0,bearings.Length).All(i=>Mathf.Repeat(bearings[(i+1)%bearings.Length]-bearings[i],360)<360f/rings.Length+20),"Ring lanes surround Om Nom across all 360 degrees instead of following the host camera");
                        Check(bots.All(b=>b.State.rings.Length==game.LAN.Players.Length),"All three clients receive the same shared ring set");
                        Check(bots.All(b=>b.State.players.All(p=>p.avatar==game.LAN.Players.First(host=>host.id==p.id).avatar)),"In-game snapshots retain every player's lobby profile picture");
                        Check(bots.All(b=>b.State.candies.Where(c=>c.ready).All(c=>c.skin==game.LAN.Players.First(p=>p.id==c.owner).readySkin)),"All clients receive the same server-resolved held candy, including Random");
                        Check(game.LAN.Players.First(p=>p.id==bots[0].Id).readySkin==1&&game.LAN.Players.First(p=>p.id==bots[1].Id).readySkin==2,"Fixed candy preferences resolve independently to donut and cupcake");HudChecks();
                        Check(bots.All(b=>b.State!=null&&b.State.candies.Count(c=>c.ready)==4),"All TCP clients receive every player's pre-throw candy pose");
                        game.StartCoroutine(CapturePlayingFrame());afterCapture=()=>{bots[1].Tracked=false;deadline=EditorApplication.timeSinceStartup+5;};break;
                    case 10:
                        if(game.LAN.Phase!=LanPhase.Paused){WaitFor(false,"Lost client card did not pause everyone");return;}
                        Check(!ui.LanScanRectangleShown,"A remote tracking loss does not show the local scan rectangle");clock=game.LAN.Remaining;Capture(Folder+"/paused-lost-card.png");break;
                    case 11:
                        Check(game.LAN.Remaining==clock&&!game.CanSimulate&&!game.Ready&&Time.timeScale==0,"One lost card freezes the host timer, physics and throwing");
                        Check(bots.All(b=>b.State.phase==LanPhase.Paused),"Every connected client receives the shared pause");game.LAN.EditorTrackingOverride=false;stage=92;break;
                    case 92:
                        Check(ui.LanScanRectangleShown,"Local tracking loss shows the scan rectangle while the group is paused");Capture(Folder+"/paused-local-card-lost.png");afterCapture=()=>game.LAN.EditorTrackingOverride=true;stage=93;break;
                    case 93:
                        Check(!ui.LanScanRectangleShown,"Recovering local tracking hides its rectangle even while another player is still lost");game.Pause();Capture(Folder+"/pause-menu-phone.png");afterCapture=()=>{game.Resume();bots[1].Tracked=true;deadline=EditorApplication.timeSinceStartup+5;};stage=12;break;
                    case 12:
                        if(!game.CanSimulate){WaitFor(false,"Recovered card did not resume match");return;}
                        game.ToggleMusic();Check(!game.GameplayMusic.isPlaying,"Master music mute still silences the dedicated LAN track");game.ToggleMusic();Check(game.GameplayMusic.isPlaying,"Master unmute resumes LAN music");
                        heldCandySkins.Clear();foreach(var p in game.LAN.Players)heldCandySkins[p.id]=p.readySkin;
                        foreach(var bot in bots)bot.Fire();deadline=EditorApplication.timeSinceStartup+4;break;
                    case 13:
                        if(game.LAN.Players.Skip(1).Any(p=>p.thrown<1)){WaitFor(false,"Host did not accept simultaneous throws from all clients");return;}
                        Check(game.World.GetComponentsInChildren<LanCandyView>(true).SelectMany(c=>c.GetComponentsInChildren<MeshRenderer>(true)).All(r=>!r.HasPropertyBlock()),"Held and flying candies retain the native materials without owner-color tints");
                        var candies=game.World.GetComponentsInChildren<LanCandyView>().Where(v=>v.Flying).ToArray();
                        Check(candies.Length==3&&candies.All(c=>!c.Body.isKinematic),"Three simultaneous client throws are simulated as host rigidbodies");
                        Check(candies.All(c=>c.Skin==heldCandySkins[c.Owner]),"Every shot uses the held server-selected candy instead of rerolling on clients");
                        Check(bots.All(b=>b.State.candies.Where(c=>!c.ready).All(c=>c.skin==heldCandySkins[c.owner])),"Flying candy skin IDs replicate consistently to every client");
                        foreach(var candy in candies){candy.Body.useGravity=false;candy.Body.linearVelocity=Vector3.zero;candy.Body.isKinematic=true;}
                        feedCandy=candies.First(c=>c.Owner==bots[0].Id);floorCandy=candies.First(c=>c.Owner==bots[1].Id);expiryCandy=candies.First(c=>c.Owner==bots[2].Id);break;
                    case 14:
                        Check(!feedCandy.Landed,"Airborne feed fixture has not previously contacted the floor");
                        var native=game.World.Find("Candies/Candy1").GetComponentsInChildren<Collider>().Select(c=>c.enabled).ToArray();
                        Check(feedCandy.GetComponentsInChildren<Collider>().Select(c=>c.enabled).SequenceEqual(native),"Host LAN candies preserve the original enabled and disabled collider configuration");
                        // First cross a real host ring trigger, then the mouth collider.
                        game.Actor.enabled=false;scoredDistance=feedCandy.ScoreDistance;
                        var targetRing=game.World.GetComponentsInChildren<LanRingView>().First(r=>!r.State.hit);
                        hitRingId=targetRing.State.id;hitRingGeneration=targetRing.State.generation;
                        feedCandy.Body.position=targetRing.transform.position-targetRing.transform.forward*120;feedCandy.Body.rotation=Quaternion.identity;feedCandy.Body.isKinematic=false;
                        feedCandy.Body.angularVelocity=Vector3.zero;feedCandy.Body.linearVelocity=targetRing.transform.forward*700;feedCandy.Body.WakeUp();Physics.SyncTransforms();
                        floorCandy.Body.position=new Vector3(300,100,0);floorCandy.Body.isKinematic=false;floorCandy.Body.useGravity=true;
                        floorCandy.Body.linearVelocity=Vector3.down*100;floorCandy.Body.WakeUp();deadline=EditorApplication.timeSinceStartup+4;break;
                    case 15:
                        if(feedCandy.RingBonus!=GameRules.VerticalRingBonus){WaitFor(false,"Host candy did not cross the real ring trigger");return;}
                        Check(game.LAN.Players.First(p=>p.id==bots[0].Id).score==0,"Passing a ring awards no points before a successful feed");
                        var hitRing=game.World.GetComponentsInChildren<LanRingView>().First(r=>r.State.id==hitRingId);
                        Check(hitRing.State.hit&&!hitRing.GetComponent<Collider>().enabled&&!game.LAN.TryPassLanRing(hitRing,feedCandy),"A host trigger marks its ring hit once and rejects repeat contacts");
                        var secondRing=game.World.GetComponentsInChildren<LanRingView>().First(r=>!r.State.hit);
                        Check(game.LAN.TryPassLanRing(secondRing,feedCandy)&&feedCandy.RingBonus==15,"Multiple ring passes assign the native 15-point bonus without stacking it");
                        secondRingId=secondRing.State.id;collectedPosition=hitRing.State.position;
                        var expiryRing=game.World.GetComponentsInChildren<LanRingView>().First(r=>!r.State.hit);
                        expiredRingId=expiryRing.State.id;Check(game.LAN.TryPassLanRing(expiryRing,expiryCandy),"A different player's candy can collect another shared ring");
                        var unclaimed=game.World.GetComponentsInChildren<LanRingView>().First(r=>!r.State.hit);unclaimedRingId=unclaimed.State.id;unclaimedPosition=unclaimed.State.position;
                        feedCandy.Body.linearVelocity=Vector3.zero;feedCandy.Body.isKinematic=true;
                        game.LAN.Match.Step(14.9f-game.LAN.Match.Elapsed);Call(game.LAN,"RefreshLanRings");
                        Check(unclaimed.State.generation==1,"An untouched ring does not relocate before 15 active seconds");
                        game.LAN.Match.Step(.2f);feedCandy.Born=expiryCandy.Born=floorCandy.Born=game.LAN.Match.Elapsed;stage=90;break;
                    case 90:
                        var retained=game.World.GetComponentsInChildren<LanRingView>().First(r=>r.State.id==hitRingId);
                        Check(retained.State.generation==hitRingGeneration&&retained.State.hit&&retained.State.position==collectedPosition,"Collected ring stays in place while its candy still exists, even past the idle deadline");
                        var timedRing=game.World.GetComponentsInChildren<LanRingView>().First(r=>r.State.id==unclaimedRingId);
                        Check(timedRing.State.generation==2&&!timedRing.State.hit&&Vector3.Distance(timedRing.State.position,unclaimedPosition)>=40,"An untouched ring relocates after 15 active seconds");
                        Bounds mouth=game.Actor.GetComponent<Collider>().bounds;
                        feedCandy.Body.position=new Vector3(mouth.center.x,mouth.center.y,mouth.min.z-100);feedCandy.Body.rotation=Quaternion.identity;feedCandy.Body.isKinematic=false;
                        feedCandy.Body.angularVelocity=Vector3.zero;feedCandy.Body.linearVelocity=Vector3.forward*700;feedCandy.Body.WakeUp();Physics.SyncTransforms();
                        expiryCandy.Born=game.LAN.Match.Elapsed-4.1f;stage=16;deadline=EditorApplication.timeSinceStartup+4;break;
                    case 16:
                        if(game.LAN.Players.First(p=>p.id==bots[0].Id).fed<1||!floorCandy.Landed){WaitFor(false,"Native host contacts did not resolve: fed="+game.LAN.Players.First(p=>p.id==bots[0].Id).fed+", floor="+floorCandy.Landed+", phase="+game.LAN.Phase);return;}
                        game.Actor.enabled=true;
                        Check(floorCandy.Skin==2&&floorCandy.GetComponent<MeshCollider>().enabled&&floorCandy.GetComponent<SphereCollider>()==null,"LAN cupcakes retain the same floor collider as the other candies");
                        Check(feedCandy==null,"Eaten donut is removed normally after using the original airborne hitbox");
                        Check(game.LAN.Players.First(p=>p.id==bots[0].Id).fed==1,"A real host mouth contact credits the correct remote player exactly once");
                        var scorer=game.LAN.Players.First(p=>p.id==bots[0].Id);
                        Check(scorer.baseScore==GameRules.DistanceScoreUnits(scoredDistance)&&scorer.ringScore==15&&scorer.score==scorer.baseScore+15,"Host catch credits original distance-at-launch points plus the ring bonus to the candy owner");
                        fedBefore=game.LAN.Players.First(p=>p.id==bots[1].Id).fed;floorCandy.Body.useGravity=false;floorCandy.Body.position=game.Actor.Mouth;floorCandy.Body.linearVelocity=Vector3.zero;break;
                    case 17:
                        Check(game.LAN.Players.First(p=>p.id==bots[1].Id).fed==fedBefore,"A candy already on the floor cannot score when moved to Om Nom");
                        Check(bots.All(b=>b.State.players.First(p=>p.id==bots[0].Id).fed==1),"Host feed scores replicate to every client");
                        Check(bots.All(b=>b.State.players.First(p=>p.id==bots[0].Id).score==GameRules.DistanceScoreUnits(scoredDistance)+15),"Distance and ring score totals replicate to all three clients");
                        break;
                    case 18:
                        var replacement=game.World.GetComponentsInChildren<LanRingView>().First(r=>r.State.id==hitRingId);
                        Check(replacement.State.generation==hitRingGeneration+1&&!replacement.State.hit&&replacement.GetComponent<Collider>().enabled&&Vector3.Distance(replacement.State.position,collectedPosition)>=40,"Eating the collector immediately moves its ring to a different place");
                        var remainingRings=game.World.GetComponentsInChildren<LanRingView>();
                        Check(remainingRings.First(r=>r.State.id==secondRingId).State.generation==2,"Every ring collected by the same candy moves when that candy is eaten");
                        Check(!expiryCandy&&remainingRings.First(r=>r.State.id==expiredRingId).State.generation==2,"A missed candy disappearing also moves the ring it collected");
                        Check(remainingRings.First(r=>r.State.id==unclaimedRingId).State.generation==2,"Resolving other candies does not reset an unrelated ring again");RelocationChecks();
                        game.LAN.Match.Step(60);deadline=EditorApplication.timeSinceStartup+4;break;
                    case 19:
                        if(game.ScreenState!=GameScreen.Results){WaitFor(false,"60 active seconds did not finish the match");return;}
                        Check(game.LAN.Remaining==0&&LanRules.Winners(game.LAN.Players).SequenceEqual(new[]{bots[0].Id}),"Highest distance and ring score wins at 60 active seconds");
                        resultScores=game.LAN.Players.Select(p=>p.score).ToArray();Capture(Folder+"/results-phone.png");afterCapture=()=>{foreach(var player in game.LAN.Match.Players)player.score=100;};stage=315;break;
                    case 315:
                        Check(LanRules.Winners(game.LAN.Players).Length==4,"Result portraits share the same layout for a draw");Capture(Folder+"/results-draw.png");afterCapture=()=>game.LAN.Match.Players[0].score=200;stage=316;break;
                    case 316:
                        Check(LanRules.Winners(game.LAN.Players).SequenceEqual(new[]{game.LAN.LocalId}),"Result portraits share the same layout for a local win");Capture(Folder+"/results-win.png");afterCapture=()=>{for(int i=0;i<resultScores.Length;i++)game.LAN.Match.Players[i].score=resultScores[i];};stage=20;break;
                    case 20: game.LAN.ReturnToRoom();Check(!game.IsLanMatch&&game.LAN.Players.Length==4,"Host returns all four players to the room for a rematch");
                        game.LAN.ToggleRoomGround();game.LAN.EditorCalibrationOverride=true;Check(game.LAN.StartMatch(),"Host starts a local Ground calibration match");stage=200;break;
                    case 200:
                        Check(game.LAN.CalibrationCueCount==0,"The host never plays the client calibration cue");
                        game.GameCamera.clearFlags=CameraClearFlags.SolidColor;game.GameCamera.backgroundColor=Color.black;
                        CalibrationMath();Check(game.LAN.ShowHostCalibration&&!game.LAN.Ground.placed&&game.LAN.Remaining==60,"Host shows the newer image before any Ground gameplay or timer starts");
                        Check(ui.LanCalibrationImage==Resources.Load<Texture2D>("images/all/marker")&&ui.LanCalibrationImage.name!="marker_old","Host calibration uses the newer CF image");
                        Check(game.LAN.SetHostOriginReady(game.LAN.Ground.generation),"Host freezes its initial origin without scanning its own screen");Capture(Folder+"/host-ground-calibration.png");deadline=EditorApplication.timeSinceStartup+5;break;
                    case 201:
                        if(bots.Any(b=>!b.State.ground.originReady)){WaitFor(false,"Host origin readiness did not replicate");return;}
                        Check(!game.LAN.PublishGroundPlacement(new Vector3(0,-.5f,.4f),Quaternion.Euler(90,0,0),game.LAN.Ground.generation),"Host cannot place before every client calibrates");bots[0].Calibrated=bots[1].Calibrated=true;break;
                    case 202:
                        Check(game.LAN.ShowHostCalibration&&game.LAN.Remaining==60,"Host keeps displaying the image while one client remains uncalibrated");bots[2].Calibrated=true;deadline=EditorApplication.timeSinceStartup+5;break;
                    case 203:
                        if(!game.LAN.AllClientsCalibrated){WaitFor(false,"Client calibration reports did not arrive");return;}
                        Check(!game.LAN.ShowHostCalibration,"All client scans release the host image screen for ground placement");
                        Check(game.LAN.PublishGroundPlacement(new Vector3(0,-.5f,.4f),Quaternion.Euler(90,0,0),game.LAN.Ground.generation),"Host publishes one ground pose relative to its frozen reference");deadline=EditorApplication.timeSinceStartup+5;break;
                    case 204:
                        if(!game.CanSimulate||game.LAN.Remaining>=60){WaitFor(false,"Shared Ground readiness did not start the clock");return;}
                        Check(bots.All(b=>b.State.groundMode&&b.State.ground.placed&&b.State.ground.markerWidth==.06f),"All real TCP peers receive the same Ground reference, physical image width and placement");
                        priorGroundGeneration=game.LAN.Ground.generation;bots[2].ReportGroundLost();deadline=EditorApplication.timeSinceStartup+5;break;
                    case 205:
                        if(game.LAN.Ground.generation==priorGroundGeneration){WaitFor(false,"Client tracking loss did not reset calibration");return;}
                        clock=game.LAN.Remaining;Check(!game.CanSimulate&&!game.LAN.Ground.placed,"Ground tracking loss pauses everyone and invalidates the old origin");break;
                    case 206:
                        Check(game.LAN.Remaining==clock&&game.LAN.ShowHostCalibration,"Recalibration preserves the paused clock and returns the host image");
                        game.LAN.SetHostOriginReady(game.LAN.Ground.generation);foreach(var bot in bots)bot.Calibrated=true;bots[2].GenerationOverride=priorGroundGeneration;break;
                    case 207:
                        Check(!game.LAN.AllClientsCalibrated&&!game.CanSimulate,"Old-generation calibration cannot resume Ground play");bots[2].GenerationOverride=null;deadline=EditorApplication.timeSinceStartup+5;break;
                    case 208:
                        if(!game.LAN.AllClientsCalibrated){WaitFor(false,"Recalibrated peers did not report the new generation");return;}
                        Check(game.LAN.PublishGroundPlacement(new Vector3(.1f,-.5f,.4f),Quaternion.Euler(90,0,0),game.LAN.Ground.generation),"The host can choose a new spot after everyone recalibrates");deadline=EditorApplication.timeSinceStartup+5;break;
                    case 209:
                        if(!game.CanSimulate){WaitFor(false,"Recalibration did not resume play");return;}
                        Check(game.LAN.Remaining<clock,"Ground match resumes the retained timer after recalibration");
                        game.Actor.transform.position=new Vector3(125,0,-75);cachedNom=game.Actor.transform.position;cachedOffset=game.LAN.Ground.offset;cachedGeneration=game.LAN.Ground.generation;cachedPlacement=game.LAN.Ground.placement;
                        game.LAN.Match.Step(60);deadline=EditorApplication.timeSinceStartup+5;stage=430;break;
                    case 430:
                        if(game.ScreenState!=GameScreen.Results){WaitFor(false,"Ground match did not finish for rematch");return;}
                        game.LAN.ReturnToRoom();Check(game.ar.RetainingLanGround&&game.LAN.CanReuseGround,"Returning to the same room retains valid Ground calibration");
                        Check(game.LAN.StartMatch(),"Calibrated participants can start a Ground rematch");
                        Check(!game.LAN.ShowHostCalibration&&game.LAN.Ground.placed&&game.LAN.Ground.generation==cachedGeneration&&game.LAN.Ground.offset==cachedOffset&&Vector3.Distance(game.Actor.transform.position,cachedNom)<.01f,"Ground rematch reuses the same reference, placement and last Om Nom position without scans");deadline=EditorApplication.timeSinceStartup+6;stage=431;break;
                    case 431:
                        if(!game.CanSimulate){WaitFor(false,"Retained Ground rematch did not become ready");return;}
                        Check(game.LAN.Remaining<60&&game.LAN.Players.All(p=>p.score==0),"Reused calibration starts a fresh score/timer session");
                        game.Pause();Check(!ui.LanGroundMoveRect.Overlaps(ui.LanGroundCalibrateRect)&&!ui.LanGroundMoveRect.Overlaps(ui.LanPauseHomeRect)&&!ui.LanGroundCalibrateRect.Overlaps(ui.LanPauseHomeRect),"Host Ground move and recalibrate controls fit without overlapping home");
                        Capture(Folder+"/ground-pause-move.png");afterCapture=()=>Click(ui.LanGroundMoveRect.center);deadline=EditorApplication.timeSinceStartup+6;stage=432;break;
                    case 432:
                        if(game.LAN.Ground.placed){WaitFor(false,"Host move control did not enter surface placement");return;}
                        clock=game.LAN.Remaining;Check(game.ScreenState==GameScreen.Playing&&game.LAN.AllClientsCalibrated&&!game.LAN.ShowHostCalibration&&game.LAN.Ground.generation==cachedGeneration,"Move Om Nom keeps all calibration and only asks for a new surface");stage=433;break;
                    case 433:
                        Check(game.LAN.Remaining==clock,"Moving Om Nom pauses the active-play clock");
                        Check(game.LAN.PublishGroundPlacement(new Vector3(.3f,-.5f,.6f),Quaternion.Euler(90,0,0),cachedGeneration)&&game.LAN.Ground.placement==cachedPlacement+1,"New placement increments only its revision, keeping reference calibration");deadline=EditorApplication.timeSinceStartup+6;stage=434;break;
                    case 434:
                        if(!game.CanSimulate){WaitFor(false,"Moving Om Nom did not resume the same match");return;}
                        Check(game.LAN.Remaining<clock&&game.LAN.Ground.generation==cachedGeneration,"Move resumes the existing timer without rescanning");game.LAN.ReturnToRoom();bots[0].ReportGroundLost();deadline=EditorApplication.timeSinceStartup+6;stage=435;break;
                    case 435:
                        if(game.LAN.Ground.generation==cachedGeneration){WaitFor(false,"Room tracking loss did not invalidate cached calibration");return;}
                        Check(!game.LAN.CanReuseGround,"Tracking loss while in the lobby prevents reuse of invalid anchors");Check(game.LAN.StartMatch(),"Room can restart after invalidated tracking");
                        Check(game.LAN.ShowHostCalibration&&!game.LAN.Ground.placed,"Invalid anchors request fresh calibration");game.LAN.SetHostOriginReady(game.LAN.Ground.generation);foreach(var bot in bots)bot.Calibrated=true;deadline=EditorApplication.timeSinceStartup+6;stage=436;break;
                    case 436:
                        if(bots.Any(b=>b.State.epoch!=game.LAN.Epoch||b.State.ground.generation!=game.LAN.Ground.generation)){WaitFor(false,"Fresh calibration generation did not reach every client");return;}
                        foreach(var bot in bots)bot.Calibrated=true;
                        if(!game.LAN.AllClientsCalibrated){WaitFor(false,"Renewed calibration reports did not arrive");return;}
                        Check(game.LAN.PublishGroundPlacement(new Vector3(.1f,-.5f,.4f),Quaternion.Euler(90,0,0),game.LAN.Ground.generation),"Renewed Ground placement succeeds");deadline=EditorApplication.timeSinceStartup+6;stage=437;break;
                    case 437:
                        if(!game.CanSimulate){WaitFor(false,"Renewed Ground placement did not become ready");return;}
                        game.LAN.ReturnToRoom();cachedGeneration=game.LAN.Ground.generation;bots[2].net.Dispose();bots.RemoveAt(2);deadline=EditorApplication.timeSinceStartup+6;stage=438;break;
                    case 438:
                        if(game.LAN.Players.Length!=3){WaitFor(false,"Departing calibrated player was not removed");return;}
                        Check(game.LAN.CanReuseGround&&game.LAN.StartMatch()&&!game.LAN.ShowHostCalibration&&game.LAN.Ground.generation==cachedGeneration,"A smaller subset of the same calibrated players reuses Ground placement");deadline=EditorApplication.timeSinceStartup+6;stage=439;break;
                    case 439:
                        if(!game.CanSimulate){WaitFor(false,"Subset rematch did not become ready");return;}
                        game.LAN.ReturnToRoom();var newcomer=new Bot("New person");bots.Add(newcomer);newcomer.net.Join("127.0.0.1:"+LanTransport.GamePort,Time.realtimeSinceStartupAsDouble);deadline=EditorApplication.timeSinceStartup+6;stage=440;break;
                    case 440:
                        if(game.LAN.Players.Length!=4){WaitFor(false,"Replacement participant did not join");return;}
                        Check(!game.LAN.CanReuseGround&&game.LAN.Ground.generation!=cachedGeneration,"A newly joined participant invalidates calibration even when its slot ID is reused");
                        Check(game.LAN.StartMatch()&&game.LAN.ShowHostCalibration&&!game.LAN.Ground.placed,"Room including a new participant calibrates before play");game.LAN.ReturnToRoom();game.LAN.ToggleRoomGround();game.LAN.EditorCalibrationOverride=null;
                        bots[2].net.Dispose();bots.RemoveAt(2);deadline=EditorApplication.timeSinceStartup+6;stage=100;break;
                    case 100:
                        if(game.LAN.Players.Length!=3){WaitFor(false,"Disconnected lobby player was not removed");return;}
                        Check(game.LAN.StartMatch(),"Three-player ring fixture starts");
                        Check(game.World.GetComponentsInChildren<LanRingView>().Length==3,"Three players produce exactly three rings");
                        game.LAN.ReturnToRoom();bots[1].net.Dispose();bots.RemoveAt(1);deadline=EditorApplication.timeSinceStartup+5;break;
                    case 101:
                        if(game.LAN.Players.Length!=2){WaitFor(false,"Second lobby disconnect was not removed");return;}
                        Check(game.LAN.StartMatch(),"Two-player ring fixture starts");
                        var pair=game.World.GetComponentsInChildren<LanRingView>();
                        Check(pair.Length==2&&Vector3.Dot(new Vector3(pair[0].State.position.x,0,pair[0].State.position.z).normalized,new Vector3(pair[1].State.position.x,0,pair[1].State.position.z).normalized)<-.9f,"Two players produce two rings on opposite sides of Om Nom");
                        game.LAN.ReturnToRoom();game.LAN.CloseRoom();foreach(var bot in bots)bot.net.Dispose();bots.Clear();game.ReturnToMenu();Resolution(768,1024);stage=21;break;
                    case 21: CheckMenuLayout();Check(!ui.MenuSoloPlayRect.Overlaps(ui.MenuSkinsRect)&&!ui.MenuSkinsRect.Overlaps(ui.MenuMultiplayerRect),"Tablet menu buttons do not overlap");Check(ui.MenuMultiplayerRect.yMax<=ui.ReferenceHeight,"Tablet lower menu row fits");Capture(Folder+"/menu-tablet.png");break;
                    case 22: game.LAN.OpenBrowser();Capture(Folder+"/finder-tablet.png");break;
                    case 23: game.LAN.LeaveRoom();Resolution(1920,1080);break;
                    case 24: CheckMenuLayout();Capture(Folder+"/menu-wide.png");break;
                    case 25: game.LAN.OpenBrowser();Capture(Folder+"/finder-wide.png");break;
                    case 26:
                        game.LAN.CloseRoom();fakeHost=new LanTransport();Check(fakeHost.Host(0),"Client integration fixture opens a real TCP host");
                        game.LAN.OpenBrowser();game.LAN.JoinRoom("127.0.0.1:"+fakeHost.Port);deadline=EditorApplication.timeSinceStartup+5;break;
                    case 27:
                        if(fakePeer==null||!game.LAN.InRoom){WaitFor(false,"Actual client controller did not join the fixture host");return;}
                        Check(fakeHelloAvatar==game.LAN.ProfilePicture,"Actual client join sends only its saved numeric profile ID");game.LAN.SetProfilePicture(41);deadline=EditorApplication.timeSinceStartup+5;stage=310;break;
                    case 310:
                        if(fakeProfileAvatar!=41){WaitFor(false,"Actual client profile change did not reach the host");return;}
                        Check(PlayerPrefs.GetInt(LanMultiplayer.ProfilePreference,-1)==41,"Actual client profile changes send the numeric ID and save locally");
                        fakeState=new LanSnapshot{epoch=77,skin=(int)NommieSkin.Greek,phase=LanPhase.FindingCard,remaining=60,players=new[]{new LanPlayer{id=0,name="Host",avatar=16,candyChoice=2,readySkin=2,tracked=true},new LanPlayer{id=1,name="Client",avatar=41,candyChoice=3,readySkin=1,tracked=true}},actorClip="idle"};
                        fakePeer.Send(new LanMessage{kind="begin",state=fakeState});deadline=EditorApplication.timeSinceStartup+5;stage=28;break;
                    case 28:
                        if(!game.IsLanMatch){WaitFor(false,"Client did not enter the match");return;}
                        Check(game.LAN.Players[0].avatar==16&&game.LAN.Players[1].avatar==41,"Actual client displays the host's and its own replicated profile IDs");
                        Check(!game.LAN.IsHost&&game.IsAR&&!game.IsNonAR&&game.Skins.Selected==NommieSkin.Greek,"Client uses image AR and the host's temporary Om Nom skin");
                        // Supply the camera calibration that a physical image tracker
                        // normally provides; the loopback fixture has no AR video rig.
                        game.GameCamera.clearFlags=CameraClearFlags.SolidColor;game.GameCamera.backgroundColor=Color.black;
                        Vector3 wideView=new Vector3(0,500,900);
                        game.SetTrackedView(wideView,Quaternion.LookRotation(new Vector3(0,70,0)-wideView),Matrix4x4.Perspective(60,(float)Screen.width/Screen.height,50,5000),true);
                        fakeState.phase=LanPhase.Playing;fakeState.remaining=43;fakeState.actorPosition=new Vector3(100,0,75);fakeState.actorRotation=Quaternion.Euler(0,90,0);fakeState.actorClip="open";
                        fakeState.rings=new[]{new LanRingState{id=2,generation=1,position=new Vector3(600,350,0),rotation=Quaternion.Euler(0,-90,0)}};
                        fakeState.candies=new[]{new LanCandyState{id=42,owner=0,skin=2,position=new Vector3(0,300,400),rotation=Quaternion.identity},new LanCandyState{id=-1,owner=0,skin=2,ready=true,position=new Vector3(200,400,500),rotation=Quaternion.identity}};
                        fakePeer.Send(new LanMessage{kind="state",state=fakeState});deadline=EditorApplication.timeSinceStartup+5;break;
                    case 29:
                        var replica=game.World.GetComponentsInChildren<LanCandyView>().FirstOrDefault(v=>v.Id==42);
                        if(!replica){WaitFor(false,"Client did not render the host candy snapshot");return;}
                        Check(replica.Skin==2&&game.CandyGhost.GetComponent<CandySkinVisual>().Skin==1,"Client renders the replicated cupcake and held donut without a local Random draw");
                        Check(replica.Body.isKinematic&&replica.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Client candy replicas do not simulate physics or award collisions");
                        Check(Vector3.Distance(game.Actor.transform.position,fakeState.actorPosition)<5,"Client follows the host-computed Om Nom position");
                        HudChecks();
                        Check(game.World.GetComponentsInChildren<LanCandyView>().Any(v=>v.Id==-1&&!v.Flying),"Client renders the host's candy before it is thrown");
                        var clientRing=game.World.GetComponentsInChildren<LanRingView>().Single();
                        Check(clientRing.State.id==2&&!clientRing.GetComponent<Collider>().enabled,"Client renders the host ring pose with trigger authority disabled");
                        fakeState.players[1].readySkin=2;fakeState.rings[0].hit=true;fakeState.players[0].score=95;fakeState.players[0].fed=3;fakePeer.Send(new LanMessage{kind="state",state=fakeState});next=EditorApplication.timeSinceStartup+.9;break;
                    case 30:
                        Check(game.CandyGhost.GetComponent<CandySkinVisual>().Skin==2,"A changed host-selected Random result updates the local held model");
                        Check(game.LAN.Players.First(p=>p.id==0).score==95&&game.Session.shots==0,"Client scores come from host snapshots without changing normal round progress");
                        Check(game.World.GetComponentsInChildren<LanRingView>().Single().State.hit,"Client follows replicated ring hits instead of awarding them locally");
                        Capture(Folder+"/playing-wide.png");afterCapture=()=>{fakeState.groundMode=true;fakeState.players[0].groundSupported=false;fakeState.phase=LanPhase.Lobby;fakePeer.Send(new LanMessage{kind="room",state=fakeState});deadline=EditorApplication.timeSinceStartup+5;stage=210;};break;
                    case 210:
                        if(game.IsLanMatch){WaitFor(false,"Client did not return to the Ground room fixture");return;}
                        Check(game.LAN.GroundWarning==""&&ui.LanFooterText=="","Clients see neither the host-only Ground warning nor duplicate room footer text");
                        game.LAN.ToggleRoomGround();Check(game.LAN.GroundMode,"Clients cannot change the host's AR room mode");game.LAN.EditorCalibrationOverride=false;
                        fakeState.epoch=78;fakeState.phase=LanPhase.FindingCard;fakeState.remaining=60;
                        foreach(var player in fakeState.players){player.score=player.fed=0;}fakeState.candies=Array.Empty<LanCandyState>();fakeState.rings=Array.Empty<LanRingState>();fakeState.ground=new LanGroundState{generation=7,originReady=true,markerWidth=.06f};
                        Capture(Folder+"/client-room.png");afterCapture=()=>{fakeState.players[0].groundSupported=true;fakePeer.Send(new LanMessage{kind="begin",state=fakeState});deadline=EditorApplication.timeSinceStartup+5;};break;
                    case 211:
                        if(!game.IsLanMatch){WaitFor(false,"Ground client did not enter calibration");return;}
                        game.GameCamera.clearFlags=CameraClearFlags.SolidColor;game.GameCamera.backgroundColor=Color.black;
                        // The synthetic camera seam must mirror native world visibility.
                        Call(game.ar,"SetTracking",false);
                        Check(!game.LAN.LocalTracking&&!game.LAN.Ready&&ui.LanScanRectangleShown,"Uncalibrated clients get the card scan frame without starting gameplay");
                        Check(!game.LAN.SetHostOriginReady(7)&&!game.LAN.PublishGroundPlacement(Vector3.zero,Quaternion.identity,7),"Clients cannot choose the shared origin or ground position");Capture(Folder+"/ground-client-calibration.png");
                        calibrationCueBefore=game.LAN.CalibrationCueCount;calibrationSoundWasEnabled=game.SoundEnabled;if(!game.SoundEnabled)game.ToggleSound();
                        afterCapture=()=>game.LAN.EditorCalibrationOverride=true;break;
                    case 212:
                        Check(game.LAN.CalibrationCueCount==calibrationCueBefore+1&&Resources.Load<AudioClip>("LanAudio/calibrated")&&game.GetComponents<AudioSource>().Any(a=>a.isPlaying&&!a.loop),"A calibrated Ground client plays the supplied star_3 cue once through the sound-effects path");
                        Check(!ui.LanScanRectangleShown&&!game.LAN.Ready,"Calibrated clients lose the scan frame and wait for the host's placement");
                        fakeState.ground.placed=true;fakeState.ground.offset=new Vector3(.2f,-.4f,.3f);fakeState.ground.rotation=Quaternion.Euler(90,0,0);fakeState.phase=LanPhase.Playing;
                        fakePeer.Send(new LanMessage{kind="state",state=fakeState});deadline=EditorApplication.timeSinceStartup+5;break;
                    case 213:
                        Check(game.LAN.CalibrationCueCount==calibrationCueBefore+1,"Remaining calibrated does not repeat the completion sound");
                        if(!game.LAN.LocalTracking){WaitFor(false,"Calibrated client did not accept shared placement");return;}
                        Call(game.ar,"SetTracking",true);
                        Check(game.LAN.Ground.offset==fakeState.ground.offset&&game.Session.shots==0,"Client uses host-relative Ground metadata without changing solo progress");
                        game.LAN.EditorCalibrationOverride=false;fakeState.ground=new LanGroundState{generation=8,markerWidth=.06f};fakeState.phase=LanPhase.Paused;
                        fakePeer.Send(new LanMessage{kind="state",state=fakeState});break;
                    case 214:
                        Check(!game.LAN.LocalTracking&&!game.LAN.Ready&&ui.LanScanRectangleShown,"A reset requires the client to scan the host's new calibration generation again");
                        game.LAN.EditorCalibrationOverride=true;Call(game.LAN,"TickCalibrationSound");Call(game.LAN,"TickCalibrationSound");
                        Check(game.LAN.CalibrationCueCount==calibrationCueBefore+2,"A fresh calibration generation plays once, while repeated calibrated updates stay silent");
                        if(game.SoundEnabled!=calibrationSoundWasEnabled)game.ToggleSound();
                        game.LAN.EditorCalibrationOverride=null;fakePeer.Dispose();deadline=EditorApplication.timeSinceStartup+5;stage=31;break;
                    case 31:
                        if(game.IsLanMatch){WaitFor(false,"Lost host did not cleanly exit the match");return;}
                        Check(game.ScreenState==GameScreen.LanLobby&&!game.LAN.InRoom,"Host disconnect returns the client to the match finder");
                        Check(game.Skins.Selected.ToString()==oldSkin&&PlayerPrefs.GetInt(TimeTravelSkins.Preference)==(int)NommieSkin.Disco,"Leaving restores the selected solo skin without rewriting its preference");
                        Check(!game.World.GetComponentsInChildren<LanRingView>(true).Any(),"Leaving LAN removes all multiplayer rings");
                        game.LAN.LeaveRoom();game.LAN.EditorTrackingOverride=null;game.LAN.EditorCalibrationOverride=null;game.LAN.EditorMarkerWidthOverride=null;game.StartGame();
                        Check(game.IsNonAR&&!game.IsAR&&game.GroundARModeEnabled&&!game.InGameMusicEnabled&&!game.GameplayMusic.isPlaying,"Normal play restores the user's Non AR, Ground and music choices");
                        Check(game.Session.Remaining==10&&!game.IsLanMatch,"Normal mode retains its original ten-candy round");game.ReturnToMenu();Resolution(640,960);break;
                    case 32:
                        CheckMenuLayout();
                        Check(!ui.MenuSoloPlayRect.Overlaps(ui.MenuSkinsRect)&&!ui.MenuSkinsRect.Overlaps(ui.MenuMultiplayerRect)&&ui.MenuMultiplayerRect.yMax<=960,"Short-phone solo, skins and LAN buttons fit without overlap");
                        Capture(Folder+"/menu-short-phone.png");
                        File.WriteAllText("Documentation/lan-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray(),limit="Real IPv4 loopback sockets with host plus three clients and a separate client-world fixture; synthetic image-card tracking. Unity Game view captures only when requested; no desktop captures. Synthetic calibration/device tracking only. Physical host-screen size, camera-to-display offset, ground detection and multi-phone alignment need device testing. No player build."},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void CalibrationMath()
        {
            Pose reference=new Pose(new Vector3(1.1f,.7f,-2),Quaternion.Euler(7,121,-3));
            Pose desired=new Pose(new Vector3(.9f,.15f,-1.6f),Quaternion.Euler(0,45,0));
            Pose relative=ARMarkerMode.RelativePose(reference,desired),roundTrip=ARMarkerMode.ApplyRelativePose(reference,relative);
            Check(Vector3.Distance(roundTrip.position,desired.position)<.00001f&&Quaternion.Angle(roundTrip.rotation,desired.rotation)<.01f,"Ground pose is preserved through a rotated host reference frame");
            Pose clientReference=new Pose(new Vector3(-3,2,4),Quaternion.Euler(-9,-62,3));
            Pose clientPlacement=ARMarkerMode.ApplyRelativePose(clientReference,relative);
            Pose recovered=ARMarkerMode.RelativePose(clientReference,clientPlacement);
            Check(Vector3.Distance(recovered.position,relative.position)<.00001f&&Quaternion.Angle(recovered.rotation,relative.rotation)<.01f,"Different local device maps reconstruct the same relative placement");
            Check(Vector3.Dot(ARMarkerMode.DisplayRotation*Vector3.up,Vector3.back)>.999f&&Vector3.Dot(ARMarkerMode.DisplayRotation*Vector3.forward,Vector3.up)>.999f,"Host screen reference faces opposite the rear camera with image-up preserved");
            Check(Mathf.Abs(CandyFlickUI.PhysicalMarkerWidth(500,1,400)-.03175f)<.00001f&&CandyFlickUI.PhysicalMarkerWidth(500,1,0)==0,"Host image width uses physical pixels and rejects unknown display DPI");
            Check(!LanMultiplayer.ValidGroundPlacement(new Vector3(float.NaN,0,0),Quaternion.identity)&&!LanMultiplayer.ValidGroundPlacement(Vector3.one*10,Quaternion.identity),"Invalid shared ground positions are rejected");
        }
        static void HudChecks()
        {
            Rect native=(Rect)Call(ui,"R","ScoresGPView$$GetPossibleScoresRect",false);
            Check(ui.LanDistanceScoreRect==native,"LAN uses the original singleplayer distance score plate position at "+Screen.width+"x"+Screen.height);
            Check(Enumerable.Range(0,4).All(i=>!ui.LanPlayerScoreRect(i).Overlaps(native)&&ui.LanPlayerScoreRect(i).width>100),"Distance score plate stays clear of all four player totals at "+Screen.width+"x"+Screen.height);
            Rect home=(Rect)Call(ui,"R","GamePlayScreen$$GetMenuRect",true),sound=(Rect)Call(ui,"R","GamePlayScreen$$GetSoundRect",true),help=(Rect)Call(ui,"R","GamePlayScreen$$GetHelpRect",true);
            Check(ui.LanPauseHomeRect.y==sound.y&&ui.LanPauseHomeRect.size==home.size&&ui.LanPauseHomeRect.x==home.x&&ui.LanPauseSoundRect.y==help.y&&ui.LanPauseSoundRect.size==sound.size&&ui.LanPauseHomeRect.y>home.y&&ui.LanPauseSoundRect.y>sound.y,"LAN pause fills the missing Help space with lower home and volume controls at "+Screen.width+"x"+Screen.height);
            Vector3 saved=game.Player.position;Vector3 origin=game.World.Find("FrogPos").position;
            foreach(int distance in new[]{100,300,650,1105,1500})
            {
                game.Player.position=new Vector3(origin.x+distance,saved.y,origin.z);
                Check(game.LAN.DistanceScore==GameRules.DistanceScoreUnits(distance),"LAN live distance display matches normal points at "+distance+" native units");
            }
            game.Player.position=saved;
        }
        static void RelocationChecks()
        {
            var saved=UnityEngine.Random.state;var quadrants=new HashSet<int>();
            try
            {
                UnityEngine.Random.InitState(314159);
                for(int i=0;i<32;i++)
                {
                    var current=game.World.GetComponentsInChildren<LanRingView>().First(r=>r.State.id==hitRingId);
                    Call(game.LAN,"SpawnLanRing",hitRingId,current.State.generation+1);
                    var moved=game.World.GetComponentsInChildren<LanRingView>().First(r=>r.State.id==hitRingId);
                    Vector3 offset=moved.State.position-game.Actor.transform.position;
                    quadrants.Add(Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(offset.x,offset.z)*Mathf.Rad2Deg,360)/90));
                }
                Check(quadrants.Count==4,"Repeated relocations of the same ring reach all four quadrants, independently of its player or initial sector");
            }
            finally{UnityEngine.Random.state=saved;}
        }
        static void CheckMenuLayout()
        {
            Check(Mathf.Abs(ui.MenuSkinsRect.center.x-ui.MenuDownloadRect.center.x)<.01f&&ui.MenuSkinsRect.y>ui.MenuDownloadRect.yMax,"Skins sits directly below Download at "+Screen.width+"x"+Screen.height);
            Check(Mathf.Abs(ui.MenuMultiplayerRect.center.x-ui.MenuSettingsRect.center.x)<.01f&&ui.MenuMultiplayerRect.y>ui.MenuSettingsRect.yMax,"LAN sits directly below Settings at "+Screen.width+"x"+Screen.height);
            Check(Mathf.Abs(ui.MenuMultiplayerRect.y-ui.MenuSkinsRect.y)<.01f&&!ui.MenuMultiplayerRect.Overlaps(ui.MenuSkinsRect)&&Mathf.Max(ui.MenuMultiplayerRect.yMax,ui.MenuSkinsRect.yMax)<=Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea)-16+.01f,"Lower menu row stays aligned and on-screen at "+Screen.width+"x"+Screen.height);
            if(ui.ArtworkFamily=="ipad2")
            {
                var native=(Rect)Call(ui,"R","MainScreen$$GetSettingRect",false);
                Check(ui.MenuSettingsRect==native,"Tablet keeps the existing button positions because both rows fit");
            }
        }
        static Vector2 RosterCenter(){Rect board=ui.LanBoardRect;return new Vector2(board.center.x,board.y+160);}
        static Vector2 ResultsCenter(){Rect board=ui.LanBoardRect;return new Vector2(board.center.x,board.y+250);}
        static void Drag(Vector2 point)
        {
            var method=typeof(SkinMenuValidation).GetMethod("Pointer",BindingFlags.Static|BindingFlags.NonPublic);
            foreach(var type in new[]{EventType.MouseDown,EventType.MouseDrag,EventType.MouseUp})method.Invoke(null,new object[]{type,point+(type==EventType.MouseDown?Vector2.zero:new Vector2(0,-400)),Vector2.zero});
        }
        static Vector2 ProfileChoice(int id)
        {
            Rect viewport=(Rect)typeof(CandyFlickUI).GetProperty("LanProfileGridRect",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            Vector2 scroll=(Vector2)typeof(CandyFlickUI).GetField("lanProfileScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            return viewport.position+((Rect)Call(ui,"LanProfileChoiceRect",id)).center-scroll;
        }
        static void SetLanguage(int language)
        {
            var manager=(CandyFlick.Original26.TextManager26)typeof(CandyFlickUI).GetField("text",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            manager.ChangeLanguage(language);Call(ui,"ApplyLanguageFont");
        }
        static void ProfileLayout()
        {
            Rect footer=ui.LanFooterRect,back=(Rect)typeof(CandyFlickUI).GetMethod("R",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ui,new object[]{"SettingScreen$$GetBackBtnRect",true}),skins=ui.LanSkinsButtonRect;
            Check(footer.xMin>back.xMax&&footer.xMax<skins.xMin&&Mathf.Abs(footer.center.x-(back.xMax+skins.xMin)/2)<.01f,"LAN footer is centered in the width between Back and Skins at "+Screen.width+"x"+Screen.height);
            Check(skins.center.x>320&&Mathf.Abs(skins.center.y-back.center.y)<.01f&&skins.xMax<=640&&skins.yMax<=Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea)&&!skins.Overlaps(ui.LanBoardRect),"LAN skins button fits bottom right opposite Back at "+Screen.width+"x"+Screen.height);
            Check(ui.LanProfileButtonRect.xMax<ui.LanNameRect.x&&!ui.LanProfileButtonRect.Overlaps(ui.LanNameRect),"Profile picture fits left of the name textbox at "+Screen.width+"x"+Screen.height);
            Check(ui.LanProfilePickerRect.y>=0&&ui.LanProfilePickerRect.yMax<=Screen.safeArea.height/CandyFlickUI.CanvasScale(Screen.safeArea),"Profile picker fits the usable screen at "+Screen.width+"x"+Screen.height);
        }
        static void Rules()
        {
            checks.AddRange(PortLocalizationValidation.Check(game));
            Frames();
            var atlas=Resources.Load<Texture2D>(LanProfiles.Resource);
            Check(atlas&&atlas.width==1024&&atlas.height==1024&&LanProfiles.Count==60,"All 60 bundled profile pictures load from one 1024-square atlas");
            Check(LanProfiles.Clean(-1)==LanProfiles.Default&&LanProfiles.Clean(99999)==LanProfiles.Default&&LanProfiles.Clean(59)==59,"Profile ID validation rejects invalid preferences or packets while preserving the final picture");
            Check(LanRules.CleanName("")=="Player"&&LanRules.CleanName("  ")=="Player","Blank names normalize to Player");
            Check(LanRules.CleanName("abcdefghijklmnop")=="abcdefghijkl"&&LanRules.CleanName("<b>Joe</b>").Length<=12,"Names are bounded and cannot inject rich text");
            var match=new LanMatch();match.Players.Add(new LanPlayer{id=0});Check(!match.Start(),"Rules reject a one-player start");
            match.Players.Add(new LanPlayer{id=1});Check(match.Start(),"Two-player matches are supported");match.Step(10);Check(match.Remaining==60,"Card finding never advances active time");
            foreach(var p in match.Players)p.tracked=true;match.Step(1);Check(match.Remaining==59,"Tracked match advances active time");
            Check(match.AcceptThrow(0,match.Epoch,1)&&!match.AcceptThrow(0,match.Epoch,1),"Duplicate throw sequences are rejected");
            Check(!match.AcceptThrow(0,match.Epoch,2),"Host enforces the shared throw cooldown");
            match.Players[1].tracked=false;match.Step(4);Check(match.Remaining==59&&!match.AcceptThrow(1,match.Epoch,1),"Tracking loss pauses time and rejects throws");
            match.Players[1].tracked=true;match.Players[1].paused=true;match.Step(1);Check(match.Remaining==59,"Manual pause also pauses the whole match");
            match.Players[1].paused=false;match.Step(1);match.Feed(0,300,0);match.Feed(0,300,0);match.Feed(1,1105,15);
            Check(match.Players[0].fed==2&&match.Players[0].score==10&&match.Players[1].fed==1&&match.Players[1].score==65&&LanRules.Winners(match.Players).SequenceEqual(new[]{1}),"Normal distance and ring points determine the winner even against a higher fed count");
            for(int distance=0;distance<=1500;distance+=25)
            {
                var solo=new GameSession();var before=match.Players[0].score;match.Feed(0,distance,15);
                if(match.Players[0].score-before!=solo.RecordUnits(true,distance,15))throw new Exception("LAN score differs from normal scoring at "+distance);
            }
            Check(true,"LAN scores match normal non-super scoring across minimum, intermediate and maximum distances");
            match.Step(60);Check(match.Phase==LanPhase.Finished&&match.Remaining==0,"Active-play match ends exactly at zero");
            match.Players[0].score=match.Players[1].score;Check(LanRules.Winners(match.Players).Length==2,"Equal point totals produce a draw regardless of fed counts");
            Check(match.Start()==false,"Finished matches require returning to the lobby");match.Lobby();Check(match.Start()&&match.Players.All(p=>p.score==0&&p.ringScore==0&&p.baseScore==0&&p.fed==0),"Rematches reset all score components");
            var importer=(AudioImporter)AssetImporter.GetAtPath("Assets/Resources/MultiplayerMusic/parkour_music_track_1.ogg");
            Check(importer&&importer.GetOverrideSampleSettings("Android").compressionFormat==AudioCompressionFormat.MP3,"Dedicated LAN music is imported as Android MP3");
        }
        static IEnumerator CapturePlayingFrame()
        {
            if(!CaptureEnabled)yield break;
            EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView")).Focus();
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Folder+"/playing-phone.png",texture.EncodeToPNG());UnityEngine.Object.Destroy(texture);
        }
        static void Frames()
        {
            var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0);listener.Start();
            using(var client=new System.Net.Sockets.TcpClient())
            {
                client.NoDelay=true;client.Connect(System.Net.IPAddress.Loopback,((System.Net.IPEndPoint)listener.LocalEndpoint).Port);
                using(var peer=new LanPeer(listener.AcceptSocket(),Time.realtimeSinceStartupAsDouble))
                {
                    var bytes=System.Text.Encoding.UTF8.GetBytes("{\"kind\":\"ping\",\"version\":1}");var frame=new byte[bytes.Length+4];
                    Buffer.BlockCopy(BitConverter.GetBytes(bytes.Length),0,frame,0,4);Buffer.BlockCopy(bytes,0,frame,4,bytes.Length);
                    var stream=client.GetStream();stream.Write(frame,0,2);peer.Pump(Time.realtimeSinceStartupAsDouble);Check(peer.Messages.Count==0,"Partial TCP frame headers wait for completion");
                    stream.Write(frame,2,5);peer.Pump(Time.realtimeSinceStartupAsDouble);Check(peer.Messages.Count==0,"Partial TCP payloads wait for completion");
                    stream.Write(frame,7,frame.Length-7);peer.Pump(Time.realtimeSinceStartupAsDouble);Check(peer.Messages.Count==1&&peer.Messages.Dequeue().Contains("ping"),"Fragmented TCP frames reconstruct one complete message");
                    stream.Write(frame,0,frame.Length);stream.Write(frame,0,frame.Length);peer.Pump(Time.realtimeSinceStartupAsDouble);Check(peer.Messages.Count==2,"Coalesced TCP frames remain separate messages");
                    stream.Write(new byte[]{255,255,255,127},0,4);peer.Pump(Time.realtimeSinceStartupAsDouble);Check(peer.Closed,"Oversized network frames close the connection");
                }
            }
            listener.Stop();
        }
        static void PumpBots(){foreach(var bot in bots)bot.Pump();}
        static void PumpFakeHost()
        {
            if(fakeHost==null)return;double now=Time.realtimeSinceStartupAsDouble;fakeHost.Pump(now);
            foreach(var peer in fakeHost.Peers)
            {
                while(peer.Messages.Count>0)
                {
                    var m=JsonUtility.FromJson<LanMessage>(peer.Messages.Dequeue());
                    if(m.kind=="profile")fakeProfileAvatar=m.avatar;
                    if(m.kind=="hello"){fakeHelloAvatar=m.avatar;fakePeer=peer;peer.Id=1;peer.Send(new LanMessage{kind="welcome",playerId=1,state=new LanSnapshot{players=new[]{new LanPlayer{id=0,name="Host"},new LanPlayer{id=1,name="Client"}}}});}
                }
                peer.Pump(now);
            }
        }
        sealed class Bot
        {
            public readonly LanTransport net=new LanTransport();readonly string name;bool hello;float nextPose,nextPing;int seq;
            public int Id=-1,Avatar=LanProfiles.Default,CandyChoice;public bool Tracked,Rejected,GroundSupported=true,Calibrated;public int? GenerationOverride;public LanSnapshot State;
            public Bot(string name){this.name=name;}
            public void Pump()
            {
                double now=Time.realtimeSinceStartupAsDouble;net.Pump(now);var peer=net.Server;if(peer==null)return;
                if(!hello){hello=true;peer.Send(new LanMessage{kind="hello",name=name,avatar=Avatar,candyChoice=CandyChoice,groundSupported=GroundSupported});}
                while(peer.Messages.Count>0){var m=JsonUtility.FromJson<LanMessage>(peer.Messages.Dequeue());if(m.kind=="welcome")Id=m.playerId;if(m.kind=="reject")Rejected=true;if(m.state!=null){if(m.state.groundMode&&(State==null||(m.state.epoch!=State.epoch&&!m.state.ground.reusable)||m.state.ground.generation!=State.ground.generation))Calibrated=false;State=m.state;}}
                if(Id>0&&Time.unscaledTime>=nextPing){nextPing=Time.unscaledTime+1;peer.Send(new LanMessage{kind="ping"});}
                if(Id>0&&State!=null&&State.phase!=LanPhase.Lobby&&Time.unscaledTime>=nextPose){nextPose=Time.unscaledTime+.03f;peer.Send(Pose("pose"));}
                peer.Pump(now);
            }
            LanMessage Pose(string kind)=>new LanMessage{kind=kind,epoch=State.epoch,calibrated=Calibrated,groundGeneration=GenerationOverride??State.ground.generation,tracked=Tracked,paused=false,cameraPosition=kind=="throw"?new Vector3(1200+Id*300,1700,1500):new Vector3((Id-2)*200,500,-600),cameraRotation=Quaternion.Euler(0,90,0),candyPosition=kind=="throw"?new Vector3(1200+Id*300,1500,1300):new Vector3((Id-2)*200,300,-400),candyRotation=Quaternion.identity};
            public void ChangeProfile(int id){Avatar=id;net.Server.Send(new LanMessage{kind="profile",avatar=id});net.Server.Pump(Time.realtimeSinceStartupAsDouble);}
            public void SetGroundSupport(bool supported){GroundSupported=supported;net.Server.Send(new LanMessage{kind="capability",groundSupported=supported});net.Server.Pump(Time.realtimeSinceStartupAsDouble);}
            public void ReportGroundLost(){net.Server.Send(new LanMessage{kind="groundLost",epoch=State.epoch,groundGeneration=State.ground.generation});net.Server.Pump(Time.realtimeSinceStartupAsDouble);}
            public void Fire(){var m=Pose("throw");m.strength=400;m.sequence=++seq;net.Server.Send(m);net.Server.Pump(Time.realtimeSinceStartupAsDouble);}
        }
        static void OnLog(string message,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            if(finishing)return;finishing=true;SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            foreach(var bot in bots)bot.net.Dispose();bots.Clear();fakeHost?.Dispose();if(game){game.Actor.enabled=true;game.LAN.CloseRoom();game.LAN.EditorTrackingOverride=null;game.LAN.EditorCalibrationOverride=null;game.LAN.EditorMarkerWidthOverride=null;game.ReturnToMenu();}
            foreach(string pref in prefs){if(SessionState.GetBool(Key+pref+"Had",false)){if(pref==LanMultiplayer.NamePreference)PlayerPrefs.SetString(pref,SessionState.GetString(Key+pref,""));else PlayerPrefs.SetInt(pref,SessionState.GetInt(Key+pref,0));}else PlayerPrefs.DeleteKey(pref);}PlayerPrefs.Save();
            if(SessionState.GetBool(Key+"SaveHad",false))PlayerPrefs.SetString(LocalAchievements.SaveKey,SessionState.GetString(Key+"Save",""));else PlayerPrefs.DeleteKey(LocalAchievements.SaveKey);
            if(SessionState.GetBool(Key+"LensHad",false))PlayerPrefs.SetString(NonARStage.LensPreference,SessionState.GetString(Key+"Lens",""));else PlayerPrefs.DeleteKey(NonARStage.LensPreference);PlayerPrefs.Save();
            AudioListener.volume=SessionState.GetFloat(Key+"Volume",1);AudioListener.pause=SessionState.GetBool(Key+"Audio",false);
            string previous=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(previous)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc,limit;public string[] checks;}
    }
}
