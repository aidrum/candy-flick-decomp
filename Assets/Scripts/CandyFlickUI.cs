using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using CandyFlick.Original26;

namespace CandyFlick
{
    // Original MainScreen, GamePlayScreen, ScoresGPView, ScoresGOView and SettingScreen.
    // Rectangle fixtures were executed from 2.6; GUI styles/textures are recovered assets.
    public sealed partial class CandyFlickUI : MonoBehaviour
    {
        public CandyFlickGame game;
        [Serializable] sealed class Layout { public float width,height; public Entry[] rectangles; public DimensionEntry[] dimensions; }
        [Serializable] sealed class DimensionEntry { public string method; public float value; }
        [Serializable] sealed class Entry { public string method; public float[] rect; }
        [Serializable] sealed class FontProfiles { public FontProfile[] languages; }
        [Serializable] sealed class FontProfile { public int language; public FontSize[] sizes; }
        [Serializable] sealed class FontSize { public string method; public int size; }
        readonly Dictionary<string, Rect> rectangles = new Dictionary<string, Rect>();
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        GUISkin mainSkin, gameSkin;
        GUIStyle numberStyle, outlineStyle, markerText;
        float height;
        bool initialized;
        string settingsPage;
        Rect possibleRect;
        Texture2D possibleTexture;
        int possibleNumber, totalNumber;
        bool freezeScores, missPossible, flyPossible, boostHit, showTotalEffect, fadeUp;
        float totalEffectAlpha;
        TextManager26 text;
        LegacyGUITexture26 menuShadow;
        Animation menuShadowAnimation;
        FontProfiles fontProfiles;
        bool resultsInitialized, showFeatureBonus;
        float menuEntryAlpha = 1;
        // GamePlayScreen.PhotoTapped / CameraEffect and SearchTargetScreen's animated GUITextures.
        CaptureManager26 capture;
        CameraEffect26 cameraEffect;
        bool hideGui, searchShown, searchGround, searchLocalTracking;
        LegacyGUITexture26 searchFocus, searchSticker, whiteFlash;
        bool restartPending;
        float searchHideTime = -1;

        void Init()
        {
            if (initialized) { LoadDeviceLayout(); return; }
            text = new TextManager26(game.assets.languages);
            LoadDeviceLayout();
            initialized = true;
            ApplyLanguageFont();
            game.Achievements.Earned+=QueueAchievementNotification;game.Achievements.Reloaded+=ClearAchievementNotifications;
            game.GameStarted += ResetHud;
            game.CandyResolved += OnCandyResolved;
            game.BoostPassed += OnBoostPassed;
            ResetHud();
            var shadowObject = new GameObject("Original menu shadows", typeof(LegacyGUITexture26), typeof(Animation));
            menuShadow = shadowObject.GetComponent<LegacyGUITexture26>();
            menuShadow.texture = Texture(DevicePath("shadows"));
            menuShadow.transform.position = new Vector3(.5f, .5f, 2);
            menuShadow.transform.localScale = new Vector3(1.1f, 1.1f, 1);
            menuShadowAnimation = shadowObject.GetComponent<Animation>();
            AnimationClip shadowClip = Resources.Load<AnimationClip>("Original26/GUIAnimations/GUIShadowsAnimation");
            menuShadowAnimation.AddClip(shadowClip, shadowClip.name); menuShadowAnimation.clip = shadowClip;
            menuShadowAnimation.Play();
            capture = gameObject.AddComponent<CaptureManager26>();
            capture.Saved += saved => ShowPopupWithMessageKey(saved ? "PHOTO_SAVED" : "DOWNLOAD_DIALOG_ERROR");
            cameraEffect = new CameraEffect26(new[] { Texture("all/camera_1"), Texture("all/camera_2"), Texture("all/camera_3"), Texture("all/camera_4"), Texture("all/camera_5") });
            // SearchTargetScreen.InitByDevice: focusing and sticker, configured for the active profile below.
            searchFocus = GUITexture("Original search focusing", "iphone4/search_target/focusing",
                new Vector3(.5f, .52f, 0), new Vector3(.55f, .22f, 1), .5f, "GUIRearSightAnimation", "EndRearSight");
            searchSticker = GUITexture("Original search sticker", "iphone4/search_target/sticker",
                new Vector3(.5f, -.06f, 0), new Vector3(.71588236f, .23f, 1), .2f, "GUIStickerAnimation_iph5", "EndSticker_iph5", "GUIStickerAnimation", "EndSticker", "GUIStickerAnimation_iPad", "EndSticker_iPad");
            ConfigureSearchArtwork();
            // WhiteShootScreen: full-screen whiteBgr GUITexture (alpha 0) with WhiteShootAnim.
            whiteFlash = GUITexture("Original white shoot screen", "all/whiteBgr",
                new Vector3(.5f, .5f, 0), Vector3.one, 0, "WhiteShootAnim");
        }
        // GamePlayScreen.RestartBtnTapped 0x30f28 / WaitForAnimation: flash, then restart at the white peak.
        void RestartTapped()
        {
            if(game.IsPhotoMode)return;
            if (restartPending) return;
            restartPending = true;
            flashStart = Time.unscaledTime;
            StartCoroutine(RestartAtFlashPeak());
        }
        IEnumerator RestartAtFlashPeak()
        {
            yield return new WaitForSecondsRealtime(.15f);
            restartPending = false;
            game.StartGame(game.IsAR); // OnGameRestart / OnRestartInGame.
        }
        // The GUITexture (color x2) renders beneath OnGUI and WhiteScreen.OnGUI adds the same texture at
        // the clip alpha with GUI.depth 200, so the HUD stays above the flash. Both cover the full display.
        // WhiteShootAnim m_Color.a keys, evaluated every GUI pass on unscaled time for a smooth flash.
        static readonly AnimationCurve FlashCurve = new AnimationCurve(
            new Keyframe(0, 0, 30, 6.6666665f), new Keyframe(.15f, 1, 0, 0), new Keyframe(.3f, 0, -6.6666665f, -30));
        float flashStart = -1;
        void DrawWhiteFlash()
        {
            if (flashStart < 0 || !whiteFlash) return;
            float t = Time.unscaledTime - flashStart;
            if (t > .31666666f) { flashStart = -1; return; } // m_Enabled key.
            float a = Mathf.Clamp01(FlashCurve.Evaluate(t));
            // GUITexture (color x2) plus WhiteScreen's pass at the clip alpha, combined into one draw.
            float alpha = 1 - (1 - Mathf.Min(1, 2 * a)) * (1 - a);
            Color saved = GUI.color; GUI.color = new Color(1, 1, 1, alpha);
            GUI.DrawTexture(fullScreen, whiteFlash.texture); GUI.color = saved;
        }
        LegacyGUITexture26 GUITexture(string name, string texture, Vector3 position, Vector3 scale, float alpha, params string[] clips)
        {
            var created = new GameObject(name, typeof(LegacyGUITexture26), typeof(Animation));
            var guiTexture = created.GetComponent<LegacyGUITexture26>();
            guiTexture.texture = Texture(texture); guiTexture.m_Color.a = alpha;
            created.transform.position = position; created.transform.localScale = scale;
            var animation = created.GetComponent<Animation>(); animation.playAutomatically = false;
            foreach (string clipName in clips)
            {
                AnimationClip clip = Resources.Load<AnimationClip>("Original26/GUIAnimations/" + clipName);
                animation.AddClip(clip, clipName);
            }
            created.SetActive(false);
            return guiTexture;
        }
        // SearchTargetScreen.OnEnable 0x3c27c / CloseScreen 0x3c678 (WaitAndDisable after .7 s).
        void SetSearchScreen(bool show, bool animate)
        {
            searchShown = show; searchGround = GroundSearchArtwork;
            searchLocalTracking=game.IsLanMatch&&game.LAN.LocalTracking;
            foreach (var (guiTexture, open, close) in new[] { (searchFocus, "GUIRearSightAnimation", "EndRearSight"), (searchSticker, StickerOpen, StickerClose) })
            {
                if(guiTexture==searchFocus && (searchGround||searchLocalTracking)) { guiTexture.gameObject.SetActive(false); continue; }
                if (show) { guiTexture.gameObject.SetActive(true); PlayUnscaled(guiTexture, open); }
                else if (animate && guiTexture.gameObject.activeSelf) PlayUnscaled(guiTexture, close);
                else guiTexture.gameObject.SetActive(false);
            }
            searchHideTime = !show && animate ? Time.unscaledTime + .7f : -1;
        }
        // The search screen is shown while gameplay time is frozen (timeScale 0 without a tracked image),
        // which would stop legacy Animation playback; sample these clips on unscaled time instead.
        readonly Dictionary<Animation, (string clip, float start)> unscaledAnimations = new Dictionary<Animation, (string, float)>();
        void PlayUnscaled(LegacyGUITexture26 guiTexture, string clip)
        {
            var animation = guiTexture.GetComponent<Animation>();
            animation.Stop();
            unscaledAnimations[animation] = (clip, Time.unscaledTime);
            SampleUnscaled(animation, clip, 0);
        }
        static void SampleUnscaled(Animation animation, string clip, float time)
        {
            AnimationState state = animation[clip];
            state.enabled = true; state.weight = 1; state.speed = 0;
            state.time = Mathf.Min(time, state.length); // Hold the last key, as the stopped clip did.
            animation.Sample();
            state.enabled = false;
        }
        void UpdateUnscaledAnimations()
        {
            foreach (var entry in unscaledAnimations)
                if (entry.Key.gameObject.activeInHierarchy)
                    SampleUnscaled(entry.Key, entry.Value.clip, Time.unscaledTime - entry.Value.start);
        }
        string T(string key) => text.GetStringByKey(key);
        void ApplyLanguageFont()
        {
            Font font = PortFont;
            foreach (var style in mainSkin.customStyles) style.font = font;
            foreach (var style in gameSkin.customStyles) style.font = font;
            numberStyle.font = outlineStyle.font = font;
            int FontSize(string name) => Array.Find(fontProfiles.languages[text.CurrentLanguage].sizes, entry => entry.method == "FontSizeManager$$get_" + name).size;
            Main(1).fontSize = FontSize("MainBtnFS"); Main(41).fontSize = FontSize("ResetLabelFS");
            Main(42).fontSize = FontSize("AboutLabelFS"); Main(43).fontSize = FontSize("AboutTitleFS");
            Game(2).fontSize = FontSize("LabelMarkerFS"); Game(9).fontSize = FontSize("LabelGOFS");
            Game(17).fontSize = FontSize("GetImageFS");
            markerText.font = font; markerText.fontSize = FontSize("ShowMarkerFontSize");
            Game(41).fontSize = numberStyle.fontSize = outlineStyle.fontSize = FontSize("ScoreFS");
            Game(8).fontSize = FontSize("LabelGPFS"); // SearchTargetScreen.InitFontSize: label_gp.
        }
        void ResetHud()
        {
            StopAllCoroutines();
            game.PhotoVisibility?.End();
            possibleRect = rectangles["ScoresGPView$$GetPossibleScoresRect"];
            possibleTexture = Texture("all/new_assets/Target_Plate");
            freezeScores = missPossible = flyPossible = boostHit = showTotalEffect = false;
            totalEffectAlpha = 0; hideGui = false;
            resultsInitialized = showFeatureBonus = false;
        }
        void Update()
        {
            if (!initialized) return;
            UpdateAchievementNotifications();
            bool showMenu = game.ScreenState == GameScreen.Menu;
            if (menuShadow && menuShadow.gameObject.activeSelf != showMenu)
            { menuShadow.gameObject.SetActive(showMenu); if (showMenu) menuShadowAnimation.Play(); }
            UpdateResetHolding();
            bool inGame = game.ScreenState == GameScreen.Playing || game.ScreenState == GameScreen.Paused;
            bool searching = inGame && game.IsAR && (game.IsPhotoMode ? PhotoPlacementPrompt || !game.ar.Tracking : game.IsLanMatch ? !game.LAN.Playing : !game.ar.Tracking);
            if (searching != searchShown || (searching && (searchGround != GroundSearchArtwork || searchLocalTracking != (game.IsLanMatch&&game.LAN.LocalTracking)))) SetSearchScreen(searching, inGame);
            if (searchHideTime >= 0 && Time.unscaledTime >= searchHideTime) SetSearchScreen(false, false);
            UpdateUnscaledAnimations();
            UpdatePhotoHighlight();
            if (!game.CanSimulate) return;
            if (!freezeScores) { possibleNumber = game.PossibleScore; totalNumber = game.Session.total; }
            if (showTotalEffect)
            {
                totalEffectAlpha += Time.deltaTime * (fadeUp ? 4 : -4);
                if (totalEffectAlpha >= 1) fadeUp = false;
            }
            if (flyPossible)
            {
                float travel=rectangles["ScoresGPView$$GetTotalScoresRect"].x-rectangles["ScoresGPView$$GetPossibleScoresRect"].x;
                possibleRect.x+=640*Time.deltaTime*1.5f*(1+GameplayExtraWidth/(2*travel));
            }
        }
        void OnBoostPassed()
        {
            boostHit = true; possibleNumber = game.PossibleScore; freezeScores = true;
            possibleTexture = Texture("all/new_assets/Target_Plate_green");
        }
        void OnCandyResolved(bool hit)
        {
            if (hit)
            {
                fadeUp = true; totalEffectAlpha = 0; freezeScores = true;
                possibleTexture = Texture(boostHit ? "all/new_assets/Score_Plate_Green" : "all/new_assets/Score_Plate");
                StartCoroutine(ToggleTotalEffect(.7f)); StartCoroutine(ToggleTotalEffect(1.2f));
                StartCoroutine(AnimatePossibleAfter(.45f));
            }
            else { missPossible = true; freezeScores = false; possibleTexture = Game(4).normal.background; }
            StartCoroutine(ResetPossibleAfter(.7f)); boostHit = false;
        }
        IEnumerator ToggleTotalEffect(float seconds)
        {
            yield return new WaitForSeconds(seconds); showTotalEffect = !showTotalEffect;
            if (showTotalEffect) freezeScores = false;
        }
        IEnumerator AnimatePossibleAfter(float seconds) { yield return new WaitForSeconds(seconds); flyPossible = true; }
        IEnumerator ResetPossibleAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds); missPossible = flyPossible = freezeScores = false;
            possibleRect = rectangles["ScoresGPView$$GetPossibleScoresRect"];
            possibleTexture = Texture("all/new_assets/Target_Plate");
        }
        Rect R(string method, bool bottom = false)
        {
            Rect rect = rectangles[method];
            if (bottom) rect.y += height - referenceHeight;
            return GameplayRect(method,rect);
        }
        Texture2D Texture(string path)
        {
            if (!textures.TryGetValue(path, out Texture2D texture))
            { texture = Resources.Load<Texture2D>("images/" + path); textures.Add(path, texture); }
            return texture;
        }
        GUIStyle Main(int index) => mainSkin.customStyles[index];
        GUIStyle Game(int index) => gameSkin.customStyles[index];
        bool Button(Rect rect, GUIStyle style, string text = "")
        {
            bool tapped = GUI.Button(rect, text, PressOnlyButtonStyle(style));
            if (tapped) game.Play(game.assets.button);
            return tapped;
        }
        // Menu-style backgrounds fill the whole display, including notch/cutout areas outside the safe area.
        Rect fullScreen;
        void Background(Texture texture) => GUI.DrawTexture(fullScreen, texture);
        void Outline(Rect rect, string text, GUIStyle style = null)
        {
            style = style ?? numberStyle;
            if (style.normal.background || style.hover.background || style.active.background || style.focused.background)
            {
                style = new GUIStyle(style);style.normal.background = null;style.normal.scaledBackgrounds = null;
                style.hover = style.active = style.focused = style.normal;
            }
            var outer = style == numberStyle ? outlineStyle : new GUIStyle(style);
            outer.normal.background = null; outer.normal.textColor = Color.black;
            outer.hover = outer.active = outer.focused = outer.normal;
            Rect offset = rect; offset.x--; GUI.Label(offset, text, outer);
            offset.x += 2; GUI.Label(offset, text, outer);
            offset = rect; offset.y--; GUI.Label(offset, text, outer);
            offset.y += 2; GUI.Label(offset, text, outer);
            GUI.Label(rect, text, style);
        }
        void OnGUI()
        {
            Init();
            bool switched = transition.BeforeGUI();
            // Unity 6 keeps GUILayout state for this component across GUI events. A
            // native screen switch can land on Repaint; restart that event's layout.
            if (switched && Event.current.type != EventType.Layout) GUIUtility.ExitGUI();
            // Menus use the bounded canvas; gameplay and pause anchors span the safe area.
            Rect safe = UnityEngine.Screen.safeArea;
            float scale = CanvasScale(safe); height = safe.height / scale;
            Matrix4x4 saved = GUI.matrix;
            Vector2 origin = CanvasOrigin(safe,UnityEngine.Screen.height,game.ScreenState);
            fullScreen = new Rect(-origin.x / scale, -origin.y / scale, UnityEngine.Screen.width / scale, UnityEngine.Screen.height / scale);
            GUI.matrix = Matrix4x4.TRS(new Vector3(origin.x, origin.y, 0), Quaternion.identity, new Vector3(scale, scale, 1));
            HandlePopupInput(); // The popup sits above every screen and takes its input first.
            GUI.skin = game.ScreenState == GameScreen.Menu || game.ScreenState == GameScreen.Settings || game.ScreenState == GameScreen.Skins ? mainSkin : gameSkin;
            // Ignore input during the native switch delay without Unity's disabled-control tint.
            if (transition.Waiting && (Event.current.isMouse || Event.current.isKey || Event.current.type == EventType.ScrollWheel))
                Event.current.Use();
            switch (game.ScreenState)
            {
                case GameScreen.Menu: DrawMenu(); break;
                case GameScreen.Playing:
                    // GamePlayScreen.OnGUI: isHideGui draws with GUI.color (0,0,0,0) for the photo.
                    Color shown = GUI.color; if (hideGui) GUI.color = Color.clear;
                    DrawGamePlay(); GUI.color = shown; break;
                case GameScreen.Paused: DrawGamePlay(); DrawPause(); break;
                case GameScreen.Results: DrawResults(); break;
                case GameScreen.Settings: DrawSettings(); break;
                case GameScreen.Skins: DrawSkins(); break;
                case GameScreen.LanLobby: DrawLanLobby(); break;
                case GameScreen.Help: DrawTutorial(); break;
                case GameScreen.Tutorial: DrawTutorial(); break;
                case GameScreen.Marker: DrawMarker(); break;
                case GameScreen.TargetOptions: DrawTargetOptions(); break;
            }
            GUI.skin = gameSkin; DrawPopup();
            transition.AfterGUI();
            if (transition.Alpha > 0)
            {
                Color color = GUI.color; GUI.color = new Color(0, 0, 0, transition.Alpha);
                GUI.DrawTexture(fullScreen, Texture2D.whiteTexture); GUI.color = color;
            }
            DrawWhiteFlash(); // Above everything, including the popup and fades.
            if(!hideGui)DrawAchievementNotification();
            GUI.matrix = saved;
            cameraEffect.Draw(); // CameraEffect.OnGUI uses raw screen pixels.
        }
        void DrawMenu()
        {
            DrawMainMenuArtwork();
            Matrix4x4 canvas = GUI.matrix; // Shadow vignette spans the full display with its background.
            GUI.matrix = canvas * Matrix4x4.Translate(new Vector3(fullScreen.x, fullScreen.y, 0));
            menuShadow.Draw(fullScreen.width, fullScreen.height); GUI.matrix = canvas;
            Color menuColor = GUI.color;
            GUI.color = new Color(menuColor.r, menuColor.g, menuColor.b, 1 - menuEntryAlpha);
            if (RoundMenuButton(MenuSoloPlayRect, Main(23)))
            {
                game.StopMenuMusic();
                transition.Begin(MenuPlay);
            }
            if (RoundMenuButton(MenuSettingsRect, Main(24))) transition.Begin(() => { settingsPage = null; game.Show(GameScreen.Settings); });
            if (RoundMenuButton(MenuDownloadRect, Main(40))) transition.Begin(() => OpenTutorial(true, false));
            if (RoundMenuButton(MenuSkinsRect, SkinsMenuButtonStyle)) OpenSkins();
            if (RoundMenuButton(MenuPhotoRect, PhotoMenuButtonStyle)) OpenPhotoMode();
            if (RoundMenuButton(MenuMultiplayerRect, LanMenuButtonStyle)) OpenLanMenu();
            GUI.color = menuColor;
            // MainScreen.OnGUI, literal at 0x34aa8: first-entry alpha decreases .05 per call.
            if(menuEntryAlpha > 0)
            {
                menuEntryAlpha -= .05f;
                GUI.color = new Color(0, 0, 0, menuEntryAlpha);
                GUI.DrawTexture(fullScreen, Texture2D.whiteTexture); GUI.color = menuColor;
            }
        }
        public static bool RoundMenuButtonContains(Rect rect,Vector2 point)
        {
            Vector2 offset=point-rect.center;offset=new Vector2(offset.x/rect.width,offset.y/rect.height);
            return offset.sqrMagnitude<=.25f;
        }
        bool RoundMenuButton(Rect rect,GUIStyle style)
        {
            // The full-size central icon shares transparent rectangle corners with
            // its neighbors. Only the round button face should own a press.
            Event current=Event.current;EventType original=current.type;
            bool ignore=original==EventType.MouseDown&&rect.Contains(current.mousePosition)&&!RoundMenuButtonContains(rect,current.mousePosition);
            if(ignore)current.type=EventType.Ignore;
            bool tapped=GUI.Button(rect,"",PressOnlyButtonStyle(style));
            if(ignore)current.type=original;
            if(!tapped||!RoundMenuButtonContains(rect,current.mousePosition))return false;
            game.Play(game.assets.button);return true;
        }
        void DrawGamePlay()
        {
            if(game.IsPhotoMode){DrawPhotoGame();return;}
            if(game.IsLanMatch)
            {
                Matrix4x4 saved=GUI.matrix;GUI.matrix=saved*Matrix4x4.Translate(new Vector3(GameplayExtraWidth/2,0,0));
                searchSticker.Draw(640,height);searchFocus.Draw(640,height);GUI.matrix=saved;
                DrawLanGame();return;
            }
            // Unity 4 GUITextures (GUILayer) render beneath OnGUI.
            Matrix4x4 canvas=GUI.matrix;
            GUI.matrix=canvas*Matrix4x4.Translate(new Vector3(GameplayExtraWidth/2,0,0));
            searchSticker.Draw(640, height); searchFocus.Draw(640, height);GUI.matrix=canvas;
            if (game.IsAR && !game.ar.Tracking)
            {
                if(game.ar.GroundMode) DrawGroundSearch();
                else
                {
                    // DrawRearSightScreen 0x3cf50: plain GUI.Label with label_gp; the button text is outlined.
                    GUI.Label(R("SearchTargetScreen$$GetLabelRect", true), T("SEARCH_TARGET_LABEL"), Game(8));
                    Rect imageButton = R("SearchTargetScreen$$GetImageButtonRect", true);
                    bool getImage = Button(imageButton, Game(17));
                    if (text.CurrentLanguage == 6) imageButton.y += 3; // Native Korean baseline offset (non-iPad3).
                    Outline(imageButton, T("GET_IMAGE"), Game(17));
                    if (getImage) OpenTargetOptions();
                }
            }
            else
            {
                Rect candy = R("ScoresGPView$$GetCandiesRect");
                Rect possible = GameplayRect("ScoresGPView$$GetPossibleScoresRect",possibleRect);
                Rect total = R("ScoresGPView$$GetTotalScoresRect");
                GUI.DrawTexture(candy, Texture("all/new_assets/Candy_number"));
                GUI.DrawTexture(possible, possibleTexture);
                GUI.DrawTexture(total, Texture("all/new_assets/Score_Plate"));
                if (showTotalEffect)
                {
                    GUI.color = new Color(1, 1, 1, totalEffectAlpha);
                    GUI.DrawTexture(new Rect(total.x - total.width * .75f, total.y - total.height * .75f, total.width * 2.5f, total.height * 2.5f), Texture("all/new_assets/Shine"));
                    GUI.color = Color.white;
                }
                Outline(candy, game.Session.Remaining.ToString());
                if (!missPossible) Outline(possible, possibleNumber.ToString());
                Outline(total, totalNumber.ToString());
                Rect restart = R("GamePlayScreen$$GetRestartRectGP", true);
                if (Button(restart, Game(10))) RestartTapped();
                // DrawGamePlayScreen 0x3150c: photo_btn above restart, y = height - (int)(restart.height * 1.5).
                if (game.ScreenState == GameScreen.Playing && !CaptureManager26.IsScreenShotSaving)
                {
                    Rect photo = restart; photo.y = height - (int)(restart.height * 1.5f);
                    if (Button(photo, Game(30))) PhotoTapped();
                }
            }
            if (game.ScreenState != GameScreen.Paused && Button(R("GamePlayScreen$$GetPauseRect", true), Game(11))) game.Pause();
            HandleGroundPlacementTap();
        }
        // GamePlayScreen.PhotoTapped 0x313b4 and WaitAndCapture/WaitAndShowEffect/WaitAndShowGui.
        void PhotoTapped()
        {
            if(hideGui||CaptureManager26.IsScreenShotSaving)return;
            hideGui = true; game.PhotoVisibility.Begin(game.HideCandyInPictures);
            if(game.IsPhotoMode){game.PhotoCapturing=true;UpdatePhotoHighlight();}
            StartCoroutine(WaitAndCapture());
        }
        IEnumerator WaitAndCapture()
        {
            try
            {
                yield return null; // WaitForSeconds(0): capture the next frame, drawn with GUI hidden.
                capture.CaptureScreenshot();
                yield return new WaitForSecondsRealtime(.05f);
                game.Play(game.assets.cameraShutter); cameraEffect.Begin();
                yield return new WaitForSecondsRealtime(.45f);
            }
            finally { hideGui = false; game.PhotoCapturing=false; game.PhotoVisibility.End(); }
        }
        void OnDisable() { if (game && game.PhotoVisibility) game.PhotoVisibility.End(); }
        void DrawPause()
        {
            if(game.IsPhotoMode){DrawPhotoPause();return;}
            if(game.IsLanMatch) { DrawLanPause(); return; }
            GUI.color = new Color(0, 0, 0, .5f); GUI.DrawTexture(fullScreen, Texture2D.whiteTexture); GUI.color = Color.white;
            DrawARToggle();
            if (Button(R(game.IsAR && !game.ar.Tracking ? "SearchTargetScreen$$GetHelpRect" : "GamePlayScreen$$GetHelpRect", true), Game(12)))
            {
                if(game.IsAR && game.ar.GroundMode) ShowPopupMessage("Move your phone slowly over a flat, well-lit surface. Aim at the surface and tap anywhere to place Om Nom.");
                else transition.Begin(() => OpenTutorial(false, false));
            }
            if(game.IsAR && game.ar.GroundMode && game.ar.GroundPlaced)
                DrawSettingsOption(GroundRepositionRect,"Place again",()=>{game.ar.RepositionGround();game.Resume();});
            if (Button(R("GamePlayScreen$$GetSoundRect", true), Game(game.SoundEnabled ? 14 : 18))) game.ToggleSound();
            if (Button(R("GamePlayScreen$$GetMenuRect", true), Game(20))) transition.Begin(game.ReturnToMenu);
            // Native controls are clipped by the screen at the lower right corner.
            if (Button(R("GamePlayScreen$$GetPauseRect", true), Game(21))) game.Resume();
        }
        void DrawResults()
        {
            if(game.IsLanMatch) { DrawLanResults(); return; }
            if (!resultsInitialized) { resultsInitialized = true; StartCoroutine(WaitAndTurn()); }
            // The original results board is drawn over the live camera output, without a background.
            GUI.DrawTexture(R("GameOverScreen$$GetBoardRect"), Texture(DevicePath("board")));
            for (int i = 0; i < 10; i++)
            {
                Rect rect = R("ScoresGOView$$GetScore" + (i + 1) + "Rect");
                GUI.DrawTexture(rect, game.Session.scores[i] > 0 ? Texture(game.Session.boosts[i] > 0 ? "all/new_assets/Score_Plate_Green" : "all/new_assets/Score_Plate") : Game(4).normal.background);
                if (game.Session.scores[i] > 0)
                { var resultStyle = new GUIStyle(numberStyle); resultStyle.normal.textColor = new Color(.384f, .216f, .051f, 1); GUI.Label(rect, game.Session.scores[i].ToString(), resultStyle); }
            }
            Rect total = R("ScoresGOView$$GetTotalRect"), best = R("ScoresGOView$$GetBestRect");
            Rect totalImage = total; totalImage.y += 7; GUI.DrawTexture(totalImage, Texture("all/new_assets/total_bgr"));
            GUI.Label(total, T("TOTAL") + ": " + game.Session.total + (game.Session.bonus > 0 ? "+" + game.Session.bonus : ""), Game(9));
            if (game.NextGameBonus > 0 && showFeatureBonus)
                GUI.Label(best, "+" + game.NextGameBonus + " " + T("BONUS_POINTS"), new GUIStyle(Game(9)) { fontSize = Game(9).fontSize / 2 });
            else GUI.Label(best, T("BEST") + ": " + game.Best, Game(9));
            if (Button(R("GameOverScreen$$GetRestartRectGO"), Game(13))) transition.Begin(() => game.StartGame(game.IsAR));
            GUILayout.BeginArea(R("GameOverScreen$$GetBottomBtnRectGO"));
            GUILayout.BeginHorizontal(); GUILayout.FlexibleSpace();
            float footerWidth=Dimension("GameOverScreen$$GetBottomBtnWidth"),footerHeight=Dimension("GameOverScreen$$GetBottomBtnHeight");
            bool achievements = GUILayout.Button("", Game(15), GUILayout.Width(footerWidth), GUILayout.Height(footerHeight));
            Rect achievementButton=GUILayoutUtility.GetLastRect();achievementButton.position+=R("GameOverScreen$$GetBottomBtnRectGO").position;ResultsAchievementsRect=achievementButton;
            GUILayout.Space(footerWidth * .1f);
            bool leaderboard = GUILayout.Button("", Game(16), GUILayout.Width(footerWidth), GUILayout.Height(footerHeight));
            GUILayout.Space(footerWidth * .1f);
            bool menu = GUILayout.Button("", Game(22), GUILayout.Width(footerWidth), GUILayout.Height(footerHeight));
            GUILayout.FlexibleSpace(); GUILayout.EndHorizontal(); GUILayout.EndArea();
            if(menu) { game.Play(game.assets.button); transition.Begin(game.ReturnToMenu); }
            // Game Center is iOS-only: the original buttons stay visible but do nothing on Android.
            if(achievements)OpenAchievements(true);if(leaderboard)game.Play(game.assets.button);
        }
        IEnumerator WaitAndTurn()
        {
            yield return new WaitForSeconds(1);
            showFeatureBonus = !showFeatureBonus;
            StartCoroutine(WaitAndTurn());
        }
        void DrawSettings()
        {
            Background(Texture(DevicePath("settings")));
            if (settingsPage == "ModelCredits")
            {
                DrawModelCredits();
                if (Button(CreditsBackRect, Main(33))) transition.Begin(() => settingsPage = "About");
                return;
            }
            if (Button(R("SettingScreen$$GetBackBtnRect", true), Main(33)))
                transition.Begin(() => { if (settingsPage == "ModelCredits") settingsPage = "About"; else if ((settingsPage == "GameCenter" || settingsPage == "Achievements") && gameCenterFromResults) { game.Show(GameScreen.Results); settingsPage = null; } else if (settingsPage != null) settingsPage = null; else game.ReturnToMenu(); });
            if (settingsPage == "Achievements") { DrawAchievements(); return; }
            if (settingsPage == "About") { DrawAbout(); return; }
            if (settingsPage == "Reset") { DrawReset(); return; }
            if (settingsPage == "MoreOptions") { DrawMoreOptions(); return; }
            float width=Dimension("SettingScreen$$GetMainBtnWidth"),buttonHeight=Dimension("SettingScreen$$GetMainBtnHeight");
            float buttonScale=buttonHeight/111;
            float rows = settingsPage == "GameCenter" ? 5.3f : 6.5f;
            Rect settingsButtons=new Rect(320-width/2,height/2-buttonHeight*rows/2,width,buttonHeight*rows);
            GUILayout.BeginArea(settingsButtons);
            GUILayout.BeginVertical();
            if (settingsPage == "GameCenter")
            {
                GUILayout.Label(L("Game Center is available on iOS."), Main(42));
                if (GUILayout.Button(T("OK"), Main(1), GUILayout.Height(buttonHeight)))
                {
                    game.Play(game.assets.button);
                    transition.Begin(() => { settingsPage = null; if(gameCenterFromResults)game.Show(GameScreen.Results); });
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("", Main(game.SoundEnabled ? 36 : 37), GUILayout.Width(219*buttonScale), GUILayout.Height(buttonHeight))) { game.Play(game.assets.button); game.ToggleSound(); }
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("", Main(game.MusicEnabled ? 34 : 35), GUILayout.Width(207*buttonScale), GUILayout.Height(102*buttonScale))) { game.Play(game.assets.button); game.ToggleMusic(); }
                GUILayout.EndHorizontal(); GUILayout.FlexibleSpace();
                if (GUILayout.Button("", Main(1), GUILayout.Width(width), GUILayout.Height(buttonHeight)))
                { text.ChangeLanguage((text.CurrentLanguage + 1) % 9); ApplyLanguageFont(); game.Play(game.assets.button); }
                Rect languageRect = GUILayoutUtility.GetLastRect();
                Outline(languageRect, T("LANGUAGE"), Main(1));
                GUI.DrawTexture(new Rect(languageRect.xMax - 79.365f * 1.5f*buttonScale, languageRect.y + languageRect.height * .45f - 55.5f*buttonScale / 2, 79.365f*buttonScale, 55.5f*buttonScale), Texture("all/flags/" + text.Flag));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("", Main(1), GUILayout.Width(width), GUILayout.Height(buttonHeight))) ChangeSettingsPage("MoreOptions");
                Outline(GUILayoutUtility.GetLastRect(), MoreOptionsLabel, Main(1));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("", Main(1), GUILayout.Width(width), GUILayout.Height(buttonHeight))) ChangeSettingsPage("About");
                Outline(GUILayoutUtility.GetLastRect(), T("ABOUT"), Main(1));
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("", Main(1), GUILayout.Width(width), GUILayout.Height(buttonHeight))) ChangeSettingsPage("Reset");
                Outline(GUILayoutUtility.GetLastRect(), T("RESET_GAME"), Main(1));
                GUILayout.FlexibleSpace();
                GUILayout.BeginHorizontal();
                // Game Center is iOS-only: keep the original buttons, without a destination.
                bool achievements=GUILayout.Button("",Main(44),GUILayout.Width(207*buttonScale),GUILayout.Height(109*buttonScale));
                Rect achievementButton=GUILayoutUtility.GetLastRect();achievementButton.position+=settingsButtons.position;SettingsAchievementsRect=achievementButton;if(achievements)OpenAchievements();
                GUILayout.FlexibleSpace(); if (GUILayout.Button("", Main(45), GUILayout.Width(207*buttonScale), GUILayout.Height(109*buttonScale))) game.Play(game.assets.button);
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical(); GUILayout.EndArea();
        }
        public string ControlModeLabel
        {
            get {
                string[] slingshot = { "Controls: Slingshot", "Управление: Рогатка", "Besturing: Katapult", "Contrôles : Fronde", "Steuerung: Schleuder", "Comandi: Fionda", "조작: 새총", "Controles: Estilingue", "Control: Tirachinas" };
                string[] flick = { "Controls: Flick", "Управление: Смахивание", "Besturing: Veeg", "Contrôles : Balayage", "Steuerung: Wischen", "Comandi: Scorri", "조작: 플릭", "Controles: Deslizar", "Control: Deslizar" };
                return (game.ControlMode == LaunchMode.Slingshot ? slingshot : flick)[text.CurrentLanguage];
            }
        }
        public string CameraFpsLockLabel
        {
            get {
                string[] labels = { "Camera FPS lock", "FPS камеры", "Camera-FPS vastzetten", "FPS de la caméra", "Kamera-FPS sperren", "Blocca FPS fotocamera", "카메라 FPS 고정", "Fixar FPS da câmera", "Fijar FPS de cámara" };
                string[] on = { "On", "Вкл", "Aan", "Oui", "An", "Sì", "켜짐", "Sim", "Sí" };
                string[] off = { "Off", "Выкл", "Uit", "Non", "Aus", "No", "꺼짐", "Não", "No" };
                return labels[text.CurrentLanguage] + ": " + (game.LockFpsToCamera ? on : off)[text.CurrentLanguage];
            }
        }
        void DrawMarker()
        {
            // ShowMarkerScreen.LoadResources and OnGUI, 0x40cb8 / 0x411e0.
            Background(Texture("all/background"));
            GUI.DrawTexture(R("ShowMarkerScreen$$GetMarkerRect"), Texture("all/marker"));
            GUI.DrawTexture(R("ShowMarkerScreen$$GetInstructionRect"), Texture("all/ipad_game"));
            GUI.Label(R("ShowMarkerScreen$$GetTextRect"), T("POINT_TO_MARKER"), markerText);
            if (Button(R("ShowMarkerScreen$$GetCloseBtnRect"), gameSkin.GetStyle("close_banner")))
                transition.Begin(game.Resume);
        }
        void OnDestroy()
        {
            if(game&&game.Achievements!=null){game.Achievements.Earned-=QueueAchievementNotification;game.Achievements.Reloaded-=ClearAchievementNotifications;}
            if(lanInputBackground)Destroy(lanInputBackground);if(lanScoreBackground)Destroy(lanScoreBackground);
            if(mainSkin)Destroy(mainSkin);if(gameSkin)Destroy(gameSkin);
            if (menuShadow) Destroy(menuShadow.gameObject);
            if (searchFocus) Destroy(searchFocus.gameObject);
            if (searchSticker) Destroy(searchSticker.gameObject);
            if (whiteFlash) Destroy(whiteFlash.gameObject);
        }
    }
}
