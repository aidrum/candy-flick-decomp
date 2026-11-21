using System;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        public enum TutorialPlayMode { Image, Ground, NonAR }

        public void LaunchTutorial(TutorialPlayMode mode)
        {
            if(mode==TutorialPlayMode.Ground&&!game.GroundSupport.Supported)
            {
                ShowPopupMessage(game.GroundSupport.Reason);return;
            }
            transition.Begin(()=>
            {
                bool useAR=mode!=TutorialPlayMode.NonAR;
                bool? ground=useAR?(bool?)(mode==TutorialPlayMode.Ground):null;
                game.SetTutorialARMode(useAR,ground,!tutorialStartsNewGame);
                tutorialReturnToMenu=false;
                if(tutorialStartsNewGame)game.StartGame(useAR);
            });
        }

        // 640-unit menu canvas: compact tablet/short-phone layout, with more space on tall phones.
        // Title, marker pictures, two image actions, separator, then two alternative actions.
        public static Rect[] TutorialStartLayout(float availableHeight,float arrowCenter=448)
        {
            float t=Mathf.InverseLerp(853,1136,availableHeight);
            float cardHeight=Mathf.Lerp(238,316,t),cardWidth=cardHeight*586/339;
            float buttonSize=Mathf.Lerp(80,104,t),rowTop=Mathf.Lerp(386,552,t),rowGap=Mathf.Lerp(96,124,t);
            float buttonX=arrowCenter-buttonSize/2;
            float separatorY=rowTop+2*rowGap+20;
            return new[]{
                new Rect(35,Mathf.Lerp(60,110,t),570,70),
                new Rect((640-cardWidth)/2,Mathf.Lerp(136,196,t),cardWidth,cardHeight),
                new Rect(buttonX,rowTop,buttonSize,buttonSize),
                new Rect(buttonX,rowTop+rowGap,buttonSize,buttonSize),
                new Rect(0,separatorY,640,8),
                new Rect(278,separatorY-32,84,71),
                new Rect(293,separatorY-27,56,40),
                new Rect(buttonX,separatorY+48,buttonSize,buttonSize),
                new Rect(buttonX,separatorY+48+rowGap,buttonSize,buttonSize)
            };
        }
        public Rect[] TutorialStartRects=>TutorialStartLayout(height,R("TutorStartScreen$$GetHaveMarkerBtnRect").center.x);
        public static Rect TutorialStartLabelRect(Rect button)=>new Rect(32,button.y,button.x-52,button.height);
        public string[] TutorialStartLabels=>new[]{T("TUTOR_HAVE_MARKER"),T("TUTOR_GETIMG"),L("Ground detection mode"),L("Play with AR off")};

        public GUIStyle TutorialStartRowStyle(Rect[] layout)
        {
            var style=new GUIStyle(gameSkin.GetStyle("label_tutor1_2")){
                font=PortFont,fontSize=Mathf.RoundToInt(Mathf.Lerp(38,46,Mathf.InverseLerp(853,1136,height))),
                alignment=TextAnchor.MiddleRight,wordWrap=false,fixedHeight=0,fixedWidth=0
            };
            float width=TutorialStartLabelRect(layout[2]).width;
            foreach(string label in TutorialStartLabels)
                while(style.fontSize>26&&style.CalcSize(new GUIContent(label)).x>width)style.fontSize--;
            style.wordWrap=true;
            foreach(string label in TutorialStartLabels)
                while(style.fontSize>20&&style.CalcHeight(new GUIContent(label),width)>layout[2].height)style.fontSize--;
            return style;
        }
        void DrawTutorialStart()
        {
            Rect[] layout=TutorialStartRects;
            var title=new GUIStyle(gameSkin.GetStyle("label_tutor1_1")){alignment=TextAnchor.MiddleCenter,wordWrap=true,fixedHeight=0};
            while(title.fontSize>22&&title.CalcHeight(new GUIContent(T("TUTOR_TITLE")),layout[0].width)>layout[0].height)title.fontSize--;
            GUI.Label(layout[0],T("TUTOR_TITLE"),title);
            GUI.DrawTexture(layout[1],Texture(DevicePath("tutorial")+"two_markers"));
            // Keep the original translated 'or' on top of the marker illustration.
            Rect nativeCards=R("TutorStartScreen$$GetMarkersRect"),nativeOr=R("TutorStartScreen$$GetOrRect");
            float scale=layout[1].width/nativeCards.width;
            var markerOr=new Rect(layout[1].x+(nativeOr.x-nativeCards.x)*scale,layout[1].y+(nativeOr.y-nativeCards.y)*scale,nativeOr.width*scale,nativeOr.height*scale);
            var orStyle=new GUIStyle(gameSkin.GetStyle("orLabel")){fontSize=Mathf.RoundToInt(NativeFontSize("TutorialOrFontSize")*scale)};
            GUI.Label(markerOr,T("TUTOR_OR"),orStyle);

            var line=Texture(DevicePath("tutorial")+"dot_line");if(line)GUI.DrawTexture(layout[4],line);
            GUI.DrawTexture(layout[5],Texture(DevicePath("tutorial")+"or"));
            orStyle.fontSize=Mathf.RoundToInt(NativeFontSize("TutorialOrFontSize")*.77f);
            GUI.Label(layout[6],T("TUTOR_OR"),orStyle);

            GUIStyle row=TutorialStartRowStyle(layout);string[] labels=TutorialStartLabels;
            Action[] actions={FinishTutorial,()=>SetTutorialPage(TutorialPage.CreateMarker),()=>LaunchTutorial(TutorialPlayMode.Ground),()=>LaunchTutorial(TutorialPlayMode.NonAR)};
            int[] buttons={2,3,7,8};
            for(int i=0;i<buttons.Length;i++)
            {
                Rect button=layout[buttons[i]];
                GUI.Label(TutorialStartLabelRect(button),labels[i],row);
                if(Button(button,gameSkin.GetStyle("next_btn")))actions[i]();
            }
        }
    }
}
