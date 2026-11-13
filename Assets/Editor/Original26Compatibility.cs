using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class Original26Compatibility
    {
        [MenuItem("Candy Flick/Validate original ring rendering")]
        public static void ValidateRing()
        {
            GameObject wrapper = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Original26/BoostV"));
            try
            {
                Transform root = wrapper.transform.Find("boostv2");
                foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = new Material(renderer.sharedMaterial);
                Renderer ring = root.Find("ring").GetComponent<Renderer>();
                if (!ring.sharedMaterial.HasProperty("_TintColor")) throw new Exception("Original ring shader is missing _TintColor");
                Animation animation = root.GetComponent<Animation>();
                AnimationClip clip = animation["RingFadeInAnim"].clip;
                clip.SampleAnimation(root.gameObject, .5f);
                float alpha = ring.sharedMaterial.GetColor("_TintColor").a;
                if (Mathf.Abs(alpha - .25f) > .00001f)
                {
                    // Unity 4 bound Material curves directly. Unity 6 binds Renderer.material properties.
                    foreach (string name in new[] { "RingFadeInAnim", "RingFadeOutAnim", "RingHitAnim" })
                    {
                        AnimationClip original = animation[name].clip;
                        foreach (var binding in AnimationUtility.GetCurveBindings(original))
                        {
                            if (binding.type != typeof(Material)) continue;
                            AnimationCurve curve = AnimationUtility.GetEditorCurve(original, binding);
                            var modern = EditorCurveBinding.FloatCurve(binding.path, typeof(MeshRenderer), "material." + binding.propertyName);
                            AnimationUtility.SetEditorCurve(original, modern, curve);
                            AnimationUtility.SetEditorCurve(original, binding, null);
                        }
                        EditorUtility.SetDirty(original);
                    }
                    clip.SampleAnimation(root.gameObject, .5f);
                    alpha = ring.sharedMaterial.GetColor("_TintColor").a;
                }
                if (Mathf.Abs(alpha - .25f) > .00001f) throw new Exception("Original fade curve did not reach native alpha .25 at t=.5: " + alpha);
                animation["RingHitAnim"].clip.SampleAnimation(root.gameObject, 1f / 12f);
                float scale = root.Find("round").localScale.x;
                if (Mathf.Abs(scale - .012f) > .000001f) throw new Exception("Original RingHitAnim scale failed: " + scale);
                File.WriteAllText("Documentation/ring26-validation.json", "{\"fadeAlphaAtHalfSecond\":" + alpha.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"hitScaleAtOneTwelfthSecond\":" + scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
                AssetDatabase.SaveAssets();
                Debug.Log("ORIGINAL26_RING_RENDERING_PASSED: native tint property and exact animation samples");
            }
            finally { UnityEngine.Object.DestroyImmediate(wrapper); }
        }
    }
}
