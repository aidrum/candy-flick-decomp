using CandyFlick.Original26;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        bool photoSkinPicker,photoTapPressed;
        Vector2 photoTapStart;
        float photoTapTime;
        PhotoOmNom photoTapTarget;
        public bool PhotoSkinPicker=>photoSkinPicker;
        bool PhotoPlacementPrompt=>game.IsPhotoMode&&game.PhotoPlacing;
        public void ShowPhotoSupportWarning(string reason)=>ShowPopupMessage(reason);
        GUIStyle photoMenuButton;
        GUIStyle PhotoMenuButtonStyle
        {
            get
            {
                if(photoMenuButton==null)
                {
                    var normal=Resources.Load<Texture2D>("PhotoModeMenu/photo_mode");
                    var pressed=Resources.Load<Texture2D>("PhotoModeMenu/photo_mode_pressed");
                    photoMenuButton=new GUIStyle{normal={background=normal},hover={background=normal},active={background=pressed}};
                }
                return photoMenuButton;
            }
        }
        public Rect PhotoCameraRect=>R("GamePlayScreen$$GetRestartRectGP",true);
        Rect PhotoActionRect(int row)
        {
            Texture image=Main(1).normal.background;float w=Mathf.Min(430,CanvasWidth-64),h=w*image.height/image.width;
            float step=Mathf.Min(h+18,(height-200)/4);
            return new Rect((CanvasWidth-w)/2,height/2-2*step+row*step,w,Mathf.Min(h,step-10));
        }
        public void OpenPhotoMode()
        {
            Init();game.GroundSupport.Refresh();
            if(!game.GroundSupport.Supported)
            {ShowPopupMessage("Photo mode requires Ground AR support. "+game.GroundSupport.Reason);return;}
            transition.Begin(()=>{photoSkinPicker=false;if(!game.StartPhotoMode())ShowPopupMessage("Photo mode requires Ground AR support. "+game.GroundSupport.Reason);});
        }
        public void OpenPhotoSkins()
        {
            if(!game.IsPhotoMode)return;
            InitSkinMenu();game.DeselectPhotoActor();
            transition.Begin(()=>{photoSkinPicker=true;skinsFromLan=false;candySkinTab=false;skinScroll=Vector2.zero;skinPointerDown=skinDragging=false;game.Show(GameScreen.Skins);});
        }
        public void ChoosePhotoSkin(NommieSkin skin)
        {
            transition.Begin(()=>{photoSkinPicker=false;game.BeginPhotoPlacement(skin);game.Resume();});
        }
        void UpdatePhotoHighlight()
        {
            if(!game.IsPhotoMode)return;
            foreach(var nom in game.PhotoActors)
                nom.Highlight(!hideGui&&!game.PhotoCapturing&&game.ScreenState==GameScreen.Playing&&nom.Tracked&&
                    (game.PhotoSelection==nom||game.PhotoTarget==nom));
        }
        void DrawPhotoGame()
        {
            if(hideGui)return;
            Matrix4x4 canvas=GUI.matrix;
            GUI.matrix=canvas*Matrix4x4.Translate(new Vector3(GameplayExtraWidth/2,0,0));
            searchSticker.Draw(640,height);GUI.matrix=canvas;
            if(PhotoPlacementPrompt||!game.ar.Tracking)DrawGroundSearch();
            else if(!game.PhotoSelection)
            {
                Color saved=GUI.color;
                GUI.color=game.PhotoTarget?new Color(1,1,1,saved.a):new Color(.35f,.35f,.35f,saved.a);
                GUI.DrawTexture(new Rect(CanvasWidth/2-18,height/2-2,36,4),Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(CanvasWidth/2-2,height/2-18,4,36),Texture2D.whiteTexture);
                GUI.color=saved;
            }
            if(game.ScreenState!=GameScreen.Playing)return;
            if(game.PhotoSelection)
            {
                DrawSettingsOption(PhotoActionRect(0),"Move",game.MovePhotoSelection);
                DrawSettingsOption(PhotoActionRect(1),"Face me",game.FacePhotoSelection);
                DrawSettingsOption(PhotoActionRect(2),"Delete",game.DeletePhotoSelection);
                DrawSettingsOption(PhotoActionRect(3),"Cancel",game.DeselectPhotoActor);
            }
            if(!game.PhotoPlacing&&!game.PhotoSelection&&!CaptureManager26.IsScreenShotSaving&&Button(PhotoCameraRect,Game(30)))PhotoTapped();
            if(Button(R("GamePlayScreen$$GetPauseRect",true),Game(11))){game.DeselectPhotoActor();game.Pause();}
            if(!hideGui){HandleGroundPlacementTap();HandlePhotoSelectionTap();}
        }
        void HandlePhotoSelectionTap()
        {
            Event e=Event.current;
            bool available=game.ScreenState==GameScreen.Playing&&!game.PhotoPlacing&&!game.PhotoSelection&&!hideGui&&!PopupActive&&!transition.Waiting&&game.ar.Tracking&&Input.touchCount<=1;
            if(e.type==EventType.MouseDown&&e.button==0)
            {
                Vector3 pixel=GUI.matrix.MultiplyPoint3x4(e.mousePosition);pixel.y=Screen.height-pixel.y;
                photoTapPressed=available&&game.PhotoTarget&&!game.PhotoCandyAt(pixel);
                photoTapTarget=game.PhotoTarget;photoTapStart=e.mousePosition;photoTapTime=Time.unscaledTime;
            }
            if(e.type==EventType.MouseDrag&&photoTapPressed&&(e.mousePosition-photoTapStart).sqrMagnitude>144)photoTapPressed=false;
            if(e.rawType==EventType.MouseUp&&e.button==0)
            {
                if(photoTapPressed&&available&&e.type!=EventType.Used&&photoTapTarget==game.PhotoTarget&&Time.unscaledTime-photoTapTime<.25f)
                {game.SelectPhotoTarget();e.Use();}
                photoTapPressed=false;photoTapTarget=null;
            }
            if(!available)photoTapPressed=false;
        }
        void DrawPhotoPause()
        {
            GUI.color=new Color(0,0,0,.5f);GUI.DrawTexture(fullScreen,Texture2D.whiteTexture);GUI.color=Color.white;
            DrawSettingsOption(PhotoActionRect(1),"Add Om Nom",OpenPhotoSkins);
            DrawSettingsOption(PhotoActionRect(2),"Delete all Om Noms",game.DeleteAllPhotoActors);
            if(game.PhotoPlacing)DrawSettingsOption(PhotoActionRect(3),"Cancel placement",()=>{game.CancelPhotoPlacement();game.Resume();});
            if(Button(R("GamePlayScreen$$GetHelpRect",true),Game(12)))
                ShowPopupMessage("Place Om Noms on a flat surface. Feed and pet them, or aim at an Om Nom and tap briefly to move, face or delete him. Take a photo with the camera button.");
            if(Button(R("GamePlayScreen$$GetSoundRect",true),Game(game.SoundEnabled?14:18)))game.ToggleSound();
            if(Button(R("GamePlayScreen$$GetMenuRect",true),Game(20)))transition.Begin(game.ReturnToMenu);
            if(Button(R("GamePlayScreen$$GetPauseRect",true),Game(21)))game.Resume();
        }
    }
}
