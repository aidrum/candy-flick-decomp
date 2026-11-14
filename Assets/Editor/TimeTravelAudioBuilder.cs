using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class TimeTravelAudioBuilder
    {
        [Serializable] sealed class Reference
        {
            public int chewSamples, chewFrequency;
            public double chewSeconds;
            public Original[] original;
        }
        [Serializable] sealed class Original { public string path, sha256; }
        static string Hash(string path) { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }

        public static void References()
        {
            var assets=Resources.Load<GameAssets>("GameAssets");
            var clips=new[]{ assets.openMouth,assets.closeMouth,assets.chew,assets.sad,assets.tap,assets.loveTouch };
            var reference=new Reference { chewSamples=assets.chew.samples,chewFrequency=assets.chew.frequency,
                chewSeconds=(double)assets.chew.samples/assets.chew.frequency,
                original=clips.Select(c=>AssetDatabase.GetAssetPath(c)).Select(p=>new Original { path=p,sha256=Hash(p) }).ToArray() };
            File.WriteAllText("Documentation/timetravel-audio-reference.json",JsonUtility.ToJson(reference,true));
            Debug.Log("TIME_TRAVEL_AUDIO_REFERENCE: chew="+reference.chewSeconds+" seconds");
        }

        [MenuItem("Candy Flick/Import Time Travel audio")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/Resources/TimeTravelAudio"); AssetDatabase.Refresh();
            foreach(NommieSkin skin in Enum.GetValues(typeof(NommieSkin)))
            {
                if(skin==NommieSkin.Original)continue;
                AudioClip Load(string suffix)
                {
                    string path="Assets/TimeTravel/Audio/"+skin+"_"+suffix+".wav";
                    var importer=(AudioImporter)AssetImporter.GetAtPath(path);
                    if(!importer)throw new Exception("Missing processed sound: "+path);
                    var settings=importer.defaultSampleSettings;
                    settings.loadType=AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat=AudioCompressionFormat.PCM;
                    settings.sampleRateSetting=AudioSampleRateSetting.PreserveSampleRate;
                    settings.preloadAudioData=true;
                    importer.defaultSampleSettings=settings; importer.forceToMono=false;
                    importer.loadInBackground=false; importer.SaveAndReimport();
                    return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                var voice=ScriptableObject.CreateInstance<TimeTravelVoice>(); voice.name=skin.ToString();
                voice.openMouth=Load("mouthOpen"); voice.closeMouth=Load("mouthClose");
                voice.chew=Load("chewing"); voice.sad=Load("sad"); voice.tap=Load("excited"); voice.loveTouch=Load("greeting");
                string output="Assets/Resources/TimeTravelAudio/"+skin+".asset";
                var existing=AssetDatabase.LoadAssetAtPath<TimeTravelVoice>(output);
                if(existing) { EditorUtility.CopySerialized(voice,existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(voice); }
                else AssetDatabase.CreateAsset(voice,output);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("TIME_TRAVEL_AUDIO_IMPORTED: 7 voices, 42 clips");
        }
    }
}
