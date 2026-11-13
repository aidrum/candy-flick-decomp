using UnityEngine;

namespace CandyFlick
{
    public sealed class GameAssets : ScriptableObject
    {
        public GameObject omNom, candy, alternateCandy, slingshot;
        public Font font;
        public Font alternateFont;
        public TextAsset[] languages;
        public Texture2D menu, background, marker, markerAlternate, shadow, candyIcon;
        public AudioClip music, button, shoot, openMouth, closeMouth, chew, sad, ring, win;
        public AudioClip[] hits, misses;
        public AudioClip[] swishes;
        public AudioClip elasticShoot, elasticStretch, tap, loveTouch, maximumScore;
        public AudioClip cameraShutter; // SoundManager.cameraAudio (Camera.wav), played by GamePlayScreen.WaitAndShowEffect.
    }
}
