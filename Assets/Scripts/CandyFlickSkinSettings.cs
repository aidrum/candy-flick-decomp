using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        Vector2 skinScroll, skinDragStart, skinScrollStart;
        bool skinPointerDown, skinDragging;
        GUIStyle skinsMenuButton, skinCard, selectedSkinCard, skinTitle, randomSkinLabel;
        Texture2D skinsBackground, skinPortraits;
        const float CardWidth=176, CardHeight=230, SkinRow=250;
        public const int SkinColumns=3;
        public static float SkinGridHeight=>Mathf.Ceil((TimeTravelSkins.Names.Length+1)/(float)SkinColumns)*SkinRow-20;
        public string SettingsPage => settingsPage;
        public Vector2 SkinScroll => skinScroll;
        public Rect SkinViewport => new Rect(28,155,584,height-330);
        public Rect MoreControlsRect => new Rect(87,height/2-273,466,111);
        public Rect MoreFpsRect => new Rect(87,height/2-128,466,111);
        public Rect MoreHideCandyRect => new Rect(87,height/2+17,466,111);
        public Rect MoreMusicRect => new Rect(87,height/2+162,466,111);
        public Rect MenuSkinsRect { get { Rect rect=R("MainScreen$$GetShareRect");rect.y+=48;return rect; } }
        GUIStyle SkinsMenuButtonStyle { get { InitSkinMenu(); return skinsMenuButton; } }
        void InitSkinMenu()
        {
            if(skinsMenuButton!=null)return;
            Texture2D Load(string name)=>Resources.Load<Texture2D>("SkinMenu/"+name);
            skinsBackground=Load("skins_bg"); skinPortraits=Load("nommies");
            skinsMenuButton=new GUIStyle { normal={background=Load("skins_btn")}, hover={background=Load("skins_btn")}, active={background=Load("skins_btn_pressed")} };
            skinCard=new GUIStyle {normal={background=Load("skin_btn_bg")}};
            selectedSkinCard=new GUIStyle {normal={background=Load("skin_btn_bg_selected")}};
            skinTitle=new GUIStyle {font=game.assets.font,fontSize=76,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            randomSkinLabel=new GUIStyle(skinTitle){fontSize=46};
        }
        public void OpenSkins()
        {
            InitSkinMenu();
            transition.Begin(()=> { settingsPage=null;skinScroll=Vector2.zero;skinPointerDown=skinDragging=false;game.Show(GameScreen.Skins); });
        }
        public static Rect SkinCardRect(int index)=>new Rect(4+188*(index%SkinColumns),SkinRow*(index/SkinColumns),CardWidth,CardHeight);
        public static Rect SkinPortraitUv(int index)=>new Rect((index%4)/4f,1-(index/4+1)/2f,1/4f,1/2f);
        void DrawSkins()
        {
            InitSkinMenu(); Background(skinsBackground);
            Outline(new Rect(0,25,640,105),"Skins",skinTitle);
            Rect viewport=SkinViewport;
            float contentHeight=SkinGridHeight;
            HandleSkinDrag(viewport,contentHeight);
            skinScroll=GUI.BeginScrollView(viewport,skinScroll,new Rect(0,0,560,contentHeight),false,false);
            for(int i=0;i<TimeTravelSkins.Names.Length;i++)
            {
                Rect card=SkinCardRect(i);
                if(Button(card,!game.Skins.RandomSelected&&(int)game.Skins.Selected==i?selectedSkinCard:skinCard))game.Skins.Select((NommieSkin)i);
                // Keep the portrait above the selected card's checkmark.
                if(skinPortraits)GUI.DrawTextureWithTexCoords(new Rect(card.center.x-75,card.y+12,150,150),skinPortraits,SkinPortraitUv(i),true);
            }
            Rect randomCard=SkinCardRect(TimeTravelSkins.RandomChoice);
            if(Button(randomCard,game.Skins.RandomSelected?selectedSkinCard:skinCard))game.Skins.SelectRandom();
            Outline(new Rect(randomCard.x,randomCard.y+12,randomCard.width,150),"Random",randomSkinLabel);
            GUI.EndScrollView();
            if(Button(R("SettingScreen$$GetBackBtnRect",true),Main(33)))Back();
        }
        void HandleSkinDrag(Rect viewport,float contentHeight)
        {
            Event current=Event.current;
            // IMGUI expresses mousePosition in the current GUI matrix.
            Vector2 point=current.mousePosition;
            Rect dragArea=viewport;dragArea.width-=24; // Leave the scrollbar's own drag control available.
            if(current.type==EventType.MouseDown && current.button==0 && dragArea.Contains(point))
            { skinPointerDown=true;skinDragging=false;skinDragStart=point;skinScrollStart=skinScroll; }
            if(current.type==EventType.MouseDrag && skinPointerDown)
            {
                if((point-skinDragStart).sqrMagnitude>64)skinDragging=true;
                if(skinDragging)
                {
                    GUIUtility.hotControl=0;
                    skinScroll.y=Mathf.Clamp(skinScrollStart.y+skinDragStart.y-point.y,0,Mathf.Max(0,contentHeight-viewport.height));
                    current.Use();
                }
            }
            if(current.rawType==EventType.MouseUp && skinPointerDown)
            {
                skinPointerDown=false;
                if(skinDragging){GUIUtility.hotControl=0;current.Use();}
                skinDragging=false;
            }
        }
        string MoreOptionsLabel=>new[]{"More Options","Дополнительно","Meer opties","Plus d’options","Weitere Optionen","Altre opzioni","추가 옵션","Mais opções","Más opciones"}[text.CurrentLanguage];
        void DrawMoreOptions()
        {
            DrawSettingsOption(MoreControlsRect,ControlModeLabel,game.ToggleControlMode);
            DrawSettingsOption(MoreFpsRect,CameraFpsLockLabel,game.ToggleCameraFpsLock);
            DrawSettingsOption(MoreHideCandyRect,HideCandyLabel,game.ToggleHideCandyInPictures);
            DrawSettingsOption(MoreMusicRect,InGameMusicLabel,game.ToggleInGameMusic);
        }
        string OptionLabel(string[] labels,bool enabled)
        {
            string[] on={"On","Вкл","Aan","Oui","An","Sì","켜짐","Sim","Sí"};
            string[] off={"Off","Выкл","Uit","Non","Aus","No","꺼짐","Não","No"};
            return labels[text.CurrentLanguage]+": "+(enabled?on:off)[text.CurrentLanguage];
        }
        string HideCandyLabel=>OptionLabel(new[]{"Hide candy in pictures","Скрывать конфету на фото","Snoep verbergen op foto's","Masquer le bonbon en photo","Bonbon auf Fotos ausblenden","Nascondi caramella nelle foto","사진에서 사탕 숨기기","Ocultar doce nas fotos","Ocultar caramelo en fotos"},game.HideCandyInPictures);
        string InGameMusicLabel=>OptionLabel(new[]{"In-game music","Музыка в игре","Muziek tijdens het spelen","Musique en jeu","Musik im Spiel","Musica in partita","게임 음악","Música durante o jogo","Música durante el juego"},game.InGameMusicEnabled);
        void DrawSettingsOption(Rect rect,string label,System.Action action)
        {
            if(Button(rect,Main(1)))action();
            var style=new GUIStyle(Main(1));
            while(style.fontSize>18&&style.CalcSize(new GUIContent(label)).x>rect.width-36)style.fontSize--;
            Outline(rect,label,style);
        }
    }
}
