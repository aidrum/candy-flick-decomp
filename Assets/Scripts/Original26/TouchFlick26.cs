using System;
using System.Collections.Generic;
using UnityEngine;

namespace CandyFlick.Original26
{
    public struct TouchFlick26
    {
        public int speed;
        public float angle, distance, holdTime;
        public Vector2 startTouch;
    }

    // Translation of TouchFlickListener (0x51fe8–0x52c64).
    // Input acquisition is supplied separately so editor mouse gestures use the same code.
    public sealed class TouchFlickListener26
    {
        public TouchMode26 mode = TouchMode26.Tweaker;
        public float maxFlickTime = 0.5f;
        public bool IsTouchBegan { get; private set; }
        public bool IsTweakerAccept { get; private set; }
        public Vector2 Start { get; private set; }
        public Vector2 Position { get; private set; }
        public event Action<Vector2> Began;
        public event Action<TouchFlick26> Flick, Hold, Tweaker;
        public event Action TweakerCancel;
        readonly List<float> speeds = new List<float>();
        float beganTime, movingTime;
        bool moved, ended;

        public void Sample(TouchPhase phase, Vector2 position, Vector2 delta, float time,
            float deltaTime, int screenWidth, int screenHeight)
        {
            Position = position;
            if (phase == TouchPhase.Began)
            {
                speeds.Clear();
                beganTime = time;
                Start = position;
                IsTouchBegan = true;
                moved = ended = false;
                Began?.Invoke(position);
            }
            if (phase == TouchPhase.Moved && IsTouchBegan)
            {
                SaveTouchInfo(position, delta, deltaTime, screenWidth, screenHeight);
                if (!moved) { moved = true; movingTime = time; }
                else if (mode == TouchMode26.Flick && time - movingTime > maxFlickTime)
                    EndTouch(position, delta, time, deltaTime, screenWidth, screenHeight);
            }
            if (phase == TouchPhase.Ended && IsTouchBegan && !ended)
                EndTouch(position, delta, time, deltaTime, screenWidth, screenHeight);
            // Unity 6 / Android can cancel input on app suspension. Do not synthesize a shot.
            if (phase == TouchPhase.Canceled) Cancel();
        }

        void SaveTouchInfo(Vector2 position, Vector2 delta, float deltaTime, int width, int height)
        {
            float distance = 100f * delta.magnitude / Mathf.Sqrt((float)width * width + (float)height * height);
            if (mode != TouchMode26.Tweaker && distance <= 1f) return;
            speeds.Add(distance / deltaTime); // Native uses Time.deltaTime, not Touch.deltaTime.
            if (position.y < Start.y) IsTweakerAccept = true;
        }

        float GetMidSpeed()
        {
            if (speeds.Count == 0) return 1f;
            float sum = 0;
            foreach (float value in speeds) sum += value;
            return sum / speeds.Count;
        }

        void EndTouch(Vector2 position, Vector2 delta, float time, float deltaTime, int width, int height)
        {
            SaveTouchInfo(position, delta, deltaTime, width, height);
            var touch = new TouchFlick26 {
                speed = (int)GetMidSpeed(), angle = LaunchMath26.CalculateAngle(Start, position),
                distance = Mathf.Abs(position.y - Start.y) / height,
                holdTime = time - beganTime, startTouch = Start
            };
            if (mode == TouchMode26.Tweaker)
            {
                if (position.y >= Start.y) TweakerCancel?.Invoke();
                else Tweaker?.Invoke(touch);
            }
            else if (mode == TouchMode26.TapHold) Hold?.Invoke(touch);
            else Flick?.Invoke(touch);
            IsTouchBegan = moved = false;
            ended = true;
            IsTweakerAccept = false;
        }

        public void Cancel()
        {
            if (IsTouchBegan) TweakerCancel?.Invoke();
            IsTouchBegan = moved = false;
            ended = true;
            IsTweakerAccept = false;
        }

        // PlayerBehaviour.CheckTouchPosSlingshot, 0x25928.
        public static bool CheckTouchPosSlingshot(Vector2 start, int width, int height)
        {
            // Force the original float32 rounding before comparison. Mono can retain a
            // wider intermediate for int-to-float products, moving the exact edge at x=128.
            float right = RoundSingle(width * .8f), left = RoundSingle(width * .2f);
            float bottom = RoundSingle(height * .15f);
            if (start.x > right && start.y < bottom) return false;
            if (start.x < left && start.y < bottom) return false;
            return start.y < height / 2;
        }
        static float RoundSingle(float value) => BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits(value));
    }
}
