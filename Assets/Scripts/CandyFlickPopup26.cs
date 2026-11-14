using System;
using UnityEngine;

namespace CandyFlick
{
    // Translation of PopupView (0x2cde4, 0x362bc–0x36e74): the game's own yellow note popup,
    // shown on iOS by the photo plugin via UnitySendMessage → ShowPopupWithMessageKey("PHOTO_SAVED").
    public sealed partial class CandyFlickUI
    {
        public static bool PopupActive { get; private set; }
        string popupMessage, popupButton;
        bool popupPressed;

        // ShowPopupWithMessageKey: message text, localized "OK", popupBtnText/popupMessage font sizes.
        public void ShowPopupWithMessageKey(string messageKey)
        {
            Init(); ShowPopupMessage(T(messageKey));
        }
        void ShowPopupMessage(string message)
        {
            Init();
            Game(42).fontSize = PopupFontSize("PopupBtnFs");
            Game(43).fontSize = PopupFontSize("PopupMessageFs");
            popupMessage = message; popupButton = T("OK");
            popupPressed = false; PopupActive = true;
        }
        int PopupFontSize(string name) => Array.Find(fontProfiles.languages[text.CurrentLanguage].sizes,
            entry => entry.method == "FontSizeManager$$get_" + name).size;

        float PopupTop => (height-referenceHeight)/2;
        Rect CenteredPopupRect(string method) { Rect rect=R("PopupView$$"+method);rect.y+=PopupTop;return rect; }
        Rect PopupRect=>CenteredPopupRect("GetPopupRect");
        Rect PopupMessageRect=>CenteredPopupRect("GetMessageRect");
        Rect PopupButtonRect=>CenteredPopupRect("GetBtnRect");

        // PopupView.Update: the button shows btn_pressed while touched and fires when the touch ends.
        // Events are consumed so controls beneath the popup do not react.
        void HandlePopupInput()
        {
            if (!PopupActive) return;
            Event current = Event.current;
            switch (current.type)
            {
                case EventType.MouseDown:
                case EventType.MouseDrag:
                    popupPressed = PopupButtonRect.Contains(current.mousePosition); current.Use(); break;
                case EventType.MouseUp:
                    if (popupPressed) ClosePopup();
                    popupPressed = false; current.Use(); break;
            }
        }
        void ClosePopup() { PopupActive = false; game.Play(game.assets.button); } // OnButtonPressed.

        // PopupView.OnGUI: full-screen skin.box button (a semi-transparent black shade on the original,
        // drawn like the pause shade), popup, button, message, outlined button text.
        void DrawPopup()
        {
            if (!PopupActive) return;
            GUI.Button(fullScreen, "", gameSkin.box);
            Color saved = GUI.color; GUI.color = new Color(0, 0, 0, .5f);
            GUI.DrawTexture(fullScreen, Texture2D.whiteTexture); GUI.color = saved;
            GUI.DrawTexture(PopupRect, Texture(DevicePath("popup")+"popup"));
            GUI.DrawTexture(PopupButtonRect, Texture(DevicePath("popup")+(popupPressed?"btn_pressed":"btn")));
            GUI.Label(PopupMessageRect, popupMessage, Game(43));
            Outline(PopupButtonRect, popupButton, Game(42));
        }
    }
}
