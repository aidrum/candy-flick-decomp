using System;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        string artworkFamily;
        float profileUnit=1,referenceHeight=1136;
        readonly System.Collections.Generic.Dictionary<string,float> dimensions=new System.Collections.Generic.Dictionary<string,float>();
        public string ArtworkFamily=>artworkFamily;
        public float ReferenceHeight=>referenceHeight;
        public static string DeviceArtworkFamily(Rect safe)
        {
            float aspect=safe.width/safe.height;
            return aspect>=.72f?"ipad2":aspect>=.62f?"iphone4":"iphone5";
        }
        // Keep all port additions in the 640-wide coordinate system; normalize
        // recovered native rectangles/styles together before applying the GUI matrix.
        public static float CanvasScale(Rect safe)
        {
            string family=DeviceArtworkFamily(safe);
            float nativeHeight=family=="ipad2"?1024f*640/768:family=="iphone4"?960:1136;
            return Mathf.Min(safe.width/640f,safe.height/nativeHeight);
        }
        public static Vector2 CanvasOrigin(Rect safe,float screenHeight)
        {
            float scale=CanvasScale(safe);
            return new Vector2(safe.center.x-320*scale,screenHeight-safe.yMax);
        }
        void LoadDeviceLayout()
        {
            string family=DeviceArtworkFamily(Screen.safeArea);
            if(artworkFamily==family)return;
            bool replacing=artworkFamily!=null;
            if(mainSkin)Destroy(mainSkin);if(gameSkin)Destroy(gameSkin);
            artworkFamily=family;profileUnit=family=="ipad2"?640f/768:1;
            string skin=family=="ipad2"?"iPad2":family=="iphone4"?"iPhone4":"iPhone5";
            mainSkin=Instantiate(Resources.Load<GUISkin>("skins/"+skin+"Skin"));
            gameSkin=Instantiate(Resources.Load<GUISkin>("skins/"+skin+"SkinGame"));
            ScaleSkin(mainSkin,profileUnit);ScaleSkin(gameSkin,profileUnit);
            var layout=JsonUtility.FromJson<Layout>(Resources.Load<TextAsset>("Original26/layout-"+family).text);
            referenceHeight=layout.height*profileUnit;
            rectangles.Clear();dimensions.Clear();
            foreach(var e in layout.rectangles)rectangles[e.method]=new Rect(e.rect[0]*profileUnit,e.rect[1]*profileUnit,e.rect[2]*profileUnit,e.rect[3]*profileUnit);
            foreach(var e in layout.dimensions)dimensions[e.method]=e.value*profileUnit;
            fontProfiles=JsonUtility.FromJson<FontProfiles>(Resources.Load<TextAsset>("Original26/fonts-"+family).text);
            foreach(var language in fontProfiles.languages)foreach(var size in language.sizes)size.size=Mathf.RoundToInt(size.size*profileUnit);
            numberStyle=new GUIStyle(gameSkin.customStyles[41]);numberStyle.normal.background=null;numberStyle.active.background=null;
            outlineStyle=new GUIStyle(numberStyle);outlineStyle.normal.textColor=Color.black;
            markerText=new GUIStyle(gameSkin.GetStyle("label_tutor1_1"));
            if(replacing)
            {
                ApplyLanguageFont();possibleRect=rectangles["ScoresGPView$$GetPossibleScoresRect"];
                menuShadow.texture=Texture(DevicePath("shadows"));
                ConfigureSearchArtwork();
            }
        }
        static void ScaleSkin(GUISkin skin,float scale)
        {
            if(Mathf.Approximately(scale,1))return;
            var styles=new System.Collections.Generic.List<GUIStyle>(skin.customStyles);
            styles.AddRange(new[]{skin.box,skin.label,skin.button,skin.toggle,skin.window,skin.horizontalScrollbar,skin.horizontalScrollbarThumb,skin.verticalScrollbar,skin.verticalScrollbarThumb});
            foreach(var style in styles)
            {
                style.fontSize=Mathf.RoundToInt(style.fontSize*scale);style.fixedWidth*=scale;style.fixedHeight*=scale;style.contentOffset*=scale;
                foreach(var offset in new[]{style.border,style.margin,style.padding,style.overflow})
                {offset.left=Mathf.RoundToInt(offset.left*scale);offset.right=Mathf.RoundToInt(offset.right*scale);offset.top=Mathf.RoundToInt(offset.top*scale);offset.bottom=Mathf.RoundToInt(offset.bottom*scale);}
            }
        }
        float Dimension(string name)=>dimensions[name];
        string DevicePath(string kind)
        {
            switch(kind)
            {
                case "settings":return artworkFamily=="ipad2"?"ipad2/main_screen/setting_bgr_ipa":artworkFamily=="iphone4"?"iphone4/main_menu/setting_bgr_iph4":"iphone5/setting_bgr_iph5";
                case "shadows":return artworkFamily=="ipad2"?"ipad2/main_screen/shadows":artworkFamily=="iphone4"?"iphone4/main_menu/shadows":"iphone5/shadows";
                case "board":return artworkFamily=="iphone5"?"iphone5/board_iph5":artworkFamily=="iphone4"?"iphone4/board_whistle":"ipad2/game_over_screen/board_whistle";
                case "tutorial":return artworkFamily=="ipad2"?"ipad2/tutorial/":"iphone4/tutorial/";
                case "popup":return artworkFamily=="ipad2"?"ipad2/popup/":"iphone4/popup/";
                default:throw new ArgumentException(kind);
            }
        }
        string StickerOpen=>artworkFamily=="ipad2"?"GUIStickerAnimation_iPad":artworkFamily=="iphone4"?"GUIStickerAnimation":"GUIStickerAnimation_iph5";
        string StickerClose=>artworkFamily=="ipad2"?"EndSticker_iPad":artworkFamily=="iphone4"?"EndSticker":"EndSticker_iph5";
        void ConfigureSearchArtwork()
        {
            bool tablet=artworkFamily=="ipad2",shortPhone=artworkFamily=="iphone4";
            searchFocus.texture=Texture(tablet?"ipad2/search_target_screen/focusing":"iphone4/search_target/focusing");
            searchSticker.texture=Texture(tablet?"ipad2/search_target_screen/sticker":"iphone4/search_target/sticker");
            searchFocus.transform.position=new Vector3(.5f,tablet ? .5f : shortPhone ? .53f : .52f,0);
            searchFocus.transform.localScale=tablet?new Vector3(.61f,.32f,1):shortPhone?new Vector3(.55f,.26f,1):new Vector3(.55f,.22f,1);
            searchSticker.transform.position=new Vector3(.5f,-.06f,0);
            searchSticker.transform.localScale=tablet?new Vector3(.64f,.27f,1):shortPhone?new Vector3(.71588236f,.29647058f,1):new Vector3(.71588236f,.23f,1);
            if(searchShown)SetSearchScreen(true,true);
        }
    }
}
