using System;
using System.Linq;
using CandyFlick.Multiplayer;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class LanMultiplayer
    {
        int calibrationSoundGeneration=-1;AudioClip calibrationSound;
#if UNITY_EDITOR
        public int CalibrationCueCount {get;private set;}
#endif
        void TickCalibrationSound()
        {
            if(!activeMatch||IsHost||!GroundMode||!LocalCalibrated||calibrationSoundGeneration==Ground.generation)return;
            calibrationSoundGeneration=Ground.generation;
            if(!calibrationSound)calibrationSound=Resources.Load<AudioClip>("LanAudio/calibrated");
            game.Play(calibrationSound);
#if UNITY_EDITOR
            CalibrationCueCount++;
#endif
        }
        Vector3 retainedNomPosition;Quaternion retainedNomRotation=Quaternion.identity;
        bool roomGround;LanGroundState hostGround=new LanGroundState();int lastGroundCapability=-1;
        string calibrationNotice="";float calibrationNoticeUntil;
        public string CalibrationNotice=>Time.unscaledTime<calibrationNoticeUntil?calibrationNotice:"";
        static readonly LanGroundState EmptyGround=new LanGroundState();
        public const string CalibrationTarget="NewTarget";
        public bool GroundMode=>IsHost?roomGround:remoteState.groundMode;
        public LanGroundState Ground=>IsHost?hostGround:remoteState.ground??EmptyGround;
#if UNITY_EDITOR
        public bool? EditorCalibrationOverride {get;set;}
        public float? EditorMarkerWidthOverride {get;set;}
#endif
        public bool LocalCalibrated
        {
            get
            {
#if UNITY_EDITOR
                if(EditorCalibrationOverride.HasValue)return EditorCalibrationOverride.Value;
#endif
                return game.ar&&game.ar.LanCalibrationReady;
            }
        }
        public float HostMarkerWidth
        {
            get
            {
#if UNITY_EDITOR
                if(EditorMarkerWidthOverride.HasValue)return EditorMarkerWidthOverride.Value;
#endif
                return game.GetComponent<CandyFlickUI>().LanCalibrationWidthMetres;
            }
        }
        public bool CanReuseGround=>GroundMode&&Ground.reusable&&Ground.originReady&&Ground.placed&&!appPaused&&LocalCalibrated&&Players.All(p=>p.calibrated&&p.groundSupported);
        public bool AllClientsCalibrated=>Players.Where(p=>p.id!=0).All(p=>p.calibrated);
        public bool ShowHostCalibration=>GroundMode&&IsHost&&!Ground.placed&&(!Ground.originReady||!AllClientsCalibrated);
        public bool CanChooseGround=>GroundMode||game.GroundSupport.Supported;
        public string GroundWarning
        {
            get
            {
                if(!IsHost)return "";
                if(Players.Any(p=>!p.groundSupported))return "The device of one or more players in the room does not support Ground mode.";
                if(GroundMode&&HostMarkerWidth<=0)return "Screen size could not be determined. Choose Image mode.";
                return "";
            }
        }
        public void ToggleRoomGround()
        {
            if(!IsHost||Phase!=LanPhase.Lobby||(!roomGround&&!game.GroundSupport.Supported))return;
            InvalidateGroundCalibration();roomGround=!roomGround;nextSnapshot=0;
        }
        static string CleanGroundReason(string text)=>new string((text??"Ground is unavailable.").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(160).ToArray());
        void SyncGroundCapability()
        {
            var support=game.GroundSupport;
            if(IsHost&&Match.Players.Count>0){Match.Players[0].groundSupported=support.Supported;Match.Players[0].groundReason=support.Reason;}
            if(IsHost||LocalId<0||network?.Server==null)return;
            int state=(int)support.State;if(lastGroundCapability==state)return;lastGroundCapability=state;
            network.Server.Send(new LanMessage{kind="capability",groundSupported=support.Supported,groundReason=support.Reason});
        }
        public static bool ValidGroundPlacement(Vector3 offset,Quaternion rotation)=>LanRules.Finite(offset)&&offset.magnitude<=5&&LanRules.Finite(rotation);
        public bool SetHostOriginReady(int generation)
        {
            if(!activeMatch||!IsHost||!GroundMode||Ground.generation!=generation)return false;
            hostGround.originReady=true;nextSnapshot=0;return true;
        }
        public bool PublishGroundPlacement(Vector3 offset,Quaternion rotation,int generation)
        {
            if(!activeMatch||!IsHost||!GroundMode||!Ground.originReady||!AllClientsCalibrated||Ground.placed||Ground.generation!=generation||!ValidGroundPlacement(offset,rotation))return false;
            hostGround.offset=offset;hostGround.rotation=rotation.normalized;hostGround.placed=hostGround.reusable=true;hostGround.placement++;
            jumpStart=-1;nomBusyUntil=Match.Elapsed;nextJump=Match.Elapsed+4;game.Actor.ResetOriginal();SetNomAction("idle");
            foreach(var player in Match.Players)player.tracked=false;
            Match.RefreshGate();nextPose=nextSnapshot=0;Flush();return true;
        }
        public bool MoveOmNom()
        {
            if(!activeMatch||!IsHost||!GroundMode||Phase==LanPhase.Finished||!Ground.placed||!AllClientsCalibrated||!LocalCalibrated)return false;
            hostGround.placed=hostGround.reusable=false;
            foreach(var player in Match.Players)player.tracked=false;
            Match.RefreshGate();nextPose=nextSnapshot=0;game.CancelCandyGesture();Time.timeScale=0;Flush();return true;
        }
        void InvalidateGroundCalibration()
        {
            hostGround=new LanGroundState{generation=hostGround.generation+1,markerWidth=HostMarkerWidth};
            foreach(var player in Match.Players){player.tracked=false;player.calibrated=false;}
            nextPose=nextSnapshot=0;
        }
        public void ResetGroundCalibration(string reason="")
        {
            if(!IsHost||!GroundMode||(!activeMatch&&!Ground.reusable))return;
            if(!string.IsNullOrEmpty(reason)){calibrationNotice=reason;calibrationNoticeUntil=Time.unscaledTime+5;}
            InvalidateGroundCalibration();Match.RefreshGate();game.CancelCandyGesture();Flush();
        }
        public void ReportGroundTrackingLoss(int generation)
        {
            if((!activeMatch&&!Ground.reusable)||!GroundMode||generation!=Ground.generation)return;
            if(IsHost)ResetGroundCalibration();
            else{network.Server?.Send(new LanMessage{kind="groundLost",epoch=Epoch,groundGeneration=generation});Flush();}
        }
        void TickRoomCalibration()
        {
            if(activeMatch)return;
            if(game.ar.RetainingLanGround&&(!GroundMode||!Ground.reusable))game.ar.End();
            if(!GroundMode||!Ground.reusable||Time.unscaledTime<nextPose)return;
            nextPose=Time.unscaledTime+.2f;
            bool calibrated=LocalCalibrated&&!appPaused;
            if(IsHost){if(Match.Players.Count>0)Match.Players[0].calibrated=calibrated;}
            else network?.Server?.Send(new LanMessage{kind="calibration",epoch=Epoch,groundGeneration=Ground.generation,calibrated=calibrated});
        }
    }
}
