using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class BuildSettingsValidation
    {
        [MenuItem("Candy Flick/Validate build settings preservation")]
        public static void Run()
        {
            Directory.CreateDirectory("Documentation");
            var checks=new List<string>();
            var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            void Unchanged(string name)
            {
                string before=EditorJsonUtility.ToJson(settings);
                ProjectBuilder.ConfigureAndroid();
                if(EditorJsonUtility.ToJson(settings)!=before)throw new Exception(name+": Android preparation changed Player Settings");
                checks.Add(name+": every serialized Player Setting is preserved");
            }
            Unchanged("Current project settings");
            string name=PlayerSettings.productName,company=PlayerSettings.companyName,version=PlayerSettings.bundleVersion;
            string identifier=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            int code=PlayerSettings.Android.bundleVersionCode;
            bool customKeystore=PlayerSettings.Android.useCustomKeystore;
            try
            {
                PlayerSettings.productName="Custom build name";
                PlayerSettings.companyName="Custom build company";
                PlayerSettings.bundleVersion="9.8.7";
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.example.custombuild");
                PlayerSettings.Android.bundleVersionCode=987;
                PlayerSettings.Android.useCustomKeystore=!customKeystore;
                Unchanged("Custom name, company, version, package, version code and signing choice");
                Unchanged("Repeated build preparation");
            }
            finally
            {
                PlayerSettings.productName=name;PlayerSettings.companyName=company;PlayerSettings.bundleVersion=version;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,identifier);
                PlayerSettings.Android.bundleVersionCode=code;PlayerSettings.Android.useCustomKeystore=customKeystore;
                AssetDatabase.SaveAssets();
            }
            File.WriteAllText("Documentation/build-settings-validation.json",JsonUtility.ToJson(new Evidence{
                utc=DateTime.UtcNow.ToString("o"),productName=PlayerSettings.productName,version=PlayerSettings.bundleVersion,
                package=PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android),checks=checks.ToArray()},true));
            Debug.Log("CANDY_FLICK_BUILD_SETTINGS_PRESERVED: "+checks.Count+" complete Player Settings comparisons passed.");
        }
        [Serializable]class Evidence{public string utc,productName,version,package;public string[] checks;}
    }
}
