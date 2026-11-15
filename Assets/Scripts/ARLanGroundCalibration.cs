using System.Linq;
using UnityEngine;
using Vuforia;

namespace CandyFlick
{
    public sealed partial class ARMarkerMode
    {
        bool LanGround=>game&&game.LAN&&game.LAN.GroundMode&&(game.IsLanMatch||lanRetained);
        bool lanRetained;public bool RetainingLanGround=>lanRetained;
        AnchorBehaviour lanReference,lanPlacement;
        int lanGeneration,lanTargetEnabledFrame,lanPlacementVersion;bool lanReferenceSeen,lanPlacementSeen,lanLossReported;
        float lanLostAt=-1;Quaternion lanPlacementRotation=Quaternion.identity;
        Pose lanInitialCamera;
        bool LanReferenceTracked=>lanReference&&lanReference.enabled&&IsGroundTracked(lanReference.TargetStatus.Status);
        bool LanPlacementTracked=>lanPlacement&&lanPlacement.enabled&&IsGroundTracked(lanPlacement.TargetStatus.Status);
        public bool LanCalibrationReady=>LanGround&&lanGeneration==game.LAN.Ground.generation&&GroundDeviceTracked&&LanReferenceTracked;
        public static readonly Quaternion DisplayRotation=Quaternion.Euler(-90,0,0);
        public static Pose RelativePose(Pose reference,Pose value)
        {
            Quaternion inverse=Quaternion.Inverse(reference.rotation);
            return new Pose(inverse*(value.position-reference.position),inverse*value.rotation);
        }
        public static Pose ApplyRelativePose(Pose reference,Pose value)=>new Pose(reference.position+reference.rotation*value.position,reference.rotation*value.rotation);
        static Pose AnchorPose(AnchorBehaviour anchor)=>new Pose(anchor.transform.position,anchor.transform.rotation);
        public void RetainLanGround()
        {
            lanRetained=true;SetTracking(false);
            foreach(var target in targets)if(target)target.enabled=false;
        }
        void UpdateRetainedLanGround()
        {
            if(!game.LAN.GroundMode||!game.LAN.Ground.reusable||game.LAN.Ground.generation!=lanGeneration){End();return;}
            if(!GroundDeviceTracked||!LanReferenceTracked||!LanPlacementTracked)LanTrackingUnavailable();
            else lanLostAt=-1;
            SetTracking(false);
        }
        void SyncLanPlacement(CandyFlick.Multiplayer.LanGroundState shared)
        {
            if(lanPlacement&&(!shared.placed||lanPlacementVersion!=shared.placement))
            {
                DestroyLanAnchor(ref lanPlacement);lanPlacementSeen=false;lanPlacementRotation=Quaternion.identity;
                lanLostAt=-1;lanLossReported=false;SetTracking(false);
            }
        }
        void ConfigureLanCalibrationTargets()
        {
            foreach(var target in targets)if(target)
            {
                float width=LanGround&&!game.LAN.IsHost&&game.LAN.Ground.markerWidth>0?game.LAN.Ground.markerWidth:.2f;
                if(Mathf.Abs(target.GetSize().x-width)>.00001f)target.SetWidth(width);
                bool enable=running&&(!GroundMode||(LanGround&&!game.LAN.IsHost&&!lanReferenceSeen&&game.LAN.Phase!=CandyFlick.Multiplayer.LanPhase.Finished&&game.LAN.Ground.markerWidth>0&&target.TargetName==LanMultiplayer.CalibrationTarget));
                if(LanGround&&enable&&!target.enabled)lanTargetEnabledFrame=Time.frameCount;
                target.enabled=enable;
            }
        }
        void UpdateLanGroundCalibration()
        {
            GroundPointAvailable=false;var shared=game.LAN.Ground;
            if(lanGeneration!=shared.generation)
            {ClearLanGroundAnchors();lanGeneration=shared.generation;SetTracking(false);}
            // Image detection must remain live while device tracking/host readiness
            // initialize. Otherwise the reference needed for calibration is gated off.
            SyncLanPlacement(shared);ConfigureLanCalibrationTargets();
            if(game.LAN.Phase==CandyFlick.Multiplayer.LanPhase.Finished&&!shared.reusable){SetTracking(false);return;}
            var visible=game.LAN.IsHost?null:targets.FirstOrDefault(t=>t&&t.enabled&&t.TargetName==LanMultiplayer.CalibrationTarget&&IsImageVisible(t.TargetStatus.Status));
            bool imageDetected=visible&&Time.frameCount>lanTargetEnabledFrame;
            if(!game.GroundSupport.Supported){Status=game.GroundSupport.Reason;SetTracking(false);return;}
            if(VuforiaRuntimeUtilities.IsSimulatorPlayMode())
            {Status="Ground calibration requires a supported phone. It is unavailable in the simulator.";SetTracking(false);return;}
            if(!game.LAN.IsHost&&!lanReference&&!imageDetected)
            {
                Status=GroundDeviceTracked?"Scan the image on the host's screen to align your phone with the shared play area.":"Keep the host image in view and move your phone slightly.";
                SetTracking(false);return;
            }
            if(!GroundDeviceTracked)
            {
                Status=imageDetected?"Image detected. Move your phone slightly while keeping the image in view.":"Move your phone slowly to track the surroundings.";
                LanTrackingUnavailable();return;
            }
            if(!vuforia.World.AnchorsSupported)
            {Status=imageDetected?"Image detected. Waiting for ground tracking to start...":"Waiting for ground tracking to start...";SetTracking(false);return;}
            if(!lanReference)
            {
                Pose reference;
                if(game.LAN.IsHost)
                {
                    // Freeze the host's initial tracked camera frame. Known UI
                    // offsets are included; camera-to-display chassis offset is
                    // approximated as zero, not claimed as precision calibration.
                    Vector2 shift=game.GetComponent<CandyFlickUI>().LanCalibrationScreenOffsetMetres;
                    reference=new Pose(Camera.transform.position+Camera.transform.rotation*new Vector3(shift.x,shift.y,0),Camera.transform.rotation*DisplayRotation);
                    lanInitialCamera=RelativePose(reference,new Pose(Camera.transform.position,Camera.transform.rotation));
                }
                else
                {
                    if(!shared.originReady)
                    {Status="Image detected. Waiting for the host's tracking to be ready. Keep the image in view.";SetTracking(false);return;}
                    reference=new Pose(visible.transform.position,visible.transform.rotation);
                }
                lanReference=vuforia.ObserverFactory.CreateAnchorBehaviour("LAN reference "+game.LAN.Epoch+"-"+lanGeneration,reference.position,reference.rotation);
                if(!lanReference){Status="Could not calibrate. Hold both phones still and try scanning again.";SetTracking(false);return;}
                ConfigureLanCalibrationTargets();Status="Calibrating...";SetTracking(false);return;
            }
            if(!LanReferenceTracked)
            {Status=game.LAN.IsHost?"Stabilizing the host reference. Hold your phone steady.":"Image detected. Keep it in view while the shared reference stabilizes.";LanTrackingUnavailable();return;}
            lanReferenceSeen=true;
            // Only detach from the displayed image once the replacement anchor
            // actually has a tracked pose, not merely after allocating its object.
            ConfigureLanCalibrationTargets();
            if(game.LAN.IsHost&&!shared.originReady)game.LAN.SetHostOriginReady(lanGeneration);
            if(!shared.placed)
            {
                SetTracking(false);lanLostAt=-1;
                if(game.LAN.IsHost&&!game.LAN.AllClientsCalibrated)
                {
                    Pose current=RelativePose(AnchorPose(lanReference),new Pose(Camera.transform.position,Camera.transform.rotation));
                    if(Vector3.Distance(current.position,lanInitialCamera.position)>.02f||Quaternion.Angle(current.rotation,lanInitialCamera.rotation)>2)
                    {game.LAN.ResetGroundCalibration("Host moved. Hold still while all other players scan again.");Status="Host moved. Hold your phone still while all other players scan.";return;}
                    Status="All other players must scan the image on your screen. Hold your phone still.";return;
                }
                if(game.LAN.IsHost)
                {
                    GroundPointAvailable=TryGroundHit(out _);
                    Status=GroundPointAvailable?"Everyone is calibrated. Tap to place Om Nom on this surface.":"Everyone is calibrated. Aim at a flat surface to choose the play area.";
                }
                else Status="Calibrated. Wait for the host to choose the play area; keep tracking your surroundings.";
                return;
            }
            if(!LanMultiplayer.ValidGroundPlacement(shared.offset,shared.rotation))
            {Status="Invalid shared placement. Ask the host to recalibrate.";SetTracking(false);return;}
            if(!lanPlacement)
            {
                Pose pose=ApplyRelativePose(AnchorPose(lanReference),new Pose(shared.offset,shared.rotation));
                lanPlacement=vuforia.ObserverFactory.CreateAnchorBehaviour("LAN placement "+game.LAN.Epoch+"-"+lanGeneration+"-"+shared.placement,pose.position,pose.rotation);
                lanPlacementVersion=shared.placement;lanPlacementRotation=Quaternion.identity;
                Status="Anchoring the shared play area...";SetTracking(false);return;
            }
            if(!LanPlacementTracked)
            {Status="Recovering the shared anchor. Hold your phone steady.";LanTrackingUnavailable();return;}
            Pose frame=AnchorPose(lanPlacement);frame.rotation*=lanPlacementRotation;
            Pose native=NativeCameraPose(frame,new Pose(Camera.transform.position,Camera.transform.rotation));
            game.SetTrackedView(native.position,native.rotation,Camera.projectionMatrix,!Tracking);
            if(game.LAN.IsHost&&!lanPlacementSeen)game.Actor.FacePlayer();
            lanPlacementSeen=true;lanLostAt=-1;Status="";SetTracking(true);
        }
        bool PlaceLanGround()
        {
            if(!LanGround||!game.LAN.IsHost||!game.LAN.AllClientsCalibrated||game.ScreenState!=GameScreen.Playing||CandyFlickUI.PopupActive||lanPlacement||!LanReferenceTracked||!TryGroundHit(out var hit))return false;
            Quaternion rotation=Quaternion.Euler(0,Camera.transform.eulerAngles.y,0);
            Pose relative=RelativePose(AnchorPose(lanReference),new Pose(hit.Position,rotation));
            if(!LanMultiplayer.ValidGroundPlacement(relative.position,relative.rotation))return false;
            lanPlacement=vuforia.ObserverFactory.CreateAnchorBehaviour("LAN placement "+game.LAN.Epoch+"-"+lanGeneration+"-"+(game.LAN.Ground.placement+1),hit);
            if(!lanPlacement){Status="Could not place Om Nom. Try another flat surface.";return false;}
            lanPlacementRotation=Quaternion.Inverse(hit.Rotation)*rotation;
            if(!game.LAN.PublishGroundPlacement(relative.position,relative.rotation,lanGeneration)){DestroyLanAnchor(ref lanPlacement);return false;}
            lanPlacementVersion=game.LAN.Ground.placement;consumingPlacement=true;placementFrame=Time.frameCount;GroundPointAvailable=false;UpdateLanGroundCalibration();return true;
        }
        void LanTrackingUnavailable()
        {
            SetTracking(false);
            if(!lanReferenceSeen&&!lanPlacementSeen)return;
            if(lanLostAt<0)lanLostAt=Time.unscaledTime;
            if(!lanLossReported&&Time.unscaledTime-lanLostAt>=1)
            {lanLossReported=true;game.LAN.ReportGroundTrackingLoss(lanGeneration);Status="Tracking was lost. Everyone must scan the host's screen again.";}
        }
        void InterruptLanGround()
        {
            SetTracking(false);
            if(!lanLossReported&&(lanReferenceSeen||lanPlacementSeen)){lanLossReported=true;game.LAN.ReportGroundTrackingLoss(lanGeneration);}
        }
        void ClearLanGroundAnchors()
        {
            DestroyLanAnchor(ref lanPlacement);DestroyLanAnchor(ref lanReference);
            lanReferenceSeen=lanPlacementSeen=lanLossReported=false;lanLostAt=-1;lanGeneration=lanPlacementVersion=0;lanTargetEnabledFrame=Time.frameCount;lanPlacementRotation=Quaternion.identity;
        }
        static void DestroyLanAnchor(ref AnchorBehaviour anchor)
        {if(anchor){anchor.UnconfigureAnchor();Destroy(anchor.gameObject);anchor=null;}}
    }
}
