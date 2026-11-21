using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class AndroidBuildSigning
    {
        static AndroidBuildSigning()=>BuildPlayerWindow.RegisterBuildPlayerHandler(BuildFromWindow);

        static void BuildFromWindow(BuildPlayerOptions options)
        {
            using(ForBuild(options.target))BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
        }
        public static IDisposable ForBuild(BuildTarget target)
        {
            bool custom=PlayerSettings.Android.useCustomKeystore;
            string path=target==BuildTarget.Android&&custom?ConfiguredKeystorePath():null;
            return new SigningOverride(NeedsDebugKey(target,custom,path));
        }
        internal static bool NeedsDebugKey(BuildTarget target,bool custom,string path)
            =>target==BuildTarget.Android&&custom&&!File.Exists(path);

        static string ConfiguredKeystorePath()
        {
            string name=PlayerSettings.Android.keystoreName;
            if(string.IsNullOrWhiteSpace(name))return name;
            // keystoreName can be relative to either the project or Unity's dedicated
            // keystore directory. Use the engine's resolver so identical names cannot
            // accidentally select a different file or trigger an unnecessary fallback.
            var resolve=typeof(PlayerSettings.Android).GetMethod("GetAndroidKeystoreFullPath",BindingFlags.Static|BindingFlags.NonPublic);
            if(resolve!=null)return (string)resolve.Invoke(null,null);
            if(Path.IsPathRooted(name))return name;
            string project=Path.GetFullPath(Path.Combine(Application.dataPath,"..",name));
            if(File.Exists(project))return project;
            var tools=Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
            string dedicated=tools?.GetProperty("keystoresDedicatedLocation",BindingFlags.Public|BindingFlags.Static)?.GetValue(null) as string;
            return string.IsNullOrEmpty(dedicated)?project:Path.Combine(dedicated,name);
        }
        internal sealed class SigningOverride : IDisposable
        {
            bool restoreCustom;
            internal SigningOverride(bool fallback,bool log=true)
            {
                restoreCustom=fallback&&PlayerSettings.Android.useCustomKeystore;
                if(!restoreCustom)return;
                PlayerSettings.Android.useCustomKeystore=false;
                if(log)Debug.Log("Configured Android keystore is unavailable on this machine. Using Unity's debug key for this build; saved signing settings will be restored afterwards.");
            }
            public void Dispose()
            {
                if(!restoreCustom)return;
                PlayerSettings.Android.useCustomKeystore=true;restoreCustom=false;
            }
        }
    }
}
