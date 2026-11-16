using System;
using System.Linq;
using CandyFlick.Multiplayer;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        string lanNameEdit,lanAddress="",lanLastSaved;Vector2 lanRoomsScroll;
        Texture2D lanInputBackground,lanScoreBackground;GUIStyle lanScoreStyle;
        Vector2 lanPlayersScroll,lanResultsScroll;LanListDrag lanPlayersDrag,lanResultsDrag;
        struct LanListDrag {public bool down,dragging;public Vector2 start,scroll;}
        static Texture2D RoundedMask(string name,float radius)
        {
            const int size=32;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=name,hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float qx=Mathf.Abs(x+.5f-size/2)-(size/2-radius),qy=Mathf.Abs(y+.5f-size/2)-(size/2-radius);
                float distance=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(.5f-distance));
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        GUIStyle LanScoreStyle
        {
            get
            {
                if(lanScoreStyle==null){lanScoreBackground=RoundedMask("LAN score background",6);lanScoreStyle=new GUIStyle{border=new RectOffset(8,8,8,8),normal={background=lanScoreBackground}};}
                return lanScoreStyle;
            }
        }
        void HandleLanListDrag(Rect view,float contentHeight,ref Vector2 scroll,ref LanListDrag drag)
        {
            Event e=Event.current;Rect area=view;area.width-=20;Vector2 point=e.mousePosition;
            if(e.type==EventType.MouseDown&&e.button==0&&area.Contains(point)){drag.down=true;drag.dragging=false;drag.start=point;drag.scroll=scroll;}
            if(e.type==EventType.MouseDrag&&drag.down)
            {
                if((point-drag.start).sqrMagnitude>64)drag.dragging=true;
                if(drag.dragging){GUIUtility.hotControl=0;scroll.y=Mathf.Clamp(drag.scroll.y+drag.start.y-point.y,0,Mathf.Max(0,contentHeight-view.height));e.Use();}
            }
            if(e.rawType==EventType.MouseUp&&drag.down){drag.down=false;if(drag.dragging){GUIUtility.hotControl=0;e.Use();}drag.dragging=false;}
        }
        Texture2D LanInputBackground
        {
            get
            {
                if(lanInputBackground)return lanInputBackground;
                const int size=32;const float radius=10;
                lanInputBackground=new Texture2D(size,size,TextureFormat.RGBA32,false){name="Rounded LAN input",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                var pixels=new Color[size*size];
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    float qx=Mathf.Abs(x+.5f-size/2)-(size/2-radius),qy=Mathf.Abs(y+.5f-size/2)-(size/2-radius);
                    float distance=new Vector2(Mathf.Max(qx,0),Mathf.Max(qy,0)).magnitude+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
                    pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(.5f-distance));
                }
                lanInputBackground.SetPixels(pixels);lanInputBackground.Apply();return lanInputBackground;
            }
        }
        GUIStyle lanMenuButton;
        GUIStyle LanMenuButtonStyle
        {
            get
            {
                if(lanMenuButton==null)
                {
                    var normal=Resources.Load<Texture2D>("LanMenu/pvp_button");
                    lanMenuButton=new GUIStyle{normal={background=normal},hover={background=normal},active={background=Resources.Load<Texture2D>("LanMenu/pvp_button_pressed")}};
                }
                return lanMenuButton;
            }
        }
        float MenuButtonsOffsetY
        {
            get
            {
                float rowBottom=Mathf.Max(R("MainScreen$$GetDwnldRect").yMax,R("MainScreen$$GetSettingRect").yMax);
                float lowerHeight=R("MainScreen$$GetShareRect").height;
                return Mathf.Min(0,height-16-(rowBottom+8+lowerHeight));
            }
        }
        Rect MenuExistingButtonRect(string method)
        {
            Rect rect=R(method);rect.y+=MenuButtonsOffsetY;return rect;
        }
        public Rect MenuSoloPlayRect=>MenuExistingButtonRect("MainScreen$$GetPlayRect");
        public Rect MenuDownloadRect=>MenuExistingButtonRect("MainScreen$$GetDwnldRect");
        public Rect MenuSettingsRect=>MenuExistingButtonRect("MainScreen$$GetSettingRect");
        Rect MenuLowerButtonRect(Rect above,bool lan)
        {
            Rect rect=R("MainScreen$$GetShareRect");
            if(lan){var image=LanMenuButtonStyle.normal.background;rect.height=rect.width*image.height/image.width;}
            rect.x=above.center.x-rect.width/2;
            rect.y=Mathf.Max(MenuDownloadRect.yMax,MenuSettingsRect.yMax)+8;return rect;
        }
        public Rect MenuMultiplayerRect=>MenuLowerButtonRect(MenuSettingsRect,true);
        public Rect LanSkinsButtonRect
        {
            get
            {
                Rect back=R("SettingScreen$$GetBackBtnRect",true);
                float space=2*Mathf.Min(back.center.y-LanBoardRect.yMax-12,height-back.center.y-12);
                float size=Mathf.Min(MenuSkinsRect.width,Mathf.Max(1,space));
                return new Rect(640-back.center.x-size/2,back.center.y-size/2,size,size);
            }
        }
        public Rect LanNameRect=>new Rect(245,95,315,55);
        public Rect LanBoardRect
        {
            get{Texture2D image=Texture(DevicePath("board"));float h=Mathf.Min(height-320,850),w=Mathf.Min(560,h*image.width/image.height);h=w*image.height/image.width;return new Rect(320-w/2,175,w,h);}
        }
        GUIStyle LanText(int size,Color? color=null,TextAnchor anchor=TextAnchor.MiddleCenter)=>new GUIStyle{font=PortFont,fontSize=size,wordWrap=true,alignment=anchor,richText=false,normal={textColor=color??Color.black}};
        Rect LanButtonRect(float y,float width=300){Texture image=Main(1).normal.background;return new Rect(320-width/2,y,width,width*image.height/image.width);}
        public Rect LanControlsRect
        {
            get{Rect start=LanButtonRect(LanBoardRect.yMax-95,Mathf.Min(300,LanBoardRect.width-56));Rect controls=LanButtonRect(0,Mathf.Min(300,LanBoardRect.width-56));controls.y=start.y-controls.height-12;return controls;}
        }
        public string LanControlsLabel=>L("Controls: {0}",L(game.LAN.Controls==LanControls.Slingshot?"Slingshot":game.LAN.Controls==LanControls.Flick?"Flick":"Player's choice"));
        bool LanButton(Rect rect,string label,bool enabled=true)
        {
            label=L(label);
            bool previous=GUI.enabled;GUI.enabled=previous&&enabled;bool tapped=Button(rect,Main(1));
            var style=new GUIStyle(Main(1)){fontSize=32};while(style.fontSize>18&&style.CalcSize(new GUIContent(label)).x>rect.width-20)style.fontSize--;
            Outline(rect,label,style);GUI.enabled=previous;return tapped;
        }
        Rect LanJoinRect(float x,float y,float width)
        {
            Texture image=gameSkin.GetStyle("short_btn").normal.background;
            return new Rect(x,y,width,width*image.height/image.width);
        }
        bool LanJoinButton(Rect rect,string label,bool enabled=true)
        {
            label=L(label);
            var style=new GUIStyle(gameSkin.GetStyle("short_btn")){font=PortFont,fontSize=30,contentOffset=Vector2.zero,
                normal={textColor=Color.white},hover={textColor=Color.white},active={textColor=Color.white}};
            while(style.fontSize>16&&style.CalcSize(new GUIContent(label)).x>rect.width-16)style.fontSize--;
            bool previous=GUI.enabled;GUI.enabled=previous&&enabled;bool tapped=Button(rect,style);
            Outline(rect,label,style);GUI.enabled=previous;return tapped;
        }
        void OpenLanMenu()
        {
            lanNameEdit=lanLastSaved=game.LAN.PlayerName;lanRoomsScroll=Vector2.zero;lanProfilePicker=false;
            transition.Begin(()=>game.LAN.OpenBrowser());
        }
        void CommitLanName(){game.LAN.SetName(lanNameEdit);lanNameEdit=lanLastSaved=game.LAN.PlayerName;GUI.FocusControl(null);}
        void DrawLanLobby()
        {
            bool enabled=GUI.enabled;GUI.enabled=enabled&&!lanProfilePicker;
            Background(Texture(DevicePath("settings")));Rect board=LanBoardRect;
            Outline(new Rect(0,18,640,64),L("LAN Multiplayer"),LanText(47,Color.white));
            Rect avatar=LanProfileButtonRect;
            DrawLanProfile(avatar,game.LAN.ProfilePicture);
            if(Button(avatar,GUIStyle.none))OpenLanProfilePicker();
            LanLabel(new Rect(145,95,90,55),"Name:",LanText(32));
            if(lanNameEdit==null)lanNameEdit=lanLastSaved=game.LAN.PlayerName;
            var field=new GUIStyle(GUI.skin.textField){font=PortFont,fontSize=30,alignment=TextAnchor.MiddleLeft,richText=false,padding=new RectOffset(12,10,6,6),border=new RectOffset(12,12,12,12),
                normal={background=LanInputBackground,textColor=Color.black},hover={background=LanInputBackground,textColor=Color.black},
                active={background=LanInputBackground,textColor=Color.black},focused={background=LanInputBackground,textColor=Color.black}};
            GUI.SetNextControlName("LAN name");lanNameEdit=GUI.TextField(LanNameRect,lanNameEdit,12,field);
            if(lanNameEdit!=lanLastSaved)
            {game.LAN.SetName(lanNameEdit);lanLastSaved=lanNameEdit;if(GUI.GetNameOfFocusedControl()!="LAN name")lanNameEdit=lanLastSaved=game.LAN.PlayerName;}
            if(GUI.GetNameOfFocusedControl()!="LAN name"&&string.IsNullOrWhiteSpace(lanNameEdit))lanNameEdit=lanLastSaved=game.LAN.PlayerName;
            if(Event.current.type==EventType.KeyDown&&(Event.current.keyCode==KeyCode.Return||Event.current.keyCode==KeyCode.KeypadEnter))CommitLanName();
            GUI.DrawTexture(board,Texture(DevicePath("board")));
            float left=board.x+28,width=board.width-56,top=board.y+64;
            LanLabel(new Rect(left,top,width,55),game.LAN.InRoom?"Room · "+game.LAN.Players.Length+"/"+LanRules.MaxPlayers:"Nearby rooms",LanText(38));
            if(game.LAN.InRoom)
            {
                Rect start=LanButtonRect(board.yMax-95,Mathf.Min(300,width));
                Rect controls=LanControlsRect;
                Rect mode=LanButtonRect(0,Mathf.Min(240,width));mode.y=controls.y-mode.height-12;
                Rect address=new Rect(left,mode.y-50,width,40);
                string roomNotice=game.LAN.GroundWarning;
                if(string.IsNullOrEmpty(roomNotice)&&game.LAN.IsHost)
                {
                    string note=game.LAN.Notice;
                    if(note!="Players can join until the host starts."&&note!="Waiting for players..."&&note!="Waiting for the host.")roomNotice=note;
                }
                Rect noticeRect=new Rect(left,address.y-60,width,52);
                float rosterBottom=string.IsNullOrEmpty(roomNotice)?address.y-12:noticeRect.y-12;
                var players=game.LAN.Players;float row=Mathf.Min(66,(board.height-440)/4);if(players.Length>4)row=Mathf.Max(40,row);
                Rect roster=new Rect(left,top+65,width,Mathf.Max(32,rosterBottom-(top+65)));float contentHeight=players.Length*row;
                HandleLanListDrag(roster,contentHeight,ref lanPlayersScroll,ref lanPlayersDrag);
                float contentWidth=width-(contentHeight>roster.height?20:0);
                lanPlayersScroll=GUI.BeginScrollView(roster,lanPlayersScroll,new Rect(0,0,contentWidth,contentHeight));
                for(int i=0;i<players.Length;i++)
                {
                    string name=players[i].name+(players[i].id==0?" "+L("(Host)"):"");
                    DrawLanIdentity(new Rect(0,i*row,contentWidth,row),name,players[i].avatar,Mathf.Min(32,Mathf.RoundToInt(row*.85f)),Color.black);
                }
                GUI.EndScrollView();
                string modeLabel=L("AR Mode: {0}",L(game.LAN.GroundMode?"Ground":"Image"));
                if(game.LAN.IsHost){if(LanButton(mode,modeLabel,game.LAN.CanChooseGround))game.LAN.ToggleRoomGround();}
                else LanLabel(mode,modeLabel,LanText(28));
                if(game.LAN.IsHost){if(LanButton(controls,LanControlsLabel))game.LAN.CycleRoomControls();}
                else LanLabel(controls,LanControlsLabel,LanText(25));
                if(!string.IsNullOrEmpty(roomNotice))LanLabel(noticeRect,roomNotice,LanText(23));
                if(game.LAN.IsHost)LanLabel(address,"Host address: "+game.LAN.HostAddress,LanText(23));
                if(game.LAN.IsHost&&LanButton(start,"Start match",game.LAN.CanStart)) {CommitLanName();game.LAN.StartMatch();}
                if(!game.LAN.IsHost)LanLabel(LanRoomStatusRect,"Waiting for the host to start.",LanText(25));
            }
            else
            {
                LanLabel(new Rect(left,top+50,width,45),"Connect to the same Wi-Fi",LanText(23));
                Rect viewport=new Rect(left,top+100,width,Mathf.Max(70,board.height-430));
                var rooms=game.LAN.Rooms;float row=76;
                lanRoomsScroll=GUI.BeginScrollView(viewport,lanRoomsScroll,new Rect(0,0,width-20,Mathf.Max(viewport.height,rooms.Length*row)));
                if(rooms.Length==0)LanLabel(new Rect(0,0,width-20,70),game.LAN.Connecting?"Connecting...":"Looking for rooms...",LanText(28));
                for(int i=0;i<rooms.Length;i++)
                {
                    var room=rooms[i];GUI.Label(new Rect(0,i*row,width-140,70),room.name+" · "+room.count+"/"+LanRules.MaxPlayers,LanText(27,null,TextAnchor.MiddleLeft));
                    bool canJoin=room.count<LanRules.MaxPlayers&&!game.LAN.Connecting;
                    bool join=LanJoinButton(LanJoinRect(width-136,i*row+3,112),"Join",canJoin);
                    if(GUI.Button(new Rect(0,i*row,width-20,row),GUIContent.none,GUIStyle.none)&&canJoin)join=true;
                    if(join) {CommitLanName();game.LAN.JoinRoom(room.address);}
                }
                GUI.EndScrollView();
                LanLabel(new Rect(left,board.yMax-295,width,30),"Or enter the host address",LanText(22));
                lanAddress=GUI.TextField(new Rect(left,board.yMax-237,width-138,52),lanAddress,48,field);
                if(LanJoinButton(LanJoinRect(board.xMax-148,board.yMax-247,120),"Join",!game.LAN.Connecting)) {CommitLanName();game.LAN.JoinRoom(lanAddress);}
                if(LanButton(LanButtonRect(board.yMax-99,Mathf.Min(300,width)),"Host room",!game.LAN.Connecting)) {CommitLanName();game.LAN.HostRoom();}
            }
            if(!string.IsNullOrEmpty(LanFooterText))LanLabel(LanFooterRect,LanFooterText,LanText(23));
            if(Button(LanSkinsButtonRect,SkinsMenuButtonStyle)){CommitLanName();OpenSkins();}
            if(Button(R("SettingScreen$$GetBackBtnRect",true),Main(33))) {CommitLanName();transition.Begin(game.LAN.LeaveRoom);}
            GUI.enabled=enabled;if(lanProfilePicker)DrawLanProfilePicker();
        }
        public Rect LanRoomStatusRect=>new Rect(LanBoardRect.x+28,LanBoardRect.yMax-100,LanBoardRect.width-56,60);
        public Rect LanFooterRect
        {
            get
            {
                Rect back=R("SettingScreen$$GetBackBtnRect",true),skins=LanSkinsButtonRect;
                float left=back.xMax+14,right=skins.x-14,h=Mathf.Min(112,2*Mathf.Min(back.center.y-LanBoardRect.yMax-8,height-back.center.y-8));
                return new Rect(left,back.center.y-Mathf.Max(40,h)/2,Mathf.Max(1,right-left),Mathf.Max(40,h));
            }
        }
        public string LanFooterText=>game.LAN.InRoom||game.LAN.IsHost?"":L(game.LAN.Notice);
        public Rect LanDistanceScoreRect=>R("ScoresGPView$$GetPossibleScoresRect");
        public Rect LanPlayerScoreRect(int index)
        {
            Rect distance=LanDistanceScoreRect;
            int count=game.LAN.Players.Length;
            if(count>4)
            {
                int row=index/5,items=Mathf.Min(5,count-row*5);float w=(CanvasWidth-64)/5,group=items*w+(items-1)*6;
                return new Rect((CanvasWidth-group)/2+(index%5)*(w+6),distance.yMax+12+row*90,w,84);
            }
            bool left=index%2==0;
            float x=left?20:distance.xMax+12;
            float width=left?distance.x-32:CanvasWidth-20-x;
            return new Rect(x,18+(index/2)*55,width,50);
        }
        void DrawLanGame()
        {
            var lan=game.LAN;
            if(lan.ShowHostCalibration){DrawLanHostCalibration();return;}
            float remaining=lan.Remaining;
            var players=lan.Players;
            for(int i=0;i<players.Length;i++)
            {
                Rect box=LanPlayerScoreRect(i);
                Color saved=GUI.color;GUI.color=new Color(0,0,0,.35f);GUI.Box(box,GUIContent.none,LanScoreStyle);GUI.color=saved;
                Color color=players[i].id==lan.LocalId?Color.yellow:Color.white;
                if(players.Length>4)
                {
                    DrawLanProfile(new Rect(box.center.x-16,box.y+5,32,32),players[i].avatar);
                    var label=LanText(22,color);label.wordWrap=false;while(label.fontSize>14&&label.CalcSize(new GUIContent(players[i].name)).x>box.width-8)label.fontSize--;
                    Outline(new Rect(box.x+4,box.y+38,box.width-8,22),players[i].name,label);
                    Outline(new Rect(box.x+4,box.y+60,box.width-8,24),players[i].score.ToString(),LanText(26,color));
                }
                else DrawLanIdentity(new Rect(box.x+5,box.y,box.width-10,box.height),players[i].name+": "+players[i].score,players[i].avatar,28,color,true);
            }
            Rect distance=LanDistanceScoreRect;
            if(lan.LocalTracking)
            {
                GUI.DrawTexture(distance,Texture("all/new_assets/Target_Plate"));
                Outline(distance,lan.DistanceScore.ToString());
            }
            Rect timer=players.Length>4?new Rect(distance.xMax+8,distance.center.y-30,Mathf.Min(180,CanvasWidth-distance.xMax-28),60):new Rect(CanvasWidth/2-150,distance.yMax+6,300,65);
            Outline(timer,L("{0}s",Mathf.CeilToInt(remaining)),LanText(43,Color.white));
            if(!lan.Playing)
            {
                if(lan.GroundMode&&!lan.LocalTracking)
                {
                    if(LanClientCalibrating)DrawLanClientCalibration();
                    else if(!lan.IsHost)LanLabel(GroundPromptRect,game.ar.Status,LanText(26));
                    else DrawGroundSearch();
                }
                else if(!lan.LocalTracking)
                {
                    LanLabel(R("SearchTargetScreen$$GetLabelRect",true),T("SEARCH_TARGET_LABEL"),Game(8));
                }
                else LanLabel(GroundPromptRect,lan.WaitingText,LanText(28));
            }
            if(game.ScreenState!=GameScreen.Paused&&Button(R("GamePlayScreen$$GetPauseRect",true),Game(11)))game.Pause();
            HandleGroundPlacementTap();
        }
        public Rect LanPauseHomeRect
        {
            get{Rect rect=R("GamePlayScreen$$GetMenuRect",true);rect.y=R("GamePlayScreen$$GetSoundRect",true).y;return rect;}
        }
        public Rect LanPauseSoundRect
        {
            get{Rect rect=R("GamePlayScreen$$GetSoundRect",true);rect.y=R("GamePlayScreen$$GetHelpRect",true).y;return rect;}
        }
        public Rect LanGroundMoveRect
        {
            get{Texture image=Main(1).normal.background;float w=Mathf.Min(340,CanvasWidth-260);return new Rect((CanvasWidth-w)/2,height/2+25,w,w*image.height/image.width);}
        }
        public Rect LanGroundCalibrateRect
        {
            get{Rect rect=LanGroundMoveRect;rect.y=rect.yMax+24;return rect;}
        }
        void DrawLanPause()
        {
            Color saved=GUI.color;GUI.color=new Color(0,0,0,.5f);GUI.DrawTexture(fullScreen,Texture2D.whiteTexture);GUI.color=saved;
            Outline(new Rect(CanvasWidth/2-250,height/2-100,500,110),L("Match paused for everyone"),LanText(38,Color.white));
            if(game.LAN.IsHost&&game.LAN.GroundMode)
            {
                DrawSettingsOption(LanGroundMoveRect,"Move Om Nom",()=>{if(game.LAN.MoveOmNom())game.Resume();});
                DrawSettingsOption(LanGroundCalibrateRect,"Calibrate again",()=>{game.LAN.ResetGroundCalibration();game.Resume();});
            }
            if(Button(LanPauseSoundRect,Game(game.SoundEnabled?14:18)))game.ToggleSound();
            if(Button(LanPauseHomeRect,Game(20)))transition.Begin(game.LAN.LeaveRoom);
            if(Button(R("GamePlayScreen$$GetPauseRect",true),Game(21)))game.Resume();
        }
        void DrawLanResults()
        {
            Background(Texture(DevicePath("settings")));Rect board=LanBoardRect;GUI.DrawTexture(board,Texture(DevicePath("board")));
            var players=game.LAN.Players.OrderByDescending(p=>p.score).ThenBy(p=>p.id).ToArray();int[] winners=LanRules.Winners(players);
            string title=winners.Length==1?(winners[0]==game.LAN.LocalId?"You win!":players.First(p=>p.id==winners[0]).name+" wins!"):"Draw!";
            LanLabel(new Rect(board.x+20,board.y+70,board.width-40,90),title,LanText(43));
            float row=Mathf.Max(56,Mathf.Min(92,(board.height-290)/4));
            Rect list=new Rect(board.x+28,board.y+185,board.width-56,Mathf.Max(80,board.height-335));float contentHeight=players.Length*row;
            HandleLanListDrag(list,contentHeight,ref lanResultsScroll,ref lanResultsDrag);float width=list.width-(contentHeight>list.height?20:0);
            lanResultsScroll=GUI.BeginScrollView(list,lanResultsScroll,new Rect(0,0,width,contentHeight));
            for(int i=0;i<players.Length;i++)DrawLanIdentity(new Rect(0,i*row,width,row),players[i].name+"   "+L("{0} points",players[i].score),players[i].avatar,31,Color.black);
            GUI.EndScrollView();
            if(game.LAN.IsHost&&LanButton(LanButtonRect(board.yMax-110,Mathf.Min(300,board.width-60)),"Back to room"))game.LAN.ReturnToRoom();
            if(!game.LAN.IsHost)LanLabel(new Rect(board.x+25,board.yMax-130,board.width-50,90),"Waiting for the host...",LanText(28));
            if(Button(R("SettingScreen$$GetBackBtnRect",true),Main(33)))transition.Begin(game.LAN.LeaveRoom);
        }
    }
}
