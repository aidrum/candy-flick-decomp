using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        Vector2 skinScroll, skinDragStart, skinScrollStart;
        bool skinPointerDown, skinDragging,candySkinTab,skinsFromLan;
        Texture2D candyPortraits;
        GUIStyle skinsMenuButton, skinCard, selectedSkinCard, skinTitle, randomSkinLabel;
        Texture2D skinsBackground, skinPortraits;
        const float CardWidth=176, CardHeight=230, SkinRow=250;
        public const int SkinColumns=3;
        public static float SkinGridHeight=>Mathf.Ceil((TimeTravelSkins.Names.Length+1)/(float)SkinColumns)*SkinRow-20;
        public string SettingsPage => settingsPage;
        public Vector2 SkinScroll => skinScroll;
        public Rect SkinViewport => new Rect(28,155,584,height-330);
        Rect MoreOptionRect(int index)
        {
            float w=Dimension("SettingScreen$$GetMainBtnWidth");
            float top=height*.08f, bottom=R("SettingScreen$$GetBackBtnRect",true).y-86;
            float step=Mathf.Min(145,(bottom-top)/5);
            float h=Mathf.Min(Dimension("SettingScreen$$GetMainBtnHeight"),step-14);
            return new Rect(320-w/2,top+index*step,w,h);
        }
        public Rect MoreControlsRect=>MoreOptionRect(0);
        public Rect MoreFpsRect=>MoreOptionRect(1);
        public Rect MoreHideCandyRect=>MoreOptionRect(2);
        public Rect MoreMusicRect=>MoreOptionRect(3);
        public Rect MoreARModeRect=>MoreOptionRect(4);
        public string ARModeLabel=>L("AR Mode: {0}",L(game.GroundARModeEnabled?"Ground":"Image"));
        public Rect MenuSkinsRect=>MenuLowerButtonRect(MenuDownloadRect,false);
        GUIStyle SkinsMenuButtonStyle { get { InitSkinMenu(); return skinsMenuButton; } }
        void InitSkinMenu()
        {
            if(skinsMenuButton!=null)return;
            Texture2D Load(string name)=>Resources.Load<Texture2D>("SkinMenu/"+name);
            candyPortraits=Resources.Load<Texture2D>("CandySkins/thumbnails");
            skinsBackground=Load("skins_bg"); skinPortraits=Load("nommies");
            skinsMenuButton=new GUIStyle { normal={background=Load("skins_btn")}, hover={background=Load("skins_btn")}, active={background=Load("skins_btn_pressed")} };
            skinCard=new GUIStyle {normal={background=Load("skin_btn_bg")}};
            selectedSkinCard=new GUIStyle {normal={background=Load("skin_btn_bg_selected")}};
            skinTitle=new GUIStyle {font=PortFont,fontSize=76,alignment=TextAnchor.MiddleCenter,normal={textColor=Color.white}};
            randomSkinLabel=new GUIStyle(skinTitle){fontSize=46};
        }
        public void OpenSkins()
        {
            if(game.IsLanMatch)return;
            InitSkinMenu();bool fromLan=game.ScreenState==GameScreen.LanLobby;skinsFromLan=fromLan;
            transition.Begin(()=>
            {
                // A host may start while a client is opening this screen.
                if(fromLan&&game.IsLanMatch)return;
                settingsPage=null;candySkinTab=false;skinScroll=Vector2.zero;skinPointerDown=skinDragging=false;game.Show(GameScreen.Skins);
            });
        }
        void CloseSkins()
        {
            bool returnToLan=skinsFromLan;
            transition.Begin(()=>
            {
                if(returnToLan){if(!game.IsLanMatch)game.Show(GameScreen.LanLobby);}
                else game.ReturnToMenu();
            });
        }
        public static Rect SkinCardRect(int index)=>SkinCardRect(index,TimeTravelSkins.Names.Length+1);
        public static Rect SkinCardRect(int index,int count)
        {
            int row=index/SkinColumns,items=Mathf.Min(SkinColumns,count-row*SkinColumns);
            float inset=(SkinColumns-items)*188f/2;
            return new Rect(4+inset+188*(index%SkinColumns),SkinRow*row,CardWidth,CardHeight);
        }
        public static Rect SkinPortraitUv(int index)=>new Rect((index%4)/4f,1-(index/4+1)/2f,1/4f,1/2f);
        public Rect OmNomSkinTabRect=>SkinTabRect(58);
        public Rect CandySkinTabRect=>SkinTabRect(334);
        Rect SkinTabRect(float x)
        {
            Texture texture=Main(1).normal.background;float h=248f*texture.height/texture.width;
            return new Rect(x,25+(105-h)/2,248,h);
        }
        void DrawSkinTab(Rect rect,string text,bool selected)
        {
            string label=L(text);
            var style=new GUIStyle(Main(1)){font=PortFont,fontSize=48,alignment=TextAnchor.MiddleCenter,fixedWidth=0,fixedHeight=0,padding=new RectOffset(),contentOffset=new Vector2(0,-6),overflow=new RectOffset(),normal={textColor=Color.white},hover={textColor=Color.white},active={textColor=Color.white}};
            while(style.fontSize>28&&style.CalcSize(new GUIContent(label)).x>rect.width-24)style.fontSize--;
            if(selected)style.normal.background=style.active.background;
            if(Button(rect,style)){candySkinTab=text=="Candy";skinScroll=Vector2.zero;skinPointerDown=skinDragging=false;}
            Outline(rect,label,style);
        }
        GUIStyle RandomSkinTextStyle(Rect rect)
        {
            InitSkinMenu();var style=new GUIStyle(randomSkinLabel){font=PortFont};string label=L("Random");
            while(style.fontSize>20&&style.CalcSize(new GUIContent(label)).x>rect.width-18)style.fontSize--;
            return style;
        }
        void DrawRandomSkinLabel(Rect rect)=>Outline(rect,L("Random"),RandomSkinTextStyle(rect));
        void DrawCandySkinCards()
        {
            for(int i=0;i<CandySkinSettings.Count;i++)
            {
                Rect card=SkinCardRect(i,CandySkinSettings.Count+1);
                if(Button(card,game.CandySkins.Selected==i?selectedSkinCard:skinCard))game.CandySkins.Select(i);
                if(candyPortraits)GUI.DrawTextureWithTexCoords(new Rect(card.center.x-75,card.y+12,150,150),candyPortraits,new Rect(i/4f,.5f,.25f,.5f),true);
            }
            Rect random=SkinCardRect(CandySkinSettings.RandomChoice,CandySkinSettings.Count+1);
            if(Button(random,game.CandySkins.Selected==CandySkinSettings.RandomChoice?selectedSkinCard:skinCard))game.CandySkins.Select(CandySkinSettings.RandomChoice);
            DrawRandomSkinLabel(new Rect(random.x,random.y+12,random.width,150));
        }
        void DrawSkins()
        {
            InitSkinMenu(); skinTitle.font=randomSkinLabel.font=PortFont; Background(skinsBackground);
            DrawSkinTab(OmNomSkinTabRect,"Om Nom",!candySkinTab);DrawSkinTab(CandySkinTabRect,"Candy",candySkinTab);
            Rect viewport=SkinViewport;
            float contentHeight=candySkinTab?SkinRow*2-20:SkinGridHeight;
            HandleSkinDrag(viewport,contentHeight);
            skinScroll=GUI.BeginScrollView(viewport,skinScroll,new Rect(0,0,560,contentHeight),false,false);
            if(candySkinTab)DrawCandySkinCards();else
            {
            for(int i=0;i<TimeTravelSkins.Names.Length;i++)
            {
                Rect card=SkinCardRect(i);
                if(Button(card,!game.Skins.RandomSelected&&(int)game.Skins.Selected==i?selectedSkinCard:skinCard))game.Skins.Select((NommieSkin)i);
                // Keep the portrait above the selected card's checkmark.
                if(skinPortraits)GUI.DrawTextureWithTexCoords(new Rect(card.center.x-75,card.y+12,150,150),skinPortraits,SkinPortraitUv(i),true);
            }
            Rect randomCard=SkinCardRect(TimeTravelSkins.RandomChoice);
            if(Button(randomCard,game.Skins.RandomSelected?selectedSkinCard:skinCard))game.Skins.SelectRandom();
            DrawRandomSkinLabel(new Rect(randomCard.x,randomCard.y+12,randomCard.width,150));
            }
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
            DrawSettingsOption(MoreARModeRect,ARModeLabel,game.ToggleGroundARMode,game.GroundARModeEnabled||game.GroundSupport.Supported);
            if(!game.GroundSupport.Supported)
            {
                var hint=new GUIStyle{font=PortFont,fontSize=22,alignment=TextAnchor.UpperCenter,wordWrap=true,normal={textColor=Color.black}};
                GUI.Label(new Rect(MoreARModeRect.x,MoreARModeRect.yMax+6,MoreARModeRect.width,64),L(game.GroundSupport.Reason),hint);
            }
        }
        string OptionLabel(string[] labels,bool enabled)
        {
            string[] on={"On","Вкл","Aan","Oui","An","Sì","켜짐","Sim","Sí"};
            string[] off={"Off","Выкл","Uit","Non","Aus","No","꺼짐","Não","No"};
            return labels[text.CurrentLanguage]+": "+(enabled?on:off)[text.CurrentLanguage];
        }
        string HideCandyLabel=>OptionLabel(new[]{"Hide candy in pictures","Скрывать конфету на фото","Snoep verbergen op foto's","Masquer le bonbon en photo","Bonbon auf Fotos ausblenden","Nascondi caramella nelle foto","사진에서 사탕 숨기기","Ocultar doce nas fotos","Ocultar caramelo en fotos"},game.HideCandyInPictures);
        string InGameMusicLabel=>OptionLabel(new[]{"In-game music","Музыка в игре","Muziek tijdens het spelen","Musique en jeu","Musik im Spiel","Musica in partita","게임 음악","Música durante o jogo","Música durante el juego"},game.InGameMusicEnabled);
        void DrawSettingsOption(Rect rect,string label,System.Action action,bool enabled=true)
        {
            label=L(label);
            bool previous=GUI.enabled;GUI.enabled=previous&&enabled;
            if(Button(rect,Main(1)))action();
            var style=new GUIStyle(Main(1));
            while(style.fontSize>18&&style.CalcSize(new GUIContent(label)).x>rect.width-36)style.fontSize--;
            Outline(rect,label,style);GUI.enabled=previous;
        }
    }
}
