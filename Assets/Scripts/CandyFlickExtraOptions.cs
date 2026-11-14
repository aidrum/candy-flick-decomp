using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickGame
    {
        public const string HideCandyPreference = "PortHideCandyInPictures";
        public const string InGameMusicPreference = "PortInGameMusic";
        public const string NonARPreference = "PortNonARMode";
        public bool HideCandyInPictures { get; private set; }
        public bool InGameMusicEnabled { get; private set; }
        public bool NonARModeEnabled { get; private set; }
        public bool IsNonAR { get; private set; }
        public NonARStage NonARBackground { get; private set; }
        public PhotoCandyVisibility PhotoVisibility { get; private set; }
        public AudioSource GameplayMusic { get; private set; }
        AudioClip[] gameplayTracks;
        bool gameplayMusicSession, gameplayMusicPaused;

        void InitializeExtraOptions()
        {
            HideCandyInPictures = PlayerPrefs.GetInt(HideCandyPreference, 1) == 1;
            InGameMusicEnabled = PlayerPrefs.GetInt(InGameMusicPreference, 0) == 1;
            NonARModeEnabled = PlayerPrefs.GetInt(NonARPreference, 0) == 1;
            GameplayMusic = gameObject.AddComponent<AudioSource>();
            GameplayMusic.playOnAwake = false; GameplayMusic.loop = true; GameplayMusic.volume = .6f;
        }
        public void ToggleHideCandyInPictures()
        {
            HideCandyInPictures = !HideCandyInPictures;
            PlayerPrefs.SetInt(HideCandyPreference, HideCandyInPictures ? 1 : 0); PlayerPrefs.Save();
        }
        public void ToggleInGameMusic()
        {
            InGameMusicEnabled = !InGameMusicEnabled;
            PlayerPrefs.SetInt(InGameMusicPreference, InGameMusicEnabled ? 1 : 0); PlayerPrefs.Save();
            SyncGameplayMusic();
        }
        public void ToggleNonARMode()
        {
            NonARModeEnabled = !NonARModeEnabled;
            PlayerPrefs.SetInt(NonARPreference, NonARModeEnabled ? 1 : 0); PlayerPrefs.Save();
        }
        void ConfigureNonARBackground()
        {
            if (IsNonAR) NonARBackground.Begin(); else NonARBackground.End();
            stablePosition = Player.position; stableRotation = Player.rotation;
        }
        void StartGameplayMusic()
        {
            gameplayMusicSession = true; GameplayMusic.Stop(); gameplayMusicPaused = false;
            if (InGameMusicEnabled && MusicEnabled) PickGameplayTrack();
            SyncGameplayMusic();
        }
        void PickGameplayTrack()
        {
            if (gameplayTracks == null) gameplayTracks = Resources.LoadAll<AudioClip>("GameplayMusic");
            if (gameplayTracks.Length > 0) GameplayMusic.clip = gameplayTracks[Random.Range(0, gameplayTracks.Length)];
        }
        void StopGameplayMusic()
        {
            gameplayMusicSession = gameplayMusicPaused = false; GameplayMusic.Stop();
        }
        void SyncGameplayMusic()
        {
            if (!GameplayMusic) return;
            if (!gameplayMusicSession || !InGameMusicEnabled || !MusicEnabled)
            { GameplayMusic.Stop(); gameplayMusicPaused = false; return; }
            if (!GameplayMusic.clip) PickGameplayTrack();
            if (CanSimulate || ScreenState == GameScreen.Results)
            {
                if (gameplayMusicPaused) { GameplayMusic.UnPause(); gameplayMusicPaused = false; }
                else if (!GameplayMusic.isPlaying && GameplayMusic.clip) GameplayMusic.Play();
            }
            else if (GameplayMusic.isPlaying) { GameplayMusic.Pause(); gameplayMusicPaused = true; }
        }
    }
}
