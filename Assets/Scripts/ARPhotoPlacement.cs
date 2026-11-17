using UnityEngine;
using Vuforia;

namespace CandyFlick
{
    public sealed partial class ARMarkerMode
    {
        public string PhotoSupportFailure {get;private set;}
        long photoAnchorSequence;
        string NextPhotoAnchorName()=>"Photo Om Nom ground "+(++photoAnchorSequence);
        void UpdatePhotoTracking()
        {
            bool supported=game.GroundSupport.Supported&&!VuforiaRuntimeUtilities.IsSimulatorPlayMode()&&vuforia.World.AnchorsSupported;
#if !UNITY_EDITOR
            if(!supported)PhotoSupportFailure="Photo mode requires Ground AR support. "+(game.GroundSupport.Supported?"Ground detection is unavailable on this device.":game.GroundSupport.Reason);
#endif
            bool tracked=supported&&GroundDeviceTracked&&!applicationPaused&&!capturePaused;
            if(tracked)game.SetTrackedView(Camera.transform.position*1000,Camera.transform.rotation,Camera.projectionMatrix,true);
            SetTracking(tracked);
            foreach(var nom in game.PhotoActors)nom.SyncAnchor(tracked);
            game.RefreshPhotoGround();
            GroundPointAvailable=game.PhotoPlacing&&tracked&&TryGroundHit(out _);
            Status=!supported?"Photo mode requires supported ground tracking on a phone.":!tracked?
                "Move your phone slowly to track your surroundings.":game.PhotoPlacing?
                (GroundPointAvailable?"Surface found. Tap anywhere to place Om Nom.":"Move your phone slowly and aim at a flat, well-lit surface."):"";
        }
        bool PlacePhotoOmNom()
        {
            if(game.ScreenState!=GameScreen.Playing||!game.PhotoPlacing||CandyFlickUI.PopupActive||!TryGroundHit(out var hit))return false;
            var anchor=vuforia.ObserverFactory.CreateAnchorBehaviour(NextPhotoAnchorName(),hit);
            if(!anchor){Status="Could not place Om Nom. Aim at the surface and try again.";return false;}
            game.CompletePhotoPlacement(anchor);
            consumingPlacement=true;placementFrame=Time.frameCount;GroundPointAvailable=false;
            UpdatePhotoTracking();return true;
        }
    }
}
