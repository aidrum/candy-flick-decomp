using System;
using System.IO;
using UnityEditor;

namespace CandyFlickEditor
{
    // Local, explicit command queue for validation while this project is open in the editor.
    [InitializeOnLoad]
    public static class WorkspaceCommands
    {
        const string Request = "Temp/candyflick-command.txt";
        const string Result = "Temp/candyflick-command-result.txt";
        static WorkspaceCommands() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
            if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string command = File.ReadAllText(Request).Trim(); File.Delete(Request);
            try
            {
                switch (command)
                {
                    case "SkinPerfBefore": SkinPerformanceValidation.Run("before"); break;
                    case "SkinPerfAfter": SkinPerformanceValidation.Run("after"); break;
                    case "BuildSettingsTest": BuildSettingsValidation.Run(); break;
                    case "TutorialModesTest": TutorialModeValidation.Run(); break;
                    case "PhotoMenuAssets": PhotoMenuAssetsBuilder.Build(); break;
                    case "UpdateCheckTest": UpdateCheckerValidation.Run(); break;
                    case "OmNelleImport": OmNelleAssetsBuilder.Build(); break;
                    case "PhotoCheck": PhotoModeValidation.Run(); break;
                    case "RestoreTestAudio": UnityEngine.AudioListener.pause=SessionState.GetBool("CandyFlickLanTestAudio",false); UnityEngine.AudioListener.volume=SessionState.GetFloat("CandyFlickLanTestVolume",1); break;
                    case "SlingshotTest": SlingshotValidation.Run(); break;
                    case "SlingshotAssets": SlingshotAssetsBuilder.Build(); break;
                    case "AchievementsTest": AchievementValidation.Run(); break;
                    case "PrehistoricIdles": PrehistoricIdleCapture.Run(); break;
                    case "CandyTest": CandySkinValidation.Run(); break;
                    case "CandyAssets": CandySkinAssetsBuilder.Build(); break;
                    case "CandyPortraits": CandyPortraitsBuilder.Build(); break;
                    case "Prepare": ProjectBuilder.PrepareProject(); break;
                    case "SkinsImport": TimeTravelSkinBuilder.Build(); break;
                    case "SkinMenuAssets": SkinMenuAssetsBuilder.Build(); break;
                    case "MenuTest": SkinMenuValidation.Run(); break;
                    case "LanCheck": LanValidation.RunWithoutCaptures(); break;
                    case "LanTest": LanValidation.Run(); break;
                    case "LanMusic": LanMusicBuilder.Build(); break;
                    case "MusicImport": GameplayMusicBuilder.Build(); break;
                    case "OptionsTest": ExtraOptionsValidation.Run(); break;
                    case "NonARTest": NonARValidation.Run(); break;
                    case "NonARControlsTest": NonARControlsValidation.Run(); break;
                    case "DragEdgeTest": SlingshotEdgeValidation.Run(); break;
                    case "GroundCheck": GroundARValidation.RunWithoutCaptures(); break;
                    case "GroundARTest": GroundARValidation.Run(); break;
                    case "ARToggleTest": ARToggleValidation.Run(); break;
                    case "SuperCandyTest": SuperCandyValidation.Run(); break;
                    case "RestartAnimationTest": RestartAnimationValidation.Run(); break;
                    case "EarlyMissTest": EarlyMissValidation.Run(); break;
                    case "CreditsTest": ModelCreditsValidation.Run(); break;
                    case "WidePointerTest": DeviceUIValidation.WidePointer(); break;
                    case "DeviceUITest": DeviceUIValidation.Run(); break;
                    case "MenuArtworkTest": MainMenuArtworkValidation.Run(); break;
                    case "LoadingTest": LoadingScreenValidation.Run(); break;
                    case "NonARLighting": NonARLightingBuilder.Apply(); break;
                    case "SkinsTest": TimeTravelSkinValidation.Run(); break;
                    case "AudioReference": TimeTravelAudioBuilder.References(); break;
                    case "AudioImport": TimeTravelAudioBuilder.Build(); break;
                    case "Native": Original26Validation.Run(); break;
                    case "Ring": Original26Compatibility.ValidateRing(); break;
                    case "Test": SmokeValidation.Run(); break;
                    case "Visual": Original26VisualValidation.Run(); break;
                    case "Vuforia": VuforiaValidation.Run(); break;
                    case "Android": ProjectBuilder.BuildAndroid(); break;
                    default: throw new ArgumentException("Unknown workspace command: " + command);
                }
                File.WriteAllText(Result, command == "Test" ? "Test: started; completion evidence is Documentation/validation.json or validation-failure.txt" : command + ": OK");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Result, command + ": FAILED\n" + exception);
                UnityEngine.Debug.LogException(exception);
            }
        }
    }
}
