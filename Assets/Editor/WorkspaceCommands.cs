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
                    case "Prepare": ProjectBuilder.PrepareProject(); break;
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
