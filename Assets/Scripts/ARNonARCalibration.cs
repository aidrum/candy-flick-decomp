using System.Collections;
using UnityEngine;
using Vuforia;

namespace CandyFlick
{
    public sealed partial class ARMarkerMode
    {
        public bool CalibratingForNonAR { get; private set; }
        Coroutine lensCalibration;
        int nonARStateCount;
        bool nonARCalibrationFailed;

        public void BeginNonARCalibration()
        {
            // Simulator projections describe a virtual camera, not the phone lens.
            if(Application.isEditor || !Application.isMobilePlatform || !game.IsNonAR)return;
            End();
            CalibratingForNonAR=true;
            nonARStateCount=0;nonARCalibrationFailed=false;
            GroundMode=false;
            startup=StartCoroutine(StartCamera());
            lensCalibration=StartCoroutine(CalibrateNonARLens());
        }
        IEnumerator CalibrateNonARLens()
        {
            // Includes time for the normal camera-permission prompt. No observers,
            // live background or gameplay tracking are needed to obtain the lens.
            float deadline=Time.realtimeSinceStartup+45;
            int width=Screen.width,height=Screen.height;
            ScreenOrientation orientation=Screen.orientation;
            while(game.IsNonAR && game.NonARBackground.Active && !nonARCalibrationFailed && Time.realtimeSinceStartup<deadline)
            {
                if(Screen.width!=width || Screen.height!=height || Screen.orientation!=orientation)break;
                // Allow the SDK to finish applying its initial projection before
                // sampling the camera, rather than retaining Unity's default lens.
                if(nonARStateCount>=2 && vuforia && Camera && VuforiaApplication.Instance.IsRunning)
                {
                    var intrinsics=vuforia.CameraDevice.GetCameraIntrinsics();
                    if(intrinsics!=null && intrinsics.Size.x>0 && intrinsics.Size.y>0 &&
                        intrinsics.FocalLength.x>0 && intrinsics.FocalLength.y>0 &&
                        game.NonARBackground.RememberARProjection(Camera.projectionMatrix,Camera.aspect))break;
                }
                yield return null;
            }
            // Stop outside the SDK state callback. End also saves the calibrated
            // projection and restores the camera before a subsequent AR session.
            lensCalibration=null;
            End();
        }
    }
}
