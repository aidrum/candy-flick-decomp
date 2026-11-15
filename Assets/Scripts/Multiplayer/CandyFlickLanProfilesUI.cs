using CandyFlick.Multiplayer;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        bool lanProfilePicker,lanProfilePointerDown,lanProfileDragging;Vector2 lanProfileScroll,lanProfileDragStart,lanProfileScrollStart;Texture2D lanProfileAtlas;
        public Rect LanProfileButtonRect=>new Rect(76,92,62,62);
        public Rect LanProfilePickerRect=>new Rect(40,(height-Mathf.Min(730,height-90))/2,560,Mathf.Min(730,height-90));
        Rect LanProfileGridRect
        {
            get{Rect panel=LanProfilePickerRect;return new Rect(panel.x+24,panel.y+86,panel.width-48,panel.height-172);}
        }
        Rect LanProfileChoiceRect(int id)
        {
            float step=(LanProfileGridRect.width-20)/7;
            return new Rect(id%7*step,id/7*step,step,step);
        }
        void DrawLanProfile(Rect rect,int id)
        {
            if(!lanProfileAtlas)lanProfileAtlas=Resources.Load<Texture2D>(LanProfiles.Resource);
            if(lanProfileAtlas)GUI.DrawTextureWithTexCoords(rect,lanProfileAtlas,LanProfiles.UV(id));
        }
        void OpenLanProfilePicker()
        {
            CommitLanName();lanProfilePicker=true;lanProfilePointerDown=lanProfileDragging=false;
            lanProfileScroll=new Vector2(0,Mathf.Max(0,LanProfileChoiceRect(game.LAN.ProfilePicture).yMax-LanProfileGridRect.height));
        }
        void HandleLanProfileDrag(Rect viewport,float contentHeight)
        {
            Event current=Event.current;Vector2 point=current.mousePosition;Rect dragArea=viewport;dragArea.width-=20;
            if(current.type==EventType.MouseDown&&current.button==0&&dragArea.Contains(point))
            {lanProfilePointerDown=true;lanProfileDragging=false;lanProfileDragStart=point;lanProfileScrollStart=lanProfileScroll;}
            if(current.type==EventType.MouseDrag&&lanProfilePointerDown)
            {
                if((point-lanProfileDragStart).sqrMagnitude>64)lanProfileDragging=true;
                if(lanProfileDragging)
                {
                    GUIUtility.hotControl=0;lanProfileScroll.y=Mathf.Clamp(lanProfileScrollStart.y+lanProfileDragStart.y-point.y,0,Mathf.Max(0,contentHeight-viewport.height));current.Use();
                }
            }
            if(current.rawType==EventType.MouseUp&&lanProfilePointerDown)
            {
                lanProfilePointerDown=false;if(lanProfileDragging){GUIUtility.hotControl=0;current.Use();}lanProfileDragging=false;
            }
        }
        void SelectLanProfile(int id){game.LAN.SetProfilePicture(id);lanProfilePicker=false;}
        void DrawLanIdentity(Rect rect,string name,int avatar,int fontSize,Color color,bool outlined=false)
        {
            float size=Mathf.Min(outlined?34:42,rect.height-4),gap=8;
            float available=rect.width-size-gap;
            var style=LanText(fontSize,color,TextAnchor.MiddleLeft);style.wordWrap=false;
            while(style.fontSize>14&&style.CalcSize(new GUIContent(name)).x>available)style.fontSize--;
            float textWidth=Mathf.Min(available,style.CalcSize(new GUIContent(name)).x+2);
            float x=rect.center.x-(size+gap+textWidth)/2;
            DrawLanProfile(new Rect(x,rect.center.y-size/2,size,size),avatar);
            Rect label=new Rect(x+size+gap,rect.y,textWidth,rect.height);
            if(outlined)Outline(label,name,style);else GUI.Label(label,name,style);
        }
        void DrawLanProfilePicker()
        {
            Color saved=GUI.color;GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(fullScreen,Texture2D.whiteTexture);GUI.color=saved;
            Rect panel=LanProfilePickerRect;
            var background=new GUIStyle{border=new RectOffset(12,12,12,12),normal={background=LanInputBackground}};
            GUI.color=new Color(1,.91f,.65f);GUI.Box(panel,GUIContent.none,background);GUI.color=saved;
            GUI.Label(new Rect(panel.x+12,panel.y+14,panel.width-24,55),"Profile picture",LanText(38));
            Rect viewport=LanProfileGridRect;float step=(viewport.width-20)/7;
            float contentHeight=Mathf.CeilToInt(LanProfiles.Count/7f)*step;HandleLanProfileDrag(viewport,contentHeight);
            lanProfileScroll=GUI.BeginScrollView(viewport,lanProfileScroll,new Rect(0,0,viewport.width-20,contentHeight));
            for(int id=0;id<LanProfiles.Count;id++)
            {
                Rect cell=LanProfileChoiceRect(id);float size=Mathf.Min(60,step-10);
                Rect frame=new Rect(cell.center.x-size/2-3,cell.center.y-size/2-3,size+6,size+6);
                Color tint=GUI.color;GUI.color=id==game.LAN.ProfilePicture?Color.yellow:new Color(0,0,0,.3f);GUI.DrawTexture(frame,Texture2D.whiteTexture);GUI.color=tint;
                DrawLanProfile(new Rect(frame.x+3,frame.y+3,size,size),id);
                if(Button(cell,GUIStyle.none))SelectLanProfile(id);
            }
            GUI.EndScrollView();
            if(LanButton(LanButtonRect(panel.yMax-64,220),"Back"))lanProfilePicker=false;
            Event current=Event.current;
            if(current.type==EventType.KeyDown&&current.keyCode==KeyCode.Escape){lanProfilePicker=false;current.Use();}
            else if(current.type==EventType.MouseDown&&!panel.Contains(current.mousePosition)){lanProfilePicker=false;current.Use();}
        }
    }
}
