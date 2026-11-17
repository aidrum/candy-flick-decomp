using System;
using System.Linq;
using CandyFlick.Multiplayer;
using UnityEngine;

namespace CandyFlick
{
    [DefaultExecutionOrder(-80)]
    public sealed partial class LanMultiplayer : MonoBehaviour
    {
        public const string NamePreference="PortLanPlayerName",ProfilePreference="PortLanProfile";
        public const string TrackResource="MultiplayerMusic/parkour_music_track_1";
        CandyFlickGame game;LanTransport network;
        public readonly LanMatch Match=new LanMatch();
        LanSnapshot remoteState=new LanSnapshot();
        bool activeMatch,helloSent,appPaused,closing;
        float nextPose,nextSnapshot,nextHeartbeat,nextLocalThrow;int shotSequence;
        NommieSkin savedSkin;LaunchMode savedControls;LanControls roomControls;string hostAddress="";
        public LanControls Controls=>IsHost?roomControls:LanRules.CleanControls(remoteState.controls);
        public void CycleRoomControls(){if(!IsHost||Phase!=LanPhase.Lobby)return;roomControls=(LanControls)(((int)roomControls+1)%3);nextSnapshot=0;}
        public void SetControlPreference(LaunchMode mode)
        {
            if(InMatch)return;
            if(IsHost&&Match.Players.Count>0){Match.Players[0].controlMode=(int)mode;nextSnapshot=0;}
            else network?.Server?.Send(new LanMessage{kind="controls",controlMode=(int)mode});
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        object multicastLock;
#endif
        public string PlayerName {get;private set;}="Player";
        public int ProfilePicture {get;private set;}=LanProfiles.Default;
        public string Notice {get;private set;}="";
        public int LocalId {get;private set;}=-1;
        public bool InMatch=>activeMatch;
        public bool InRoom=>IsHost||LocalId>=0;
        public bool IsHost=>network!=null&&network.IsHost;
        public bool Connecting=>network!=null&&network.Connecting;
        public LanRoom[] Rooms=>network==null?Array.Empty<LanRoom>():network.Rooms.ToArray();
        public LanPlayer[] Players=>IsHost?Match.Players.ToArray():remoteState.players??Array.Empty<LanPlayer>();
        public LanPhase Phase=>IsHost?Match.Phase:remoteState.phase;
        public float Remaining=>IsHost?Match.Remaining:remoteState.remaining;
        public int Epoch=>IsHost?Match.Epoch:remoteState.epoch;
        public bool LocalTracking=>(EditorTrackingOverride??(game.ar&&game.ar.Tracking&&!appPaused))&&(!InMatch||!GroundMode||(Ground.placed&&LocalCalibrated));
        public bool LocallyPaused=>appPaused||(activeMatch&&game.ScreenState!=GameScreen.Playing&&game.ScreenState!=GameScreen.Results);
        public bool Playing=>InMatch&&Phase==LanPhase.Playing&&LocalTracking&&!LocallyPaused;
        public bool Ready=>Playing&&Time.unscaledTime>=nextLocalThrow&&Players.Any(p=>p.id==LocalId&&60-Remaining+.0001f>=p.nextThrow);
        public bool CanStart=>IsHost&&Match.Phase==LanPhase.Lobby&&Match.Players.Count>=2&&(!GroundMode||(game.GroundSupport.Supported&&Match.Players.All(p=>p.groundSupported)&&HostMarkerWidth>0));
        public string HostAddress=>hostAddress;
        public int LocalCandySkin=>CandySkinSettings.CleanSkin(Players.FirstOrDefault(p=>p.id==LocalId)?.readySkin??0);
        public int DistanceScore=>GameRules.DistanceScoreUnits(game.ScoreDistance);
#if UNITY_EDITOR
        // Explicit tracking/camera seam for loopback editor validation. Never compiled into a phone player.
        public bool? EditorTrackingOverride {get;set;}
#else
        bool? EditorTrackingOverride=>null;
#endif
        public bool SkipEditorCamera=>EditorTrackingOverride.HasValue;
        public void Initialize(CandyFlickGame owner){game=owner;PlayerName=LanRules.CleanName(PlayerPrefs.GetString(NamePreference,"Player"));ProfilePicture=LanProfiles.Clean(PlayerPrefs.GetInt(ProfilePreference,LanProfiles.Default));}
        public void SetName(string name)
        {
            PlayerName=LanRules.CleanName(name);PlayerPrefs.SetString(NamePreference,PlayerName);PlayerPrefs.Save();
            if(IsHost&&Match.Players.Count>0)Match.Players[0].name=PlayerName;
            else if(network?.Server!=null)network.Server.Send(new LanMessage{kind="name",name=PlayerName});
        }
        public void SetProfilePicture(int id)
        {
            if(InMatch)return;
            ProfilePicture=LanProfiles.Clean(id);PlayerPrefs.SetInt(ProfilePreference,ProfilePicture);PlayerPrefs.Save();
            if(IsHost&&Match.Players.Count>0){Match.Players[0].avatar=ProfilePicture;nextSnapshot=0;}
            else if(network?.Server!=null)network.Server.Send(new LanMessage{kind="profile",avatar=ProfilePicture});
        }
        public void SetCandyChoice(int choice)
        {
            if(InMatch)return;choice=CandySkinSettings.CleanChoice(choice);
            if(IsHost&&Match.Players.Count>0){Match.Players[0].candyChoice=choice;nextSnapshot=0;}
            else if(network?.Server!=null)network.Server.Send(new LanMessage{kind="candy",candyChoice=choice});
        }
        public void OpenBrowser()
        {
            CloseRoom();network=new LanTransport();network.Browse();AcquireMulticast();Notice=network.Error;game.Show(GameScreen.LanLobby);
        }
        public void HostRoom()
        {
            if(network==null)OpenBrowser();if(InRoom||Connecting)return;
            if(!network.Host()){Notice=network.Error;return;}
            hostAddress=LanTransport.LocalAddress()+":"+network.Port;LocalId=0;Match.Players.Clear();Match.Players.Add(new LanPlayer{id=0,name=PlayerName,avatar=ProfilePicture,candyChoice=game.CandySkins.Selected,controlMode=(int)game.ControlMode,groundSupported=game.GroundSupport.Supported,groundReason=game.GroundSupport.Reason});Match.Lobby();roomControls=LanControls.PlayersChoice;roomGround=game.GroundARModeEnabled;Notice="Waiting for players...";
        }
        public void JoinRoom(string address)
        {
            if(network==null)OpenBrowser();if(InRoom||Connecting)return;
            if(network.Join(address,Time.realtimeSinceStartupAsDouble)){helloSent=false;Notice="Connecting...";}else Notice=network.Error;
        }
        public bool StartMatch()
        {
            bool reuse=CanReuseGround;
            if(!CanStart||!Match.Start(reuse))return false;
            BeginLocalMatch(reuse);network.Broadcast(new LanMessage{kind="begin",state=BuildSnapshot()});Flush();return true;
        }
        void BeginLocalMatch(bool reuseGround=false)
        {
            activeMatch=true;savedSkin=game.Skins.Selected;savedControls=game.ControlMode;game.ApplySessionControlMode(LanRules.EffectiveControl(Controls,(int)savedControls));nextLocalThrow=nextPose=nextSnapshot=nextHeartbeat=0;shotSequence=0;
            calibrationNotice="";
            if(IsHost&&!reuseGround)InvalidateGroundCalibration();
            game.StartGame(true);if(IsHost&&reuseGround)game.Actor.transform.SetPositionAndRotation(retainedNomPosition,retainedNomRotation);InitializeLanWorld();Notice=GroundMode?"All clients scan the host image once before ground placement.":"Find the card. The match starts when everyone is ready.";
        }
        void Update()
        {
            if(network==null)return;SyncGroundCapability();TickRoomCalibration();TickCalibrationSound();double now=Time.realtimeSinceStartupAsDouble;network.Pump(now);
            if(IsHost)
            {
                foreach(var peer in network.Peers.ToArray())
                {
                    if(now-peer.LastSeen>10||(peer.Id<0&&now-peer.OpenedAt>5))peer.Dispose();
                    while(!peer.Closed&&peer.Messages.Count>0)ReceiveHost(peer,peer.Messages.Dequeue());
                    if(peer.Closed)
                    {
                        network.Peers.Remove(peer);var player=Match.Players.Find(p=>p.id==peer.Id);
                        if(player!=null){Match.Players.Remove(player);if(activeMatch)ReturnToRoom(player.name+" disconnected. Match cancelled.");}
                    }
                }
                network.Advertise(PlayerName,Match.Players.Count,Match.Phase==LanPhase.Lobby,now);
            }
            else if(network.Server!=null)
            {
                var server=network.Server;
                if(!helloSent){server.Send(new LanMessage{kind="hello",name=PlayerName,avatar=ProfilePicture,candyChoice=game.CandySkins.Selected,controlMode=(int)game.ControlMode,groundSupported=game.GroundSupport.Supported,groundReason=game.GroundSupport.Reason});helloSent=true;}
                while(server.Messages.Count>0)ReceiveClient(server.Messages.Dequeue());
                if(network.Server!=server)return;
                if(server.Closed||now-server.LastSeen>10){Disconnect("The host disconnected.");return;}
            }
            if(!string.IsNullOrEmpty(network.Error)&&!InRoom)Notice=network.Error;
            if(activeMatch)
            {
                if(Time.unscaledTime>=nextPose)
                {
                    nextPose=Time.unscaledTime+.05f;var message=LocalPose("pose");
                    if(IsHost)UpdatePlayerPose(Match.Players[0],message);else network.Server?.Send(message);
                }
                if(IsHost)
                {
                    Match.RefreshGate();RefreshLanWorld();
                    if(Match.Phase==LanPhase.Finished&&game.ScreenState!=GameScreen.Results){game.Show(GameScreen.Results);Time.timeScale=0;game.Play(game.assets.win);}
                }
                else UpdateRemoteWorld();
                if(!Playing&&game.IsDragging)game.CancelCandyGesture();
            }
            if(IsHost&&Time.unscaledTime>=nextSnapshot)
            {
                nextSnapshot=Time.unscaledTime+(activeMatch?.05f:.25f);
                network.Broadcast(new LanMessage{kind="state",state=BuildSnapshot()});
            }
            if(!IsHost&&network.Server!=null&&Time.unscaledTime>=nextHeartbeat)
            {nextHeartbeat=Time.unscaledTime+1;network.Server.Send(new LanMessage{kind="ping"});}
            Flush();
        }
        void FixedUpdate()
        {
            if(activeMatch&&IsHost&&Playing)Match.Step(Time.fixedDeltaTime);
        }
        void LateUpdate(){if(activeMatch&&IsHost)ResolveLanContacts();if(activeMatch){RefreshRemoteSlingshots();RefreshLanAudio();}}
        void Flush(){if(network==null)return;double now=Time.realtimeSinceStartupAsDouble;foreach(var p in network.Peers)p.Pump(now);network.Server?.Pump(now);}
        static LanMessage Decode(string text){try{return JsonUtility.FromJson<LanMessage>(text);}catch{return null;}}
        void ReceiveHost(LanPeer peer,string text)
        {
            var m=Decode(text);if(m==null||m.version!=LanRules.Version){Reject(peer,"Different game version.");return;}
            if(peer.Id<0)
            {
                if(m.kind!="hello"){Reject(peer,"Join the room first.");return;}
                if(Match.Phase!=LanPhase.Lobby||Match.Players.Count>=LanRules.MaxPlayers){Reject(peer,Match.Phase==LanPhase.Lobby?"This room is full.":"This match has already started.");return;}
                if(Ground.reusable)InvalidateGroundCalibration();
                int id=Enumerable.Range(1,LanRules.MaxPlayers-1).First(i=>Match.Players.All(p=>p.id!=i));peer.Id=id;
                Match.Players.Add(new LanPlayer{id=id,name=LanRules.CleanName(m.name),avatar=LanProfiles.Clean(m.avatar),candyChoice=CandySkinSettings.CleanChoice(m.candyChoice),controlMode=(int)LanRules.CleanControlMode(m.controlMode),groundSupported=m.groundSupported,groundReason=CleanGroundReason(m.groundReason)});
                peer.Send(new LanMessage{kind="welcome",playerId=id,state=BuildSnapshot()});Notice="Players can join until the host starts.";return;
            }
            var player=Match.Players.Find(p=>p.id==peer.Id);if(player==null){peer.Dispose();return;}
            if(m.kind=="name"&&Match.Phase==LanPhase.Lobby)player.name=LanRules.CleanName(m.name);
            else if(m.kind=="profile"&&Match.Phase==LanPhase.Lobby){player.avatar=LanProfiles.Clean(m.avatar);nextSnapshot=0;}
            else if(m.kind=="candy"&&Match.Phase==LanPhase.Lobby){player.candyChoice=CandySkinSettings.CleanChoice(m.candyChoice);nextSnapshot=0;}
            else if(m.kind=="controls"&&Match.Phase==LanPhase.Lobby){player.controlMode=(int)LanRules.CleanControlMode(m.controlMode);nextSnapshot=0;}
            else if(m.kind=="capability"){player.groundSupported=m.groundSupported;player.groundReason=CleanGroundReason(m.groundReason);}
            else if(m.kind=="calibration"&&!activeMatch&&Ground.reusable&&m.epoch==Epoch&&m.groundGeneration==Ground.generation)player.calibrated=m.calibrated;
            else if(m.kind=="groundLost"&&(activeMatch||Ground.reusable)&&m.epoch==Epoch&&m.groundGeneration==Ground.generation)ResetGroundCalibration();
            else if(m.kind=="pose"&&activeMatch&&m.epoch==Match.Epoch)UpdatePlayerPose(player,m);
            else if(m.kind=="throw"&&activeMatch&&m.epoch==Match.Epoch)AcceptThrow(player,m);
            else if(m.kind=="leave")peer.Dispose();
        }
        void Reject(LanPeer peer,string reason){peer.Send(new LanMessage{kind="reject",text=reason});peer.Pump(Time.realtimeSinceStartupAsDouble);peer.Dispose();}
        void ReceiveClient(string text)
        {
            var m=Decode(text);if(m==null||m.version!=LanRules.Version){Disconnect("Different game version.");return;}
            if(m.kind=="reject"||m.kind=="closed"){Disconnect(m.text??"Room closed.");return;}
            if(m.kind=="welcome")LocalId=m.playerId;
            if(m.kind=="sound"){ReceiveLanSound(m);return;}
            if(m.state==null)return;
            if(m.kind=="begin")
            {
                remoteState=m.state;if(!activeMatch)BeginLocalMatch(remoteState.groundMode&&remoteState.ground.reusable);game.Skins.ApplyForSession((NommieSkin)Mathf.Clamp(remoteState.skin,0,7));ApplyRemoteSnapshot();
            }
            else if(m.kind=="room")
            {
                StopLocalMatch(m.state.groundMode&&m.state.ground.reusable);remoteState=m.state;Notice=m.text??"Waiting for the host.";game.Show(GameScreen.LanLobby);
            }
            else if(m.kind=="state"||m.kind=="welcome")
            {
                if(activeMatch&&m.state.epoch!=remoteState.epoch)return;remoteState=m.state;
                if(activeMatch){ApplyRemoteSnapshot();if(remoteState.phase==LanPhase.Finished&&game.ScreenState!=GameScreen.Results){game.Show(GameScreen.Results);Time.timeScale=0;game.Play(game.assets.win);}}
                else if(LocalId>=0)Notice="Waiting for the host to start.";
            }
        }
        LanMessage LocalPose(string kind)=>new LanMessage{kind=kind,epoch=Epoch,tracked=LocalTracking,paused=LocallyPaused,calibrated=LocalCalibrated,groundGeneration=Ground.generation,
            stretching=game.LanElasticMoving,controlMode=(int)(activeMatch?savedControls:game.ControlMode),cameraPosition=game.Player.position,cameraRotation=game.Player.rotation,candyPosition=game.CandyGhost.position,candyRotation=game.CandyGhost.rotation};
        void UpdatePlayerPose(LanPlayer player,LanMessage m)
        {
            player.calibrated=m.calibrated&&m.groundGeneration==Ground.generation;
            player.tracked=m.tracked&&(!GroundMode||(Ground.placed&&player.calibrated&&player.groundSupported&&m.groundGeneration==Ground.generation));player.paused=m.paused;
            if(!LanRules.ValidPose(m)){player.hasPose=false;return;}
            player.controlMode=(int)LanRules.CleanControlMode(m.controlMode);
            player.stretching=m.stretching;player.hasPose=true;player.cameraPosition=m.cameraPosition;player.cameraRotation=m.cameraRotation.normalized;
            player.candyPosition=m.candyPosition;player.candyRotation=m.candyRotation.normalized;
        }
        public void Throw(float strength,float ratio,float angle)
        {
            if(!Ready)return;var m=LocalPose("throw");m.strength=strength;m.ratio=ratio;m.angle=angle;m.sequence=++shotSequence;
            nextLocalThrow=Time.unscaledTime+LanRules.ThrowInterval;
            if(IsHost)AcceptThrow(Match.Players[0],m);else{network.Server?.Send(m);Flush();}
        }
        void AcceptThrow(LanPlayer player,LanMessage m)
        {
            if(!LanRules.ValidPose(m)||!float.IsFinite(m.strength)||!float.IsFinite(m.ratio)||!float.IsFinite(m.angle)||m.strength<0||m.ratio<0)return;
            UpdatePlayerPose(player,m);
            if(!Match.AcceptThrow(player.id,m.epoch,m.sequence))return;
            SpawnHostCandy(player,Mathf.Clamp(m.strength,0,10000),Mathf.Clamp01(m.ratio),Mathf.Clamp(m.angle,-89,89));
            player.stretching=false;PublishLanSound("launch",player.id,UnityEngine.Random.Range(0,Mathf.Max(1,game.assets.swishes.Length-1)));
        }
        public void ReturnToRoom(string reason="Waiting for the host.")
        {
            if(!IsHost)return;bool keep=CanReuseGround;
            if(keep){retainedNomPosition=game.Actor.transform.position;retainedNomRotation=game.Actor.transform.rotation;}
            StopLocalMatch(keep);if(!keep&&Ground.reusable)InvalidateGroundCalibration();Match.Lobby(keep);nextPose=nextSnapshot=0;Notice=reason;game.Show(GameScreen.LanLobby);
            network.Broadcast(new LanMessage{kind="room",text=reason,state=BuildSnapshot()});Flush();
        }
        void StopLocalMatch(bool keepGround=false)
        {
            if(!activeMatch)return;activeMatch=false;ClearLanWorld();
            if(!game||!game.World||!game.Actor||!game.Skins)return;
            game.ReturnToMenu(keepGround);game.ApplySessionControlMode(savedControls);game.Skins.ApplyForSession(savedSkin);game.Actor.ResetOriginal();game.GameplayMusic.clip=null;
        }
        public void CloseRoom()
        {
            if(closing)return;closing=true;
            if(IsHost){network.Broadcast(new LanMessage{kind="closed",text="The host closed the room."});Flush();}
            else{network?.Server?.Send(new LanMessage{kind="leave"});Flush();}
            StopLocalMatch();if(game&&game.ar&&game.ar.RetainingLanGround)game.ar.End();hostGround=new LanGroundState();calibrationSoundGeneration=-1;network?.Dispose();network=null;LocalId=-1;helloSent=false;Match.Players.Clear();Match.Lobby();remoteState=new LanSnapshot();lastGroundCapability=-1;ReleaseMulticast();closing=false;
        }
        public void LeaveRoom(){CloseRoom();game.ReturnToMenu();}
        void Disconnect(string reason){CloseRoom();network=new LanTransport();network.Browse();AcquireMulticast();Notice=reason;game.Show(GameScreen.LanLobby);}
        public string WaitingText
        {
            get
            {
                if(GroundMode&&!LocalTracking)return game.ar.Status;
                if(!LocalTracking)return "Point your camera at the image card.";
                var waiting=Players.Where(p=>!p.tracked||p.paused).Select(p=>p.name).ToArray();
                return waiting.Length>0?"Waiting for "+string.Join(", ",waiting)+"...":"Starting match...";
            }
        }
        void OnApplicationPause(bool paused)
        {
            appPaused=paused;if(!activeMatch)return;nextPose=0;
            if(IsHost){UpdatePlayerPose(Match.Players[0],LocalPose("pose"));Match.RefreshGate();network.Broadcast(new LanMessage{kind="state",state=BuildSnapshot()});}
            else network?.Server?.Send(LocalPose("pose"));Flush();
        }
        void OnDestroy(){CloseRoom();}
        void AcquireMulticast()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(multicastLock!=null)return;
            try{using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var context=activity.Call<AndroidJavaObject>("getApplicationContext"))using(var wifi=context.Call<AndroidJavaObject>("getSystemService","wifi"))
            {var handle=wifi.Call<AndroidJavaObject>("createMulticastLock","CandyFlickLAN");handle.Call("setReferenceCounted",false);handle.Call("acquire");multicastLock=handle;}}catch(Exception){Notice="If rooms do not appear, join using the host address.";}
#endif
        }
        void ReleaseMulticast()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if(multicastLock is AndroidJavaObject handle){try{handle.Call("release");}catch{}handle.Dispose();multicastLock=null;}
#endif
        }
    }
}
