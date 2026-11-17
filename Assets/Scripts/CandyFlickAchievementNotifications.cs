using System.Collections.Generic;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        const float AchievementFadeIn=.18f,AchievementHold=2f,AchievementFadeOut=.32f;
        readonly Queue<LocalAchievements.Entry> achievementNotifications=new Queue<LocalAchievements.Entry>();
        LocalAchievements.Entry activeAchievementNotification;
        float achievementNotificationStarted;
        Texture2D achievementNotificationBackground;
        AudioClip achievementNotificationSound;
        public LocalAchievements.Entry ActiveAchievementNotification=>activeAchievementNotification;
        public int PendingAchievementNotifications=>achievementNotifications.Count;
        public float AchievementNotificationAlpha
        {
            get
            {
                if(activeAchievementNotification==null)return 0;
                float age=Time.unscaledTime-achievementNotificationStarted;
                return Mathf.Clamp01(Mathf.Min(age/AchievementFadeIn,(AchievementFadeIn+AchievementHold+AchievementFadeOut-age)/AchievementFadeOut));
            }
        }
        public Rect AchievementNotificationRect
        {
            get
            {
                float width=Mathf.Min(Screen.width,Screen.height*.75f)*.94f;
                float h=width*259/857;
                return new Rect(Screen.width-width,Screen.height-h,width,h);
            }
        }
        void QueueAchievementNotification(LocalAchievements.Entry entry)=>achievementNotifications.Enqueue(entry);
        void ClearAchievementNotifications(){achievementNotifications.Clear();activeAchievementNotification=null;}
        void UpdateAchievementNotifications()
        {
            if(activeAchievementNotification!=null&&Time.unscaledTime-achievementNotificationStarted>=AchievementFadeIn+AchievementHold+AchievementFadeOut)activeAchievementNotification=null;
            if(activeAchievementNotification!=null||achievementNotifications.Count==0)return;
            activeAchievementNotification=achievementNotifications.Dequeue();achievementNotificationStarted=Time.unscaledTime;
            if(!achievementNotificationSound)achievementNotificationSound=Resources.Load<AudioClip>("LanAudio/calibrated");
            game.Play(achievementNotificationSound);
        }
        void AchievementNotificationTextLayout(LocalAchievements.Entry entry,float width,float h,out Rect titleRect,out Rect descriptionRect,out GUIStyle title,out GUIStyle description)
        {
            float unit=Mathf.Min(1.5f,width/400),gap=2*unit;
            title=new GUIStyle(AchievementNameStyle){fontSize=Mathf.RoundToInt(26*unit)};
            description=new GUIStyle(AchievementDescriptionStyle){fontSize=Mathf.RoundToInt(20*unit)};
            Rect content=new Rect(width*.34f,h*.30f,width*.64f,h*.65f);
            float titleHeight,descriptionHeight;
            while(true)
            {
                titleHeight=title.CalcHeight(new GUIContent(entry.name),content.width);
                descriptionHeight=description.CalcHeight(new GUIContent(entry.description),content.width);
                if(titleHeight+gap+descriptionHeight<=content.height||title.fontSize<=10||description.fontSize<=8)break;
                title.fontSize--;description.fontSize--;
            }
            titleRect=new Rect(content.x,content.y,content.width,titleHeight);
            descriptionRect=new Rect(content.x,titleRect.yMax+gap,content.width,descriptionHeight);
        }
        void DrawAchievementNotification()
        {
            var entry=activeAchievementNotification;if(entry==null)return;
            if(!achievementNotificationBackground)achievementNotificationBackground=Resources.Load<Texture2D>("Achievements/notification");
            if(!achievementAtlas)achievementAtlas=Resources.Load<Texture2D>("Achievements/icons");
            Rect box=AchievementNotificationRect;float scale=CanvasScale(Screen.safeArea),width=box.width/scale,h=box.height/scale;
            Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;
            try
            {
                GUI.matrix=Matrix4x4.TRS(new Vector3(box.x,box.y,0),Quaternion.identity,Vector3.one*scale);
                GUI.color=new Color(1,1,1,AchievementNotificationAlpha);
                GUI.DrawTexture(new Rect(0,0,width,h),achievementNotificationBackground);
                GUI.DrawTextureWithTexCoords(new Rect(width*.18f,h*.42f,width*.126f,width*.126f),achievementAtlas,AchievementUV(entry.uv));
                AchievementNotificationTextLayout(entry,width,h,out var titleRect,out var descriptionRect,out var title,out var description);
                Outline(titleRect,entry.name,title);GUI.Label(descriptionRect,entry.description,description);
            }
            finally{GUI.matrix=matrix;GUI.color=color;}
        }
    }
}
