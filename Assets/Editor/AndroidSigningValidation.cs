using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class AndroidSigningValidation
    {
        [MenuItem("Candy Flick/Validate Android signing fallback")]
        public static void Run()
        {
            var checks=new List<string>();
            void Check(bool valid,string message){if(!valid)throw new Exception(message);checks.Add(message);}
            const string folder="Temp/AndroidSigningValidation";
            Directory.CreateDirectory(folder);Directory.CreateDirectory("Documentation");
            string present=Path.GetFullPath(Path.Combine(folder,"fixture.keystore")),missing=Path.GetFullPath(Path.Combine(folder,Guid.NewGuid()+".keystore"));
            File.WriteAllBytes(present,Array.Empty<byte>()); // Existence only: no private key or credentials are read.
            Check(!AndroidBuildSigning.NeedsDebugKey(BuildTarget.Android,true,present),"An existing configured keystore retains custom signing, even if its contents or passwords are invalid");
            Check(!AndroidBuildSigning.NeedsDebugKey(BuildTarget.Android,true,Path.Combine(folder,"fixture.keystore")),"An existing project-relative path retains custom signing");
            Check(AndroidBuildSigning.NeedsDebugKey(BuildTarget.Android,true,missing),"Missing keystore selects debug signing");
            Check(AndroidBuildSigning.NeedsDebugKey(BuildTarget.Android,true,""),"Empty custom keystore path selects debug signing");
            Check(!AndroidBuildSigning.NeedsDebugKey(BuildTarget.Android,false,present),"Explicit debug signing is respected when a keystore exists");
            Check(!AndroidBuildSigning.NeedsDebugKey(BuildTarget.StandaloneWindows64,true,missing),"Non-Android builds do not change signing");
            bool original=PlayerSettings.Android.useCustomKeystore;
            var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            string initial=EditorJsonUtility.ToJson(settings);
            try
            {
                PlayerSettings.Android.useCustomKeystore=true;
                string configured=EditorJsonUtility.ToJson(settings);
                using(new AndroidBuildSigning.SigningOverride(false,false))
                    Check(PlayerSettings.Android.useCustomKeystore,"Normal custom-signed build keeps the signing choice enabled");
                using(new AndroidBuildSigning.SigningOverride(true,false))
                {
                    Check(!PlayerSettings.Android.useCustomKeystore,"Missing-key build temporarily uses Unity debug signing");
                    using(new AndroidBuildSigning.SigningOverride(true,false)){}
                    Check(!PlayerSettings.Android.useCustomKeystore,"Nested build scopes cannot restore custom signing early");
                }
                Check(EditorJsonUtility.ToJson(settings)==configured,"Successful build restores every serialized Player Setting, including the configured key path and alias");
                try
                {
                    using(new AndroidBuildSigning.SigningOverride(true,false))throw new OperationCanceledException("Simulated build cancellation/failure");
                }
                catch(OperationCanceledException){}
                Check(EditorJsonUtility.ToJson(settings)==configured,"Failed or cancelled build restores every serialized Player Setting");
                PlayerSettings.Android.useCustomKeystore=false;
                using(new AndroidBuildSigning.SigningOverride(true,false)){}
                Check(!PlayerSettings.Android.useCustomKeystore,"Explicit debug signing remains selected after a build");
            }
            finally{PlayerSettings.Android.useCustomKeystore=original;}
            Check(EditorJsonUtility.ToJson(settings)==initial,"Validation leaves the user's original configuration intact");
            using(AndroidBuildSigning.ForBuild(BuildTarget.Android)){}
            Check(EditorJsonUtility.ToJson(settings)==initial,"Configured keystore path resolution and a real build scope preserve user settings");
            File.WriteAllText("Documentation/android-signing-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));
            Debug.Log("CANDY_FLICK_SIGNING_VALIDATED: "+checks.Count+" checks passed. No private keys or passwords were read.");
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
