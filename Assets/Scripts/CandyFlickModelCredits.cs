using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        public const string ModelCreditsTitle = "Model Credits";
        public static readonly string[] ModelCreditText = {
            "\"Coffee Mug (School Project)\" (https://skfb.ly/6toBy) by Ole Gunnar Isager is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/).",
            "\"Lunch Plate (School Project)\" (https://skfb.ly/6toBA) by Ole Gunnar Isager is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/).",
            "\"Cookie (School Project)\" (https://skfb.ly/6toBB) by Ole Gunnar Isager is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/).",
            "Om Nelle Model: MlopsFunny"
        };
        public Rect CreditsBackRect => R("SettingScreen$$GetBackBtnRect",true);
        public Rect CreditsNextRect { get { Rect rect=CreditsBackRect;rect.x=640-rect.xMax;return rect; } }
        bool CreditsNextButton()
        {
            Rect rect=CreditsNextRect;Matrix4x4 saved=GUI.matrix;
            try
            {
                GUI.matrix=saved*Matrix4x4.TRS(new Vector3(rect.center.x*2,0,0),Quaternion.identity,new Vector3(-1,1,1));
                return GUI.Button(rect,"",Main(33)); // ChangeSettingsPage owns the button sound.
            }
            finally { GUI.matrix=saved; }
        }
        void DrawModelCredits()
        {
            var title=new GUIStyle(Main(43)){font=PortFont,fontSize=66,alignment=TextAnchor.MiddleCenter,wordWrap=false,fixedHeight=0};
            title.normal.textColor=Color.black;
            title.normal.background=null;
            string heading=L(ModelCreditsTitle);while(title.fontSize>28&&title.CalcSize(new GUIContent(heading)).x>520)title.fontSize--;
            GUI.Label(new Rect(60,120,520,85),heading,title);
            var body=new GUIStyle(Main(42)){font=PortFont,fontSize=30,alignment=TextAnchor.UpperCenter,wordWrap=true,fixedHeight=0};
            body.normal.textColor=Color.black;
            body.normal.background=null;
            Rect[] entries=ModelCreditsLayout(body,height);
            for(int i=0;i<entries.Length;i++)GUI.Label(entries[i],L(ModelCreditText[i]),body);
        }
        public static Rect[] ModelCreditsLayout(GUIStyle body,float availableHeight)
        {
            string[] localized=ModelCreditText.Select(PortLocalization.Translate).ToArray();
            const float width=528,top=225,gap=18;
            float Measure()
            {
                float sum=gap*(ModelCreditText.Length-1);
                foreach(string entry in localized)sum+=Mathf.Ceil(body.CalcHeight(new GUIContent(entry),width));
                return sum;
            }
            while(body.fontSize>16&&(top+Measure()>availableHeight-175||body.CalcSize(new GUIContent("http://creativecommons.org/licenses/by/4.0/")).x>width))body.fontSize--;
            var rectangles=new Rect[ModelCreditText.Length];float y=top;
            for(int i=0;i<rectangles.Length;i++)
            {
                float h=Mathf.Ceil(body.CalcHeight(new GUIContent(localized[i]),width));
                rectangles[i]=new Rect(56,y,width,h);y+=h+gap;
            }
            return rectangles;
        }
    }
}
