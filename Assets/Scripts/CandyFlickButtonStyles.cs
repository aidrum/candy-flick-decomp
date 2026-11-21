using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        static void ConfigureButtonFeedback(GUISkin skin)
        {
            foreach(var style in skin.customStyles)PressOnlyButtonStyle(style,true);
            foreach(var style in new[]{skin.button,skin.toggle,skin.box,
                skin.horizontalScrollbarLeftButton,skin.horizontalScrollbarRightButton,
                skin.verticalScrollbarUpButton,skin.verticalScrollbarDownButton,
                skin.horizontalScrollbarThumb,skin.verticalScrollbarThumb})PressOnlyButtonStyle(style,true);
        }

        static GUIStyle PressOnlyButtonStyle(GUIStyle style,bool recoverPressedArtwork=false)
        {
            // Some recovered styles put their pressed artwork only in Hover/Focused.
            // Preserve that artwork as Active before removing hover/focus feedback.
            if(recoverPressedArtwork&&(!style.active.background||style.active.background==style.normal.background))
            {
                if(style.hover.background&&style.hover.background!=style.normal.background)style.active=style.hover;
                else if(style.focused.background&&style.focused.background!=style.normal.background)style.active=style.focused;
            }
            if(recoverPressedArtwork&&(!style.onActive.background||style.onActive.background==style.onNormal.background))
            {
                if(style.onHover.background&&style.onHover.background!=style.onNormal.background)style.onActive=style.onHover;
                else if(style.onFocused.background&&style.onFocused.background!=style.onNormal.background)style.onActive=style.onFocused;
            }
            style.hover=style.normal;style.focused=style.normal;
            style.onHover=style.onNormal;style.onFocused=style.onNormal;
            return style;
        }
    }
}
