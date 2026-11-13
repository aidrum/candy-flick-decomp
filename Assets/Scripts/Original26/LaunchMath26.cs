using UnityEngine;

namespace CandyFlick.Original26
{
    // Addresses refer to the unmodified ARMv7 image from the supplied 2.6 IPA.
    // Keep intermediate operations separate: their single-precision rounding is observable.
    public enum TouchMode26 { Flick = 0, TapHold = 1, Tweaker = 2 }

    public static class LaunchMath26
    {
        // PlayerBehaviour.CalculateSpeed, 0x25f18.
        public static float CalculateSpeed(float rawSpeed, float cameraHeight, float horizontalDistance,
            float sensitivity = 0.8f)
        {
            float speed = Mathf.Clamp(rawSpeed * sensitivity, 80f, 400f);
            speed *= Mathf.Lerp(1f, 500f / cameraHeight, 0.4f);
            speed *= Mathf.Lerp(1f, horizontalDistance / 750f, 0.5f);
            return speed;
        }

        // TouchFlickListener.CalculateAngle, 0x52bd4. This is deliberately NOT atan2.
        public static float CalculateAngle(Vector2 start, Vector2 end)
        {
            if (end.y == start.y) return 0f;
            float dy = Mathf.Abs(end.y - start.y);
            float dx = Mathf.Abs(end.x - start.x);
            float dySquared = dy * dy;
            float dxSquared = dx * dx;
            float hypotenuse = Mathf.Sqrt(dxSquared + dySquared);
            float denominator = hypotenuse * hypotenuse;
            float angle = Mathf.Acos(dySquared / denominator) * 57.296f;
            return end.x < start.x ? -angle : angle;
        }

        // PlayerBehaviour.OnTouchTweaker, 0x25848. Order matches the VFP instructions.
        public static float SlingshotStrength(float distance)
        {
            float strength = distance > 0.25f ? 1f : distance * 4f;
            strength *= 400f;
            strength *= 1.2f;
            return strength;
        }

        // PlayerBehaviour.OnTouchHold, 0x25788.
        public static float HoldStrength(float duration) => duration <= 1f ? duration * 400f : 400f;

        // PlayerBehaviour.PrepareAndShootBullet / RotateCameraForShoot, 0x255f8 / 0x25df0.
        public static Quaternion ShootRotation(float playerYaw, float angleXRatio, float angle,
            float shootAngleDivider = 2.3f)
            => Quaternion.Euler(-(angleXRatio * 15f - 15f), playerYaw + angle / shootAngleDivider, 0f);

        public static float HorizontalDistance(Vector3 player, Vector3 target)
        {
            float x = player.x - target.x, z = player.z - target.z;
            return Mathf.Sqrt(x * x + z * z);
        }
    }
}
