using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace CandyFlick.Original26
{
    // Translation of CaptureManager (0x2f5a4, 0x4aa54, 0x4ac24) and CameraEffect (0x2a5d4–0x2a7a8).
    // The iOS camera-roll call is replaced by Android MediaStore (ScreenshotSaver.java).
    public sealed class CaptureManager26 : MonoBehaviour
    {
        public static bool IsScreenShotSaving { get; private set; }
        // The iOS plugin answered with UnitySendMessage → PopupView.ShowPopupWithMessageKey("PHOTO_SAVED").
        public event Action<bool> Saved;
        string path, fileName;
        float captureTime, checkTime;
        bool waitingForFile;

        public void CaptureScreenshot()
        {
            fileName = "CandyFlick_" + DateTime.Now.ToString("dd_MM_yyyy_HH_mm_ss") + ".png";
            path = Path.Combine(Application.persistentDataPath, fileName);
#if UNITY_ANDROID && !UNITY_EDITOR
            ScreenCapture.CaptureScreenshot(fileName); // Mobile players prefix persistentDataPath.
#else
            ScreenCapture.CaptureScreenshot(path);
#endif
            captureTime = checkTime = Time.unscaledTime;
            waitingForFile = IsScreenShotSaving = true;
        }

        void Update()
        {
            if (!waitingForFile || Time.unscaledTime - checkTime <= 1) return; // Native polls once per second.
            if (Time.unscaledTime - captureTime > 30) { waitingForFile = false; StartCoroutine(WaitAndStopSavingFlag()); }
            checkTime = Time.unscaledTime;
            if (!File.Exists(path)) return;
            waitingForFile = false;
            Saved?.Invoke(SaveToGallery());
            StartCoroutine(WaitAndStopSavingFlag());
        }

        bool SaveToGallery()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var saver = new AndroidJavaClass("com.aidrum.candyflick.ScreenshotSaver"))
            {
                bool saved = saver.CallStatic<bool>("save", activity, path, fileName);
                if (saved) File.Delete(path);
                return saved;
            }
#else
            Debug.Log("Candy Flick photo saved: " + path);
            return true;
#endif
        }

        static IEnumerator WaitAndStopSavingFlag()
        {
            yield return new WaitForSecondsRealtime(2);
            IsScreenShotSaving = false;
        }
    }

    // CameraEffect: five diaphragm frames close over .1 s, hold .2 s, then reopen and disable.
    public sealed class CameraEffect26
    {
        const float AnimTime = .1f, PauseTime = .2f;
        readonly Texture2D[] frames;
        float startTime, pausedUntil;
        bool active, back;
        public CameraEffect26(Texture2D[] frames) { this.frames = frames; }
        public void Begin() { active = true; back = false; pausedUntil = 0; startTime = Time.unscaledTime; }
        public void Draw()
        {
            if (!active || frames.Length == 0) return;
            float now = Time.unscaledTime, interval = AnimTime / frames.Length;
            int index;
            if (now < pausedUntil) index = frames.Length - 1;
            else
            {
                if (pausedUntil > 0 && startTime < pausedUntil) startTime = pausedUntil;
                int step = (int)((now - startTime) / interval);
                if (!back)
                {
                    index = step;
                    if (index >= frames.Length) { back = true; index = frames.Length - 1; pausedUntil = now + PauseTime; }
                }
                else
                {
                    index = frames.Length - 1 - step;
                    if (index < 0) { active = false; return; }
                }
            }
            // OnEnable 0x2a5d4: a screen-height square centred horizontally.
            int width = Screen.width, height = Screen.height;
            GUI.DrawTexture(new Rect((width - height) / 2, 0, height, height), frames[index]);
        }
    }
}
