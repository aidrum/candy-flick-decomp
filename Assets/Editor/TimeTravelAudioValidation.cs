using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CandyFlick;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class TimeTravelAudioValidation
    {
        [Serializable] sealed class Manifest { public double chewSeconds,sadDelaySeconds; public Entry[] clips; public Original[] originalAudioPreserved; }
        [Serializable] sealed class Entry { public string skin,cue,output,sha256; public int frequency,channels,frames,prefixSilenceFrames; }
        [Serializable] sealed class Original { public string path,sha256; }
        [Serializable] sealed class Evidence { public string unity,utc; public string[] checks; }
        static readonly List<string> checks=new List<string>();
        static Manifest manifest;
        static readonly string[] suffixes={"mouthOpen","mouthClose","chewing","sad","excited","greeting"};
        static void Check(bool value,string message) { if(!value)throw new Exception(message); checks.Add(message); }
        static string Hash(string path) { using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }

        public static void CheckSkin(CandyFlickGame game,NommieSkin skin)
        {
            if(skin==NommieSkin.OmNelle){OmNelleValidation.CheckAudio(game);Check(true,"Om Nelle has five supplied PCM cues and shares original chewing; no missing cues");return;}
            if(manifest==null)manifest=JsonUtility.FromJson<Manifest>(File.ReadAllText("Documentation/timetravel-audio-source.json"));
            Check(skin==NommieSkin.Original?!game.Skins.Voice:game.Skins.Voice!=null,skin+" selects the correct voice profile");
            foreach(NommieSound cue in Enum.GetValues(typeof(NommieSound)))
            {
                int index=(int)cue;
                AudioClip clip=game.Skins.Sound(cue);
                string expected=skin==NommieSkin.Original?manifest.originalAudioPreserved[index].path
                    :"Assets/TimeTravel/Audio/"+skin+"_"+suffixes[index]+".wav";
                Check(clip && AssetDatabase.GetAssetPath(clip)==expected,skin+" routes "+cue+" to "+expected);
                if(skin==NommieSkin.Original)continue;
                Entry entry=manifest.clips.Single(e=>e.skin==skin.ToString()&&e.cue==suffixes[index]);
                Check(clip.samples==entry.frames && clip.frequency==entry.frequency && clip.channels==entry.channels,skin+" "+cue+" imported sample count/rate/channels match WAV");
                Check(Hash(expected)==entry.sha256,skin+" "+cue+" processed WAV hash matches");
                var importer=(AudioImporter)AssetImporter.GetAtPath(expected);
                var settings=importer.GetOverrideSampleSettings("Android");
                Check(settings.compressionFormat==AudioCompressionFormat.PCM && settings.loadType==AudioClipLoadType.DecompressOnLoad
                    && settings.preloadAudioData,skin+" "+cue+" preserves PCM timing on Android");
                Check(clip.LoadAudioData(),skin+" "+cue+" audio loads");
                var pcm=new float[clip.samples*clip.channels];
                Check(clip.GetData(pcm,0) && pcm.Any(v=>Mathf.Abs(v)>.001f),skin+" "+cue+" contains decoded audio");
                if(cue==NommieSound.Chew)Check(Math.Abs((double)clip.samples/clip.frequency-manifest.chewSeconds)<=.5/clip.frequency+1e-9,
                    skin+" eating duration matches Candy Flick within half a sample");
                if(cue==NommieSound.Sad)Check(pcm.Take(entry.prefixSilenceFrames*clip.channels).All(v=>v==0)
                    && Math.Abs((double)entry.prefixSilenceFrames/clip.frequency-manifest.sadDelaySeconds)<=.5/clip.frequency+1e-9,
                    skin+" sad audio starts with the original added silence");
            }
        }
        public static void Save()
        {
            foreach(var original in manifest.originalAudioPreserved)
                Check(Hash(original.path)==original.sha256,"Original Om Nom sound unchanged: "+original.path);
            File.WriteAllText("Documentation/timetravel-audio-validation.json",JsonUtility.ToJson(new Evidence {
                unity=Application.unityVersion,utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()
            },true));
        }
    }
}
