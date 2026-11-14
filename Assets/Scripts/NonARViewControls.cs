using CandyFlick.Original26;
using UnityEngine;

namespace CandyFlick
{
    // Camera gestures own their complete touch sequence, including fingers left
    // after a pinch, so a release cannot turn into a candy launch.
    public sealed class NonARViewControls
    {
        enum Gesture { None, CameraPending, Orbit, Launcher }
        public float Distance { get; private set; }
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public bool BlocksPetting => multiTouch || gesture == Gesture.Orbit || blockPettingFrame;
        public static float MinDistance => GameRules.MinDistance * 100;
        public static float MaxDistance => GameRules.MaxDistance * 100;
        readonly CandyFlickGame game;
        Bounds table;
        Vector3 center;
        Gesture gesture;
        Vector2 start, previous;
        int finger = -1, pinchA = -1, pinchB = -1;
        float previousSeparation;
        bool multiTouch, blockPettingFrame;

        public NonARViewControls(CandyFlickGame owner) { game = owner; }
        public void Begin(Bounds tableBounds)
        {
            Cancel(); table = tableBounds; center = game.World.Find("FrogPos").position;
            Vector3 offset = game.Player.position - (center + Vector3.up * 70);
            Distance = Mathf.Clamp(LaunchMath26.HorizontalDistance(game.Player.position, center), MinDistance, MaxDistance);
            Yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            Pitch = Mathf.Atan2(offset.y, Distance) * Mathf.Rad2Deg;
        }
        public void Cancel()
        {
            gesture = Gesture.None; multiTouch = blockPettingFrame = false;
            finger = pinchA = pinchB = -1; previousSeparation = 0;
        }
        public bool PollInput()
        {
            if (!game.IsNonAR || !game.CanSimulate) { Cancel(); return false; }
            if (CandyFlickUI.PopupActive) { game.CancelCandyGesture(); Cancel(); return true; }
            if (Input.touchCount > 0)
                return HandleTouches(Input.touchCount, Input.GetTouch(0), Input.touchCount > 1 ? Input.GetTouch(1) : default);
#if UNITY_EDITOR || UNITY_STANDALONE
            float wheel = Input.mouseScrollDelta.y;
            if (wheel != 0 && !Input.GetMouseButton(0))
            {
                game.CancelCandyGesture(); ZoomBy(Mathf.Exp(-wheel * .12f)); blockPettingFrame = true; return true;
            }
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
            {
                var phase = Input.GetMouseButtonDown(0) ? TouchPhase.Began : Input.GetMouseButtonUp(0) ? TouchPhase.Ended : TouchPhase.Moved;
                return HandleTouches(1, new Touch { fingerId = -2, phase = phase, position = Input.mousePosition }, default);
            }
#endif
            return HandleTouches(0, default, default);
        }
        public bool HandleTouches(int count, Touch first, Touch second)
        {
            blockPettingFrame = false;
            if (!game.IsNonAR || !game.CanSimulate || CandyFlickUI.PopupActive) { Cancel(); return false; }
            if (count == 0)
            {
                bool consumed = multiTouch || gesture == Gesture.Orbit || gesture == Gesture.CameraPending;
                if (gesture == Gesture.Launcher) game.CancelCandyGesture();
                Cancel(); blockPettingFrame = consumed; return consumed;
            }
            if (count >= 2)
            {
                if (!multiTouch) game.CancelCandyGesture();
                multiTouch = true; gesture = Gesture.None; finger = -1;
                if (Ended(first) || Ended(second)) { previousSeparation = 0; return true; }
                float separation = Vector2.Distance(first.position, second.position);
                bool samePair = (first.fingerId == pinchA && second.fingerId == pinchB) || (first.fingerId == pinchB && second.fingerId == pinchA);
                if (samePair && previousSeparation > 1 && separation > 1) ZoomBy(previousSeparation / separation);
                pinchA = first.fingerId; pinchB = second.fingerId; previousSeparation = separation;
                return true;
            }
            if (multiTouch) return true; // Wait for every finger to lift before another gesture.
            if (first.phase == TouchPhase.Began)
            {
                finger = first.fingerId; start = previous = first.position;
                gesture = IsCameraArea(first.position) ? Gesture.CameraPending : Gesture.Launcher;
                if (gesture == Gesture.CameraPending) game.CancelCandyGesture();
            }
            if (finger != first.fingerId) return true;
            bool camera = gesture == Gesture.CameraPending || gesture == Gesture.Orbit;
            if (Ended(first))
            {
                blockPettingFrame = gesture == Gesture.Orbit; gesture = Gesture.None; finger = -1; return camera;
            }
            if (gesture == Gesture.CameraPending && (first.position - start).magnitude > 8 * CandyFlickUI.CanvasScale(Screen.safeArea))
            { gesture = Gesture.Orbit; game.CancelCandyGesture(); }
            if (gesture == Gesture.Orbit)
            {
                Vector2 delta = first.position - previous;
                OrbitBy(new Vector2(delta.x / Screen.safeArea.width * 180, -delta.y / Screen.safeArea.height * 90));
            }
            previous = first.position; return camera;
        }
        bool IsCameraArea(Vector2 point)
        {
            Rect safe = Screen.safeArea;
            if (!safe.Contains(point) || point.y <= safe.yMin + safe.height * .42f) return false;
            return !game.CandyGhost.GetComponent<Collider>().Raycast(game.CandyCamera.ScreenPointToRay(point), out _, Mathf.Infinity);
        }
        static bool Ended(Touch touch) => touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
        public void ZoomBy(float multiplier)
        {
            if (!game.IsNonAR || !game.CanSimulate || multiplier <= 0 || !float.IsFinite(multiplier)) return;
            Distance = Mathf.Clamp(Distance * multiplier, MinDistance, MaxDistance); ApplyView();
        }
        public void OrbitBy(Vector2 degrees)
        {
            if (!game.IsNonAR || !game.CanSimulate || !float.IsFinite(degrees.x) || !float.IsFinite(degrees.y)) return;
            Yaw += degrees.x; Pitch += degrees.y; ApplyView();
        }
        void ApplyView()
        {
            // Wrap freely around the full circle while retaining the vertical
            // table/launcher clearance limits.
            Yaw = Mathf.Repeat(Yaw + 180, 360) - 180;
            float minPitch = Mathf.Max(10, Mathf.Atan2(Mathf.Max(0, table.max.y + 250 - center.y - 70), Distance) * Mathf.Rad2Deg);
            Pitch = Mathf.Clamp(Pitch, minPitch, 60);
            Vector3 target = center + Vector3.up * 70;
            // The recovered score truncates distance to an integer: keep its far
            // endpoint at 50 despite float rounding in the orbit's sin/cos.
            float radius = Distance >= MaxDistance ? Distance + .01f : Distance;
            Vector3 position = target + new Vector3(Mathf.Sin(Yaw * Mathf.Deg2Rad) * radius,
                Mathf.Tan(Pitch * Mathf.Deg2Rad) * radius, Mathf.Cos(Yaw * Mathf.Deg2Rad) * radius);
            game.Player.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        }
    }
}
