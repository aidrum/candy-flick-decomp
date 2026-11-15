using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class LanMusicBuilder
    {
        public static void Build()
        {
            const string source=@"D:\Downloads\CtR Music Video\parkour_music_track_1.ogg";
            const string output="Assets/Resources/MultiplayerMusic/parkour_music_track_1.ogg";
            Directory.CreateDirectory(Path.GetDirectoryName(output));File.Copy(source,output,true);
            AssetDatabase.ImportAsset(output,ImportAssetOptions.ForceSynchronousImport);
            var importer=(AudioImporter)AssetImporter.GetAtPath(output);importer.forceToMono=false;importer.loadInBackground=true;
            var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.MP3;
            settings.quality=.75f;settings.preloadAudioData=false;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings=settings;importer.SetOverrideSampleSettings("Android",settings);importer.SaveAndReimport();
            var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(output);
            string format=typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil").GetMethod("GetTargetPlatformSoundCompressionFormat").Invoke(null,new object[]{clip}).ToString();
            if(format!="MP3")throw new Exception("LAN music did not import as MP3: "+format);
            File.WriteAllText("Documentation/lan-music.json",JsonUtility.ToJson(new Evidence{source=source,resource=CandyFlick.LanMultiplayer.TrackResource,compression=format,seconds=clip.length},true));
        }
        [Serializable]class Evidence{public string source,resource,compression;public float seconds;}
    }
}
