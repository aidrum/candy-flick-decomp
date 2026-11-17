using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickGame
    {
        public const string HideCandyPreference = "PortHideCandyInPictures";
        public const string InGameMusicPreference = "PortInGameMusic";
        public const string NonARPreference = "PortNonARMode";
        public const string GroundARPreference = "PortGroundARMode";
        public bool GroundARModeEnabled { get; private set; }
        public GroundTrackingSupport GroundSupport {get;private set;}
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
            GroundSupport=gameObject.AddComponent<GroundTrackingSupport>();
            HideCandyInPictures = PlayerPrefs.GetInt(HideCandyPreference, 1) == 1;
            InGameMusicEnabled = PlayerPrefs.GetInt(InGameMusicPreference, 1) == 1;
            NonARModeEnabled = PlayerPrefs.GetInt(NonARPreference, 0) == 1;
            GroundARModeEnabled = PlayerPrefs.GetInt(GroundARPreference, 0) == 1;
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
        public void ToggleGroundARMode()
        {
            if(!GroundARModeEnabled&&!GroundSupport.Supported)return;
            GroundARModeEnabled = !GroundARModeEnabled;
            PlayerPrefs.SetInt(GroundARPreference, GroundARModeEnabled ? 1 : 0); PlayerPrefs.Save();
        }
        public void ToggleNonARMode()
        {
            NonARModeEnabled = !NonARModeEnabled;
            PlayerPrefs.SetInt(NonARPreference, NonARModeEnabled ? 1 : 0); PlayerPrefs.Save();
        }
        public void ToggleARMode()
        {
            if(ScreenState!=GameScreen.Paused || IsLanMatch || IsPhotoMode)return;
            bool nonAR=!IsNonAR;
            float previousClearance=IsNonAR?NonARStage.GroundClearance:0;
            CancelCandyGesture();NonARBackground.Navigation.Cancel();PhotoVisibility.End();ar.End();
            NonARModeEnabled=IsNonAR=nonAR;IsAR=!nonAR;
            PlayerPrefs.SetInt(NonARPreference,nonAR?1:0);PlayerPrefs.Save();
            ConfigureNonARBackground();
            Actor.MoveGround((nonAR?NonARStage.GroundClearance:0)-previousClearance);Actor.FacePlayer();
            GameCamera.clearFlags=IsAR?CameraClearFlags.Depth:CameraClearFlags.SolidColor;
            GameCamera.backgroundColor=Color.black;SetWorldVisible(true);
            if(IsAR)ar.Begin();
            Time.timeScale=0;SyncGameplayMusic();
        }
        void ConfigureNonARBackground()
        {
            if (IsNonAR) NonARBackground.Begin(); else NonARBackground.End();
            stablePosition = Player.position; stableRotation = Player.rotation;
        }
        void StartGameplayMusic()
        {
            gameplayMusicSession = true; GameplayMusic.Stop(); gameplayMusicPaused = false;
            if (IsPhotoMode || IsLanMatch || (InGameMusicEnabled && MusicEnabled)) PickGameplayTrack();
            SyncGameplayMusic();
        }
        void PickGameplayTrack()
        {
            if(IsPhotoMode) { GameplayMusic.clip=Resources.Load<AudioClip>(PhotoTrackResource); return; }
            if(IsLanMatch) { GameplayMusic.clip=Resources.Load<AudioClip>(LanMultiplayer.TrackResource); return; }
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
            if (!gameplayMusicSession || (!InGameMusicEnabled && !IsLanMatch && !IsPhotoMode) || !MusicEnabled)
            { GameplayMusic.Stop(); gameplayMusicPaused = false; return; }
            if (!GameplayMusic.clip) PickGameplayTrack();
            if (IsPhotoMode || CanSimulate || ScreenState == GameScreen.Results)
            {
                if (gameplayMusicPaused) { GameplayMusic.UnPause(); gameplayMusicPaused = false; }
                else if (!GameplayMusic.isPlaying && GameplayMusic.clip) GameplayMusic.Play();
            }
            else if (GameplayMusic.isPlaying) { GameplayMusic.Pause(); gameplayMusicPaused = true; }
        }
    }
}
