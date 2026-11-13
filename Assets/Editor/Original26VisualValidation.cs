using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using CandyFlick;
using CandyFlick.Original26;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class Original26VisualValidation
    {
        const string Key = "CandyFlickVisual26";
        static int stage;
        static double next;
        static CandyFlickGame game;
        static CandyFlickUI ui;
        static GameSession pausedSession;
        static bool transitionValidationDone;
        static double transitionDeadline;
        static readonly List<string> checks = new List<string>();
        static Original26VisualValidation()
        {
            if (SessionState.GetBool(Key, false))
            { EditorApplication.update += Tick; Application.logMessageReceived += OnLog; }
        }
        public static void Run()
        {
            SetReferenceResolution();
            EditorSceneManager.OpenScene(ProjectBuilder.ScenePath);
            Directory.CreateDirectory("Documentation/Visual26");
            if (File.Exists("Documentation/Visual26/failure.txt")) File.Delete("Documentation/Visual26/failure.txt");
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key+"HadStartKey",PlayerPrefs.HasKey("ProgramStartKey"));
            SessionState.SetBool(Key+"HadControlMode",PlayerPrefs.HasKey(CandyFlickGame.ControlModePreference));
            SessionState.SetInt(Key+"ControlMode",PlayerPrefs.GetInt(CandyFlickGame.ControlModePreference,2));
            PlayerPrefs.DeleteKey(CandyFlickGame.ControlModePreference);
            SessionState.SetBool(Key+"HadCameraFpsLock",PlayerPrefs.HasKey(CandyFlickGame.CameraFpsLockPreference));
            SessionState.SetInt(Key+"CameraFpsLock",PlayerPrefs.GetInt(CandyFlickGame.CameraFpsLockPreference,0));
            PlayerPrefs.DeleteKey(CandyFlickGame.CameraFpsLockPreference);
            SessionState.SetString(Key+"StartKey",PlayerPrefs.GetString("ProgramStartKey",""));
            EditorApplication.EnterPlaymode();
        }
        static void SetReferenceResolution(int width = 640, int height = 1136)
        {
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            object sizes = sizesType.BaseType.GetProperty("instance").GetValue(null);
            object group = sizesType.GetProperty("currentGroup").GetValue(sizes);
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
            object fixedResolution = Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"), "FixedResolution");
            object size = Activator.CreateInstance(sizeType, fixedResolution, width, height, "Original 2.6 reference");
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            EditorWindow view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
            view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
            view.Show();
        }
        static void Capture(string name) => ScreenCapture.CaptureScreenshot("Documentation/Visual26/" + name + ".png");
        static void Check(bool condition,string message)
        { if(!condition)throw new Exception(message);checks.Add(message); }
        static void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            File.WriteAllText("Documentation/Visual26/failure.txt", message + "\n" + stack); Finish(1);
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
            try
            {
                switch (stage++)
                {
                    case 0:
                        game = UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();
                        if (!game) { stage = 0; return; }
                        ui=game.GetComponent<CandyFlickUI>();
                        Application.runInBackground = true;
                        break;
                    case 1: Capture("menu"); break;
                    case 2: game.Show(GameScreen.Settings); break;
                    case 3: Capture("settings"); break;
                    case 4: game.StartGame(); break;
                    case 5: Capture("gameplay"); break;
                    case 6: game.Pause(); break;
                    case 7: Capture("pause"); break;
                    case 8: game.Resume(); game.Boosts.OnNewRound(3); break;
                    case 9: Capture("ring"); break;
                    case 10: game.Show(GameScreen.Results); break;
                    case 11: Capture("results-empty-fixture"); break;
                    case 12: game.Show(GameScreen.Marker); break;
                    case 13: Capture("marker"); break;
                    case 14: game.ReturnToMenu(); PlayerPrefs.DeleteKey("ProgramStartKey"); ui.MenuPlay(); break;
                    case 15:
                        Check(game.ScreenState==GameScreen.Tutorial && ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Welcome,"First Play opens original Welcome tutorial");
                        Capture("tutorial-welcome");break;
                    case 16: ui.SetTutorialPage(CandyFlickUI.TutorialPage.Start);break;
                    case 17:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Start,"Native GUI transition opens marker selection");Capture("tutorial-start");break;
                    case 18: ui.SetTutorialPage(CandyFlickUI.TutorialPage.CreateMarker);break;
                    case 19: Capture("tutorial-create");break;
                    case 20: ui.SetTutorialPage(CandyFlickUI.TutorialPage.Download);break;
                    case 21: Capture("tutorial-download");break;
                    case 22: ui.SetTutorialPage(CandyFlickUI.TutorialPage.Print);break;
                    case 23: Capture("tutorial-print");break;
                    case 24: ui.FinishTutorial();break;
                    case 25:
                        Check(game.ScreenState==GameScreen.Playing && Time.timeScale==1,"Finishing first-run tutorial starts gameplay and restores time");break;
                    case 26: game.Pause();pausedSession=game.Session;ui.OpenTutorial(false,false);break;
                    case 27: ui.FinishTutorial();break;
                    case 28:
                        Check(game.ScreenState==GameScreen.Playing && ReferenceEquals(game.Session,pausedSession),"Pause help returns to the existing game without resetting scores");
                        game.ReturnToMenu();game.Show(GameScreen.Settings);ui.ChangeSettingsPage("About");break;
                    case 29: ValidateAbout(); Capture("about");break;
                    case 30: ui.ChangeSettingsPage("Reset");break;
                    case 31: Capture("reset");break;
                    case 32: game.ReturnToMenu();ui.OpenTutorial(true,false);ui.CloseTutorial();break;
                    case 33:
                        Check(game.ScreenState==GameScreen.Menu && Time.timeScale==1,"Closing menu tutorial returns to menu");
                        game.StartGame();game.Boosts.OnNewRound(3);break;
                    case 34:
                        game.Boosts.Current.GetComponentInChildren<LegacyParticles26>().SetEmitting(true);break;
                    case 35: Capture("ring-sparkles");break;
                    case 36:
                        var ghostEffect=game.CandyGhost.Find("CandyGhost/SuperEffect");
                        ghostEffect.gameObject.SetActive(true);break;
                    case 37:
                        var emitter=game.CandyGhost.GetComponentInChildren<LegacyParticles26>();
                        Check(emitter && emitter.System.particleCount>0,"Original super-candy emitter produces live particles");Capture("super-candy");break;
                    case 38:
                        ui.OpenTargetOptions();break;
                    case 39:
                        Check(game.ScreenState==GameScreen.TargetOptions,"Get image opens original Show/Print/Download options");Capture("target-options");break;
                    case 40: ui.Back();break;
                    case 41:
                        Check(game.ScreenState==GameScreen.Playing,"Target options close back to gameplay");
                        File.WriteAllText("Documentation/screen26-validation.json",JsonUtility.ToJson(new Evidence{checks=checks.ToArray(),unity=Application.unityVersion},true));break;
                    case 42: game.ReturnToMenu();game.SetMode(LaunchMode.Slingshot);game.Show(GameScreen.Settings);ui.ChangeSettingsPage(null);break;
                    case 43: Capture("settings-slingshot");break;
                    case 44: game.ToggleControlMode();break;
                    case 45: Capture("settings-flick");break;
                    case 46: game.ToggleCameraFpsLock();break;
                    case 47: Capture("settings-camera-fps-on");break;
                    case 48: game.ToggleCameraFpsLock();break;
                    case 49: Capture("settings-camera-fps-off");break;
                    case 50: game.ReturnToMenu();ui.OpenTutorial(true,false);break;
                    case 51: ui.SetTutorialPage(CandyFlickUI.TutorialPage.CreateMarker);break;
                    case 52: ClickTutorial(new Vector2(320,524));break;
                    case 53:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Download,"Apple-device OK button opens Download through GUI input");
                        ClickTutorial(new Vector2(66,1070));break;
                    case 54:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.CreateMarker,"Download back button returns to marker choices");
                        ClickTutorial(new Vector2(320,1074));break;
                    case 55:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Print,"Printer OK button opens Print through GUI input");
                        ClickTutorial(new Vector2(66,1070));break;
                    case 56:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.CreateMarker,"Print back button returns to marker choices");
                        SetReferenceResolution(640,1422);break;
                    case 57: Capture("tutorial-create-tall");break;
                    case 58: ClickTutorial(new Vector2(66,1356));break;
                    case 59:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Start,"Marker-choice back button stays at the bottom on a tall screen and opens Start");
                        ui.SetTutorialPage(CandyFlickUI.TutorialPage.CreateMarker);break;
                    case 60: ClickTutorial(new Vector2(320,524));break;
                    case 61:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Download,"Apple-device OK remains interactive on a tall screen");
                        Capture("tutorial-download-tall");break;
                    case 62: ClickTutorial(new Vector2(66,1356));break;
                    case 63:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.CreateMarker,"Download back button stays at the bottom on a tall screen");
                        ClickTutorial(new Vector2(320,1074));break;
                    case 64:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.Print,"Printer OK remains interactive on a tall screen");
                        Capture("tutorial-print-tall");break;
                    case 65: ClickTutorial(new Vector2(66,1356));break;
                    case 66:
                        Check(ui.CurrentTutorialPage==CandyFlickUI.TutorialPage.CreateMarker,"Print back button stays at the bottom on a tall screen");
                        SetReferenceResolution();break;
                    case 67:
                        transitionDeadline=EditorApplication.timeSinceStartup+30;
                        // Game start cancels game/HUD coroutines. The AR bridge
                        // remains alive across both AR and non-AR menu switches.
                        game.ar.StartCoroutine(RunTransitionValidation());break;
                    case 68:
                        if(!transitionValidationDone) {
                            if(EditorApplication.timeSinceStartup>transitionDeadline)throw new Exception("Rendered transition validation timed out");
                            stage=68;
                        }
                        break;
                    default: Finish(0); break;
                }
                next = EditorApplication.timeSinceStartup + .6;
            }
            catch (Exception exception) { File.WriteAllText("Documentation/Visual26/failure.txt", exception.ToString()); Finish(1); }
        }
        static IEnumerator RunTransitionValidation()
        {
            IEnumerator routine=ValidateRenderedTransitions();
            while(true) {
                object current;
                try {
                    if(!routine.MoveNext()) { transitionValidationDone=true;yield break; }
                    current=routine.Current;
                } catch(Exception exception) {
                    File.WriteAllText("Documentation/Visual26/failure.txt",exception.ToString());Finish(1);yield break;
                }
                yield return current;
            }
        }
        static IEnumerator ValidateRenderedTransitions()
        {
            var screenTransition=(ScreenTransition26)typeof(CandyFlickUI).GetField("transition",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ui);
            var settings=typeof(CandyFlickUI).GetField("settingsPage",BindingFlags.Instance|BindingFlags.NonPublic);
            game.ReturnToMenu();settings.SetValue(ui,null);
            while(screenTransition.Fading)yield return new WaitForEndOfFrame();
            string[] names={"menu-settings","settings-credits","credits-settings","settings-menu","menu-gameplay","gameplay-menu"};
            Action[] actions={
                ()=>ClickTutorial(new Vector2(449.6f,832)),
                ()=>ui.ChangeSettingsPage("About"),
                ()=>ClickTutorial(new Vector2(79.2f,1056.8f)),
                ()=>ClickTutorial(new Vector2(79.2f,1056.8f)),
                ()=>ClickTutorial(new Vector2(323,638)),
                ()=>{game.Pause();ui.Back();}
            };
            GameScreen[] destinations={GameScreen.Settings,GameScreen.Settings,GameScreen.Settings,GameScreen.Menu,GameScreen.Playing,GameScreen.Menu};
            for(int route=0;route<names.Length;route++) {
                bool darkening=false,black=false,revealing=false,started=false,finished=false;
                actions[route]();
                for(int frame=0;frame<120;frame++) {
                    yield return new WaitForEndOfFrame();
                    float alpha=screenTransition.Alpha;
                    started|=screenTransition.Waiting || screenTransition.Fading;
                    bool captureOut=!darkening && screenTransition.Waiting && alpha>.2f && alpha<.9f;
                    bool captureBlack=!black && alpha>=1;
                    bool captureIn=!revealing && !screenTransition.Waiting && alpha>.2f && alpha<.9f;
                    if(captureOut || captureBlack || captureIn) {
                        Texture2D rendered=ScreenCapture.CaptureScreenshotAsTexture();
                        try {
                            if(captureBlack) {
                                foreach(Color32 pixel in rendered.GetPixels32())
                                    if(pixel.r>3 || pixel.g>3 || pixel.b>3)throw new Exception("Transition did not cover the full rendered screen in black: "+names[route]);
                            }
                            File.WriteAllBytes("Documentation/Visual26/transition-"+names[route]+(captureBlack?"-black":captureOut?"-out":"-in")+".png",rendered.EncodeToPNG());
                        } finally { UnityEngine.Object.Destroy(rendered); }
                        darkening|=captureOut;black|=captureBlack;revealing|=captureIn;
                    }
                    if(started && !screenTransition.Waiting && !screenTransition.Fading) { finished=true;break; }
                }
                Check(finished && darkening && black && revealing && game.ScreenState==destinations[route],
                    "Rendered "+names[route]+" transition fades out, covers the full screen in black, and fades in to the destination");
                if(route==1)Check((string)settings.GetValue(ui)=="About","Credits destination opens behind the black fade");
                if(route==2)Check(settings.GetValue(ui)==null,"Credits back transition returns to settings");
            }
        }
        // Send real IMGUI pointer events in rendered game pixels, bypassing only
        // the editor window's own toolbar/zoom transform.
        static void ClickTutorial(Vector2 nativePoint)
        {
            Rect safe=Screen.safeArea;float scale=CandyFlickUI.CanvasScale(safe);
            Vector2 point=new Vector2(safe.x+(safe.width/scale-640)*scale/2,Screen.height-safe.yMax)+nativePoint*scale;
            MethodInfo queue=typeof(EditorGUIUtility).GetMethod("QueueGameViewInputEvent",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            if(queue==null)throw new Exception("Unity Game View input queue is unavailable");
            foreach(var type in new[]{EventType.MouseDown,EventType.MouseUp})
                queue.Invoke(null,new object[]{new Event { type=type,mousePosition=point,button=0,clickCount=1 }});
        }
        static void ValidateAbout()
        {
            var textField=typeof(CandyFlickUI).GetField("text",BindingFlags.Instance|BindingFlags.NonPublic);
            var text=(TextManager26)textField.GetValue(ui);
            int original=text.CurrentLanguage;
            var fontMethod=typeof(CandyFlickUI).GetMethod("ApplyLanguageFont",BindingFlags.Instance|BindingFlags.NonPublic);
            GUISkin skin=Resources.Load<GUISkin>("skins/iPhone5Skin");
            try {
                for(int language=0;language<9;language++) {
                    text.ChangeLanguage(language);fontMethod.Invoke(ui,null);
                    foreach(float h in new[]{853f,1136f,1422f}) {
                        var title=new GUIStyle(skin.customStyles[43]) { alignment=TextAnchor.UpperCenter,wordWrap=true,fixedHeight=0 };
                        var body=new GUIStyle(skin.customStyles[42]) { alignment=TextAnchor.UpperCenter,fixedHeight=0 };
                        string copy=string.Join("\n",text.GetStringByKey("ABOUT_VERSION"),text.GetStringByKey("ABOUT_TEXT1"),text.GetStringByKey("ABOUT_TEXT2"),text.GetStringByKey("ABOUT_TEXT3"));
                        Rect[] boxes=CandyFlickUI.AboutLayout(text.GetStringByKey("ABOUT_TITLE"),copy,title,body,h);
                        Check(boxes[0].yMax<=boxes[1].y && boxes[1].yMax<=boxes[2].y && boxes[2].yMax<=boxes[3].y && boxes[3].yMax<=h-155,
                            "Credits fit without overlap: language "+language+", usable height "+h);
                        Check(boxes[1].height>=body.CalcHeight(new GUIContent(copy),boxes[1].width),"Credits text is fully measured: language "+language+", height "+h);
                    }
                }
                foreach(var safe in new[]{new Rect(0,0,640,1136),new Rect(0,24,1080,2300),new Rect(20,30,720,960)}) {
                    float scale=CandyFlickUI.CanvasScale(safe);
                    Check(scale*640<=safe.width+.01f && scale*1136<=safe.height+.01f,"Native secondary screens fit usable device area "+safe.size);
                }
            } finally { text.ChangeLanguage(original);fontMethod.Invoke(ui,null); }
        }
        static void Finish(int code)
        {
            if(code==0)File.WriteAllText("Documentation/screen26-validation.json",JsonUtility.ToJson(new Evidence{checks=checks.ToArray(),unity=Application.unityVersion},true));
            SessionState.SetBool(Key, false); EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            if(SessionState.GetBool(Key+"HadStartKey",false))PlayerPrefs.SetString("ProgramStartKey",SessionState.GetString(Key+"StartKey",""));
            else PlayerPrefs.DeleteKey("ProgramStartKey");
            PlayerPrefs.Save();
            if(SessionState.GetBool(Key+"HadControlMode",false))PlayerPrefs.SetInt(CandyFlickGame.ControlModePreference,SessionState.GetInt(Key+"ControlMode",2));
            else PlayerPrefs.DeleteKey(CandyFlickGame.ControlModePreference);
            if(SessionState.GetBool(Key+"HadCameraFpsLock",false))PlayerPrefs.SetInt(CandyFlickGame.CameraFpsLockPreference,SessionState.GetInt(Key+"CameraFpsLock",0));
            else PlayerPrefs.DeleteKey(CandyFlickGame.CameraFpsLockPreference);
            PlayerPrefs.Save();
            EditorApplication.ExitPlaymode();
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }
        [Serializable] sealed class Evidence { public string unity; public string[] checks; }
    }
}
