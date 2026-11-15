using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        Vector2 lanScreenDpi;int lanDpiWidth,lanDpiHeight;
        Vector2 LanScreenDpi
        {
            get
            {
                if(lanDpiWidth==Screen.width&&lanDpiHeight==Screen.height)return lanScreenDpi;
                lanDpiWidth=Screen.width;lanDpiHeight=Screen.height;
#if UNITY_ANDROID && !UNITY_EDITOR
                try
                {
                    using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
                    using(var manager=activity.Call<AndroidJavaObject>("getWindowManager"))
                    using(var display=manager.Call<AndroidJavaObject>("getDefaultDisplay"))
                    using(var metrics=new AndroidJavaObject("android.util.DisplayMetrics"))
                    {
                        display.Call("getRealMetrics",metrics);
                        lanScreenDpi=new Vector2(metrics.Get<float>("xdpi"),metrics.Get<float>("ydpi"));
                    }
                }
                catch(System.Exception){lanScreenDpi=Vector2.zero;}
#else
                lanScreenDpi=Vector2.one*Screen.dpi;
#endif
                return lanScreenDpi;
            }
        }
        public Texture2D LanCalibrationImage=>Texture("all/marker");
        public Rect LanCalibrationImageRect
        {
            get
            {
                float aspect=55.157593f/55f;
                Vector2 dpi=LanScreenDpi;if(dpi.x>=50&&dpi.y>=50)aspect*=dpi.x/dpi.y;
                float width=Mathf.Min(500,Mathf.Min(CanvasWidth-80,(height-390)*aspect));
                return new Rect((CanvasWidth-width)/2,(height-width/aspect)/2,width,width/aspect);
            }
        }
        public static float PhysicalMarkerWidth(float width,float scale,float dpi)
        {
            if(!float.IsFinite(dpi)||dpi<50||dpi>1500)return 0;
            float metres=width*scale/dpi*.0254f;return metres>=.025f&&metres<=.3f?metres:0;
        }
        public float LanCalibrationWidthMetres=>PhysicalMarkerWidth(LanCalibrationImageRect.width,CanvasScale(Screen.safeArea),LanScreenDpi.x);
        public Vector2 LanCalibrationScreenOffsetMetres
        {
            get
            {
                Vector2 dpi=LanScreenDpi;
                if(dpi.x<50||dpi.y<50)return Vector2.zero;
                float scale=CanvasScale(Screen.safeArea);
                Vector2 origin=CanvasOrigin(Screen.safeArea,Screen.height,game.ScreenState);
                Vector2 centre=origin+LanCalibrationImageRect.center*scale;
                return new Vector2((centre.x-Screen.width/2)/dpi.x,(Screen.height/2-centre.y)/dpi.y)*.0254f;
            }
        }
        bool LanClientCalibrating=>game.IsLanMatch&&game.LAN.GroundMode&&!game.LAN.IsHost&&!game.LAN.LocalCalibrated&&!game.LAN.Ground.placed;
        bool GroundSearchArtwork=>(game.IsLanMatch?game.LAN.GroundMode:game.ar.GroundMode)&&!LanClientCalibrating;
        public bool LanScanRectangleShown=>searchFocus&&searchFocus.gameObject.activeSelf;
        void DrawLanHostCalibration()
        {
            Background(Texture(DevicePath("settings")));
            Rect image=LanCalibrationImageRect;
            GUI.Label(new Rect(CanvasWidth/2-290,image.y-130,580,112),"All other players must scan this image.\nHold your phone still.",LanText(34));
            GUI.DrawTexture(image,LanCalibrationImage,ScaleMode.StretchToFill);
            var lan=game.LAN;int total=lan.Players.Length-1,calibrated=0;
            foreach(var player in lan.Players)if(player.id!=0&&player.calibrated)calibrated++;
            string detail=lan.CalibrationNotice;
            if(string.IsNullOrEmpty(detail)&&!lan.Ground.originReady)detail=game.ar.Status;
            string footer=calibrated+" / "+total+" players calibrated"+(!string.IsNullOrEmpty(detail)?"\n"+detail:"");
            var footerStyle=LanText(30);float footerHeight=Mathf.Min(140,height-100-image.yMax-12);
            while(footerStyle.fontSize>20&&footerStyle.CalcHeight(new GUIContent(footer),580)>footerHeight)footerStyle.fontSize--;
            GUI.Label(new Rect(CanvasWidth/2-290,image.yMax+12,580,footerHeight),footer,footerStyle);
            if(game.ScreenState!=GameScreen.Paused&&Button(R("GamePlayScreen$$GetPauseRect",true),Game(11)))game.Pause();
        }
        void DrawLanClientCalibration()
        {
            Rect label=R("SearchTargetScreen$$GetLabelRect",true),button=R("SearchTargetScreen$$GetImageButtonRect",true);
            label.x-=40;label.width+=80;label.height=Mathf.Min(height-16,button.yMax-16)-label.y;
            string message="Scan the image on the host's screen to align your phone with the shared play area.";
            string detail=game.ar.Status;
            if(!string.IsNullOrEmpty(detail)&&!detail.StartsWith("Scan the image on the host's screen"))message+="\n\n"+detail;
            var style=LanText(28);while(style.fontSize>20&&style.CalcHeight(new GUIContent(message),label.width)>label.height)style.fontSize--;
            GUI.Label(label,message,style);
        }
    }
}
