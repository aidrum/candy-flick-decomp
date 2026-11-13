using System.IO;
using UnityEngine;
namespace CandyFlick.Original26
{
    public static class MarkerExport26
    {
        public static void Save()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var exporter = new AndroidJavaClass("com.aidrum.candyflick.MarkerExportActivity"))
                exporter.CallStatic("save", activity);
#elif UNITY_EDITOR
            string destination = UnityEditor.EditorUtility.SaveFilePanel("Save original target card", "", "candy-flick-marker", "png");
            if (!string.IsNullOrEmpty(destination)) File.Copy(Path.Combine(Application.streamingAssetsPath, "Raw/marker.png"), destination, true);
#endif
        }
        public static void Print()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var exporter = new AndroidJavaClass("com.aidrum.candyflick.MarkerExportActivity"))
                exporter.CallStatic("print", activity);
#elif UNITY_EDITOR
            Save();
#endif
        }
    }
}
