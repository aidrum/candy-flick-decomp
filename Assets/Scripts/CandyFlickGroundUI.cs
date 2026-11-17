using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        bool groundTapPressed;
        Vector2 groundTapStart;
        public Rect GroundRepositionRect
        {
            get
            {
                Texture2D button=Main(1).normal.background;
                float w=Dimension("SettingScreen$$GetMainBtnWidth"), h=w*button.height/button.width;
                return new Rect((CanvasWidth-w)/2,(height-h)/2,w,h);
            }
        }
        public bool CanTapGroundPlacement(Vector2 point) =>
            game.ScreenState==GameScreen.Playing && game.IsAR && game.ar.GroundMode &&
            (!game.IsLanMatch||game.LAN.IsHost) &&
            (game.IsPhotoMode ? PhotoPlacementPrompt : !game.ar.GroundPlaced && !game.ar.Tracking) && !PopupActive && !hideGui && !transition.Waiting &&
            Input.touchCount<=1 && fullScreen.Contains(point) && !R("GamePlayScreen$$GetPauseRect",true).Contains(point);
        void HandleGroundPlacementTap()
        {
            // Run after buttons have received this event so a UI gesture never places.
            Event current=Event.current;
            if(current.type==EventType.MouseDown && current.button==0)
            {
                groundTapPressed=CanTapGroundPlacement(current.mousePosition);
                groundTapStart=current.mousePosition;
                if(groundTapPressed) current.Use();
            }
            else if(current.type==EventType.MouseDrag && groundTapPressed)
            {
                if((current.mousePosition-groundTapStart).sqrMagnitude>144) groundTapPressed=false;
                current.Use();
            }
            else if(current.rawType==EventType.MouseUp && current.button==0)
            {
                bool place=groundTapPressed && current.type!=EventType.Used && CanTapGroundPlacement(current.mousePosition);
                groundTapPressed=false;
                if(place) { game.ar.PlaceGround(); current.Use(); }
            }
            if(game.ScreenState!=GameScreen.Playing || !game.IsAR || (game.IsPhotoMode?!PhotoPlacementPrompt:game.ar.Tracking) || PopupActive || Input.touchCount>1)
                groundTapPressed=false;
        }
        public Rect GroundPromptRect
        {
            get
            {
                Vector3 position=searchSticker.transform.position, scale=searchSticker.transform.lossyScale;
                Rect paper=new Rect(GameplayExtraWidth/2+(position.x-scale.x/2)*640,
                    height-(position.y+scale.y/2)*height,scale.x*640,scale.y*height);
                // Follow the original yellow note's animation and device layout.
                return new Rect(paper.x+28,paper.y+38,paper.width-56,Mathf.Min(paper.yMax,height)-paper.y-58);
            }
        }
        void DrawGroundSearch()
        {
            var label=new GUIStyle(Game(8)) { fontSize=28,wordWrap=true,alignment=TextAnchor.MiddleCenter,
                normal={background=null,textColor=Color.black},hover={background=null,textColor=Color.black},active={background=null,textColor=Color.black},focused={background=null,textColor=Color.black} };
            Rect prompt=GroundPromptRect;string message=L(game.ar.Status);
            while(label.fontSize>20 && label.CalcHeight(new GUIContent(message),prompt.width)>prompt.height) label.fontSize--;
            Color saved=GUI.color;
            GUI.color=new Color(saved.r,saved.g,saved.b,saved.a*Mathf.Clamp01(searchSticker.m_Color.a*2));
            GUI.Label(prompt,message,label);
            GUI.color=saved;
            if(game.IsPhotoMode?!PhotoPlacementPrompt:game.ar.GroundPlaced) return; // Waiting for the first native anchor observation.
            GUI.color=game.ar.GroundPointAvailable?new Color(.6f,1,.4f,1):Color.white;
            GUI.DrawTexture(new Rect(CanvasWidth/2-18,height/2-2,36,4),Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(CanvasWidth/2-2,height/2-18,4,36),Texture2D.whiteTexture);
            GUI.color=saved;
        }
    }
}
