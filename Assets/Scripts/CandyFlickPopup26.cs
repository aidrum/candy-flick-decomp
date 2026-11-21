using System;
using UnityEngine;

namespace CandyFlick
{
    // Translation of PopupView (0x2cde4, 0x362bc–0x36e74): the game's own yellow note popup,
    // shown on iOS by the photo plugin via UnitySendMessage → ShowPopupWithMessageKey("PHOTO_SAVED").
    public sealed partial class CandyFlickUI
    {
        public static bool PopupActive { get; private set; }
        string popupMessage, popupButton, popupSecondaryButton;
        Action popupConfirm;
        int popupPressed, popupArmed;

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
            popupMessage = L(message); popupButton = T("OK");
            popupSecondaryButton=null;popupConfirm=null;
            popupPressed = popupArmed = 0; PopupActive = true;
        }
        public void ShowChoicePopup(string message,string confirm,string cancel,Action action)
        {
            ShowPopupMessage(message);popupButton=L(confirm);popupSecondaryButton=L(cancel);popupConfirm=action;
        }
        int PopupFontSize(string name) => Array.Find(fontProfiles.languages[text.CurrentLanguage].sizes,
            entry => entry.method == "FontSizeManager$$get_" + name).size;

        float PopupTop => (height-referenceHeight)/2;
        Rect CenteredPopupRect(string method) { Rect rect=R("PopupView$$"+method);rect.y+=PopupTop;return rect; }
        float PopupExtraHeight=>popupSecondaryButton==null?0:CenteredPopupRect("GetBtnRect").height+14;
        Rect PopupRect
        {
            get{Rect rect=CenteredPopupRect("GetPopupRect");rect.y-=PopupExtraHeight/2;rect.height+=PopupExtraHeight;return rect;}
        }
        Rect PopupMessageRect
        {
            get{Rect rect=CenteredPopupRect("GetMessageRect");rect.y-=PopupExtraHeight/2;return rect;}
        }
        Rect PopupButtonRect
        {
            get{Rect rect=CenteredPopupRect("GetBtnRect");rect.y-=PopupExtraHeight/2;return rect;}
        }
        Rect PopupCancelRect
        {
            get{Rect rect=PopupButtonRect;rect.y+=PopupExtraHeight;return rect;}
        }
        int PopupButtonAt(Vector2 point)=>PopupButtonRect.Contains(point)?1:popupSecondaryButton!=null&&PopupCancelRect.Contains(point)?2:0;

        // PopupView.Update: the button shows btn_pressed while touched and fires when the touch ends.
        // Events are consumed so controls beneath the popup do not react.
        void HandlePopupInput()
        {
            if (!PopupActive) return;
            Event current = Event.current;
            switch (current.type)
            {
                case EventType.MouseDown:
                    if(current.button==0)popupArmed=popupPressed=PopupButtonAt(current.mousePosition);
                    current.Use();break;
                case EventType.MouseDrag:
                    popupPressed=PopupButtonAt(current.mousePosition)==popupArmed?popupArmed:0;current.Use();break;
                case EventType.MouseUp:
                    int chosen=current.button==0&&PopupButtonAt(current.mousePosition)==popupArmed?popupArmed:0;
                    var action=chosen==1?popupConfirm:null;
                    popupPressed=popupArmed=0;current.Use();
                    if(chosen!=0){ClosePopup();action?.Invoke();}break;
                case EventType.KeyDown:
                    if(current.keyCode==KeyCode.Escape)ClosePopup();current.Use();break;
                case EventType.ScrollWheel:current.Use();break;
            }
        }
        void ClosePopup()
        {
            PopupActive = false;popupConfirm=null;popupSecondaryButton=null;popupPressed=popupArmed=0;
            game.Play(game.assets.button);
        } // OnButtonPressed.

        // PopupView.OnGUI: full-screen skin.box button (a semi-transparent black shade on the original,
        // drawn like the pause shade), popup, button, message, outlined button text.
        void DrawPopup()
        {
            if (!PopupActive) return;
            GUI.Button(fullScreen, "", gameSkin.box);
            Color saved = GUI.color; GUI.color = new Color(0, 0, 0, .5f);
            GUI.DrawTexture(fullScreen, Texture2D.whiteTexture); GUI.color = saved;
            GUI.DrawTexture(PopupRect, Texture(DevicePath("popup")+"popup"));
            DrawPopupButton(PopupButtonRect,popupButton,1);
            if(popupSecondaryButton!=null)DrawPopupButton(PopupCancelRect,popupSecondaryButton,2);
            var message=new GUIStyle(Game(43)){font=PortFont,wordWrap=true};
            while(message.fontSize>18&&message.CalcHeight(new GUIContent(popupMessage),PopupMessageRect.width)>PopupMessageRect.height)message.fontSize--;
            GUI.Label(PopupMessageRect, popupMessage, message);
        }
        void DrawPopupButton(Rect rect,string label,int button)
        {
            GUI.DrawTexture(rect,Texture(DevicePath("popup")+(popupPressed==button?"btn_pressed":"btn")));
            var style=new GUIStyle(Game(42)){font=PortFont};
            while(style.fontSize>18&&style.CalcSize(new GUIContent(label)).x>rect.width-28)style.fontSize--;
            Outline(rect,label,style);
        }
    }
}
