using UnityEngine;

namespace CandyFlick
{
    public enum NommieSound { Open, Close, Chew, Sad, Tap, Pet }

    public sealed class TimeTravelVoice : ScriptableObject
    {
        public AudioClip openMouth, closeMouth, chew, sad, tap, loveTouch;

        public static AudioClip Resolve(NommieSound sound, TimeTravelVoice voice, GameAssets original)
        {
            switch (sound)
            {
                case NommieSound.Open: return voice ? voice.openMouth : original.openMouth;
                case NommieSound.Close: return voice ? voice.closeMouth : original.closeMouth;
                case NommieSound.Chew: return voice ? voice.chew : original.chew;
                case NommieSound.Sad: return voice ? voice.sad : original.sad;
                case NommieSound.Tap: return voice ? voice.tap : original.tap;
                case NommieSound.Pet: return voice ? voice.loveTouch : original.loveTouch;
                default: return null;
            }
        }
    }
}
