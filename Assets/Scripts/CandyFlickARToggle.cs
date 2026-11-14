using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        GUIStyle arToggle,arLabel;
        Texture2D arOn,arOff;
        public Rect ARToggleRect=>new Rect(CanvasWidth-150,R("ScoresGPView$$GetTotalScoresRect").yMax+66,136,136);
        public Rect ARLabelRect { get { Rect rect=ARToggleRect;rect.y-=45;rect.height=44;return rect; } }
        void DrawARToggle()
        {
            if(arToggle==null)
            {
                arOn=Resources.Load<Texture2D>("ARToggle/ar_on");arOff=Resources.Load<Texture2D>("ARToggle/ar_off");
                arToggle=new GUIStyle();
                arLabel=new GUIStyle {font=game.assets.font,fontSize=48,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            }
            Texture2D image=game.IsNonAR?arOff:arOn;
            arToggle.normal.background=arToggle.hover.background=arToggle.active.background=image;
            Outline(ARLabelRect,"AR",arLabel);
            if(Button(ARToggleRect,arToggle))game.ToggleARMode();
        }
    }
}
