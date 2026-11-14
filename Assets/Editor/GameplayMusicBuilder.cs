using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class GameplayMusicBuilder
    {
        public static void Build()
        {
            const string source=@"D:\Downloads\candyflickmusic", output="Assets/Resources/GameplayMusic";
            Directory.CreateDirectory(output);
            var tracks=Directory.GetFiles(source,"*.wav").OrderBy(p=>p).Select(path=>
            {
                string destination=output+"/"+Path.GetFileName(path);File.Copy(path,destination,true);
                AssetDatabase.ImportAsset(destination,ImportAssetOptions.ForceSynchronousImport);
                var importer=(AudioImporter)AssetImporter.GetAtPath(destination);
                importer.forceToMono=false;importer.loadInBackground=true;
                var settings=importer.defaultSampleSettings;
                settings.loadType=AudioClipLoadType.Streaming;settings.compressionFormat=AudioCompressionFormat.MP3;
                settings.preloadAudioData=false;
                settings.quality=.75f;settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings=settings;
                importer.SetOverrideSampleSettings("Android",settings);importer.SaveAndReimport();
                if(importer.GetOverrideSampleSettings("Android").compressionFormat!=AudioCompressionFormat.MP3)
                    throw new Exception("Unity did not retain the Android MP3 setting: "+destination);
                var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(destination);
                string compression=typeof(Editor).Assembly.GetType("UnityEditor.AudioUtil").GetMethod("GetTargetPlatformSoundCompressionFormat").Invoke(null,new object[]{clip}).ToString();
                if(compression!="MP3")throw new Exception("Target-platform music is not MP3: "+destination+" ("+compression+")");
                using(var sha=SHA256.Create())return new Track{file=Path.GetFileName(path),sha256=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant(),compression=compression,seconds=clip.length,channels=clip.channels,hz=clip.frequency};
            }).ToArray();
            File.WriteAllText("Documentation/gameplay-music-source.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),tracks=tracks},true));
        }
        [Serializable]class Track{public string file,sha256,compression;public float seconds;public int channels,hz;}
        [Serializable]class Evidence{public string utc;public Track[] tracks;}
    }
}
