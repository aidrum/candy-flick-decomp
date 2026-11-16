using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        public Rect SettingsAchievementsRect {get;private set;}
        public Rect ResultsAchievementsRect {get;private set;}
        Vector2 achievementScroll;LanListDrag achievementDrag;
        float achievementThumbGrab;
        GUIStyle achievementNameStyle,achievementDescriptionStyle;
        Texture2D achievementAtlas,achievementTrack,achievementThumb,achievementThumbPressed;
        GUIStyle AchievementNameStyle=>achievementNameStyle??(achievementNameStyle=new GUIStyle{font=game.assets.font,fontSize=38,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=Color.white}});
        GUIStyle AchievementDescriptionStyle=>achievementDescriptionStyle??(achievementDescriptionStyle=new GUIStyle{font=game.assets.font,fontSize=30,wordWrap=true,alignment=TextAnchor.UpperLeft,normal={textColor=Color.black}});
        public Rect AchievementsViewportRect=>new Rect(28,145,540,Mathf.Max(120,R("SettingScreen$$GetBackBtnRect",true).y-165));
        public Rect AchievementsScrollbarRect {get{Rect view=AchievementsViewportRect;return new Rect(588,view.y+20,15,view.height-40);}}
        public float AchievementContentHeight
        {
            get{float total=0;foreach(var entry in game.Achievements.Definitions.entries)total+=AchievementRowHeight(entry);return total;}
        }
        float AchievementRowHeight(LocalAchievements.Entry entry)=>Mathf.Max(168,AchievementNameStyle.CalcHeight(new GUIContent(entry.name),378)+AchievementDescriptionStyle.CalcHeight(new GUIContent(entry.description),378)+40);
        public void OpenAchievements(bool fromResults=false)
        {
            game.Play(game.assets.button);
            transition.Begin(()=>{settingsPage="Achievements";gameCenterFromResults=fromResults;achievementScroll=Vector2.zero;achievementDrag=default;game.Show(GameScreen.Settings);});
        }
        static Rect AchievementUV(float[] uv)=>new Rect(uv[0],uv[1],uv[2],uv[3]);
        void DrawAchievements()
        {
            if(!achievementAtlas)
            {
                achievementAtlas=Resources.Load<Texture2D>("Achievements/icons");achievementTrack=Resources.Load<Texture2D>("Achievements/track");
                achievementThumb=Resources.Load<Texture2D>("Achievements/thumb");achievementThumbPressed=Resources.Load<Texture2D>("Achievements/thumb_pressed");
            }
            Outline(new Rect(35,44,570,74),"Achievements",new GUIStyle(AchievementNameStyle){fontSize=49,alignment=TextAnchor.MiddleCenter});
            Rect view=AchievementsViewportRect;float contentHeight=AchievementContentHeight;
            HandleLanListDrag(view,contentHeight,ref achievementScroll,ref achievementDrag);
            achievementScroll=GUI.BeginScrollView(view,achievementScroll,new Rect(0,0,view.width,contentHeight),false,false,GUIStyle.none,GUIStyle.none);
            float y=0;
            foreach(var entry in game.Achievements.Definitions.entries)
            {
                float row=AchievementRowHeight(entry),nameHeight=AchievementNameStyle.CalcHeight(new GUIContent(entry.name),378);
                GUI.DrawTextureWithTexCoords(new Rect(22,y+17,112,112),achievementAtlas,AchievementUV(game.Achievements.Unlocked(entry)?entry.uv:game.Achievements.Definitions.locked));
                Outline(new Rect(148,y+16,378,nameHeight),entry.name,AchievementNameStyle);
                GUI.Label(new Rect(148,y+nameHeight+24,378,row-nameHeight-24),entry.description,AchievementDescriptionStyle);
                y+=row;
            }
            GUI.EndScrollView();
            DrawAchievementScrollbar(contentHeight);
        }
        void DrawAchievementScrollbar(float contentHeight)
        {
            const float thumbSize=84;
            Rect track=AchievementsScrollbarRect;float maximum=Mathf.Max(0,contentHeight-AchievementsViewportRect.height),travel=Mathf.Max(1,track.height-thumbSize);
            achievementScroll.y=Mathf.Clamp(achievementScroll.y,0,maximum);
            Rect thumb=new Rect(track.center.x-thumbSize/2,track.y+(maximum>0?achievementScroll.y/maximum*travel:0),thumbSize,thumbSize);
            Rect hit=new Rect(thumb.x,track.y,thumb.width,track.height);
            int id=GUIUtility.GetControlID(873961,FocusType.Passive,hit);Event e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&hit.Contains(e.mousePosition))
            {
                GUIUtility.hotControl=id;achievementThumbGrab=thumb.Contains(e.mousePosition)?e.mousePosition.y-thumb.y:thumbSize/2;
                achievementScroll.y=Mathf.Clamp01((e.mousePosition.y-track.y-achievementThumbGrab)/travel)*maximum;e.Use();
            }
            if(e.type==EventType.MouseDrag&&GUIUtility.hotControl==id){achievementScroll.y=Mathf.Clamp01((e.mousePosition.y-track.y-achievementThumbGrab)/travel)*maximum;e.Use();}
            if(e.rawType==EventType.MouseUp&&GUIUtility.hotControl==id){GUIUtility.hotControl=0;e.Use();}
            thumb.y=track.y+(maximum>0?achievementScroll.y/maximum*travel:0);
            GUI.DrawTexture(track,achievementTrack);
            GUI.DrawTexture(thumb,GUIUtility.hotControl==id?achievementThumbPressed:achievementThumb,ScaleMode.ScaleToFit);
        }
    }
}
