using System;
using UnityEngine;
using CandyFlick.Original26;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        public enum TutorialPage { Welcome, Start, CreateMarker, Download, Print }
        public TutorialPage CurrentTutorialPage { get; private set; }
        readonly ScreenTransition26 transition = new ScreenTransition26();
        bool tutorialReturnToMenu, tutorialStartsNewGame;
        float resetHolding;
        bool resetArmed, resetPointerDown;
        bool gameCenterFromResults;

        int NativeFontSize(string name) => Array.Find(fontProfiles.languages[text.CurrentLanguage].sizes,
            entry => entry.method == "FontSizeManager$$get_" + name).size;
        public void MenuPlay()
        {
            if (game.NonARModeEnabled || game.GroundARModeEnabled) { StartPlatformGame(); return; }
            if (!PlayerPrefs.HasKey("ProgramStartKey")) OpenTutorial(false, true);
            else StartPlatformGame();
        }
        void StartPlatformGame()
        {
            if (game.NonARModeEnabled) { game.StartGame(); return; }
#if UNITY_ANDROID && !UNITY_EDITOR
            game.StartGame(true);
#else
            game.StartGame(game.GroundARModeEnabled);
#endif
        }
        public void OpenTutorial(bool fromMenu, bool startsNewGame)
        {
            Init();
            tutorialReturnToMenu = fromMenu; tutorialStartsNewGame = startsNewGame || game.ScreenState == GameScreen.Menu;
            PlayerPrefs.SetString("ProgramStartKey", ""); PlayerPrefs.Save();
            CurrentTutorialPage = TutorialPage.Welcome;
            game.Show(GameScreen.Tutorial); Time.timeScale = 0;
        }
        public void SetTutorialPage(TutorialPage page) => transition.Begin(() => CurrentTutorialPage = page);
        public void CloseTutorial() => transition.Begin(() =>
        {
            if (tutorialReturnToMenu) game.ReturnToMenu();
            else if (tutorialStartsNewGame) StartPlatformGame();
            else game.Resume();
        });
        public void FinishTutorial()
        {
            tutorialReturnToMenu = false; CloseTutorial();
        }
        public void Back()
        {
            if(game.ScreenState==GameScreen.LanLobby) { transition.Begin(game.LAN.LeaveRoom);return; }
            if(game.ScreenState==GameScreen.Skins) { CloseSkins();return; }
            if (game.ScreenState == GameScreen.TargetOptions || game.ScreenState == GameScreen.Marker)
            { transition.Begin(game.Resume); return; }
            if (game.ScreenState == GameScreen.Tutorial || game.ScreenState == GameScreen.Help)
            {
                if (CurrentTutorialPage == TutorialPage.Welcome || CurrentTutorialPage == TutorialPage.Start) CloseTutorial();
                else SetTutorialPage(CurrentTutorialPage == TutorialPage.CreateMarker ? TutorialPage.Start : TutorialPage.CreateMarker);
            }
            else if (settingsPage == "ModelCredits") ChangeSettingsPage("About");
            else if ((settingsPage == "GameCenter" || settingsPage == "Achievements") && gameCenterFromResults)
                transition.Begin(() => { settingsPage = null; game.Show(GameScreen.Results); });
            else if (settingsPage != null) ChangeSettingsPage(null);
            else transition.Begin(game.ReturnToMenu);
        }
        public void OpenTargetOptions() => transition.Begin(() => { game.Show(GameScreen.TargetOptions); Time.timeScale = 0; });
        void DrawTargetOptions()
        {
            Background(Texture("all/background"));
            GUI.DrawTexture(R("MarkerScreen$$GetBoardRect"), Texture("ipad2/marker_screen/board_marker"));
            GUI.DrawTexture(R("MarkerScreen$$GetArrowRect"), Texture("all/arrow"));
            GUI.Label(R("MarkerScreen$$GetLabelRect"), T("MARKER_LABEL"), gameSkin.GetStyle("label_marker"));
            GUIStyle style = new GUIStyle(gameSkin.GetStyle("main_btn")) { fontSize = NativeFontSize("AboutTitleFS") };
            GUILayout.BeginArea(R("MarkerScreen$$GetButtonsRect"));GUILayout.BeginVertical();
            if(GUILayout.Button("",style,GUILayout.Width(Dimension("MarkerScreen$$GetButtonsWidth")),GUILayout.Height(Dimension("MarkerScreen$$GetButtonsHeight"))))
            { game.Play(game.assets.button);transition.Begin(()=>game.Show(GameScreen.Marker)); }
            Outline(GUILayoutUtility.GetLastRect(),T("SHOW"),style);
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("",style,GUILayout.Width(Dimension("MarkerScreen$$GetButtonsWidth")),GUILayout.Height(Dimension("MarkerScreen$$GetButtonsHeight")))) { game.Play(game.assets.button);MarkerExport26.Print(); }
            Outline(GUILayoutUtility.GetLastRect(),T("PRINT"),style);
            GUILayout.FlexibleSpace();
            if(GUILayout.Button("",style,GUILayout.Width(Dimension("MarkerScreen$$GetButtonsWidth")),GUILayout.Height(Dimension("MarkerScreen$$GetButtonsHeight")))) { game.Play(game.assets.button);MarkerExport26.Save(); }
            Outline(GUILayoutUtility.GetLastRect(),T("DOWNLOAD"),style);
            GUILayout.EndVertical();GUILayout.EndArea();
            if(Button(R("MarkerScreen$$GetOkButtonRect",true),gameSkin.GetStyle("ok_btn")))transition.Begin(game.Resume);
        }
        void TutorialImage(string screen, string method, string image)
            => GUI.DrawTexture(R(screen + "$$" + method), Texture(DevicePath("tutorial") + image));
        void TutorialLabel(string screen, string method, string key, string style = "label_tutor1_1")
            => GUI.Label(R(screen + "$$" + method), T(key), gameSkin.GetStyle(style));
        void TutorialButton(string screen, string method, string style, Action action, string key = null)
        {
            Rect rect = R(screen + "$$" + method, style == "back_btn" || style == "next2_btn");
            GUIStyle buttonStyle = gameSkin.GetStyle(style);
            if (Button(rect, buttonStyle)) action();
            if (key != null)
            {
                // Native OutlineWriter explicitly supplies white fill, including
                // short_btn whose recovered text color is black.
                var labelStyle = new GUIStyle(buttonStyle);
                labelStyle.normal.textColor = Color.white;
                Outline(rect, T(key), labelStyle);
            }
        }
        void DrawTutorial()
        {
            Background(game.assets.background);
            gameSkin.GetStyle("label_tutor1_1").fontSize = NativeFontSize(CurrentTutorialPage == TutorialPage.Welcome
                ? "TutorialWelcomeFontSize" : CurrentTutorialPage == TutorialPage.Download ? "TutorialFontSizeDwnldScreen" : "TutorialFontSize");
            gameSkin.GetStyle("label_tutor1_2").fontSize = NativeFontSize("TutorialFontSize");
            gameSkin.GetStyle("short_btn").fontSize = NativeFontSize("TutorialShortBtnSize");
            gameSkin.GetStyle("main_btn").fontSize = NativeFontSize("TutorialFontSize");
            gameSkin.GetStyle("orLabel").fontSize = NativeFontSize("TutorialOrFontSize");
            string screen;
            switch (CurrentTutorialPage)
            {
                case TutorialPage.Welcome:
                    screen = "TutorWelcomeScreen";
                    TutorialImage(screen, "GetHandIphoneRect", "hand_iphone");
                    TutorialLabel(screen, "GetFirstTitleRect", "TUTOR_WELCOME");
                    TutorialButton(screen, "GetNextBtnRect", "next_btn", () => SetTutorialPage(TutorialPage.Start));
                    break;
                case TutorialPage.Start:
                    screen = "TutorStartScreen";
                    TutorialImage(screen, "GetMarkersRect", "two_markers");
                    TutorialLabel(screen, "GetTitleRect", "TUTOR_TITLE");
                    TutorialLabel(screen, "GetOrRect", "TUTOR_OR", "orLabel");
                    TutorialLabel(screen, "GetHaveMarkerRect", "TUTOR_HAVE_MARKER", "label_tutor1_2");
                    TutorialLabel(screen, "GetImgRect", "TUTOR_GETIMG", "label_tutor1_2");
                    TutorialButton(screen, "GetHaveMarkerBtnRect", "next_btn", FinishTutorial);
                    TutorialButton(screen, "GetImgBtnRect", "next_btn", () => SetTutorialPage(TutorialPage.CreateMarker));
                    break;
                case TutorialPage.CreateMarker:
                    screen = "TutorCreateMarkerScreen";
                    TutorialImage(screen, "GetIphoneIpaRect", "ipad_marker");
                    TutorialImage(screen, "GetPrinterRect", "printer");
                    TutorialImage(screen, "GetDotLineRect", "dot_line");
                    TutorialImage(screen, "GetOrImgRect", "or");
                    TutorialLabel(screen, "GetOrLabelRect", "TUTOR_OR", "orLabel");
                    TutorialLabel(screen, "GetFirstTitleRect", "TUTOR_ON_IPA");
                    TutorialLabel(screen, "GetSecTitleRect", "TUTOR_GOT_PRINTER");
                    TutorialButton(screen, "GetOk1BtnRect", "short_btn", () => SetTutorialPage(TutorialPage.Download), "OK");
                    TutorialButton(screen, "GetOk2BtnRect", "short_btn", () => SetTutorialPage(TutorialPage.Print), "OK");
                    TutorialButton(screen, "GetBackRect", "back_btn", () => SetTutorialPage(TutorialPage.Start));
                    break;
                case TutorialPage.Download:
                    screen = "TutorDownloadScreen";
                    TutorialImage(screen, "GetAppStoreRect", "app_store");
                    TutorialImage(screen, "GetIpadMarkerRect", "big_marker_ipad");
                    TutorialLabel(screen, "GetTitle1Rect", "TUTOR_DWNLD_ANOTHER_DEV");
                    TutorialLabel(screen, "GetTitle2Rect", "TUTOR_USE_PIC");
                    TutorialButton(screen, "GetBackRect", "back_btn", () => SetTutorialPage(TutorialPage.CreateMarker));
                    TutorialButton(screen, "GetOkRect", "next2_btn", FinishTutorial);
                    break;
                case TutorialPage.Print:
                    screen = "TutorPrintScreen";
                    TutorialImage(screen, "GetPhoneMacPrinterRect", "phone_mac_printer");
                    TutorialImage(screen, "GetWebPrinterRect", "send_to_printer");
                    TutorialImage(screen, "GetDotLineRect", "dot_line");
                    TutorialImage(screen, "GetOrImgRect", "or");
                    TutorialLabel(screen, "GetOrLabelRect", "TUTOR_OR", "orLabel");
                    TutorialLabel(screen, "GetFirstTitleRect", "TUTOR_DOP");
                    TutorialLabel(screen, "GetSecTitleRect", "TUTOR_DP");
                    TutorialButton(screen, "GetDwnldBtnRect", "main_btn", MarkerExport26.Save, "DOWNLOAD");
                    TutorialButton(screen, "GetBackRect", "back_btn", () => SetTutorialPage(TutorialPage.CreateMarker));
                    TutorialButton(screen, "GetOkBtnRect", "next2_btn", FinishTutorial);
                    break;
            }
            TutorialButton("Tutorial2Screen", "GetCloseRect", "close_banner", CloseTutorial);
        }

        public void ChangeSettingsPage(string page)
        {
            game.Play(game.assets.button);
            transition.Begin(() => { settingsPage = page; gameCenterFromResults = false; resetHolding = 0; resetArmed = resetPointerDown = false; });
        }
        void DrawAbout()
        {
            string copy = string.Join("\n", T("ABOUT_VERSION"), T("ABOUT_TEXT1"), T("ABOUT_TEXT2"), T("ABOUT_TEXT3"));
            var titleStyle = new GUIStyle(Main(43)) { alignment = TextAnchor.UpperCenter, wordWrap = true, fixedHeight = 0 };
            var bodyStyle = new GUIStyle(Main(42)) { alignment = TextAnchor.UpperCenter, fixedHeight = 0 };
            Rect[] layout = AboutLayout(T("ABOUT_TITLE"), copy, titleStyle, bodyStyle, height);
            GUI.Label(layout[0], T("ABOUT_TITLE"), titleStyle);
            GUI.Label(layout[1], copy, bodyStyle);
            if (Button(layout[2], Main(1))) Application.OpenURL("http://privacy-policy.truste.com/certified-policy/mobile/app/en/CutTheRope.com/");
            Outline(layout[2], T("PRIVACY_POLICY"), Main(1));
            if (Button(layout[3], Main(1))) Application.OpenURL("http://www.zeptolab.com/tc.htm");
            Outline(layout[3], T("TERMS_OF_USE"), Main(1));
            if (CreditsNextButton()) ChangeSettingsPage("ModelCredits");
        }
        // Unity 6 font metrics differ from legacy IMGUI. Measure the original copy
        // instead of letting centred text overflow its 22/340-pixel native boxes.
        // Preserve native widths, fonts and button sizes; reduce type only when the
        // usable screen cannot fit the complete translated copy above the back button.
        public static Rect[] AboutLayout(string title, string copy, GUIStyle titleStyle, GUIStyle bodyStyle, float availableHeight)
        {
            float top = Mathf.Min(170.4f, availableHeight * .15f), limit = availableHeight - 155;
            int titleSize = titleStyle.fontSize, bodySize = bodyStyle.fontSize;
            float titleHeight, bodyHeight;
            float factor = 1;
            do {
                titleStyle.fontSize = Mathf.Max(12, Mathf.FloorToInt(titleSize * factor));
                bodyStyle.fontSize = Mathf.Max(12, Mathf.FloorToInt(bodySize * factor));
                titleHeight = Mathf.Ceil(titleStyle.CalcHeight(new GUIContent(title), 448));
                bodyHeight = Mathf.Ceil(bodyStyle.CalcHeight(new GUIContent(copy), 448));
                if(top + titleHeight + 16 + bodyHeight + 24 + 238.65f <= limit || factor <= .35f)break;
                factor -= .025f;
            } while(true);
            Rect heading = new Rect(96, top, 448, titleHeight);
            Rect body = new Rect(96, heading.yMax + 16, 448, bodyHeight);
            float buttonsTop = Mathf.Max(568, body.yMax + 24);
            buttonsTop = Mathf.Min(buttonsTop, limit - 238.65f);
            return new[] { heading, body, new Rect(87, buttonsTop, 466, 111), new Rect(87, buttonsTop + 127.65f, 466, 111) };
        }
        void DrawReset()
        {
            Rect label = R("ResetScreen$$GetLabelRect"); Outline(label, T("RESET_QUESTION"), Main(41));
            label.y += label.height; GUI.Label(label, T("HOLD_YES"), Main(42));
            Rect yes = R("ResetScreen$$GetYesBtnRect");
            var style = new GUIStyle(Main(1));
            if ((resetHolding >= 3 && resetHolding <= 3.4f) || (resetHolding >= 4 && resetHolding <= 4.4f)) style.active.background = style.normal.background;
            if (Button(yes, style) && resetArmed) { game.ResetProgress(); transition.Begin(() => settingsPage = null); resetArmed = false; }
            Outline(yes, T("YES"), Main(1));
            Rect no = R("ResetScreen$$GetNoBtnRect");
            if (Button(no, Main(1))) transition.Begin(() => settingsPage = null);
            Outline(no, T("NO"), Main(1));
        }
        void UpdateResetHolding()
        {
            if (game.ScreenState != GameScreen.Settings || settingsPage != "Reset") return;
            Vector2 position = Input.mousePosition;
            bool down = Input.GetMouseButton(0);
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0); position = touch.position;
                if(touch.phase==TouchPhase.Canceled) { resetArmed=resetPointerDown=false; resetHolding=0; return; }
                down = touch.phase != TouchPhase.Ended && touch.phase != TouchPhase.Canceled;
            }
            Rect safe = Screen.safeArea; float scale = CanvasScale(safe);
            position = new Vector2((position.x - safe.x) / scale - (safe.width / scale - 640) / 2, (safe.yMax - position.y) / scale);
            if (down)
            {
                if (!resetPointerDown) { resetHolding = 0; resetArmed = false; }
                else if (R("ResetScreen$$GetYesBtnRect").Contains(position)) resetHolding += Time.unscaledDeltaTime;
                else resetHolding = 0;
                if (resetHolding >= 3) resetArmed = true;
            }
            else resetHolding = 0;
            // Keep the arm bit through the release GUI event; Unity 6 delivers synthetic
            // touch mouse-up after Update. Starting a new press clears it.
            resetPointerDown = down;
        }
    }
}
