using System;
using System.IO;
using CandyFlick.Original26;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class Original26Validation
    {
        [Serializable] sealed class Fixtures
        {
            public string binary_sha256;
            public Speed[] calculateSpeed;
            public Angle[] calculateAngle;
            public Sling[] slingshot;
            public Hold[] hold;
            public Region[] touchRegions;
        }
        [Serializable] sealed class Speed { public float height, distance, sensitivity, rawSpeed, expected; }
        [Serializable] sealed class Angle { public float[] start, end; public float expected; }
        [Serializable] sealed class Sling { public float distance, inputAngle; public Dispatch expected; }
        [Serializable] sealed class Dispatch { public float speed, angleXRatio, angle; }
        [Serializable] sealed class Hold { public float duration; public Dispatch expected; }
        [Serializable] sealed class Region { public float[] start; public bool expected; }
        [Serializable] sealed class Evidence
        {
            public string source, binary_sha256, unity;
            public int cases;
            public float largestSpeedError, largestAngleError;
        }

        [MenuItem("Candy Flick/Validate against original 2.6 ARM")]
        public static void Run()
        {
            var fixtures = JsonUtility.FromJson<Fixtures>(File.ReadAllText("Documentation/Recovered/native26-goldens.json"));
            var evidence = new Evidence { source = "Original 2.6 ARMv7 instructions executed by Tools/native26_oracle.py", binary_sha256 = fixtures.binary_sha256, unity = Application.unityVersion };
            foreach (var c in fixtures.calculateSpeed)
            {
                float actual = LaunchMath26.CalculateSpeed(c.rawSpeed, c.height, c.distance, c.sensitivity);
                evidence.largestSpeedError = Mathf.Max(evidence.largestSpeedError, Mathf.Abs(actual - c.expected));
                Check(actual, c.expected, 0.0001f, "CalculateSpeed");
                evidence.cases++;
            }
            foreach (var c in fixtures.calculateAngle)
            {
                float actual = LaunchMath26.CalculateAngle(new Vector2(c.start[0], c.start[1]), new Vector2(c.end[0], c.end[1]));
                evidence.largestAngleError = Mathf.Max(evidence.largestAngleError, Mathf.Abs(actual - c.expected));
                Check(actual, c.expected, 0.00002f, "CalculateAngle");
                evidence.cases++;
            }
            foreach (var c in fixtures.slingshot)
            {
                Check(LaunchMath26.SlingshotStrength(c.distance), c.expected.speed, 0.0001f, "OnTouchTweaker strength");
                Check(-c.inputAngle, c.expected.angle, 0f, "OnTouchTweaker angle");
                Check(0f, c.expected.angleXRatio, 0f, "OnTouchTweaker pitch input");
                evidence.cases++;
            }
            foreach (var c in fixtures.hold)
            {
                Check(LaunchMath26.HoldStrength(c.duration), c.expected.speed, 0.0001f, "OnTouchHold strength");
                evidence.cases++;
            }
            foreach (var c in fixtures.touchRegions)
            {
                bool actual = TouchFlickListener26.CheckTouchPosSlingshot(new Vector2(c.start[0], c.start[1]), 640, 1136);
                if (actual != c.expected) throw new Exception("Slingshot start region differs from original at " + c.start[0] + ", " + c.start[1]);
                evidence.cases++;
            }
            File.WriteAllText("Documentation/native26-validation.json", JsonUtility.ToJson(evidence, true));
            Debug.Log("ORIGINAL_26_VALIDATION_PASSED: " + evidence.cases + " cases against original binary");
        }

        static void Check(float actual, float expected, float tolerance, string name)
        {
            if (float.IsNaN(actual) || Mathf.Abs(actual - expected) > tolerance)
                throw new Exception(name + ": expected " + expected.ToString("R") + ", got " + actual.ToString("R"));
        }
    }
}
