using UnityEngine;
using Vuforia;

namespace CandyFlick
{
    // Shares Vuforia capture/calibration with Image mode. Fusion uses ARCore on
    // supported Android devices; only successful surface hit tests can place content.
    public sealed partial class ARMarkerMode
    {
        public bool GroundMode { get; private set; }
        public bool GroundPlaced => groundAnchor || (LanGround&&lanPlacement);
        public bool GroundPointAvailable { get; private set; }
        AnchorBehaviour groundAnchor;
        bool consumingPlacement, faceGroundOnTrack, groundAnchorWasTracked;
        int placementFrame;

        void ConfigureTrackingMode()
        {
            // Device tracking stays off for the original image-only experience.
            vuforia.DevicePoseBehaviour.enabled = running && GroundMode;
            // LAN's first target is an upright screen/reference, not the floor.
            // FIRST_TARGET would make that frame Unity's origin and rotate real
            // horizontal hits away from Vector3.up. With device tracking enabled,
            // DEVICE retains the gravity-aligned tracking frame and moving camera;
            // the separate LAN reference anchor still supplies shared coordinates.
            vuforia.SetWorldCenter(GroundMode && !LanGround ? WorldCenterMode.FIRST_TARGET : WorldCenterMode.DEVICE, null);
            if(!GroundMode||LanGround) LoadTargets();
            ConfigureLanCalibrationTargets();
        }
        bool GroundDeviceTracked => vuforia && vuforia.DevicePoseBehaviour &&
            vuforia.DevicePoseBehaviour.enabled && IsGroundTracked(vuforia.DevicePoseBehaviour.TargetStatus.Status);
        public static bool IsGroundTracked(Vuforia.Status status) =>
            status == Vuforia.Status.TRACKED || status == Vuforia.Status.EXTENDED_TRACKED;

        void UpdateGroundTracking()
        {
            GroundPointAvailable = false;
            if(!game.GroundSupport.Supported){Status=game.GroundSupport.Reason;LoseTracking();return;}
            if(VuforiaRuntimeUtilities.IsSimulatorPlayMode() || !vuforia.World.AnchorsSupported)
            {
#if UNITY_EDITOR
                Status = "Ground detection needs a supported phone. It is unavailable in the Vuforia simulator.";
#else
                Status = "Ground detection is unavailable. Check Google Play Services for AR, or choose AR Mode: Image.";
#endif
                LoseTracking(); return;
            }
            if(!groundAnchor && groundAnchorWasTracked) LoseTracking();
            if(!GroundDeviceTracked)
            {
                Status = "Move your phone slowly to track your surroundings.";
                LoseTracking(); return;
            }
            if(groundAnchor)
            {
                bool tracked = groundAnchor.enabled && IsGroundTracked(groundAnchor.TargetStatus.Status);
                if(!tracked)
                {
                    Status = "Placing Om Nom...";
                    LoseTracking(); return;
                }
                else
                {
                    groundAnchorWasTracked = true;
                    bool snap = !Tracking;
                    Pose native = NativeCameraPose(new Pose(groundAnchor.transform.position, groundAnchor.transform.rotation),
                        new Pose(Camera.transform.position, Camera.transform.rotation));
                    game.SetTrackedView(native.position, native.rotation, Camera.projectionMatrix, snap);
                    if(faceGroundOnTrack) { game.Actor.FacePlayer(); faceGroundOnTrack = false; }
                    Status = "";
                }
                SetTracking(tracked);
                return;
            }
            SetTracking(false);
            GroundPointAvailable = TryGroundHit(out _);
            Status = GroundPointAvailable ? "Surface found. Tap anywhere to place Om Nom." :
                "Move your phone slowly and aim at a flat, well-lit surface.";
        }
        bool TryGroundHit(out HitTestResult hit)
        {
            hit = null;
            if(!running || !GroundMode || applicationPaused || capturePaused || !VuforiaApplication.Instance.IsRunning ||
                VuforiaRuntimeUtilities.IsSimulatorPlayMode() || !vuforia.World.AnchorsSupported || !GroundDeviceTracked) return false;
            Vector2 point = VuforiaRuntimeUtilities.ConvertScreenToImageSpace(Screen.safeArea.center);
            return vuforia.World.HitTest(point, 1.4f, out hit) && IsHorizontalSurface(new Pose(hit.Position,hit.Rotation));
        }
        public static bool IsHorizontalSurface(Pose surface) =>
            float.IsFinite(surface.position.x) && float.IsFinite(surface.position.y) && float.IsFinite(surface.position.z) &&
            Vector3.Dot(surface.rotation * Vector3.up, Vector3.up) >= .98f;

        public bool PlaceGround()
        {
            if(LanGround)return PlaceLanGround();
            if(game.ScreenState != GameScreen.Playing || CandyFlickUI.PopupActive || groundAnchor || !TryGroundHit(out var hit)) return false;
            // CreateHitTestAnchor keeps the native plane association, instead of
            // guessing a point at a fixed camera distance or storing a camera-relative pose.
            groundAnchor = vuforia.ObserverFactory.CreateAnchorBehaviour("Om Nom ground", hit);
            if(!groundAnchor) { Status = "Could not place Om Nom. Aim at the surface and try again."; return false; }
            faceGroundOnTrack = consumingPlacement = true; placementFrame = Time.frameCount;
            GroundPointAvailable = false;
            UpdateGroundTracking();
            return true;
        }
        // Only discard an anchor after it has produced a tracked pose. A newly
        // created anchor can need another SDK frame before its first observation.
        void LoseTracking()
        {
            if(LanGround){InterruptLanGround();return;}
            if(GroundMode && groundAnchorWasTracked) RepositionGround();
            else SetTracking(false);
        }
        public void RepositionGround()
        {
            if(!running || !GroundMode) return;
            if(LanGround){if(game.LAN.IsHost)game.LAN.ResetGroundCalibration();return;}
            game.CancelCandyGesture(); ClearGroundAnchor(); SetTracking(false);
            Status = "Move your phone slowly to find a flat surface.";
        }
        void ClearGroundAnchor()
        {
            ClearLanGroundAnchors();
            if(groundAnchor)
            {
                groundAnchor.UnconfigureAnchor();
                Destroy(groundAnchor.gameObject); groundAnchor = null;
            }
            GroundPointAvailable = consumingPlacement = faceGroundOnTrack = groundAnchorWasTracked = false;
        }
        public bool ConsumePlacementInput()
        {
            if(!consumingPlacement) return false;
            if(Time.frameCount > placementFrame + 1 && Input.touchCount == 0 && !Input.GetMouseButton(0) && !Input.GetMouseButtonUp(0))
                consumingPlacement = false;
            return true; // Always consume the final release as well.
        }
    }
}
